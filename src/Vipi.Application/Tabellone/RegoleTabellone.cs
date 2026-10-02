using System.Globalization;
using System.Text;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Tabellone;

/// <summary>Quello che serve per calcolare un tabellone: le fonti come sono adesso, già lette.</summary>
/// <param name="Booking">L'ultima lettura BUONA del booking (null = mai letta). Il booking cambia di rado: se la
/// fonte tace si continua a usare quella, con <c>fonti.booking.ok</c> a false.</param>
/// <param name="BookingLetto">Quando è stata fatta quella lettura.</param>
/// <param name="Board">La voce <c>board</c> del documento del ponte RFO (<c>"arr:AZA1" → "509"</c>), o null.</param>
/// <param name="BoardAt">La voce <c>boardAt</c> dello stesso documento.</param>
/// <param name="EventoRfo">Se lo scalo ha un evento RFO configurato: senza, il Gate Manager non c'è per definizione.</param>
/// <param name="Anagrafica">Città e IATA di un altro scalo, dall'anagrafica di vIPI; null se non si sa.</param>
public sealed record IngressiTabellone(
    ScaloTabellone Scalo,
    string? NomeEvento,
    string Voli,
    LetturaBooking? Booking,
    DateTimeOffset? BookingLetto,
    FotografiaPiloti.Istantanea Piloti,
    IReadOnlyDictionary<string, string>? Board,
    DateTimeOffset? BoardAt,
    bool EventoRfo,
    Func<string, (string? Citta, string? Iata)?> Anagrafica);

/// <summary>
/// Le regole del tabellone partenze/arrivi: fusione di booking e Whazzup, stati, stand effettivo, finestra, uscite e
/// ordine. Pura: niente rete né database, l'orologio arriva da fuori. Le provano i test su risposte salvate.
/// Formato concordato: <c>Dep_arr_board/FORMATO-DATI.md</c> (2 ottobre 2026); carta
/// <c>docs/feature/2026-10-02-tabellone-partenze-arrivi.md</c>.
///
/// <para>⚠️ <b>Ha memoria</b> (<see cref="MemoriaTabellone"/>): «sparisce 10' dopo DEPARTED» vuol dire sapere
/// QUANDO è diventato DEPARTED, e una fotografia sola non lo dice. La memoria vive nel processo: dopo un riavvio
/// una partenza già decollata resta in lista altri dieci minuti, non di più.</para>
/// </summary>
public static class RegoleTabellone
{
    public const int Versione = 1;

    public const string VoliTutti = "tutti";
    public const string VoliPrenotati = "prenotati";

    // Gli stati, già nella scritta che mostra il tabellone (§4: inglese, deciso dal committente).
    public const string Scheduled = "SCHEDULED";
    public const string OnTime = "ON TIME";
    public const string Delayed = "DELAYED";
    public const string Boarding = "BOARDING";
    public const string Departed = "DEPARTED";
    public const string Airborne = "AIRBORNE";
    public const string Approaching = "APPROACHING";
    public const string Landed = "LANDED";

    /// <summary>Oltre questo ritardo della stima sul programmato il volo è DELAYED.</summary>
    public static readonly TimeSpan SogliaRitardo = TimeSpan.FromMinutes(15);

    /// <summary>Una fonte ferma da più di così non vale (§2): Whazzup e booking.</summary>
    public static readonly TimeSpan FonteFerma = TimeSpan.FromMinutes(3);

    /// <summary>Un <c>boardAt</c> più vecchio di così vuol dire nessuna postazione che dà la clearance (§6).</summary>
    public static readonly TimeSpan BoardVecchio = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan FinestraPrima = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan FinestraDopo = TimeSpan.FromHours(6);
    public static readonly TimeSpan UscitaPartenza = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan UscitaArrivo = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan UscitaMaiOnline = TimeSpan.FromMinutes(60);

    /// <summary>Il tempo aggiunto alla stima d'arrivo per le ultime miglia e il rullaggio (§7).</summary>
    public static readonly TimeSpan CodaArrivo = TimeSpan.FromMinutes(6);

    public const int RigheMassime = 40;

    /// <summary>Entro questa distanza dalla partenza un «On Blocks» è un aereo che deve ancora partire.</summary>
    internal const double VicinoAllaPartenzaNm = 10;

    private enum Verso { Partenza, Arrivo }

    public static RispostaTabellone Calcola(IngressiTabellone dati, MemoriaTabellone memoria, DateTimeOffset adesso)
    {
        var icao = dati.Scalo.Icao;

        // Whazzup: dopo tre minuti senza una lettura i dati non si usano più, o restano voli «online» che non ci sono.
        var pilotiOk = dati.Piloti.AsOf != DateTimeOffset.MinValue && adesso - dati.Piloti.AsOf <= FonteFerma;
        var piloti = pilotiOk ? dati.Piloti.Piloti : Array.Empty<SourcePilotFix>();
        var asOf = pilotiOk ? dati.Piloti.AsOf : adesso;

        var bookingOk = dati.Booking is { Ok: true } && dati.BookingLetto is { } bl && adesso - bl <= FonteFerma;
        var prenotati = (dati.Booking?.Voli ?? Array.Empty<VoloPrenotato>())
            .Where(v => Uguale(v.Origine, icao) || Uguale(v.Destinazione, icao))
            .ToList();

        var boardFresco = dati.EventoRfo && dati.Board is not null && dati.BoardAt is { } ba && adesso - ba <= BoardVecchio;
        var board = boardFresco ? dati.Board : null;

        var evento = prenotati.Count == 0 ? null : new EventoTabellone(
            Palette(string.IsNullOrWhiteSpace(dati.NomeEvento) ? "EVENT" : dati.NomeEvento, 32),
            Attivo(prenotati, icao, adesso));

        // Fuori dagli eventi si usa sempre «tutti»: il booking non c'è (§5).
        var voli = evento is not null && string.Equals(dati.Voli?.Trim(), VoliPrenotati, StringComparison.OrdinalIgnoreCase)
            ? VoliPrenotati : VoliTutti;

        var partenze = Lista(Verso.Partenza, icao, prenotati, piloti, voli, board, dati.Anagrafica, memoria, adesso, asOf);
        var arrivi = Lista(Verso.Arrivo, icao, prenotati, piloti, voli, board, dati.Anagrafica, memoria, adesso, asOf);
        memoria.Pota(adesso);

        return new RispostaTabellone(
            Versione,
            dati.Scalo,
            Secondi(adesso),
            evento,
            voli,
            new FontiTabellone(
                new StatoFonte(bookingOk, dati.BookingLetto is { } l ? Secondi(l) : null),
                new StatoFonte(pilotiOk, dati.Piloti.AsOf == DateTimeOffset.MinValue ? null : Secondi(dati.Piloti.AsOf)),
                new StatoFonte(boardFresco, dati.EventoRfo && dati.BoardAt is { } b ? Secondi(b) : null)),
            partenze,
            arrivi);
    }

    /// <summary>
    /// L'evento è attivo da due ore prima del primo volo prenotato a un'ora dopo l'ultimo. Il booking restituisce
    /// l'evento «in corso», che può essere anche quello di domani: lo dice questo campo.
    /// </summary>
    private static bool Attivo(List<VoloPrenotato> prenotati, string icao, DateTimeOffset adesso)
    {
        var orari = prenotati.Select(v => Programmato(v, Uguale(v.Origine, icao) ? Verso.Partenza : Verso.Arrivo))
            .OfType<DateTimeOffset>().ToList();
        return orari.Count > 0 && adesso >= orari.Min() - TimeSpan.FromHours(2) && adesso <= orari.Max() + TimeSpan.FromHours(1);
    }

    private sealed class Candidato
    {
        public required string Id { get; init; }
        public required string Callsign { get; init; }
        public VoloPrenotato? Prenotazione { get; init; }
        public SourcePilotFix? Pilota { get; set; }
        public DateTimeOffset? Programmato { get; init; }
    }

    private static List<RigaTabellone> Lista(Verso verso, string icao, List<VoloPrenotato> prenotati,
        IReadOnlyList<SourcePilotFix> piloti, string voli, IReadOnlyDictionary<string, string>? board,
        Func<string, (string? Citta, string? Iata)?> anagrafica, MemoriaTabellone memoria,
        DateTimeOffset adesso, DateTimeOffset asOf)
    {
        // 1. I prenotati di questo verso. Lo stesso callsign due volte nella giornata: AZA1, AZA1#2, in ordine d'orario.
        var candidati = new List<Candidato>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in prenotati
                     .Where(v => verso == Verso.Partenza ? Uguale(v.Origine, icao) : Uguale(v.Destinazione, icao))
                     .OrderBy(v => Programmato(v, verso) ?? DateTimeOffset.MaxValue)
                     .ThenBy(v => v.Callsign, StringComparer.Ordinal))
            candidati.Add(new Candidato
            {
                Id = IdNuovo(v.Callsign, ids), Callsign = v.Callsign, Prenotazione = v, Programmato = Programmato(v, verso),
            });

        // 2. I piloti online col piano da o per lo scalo. Lo stesso callsign nelle due fonti è UNA riga (§5).
        foreach (var p in piloti)
        {
            var codice = p.Callsign.Trim().ToUpperInvariant();
            if (codice.Length == 0) continue;
            if (!Uguale(verso == Verso.Partenza ? p.DepIcao : p.ArrIcao, icao)) continue;

            var programmatoPiano = ProgrammatoDalPiano(p, verso);
            var riferimento = programmatoPiano ?? asOf;
            var prenotazione = candidati
                .Where(c => c.Prenotazione is not null && c.Pilota is null && string.Equals(c.Callsign, codice, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Programmato is { } t ? Math.Abs((t - riferimento).TotalMinutes) : double.MaxValue)
                .FirstOrDefault();
            if (prenotazione is not null)
            {
                prenotazione.Pilota = p;
                continue;
            }
            // Con «prenotati» i voli solo Whazzup restano fuori (§5).
            if (voli == VoliPrenotati) continue;
            // Lo stesso callsign online due volte (riconnessione a cavallo della fotografia): basta la prima.
            if (candidati.Any(c => c.Prenotazione is null && string.Equals(c.Callsign, codice, StringComparison.OrdinalIgnoreCase))) continue;
            candidati.Add(new Candidato { Id = IdNuovo(codice, ids), Callsign = codice, Pilota = p, Programmato = programmatoPiano });
        }

        // 3. Le righe, con le uscite e la finestra.
        var righe = new List<RigaTabellone>();
        foreach (var c in candidati)
            if (Riga(verso, c, board, anagrafica, memoria, adesso, asOf) is { } r) righe.Add(r);

        // Ordine per programmato, a parità per callsign: una stima nuova non fa saltare le righe (§5).
        return righe
            .OrderBy(r => r.Programmato ?? DateTimeOffset.MaxValue)
            .ThenBy(r => r.Volo, StringComparer.Ordinal)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .Take(RigheMassime)
            .ToList();
    }

    private static RigaTabellone? Riga(Verso verso, Candidato c, IReadOnlyDictionary<string, string>? board,
        Func<string, (string? Citta, string? Iata)?> anagrafica, MemoriaTabellone memoria,
        DateTimeOffset adesso, DateTimeOffset asOf)
    {
        var p = c.Pilota;
        var online = p is not null;
        var traccia = memoria.Traccia((verso == Verso.Partenza ? "dep:" : "arr:") + c.Id, adesso);

        string stato;
        DateTimeOffset? stimato = null;
        if (verso == Verso.Partenza)
        {
            // Dalla partenza effettiva del piano, se c'è: un volo decollato due ore fa, visto per la prima volta adesso
            // (sito appena riavviato), non deve restare in lista altri dieci minuti. Visto dal vivo il 2 ottobre 2026.
            if (p is not null && Partito(p))
                traccia.Partito ??= OrarioDalPiano(p.Plan?.ActualDepartureTimeSec, p.Plan?.CreatedAt, asOf) is { } eff && eff <= asOf
                    ? eff : asOf;
            if (traccia.Partito is { } partito)
            {
                stato = Departed;
                stimato = p is not null ? OrarioDalPiano(p.Plan?.ActualDepartureTimeSec, p.Plan?.CreatedAt, asOf) ?? partito : partito;
            }
            else if (p is not null)
            {
                stimato = OrarioDalPiano(p.Plan?.DepartureTimeSec, p.Plan?.CreatedAt, asOf) is { } piano
                    ? (piano > asOf ? piano : asOf) : null;
                stato = AlParcheggio(p) ? Boarding : InOrarioORitardo(c.Programmato, stimato);
            }
            else stato = Scheduled;
        }
        else
        {
            if (p is not null && Atterrato(p)) traccia.Atterrato ??= asOf;
            if (traccia.Atterrato is { } atterrato)
            {
                stato = Landed;
                stimato = atterrato;
            }
            else if (p is not null)
            {
                stimato = StimaArrivo(p, asOf);
                stato = (p.State ?? "").Trim() switch
                {
                    "Approaching" => Approaching,
                    "En Route" or "Initial Climb" => Airborne,
                    _ when !p.OnGround => Airborne,
                    _ => InOrarioORitardo(c.Programmato, stimato),
                };
            }
            else stato = Scheduled;
        }
        if (stimato is { } s) traccia.UltimaStima = s;

        // Uscite (§5).
        if (traccia.Partito is { } dp && adesso - dp > UscitaPartenza) return null;
        if (traccia.Atterrato is { } la && adesso - la > UscitaArrivo) return null;
        var concluso = traccia.Partito is not null || traccia.Atterrato is not null;
        if (!online && !concluso && (c.Programmato is not { } prog || adesso - prog > UscitaMaiOnline)) return null;

        // Finestra: da meno 30' a più 6 ore sull'orario stimato (o sul programmato). Il «meno 30'» vale per chi è
        // online e non ha ancora concluso: per gli altri decidono le uscite qui sopra, che sono più lunghe.
        var riferimento = stimato ?? c.Programmato ?? adesso;
        if (riferimento > adesso + FinestraDopo) return null;
        if (online && !concluso && riferimento < adesso - FinestraPrima) return null;

        // L'altro scalo.
        var b = c.Prenotazione;
        var altroIcao = (verso == Verso.Partenza ? b?.Destinazione ?? p?.ArrIcao : b?.Origine ?? p?.DepIcao) ?? "";
        var cittaBooking = verso == Verso.Partenza ? b?.CittaDestinazione : b?.CittaOrigine;
        var iataBooking = verso == Verso.Partenza ? b?.IataDestinazione : b?.IataOrigine;
        var anag = altroIcao.Length > 0 ? anagrafica(altroIcao.ToUpperInvariant()) : null;
        var citta = Palette(cittaBooking ?? anag?.Citta ?? altroIcao, 16);
        var iata = Iata(iataBooking ?? anag?.Iata);

        // Lo stand effettivo (§6): quello del Gate Manager se boardAt è fresco, altrimenti il prenotato.
        var prenotato = Palette(b?.Gate, 4) is { Length: > 0 } g ? g : null;
        string? gm = null;
        if (board is not null
            && board.TryGetValue((verso == Verso.Partenza ? "dep:" : "arr:") + c.Callsign, out var daBoard)
            && Palette(daBoard, 4) is { Length: > 0 } g2)
            gm = g2;
        // GATE CHANGE solo se c'era uno stand prenotato da cambiare: a chi aveva «TBD» il Gate Manager lo DÀ.
        var cambiato = gm is not null && prenotato is not null && !string.Equals(gm, prenotato, StringComparison.Ordinal);

        var volo = Palette(c.Callsign, 7);
        return new RigaTabellone(
            Id: c.Id,
            Volo: volo,
            Compagnia: Compagnia(c.Callsign),
            Scalo: new AltroScalo(Palette(altroIcao, 4), iata, citta),
            Aereo: Palette(b?.Aereo ?? p?.AircraftIcao, 4) is { Length: > 0 } a ? a : null,
            Programmato: c.Programmato is { } pr ? Secondi(pr) : null,
            Stimato: stimato is { } st ? Secondi(st) : null,
            Gate: gm ?? prenotato,
            GateCambiato: cambiato,
            Stato: stato,
            Online: online,
            Prenotato: b is not null);
    }

    // ---------------------------------------------------------------- stati

    /// <summary>Decollato o in rullaggio per decollare (§4: <c>Departing</c> o <c>Initial Climb</c>, e ciò che viene dopo).</summary>
    internal static bool Partito(SourcePilotFix p) => (p.State ?? "").Trim() switch
    {
        "Departing" or "Initial Climb" or "En Route" or "Approaching" or "Landed" => true,
        // «On Blocks» lontano dalla partenza è un aereo arrivato altrove, quindi partito da qui.
        "On Blocks" => p.DepartureDistanceNm is > VicinoAllaPartenzaNm,
        "Boarding" => false,
        _ => !p.OnGround,
    };

    /// <summary>Fermo al parcheggio prima di partire: <c>Boarding</c>, o «On Blocks» ancora vicino alla partenza.</summary>
    private static bool AlParcheggio(SourcePilotFix p) => (p.State ?? "").Trim() switch
    {
        "Boarding" => true,
        "On Blocks" => p.DepartureDistanceNm is null or <= VicinoAllaPartenzaNm,
        _ => false,
    };

    /// <summary>
    /// <c>Landed</c> o <c>On Blocks</c> (§4). ⚠️ Ma «On Blocks» è anche chi si è appena connesso al parcheggio della
    /// PARTENZA: lo smaschera la distanza dalla partenza (misurato il 24 agosto 2026: 453 NM = è arrivato).
    /// </summary>
    internal static bool Atterrato(SourcePilotFix p) => (p.State ?? "").Trim() switch
    {
        "Landed" => true,
        "On Blocks" => p.DepartureDistanceNm is > VicinoAllaPartenzaNm
                       || (p.DepartureDistanceNm is null && p.Plan?.ArrivalDistanceNm is <= VicinoAllaPartenzaNm),
        _ => false,
    };

    private static string InOrarioORitardo(DateTimeOffset? programmato, DateTimeOffset? stimato) =>
        programmato is { } p && stimato is { } s && s - p > SogliaRitardo ? Delayed : OnTime;

    // ---------------------------------------------------------------- orari

    private static DateTimeOffset? Programmato(VoloPrenotato v, Verso verso) =>
        // Chi si prenota da solo ha solo l'eobt, che è l'orario allo scalo dell'evento anche per un arrivo (§7).
        verso == Verso.Partenza ? v.Eobt : v.Eat ?? v.Eobt;

    /// <summary>Volo solo su Whazzup (§5): la partenza del piano; per un arrivo, partenza + durata prevista.</summary>
    private static DateTimeOffset? ProgrammatoDalPiano(SourcePilotFix p, Verso verso)
    {
        if (OrarioDalPiano(p.Plan?.DepartureTimeSec, p.Plan?.CreatedAt, null) is not { } partenza) return null;
        if (verso == Verso.Partenza) return partenza;
        return p.Plan?.EetSeconds is > 0 and var eet ? partenza.AddSeconds(eet) : null;
    }

    /// <summary>
    /// Stima d'arrivo (§7): in volo, distanza residua / velocità al suolo + 6'; non ancora partito, partenza + eet + 6'.
    /// </summary>
    internal static DateTimeOffset? StimaArrivo(SourcePilotFix p, DateTimeOffset asOf)
    {
        if (!p.OnGround && p.GroundSpeed > 60 && p.Plan?.ArrivalDistanceNm is > 0 and var d)
            return asOf.AddHours(d / p.GroundSpeed) + CodaArrivo;

        if (p.Plan?.EetSeconds is not > 0) return null;
        var partenza = OrarioDalPiano(p.Plan.ActualDepartureTimeSec, p.Plan.CreatedAt, asOf)
                       ?? OrarioDalPiano(p.Plan.DepartureTimeSec, p.Plan.CreatedAt, asOf);
        if (partenza is not { } t) return null;
        // Ancora a terra alla partenza con l'orario passato: parte adesso, non nel passato.
        if (p.OnGround && t < asOf) t = asOf;
        return t.AddSeconds(p.Plan.EetSeconds.Value) + CodaArrivo;
    }

    /// <summary>
    /// Gli orari del piano sono secondi dalla mezzanotte UTC: il giorno si ricava da <c>createdAt</c>, spostandolo
    /// di un giorno se lo scarto supera le 12 ore (piano depositato a tarda sera per un volo dopo la mezzanotte, o
    /// viceversa). Senza <c>createdAt</c> fa da riferimento <paramref name="riserva"/>.
    /// </summary>
    internal static DateTimeOffset? OrarioDalPiano(int? secondi, DateTimeOffset? creato, DateTimeOffset? riserva)
    {
        if (secondi is not { } s || s < 0 || s >= 48 * 3600) return null;
        if ((creato ?? riserva) is not { } rif) return null;
        var t = new DateTimeOffset(rif.UtcDateTime.Date, TimeSpan.Zero).AddSeconds(s);
        if (t - rif > TimeSpan.FromHours(12)) t = t.AddDays(-1);
        else if (rif - t > TimeSpan.FromHours(12)) t = t.AddDays(1);
        return t;
    }

    private static DateTimeOffset Secondi(DateTimeOffset t)
    {
        var u = t.ToUniversalTime();
        return new DateTimeOffset(u.Ticks - u.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    // ---------------------------------------------------------------- testi

    private static string IdNuovo(string callsign, HashSet<string> usati)
    {
        var id = callsign;
        for (var n = 2; !usati.Add(id); n++) id = callsign + "#" + n.ToString(CultureInfo.InvariantCulture);
        return id;
    }

    /// <summary>Le prime tre lettere se il callsign è di una compagnia (<c>AZA123</c>), null per marche (<c>IABCD</c>).</summary>
    internal static string? Compagnia(string callsign)
    {
        var c = callsign.Trim().ToUpperInvariant();
        return c.Length >= 4 && c[..3].All(ch => ch is >= 'A' and <= 'Z') && char.IsAsciiDigit(c[3]) ? c[..3] : null;
    }

    private static string? Iata(string? s)
    {
        var v = Palette(s, 3);
        return v.Length == 3 && v.All(char.IsAsciiLetterOrDigit) ? v : null;
    }

    /// <summary>
    /// Il testo pronto per le palette (§3): maiuscolo, senza accenti, solo <c>A-Z 0-9</c>, spazio e <c>. - / :</c>,
    /// tagliato a <paramref name="massimo"/>. Gli altri caratteri diventano spazi, gli spazi doppi uno.
    /// </summary>
    public static string Palette(string? s, int massimo)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var u = char.ToUpperInvariant(ch);
            var buono = u is >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '/' or ':';
            if (buono) sb.Append(u);
            else if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }
        var t = sb.ToString().Trim();
        return t.Length <= massimo ? t : t[..massimo].TrimEnd();
    }

    private static bool Uguale(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Quello che una fotografia sola non dice: quando un volo è diventato DEPARTED o LANDED. Una per scalo, nel
/// processo. Le voci non toccate da un giorno se ne vanno da sole.
/// </summary>
public sealed class MemoriaTabellone
{
    public sealed class Voce
    {
        public DateTimeOffset? Partito { get; set; }
        public DateTimeOffset? Atterrato { get; set; }
        public DateTimeOffset? UltimaStima { get; set; }
        public DateTimeOffset VistaIl { get; set; }
    }

    private readonly Dictionary<string, Voce> _voci = new(StringComparer.OrdinalIgnoreCase);

    public int Conta => _voci.Count;

    internal Voce Traccia(string chiave, DateTimeOffset adesso)
    {
        if (!_voci.TryGetValue(chiave, out var v)) _voci[chiave] = v = new Voce();
        v.VistaIl = adesso;
        return v;
    }

    internal void Pota(DateTimeOffset adesso)
    {
        foreach (var k in _voci.Where(kv => adesso - kv.Value.VistaIl > TimeSpan.FromDays(1)).Select(kv => kv.Key).ToList())
            _voci.Remove(k);
    }
}
