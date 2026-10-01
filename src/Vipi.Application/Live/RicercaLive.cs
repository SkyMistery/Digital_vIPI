using System.Collections.Concurrent;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>Uno scalo trovato dalla ricerca della vista live: quanto basta per aprirgli il pannello rapido.</summary>
/// <param name="AccCode">L'ACC del documento: il pannello lo usa per gli indirizzi, e lo scalo cercato può stare in
/// un ACC diverso da quello di chi guarda (sono la NE di Roma e cerco LIPE).</param>
public sealed record LiveAeroportoTrovato(string Icao, string Nome, string AccCode, bool HaVipi, bool HaVsop);

/// <summary>Una postazione del catalogo: callsign, nominativo radio («Roma Radar») e frequenza, se dichiarata.</summary>
public sealed record LivePostazioneTrovata(string Callsign, string? Nominativo, string? Frequenza, string? Icao);

/// <summary>
/// Chi c'è su una postazione adesso. <paramref name="Catena"/> è la copertura verso l'alto (dal padre alla radice);
/// <paramref name="ChiCopre"/> il primo online di quella catena, null = nessuno (UNICOM).
/// <paramref name="FeedScaduto"/>: la fotografia degli online è vecchia, e «nessuno» vuol dire «non si sa».
/// </summary>
public sealed record LiveCopertura(string Callsign, bool Online, IReadOnlyList<string> Catena, string? ChiCopre, bool FeedScaduto);

/// <summary>Una riga di SID o STAR pubblicata, con lo scalo e il suo ACC: per «in quali procedure compare AGNIS».</summary>
public sealed record LiveProceduraTrovata(string Icao, string AccCode, bool Sid, string Nome, string Fix, string Transizione, string Pista);

/// <summary>Un punto di trasferimento: dove, da chi a chi, a che livello e a quale condizione.</summary>
public sealed record LiveTrasferimentoTrovato(string Cop, string Da, string? A, string Livello, string? Condizione, string AccCode,
    IReadOnlyList<string> Scali);

/// <summary>Una radioassistenza dell'anagrafica: codice, natura, frequenza o canale, posizione.</summary>
public sealed record LiveNavaidTrovato(string Codice, string Natura, string? Tipo, string? Frequenza, string? Canale, double? Lat, double? Lon);

/// <summary>
/// La RICERCA RAPIDA della vista live (committente, 1 ottobre 2026): da una postazione che ha scali sotto (APP, ACC)
/// si cerca uno scalo qualunque con vIPI o vSOP pubblicati — e lo si apre come se fosse del proprio settore — o le
/// aree regolamentate, tutte, da vedere su mappa con le informazioni di attivazione.
/// <para>Gli elenchi sono piccoli (qualche centinaio di righe) e si chiedono UNA volta, all'apertura del pannello: il
/// filtro gira poi in memoria a ogni tasto (<see cref="RicercaLiveFiltro"/>), senza una query per lettera.</para>
/// </summary>
public interface IRicercaLive
{
    /// <summary>Gli scali con vIPI civile o vSOP militare PUBBLICI (lo stesso cancello del quadro vAWOS), con il loro ACC.</summary>
    Task<IReadOnlyList<LiveAeroportoTrovato>> AeroportiAsync(CancellationToken ct = default);

    /// <summary>Tutte le aree regolamentate dell'anagrafica.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default);

    /// <summary>Le aree scelte, con poligono e testo di attivazione, nell'ordine degli id.</summary>
    Task<IReadOnlyList<AccSpecialAreaView>> DettagliAreeAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default);

    /// <summary>Tutte le postazioni del catalogo (CTR, APP, TWR, GND, DEL di ogni ACC), col nominativo e la frequenza.</summary>
    Task<IReadOnlyList<LivePostazioneTrovata>> PostazioniAsync(CancellationToken ct = default);

    /// <summary>
    /// La topologia GLOBALE (cross-ACC), per dire chi copre una postazione chiusa (<see cref="RicercaLiveFiltro.Copertura"/>).
    /// Dalla memoria condivisa: un minuto, poi si ricostruisce.
    /// </summary>
    Task<Topology> TopologiaAsync(CancellationToken ct = default);

    /// <summary>
    /// Le SID e STAR pubblicate di tutti gli scali (le stesse righe dei documenti). ⚠️ Costa: una derivazione per scalo
    /// la prima volta (poi c'è la memoria di dieci minuti di <see cref="IProcedureCercabili"/>); il pannello la chiede
    /// in sottofondo, dopo aver già mostrato il resto.
    /// </summary>
    Task<IReadOnlyList<LiveProceduraTrovata>> ProcedureAsync(CancellationToken ct = default);

    /// <summary>
    /// I punti di trasferimento di tutti gli ACC. ⚠️ Nella vista live i trasferimenti sono dello STAFF DI DIVISIONE
    /// (S81): chi chiama questo metodo fuori da lì deve fare lo stesso cancello.
    /// </summary>
    Task<IReadOnlyList<LiveTrasferimentoTrovato>> TrasferimentiAsync(CancellationToken ct = default);

    /// <summary>Le radioassistenze dell'anagrafica (VOR, DME, NDB) con frequenza e posizione.</summary>
    Task<IReadOnlyList<LiveNavaidTrovato>> NavaidAsync(CancellationToken ct = default);
}

/// <summary>
/// 🔴 LA MEMORIA CONDIVISA DELLA RICERCA, per tutto il processo. Gli elenchi della ricerca sono gli stessi per tutti
/// (scali pubblicati, aree, postazioni, radioassistenze, procedure pubblicate, punti di trasferimento): ricaricarli per
/// ogni utente e ogni ricerca voleva dire, con dieci controllori che cercano insieme, dieci giri sugli accordi di tutti gli
/// ACC e dieci letture delle procedure di sessanta scali (review del 1 ottobre 2026, committente: «rischiamo di far
/// esplodere tutto»). Qui ogni elenco ha un CANCELLO: il primo che lo chiede lo carica, chi arriva intanto aspetta e
/// riceve lo stesso risultato, e per <see cref="Durata"/> nessuno torna sul database.
/// <para>Stesso schema della memoria di <see cref="ProcedureCercabili"/> (statica, a scadenza). Il prezzo è un ritardo:
/// una correzione dell'admin si vede nella ricerca entro cinque minuti.</para>
/// </summary>
internal static class MemoriaRicercaLive
{
    internal static readonly TimeSpan Durata = TimeSpan.FromMinutes(5);

    private sealed record Valore(DateTime Quando, object Dati);

    private sealed class Voce
    {
        public readonly SemaphoreSlim Cancello = new(1, 1);
        public volatile Valore? Ultimo;
    }

    private static readonly ConcurrentDictionary<string, Voce> Voci = new(StringComparer.Ordinal);

    public static async Task<T> PrendiAsync<T>(string chiave, TimeSpan durata, Func<Task<T>> carica, CancellationToken ct)
        where T : class
    {
        var voce = Voci.GetOrAdd(chiave, _ => new Voce());
        if (Fresco<T>(voce.Ultimo, durata) is { } pronto) return pronto;
        await voce.Cancello.WaitAsync(ct);
        try
        {
            // Chi ha aspettato al cancello trova quasi sempre il lavoro già fatto da chi c'era prima.
            if (Fresco<T>(voce.Ultimo, durata) is { } intanto) return intanto;
            var dati = await carica();
            voce.Ultimo = new Valore(DateTime.UtcNow, dati);
            return dati;
        }
        finally
        {
            voce.Cancello.Release();
        }
    }

    private static T? Fresco<T>(Valore? v, TimeSpan durata) where T : class =>
        v is not null && DateTime.UtcNow - v.Quando < durata ? v.Dati as T : null;

    /// <summary>Per i test: si riparte da vuoto.</summary>
    internal static void Svuota() => Voci.Clear();
}

internal sealed class RicercaLive(
    IDocumentAdminService documenti, ISpecialAreaRepository aree, IFrequenzeDegliEnti enti, ITopologyProvider topologie,
    IProcedureCercabili procedure, IAgreementService accordi,
    IStructureEditingRepository struttura, INavaidCatalog navaid) : IRicercaLive
{
    public Task<IReadOnlyList<LiveAeroportoTrovato>> AeroportiAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("aeroporti", MemoriaRicercaLive.Durata, () => CaricaAeroportiAsync(ct), ct);

    private async Task<IReadOnlyList<LiveAeroportoTrovato>> CaricaAeroportiAsync(CancellationToken ct)
    {
        var docs = await documenti.ListAsync(ct);
        var trovati = new List<LiveAeroportoTrovato>();
        foreach (var a in Awos.AwosGate.Elenco(docs))
        {
            // L'ACC dal documento dello scalo (civile prima, militare poi): senza, il pannello non saprebbe dove portare.
            var acc = docs
                .Where(m => m.Kind is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil
                            && string.Equals(m.Scope, a.Icao, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(m.AccCode))
                .OrderBy(m => m.Kind == ReleaseTargetType.Airport ? 0 : 1)
                .Select(m => m.AccCode)
                .FirstOrDefault();
            if (acc is null) continue;
            trovati.Add(new LiveAeroportoTrovato(a.Icao, a.Nome, acc, a.HaVipi, a.HaVsop));
        }
        return trovati;
    }

    public Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("aree", MemoriaRicercaLive.Durata, () => aree.ListAllSpecialAreasAsync(ct), ct);

    public async Task<IReadOnlyList<AccSpecialAreaView>> DettagliAreeAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default)
    {
        if (ivaoIds.Count == 0) return Array.Empty<AccSpecialAreaView>();
        var dettagli = await aree.GetSpecialAreasByIdsAsync(ivaoIds, ct);
        return SpecialAreaProjection.Build(dettagli, ivaoIds);
    }

    public Task<IReadOnlyList<LivePostazioneTrovata>> PostazioniAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("postazioni", MemoriaRicercaLive.Durata, () => CaricaPostazioniAsync(ct), ct);

    private async Task<IReadOnlyList<LivePostazioneTrovata>> CaricaPostazioniAsync(CancellationToken ct)
    {
        // Il nominativo da un elenco, la frequenza dall'altro: un ente senza frequenza dichiarata resta cercabile.
        var frequenze = (await enti.TutteAsync(ct))
            .GroupBy(f => f.SectorId)
            .ToDictionary(g => g.Key, g => g.First().FrequencyMhz);
        return (await enti.NominativiAsync(ct))
            .GroupBy(e => e.Callsign, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(e => new LivePostazioneTrovata(e.Callsign, e.AtcCallsign,
                frequenze.TryGetValue(e.SectorId, out var f) ? f : null, e.Icao))
            .ToList();
    }

    // Un minuto e non cinque: la gerarchia la cambia chi lavora sulla Struttura, e chi guarda «chi mi copre» la vuole fresca.
    public Task<Topology> TopologiaAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("topologia", TimeSpan.FromMinutes(1), () => topologie.BuildGlobalAsync(ct), ct);

    public Task<IReadOnlyList<LiveProceduraTrovata>> ProcedureAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("procedure", MemoriaRicercaLive.Durata, () => CaricaProcedureAsync(ct), ct);

    private async Task<IReadOnlyList<LiveProceduraTrovata>> CaricaProcedureAsync(CancellationToken ct)
    {
        var righe = new List<LiveProceduraTrovata>();
        foreach (var a in await AeroportiAsync(ct))
        {
            // L'edizione da cui si legge: la civile se c'è, altrimenti il vSOP militare (la stessa regola del pannello).
            var edizione = a.HaVipi ? ReleaseTargetType.Airport : ReleaseTargetType.AirportMil;
            var p = await procedure.PerScaloAsync(a.Icao, edizione, ct);
            foreach (var s in p.Sids) righe.Add(new(a.Icao, a.AccCode, true, s.Name, s.Fix, s.Transition, s.Runway));
            foreach (var s in p.Stars) righe.Add(new(a.Icao, a.AccCode, false, s.Name, s.Fix, s.Transition, s.Runway));
        }
        return righe;
    }

    public Task<IReadOnlyList<LiveTrasferimentoTrovato>> TrasferimentiAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("trasferimenti", MemoriaRicercaLive.Durata, () => CaricaTrasferimentiAsync(ct), ct);

    private async Task<IReadOnlyList<LiveTrasferimentoTrovato>> CaricaTrasferimentiAsync(CancellationToken ct)
    {
        var righe = new List<LiveTrasferimentoTrovato>();
        var visti = new HashSet<int>();
        foreach (var acc in await struttura.ListAccsAsync(ct))
        {
            // ⚠️ Un accordo fra due ACC esce dall'elenco di tutti e due: si tiene una volta sola, per Id del flusso.
            foreach (var flusso in await accordi.ListFlowsByAccAsync(acc.Code, ct))
            {
                if (!visti.Add(flusso.Id)) continue;
                var scali = flusso.AirportIcaos.Count > 0 ? flusso.AirportIcaos
                    : flusso.AirportIcao is { } un ? new[] { un } : Array.Empty<string>();
                foreach (var p in flusso.Points)
                    righe.Add(new(p.Cop, flusso.OwningSectorCallsign, p.NextSectorCallsign, p.LevelText, p.ConditionDisplay,
                        flusso.AccCode, scali));
            }
        }
        // ⚠️ Due flussi possono portare la STESSA riga (lo stesso accordo espanso per gruppo, visto su ASPIR il 1 ottobre
        // 2026): a chi cerca serve una volta. `Scali` è una lista, quindi la chiave la si scrive a mano.
        return righe
            .GroupBy(r => (r.Cop, r.Da, r.A, r.Livello, r.Condizione, string.Join(" ", r.Scali)))
            .Select(g => g.First())
            .ToList();
    }

    public Task<IReadOnlyList<LiveNavaidTrovato>> NavaidAsync(CancellationToken ct = default) =>
        MemoriaRicercaLive.PrendiAsync("navaid", MemoriaRicercaLive.Durata, () => CaricaNavaidAsync(ct), ct);

    private async Task<IReadOnlyList<LiveNavaidTrovato>> CaricaNavaidAsync(CancellationToken ct) =>
        (await navaid.ListAsync(ct))
            .Select(n => new LiveNavaidTrovato(n.Code, n.Kind, n.Type, n.Frequency, n.Channel, n.Latitude, n.Longitude))
            .ToList();
}

/// <summary>
/// Il confronto della ricerca live, puro e provato da solo. Si confronta il testo RIPULITO — maiuscole, senza spazi né
/// trattini né punti — perché le aree si scrivono in dieci modi: «LI R14A - S.Severa», «LI-R14», «R14», «r 14 a»
/// devono trovare la stessa riga.
/// </summary>
public static class RicercaLiveFiltro
{
    /// <summary>Sotto questa lunghezza non si cerca: una lettera sola troverebbe mezza Italia.</summary>
    public const int MinimoCaratteri = 2;

    /// <summary>Quante aree al massimo si elencano: oltre, si chiede di restringere.</summary>
    public const int MassimoAree = 60;

    public static string Pulito(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var b = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) if (char.IsLetterOrDigit(c)) b.Append(char.ToUpperInvariant(c));
        return b.ToString();
    }

    /// <summary>Gli scali: prima l'ICAO esatto, poi quelli che cominciano così, poi il nome che lo contiene.</summary>
    public static IReadOnlyList<LiveAeroportoTrovato> Aeroporti(IEnumerable<LiveAeroportoTrovato> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length < MinimoCaratteri || SoloFrequenza(q)) return Array.Empty<LiveAeroportoTrovato>();
        return elenco
            .Select(a => (a, peso: a.Icao.Equals(p, StringComparison.OrdinalIgnoreCase) ? 0
                                 : a.Icao.StartsWith(p, StringComparison.OrdinalIgnoreCase) ? 1
                                 : Pulito(a.Nome).Contains(p, StringComparison.Ordinal) ? 2 : -1))
            .Where(x => x.peso >= 0)
            .OrderBy(x => x.peso).ThenBy(x => x.a.Icao, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.a)
            .ToList();
    }

    /// <summary>Quante righe al massimo per gruppo di risultati (postazioni, punti, trasferimenti, radioassistenze).</summary>
    public const int MassimoRighe = 30;

    /// <summary>Il testo può essere una frequenza? Cifre (e un punto), almeno tre: «128.7», «1287», «118.105».</summary>
    public static bool SembraFrequenza(string? q)
    {
        var t = (q ?? "").Trim();
        return t.Length >= 3 && t.All(c => char.IsDigit(c) || c is '.' or ',') && t.Count(char.IsDigit) >= 3;
    }

    /// <summary>
    /// Il testo È una frequenza, e nient'altro: cifre COL punto («128.7»). Solo allora scali, aree, punti e trasferimenti
    /// tacciono. Le sole cifre («120») possono essere una frequenza E un nome — «LI D120» — e cercano tutte e due le cose
    /// (review del 1 ottobre 2026: prima «120» non trovava l'area D120).
    /// </summary>
    public static bool SoloFrequenza(string? q) => SembraFrequenza(q) && (q ?? "").IndexOfAny(new[] { '.', ',' }) >= 0;

    /// <summary>
    /// Chi c'è su una postazione adesso, dalla topologia globale e dagli online: se è chiusa, il primo online risalendo la
    /// catena di copertura; nessuno = UNICOM. Pura: la pagina la rifà a ogni giro del feed, e il dettaglio non invecchia.
    /// </summary>
    public static LiveCopertura Copertura(Topology topologia, string callsign, IReadOnlySet<string> online, bool feedScaduto)
    {
        var catena = LiveStationParts.CoverageChain(topologia, callsign);
        var chi = catena.FirstOrDefault(online.Contains);
        return new LiveCopertura(callsign, online.Contains(callsign), catena, chi, feedScaduto);
    }

    /// <summary>
    /// Le postazioni: per FREQUENZA (le cifre senza punto, dall'inizio: «128.7» trova 128.705) se il testo sembra una
    /// frequenza; altrimenti per callsign o nominativo che lo contengono. Prima chi comincia così, poi gli altri.
    /// </summary>
    public static IReadOnlyList<LivePostazioneTrovata> Postazioni(IEnumerable<LivePostazioneTrovata> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length < MinimoCaratteri) return Array.Empty<LivePostazioneTrovata>();
        if (SoloFrequenza(q))
            return elenco.Where(x => Pulito(x.Frequenza).StartsWith(p, StringComparison.Ordinal))
                .OrderBy(x => x.Frequenza, StringComparer.Ordinal).ThenBy(x => x.Callsign, StringComparer.OrdinalIgnoreCase)
                .ToList();
        if (SembraFrequenza(q))
            return elenco.Where(x => Pulito(x.Frequenza).StartsWith(p, StringComparison.Ordinal)
                                     || Pulito(x.Callsign).Contains(p, StringComparison.Ordinal))
                .OrderBy(x => x.Frequenza, StringComparer.Ordinal).ThenBy(x => x.Callsign, StringComparer.OrdinalIgnoreCase)
                .ToList();
        return elenco
            .Select(x => (x, peso: Pulito(x.Callsign).StartsWith(p, StringComparison.Ordinal) ? 0
                                 : Pulito(x.Callsign).Contains(p, StringComparison.Ordinal) ? 1
                                 : Pulito(x.Nominativo).Contains(p, StringComparison.Ordinal) ? 2 : -1))
            .Where(t => t.peso >= 0)
            .OrderBy(t => t.peso).ThenBy(t => t.x.Callsign, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.x)
            .ToList();
    }

    /// <summary>Le procedure in cui compare un PUNTO (fix iniziale/finale o transition), dall'inizio del nome, da tre lettere.</summary>
    public static IReadOnlyList<LiveProceduraTrovata> Punti(IEnumerable<LiveProceduraTrovata> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length < 3 || SoloFrequenza(q)) return Array.Empty<LiveProceduraTrovata>();
        return elenco
            .Where(x => Pulito(x.Fix).StartsWith(p, StringComparison.Ordinal) || Pulito(x.Transizione).StartsWith(p, StringComparison.Ordinal))
            .OrderBy(x => x.Fix, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Icao).ThenBy(x => x.Sid ? 0 : 1)
            .ThenBy(x => x.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>I punti di trasferimento col nome che comincia così, da tre lettere.</summary>
    public static IReadOnlyList<LiveTrasferimentoTrovato> Trasferimenti(IEnumerable<LiveTrasferimentoTrovato> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length < 3 || SoloFrequenza(q)) return Array.Empty<LiveTrasferimentoTrovato>();
        return elenco
            .Where(x => Pulito(x.Cop).StartsWith(p, StringComparison.Ordinal))
            .OrderBy(x => x.Cop, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Da, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Le radioassistenze: per codice dall'inizio («PES»), o per frequenza se il testo sembra una frequenza.</summary>
    public static IReadOnlyList<LiveNavaidTrovato> Navaid(IEnumerable<LiveNavaidTrovato> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length < MinimoCaratteri) return Array.Empty<LiveNavaidTrovato>();
        var freq = SembraFrequenza(q);
        var solo = SoloFrequenza(q);
        return elenco
            .Where(x => (freq && Pulito(x.Frequenza).StartsWith(p, StringComparison.Ordinal))
                        || (!solo && Pulito(x.Codice).StartsWith(p, StringComparison.Ordinal)))
            .OrderBy(x => x.Codice, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Le aree: il nome ripulito che contiene il testo ripulito, oppure il TIPO uguale al testo («TRA», «D»: tutte
    /// quelle di quel tipo). Ordinate per nome.
    /// </summary>
    public static IReadOnlyList<SpecialAreaPick> Aree(IEnumerable<SpecialAreaPick> elenco, string? q)
    {
        var p = Pulito(q);
        if (p.Length == 0 || SoloFrequenza(q)) return Array.Empty<SpecialAreaPick>();
        return elenco
            .Where(a => (p.Length >= MinimoCaratteri && Pulito(a.Name).Contains(p, StringComparison.Ordinal))
                        || string.Equals(Pulito(a.Type), p, StringComparison.Ordinal))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
