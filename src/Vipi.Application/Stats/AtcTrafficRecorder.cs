using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>
/// Il giro dell'attribuzione: prende una fotografia della rete, decide di chi è ogni aereo e tiene aggiornate
/// le tratte. Vive quanto l'applicazione (il registro in memoria è suo) e lo chiama il solo poller.
///
/// <para>Ordine delle cose, a ogni giro:</para>
/// <list type="number">
///   <item>i volumi rivendicati da chi è in frequenza (<see cref="SectorVolumeMap"/>, copertura top-down);</item>
///   <item>ogni pilota va a <b>una</b> sessione sola (<see cref="TrafficAttribution"/>);</item>
///   <item>il registro aggiorna le tratte, e si scrive solo quando serve (tratta nuova o checkpoint).</item>
/// </list>
/// </summary>
public sealed class AtcTrafficRecorder
{
    private readonly ISectorVolumeCatalog _catalogo;
    private readonly TrafficLedger _registro = new();

    /// <summary>A chi era attribuito ogni pilota nel giro precedente: serve a riconoscere le consegne.</summary>
    private readonly Dictionary<string, (long SessionId, DateTimeOffset When)> _giroPrecedente =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<SectorVolumeRow>? _settori;
    private DateTimeOffset _settoriLetti = DateTimeOffset.MinValue;

    /// <summary>Ogni quanto rileggere i cataloghi: cambiano coi giri di import, cioè una volta al giorno.</summary>
    public static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(1);

    /// <summary>Ogni quanto salvare le tratte che stanno solo cambiando minuti e ultimo avvistamento.</summary>
    public static readonly TimeSpan Checkpoint = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Quanto può distare l'avvistamento precedente perché un cambio di sessione sia una <b>consegna</b>.
    ///
    /// <para>⚠️ La finestra non è un margine di comodo: il poll gira ogni 60 secondi, e senza limite un
    /// poller fermo un'ora scriverebbe «consegnato a…» ogni volta che, tornato su, un aeroplano si trova
    /// sotto un altro controllore. Due giri e mezzo di tolleranza coprono un giro perso; oltre, il passaggio
    /// non l'abbiamo visto e non si scrive.</para>
    /// </summary>
    public static readonly TimeSpan HandoffWindow = TimeSpan.FromMinutes(2.5);

    public AtcTrafficRecorder(ISectorVolumeCatalog catalogo) => _catalogo = catalogo;

    /// <summary>Esito di un giro, per il log e per i test.</summary>
    public sealed record Result(int Attributed, int NewLegs, int WrittenLegs, int Sessions);

    public async Task<Result> RecordAsync(NetworkSnapshot snapshot, IAtcTrafficStore store, CancellationToken ct = default)
    {
        var online = new HashSet<string>(snapshot.Atc.Select(a => a.Callsign), StringComparer.OrdinalIgnoreCase);
        var perCallsign = snapshot.Atc.ToDictionary(a => a.Callsign, a => a.SessionId, StringComparer.OrdinalIgnoreCase);

        // ⚠️ PRIMA di ogni altra cosa: chi non è più in frequenza va salvato e liberato. Stava in fondo, e
        // bastava che gli unici online fossero settori senza poligono — o che non ci fosse nessuno — perché
        // il giro uscisse prima, lasciando in memoria l'ultimo tratto di chi aveva appena staccato.
        await ChiudiSparitAsync(perCallsign.Values.ToHashSet(), snapshot.AsOf, store, ct);

        if (online.Count == 0) return new Result(0, 0, 0, 0);

        var settori = await SettoriAsync(snapshot.AsOf, ct);
        var claims = SectorVolumeMap.BuildClaims(settori, online);
        if (claims.Count == 0) return new Result(0, 0, 0, 0);

        // Dopo un riavvio il registro è vuoto ma l'archivio no: si rilegge solo per le sessioni che non
        // conosce, non a ogni giro.
        await IdrataAsync(snapshot, store, ct);
        var conTraffico = new HashSet<long>();
        var subito = new HashSet<long>();
        var attribuiti = 0;
        var nuove = 0;

        var visti = new Dictionary<string, (long, DateTimeOffset)>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in snapshot.Pilots)
        {
            var fase = FlightPhases.Of(p.OnGround, p.GroundSpeed, p.State, p.DepartureDistanceNm);
            var sessione = TrafficAttribution.Attribute(claims, p.Latitude, p.Longitude, p.AltitudeFt, fase);
            if (sessione is null || !perCallsign.TryGetValue(sessione, out var sessionId)) continue;

            attribuiti++;
            conTraffico.Add(sessionId);

            var obs = new LegObservation(
                p.Callsign, p.UserId, p.FlightPlanId, p.DepIcao, p.ArrIcao, p.AircraftIcao, fase, p.AltitudeFt);

            if (_registro.Observe(sessionId, obs, snapshot.AsOf))
            {
                nuove++;
                subito.Add(sessionId);   // un aereo nuovo si scrive senza aspettare il checkpoint
            }

            // La consegna: al giro scorso era di un altro, e il giro scorso è appena stato. L'ordine conta —
            // `Observe` deve aver già aperto la tratta nella sessione che riceve, o non ci sarebbe niente
            // su cui scrivere «ricevuto da».
            if (_giroPrecedente.TryGetValue(p.Callsign, out var prima)
                && prima.SessionId != sessionId
                && snapshot.AsOf - prima.When <= HandoffWindow)
            {
                _registro.NoteHandoff(prima.SessionId, sessionId, p.Callsign);
                subito.Add(sessionId);
                subito.Add(prima.SessionId);
            }

            visti[p.Callsign] = (sessionId, snapshot.AsOf);
        }

        // Il registro dei piloti visti si riscrive a ogni giro invece di accumularsi: le voci che restano
        // fuori sono quelle uscite da ogni area, e tenerle vorrebbe dire far crescere un dizionario per
        // sempre — con dentro consegne che, passata la finestra, non varrebbero comunque più.
        _giroPrecedente.Clear();
        foreach (var (k, v) in visti) _giroPrecedente[k] = v;

        foreach (var id in perCallsign.Values)
            _registro.EndPoll(id, conTraffico.Contains(id));

        var flush = _registro.Take(snapshot.AsOf, Checkpoint, subito);
        var scritte = flush.Nothing ? 0 : await store.SaveAsync(flush, ct);

        return new Result(attribuiti, nuove, scritte, perCallsign.Count);
    }

    /// <summary>Salva tutto quel che è rimasto in memoria: allo spegnimento, per non perdere l'ultimo tratto.</summary>
    public async Task FlushAsync(IAtcTrafficStore store, DateTimeOffset now, CancellationToken ct = default)
    {
        var flush = _registro.TakeAll(now);
        if (!flush.Nothing) await store.SaveAsync(flush, ct);
    }

    private async Task<IReadOnlyList<SectorVolumeRow>> SettoriAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_settori is not null && now - _settoriLetti < CatalogTtl) return _settori;
        _settori = await _catalogo.GetAllAsync(ct);
        _settoriLetti = now;
        return _settori;
    }

    private async Task IdrataAsync(NetworkSnapshot snapshot, IAtcTrafficStore store, CancellationToken ct)
    {
        var ignote = snapshot.Atc.Select(a => a.SessionId).Where(id => !_registro.Knows(id)).ToList();
        if (ignote.Count == 0) return;

        var vecchie = await store.GetLegsAsync(ignote, ct);
        foreach (var id in ignote)
            _registro.Hydrate(id,
                vecchie.TryGetValue(id, out var v) ? v.Legs : Array.Empty<TrafficLegRow>(),
                vecchie.TryGetValue(id, out var w) ? w.TrafficMinutes : 0);
    }

    private async Task ChiudiSparitAsync(
        IReadOnlySet<long> ancoraOnline, DateTimeOffset now, IAtcTrafficStore store, CancellationToken ct)
    {
        var sparite = _registro.Sessions.Where(id => !ancoraOnline.Contains(id)).ToList();
        if (sparite.Count == 0) return;

        var flush = _registro.TakeOnly(sparite.ToHashSet(), now);
        if (!flush.Nothing) await store.SaveAsync(flush, ct);
        foreach (var id in sparite) _registro.Forget(id);
    }
}
