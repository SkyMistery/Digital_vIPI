using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// La prosa delle SID e delle STAR convenzionali (carta F7, slice 7b): «DELIK – TR 310° (RDL 130 LMT VOR) – LMT VOR/DME to be
/// left on RDL 258 LMT VOR until 14 NM LMT DME, then turn right to join RDL 273 LMT VOR (TR 093°) bound to SUGEP» diventa
/// tratti come quelli delle tabelle. I punti con nome restano col nome; quelli senza (una radiale a una distanza DME, due
/// radiali che si incrociano, la fine di un arco DME) si <b>calcolano</b> dalle radioassistenze dell'AIP, con la declinazione
/// della stazione, e il tratto dice com'è definito (<see cref="ProcedureLeg.Computed"/>).
/// <para>
/// Si legge una frase solo se è fatta <b>tutta</b> di pezzi noti: una parola che resta fuori (una quota da lasciare,
/// «Leaving 4000 FT», un localizzatore) e la procedura resta col suo testo, senza tratti, e il perché va nelle segnalazioni.
/// Non si inventa niente.
/// </para>
/// </summary>
public static partial class ItalianAd2Prose
{
    /// <summary>Una radioassistenza per i calcoli: dove sta e la declinazione del VOR (gradi, est positivo).</summary>
    public sealed record Station(Position Position, double Variation);

    /// <summary>La pista di una SID: la soglia e la rotta vera, per «RWY heading».</summary>
    public sealed record Runway(Position Threshold, double TrueBearing);

    /// <summary>
    /// I tratti di <paramref name="procedure"/> dalla sua prosa, o il perché non si leggono. <paramref name="point"/> dà la
    /// posizione di un punto con nome (ENR 4.4, WAYPOINT LIST), <paramref name="station"/> quella di una radioassistenza,
    /// <paramref name="runway"/> la prima pista della procedura.
    /// </summary>
    public static (IReadOnlyList<ProcedureLeg> Legs, string? Problem) Decode(
        Procedure procedure, Func<string, Position?> point, Func<string, Station?> station, Runway? runway = null)
    {
        var (tratti, problema, _) = DecodeWithWarnings(procedure, point, station, runway);
        return (tratti, problema);
    }

    /// <summary>
    /// Come <see cref="Decode"/>, e in più le approssimazioni fatte leggendo una procedura che si legge: una prua disegnata
    /// come rotta, senza vento (committente, 23 ottobre: «disegnalo come se non ci fosse vento e segnalo»).
    /// </summary>
    public static (IReadOnlyList<ProcedureLeg> Legs, string? Problem, IReadOnlyList<string> Warnings) DecodeWithWarnings(
        Procedure procedure, Func<string, Position?> point, Func<string, Station?> station, Runway? runway = null, double? aerodromeVariation = null,
        string? initialClimb = null, Departure? departure = null, Func<string, Localizer?>? localizer = null)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(station);
        if (procedure.Kind is not (ProcedureKind.Sid or ProcedureKind.Star or ProcedureKind.Transition or ProcedureKind.InitialClimb))
            return ([], "non è una SID, una STAR o una transizione", []);
        // Una STAR che non dice da dove comincia («Proceed on TR 119° … bound to TAQ VOR/DME») parte dal punto del suo nome.
        var inizio = procedure.Kind == ProcedureKind.Star ? procedure.Designator.Split(' ')[0] : null;
        bool EStazione(string n) => station(n) is not null;
        bool EConosciuto(string n) => station(n) is not null || point(n) is not null;
        // Una salita iniziale si legge per la sua parte comune (i rami sono per le SID, SC1).
        var descrizione = procedure.Kind == ProcedureKind.InitialClimb && procedure.Description is { } d ? Dividi(d).Comune : procedure.Description;
        var (ricucito, ricuciture) = Ricuci(descrizione, EStazione, EConosciuto, inizio);
        var testo = Route(ricucito);
        if (testo is null) return ([], "senza testo", []);
        // SC1: la SID che non parte dalla pista continua il percorso della salita iniziale (il testo che vale per lei).
        if (initialClimb is not null)
        {
            var (salita, ricucitureDellaSalita) = Ricuci(initialClimb, EStazione, EConosciuto, null);
            if (Route(salita) is not { } percorso) return ([], "la salita iniziale non ha un percorso", []);
            testo = percorso + " " + testo;
            ricuciture = [.. ricucitureDellaSalita, .. ricuciture];
        }
        var lettore = new Lettore(testo, point, station, runway, dallaPista: procedure.Kind is ProcedureKind.Sid or ProcedureKind.InitialClimb,
            declinazioneDelloScalo: aerodromeVariation, decollo: departure, quoteMinime: procedure.Minimums + "\n" + procedure.Description,
            localizzatore: localizer);
        var (tratti, problema) = lettore.Leggi(inizio, procedure.Kind == ProcedureKind.Sid ? procedure.Designator.Split(' ')[0] : null);
        // Passo 8 (committente: «per tutte le STAR»): le MEA a tratti delle minime.
        if (problema is null && procedure.Kind == ProcedureKind.Star) tratti = ConLeMea(tratti, procedure.Minimums, point, station);
        return (tratti, problema, problema is null ? [.. ricuciture.Concat(lettore.Avvisi).Distinct(StringComparer.Ordinal)] : []);
    }

    /// <summary>
    /// Segnalazioni 2611, revisione col committente (carta §6, gruppi 1, 3 e 7): il testo rotto dal PDF e gli errori di stampa
    /// che lo stesso testo corregge, prima di tutto il resto. Si ricuce solo se ne esce una cosa che esiste — una radioassistenza
    /// (<paramref name="stazione"/>), il punto del nome della STAR, una parola del percorso —; se no il testo resta com'è e la
    /// procedura si ferma come prima. Gli errori di stampa letti lo stesso tornano negli avvisi («se qualcosa non torna lo
    /// correggiamo dall'app»).
    /// </summary>
    internal static (string? Testo, IReadOnlyList<string> Avvisi) Ricuci(
        string? descrizione, Func<string, bool> stazione, Func<string, bool> conosciuto, string? inizio)
    {
        if (descrizione is null) return (null, []);
        var avvisi = new List<string>();
        var t = descrizione;
        string Stampa(Match m, string letto)
        {
            avvisi.Add($"«{RxSpazi().Replace(m.Value, " ").Trim()}» è un errore di stampa: letto come «{letto.Trim()}»");
            return letto;
        }
        // R4: «M OROB –», «T ZO VOR/DME» in testa a una STAR; anche senza la prima lettera («ELDA –» per NELDA 3M).
        if (inizio is not null) t = NomeSpezzatoInTesta(t, inizio, conosciuto);
        // R3: «P roceed» (LICR), «pro ceed» (LIRN RWY 06).
        t = RxParolaSpezzata().Replace(t, m => ParoleDelPercorso.Contains(m.Groups[1].Value + m.Groups[2].Value) ? m.Groups[1].Value + m.Groups[2].Value : m.Value);
        foreach (var (prima, dopo) in RxParolaSpezzataMinuscola().Matches(t).Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList())
            if (ParoleDelPercorso.Contains(prima + dopo))
                t = Regex.Replace(t, $@"\b{prima}\s{dopo}\b", prima + dopo, RegexOptions.CultureInvariant);
        // R1: «RDL 064LPD VOR»; R5: «RDL 07 8L PD VOR» (LICD PURZE 1Y). Tre cifre e una radioassistenza, o niente.
        t = RxSiglaAttaccataAllaRadiale().Replace(t, m => stazione(m.Groups[2].Value) ? $"RDL {m.Groups[1].Value} {m.Groups[2].Value}" : m.Value);
        t = RxRadialeSparsa().Replace(t, m => RxCifreESigla().Match(RxSpazi().Replace(m.Groups[1].Value, "")) is { Success: true } x && stazione(x.Groups[2].Value)
            ? $"RDL {x.Groups[1].Value} {x.Groups[2].Value} VOR" : m.Value);
        // G3.1: «R 199 RCA VOR»; G3.2: «R318 (TR 318°) CAR VOR», la stazione dopo la rotta.
        t = RxRadialeRConLoSpazio().Replace(t, m => stazione(m.Groups[2].Value) ? $"RDL {m.Groups[1].Value} {m.Groups[2].Value}" : m.Value);
        t = RxRadialeRStazioneDopo().Replace(t, m => stazione(m.Groups[3].Value)
            ? $"RDL {m.Groups[1].Value} {m.Groups[3].Value} {m.Groups[4].Value} (TR {m.Groups[2].Value}°)" : m.Value);
        // Gruppo 7: errori di stampa che lo stesso testo corregge.
        t = RxGradiComeVirgolette().Replace(t, m => Stampa(m, $"TR {m.Groups[1].Value}°"));
        t = RxDoppiaBarra().Replace(t, m => Stampa(m, "VOR/DME"));
        t = RxRadialeBarra().Replace(t, m => Stampa(m, $"RDL {m.Groups[1].Value}"));
        t = RxTrattinoAttaccato().Replace(t, m => stazione(m.Groups[1].Value) ? Stampa(m, $"{m.Groups[1].Value} {m.Groups[2].Value}") : m.Value);
        t = RxPuntini().Replace(t, m => Stampa(m, m.Groups[1].Value + " "));
        t = RxRottaSenzaTr().Replace(t, m =>
        {
            if ((Numero(m.Groups[3]) + 180) % 360 != Numero(m.Groups[1])) return m.Value;
            avvisi.Add($"«{m.Groups[1].Value}°» senza «TR» è un errore di stampa: letto come TR {m.Groups[1].Value}° (la radiale {m.Groups[3].Value} fra parentesi dice la stessa rotta)");
            return $"TR {m.Groups[1].Value}°{m.Groups[2].Value}";
        });
        t = RxDne().Replace(t, m => stazione(m.Groups[1].Value) ? Stampa(m, $"{m.Groups[1].Value} DME") : m.Value);
        t = RxRadialeSenzaSigla().Replace(t, m =>
        {
            var sigla = m.Groups[4].Value;
            if (!stazione(sigla) || (Numero(m.Groups[2]) + 180) % 360 != Numero(m.Groups[1])) return m.Value;
            avvisi.Add($"«RDL {m.Groups[2].Value} VOR» senza la stazione è un errore di stampa: letto come RDL {m.Groups[2].Value} {sigla}, la stazione verso cui va la rotta");
            return $"TR {m.Groups[1].Value}° (RDL {m.Groups[2].Value} {sigla} VOR){m.Groups[3].Value}{sigla}{m.Groups[5].Value}";
        });
        // Senza la stazione da nessuna parte («ILOWE – TR 199° (RDL 019 VOR) bound NOLHE.», LIRS ILOWE 1W): vale la rotta.
        t = RxRadialeSenzaStazioneFraParentesi().Replace(t, m =>
        {
            avvisi.Add($"«RDL {m.Groups[1].Value} VOR» senza la stazione è un errore di stampa: vale la rotta");
            return "";
        });
        // Revisione 2 (committente, 24 ottobre): «until joining 088 ANC VOR» senza «RDL» (LIPY KEGED 9H), «(IAS MAX 210)» senza
        // «kt» (LIPY LANLI 4C).
        t = RxRadialeSenzaRdl().Replace(t, m => stazione(m.Groups[3].Value) ? Stampa(m, $"{m.Groups[1].Value} RDL {m.Groups[2].Value} {m.Groups[3].Value} VOR") : m.Value);
        t = RxVelocitaSenzaKt().Replace(t, m => Stampa(m, $"(IAS MAX {m.Groups[1].Value} KT)"));
        // Revisione 3 (LIBD BANAV 8B): «join RDL 273 (TR 093°) bound to BAR VOR»: la radiale senza stazione è di quella verso
        // cui si va, se la rotta è la radiale al contrario.
        t = RxRadialeVersoLaStazione().Replace(t, m => stazione(m.Groups[3].Value) && (Numero(m.Groups[1]) + 180) % 360 == Numero(m.Groups[2])
            ? Stampa(m, $"RDL {m.Groups[1].Value} {m.Groups[3].Value} VOR (TR {m.Groups[2].Value}°) bound to {m.Groups[3].Value}") : m.Value);
        // Revisione 4 (LIEE KOVAS 6R): «(TR Turn left … (TR QDR 298°)»: la parentesi aperta davanti a «Turn» si toglie, e la
        // rotta col QDR in mezzo è la rotta.
        t = RxParentesiDavantiAllaVirata().Replace(t, m =>
        {
            avvisi.Add("«(TR» davanti a «Turn» è un errore di stampa: tolto");
            return "";
        });
        t = RxRottaColQdr().Replace(t, m => Stampa(m, $"(TR {m.Groups[1].Value}°)"));
        return (t, avvisi);
    }

    [GeneratedRegex(@"\(\s*TR\s+(?=[Tt]urn\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxParentesiDavantiAllaVirata();

    [GeneratedRegex(@"\(\s*TR\s+QDR\s+(\d{3})\s*°?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxRottaColQdr();

    // R3: le parole del percorso che il PDF spezza dopo la prima lettera.
    private static readonly HashSet<string> ParoleDelPercorso = new(StringComparer.Ordinal)
    {
        "Proceed", "Turn", "Join", "Intercept", "Continue", "Leave", "Follow", "Crossing", "Bound", "After", "Then", "Over",
        "Overflying", "Cross", "Maintain", "From",
        "proceed", "intercept", "intercepting", "following", "joining", "crossing", "inbound",
    };

    /// <summary>
    /// R4: le lettere maiuscole in testa al testo (dopo le note fra parentesi), spazi compresi: il primo pezzo che, senza spazi,
    /// è il punto del nome della STAR — o il nome senza la prima lettera, se non è un altro nome dell'AIP — diventa il nome.
    /// </summary>
    private static string NomeSpezzatoInTesta(string t, string nome, Func<string, bool> conosciuto)
    {
        if (RxLettereInTesta().Match(t) is not { Success: true } m) return t;
        var lettere = m.Groups["lettere"];
        for (var fine = lettere.Length; fine > 1; fine--)
        {
            if (fine < lettere.Length && lettere.Value[fine] != ' ') continue;
            var pezzo = lettere.Value[..fine];
            if (pezzo == nome) return t;
            var unito = pezzo.Replace(" ", "", StringComparison.Ordinal);
            if (unito == nome || (nome.Length > 3 && unito == nome[1..] && !conosciuto(unito)))
                return string.Concat(t.AsSpan(0, lettere.Index), nome, t.AsSpan(lettere.Index + fine));
        }
        return t;
    }

    /// <summary>
    /// Tutte le procedure di <paramref name="document"/> senza tratti, lette dalla prosa coi punti e le radioassistenze dello
    /// stesso documento (dopo le correzioni: un punto corretto a mano vale anche qui). Le altre restano come sono; quelle
    /// che non si leggono tornano col perché, quelle lette con un'approssimazione (una prua come rotta) con l'approssimazione.
    /// </summary>
    public static (AipDocument Document, IReadOnlyList<(Procedure Procedure, string Problem)> NotRead,
        IReadOnlyList<(Procedure Procedure, string Warning)> Approximated) DecodeAll(AipDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Procedures is not { Count: > 0 } procedure) return (document, [], []);
        var punti = (document.Points ?? []).GroupBy(p => p.Designator, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Position, StringComparer.Ordinal);
        // Un VOR (la declinazione è sua); una sigla con più impianti (PIS è un DVOR/DME e un locator) prende quello col VOR.
        // Anche quelli di scalo (AD 2.19: BRL, CMP, CSL non sono in ENR 4.1).
        // Segnalazioni 2611, B1 e B3: un localizzatore, un NDB, un TACAN non hanno la declinazione stampata (IBRN, BAP, DEC…),
        // e prima si scartavano. Si tengono: la declinazione è quella dello scalo (AD 2.2) se è una radioassistenza di scalo,
        // se no quella del VOR più vicino che la stampa. Con la sua, se ce l'ha, vince comunque.
        var tutte = (document.Navaids ?? []).Select(n => (Navaid: n, Scalo: (Aerodrome?)null))
            .Concat((document.Aerodromes ?? []).SelectMany(a => a.NavigationAids.Select(n => (Navaid: n, Scalo: (Aerodrome?)a))))
            .Select(x => (x.Navaid.Designator, Posizione: x.Navaid.Position, Sua: Variation(x.Navaid.MagneticVariation),
                DelloScalo: Variation(x.Scalo?.MagneticVariation)))
            // Il DME di un ILS («ARC 9 NM IPR DME», «7 NM IFA DME»): dove sta l'antenna del DME, non il localizzatore (sono a un
            // miglio l'uno dall'altro), con la declinazione dell'ILS. Un ILS senza DME non c'è: il localizzatore non misura distanze.
            .Concat((document.Aerodromes ?? []).SelectMany(a => a.Ils.Select(i => (Ils: i, Scalo: a)))
                .Where(x => x.Ils.Components.Any(c => c.Type.StartsWith("DME", StringComparison.Ordinal)))
                .Select(x => (x.Ils.Designator, Posizione: x.Ils.Components.First(c => c.Type.StartsWith("DME", StringComparison.Ordinal)).Position,
                    Sua: Variation(x.Ils.MagneticVariation), DelloScalo: Variation(x.Scalo.MagneticVariation))))
            .ToList();
        var conLaSua = tutte.Where(x => x.Sua is not null).ToList();
        double? DalPiuVicino(Position p) => conLaSua.Count == 0
            ? null
            : conLaSua.MinBy(x => Sphere.DistanceNm(p, x.Posizione)).Sua;
        var stazioni = tutte
            .Select(x => (x.Designator, x.Posizione, x.Sua, Declinazione: x.Sua ?? x.DelloScalo ?? DalPiuVicino(x.Posizione)))
            .Where(x => x.Declinazione is not null)
            .GroupBy(x => x.Designator, StringComparer.Ordinal)
            // Una sigla con più impianti (PIS è un DVOR/DME e un locator): quello con la declinazione sua.
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sua is null).Select(x => new Station(x.Posizione, x.Declinazione!.Value)).First(),
                StringComparer.Ordinal);
        // Passo 6: i localizzatori, dalle radioassistenze degli scali (AD 2.19: l'antenna del LOC) e dalla loro pista (AD 2.12: la
        // rotta vera dell'atterraggio; «NIL» dalle due soglie).
        var localizzatori = (document.Aerodromes ?? [])
            .SelectMany(a => a.Ils.Select(i => (Scalo: a, Ils: i)))
            .Where(x => x.Ils.Components.Any(c => c.Type.StartsWith("LOC", StringComparison.Ordinal)))
            .Select(x => (x.Ils.Designator, Pista: Pista(x.Scalo, x.Ils.Runway), Antenna: x.Ils.Components.First(c => c.Type.StartsWith("LOC", StringComparison.Ordinal)).Position,
                Declinazione: Variation(x.Ils.MagneticVariation) ?? Variation(x.Scalo.MagneticVariation) ?? 0))
            .Where(x => x.Pista is not null)
            .GroupBy(x => x.Designator, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new Localizer(g.First().Antenna, g.First().Pista!.TrueBearing, g.First().Declinazione), StringComparer.Ordinal);
        Localizer? Localizzatore(string n) => localizzatori.GetValueOrDefault(n);
        var nonLette = new List<(Procedure, string)>();
        var approssimate = new List<(Procedure, string)>();
        var scali = document.Aerodromes ?? [];
        var salite = procedure.Where(p => p.Kind == ProcedureKind.InitialClimb).ToLookup(p => p.Aerodrome, StringComparer.Ordinal);
        Position? Punto(string n) => punti.TryGetValue(n, out var x) ? x : null;
        Station? Stazione(string n) => stazioni.GetValueOrDefault(n);

        List<Procedure> Leggi(Procedure p)
        {
            if (p.Kind is not (ProcedureKind.Sid or ProcedureKind.Star or ProcedureKind.Transition or ProcedureKind.InitialClimb)) return [p];
            var scalo = scali.FirstOrDefault(a => a.Icao == p.Aerodrome);
            var declinazione = DeclinazioneDelloScalo(scalo, scali);
            (IReadOnlyList<ProcedureLeg> Tratti, string? Problema, IReadOnlyList<string> Avvisi) Decodifica(Procedure q, string? pista, string? salita) =>
                DecodeWithWarnings(q, Punto, Stazione, Pista(scalo, pista), declinazione, salita, DecolloDi(q, scalo, pista), Localizzatore);
            IReadOnlyList<string> PisteDi(Procedure q) =>
                q.Runways.Count > 0 ? q.Runways : PistaDallaRotta(q, scalo, declinazione, null) is { } r ? [r] : [];
            // Passo 5: le pendenze delle salite iniziali valgono anche per le SID — quelle che le nominano («For LAKOX 7W, GITOD
            // 8W…», «if cleared via SID KOVAS 6D») e quelle senza nomi della salita della sua pista (della stessa pagina, se ce
            // n'è più d'una).
            Procedure ConLePendenzeDelleSalite(Procedure q)
            {
                var lette = salite[q.Aerodrome].Select(s => (Salita: s, Lette: ItalianClimbGradients.Read(s.Description))).ToList();
                // Una SID senza la pista nel nome: le salite della sua pagina (ASPIR 8G sotto la salita della RWY 22).
                var dellaPista = q.Runways.Count > 0
                    ? lette.Where(x => PisteDi(x.Salita).Intersect(q.Runways, StringComparer.Ordinal).Any()).ToList()
                    : lette.Where(x => x.Salita.Page?.Reference is { } pag && pag == q.Page?.Reference).ToList();
                if (dellaPista.Count > 1 && dellaPista.Where(x => x.Salita.Page?.Reference == q.Page?.Reference).ToList() is [var stessa])
                    dellaPista = [stessa];
                var pendenze = (q.ClimbGradients ?? [])
                    .Concat(lette.SelectMany(x => ItalianClimbGradients.Per(x.Lette, q.Designator, ancheSenzaNomi: false)))
                    .Concat(dellaPista.Count == 1 ? ItalianClimbGradients.Per(dellaPista[0].Lette, q.Designator, ancheSenzaNomi: true) : [])
                    .DistinctBy(x => x.Text).ToList();
                return pendenze.Count == 0 ? q : q with { ClimbGradients = pendenze };
            }
            if (p.Kind == ProcedureKind.Sid) p = ConLePendenzeDelleSalite(p);
            if (p.Legs.Count > 0) return [p];
            Procedure Letta(Procedure q, (IReadOnlyList<ProcedureLeg> Tratti, string? Problema, IReadOnlyList<string> Avvisi) esito, params string[] dette)
            {
                var avvisi = esito.Avvisi.Concat(dette).Concat(QuoteFuoriDalPercorso(q, esito.Tratti, n => Punto(n) ?? Stazione(n)?.Position)).ToList();
                approssimate.AddRange(avvisi.Select(a => (q, a)));
                return q with { Legs = esito.Tratti, LegsFromDescription = true };
            }

            // SC1: la salita iniziale ha i suoi tratti, se ha un percorso (alcune sono solo avvertenze e pendenze).
            if (p.Kind == ProcedureKind.InitialClimb)
            {
                if (!HaUnPercorso(p.Description)) return [p];
                var salita = Decodifica(p, PisteDi(p).FirstOrDefault(), null);
                if (salita.Problema is null) return [Letta(p, salita)];
                // Una salita senza un punto suo («After take off proceed on track 135°», LIEE RWY 14) è aperta: la continua la SID.
                // Né una salita fatta solo di rami per le SID (LICR RWY 29 e 33, passo 8): il testo è nei rami.
                if (salita.Problema is not ("nessun punto" or "senza testo")) nonLette.Add((p, salita.Problema));
                return [p];
            }

            // Le salite iniziali che nominano la SID in un ramo («- if cleared for SID INKIM 9G or OTRET 8G: turn right on TR
            // 043°», LIBP RWY 22): la SID le continua, comunque cominci il suo testo, e la pista è la loro.
            var nominanti = p.Kind != ProcedureKind.Sid ? []
                : salite[p.Aerodrome].Where(s => ValePer(s.Description, p.Designator)).ToList();
            // La pista: quella del nome; se la SID non la dice, quella della salita che la nomina, o della salita stampata sulla
            // stessa pagina (committente, 24 ottobre: LIBP ASPIR 8G sta sotto «INITIAL CLIMB PROCEDURE RWY 22», le «F» sotto la 04),
            // o della prima rotta del testo (entro 15°); se ne dice due, quella della prima rotta.
            var dellaPagina = p.Kind == ProcedureKind.Sid && p.Runways.Count == 0
                && salite[p.Aerodrome].Where(s => s.Page?.Reference is { } pag && pag == p.Page?.Reference).SelectMany(PisteDi).Distinct(StringComparer.Ordinal).ToList() is [var unica]
                ? unica : null;
            // Revisione 3 (committente, LICR BERMI 6F): una SID che comincia come la salita iniziale di una pista («After takeoff
            // proceed on track 164°, reaching 800ft …» come la RWY 15) è di quella pista.
            var comeLaSalita = p.Kind == ProcedureKind.Sid && p.Runways.Count == 0 && RottaDopoIlDecollo(p.Description) is { } primaRotta
                && salite[p.Aerodrome].Where(s => RottaDopoIlDecollo(s.Description) == primaRotta).SelectMany(PisteDi).Distinct(StringComparer.Ordinal).ToList() is [var stessa]
                ? stessa : null;
            var pistaDellaSid = p.Kind != ProcedureKind.Sid ? p.Runways.FirstOrDefault()
                : p.Runways.Count == 1 ? p.Runways[0]
                : p.Runways.Count == 0 && nominanti.SelectMany(PisteDi).FirstOrDefault() is { } dallaSalita ? dallaSalita
                : dellaPagina ?? comeLaSalita
                ?? PistaDallaRotta(p, scalo, declinazione, p.Runways.Count > 0 ? p.Runways : null) ?? p.Runways.FirstOrDefault();
            var da = Decodifica(p, pistaDellaSid, null);
            if (p.Kind != ProcedureKind.Sid || !(ContinuaLaSalita(p.Description) || nominanti.Count > 0))
            {
                if (da.Problema is null) return [Letta(p, da)];
                nonLette.Add((p, da.Problema));
                return [p];
            }

            // SC1 e SC2: la SID che non parte dalla pista continua la salita iniziale della sua pista, una volta per pista se le
            // piste hanno salite diverse. La pista, se il nome non la dice, è quella della salita che nomina la SID.
            var dellaPista = (string pista) => salite[p.Aerodrome].Where(s => PisteDi(s).Contains(pista, StringComparer.Ordinal)).ToList();
            IReadOnlyList<string> piste = p.Runways.Count > 0 ? p.Runways : nominanti.SelectMany(PisteDi).Distinct(StringComparer.Ordinal).ToList();
            if (piste.Count == 0 && pistaDellaSid is { } dallaRotta) piste = [dallaRotta];
            var gruppi = piste.Select(r => (Pista: r, Salita: SalitaPerLaSid(p.Designator, dellaPista(r), p.Page)))
                .GroupBy(x => x.Salita.Testo ?? "", StringComparer.Ordinal).ToList();
            if (gruppi.Count == 0)
            {
                if (da.Problema is null) return [Letta(p, da)];
                nonLette.Add((p, $"{da.Problema}; la SID non parte dalla pista e la pista non si sa"));
                return [p];
            }
            var esiti = gruppi.Select(g =>
            {
                // Passo 8: con la pista si sa anche la salita, e le sue pendenze senza nomi valgono (LICB: la pagina ha la salita
                // della 05 e quella della 23, e l'8.5% della 05 non arrivava alle SID).
                var conPiste = p with { Runways = [.. g.Select(x => x.Pista)] };
                var q = (gruppi.Count == 1 ? p : conPiste) with { ClimbGradients = ConLePendenzeDelleSalite(conPiste).ClimbGradients };
                var (testo, perche) = g.First().Salita;
                var conLaSalita = testo is null ? default : Decodifica(q, g.First().Pista, testo);
                return (Procedura: q, Testo: testo, Perche: perche, Esito: conLaSalita);
            }).ToList();
            if (gruppi.Count > 1 && esiti.All(e => e.Testo is null || e.Esito.Problema is not null))
            {
                // Nessuna pista si legge con la sua salita: resta com'era, una procedura sola.
                if (da.Problema is null) return [Letta(p, da, $"la SID non parte dalla pista: {esiti[0].Perche ?? esiti[0].Esito.Problema}; disegnata da sola")];
                nonLette.Add((p, da.Problema));
                return [p];
            }
            var lette = new List<Procedure>();
            foreach (var e in esiti)
            {
                if (e.Testo is not null && e.Esito.Problema is null)
                {
                    lette.Add(Letta(e.Procedura, e.Esito));
                    continue;
                }
                var perche = e.Testo is null ? e.Perche : $"la salita iniziale non si legge ({e.Esito.Problema})";
                if (gruppi.Count == 1 && da.Problema is null)
                {
                    // Se la pista una salita iniziale non ce l'ha, la SID è tutta lì: non c'è niente da dire.
                    lette.Add(e.Testo is null && perche == NessunaSalita ? Letta(p, da) : Letta(p, da, $"la SID non parte dalla pista e {perche}: disegnata da sola"));
                    continue;
                }
                nonLette.Add((e.Procedura, gruppi.Count == 1 && da.Problema is not null ? $"{da.Problema}; {perche}" : perche!));
                lette.Add(e.Procedura);
            }
            return lette;
        }

        var nuove = procedure.SelectMany(Leggi).ToList();
        return (document with { Procedures = nuove }, nonLette, approssimate);
    }

    /// <summary>
    /// La pista della prima rotta stampata nel testo («Proceed on TR 041°» → 04): quella, fra <paramref name="fra"/> o fra
    /// tutte quelle dello scalo, la cui rotta vera è entro 15° (SC2 della revisione col committente: LIPY, LIDT).
    /// </summary>
    private static string? PistaDallaRotta(Procedure p, Aerodrome? scalo, double? declinazione, IReadOnlyList<string>? fra)
    {
        if (scalo is null || declinazione is null || Route(p.Description) is not { } testo || RxPrimaRotta().Match(testo) is not { Success: true } m) return null;
        var vera = Numero(m.Groups[1]) + declinazione.Value;
        return scalo.Runways.Where(r => fra is null || fra.Contains(r.Designator, StringComparer.Ordinal))
            .Select(r => (r.Designator, Pista: Pista(scalo, r.Designator)))
            .Where(x => x.Pista is not null)
            .Select(x => (x.Designator, Scarto: Math.Abs(Sphere.Normalize(x.Pista!.TrueBearing - vera + 180) - 180)))
            .Where(x => x.Scarto <= 15).OrderBy(x => x.Scarto).Select(x => x.Designator).FirstOrDefault();
    }

    [GeneratedRegex(@"\bTR\s+(\d{3})°", RegexOptions.CultureInvariant)]
    private static partial Regex RxPrimaRotta();

    /// <summary>
    /// Passo 5 (gruppo 6, committente): da dove sale una SID o una salita iniziale. La fine della pista è la soglia opposta, alla sua
    /// quota (AD 2.12); se la pista non si sa, lo scalo (ARP, quota dello scalo). La pendenza degli ostacoli, o la sola senza motivo
    /// (<see cref="ItalianClimbGradients.PerDisegnare"/>); se no il 3,3% (200 ft/NM), e si dice.
    /// </summary>
    internal static Departure? DecolloDi(Procedure p, Aerodrome? scalo, string? pista)
    {
        if (scalo is null || p.Kind is not (ProcedureKind.Sid or ProcedureKind.InitialClimb)) return null;
        var (piedi, detta) = ItalianClimbGradients.PerDisegnare(p.ClimbGradients) is { } g && ItalianClimbGradients.PiediPerMiglio(g) is { } ft
            ? (ft, $"{ft:0} ft/NM{(g.Percent is { } pc ? $" ({pc:0.#}%)" : "")}{(g.Reason == ClimbGradientReason.Obstacles ? ", la pendenza degli ostacoli" : ", la pendenza stampata")}")
            : (200.0, "il 3,3% (200 ft/NM): la procedura non stampa una pendenza degli ostacoli");
        if (pista is not null && PistaOpposta(pista) is { } opposta && scalo.Runways.FirstOrDefault(r => r.Designator == opposta) is { Threshold: { } fine } altra
            && QuotaInPiedi(altra.ThresholdElevation) is { } quota)
            return new Departure(fine, quota, piedi, detta, false);
        return scalo.Arp is { } arp && QuotaInPiedi(scalo.Elevation) is { } dello ? new Departure(arp, dello, piedi, detta, true) : null;
    }

    /// <summary>Una quota stampata in piedi: «611.3 FT / 611.3 FT» → 611,3; «43 M AMSL» → 141; «NIL» → null.</summary>
    internal static double? QuotaInPiedi(string? stampata)
    {
        if (stampata is null || RxQuotaStampata().Match(stampata) is not { Success: true } m) return null;
        var valore = double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture);
        return m.Groups["u"].Value.Equals("M", StringComparison.OrdinalIgnoreCase) ? valore * 3.28084 : valore;
    }

    [GeneratedRegex(@"(?<v>\d+(?:\.\d+)?)\s*(?<u>FT|ft|M|m)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxQuotaStampata();

    /// <summary>
    /// N2 della revisione col committente (LIPH ADOSA 1A): le righe delle quote sotto il percorso nominano un punto che nel
    /// percorso letto non c'è («PH006 – VIC VOR/DME: 5500 FT» dove il testo dice «PH005 – VIC»): si dice. Un punto che sta sul
    /// percorso disegnato (entro un miglio) va bene anche se il testo non lo nomina (AKILI di LIRA, il punto di una radiale); le
    /// salite iniziali nominano le SID, e non si guardano.
    /// </summary>
    private static IEnumerable<string> QuoteFuoriDalPercorso(Procedure p, IReadOnlyList<ProcedureLeg> tratti, Func<string, Position?> dove)
    {
        if (p.Description is null || p.Kind == ProcedureKind.InitialClimb) yield break;
        var percorso = tratti.Select(t => t.Position ?? (t.Fix is { } f ? dove(f) : null)).OfType<Position>().ToList();
        var nelPercorso = tratti.Select(t => t.Fix).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var detti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var riga in p.Description.Split('\n').Skip(1))
        {
            if (!RxRigaDopo().IsMatch(riga) || RxVerbo().IsMatch(riga)) continue;
            foreach (Match m in RxNomeDiPunto().Matches(riga))
            {
                if (dove(m.Value) is not { } punto || nelPercorso.Contains(m.Value)) continue;
                // Sul percorso disegnato: si dice solo se le quote ne fanno un tratto («PH005 – PH006: FL 90; PH006 – VIC», ADOSA 1A).
                var lontano = DalPercorso(punto, percorso) > 1;
                if ((lontano || RxNomeInUnTratto(m.Value).IsMatch(riga)) && detti.Add(m.Value))
                    yield return lontano
                        ? $"le quote sotto il percorso nominano {m.Value}, che nel percorso non c'è"
                        : $"le quote sotto il percorso fanno un tratto da {m.Value}, che il testo del percorso non nomina (sta sul percorso disegnato)";
            }
        }
    }

    [GeneratedRegex(@"^\+(\d{3,5})$", RegexOptions.CultureInvariant)]
    private static partial Regex RxQuotaMinima();

    [GeneratedRegex(@"\(\s*([A-Z]{3,5})\s+LOC\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxLocNellaDefinizione();

    private static Regex RxNomeInUnTratto(string nome) =>
        new($@"(?:[–-]\s*{Regex.Escape(nome)}\b|\b{Regex.Escape(nome)}\s*[–-])", RegexOptions.CultureInvariant);

    /// <summary>Quanto <paramref name="p"/> sta lontano dalla spezzata <paramref name="percorso"/> (NM, nel piano attorno a lui).</summary>
    private static double DalPercorso(Position p, IReadOnlyList<Position> percorso)
    {
        if (percorso.Count == 0) return double.MaxValue;
        var k = Math.Cos(p.Lat * Math.PI / 180);
        (double X, double Y) Piano(Position q) => ((q.Lon - p.Lon) * 60 * k, (q.Lat - p.Lat) * 60);
        var minimo = percorso.Min(q => Sphere.DistanceNm(p, q));
        for (var i = 1; i < percorso.Count; i++)
        {
            var (ax, ay) = Piano(percorso[i - 1]);
            var (bx, by) = Piano(percorso[i]);
            var (dx, dy) = (bx - ax, by - ay);
            var l2 = dx * dx + dy * dy;
            if (l2 < 1e-9) continue;
            var t = Math.Clamp(-(ax * dx + ay * dy) / l2, 0, 1);
            minimo = Math.Min(minimo, Math.Sqrt(Math.Pow(ax + t * dx, 2) + Math.Pow(ay + t * dy, 2)));
        }
        return minimo;
    }

    [GeneratedRegex(@"\b(?:[A-Z]{2}\d{3}|[A-Z]{5})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxNomeDiPunto();

    /// <summary>
    /// La pista <paramref name="designatore"/> di <paramref name="scalo"/>: la soglia e la rotta vera. SC3 della revisione col
    /// committente: dove AD 2.12 stampa «NIL» per la rotta vera (LICG, LIDT, LIBD, LIPY) la direzione va dalla soglia a quella
    /// della pista opposta, che c'è.
    /// </summary>
    internal static Runway? Pista(Aerodrome? scalo, string? designatore)
    {
        if (scalo is null || designatore is null) return null;
        if (scalo.Runways.FirstOrDefault(r => r.Designator == designatore) is not { Threshold: { } soglia } pista) return null;
        if (Gradi(pista.TrueBearing) is { } vera) return new Runway(soglia, vera);
        return PistaOpposta(designatore) is { } opposta && scalo.Runways.FirstOrDefault(r => r.Designator == opposta) is { Threshold: { } altra }
            ? new Runway(soglia, Sphere.BearingDeg(soglia, altra))
            : null;
    }

    /// <summary>La pista dall'altra parte: 07 → 25, 22L → 04R, 18 → 36.</summary>
    internal static string? PistaOpposta(string designatore)
    {
        if (RxDesignatoreDiPista().Match(designatore) is not { Success: true } m) return null;
        var numero = (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + 17) % 36 + 1;
        var lato = m.Groups[2].Value switch { "L" => "R", "R" => "L", var x => x };
        return $"{numero:00}{lato}";
    }

    [GeneratedRegex(@"^(\d{2})([LRC]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex RxDesignatoreDiPista();

    /// <summary>
    /// La declinazione dello scalo (AD 2.2), o quella dello scalo più vicino che la stampa (committente, 23 ottobre): serve
    /// alla rotta di una procedura il cui testo non nomina una radioassistenza con la declinazione.
    /// </summary>
    private static double? DeclinazioneDelloScalo(Aerodrome? scalo, IReadOnlyList<Aerodrome> scali)
    {
        if (scalo is null) return null;
        if (Variation(scalo.MagneticVariation) is { } sua) return sua;
        if (scalo.Arp is not { } qui) return null;
        return scali.Where(a => a.Arp is not null && Variation(a.MagneticVariation) is not null)
            .MinBy(a => Sphere.DistanceNm(qui, a.Arp!.Value)) is { } vicino ? Variation(vicino.MagneticVariation) : null;
    }

    /// <summary>La declinazione stampata, est positivo: <c>3° E-2010.0</c> → 3; null se non c'è.</summary>
    internal static double? Variation(string? printed)
    {
        if (printed is null || RxDeclinazione().Match(printed) is not { Success: true } m) return null;
        var gradi = double.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture);
        // Revisione 4: in Italia la declinazione è di pochi gradi. Un numero grande non lo è (LIDT: l'AD 2.2 ha nel campo
        // l'indirizzo dell'ENAC, e «fax … 946665 E-mail» si leggeva 946665° E): non c'è, vale lo scalo più vicino.
        if (gradi > 30) return null;
        return m.Groups["v"].Value == "W" ? -gradi : gradi;
    }

    [GeneratedRegex(@"(?<g>\d+(?:\.\d+)?)\s*\D?\s*(?<v>[EW])\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDeclinazione();

    /// <summary>I gradi di una rotta stampata: <c>098.49°</c> → 98,49.</summary>
    private static double? Gradi(string? printed) =>
        printed is not null && RxGradi().Match(printed) is { Success: true } m ? double.Parse(m.Value, CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex RxGradi();

    /// <summary>
    /// Il percorso dalla prosa: le righe fino alla prima che finisce col punto, senza le note fra parentesi in testa
    /// («(ATC discretion)») e senza le righe delle quote («SUGEP: 4000 FT») che stanno sotto.
    /// </summary>
    internal static string? Route(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var righe = new List<string>();
        var colPunto = false;
        foreach (var grezza in description.Split('\n'))
        {
            var riga = grezza.Trim();
            if (riga.Length == 0) continue;
            if (righe.Count == 0 && (riga.StartsWith('(') || riga.StartsWith("Minimum", StringComparison.Ordinal))) continue;
            // B5: dopo una riga che finisce col punto il percorso continua solo se la riga dopo comincia come un pezzo di
            // percorso («After take-off proceed on RDL172 CRN VOR. / At 5 NM CRN DME turn left …»).
            if (colPunto && !RxRigaCheContinua().IsMatch(riga)) break;
            // Una riga con le quote e senza verbi è sotto il percorso; una con un verbo ne fa ancora parte («Leaving 4000 FT turn»).
            // Una parentesi aperta sulla riga prima continua («inbound to LICFE (RDL 139/49.4NM BSM VOR/DME or\nRDL319/19NM BOA
            // VOR/DME).», LIPO LICFE 6C): la riga che comincia con un punto ne fa parte.
            var aperta = righe.Sum(r => r.Count(c => c == '(') - r.Count(c => c == ')')) > 0;
            if (righe.Count > 0 && RxRigaDopo().IsMatch(riga) && !RxVerbo().IsMatch(riga) && !(aperta && RxRigaColPunto().IsMatch(riga))) break;
            righe.Add(riga);
            colPunto = riga.EndsWith('.');
        }
        return righe.Count == 0 ? null : Normalizza(RxNotaInCoda().Replace(string.Join(' ', righe), "."));
    }

    internal static string Normalizza(string testo)
    {
        // U+F0B0: il segno dei gradi col carattere privato del font di alcune pagine (LIBP, B5).
        var t = testo.Replace('–', '-').Replace('—', '-').Replace('', '°');
        t = RxTrattino().Replace(t, " - ");
        // Revisione col committente (carta §6): «ARC16 NM» (R2), «arc 17.0NM» e «tr 297°» minuscoli (U4, R6).
        t = RxArcoAttaccato().Replace(t, "ARC $1");
        t = RxRottaMinuscola().Replace(t, "TR $1");
        t = RxNumeroSpezzato().Replace(t, "TR $1$2°");
        t = RxNm().Replace(t, "$1 NM");
        t = RxAttaccato().Replace(t, "$1 $2");
        // Passo 8 (LICB, LIEE): la stazione della radiale dopo la rotta fra parentesi, «RDL 231 (TR 231°) COM VOR», è «RDL 231 COM
        // VOR (TR 231°)» (prima COM si leggeva come un punto da raggiungere); «track 315deg» è «track 315°».
        t = RxStazioneDopoLaRotta().Replace(t, "RDL $1 $3 $4 $2");
        t = RxGradiInParola().Replace(t, "$1°");
        // Segnalazioni 2611, B2: le stesse cose scritte in un altro modo. «R199 RCA» è la radiale 199; un DVOR è un VOR;
        // «19 NM TRP TACAN» è una distanza DME e, dopo, «TRP TACAN» è la stazione; l'alternativa per chi vola con l'NDB
        // («or QDR 019° TRP NDB (TR 199°)», «RDL/QDR 325 TRP VOR/NDB») si toglie: il percorso è quello del VOR.
        // B1: la stessa distanza letta da un altro DME, fra parentesi dopo la prima («16 NM BAR DME (or 17.5 NM BAP DME)»,
        // «13 NM PRU DME (12 NM IPER DME)»): il percorso è quello della prima. E «ARC 15 NM PRU (IPER) DME»: l'arco è di PRU.
        t = RxDistanzaAlternativa().Replace(t, "");
        // Passo 4: anche senza parentesi, «At 4 NM PIS DME or 7 NM PIN TACAN turn right» (LIRP RWY 21: prima U1 la leggeva come
        // un errore di stampa).
        t = RxDistanzaAlternativaSenzaParentesi().Replace(t, "$1");
        // U5: «PRU(IPER) DME», senza lo spazio prima della parentesi: toglierla incollava «PRUDME» (LIRZ LAKOX 3B).
        t = RxSiglaFraParentesiPrimaDiDme().Replace(t, " ");
        // Revisione col committente (carta §6), gruppo 2: le distanze DME scritte in un altro modo. «4NM ALG/AHO TACAN» è la
        // prima stazione (D7); «26 NM ARC OZE DME», «ARC 15 GRO TACAN», «ARC 13 NM BSM» sono l'arco (D2, D3); «At 7.2 TOP DME»
        // e «until 8NM TVS» la distanza (D4, D5); «RDL324/D25» il punto (D6); «5 NM before SME DME» la distanza davanti (D8).
        t = RxDueStazioni().Replace(t, "$1 $2");
        t = RxNumeroPrimaDiArc().Replace(t, "ARC $1 NM $2 DME");
        t = RxArcoSenzaNm().Replace(t, "ARC $1 NM $2 DME");
        t = RxArcoSenzaDme().Replace(t, "ARC $1 NM $2 DME");
        t = RxDistanzaSenzaNm().Replace(t, "$1 NM $2 DME");
        t = RxDistanzaSenzaDme().Replace(t, "$1 NM $2 DME");
        t = RxRadialeDDistanza().Replace(t, "RDL $1/$2 NM $3 VOR");
        t = RxPrimaDi().Replace(t, "NM ");
        // Gruppo 4: «to leave on RDL» = «to be left on» (V2); «Leave KOVAS on TR 165°» = da KOVAS sulla TR 165° (V3);
        // «take off», «takeoff» (V5).
        t = RxDaLasciare().Replace(t, "to be left on");
        t = RxLasciaSullaRotta().Replace(t, "$1 - TR");
        t = RxDecollo().Replace(t, "$1ake-off");
        // L1: «(293° IBLN LOC)» dopo una radiale è un'alternativa; la rotta per quando il VOR non c'è anche (LIPZ: «(TR 218°
        // in case of TES VOR unavailability)», che prima diventava una rotta e poi un punto, TES).
        t = RxLocFraParentesi().Replace(t, "");
        // Passo 4, nelle salite iniziali: «at 3 NM PIS DME or overhead PIN TACAN» è un'alternativa (LIRP); «on DR 160°» è la rotta
        // (LIPZ RWY 04).
        t = RxSopraInAlternativa().Replace(t, "");
        t = RxRottaDr().Replace(t, "TR $1°");
        t = RxSeIlVorNonCe().Replace(t, m => Marca('N', m.Value.Trim().TrimStart('(').TrimEnd(')')));
        t = RxRadialeConLaR().Replace(t, "RDL $1");
        t = RxDvor().Replace(t, "VOR");
        t = RxTacanDistanza().Replace(t, "$1 DME");
        t = RxTacan().Replace(t, "$1 VORTAC");
        t = RxAlternativaNdb().Replace(t, "");
        t = RxRadialeOQdr().Replace(t, "RDL");
        t = RxVorONdb().Replace(t, "VOR");
        // N1 (revisione col committente): il QDR. Come alternativa all'NDB, anche senza «or», si toglie: vale il VOR («RDL 126
        // TRP VOR (QDR 127° TRP NDB)», «TR 047° (RDL 227 TRP VOR) or TR 045° (QDR 225° TRP NDB)», la parentesi che l'AIP non
        // chiude in «(RDL 355 TRP VOR or TR 176° (QDR 356° TRP NDB)», «QDR/RDL 217 TRP NDB/VOR», «TRP VORTAC (TRP NDB)»). Come
        // unico percorso è la radiale dell'NDB («Leave DEC NDB on QDR 012°», «follow QDR 118° DEC NDB»), con la declinazione
        // che l'NDB ha (B1). Le altre forme col QDR continuano a fermare il lettore.
        t = RxQdrNonChiusa().Replace(t, "$1)");
        t = RxQdrAlternativa().Replace(t, "");
        t = RxNdbFraParentesi().Replace(t, "");
        t = RxQdrORdl().Replace(t, "RDL");
        t = RxNdbOVor().Replace(t, "VOR");
        t = RxLasciaLNdb().Replace(t, "$1 NDB - RDL $2 $1");
        t = RxQdr().Replace(t, "RDL $1 $2");
        // B5: «as soon as practicable» dice quando, non dove; «Initial climb procedure completed» è «After take-off» per il
        // disegno (la salita iniziale è una procedura sua: la SID si disegna dal primo punto che si sa calcolare, come
        // sempre dopo il decollo); «Leave AEA VOR on RDL 149» è «AEA VOR, poi la radiale 149 di AEA».
        t = RxAppenaPossibile().Replace(t, m => Marca('N', m.Value.Trim().Trim(',')));
        t = RxFineSalitaIniziale().Replace(t, "After take-off");
        // «to leave on RDL 215° GRO VOR» (LIRS ILOWE 1X): il segno dei gradi va tolto prima, se no resta da solo; «When at
        // D3 DEC TACAN» è «At» (LIEE KOVAS 7A).
        t = RxRadialeColGrado().Replace(t, "RDL $1");
        t = RxQuando().Replace(t, "At");
        t = RxLasciaSullaRadiale().Replace(t, "$1 $2 - RDL $3 $1 VOR");
        // B5: «RDL 273 (TR 093°)» senza la stazione: vale la rotta stampata fra parentesi (la stazione non si indovina);
        // «(74 VIE DME)» fra parentesi è la definizione del punto letta da un altro DME.
        t = RxRadialeSenzaStazione().Replace(t, "TR $1°");
        t = RxDefinizioneDme().Replace(t, " ");
        // B5: «TR 214)», «TR 285 (RDL …»: la rotta anche senza il segno dei gradi; un VORTAC è un VOR per il percorso;
        // «SME VOR to be left on RDL 192 bound to KOLUS»: la radiale è di SME, che la frase stessa nomina.
        t = RxRottaSenzaGradi().Replace(t, m => $"TR {(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)}°");
        t = RxVortac().Replace(t, "$1 VOR");
        t = RxLasciataSullaRadiale().Replace(t, "$1 $2 to be left on RDL $3 $1 VOR");
        t = RxVorDme().Replace(t, "VOR/DME");
        // D1 (revisione col committente): «At 24 NM OZE VOR/DME», «until 3NM VIL VORTAC» è la distanza DME; non il punto
        // «RDL 161/7.2NM TOP VOR/DME», che ha la barra davanti.
        t = RxDistanzaDaUnVor().Replace(t, "$1 NM $2 DME");
        t = RxVorSpezzato().Replace(t, "VOR");
        t = RxTrack().Replace(t, "TR $1°");
        t = RxDmeAbbreviato().Replace(t, "${d} NM ${n} DME"); // «D20 OST DME», «43 NM PNZ TAC»
        t = RxDmePrimaDellaStazione().Replace(t, "$1 NM $2 DME");
        t = RxPrimaDellaStazione().Replace(t, "until ");
        // Revisione 3 (LIRQ RWY 23, BIBNO 1H): «turn right immediately» è la virata; «then passing RDL 308 PRT VOR» è «crossing».
        t = RxSubito().Replace(t, "$1");
        t = RxPassandoLaRadiale().Replace(t, "crossing RDL");
        // Passo 2 della revisione col committente (carta §6, gruppo 5): «non si butta niente». Quote, velocità, autorizzazioni
        // e condizioni non sono pezzi del percorso, ma restano: un segnaposto (⟦Q+2800⟧ la quota, ⟦V-210⟧ la velocità, ⟦N…⟧
        // una nota, ⟦A…⟧ un avviso, ⟦B+1500⟧ «not below» da controllare) che il lettore mette nel tratto dove sta.
        // Una parentesi che vale per una SID sola («(1600 ft or above only for SID NUCRE 6G)», «(left on DR 234° for SID VIC
        // 1V only)»): una nota, com'è.
        t = RxParentesiPerUnaSid().Replace(t, m => Marca('N', m.Groups[1].Value));
        t = RxMca().Replace(t, m => m.Groups["q"].Success ? Marca('Q', "+" + m.Groups["q"].Value) : Marca('N', m.Value.Trim('(', ')', ' ')));
        t = RxIas().Replace(t, m => Marca('V', "-" + m.Groups["v"].Value)
            + (m.Groups["resto"].Value.Trim() is { Length: > 0 } resto ? Marca('N', $"IAS MAX {m.Groups["v"].Value} KT {resto}") : ""));
        t = RxQuotaDaRaggiungere().Replace(t, m => Marca('Q', "+" + m.Groups["q"].Value));
        t = RxIncrociaAUnaQuota().Replace(t, m => $"{m.Groups[1].Value} {Marca('Q', "+" + Quota(m.Groups[2].Value))}"
            + (m.Groups[3].Success ? Marca('N', m.Groups[3].Value) : ""));
        t = RxIncrociaComeAutorizzato().Replace(t, m => $"{m.Groups[1].Value} {Marca('N', m.Groups[2].Value)}");
        // «(to complete the turn at 3500ft or above)» prima di «at … or above», che se no ne prende la quota (LIEE KOVAS 6D).
        t = RxCompletareLaVirata().Replace(t, m => Marca('N', m.Value.Trim().TrimStart('(').TrimEnd(')')));
        t = RxScostamento().Replace(t, m => Marca('N', m.Value.Trim().TrimStart('(').TrimEnd(')')));
        t = RxAUnaQuotaOSopra().Replace(t, m => Marca('Q', "+" + Quota(m.Groups[1].Value)));
        t = RxSeAutorizzato().Replace(t, m => Marca('N', m.Value.Trim().Trim(',').Trim()));
        t = RxLivelloAssegnato().Replace(t, m => Marca('N', m.Value.Trim()));
        t = RxNonSotto().Replace(t, m => Marca('B', "+" + m.Groups[1].Value));
        t = RxCondizioniAVista().Replace(t, m => Marca('N', m.Value.Trim().TrimStart(';').Trim()));
        t = RxSalitaNellAttesa().Replace(t, m => Marca('N', m.Value.Trim().TrimStart(',').Trim()));
        // Q3: «suggested Track 050°» è la TR 050°, e si dice (committente).
        t = RxRottaSuggerita().Replace(t, m => Marca('A', $"«suggested Track {m.Groups[1].Value}°» è una rotta suggerita: disegnata come TR {m.Groups[1].Value}°") + $"TR {m.Groups[1].Value}°");
        // Q4: «Crossing RDL 166 BSM VOR (in any case within 7NM BSM DME) turn left»: la carta segna la virata sul punto R166
        // BSM / D7 BSM, la radiale alla distanza (LIPO OSKOR 6X, 7W).
        // Passo 5: «at 500ft AGL (850ft AMSL)» è la virata a 850 ft; «anyway not further than point RDL 180/14NM CSL VOR/DME (or …)»
        // e «(to be reached not further than 10NM ALG TACAN …)» la fermano (⟦M…⟧); «not before LIN VOR/DME», «not before DER»
        // senza parentesi come «(not before …)».
        t = RxAglAmsl().Replace(t, m => $"passing {m.Groups[2].Value} ft " + Marca('N', m.Value));
        t = RxNonOltre().Replace(t, m => Marca('M', m.Value.Trim().Trim(',').Trim()));
        t = RxNonOltreFraParentesi().Replace(t, m => Marca('M', m.Groups[1].Value));
        t = RxNonPrimaSenzaParentesi().Replace(t, m => Marca('N', m.Value.Trim().Trim(',').Trim()));
        t = RxInOgniCasoEntro().Replace(t, m => $"RDL {m.Groups[1].Value}/{m.Groups[3].Value} NM {m.Groups[2].Value} VOR/DME "
            + Marca('N', $"crossing RDL {m.Groups[1].Value} {m.Groups[2].Value} VOR (in any case within {m.Groups[3].Value} NM {m.Groups[2].Value} DME)"));
        t = RxParentesiDiNota().Replace(t, " ");
        return RxSpazi().Replace(t, " ").Trim();
    }

    /// <summary>Il segnaposto di quel che non è percorso ma resta (passo 2): gli spazi diventano «_», così le regole dopo non lo toccano.</summary>
    private static string Marca(char tipo, string testo) => $" ⟦{tipo}{RxSpazi().Replace(testo.Trim(), "_")}⟧ ";

    /// <summary>La quota come nelle tabelle: «FL 110» → FL110, «2100 ft» → 2100.</summary>
    private static string Quota(string scritta) => RxSpazi().Replace(scritta, "").Replace("ft", "", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Da dove si sale, per le virate a una quota (passo 5, gruppo 6): la fine della pista (la soglia opposta) e la sua quota, o
    /// lo scalo se la pista non si sa; la pendenza in piedi per miglio e come si dice.
    /// </summary>
    public sealed record Departure(Position End, double Elevation, double FeetPerNm, string Gradient, bool FromAerodrome);

    /// <summary>
    /// Un localizzatore (passo 6): dove sta l'antenna (AD 2.19), la rotta vera dell'atterraggio (quella della pista, AD 2.12) e la
    /// declinazione per dire la rotta magnetica.
    /// </summary>
    public sealed record Localizer(Position Antenna, double TrueCourse, double Variation);

    private sealed class Lettore(string testo, Func<string, Position?> punto, Func<string, Station?> stazione, Runway? pista, bool dallaPista = false,
        double? declinazioneDelloScalo = null, Departure? decollo = null, string? quoteMinime = null, Func<string, Localizer?>? localizzatore = null)
    {
        private readonly List<ProcedureLeg> _tratti = [];

        /// <summary>La direzione vera in cui si vola al punto dove si è (passo 6: da qui parte la curva verso il localizzatore).</summary>
        private double? _direzione;

        /// <summary>Il nome che si sta leggendo è definito su un localizzatore («NOGLU (IBRN LOC/12.7NM BCS DME …)», U3).</summary>
        private string? _nomeSulLoc;

        /// <summary>Il percorso fatto dalla fine della pista fino al punto dove si è (NM): la quota stimata è la sua più la pendenza.</summary>
        private double _percorso;

        /// <summary>Si è usciti dalla stazione dove si era, su una sua radiale (passo 5: l'anello di ASPIR 8G se poi ci si torna).</summary>
        private string? _uscitaDa;

        /// <summary>L'ultimo punto è una virata a una quota: «not before …» e «not further than …» lo spostano lungo questa linea.</summary>
        private (Linea Linea, Position Base, double Quota)? _allaQuota;
        private Position? _qui;
        private string? _quiNome;
        private Linea? _linea;
        private (string Stazione, double Raggio)? _arco;
        private string? _virata;
        private double? _declinazione;
        private string? _problema;
        private readonly List<string> _avvisi = [];

        /// <summary>
        /// Una virata dopo che la rotta corrente è stata presa: la rotta non vale più per intercettare («RWY heading until 1 NM
        /// OST DME then turn left to join RDL 278»: dopo la virata non si è più sulla pista). Una rotta nuova la rimette a posto.
        /// </summary>
        private bool _virataDopoLaRotta;

        /// <summary>Il pezzo di prima ha messo un punto: una quota «not below» che segue vale per lui (passo 2).</summary>
        private bool _puntoAppena;

        /// <summary>Quote, velocità e note venute prima del primo punto: vanno nel primo tratto (passo 2).</summary>
        private readonly List<Func<ProcedureLeg, ProcedureLeg>> _inSospeso = [];

        /// <summary>Le approssimazioni fatte leggendo (una prua come rotta): si dicono anche se la procedura si legge.</summary>
        public IReadOnlyList<string> Avvisi => _avvisi;

        /// <summary>
        /// Passo 2 della revisione col committente («non si butta niente»): la quota (Q), la velocità (V), la nota (N) vanno
        /// nel tratto appena letto, o nel primo se vengono prima; un avviso (A) si dice; «not below» (B) vale come quota se
        /// viene subito dopo un punto, se no la virata è a una quota e non si sa dove (passo 5).
        /// </summary>
        private void Marca(char tipo, string testo)
        {
            if (tipo == 'A')
            {
                _avvisi.Add(testo);
                return;
            }
            if (tipo == 'B')
            {
                // Passo 5: «Proceed on TR 078°, not below 1500 ft, turn left»: senza un punto prima è la virata a una quota.
                if (!_puntoAppena)
                {
                    AllaQuota(double.Parse(testo.TrimStart('+'), CultureInfo.InvariantCulture), $"not below {testo.TrimStart('+')} ft");
                    return;
                }
                tipo = 'Q';
            }
            // Passo 5: i vincoli sulla virata a una quota. «not before …» la porta avanti; «not further than …» (M) indietro.
            if ((tipo is 'N' or 'M') && LimiteDelVincolo(testo) is { } limite) Vincolo(limite, nonPrima: tipo == 'N', testo);
            if (tipo == 'M') tipo = 'N';
            Func<ProcedureLeg, ProcedureLeg> cosa = tipo switch
            {
                'Q' => t => t with { Altitude = t.Altitude is null ? testo : $"{t.Altitude} {testo}" },
                'V' => t => t with { Speed = t.Speed is null ? testo : $"{t.Speed} {testo}" },
                _ => t => t with { Notes = t.Notes is null ? testo : $"{t.Notes}; {testo}" },
            };
            if (_tratti.Count > 0) _tratti[^1] = cosa(_tratti[^1]);
            else _inSospeso.Add(cosa);
        }

        public (IReadOnlyList<ProcedureLeg>, string?) Leggi(string? inizio, string? fine = null)
        {
            if (inizio is not null && RxPezzo().Match(testo) is var primo && !(primo.Index == 0 && primo.Groups["nome"].Success && !Parola(primo.Groups["nome"].Value)))
            {
                if ((punto(inizio) ?? stazione(inizio)?.Position) is not { } p)
                    return ([], $"la STAR non dice da dove comincia e {inizio} non è un punto dell'AIP");
                VersoUnNome(inizio, p);
            }
            var resto = new System.Text.StringBuilder();
            var da = 0;
            foreach (Match m in RxPezzo().Matches(testo))
            {
                resto.Append(testo, da, m.Index - da).Append(' ');
                da = m.Index + m.Length;
                Pezzo(m);
                if (_problema is not null) return ([], _problema);
            }
            resto.Append(testo, da, testo.Length - da);
            // V1 (revisione col committente): «Bound to», «Continue on», «Follow» con l'iniziale maiuscola sono le stesse parole;
            // tutte maiuscole no («OR» deve ancora fermare, B2).
            var minuscolo = RxIniziale().Replace(resto.ToString(), m => m.Value.ToLowerInvariant());
            var avanzo = RxRiempitivo().Replace(minuscolo, " ").Trim();
            if (avanzo.Length > 0) return ([], $"non capito: «{avanzo}»");
            if (_arco is not null) return ([], "un arco DME senza la radiale dove finisce");
            // Una SID che non dice dove finisce («At 22 NM OZE VOR/DME turn left to join RDL 312 VIC VOR/DME.», LIPB VIC 6B)
            // finisce sul punto del suo nome, come la STAR che non dice da dove comincia parte dal suo (committente, 23 ottobre).
            if (fine is not null && _linea is not null && !_puntoAppena && _tratti.Count > 0 && _quiNome != fine
                && (punto(fine) ?? stazione(fine)?.Position) is { } doveFinisce)
                VersoUnNome(fine, doveFinisce);
            if (_problema is not null) return ([], _problema);
            if (_tratti.Count == 0) return ([], "nessun punto");
            foreach (var cosa in _inSospeso) _tratti[0] = cosa(_tratti[0]);
            return (_tratti, null);
        }

        private void Pezzo(Match m)
        {
            var g = m.Groups;
            if (g["marcaK"].Success)
            {
                Marca(g["marcaK"].Value[0], g["marcaT"].Value.Replace('_', ' '));
                return;
            }
            _puntoAppena = false;
            if (g["virata"].Success)
            {
                // Il pezzo è «turn left»: conta la parola dopo «turn» (prima si guardava la prima lettera, sempre «t»: tutte a destra).
                // Revisione 2: «turn left/right» (LIPX RWY 22) è una virata senza verso: il verso lo dice la SID.
                var v = g["virata"].Value;
                _virata = v.Contains("left", StringComparison.OrdinalIgnoreCase) ? "L" : v.Contains("right", StringComparison.OrdinalIgnoreCase) ? "R" : null;
                _virataDopoLaRotta = true;
                return;
            }
            if (g["loc"].Success)
            {
                Localizzatore(g["locN"].Value, g["locT"].Success ? Numero(g["locT"]) : g["locT2"].Success ? Numero(g["locT2"]) : null);
                return;
            }
            if (g["lascia"].Success)
            {
                AllaQuota(Numero(g["quota"]), g["lascia"].Value.Trim());
                return;
            }
            if (g["ctrlR"].Success)
            {
                // V6 (revisione col committente): il punto appena calcolato deve stare sulla radiale, entro un miglio.
                var nome = g["ctrlN"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = Numero(g["ctrlR"]);
                if (_qui is not { } qui)
                {
                    _problema = $"«intersection RDL {radiale:000} {nome}» senza un punto da controllare";
                    return;
                }
                var angolo = (Sphere.BearingDeg(s.Position, qui) - radiale - s.Variation) * Math.PI / 180;
                var scarto = Sphere.DistanceNm(s.Position, qui) * Math.Sin(angolo);
                if (Math.Abs(scarto) > 1 || Math.Cos(angolo) <= 0)
                    _problema = $"il punto non sta sulla RDL {radiale:000} {nome} che il testo gli dà ({Math.Abs(scarto):0.0} NM)";
                return;
            }
            if (g["pistaH"].Success)
            {
                if (pista is null)
                {
                    _problema = "«RWY heading» senza la soglia della pista nell'AIP";
                    return;
                }
                _linea = Linea.DallaPista(pista);
                _virataDopoLaRotta = false;
                return;
            }
            if (g["lasciaR"].Success)
            {
                // «LMT VOR/DME to be left on RDL 258 LMT VOR until 14 NM LMT DME»: prima la stazione, poi fuori sulla radiale.
                var nome = g["lasciaN1"].Success ? g["lasciaN1"].Value : g["lasciaN2"].Success ? g["lasciaN2"].Value : g["lasciaN3"].Value;
                if (Stazione(nome) is not { } s) return;
                if (_qui is null || _quiNome != nome) VersoUnNome(nome, s.Position);
                var radiale = Numero(g["lasciaR"]);
                var d = Numero(g["lasciaD"]);
                if (g["lasciaN3"].Value != nome)
                {
                    _problema = $"la radiale di {nome} con la distanza di {g["lasciaN3"].Value}";
                    return;
                }
                _linea = Linea.SuRadiale(nome, s, radiale, versoFuori: true);
                Calcolato(Sphere.Destination(s.Position, radiale + s.Variation, d), $"RDL {radiale:000}/{Dist(d)} NM {nome}", $"{radiale:000}°", ADistanza(nome, radiale, d));
                return;
            }
            if (g["arcoD"].Success)
            {
                var nome = g["arcoN"].Value;
                if (Stazione(nome) is not { } s) return;
                var d = Numero(g["arcoD"]);
                if (_qui is { } qui && Math.Abs(Sphere.DistanceNm(s.Position, qui) - d) > 0.05)
                {
                    // Dall'arco più lontano (17 NM) a quello della procedura (15 NM): sulla stessa radiale.
                    var radiale = Sphere.BearingDeg(s.Position, qui);
                    Calcolato(Sphere.Destination(s.Position, radiale, d), $"RDL {Sphere.Normalize(radiale - s.Variation):000}/{Dist(d)} NM {nome}", null, ADistanza(nome, radiale - s.Variation, d));
                }
                _arco = (nome, d);
                _linea = null;
                return;
            }
            if (g["incrocioR"].Success)
            {
                var nome = g["incrocioN"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = Numero(g["incrocioR"]);
                if (_arco is { } arco)
                {
                    // La fine dell'arco DME: la radiale della stazione dell'arco. Una radiale di un'altra stazione taglia
                    // l'arco altrove, e questo calcolo non la sa: si dice (prima la si prendeva come se fosse del centro).
                    if (nome != arco.Stazione)
                    {
                        // Passo 6 (committente): l'incrocio del cerchio con la radiale dell'altra stazione (LIPB ALESE 6A, VIC 6A:
                        // «join 26 NM ARC OZE DME. Crossing RDL 335 VIC VOR/DME»), quello più vicino lungo l'arco.
                        var centroDellArco = stazione(arco.Stazione)!.Position;
                        var radialeDellAltra = Linea.SuRadiale(nome, s, radiale, versoFuori: true);
                        var incroci = radialeDellAltra.IncrociConUnCerchio(centroDellArco, arco.Raggio)
                            .Where(x => radialeDellAltra.Avanti(s.Position, x) >= 0).ToList();
                        if (incroci.Count == 0)
                        {
                            _problema = $"l'arco di {arco.Stazione} a {Dist(arco.Raggio)} NM non incrocia la RDL {radiale:000} {nome}";
                            return;
                        }
                        var daQui = _qui is { } q ? Sphere.BearingDeg(centroDellArco, q) : 0;
                        var fine = incroci.MinBy(x => Math.Abs(Sphere.Normalize(Sphere.BearingDeg(centroDellArco, x) - daQui + 180) - 180));
                        ChiudiLArco(fine, $"ARC {Dist(arco.Raggio)} NM {arco.Stazione}/RDL {radiale:000} {nome}");
                        return;
                    }
                    ChiudiLArco(Sphere.Destination(s.Position, radiale + s.Variation, arco.Raggio), $"RDL {radiale:000}/{Dist(arco.Raggio)} NM {arco.Stazione}");
                    return;
                }
                if (_linea is null)
                {
                    _problema = $"«crossing RDL {radiale:000} {nome}» senza una rotta da incrociare";
                    return;
                }
                var altra = Linea.SuRadiale(nome, s, radiale, versoFuori: null);
                if (_linea.Incrocio(altra) is not { } p)
                {
                    _problema = $"la rotta non incrocia la RDL {radiale:000} {nome}";
                    return;
                }
                Calcolato(p, $"INT {_linea.Nome}/RDL {radiale:000} {nome}", _linea.Rotta, ARadiale(nome, radiale));
                return;
            }
            if (g["puntoR"].Success)
            {
                var nome = g["puntoN"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = Numero(g["puntoR"]);
                var d = Numero(g["puntoD"]);
                Calcolato(Sphere.Destination(s.Position, radiale + s.Variation, d), $"RDL {radiale:000}/{Dist(d)} NM {nome}", _linea?.Rotta, ADistanza(nome, radiale, d));
                return;
            }
            if (g["lineaR"].Success || g["lineaR2"].Success)
            {
                var nome = g["lineaR"].Success ? g["lineaN"].Value : g["lineaN2"].Value;
                if (Stazione(nome) is not { } s) return;
                var radiale = g["lineaR"].Success ? Numero(g["lineaR"]) : Numero(g["lineaR2"]);
                // B5: sull'arco, «then intercepting RDL 272 CTF VOR turn left …»: una radiale della stazione dell'arco è dove
                // l'arco finisce (UXUTA 1F/1G, NELDA 8A).
                if (_arco is { } arco && arco.Stazione == nome)
                    ChiudiLArco(Sphere.Destination(s.Position, radiale + s.Variation, arco.Raggio), $"RDL {radiale:000}/{Dist(arco.Raggio)} NM {nome}");
                var tr = g["lineaT"].Success ? Numero(g["lineaT"]) : g["lineaT2"].Success ? Numero(g["lineaT2"]) : (double?)null;
                bool? fuori = tr is { } t ? Math.Abs(Sphere.Normalize(t - radiale + 180) - 180) < 90 : null;
                var nuova = Linea.SuRadiale(nome, s, radiale, fuori, tr);
                // Fuori dalla stazione dove si è, sulla sua radiale: se poi si vira e ci si torna, è un anello (ASPIR 8G).
                if (fuori == true && _quiNome == nome) _uscitaDa = nome;
                // B6: da una rotta («on TR 073° until joining RDL 103 CRN VOR», o dalla pista) la radiale si intercetta: il
                // punto dove la si prende, se è davanti, vicino (100 NM) e le due linee non sono quasi parallele. Da un'altra
                // radiale no: due radiali della stessa stazione si incrociano sulla stazione.
                if (_arco is null && !_virataDopoLaRotta && _linea is { Stazione: null } corrente && corrente.Intercetta(nuova, _qui) is { } presa)
                    Calcolato(presa, $"INT {corrente.Nome}/RDL {radiale:000} {nome}", corrente.Rotta, ARadiale(nome, radiale));
                // Passo 8: dal decollo, una radiale quasi parallela alla pista e vicina (LICB NELDA 6E, «proceed runway heading to
                // intercept and follow RDL 231 COM VOR»): l'incrocio non si calcola; la si prende alla fine della pista, e si dice.
                else if (_arco is null && _qui is null && _linea is { DallaSoglia: true } dallaSoglia && decollo is { } dec
                    && Math.Abs(Sphere.Normalize(nuova.Direzione - dallaSoglia.Direzione + 180) - 180) < 10 && nuova.DiLato(dec.End) < 2)
                {
                    // Se la radiale è il prolungamento della pista (LICA, a 0.3 NM) non c'è niente da dire.
                    if (nuova.DiLato(dec.End) > 0.3)
                        _avvisi.Add($"la pista e la RDL {radiale:000} {nome} sono quasi parallele: la radiale si prende alla fine della pista, a {nuova.DiLato(dec.End):0.0} NM");
                    Calcolato(nuova.Proiezione(dec.End), $"INT {dallaSoglia.Nome}/RDL {radiale:000} {nome}", dallaSoglia.Rotta, ARadiale(nome, radiale));
                }
                _linea = nuova;
                _virataDopoLaRotta = false;
                return;
            }
            if (g["rotta"].Success || g["prua"].Success)
            {
                // «on TR 224°» da dove si è; prima del primo punto (una SID appena decollata) non disegna niente.
                // «turn right heading 025°»: una prua; si disegna come la rotta, senza vento, e si dice (committente, 23 ottobre).
                var tr = Numero(g["rotta"].Success ? g["rotta"] : g["prua"]);
                _virataDopoLaRotta = false;
                if (g["prua"].Success)
                    _avvisi.Add($"prua {tr:000}° disegnata come rotta, senza vento");
                if (_qui is { } qui)
                {
                    if ((_declinazione ?? DeclinazioneDelTesto()) is not { } v) return;
                    _linea = Linea.DaUnPunto(qui, tr, v, prua: g["prua"].Success);
                }
                else if (dallaPista && pista is not null && _tratti.Count == 0 && (_declinazione ?? DeclinazioneSenzaProblema()) is { } v)
                {
                    // B6: una SID che comincia con una rotta («Proceed on TR 263° until intercepting …»): dalla soglia della
                    // pista, come «RWY heading». Se la rotta si scosta dalla pista c'è una virata dopo il decollo che non si
                    // disegna: si dice.
                    _linea = Linea.DaUnPunto(pista.Threshold, tr, v, prua: g["prua"].Success, dallaSoglia: true);
                    var scarto = Math.Abs(Sphere.Normalize(tr + v - pista.TrueBearing + 180) - 180);
                    if (scarto > 15)
                        _avvisi.Add($"il primo tratto parte dalla soglia della pista su TR {tr:000}°: la virata dopo il decollo ({scarto:0}°) non è disegnata");
                }
                else
                {
                    _linea = null;
                }
                return;
            }
            if (g["dmeD"].Success)
            {
                var nome = g["dmeN"].Value;
                if (Stazione(nome) is not { } s) return;
                var d = Numero(g["dmeD"]);
                if (_linea is null)
                {
                    _problema = $"«{Dist(d)} NM {nome} DME» senza una rotta";
                    return;
                }
                if (_linea.Stazione == nome)
                {
                    var radiale = _linea.Radiale!.Value;
                    Calcolato(Sphere.Destination(s.Position, radiale + s.Variation, d), $"RDL {radiale:000}/{Dist(d)} NM {nome}", _linea.Rotta, ADistanza(nome, radiale, d));
                }
                else if (_linea.Distanza(s.Position, d, _qui) is { } p)
                {
                    Calcolato(p, $"{_linea.Nome}/{Dist(d)} NM {nome} DME", _linea.Rotta, ADistanza(nome, Sphere.BearingDeg(s.Position, p) - s.Variation, d));
                }
                else if (_linea.Stazione is { } della && della != nome && stazione(della) is { } sd
                    && RxRadialeDi(della).IsMatch(testo, m.Index + m.Length)
                    && Sphere.Destination(sd.Position, _linea.Radiale!.Value + sd.Variation, d) is var q)
                {
                    // U1 (revisione col committente): LIMC LEGLO 4U/4X, «TR 267° (RDL 087 SRN VOR) until 3 NM VOG DME, then turn
                    // left on TR 182° (RDL 182 SRN VOR)»: la radiale di SRN non passa mai a 3 NM da VOG, e il tratto dopo è
                    // ancora di SRN. È la distanza da SRN, stampata male: si legge e si dice.
                    _avvisi.Add($"«{Dist(d)} NM {nome} DME» è un errore di stampa: la RDL {_linea.Radiale:000} {della} non passa a {Dist(d)} NM da {nome}, letto come {Dist(d)} NM {della} DME");
                    Calcolato(q, $"RDL {_linea.Radiale:000}/{Dist(d)} NM {della}", _linea.Rotta, ADistanza(della, _linea.Radiale!.Value, d));
                }
                else
                {
                    _problema = $"la rotta non arriva a {Dist(d)} NM da {nome}";
                }
                return;
            }
            if (g["nome"].Success)
            {
                var nome = g["nome"].Value;
                if (Parola(nome)) return;
                var posizione = g["vor"].Success ? stazione(nome)?.Position ?? punto(nome) : punto(nome) ?? stazione(nome)?.Position;
                if (posizione is null && g["defR"].Success && stazione(g["defN"].Value) is { } s)
                    posizione = Sphere.Destination(s.Position, Numero(g["defR"]) + s.Variation, Numero(g["defD"]));
                if (posizione is null)
                {
                    _problema = $"{nome}: non è un punto né una radioassistenza dell'AIP";
                    return;
                }
                // U3: un punto definito su un localizzatore («NOGLU (IBRN LOC/12.7NM BCS DME …)», LIBR DAMIC 2Y).
                _nomeSulLoc = RxLocNellaDefinizione().Match(m.Value) is { Success: true } sulLoc ? sulLoc.Groups[1].Value : null;
                VersoUnNome(nome, posizione.Value);
                _nomeSulLoc = null;
            }
        }

        /// <summary>
        /// La fine dell'arco DME in corso: un tratto AF dal punto dove si è a <paramref name="fine"/>, col verso dato dai due
        /// rilevamenti dal centro; per una SID che comincia sull'arco si parte da dove l'arco finisce.
        /// </summary>
        private void ChiudiLArco(Position fine, string definizione, string? fix = null)
        {
            var arco = _arco!.Value;
            _arco = null;
            var c = stazione(arco.Stazione)!;
            if (_qui is { } qui)
            {
                var delta = Sphere.Normalize(Sphere.BearingDeg(c.Position, fine) - Sphere.BearingDeg(c.Position, qui));
                _tratti.Add(new ProcedureLeg
                {
                    PathTerminator = "AF", Fix = fix, Computed = fix is null ? definizione : null, Position = fine,
                    ArcCenter = arco.Stazione, ArcRadiusNm = arco.Raggio, Turn = delta < 180 ? "R" : "L",
                    Navaid = arco.Stazione, Radial = Math.Round(Sphere.Normalize(Sphere.BearingDeg(c.Position, fine) - c.Variation), 1), DmeNm = arco.Raggio,
                });
                // Lungo l'arco: il raggio per l'angolo.
                Avanza(fine, arco.Raggio * Math.Min(delta, 360 - delta) * Math.PI / 180);
                // Alla fine dell'arco si vola lungo la tangente (passo 6: di lì parte la curva verso un localizzatore).
                _direzione = Sphere.Normalize(Sphere.BearingDeg(c.Position, fine) + (delta < 180 ? 90 : -90));
                _qui = fine;
                _quiNome = fix;
                _puntoAppena = true;
                _virata = null;
            }
            else if (fix is not null)
            {
                VersoUnNome(fix, fine);
            }
            else
            {
                Calcolato(fine, definizione, null);
            }
        }

        /// <summary>
        /// Il punto di un vincolo, sulla linea della virata a una quota: «not before 0.8 NM BRL DME», «not further than 10 NM ALG DME»
        /// (la distanza davanti), «RDL 180/14 NM CSL» (il punto), «not before LIN VOR/DME» (dove la linea passa più vicina).
        /// «not before DER» non sposta niente: la virata è già dopo la fine della pista.
        /// </summary>
        private Position? LimiteDelVincolo(string testo)
        {
            if (_allaQuota is not { } q || !RxVincolo().IsMatch(testo)) return null;
            if (RxPuntoDelVincolo().Match(testo) is { Success: true } r && stazione(r.Groups["s"].Value) is { } sr)
                return Sphere.Destination(sr.Position, Numero(r.Groups["r"]) + sr.Variation, Numero(r.Groups["d"]));
            if (RxDistanzaDelVincolo().Match(testo) is { Success: true } d && stazione(d.Groups["s"].Value) is { } sd)
                return q.Linea.Distanza(sd.Position, Numero(d.Groups["d"]), q.Base);
            if (RxStazioneDelVincolo().Match(testo) is { Success: true } s && stazione(s.Groups["s"].Value) is { } ss)
                return q.Linea.Proiezione(ss.Position);
            return null;
        }

        /// <summary>
        /// Passo 6 della revisione col committente: il localizzatore. Da una rotta («TR 260° … until joining IBRM LOC», «on track
        /// 060° to join and follow 015° REG LOC») è l'incrocio, come per le radiali (L2); dopo una virata senza una rotta
        /// («crossing RDL 029 ANC VOR turn left to intercept and follow IFA LOC bound to LADUV») è una curva, come sulle carte
        /// (L3): l'arco tangente alla direzione in cui si vola e al localizzatore, dalla parte della virata. Poi si è sul LOC.
        /// </summary>
        private void Localizzatore(string nome, double? stampata)
        {
            if (localizzatore?.Invoke(nome) is not { } l)
            {
                _problema = $"{nome} LOC: un localizzatore che l'AIP non ha";
                return;
            }
            var vera = l.TrueCourse;
            if (stampata is { } tr)
            {
                var scarto = Math.Abs(Sphere.Normalize(tr + l.Variation - l.TrueCourse + 180) - 180);
                if (scarto > 5) _avvisi.Add($"il {nome} LOC stampato a {tr:000}° si scosta di {scarto:0}° dalla pista: vale la rotta stampata");
                vera = tr + l.Variation;
            }
            var loc = Linea.DelLocalizzatore(nome, l, vera);
            if (_linea is { } corrente && !_virataDopoLaRotta && _arco is null)
            {
                if (corrente.Intercetta(loc, _qui) is not { } presa)
                {
                    _problema = $"la rotta non incrocia il {nome} LOC";
                    return;
                }
                Calcolato(presa, $"INT {corrente.Nome}/{nome} LOC", corrente.Rotta);
            }
            else if (_qui is { } qui && _direzione is { } direzione && _virata is { } verso && _arco is null)
            {
                if (Raccordo(qui, direzione, verso == "R", loc) is not { } r)
                {
                    _problema = $"la virata verso il {nome} LOC non si raccorda al localizzatore";
                    return;
                }
                var angolo = Math.Abs(Sphere.Normalize(Sphere.BearingDeg(r.Centro, r.Tangente) - Sphere.BearingDeg(r.Centro, qui) + 180) - 180);
                _tratti.Add(new ProcedureLeg
                {
                    PathTerminator = "RF", Computed = $"INT {nome} LOC", Position = r.Tangente,
                    ArcCenterPosition = r.Centro, ArcRadiusNm = Math.Round(r.Raggio, 2), Turn = verso, Course = loc.Rotta,
                });
                Avanza(r.Tangente, r.Raggio * angolo * Math.PI / 180);
                _qui = r.Tangente;
                _quiNome = null;
                _virata = null;
                _puntoAppena = true;
                _direzione = loc.Direzione;
                if (r.Raggio is < 0.5 or > 4)
                    _avvisi.Add($"il raccordo al {nome} LOC ha un raggio di {r.Raggio:0.0} NM, lontano da una virata vera (circa 1,5 NM a 210 KT)");
            }
            else
            {
                _problema = $"«{nome} LOC» senza una rotta né una virata da cui prenderlo";
                return;
            }
            _linea = loc;
            _virataDopoLaRotta = false;
        }

        /// <summary>
        /// L'arco da <paramref name="p"/>, dove si vola verso <paramref name="direzione"/> (vera), tangente alla linea
        /// <paramref name="loc"/>, girando a destra o a sinistra: il centro, il punto dove tocca la linea e il raggio. Il centro sta
        /// dalla parte della virata, sia rispetto alla direzione di partenza sia rispetto alla linea percorsa dopo.
        /// </summary>
        private static (Position Centro, Position Tangente, double Raggio)? Raccordo(Position p, double direzione, bool destra, Linea loc)
        {
            var k = Math.Cos(p.Lat * Math.PI / 180);
            (double X, double Y) Piano(Position q) => ((q.Lon - p.Lon) * 60 * k, (q.Lat - p.Lat) * 60);
            Position DalPiano(double x, double y) => new(p.Lat + y / 60, p.Lon + x / (60 * k));
            var (dx, dy) = (Math.Sin(direzione * Math.PI / 180), Math.Cos(direzione * Math.PI / 180));
            var (nx, ny) = destra ? (dy, -dx) : (-dy, dx);
            var (ux, uy) = (Math.Sin(loc.Direzione * Math.PI / 180), Math.Cos(loc.Direzione * Math.PI / 180));
            var (mx, my) = (-uy, ux); // a sinistra della linea
            var (ax, ay) = Piano(loc.Origine);
            var lato = -(ax * mx + ay * my);
            var segno = destra ? -1.0 : 1.0;
            var denominatore = segno - (nx * mx + ny * my);
            if (Math.Abs(denominatore) < 1e-6) return null;
            var raggio = lato / denominatore;
            if (raggio <= 0.05) return null;
            var (cx, cy) = (raggio * nx, raggio * ny);
            return (DalPiano(cx, cy), DalPiano(cx - segno * raggio * mx, cy - segno * raggio * my), raggio);
        }

        /// <summary>Il percorso fatto (passo 5): dalla fine della pista al primo punto, poi di punto in punto.</summary>
        private void Avanza(Position p, double? quanto = null)
        {
            if (_qui is { } prima && Sphere.DistanceNm(prima, p) > 0.01) _direzione = Sphere.BearingDeg(prima, p);
            _percorso = _qui is { } qui ? _percorso + (quanto ?? Sphere.DistanceNm(qui, p)) : decollo is { } d ? Sphere.DistanceNm(d.End, p) : 0;
            _allaQuota = null;
            _uscitaDa = null;
        }

        /// <summary>
        /// Passo 5, gruppo 6 (committente): la virata «a <paramref name="quota"/> ft» si disegna dove l'aereo ci arriva salendo con la
        /// pendenza della procedura, contando dalla fine della pista lungo il percorso; se non si è ancora a un punto, dal punto
        /// della rotta più vicino alla fine della pista. Si dice com'è stata disegnata.
        /// </summary>
        private void AllaQuota(double quota, string detto)
        {
            // Passo 8: «After take off at 1200ft turn left» (LICB RWY 05) senza una rotta: dritti dalla pista fino alla quota.
            if (_linea is null && _qui is null && dallaPista && pista is not null) _linea = Linea.DallaPista(pista);
            if (_linea is not { } linea)
            {
                _problema = $"«{detto}» senza una rotta: il punto dove si arriva a {quota:0} ft non si calcola";
                return;
            }
            if (decollo is not { } d)
            {
                _problema = $"«{detto}»: la virata è a una quota, e la pista da cui si sale non si sa";
                return;
            }
            var (partenza, avanti) = _qui is { } qui
                ? (qui, (quota - (d.Elevation + d.FeetPerNm * _percorso)) / d.FeetPerNm)
                : (linea.Proiezione(d.End), (quota - d.Elevation) / d.FeetPerNm);
            var gia = avanti <= 0;
            var p = Sphere.Destination(partenza, linea.Direzione, Math.Max(avanti, 0));
            var percorsoPrima = _percorso;
            var quiPrima = _qui;
            Calcolato(p, $"{quota:0} ft", linea.Rotta, new Fine("CA"));
            _allaQuota = (linea, partenza, quota);
            var dove = quiPrima is null ? $"a {_percorso:0.0} NM dalla fine della pista" : $"{Math.Max(avanti, 0):0.0} NM dopo il punto prima";
            _avvisi.Add(gia
                ? $"virata a {quota:0} ft disegnata sul punto prima: con {d.Gradient} lì si è già a {d.Elevation + d.FeetPerNm * percorsoPrima:0} ft"
                : $"virata a {quota:0} ft disegnata {dove}, salendo con {d.Gradient}{(d.FromAerodrome ? " (la pista non si sa: dalla quota dello scalo)" : "")}");
        }

        /// <summary>
        /// Passo 5: un vincolo sulla virata a una quota appena disegnata. «not before 0.8 NM BRL DME» la porta avanti se cade prima,
        /// «anyway not further than RDL 180/14NM CSL» e «to be reached not further than 10NM ALG TACAN» la portano indietro se cade
        /// dopo. Lungo la stessa linea.
        /// </summary>
        private void Vincolo(Position limite, bool nonPrima, string detto)
        {
            if (_allaQuota is not { } q || _tratti.Count == 0 || _tratti[^1].Position is not { } p) return;
            var oltre = q.Linea.Avanti(q.Base, limite) - q.Linea.Avanti(q.Base, p);
            if (nonPrima ? oltre <= 0 : oltre >= 0) return;
            var spostato = Sphere.Destination(q.Base, q.Linea.Direzione, Math.Max(q.Linea.Avanti(q.Base, limite), 0));
            _tratti[^1] = _tratti[^1] with { Position = spostato, Computed = $"{q.Quota:0} ft ({detto})" };
            _percorso += oltre;
            _qui = spostato;
            _avvisi.Add($"la virata a {q.Quota:0} ft è spostata per «{detto}»");
        }

        private void VersoUnNome(string nome, Position posizione)
        {
            _puntoAppena = true;
            // ASPIR 8G (committente): «Leave PES VOR on RDL 083 (TR 083°), then turn left bound to PES VOR to leave on RDL 230»:
            // fuori sulla radiale e di nuovo sulla stazione, per salire. L'anello è lungo quanto serve per arrivare alla quota delle
            // quote minime della procedura («PES VOR: 5000 FT») dalla quota stimata al primo passaggio.
            if (_quiNome == nome && _uscitaDa == nome && _virata is not null && _linea is { Stazione: { } della } uscita && della == nome && decollo is { } d
                && quoteMinime is not null && Regex.Match(quoteMinime, $@"\b{Regex.Escape(nome)}(?:\s+VOR(?:/DME)?|\s+NDB)?\s*:\s*(\d{{3,5}})\s*FT\b") is { Success: true } mca)
            {
                // La quota sulla stazione: la stimata, o la MCA stampata lì se è più alta («turn right bound to PES VOR/DME (MCA
                // 2500 ft)», nel ramo della salita iniziale della RWY 22 di LIBP: è la MCA della carta).
                var stampata = _tratti.LastOrDefault(t => t.Fix == nome)?.Altitude is { } quotaLi && RxQuotaMinima().Match(quotaLi) is { Success: true } qm
                    ? double.Parse(qm.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                var dallaMca = stampata > d.Elevation + d.FeetPerNm * _percorso;
                var stimata = Math.Max(d.Elevation + d.FeetPerNm * _percorso, stampata);
                var serve = (Numero(mca.Groups[1]) - stimata) / d.FeetPerNm;
                if (serve > 0)
                {
                    var virata = _virata;
                    _virata = null;
                    var fuori = serve / 2;
                    Calcolato(Sphere.Destination(posizione, uscita.Direzione, fuori), $"RDL {uscita.Radiale:000}/{Dist(Math.Round(fuori, 1))} NM {nome}", uscita.Rotta, ADistanza(nome, uscita.Radiale!.Value, Math.Round(fuori, 1)));
                    _virata = virata;
                    _linea = null;
                    _avvisi.Add($"anello sulla RDL {uscita.Radiale:000} {nome} per salire da {stimata:0} ft ({(dallaMca ? "la MCA stampata su " + nome : "stimata, con " + d.Gradient)}) a {mca.Groups[1].Value} ft: fuori {fuori:0.0} NM e ritorno");
                }
            }
            if (_quiNome == nome) return;
            // B5: «until joining ARC 20 NM BAR DME bound to LUXIL»: l'arco finisce sul punto, se il punto sta sull'arco
            // (mezzo miglio); se no l'arco resta aperto e si dice.
            if (_arco is { } arco && stazione(arco.Stazione) is { } centro)
            {
                if (Math.Abs(Sphere.DistanceNm(centro.Position, posizione) - arco.Raggio) <= 0.5)
                {
                    ChiudiLArco(posizione, nome, nome);
                    return;
                }
                // U3 (committente): il punto sta su un localizzatore: l'arco finisce dove incrocia il localizzatore, e di lì sul
                // localizzatore fino al punto. Il raccordo fra l'arco e il localizzatore non si disegna (non se ne sa il raggio): si dice.
                if (_nomeSulLoc is { } x && localizzatore?.Invoke(x) is { } l)
                {
                    var delPunto = Linea.DelLocalizzatore(x, l, l.TrueCourse);
                    var daQui = _qui is { } q ? Sphere.BearingDeg(centro.Position, q) : 0;
                    if (delPunto.IncrociConUnCerchio(centro.Position, arco.Raggio).OrderBy(i => Math.Abs(Sphere.Normalize(Sphere.BearingDeg(centro.Position, i) - daQui + 180) - 180))
                        .FirstOrDefault() is { Lat: not 0 } incrocio)
                    {
                        ChiudiLArco(incrocio, $"ARC {Dist(arco.Raggio)} NM {arco.Stazione}/{x} LOC");
                        _linea = delPunto;
                        _virataDopoLaRotta = false;
                        _avvisi.Add($"l'arco di {arco.Stazione} finisce dove incrocia il {x} LOC: il raccordo fra l'arco e il localizzatore non è disegnato");
                        VersoUnNome(nome, posizione);
                        return;
                    }
                }
                _problema = $"l'arco di {arco.Stazione} a {Dist(arco.Raggio)} NM non passa per {nome}";
                return;
            }
            // Passo 6: sul localizzatore, il punto dopo deve starci (mezzo miglio) e venire dopo il raccordo.
            if (_linea is { Localizzatore: { } sulLoc } loc && _qui is { } daRaccordo)
            {
                if (loc.DiLato(posizione) > 0.5) _avvisi.Add($"{nome} sta a {loc.DiLato(posizione):0.0} NM dal {sulLoc} LOC");
                if (loc.Avanti(daRaccordo, posizione) < 0) _avvisi.Add($"il localizzatore {sulLoc} si prende dopo {nome}");
            }
            var primo = _tratti.Count == 0 && _qui is null;
            var conRotta = _linea?.Rotta is not null && !_virataDopoLaRotta;
            _tratti.Add(new ProcedureLeg
            {
                // Passo 8: dopo un tratto che non finisce su un punto (una quota, una distanza DME, una radiale) «bound to» è un DF.
                // Dopo una virata la rotta di prima non vale più («until 14 NM LMT DME, then turn right bound to SUGEP»): non è
                // un CF con quella rotta (ATCSim la volerebbe).
                PathTerminator = primo ? "IF" : conRotta ? "CF" : _tratti.Count > 0 && FinisceNelVuoto(_tratti[^1]) ? "DF" : "TF",
                Fix = nome,
                Course = primo || !conRotta ? null : _linea!.Rotta,
                Turn = primo ? null : _virata,
            });
            _virata = null;
            Avanza(posizione);
            _qui = posizione;
            _quiNome = nome;
            if (_linea is { Stazione: null }) _linea = null;
        }

        /// <summary>
        /// Passo 8 (committente, per ATCSim): come finisce un tratto verso un punto calcolato. <c>CD</c> a una distanza DME
        /// (<c>FD</c> se parte da un punto con nome), <c>CR</c> su una radiale, <c>CA</c> a una quota; su una prua (o «RWY
        /// heading») <c>VD</c>, <c>VR</c>, <c>VA</c>. Senza, il tratto resta un <c>CF</c> verso il punto.
        /// </summary>
        private sealed record Fine(string Tipo, string? Navaid = null, double? Radiale = null, double? Dme = null);

        private static Fine ADistanza(string navaid, double radiale, double dme) => new("CD", navaid, Math.Round(Sphere.Normalize(radiale), 1), dme);

        private static Fine ARadiale(string navaid, double radiale) => new("CR", navaid, radiale);

        private static bool FinisceNelVuoto(ProcedureLeg t) => t.PathTerminator is "CA" or "VA" or "CD" or "FD" or "VD" or "CR" or "VR";

        private void Calcolato(Position p, string definizione, string? rotta, Fine? fine = null)
        {
            // Lo stesso punto detto due volte di fila: la salita iniziale finisce «until 13NM PRU DME» e la SID comincia «At
            // 13NM PRU DME» (SC1). Un punto solo.
            if (_qui is { } qui && _quiNome is null && Sphere.DistanceNm(qui, p) < 0.05)
            {
                _puntoAppena = true;
                return;
            }
            // Dalla pista (una SID che decolla su «RWY heading» o su una rotta dalla soglia) il primo tratto non è un IF: comincia
            // alla pista e finisce lì (VA a una quota, VD a una distanza DME).
            var primo = _tratti.Count == 0 && _qui is null && !(fine is not null && _linea?.DallaSoglia == true);
            var tipo = fine?.Tipo ?? "CF";
            if (fine is not null && _linea?.Prua == true) tipo = "V" + tipo[1];
            else if (tipo == "CD" && _quiNome is not null) tipo = "FD";
            _tratti.Add(new ProcedureLeg
            {
                PathTerminator = primo ? "IF" : tipo, Computed = definizione, Position = p,
                Course = primo ? null : rotta, Turn = primo ? null : _virata,
                Navaid = primo ? null : fine?.Navaid, Radial = primo ? null : fine?.Radiale, DmeNm = primo ? null : fine?.Dme,
            });
            _virata = null;
            Avanza(p);
            _qui = p;
            _quiNome = null;
            _puntoAppena = true;
        }

        private Station? Stazione(string nome)
        {
            if (stazione(nome) is { } s)
            {
                _declinazione = s.Variation;
                return s;
            }
            _problema = $"{nome}: non è una radioassistenza dell'AIP";
            return null;
        }

        /// <summary>
        /// Come <see cref="DeclinazioneDelTesto"/>, ma senza fermare la lettura se non c'è una stazione. Prima un VOR nominato
        /// nel testo; se non c'è, un NDB (la sua declinazione è quella della B1); se no quella dello scalo (revisione col
        /// committente, gruppo 7: LIRS ILOWE 1W, LIEE KOVAS 2C e LEDRO 2C).
        /// </summary>
        private double? DeclinazioneSenzaProblema()
        {
            foreach (var rx in new[] { RxStazioneNelTesto(), RxNdbNelTesto() })
                foreach (Match m in rx.Matches(testo))
                    if (stazione(m.Groups[1].Value) is { } s)
                        return s.Variation;
            return declinazioneDelloScalo;
        }

        private double? DeclinazioneDelTesto()
        {
            if (DeclinazioneSenzaProblema() is { } v) return v;
            _problema = "una rotta senza una radioassistenza da cui prendere la declinazione";
            return null;
        }
    }

    /// <summary>Una rotta nel piano: una radiale di una stazione, o una rotta da un punto.</summary>
    private sealed class Linea
    {
        public string? Stazione { get; private init; }
        public double? Radiale { get; private init; }
        public required string Nome { get; init; }
        public string? Rotta { get; private init; }
        public Position Origine { get; private init; }
        public double Direzione { get; private init; } // vera, nel verso del volo se noto

        /// <summary>Il punto della linea più vicino a <paramref name="p"/> (passo 5: dove la fine della pista cade sulla rotta).</summary>
        public Position Proiezione(Position p)
        {
            var (dx, dy) = Versore;
            var (px, py) = Piano(p);
            var t = px * dx + py * dy;
            return DalPiano(t * dx, t * dy);
        }

        /// <summary>Quanto <paramref name="p"/> sta avanti a <paramref name="da"/> lungo la linea (NM, negativo se dietro).</summary>
        public double Avanti(Position da, Position p)
        {
            var (dx, dy) = Versore;
            var (ax, ay) = Piano(da);
            var (px, py) = Piano(p);
            return (px - ax) * dx + (py - ay) * dy;
        }
        private bool VersoNoto { get; init; }

        public static Linea SuRadiale(string stazione, Station s, double radiale, bool? versoFuori, double? tr = null) => new()
        {
            Stazione = stazione, Radiale = radiale, Nome = $"RDL {radiale:000} {stazione}", Origine = s.Position,
            Direzione = Sphere.Normalize(radiale + s.Variation + (versoFuori == false ? 180 : 0)), VersoNoto = versoFuori is not null,
            Rotta = tr is { } t ? $"{t:000}°" : versoFuori == true ? $"{radiale:000}°" : versoFuori == false ? $"{Sphere.Normalize(radiale + 180):000}°" : null,
        };

        // Dalla soglia lungo la pista: il decollo e la «RWY heading» (una pista parallela sposta di qualche centinaio di metri).
        public static Linea DallaPista(Runway pista) => new()
        {
            Nome = "RWY heading", Origine = pista.Threshold, Direzione = pista.TrueBearing, VersoNoto = true, Prua = true, DallaSoglia = true,
        };

        /// <summary>Passo 8: la linea comincia alla soglia della pista (il decollo).</summary>
        public bool DallaSoglia { get; private init; }

        /// <summary>Il localizzatore (passo 6): la linea per l'antenna, nel verso dell'atterraggio.</summary>
        public static Linea DelLocalizzatore(string nome, Localizer l, double vera) => new()
        {
            Nome = $"{nome} LOC", Origine = l.Antenna, Direzione = Sphere.Normalize(vera), VersoNoto = true,
            Rotta = $"{Sphere.Normalize(vera - l.Variation):000}°", Localizzatore = nome,
        };

        /// <summary>Il localizzatore di questa linea, se lo è.</summary>
        public string? Localizzatore { get; private init; }

        /// <summary>Dove questa linea taglia il cerchio di <paramref name="centro"/> e <paramref name="raggio"/> (NM): zero, uno o due punti.</summary>
        public IReadOnlyList<Position> IncrociConUnCerchio(Position centro, double raggio)
        {
            var (dx, dy) = Versore;
            var (cx, cy) = Piano(centro);
            var b = -2 * (dx * cx + dy * cy);
            var c = cx * cx + cy * cy - raggio * raggio;
            var disc = b * b - 4 * c;
            if (disc < 0) return [];
            return [.. new[] { (-b - Math.Sqrt(disc)) / 2, (-b + Math.Sqrt(disc)) / 2 }.Distinct().Select(t => DalPiano(t * dx, t * dy))];
        }

        /// <summary>Quanto <paramref name="p"/> sta di lato alla linea (NM).</summary>
        public double DiLato(Position p)
        {
            var (dx, dy) = Versore;
            var (px, py) = Piano(p);
            return Math.Abs(px * dy - py * dx);
        }

        public static Linea DaUnPunto(Position da, double tr, double declinazione, bool prua = false, bool dallaSoglia = false) => new()
        {
            Nome = $"TR {tr:000}°", Origine = da, Direzione = Sphere.Normalize(tr + declinazione), VersoNoto = true, Rotta = $"{tr:000}°",
            Prua = prua, DallaSoglia = dallaSoglia,
        };

        /// <summary>Passo 8: una prua («heading 025°», «RWY heading»), non una rotta: i tratti che finisce sono <c>V…</c>.</summary>
        public bool Prua { get; private init; }

        // Nel piano tangente all'origine (NM): bastano per distanze di decine di miglia, sotto la precisione della prosa.
        private (double X, double Y) Piano(Position p) =>
            ((p.Lon - Origine.Lon) * 60 * Math.Cos(Origine.Lat * Math.PI / 180), (p.Lat - Origine.Lat) * 60);

        private Position DalPiano(double x, double y) =>
            new(Origine.Lat + y / 60, Origine.Lon + x / (60 * Math.Cos(Origine.Lat * Math.PI / 180)));

        private (double X, double Y) Versore => (Math.Sin(Direzione * Math.PI / 180), Math.Cos(Direzione * Math.PI / 180));

        public Position? Incrocio(Linea altra)
        {
            var (dx, dy) = Versore;
            var (ox, oy) = Piano(altra.Origine);
            var (ex, ey) = altra.Versore;
            var det = dx * -ey - dy * -ex;
            if (Math.Abs(det) < 1e-6) return null;
            var t = (ox * -ey - oy * -ex) / det;
            if (Radiale is not null && !VersoNoto && t < 0) t = -t; // una radiale si legge dalla stazione in fuori
            return DalPiano(t * dx, t * dy);
        }

        /// <summary>
        /// Dove questa rotta prende <paramref name="altra"/>: davanti a <paramref name="qui"/> (o all'origine), entro 100 NM, con
        /// almeno 10° fra le due direzioni; se no null (e il tratto resta come prima: dritto al punto dopo).
        /// </summary>
        public Position? Intercetta(Linea altra, Position? qui)
        {
            var angolo = Math.Abs(Sphere.Normalize(Direzione - altra.Direzione + 180) - 180);
            if (angolo < 10 || angolo > 170) return null;
            var (dx, dy) = Versore;
            var (ox, oy) = Piano(altra.Origine);
            var (ex, ey) = altra.Versore;
            var det = dx * -ey - dy * -ex;
            if (Math.Abs(det) < 1e-6) return null;
            var t = (ox * -ey - oy * -ex) / det;
            var tQui = 0.0;
            if (qui is { } q)
            {
                var (qx, qy) = Piano(q);
                tQui = qx * dx + qy * dy;
            }
            if (t <= tQui + 0.1 || t - tQui > 100) return null;
            return DalPiano(t * dx, t * dy);
        }

        /// <summary>Dove la rotta arriva a <paramref name="d"/> NM da <paramref name="centro"/>: il primo davanti a <paramref name="qui"/>.</summary>
        public Position? Distanza(Position centro, double d, Position? qui)
        {
            var (dx, dy) = Versore;
            var (cx, cy) = Piano(centro);
            // |t·v − c|² = d²
            var b = -2 * (dx * cx + dy * cy);
            var c = cx * cx + cy * cy - d * d;
            var disc = b * b - 4 * c;
            if (disc < 0) return null;
            var t1 = (-b - Math.Sqrt(disc)) / 2;
            var t2 = (-b + Math.Sqrt(disc)) / 2;
            var tQui = 0.0;
            if (qui is { } q)
            {
                var (qx, qy) = Piano(q);
                tQui = qx * dx + qy * dy;
            }
            // Davanti, nel verso del volo: dietro non ci si torna (null, e la procedura resta in prosa).
            var t = new[] { t1, t2 }.Where(x => x > tQui - 1e-6).DefaultIfEmpty(double.NaN).Min();
            if (double.IsNaN(t)) return null;
            return DalPiano(t * dx, t * dy);
        }
    }

    private static double Numero(Group g) => double.Parse(g.Value, CultureInfo.InvariantCulture);

    private static string Dist(double d) => d.ToString("0.#", CultureInfo.InvariantCulture);

    // Segnalazioni 2611, B2: AT («AT 8 NM TAG DME»), TKOF («After TKOF»), IAF («TURUD IAF») non dicono niente del percorso.
    // «OR» e «QDR» NO: un'alternativa che il lettore non toglie deve fermarsi, non diventare un tratto.
    private static bool Parola(string s) => s is "RDL" or "TR" or "NM" or "DME" or "VOR" or "ARC" or "INT" or "LOC" or "IAS" or "KT" or "MAX"
        or "ATC" or "FT" or "FL" or "RWY" or "ILS" or "NDB" or "TRL" or "MCL" or "MCA" or "AT" or "TKOF" or "IAF";

    private const string Nav = @"[A-Z]{2,3}";
    private const string D = @"\d+(?:\.\d+)?";

    // I pezzi, in ordine di precedenza: chi comincia prima vince, a parità vince il primo dell'alternativa.
    [GeneratedRegex(
        @"⟦(?<marcaK>[QVNABM])(?<marcaT>[^⟧]*)⟧"
        // Passo 5: la virata a una quota («until leaving 1350 ft», «Passing 4000 ft», «reaching 800ft», «crossing 500 ft», «climbing
        // to 2500 ft»): dove l'aereo ci arriva salendo dalla fine della pista.
        // Passo 8: anche «After take off at 1200ft turn left» (LICB RWY 05).
        + @"|(?<lascia>\b(?:(?:until\s+|till\s+)?(?:[Ll]eaving|[Pp]assing|[Rr]eaching)|[Cc]rossing|climbing\s+to)\s+(?<quota>\d{3,5})\s*(?:FT|ft)\b"
        + @"|\b[Aa]t\s+(?<quota>\d{3,5})\s*(?:FT|ft)\b(?=\s*,?\s*turn\b))"
        + @"|(?<pistaH>\b(?:RWY|runway|RUNWAY)\s+heading\b(?:\s*\(\s*TR\s+\d{3}°\s*\))?)"
        // V6: «at 17 NM CMP DME (intersection RDL 073 OST VOR)»: lo stesso punto detto in un altro modo, da controllare.
        + @"|\(\s*intersection\s+RDL\s+(?<ctrlR>\d{3})\s+(?<ctrlN>" + Nav + @")(?:\s+VOR)?\s*\)"
        // Passo 6: il localizzatore, anche con la rotta stampata («015° REG LOC», «IPTL LOC (TR 253°)»).
        + @"|(?<loc>(?:\b(?<locT>\d{3})°\s+)?\b(?<locN>[A-Z]{3,5})\s+LOC\b(?:\s*\(\s*TR\s+(?<locT2>\d{3})°\s*\))?)"
        + @"|(?:(?<lasciaN1>" + Nav + @")\s+VOR(?:/DME)?\s+to\s+be\s+left\s+on|\bLeave\s+(?<lasciaN2>" + Nav + @")\s+VOR(?:/DME)?\s+on)\s+RDL\s+(?<lasciaR>\d{3})(?:\s+" + Nav + @"\s+VOR)?\s+until\s+(?<lasciaD>" + D + @")\s+NM\s+(?<lasciaN3>" + Nav + @")\s+DME"
        + @"|\bARC\s+(?<arcoD>" + D + @")\s+NM\s+(?<arcoN>" + Nav + @")\s+DME"
        // «Crossing RDL 177 CRN VOR turn left»: anche con la maiuscola (B5: prima diventava una rotta, e l'arco restava aperto).
        + @"|(?:\b[Cc]rossing|\bINT\s+TR\s+\d{3}°\s*/)\s+RDL\s+(?<incrocioR>\d{3})\s+(?<incrocioN>" + Nav + @")(?:\s+VOR(?:/DME)?)?"
        + @"|\b(?:INT\s+)?RDL\s+(?<puntoR>\d{3})\s*/\s*(?<puntoD>" + D + @")\s+NM\s+(?<puntoN>" + Nav + @")\s+VOR/DME"
        + @"|\bTR\s+(?<lineaT>\d{3})°\s*\(\s*RDL\s+(?<lineaR>\d{3})\s+(?<lineaN>" + Nav + @")\s+VOR(?:\s+or\s+RDL\s+\d{3}\s+" + Nav + @"\s+VOR)?\s*\)"
        + @"|\bRDL\s+(?<lineaR2>\d{3})\s+(?<lineaN2>" + Nav + @")(?:\s+VOR(?:/DME)?)?(?:\s*\(\s*RDL\s+\d{3}\s+" + Nav + @"\s+VOR\s*\))?(?:\s*\(\s*TR\s+(?<lineaT2>\d{3})°\s*\))?"
        + @"|\bTR\s+(?<rotta>\d{3})°"
        + @"|\b(?:on\s+)?heading\s+(?<prua>\d{3})°"
        + @"|\b(?<dmeD>" + D + @")\s+NM\s+(?<dmeN>" + Nav + @")\s+DME(?:\s*\(\s*RDL\s+\d{3}\s*/\s*" + D + @"\s+NM\s+" + Nav + @"\s+VOR/DME\s*\))?"
        // «turn left», e al contrario «then left turn on track 109°» (LICR FOSQA 2A: prima il verso restava vuoto).
        + @"|\b(?<virata>[Tt]urn\s+left\s*/\s*right|[Tt]urn\s+(?:left|right|LEFT|RIGHT)|(?:left|right)\s+turn)\b"
        // Un nome, e fra parentesi la sua definizione: una radiale e una distanza (serve se il punto non è nell'AIP), o
        // un'altra («NPC LOC/13 NM NPC DME») che non serve.
        // Un nome: lettere (ELKAP, TAQ) o due lettere e tre cifre, i punti RNAV degli scali (BP401, PH001: B5).
        + @"|\b(?<nome>[A-Z]{2}\d{3}|[A-Z]{2,5})\b(?<vor>\s+(?:VORTAC|VOR(?:/DME)?)\b)?(?:\s*\(\s*(?:INT\s+)?RDL\s+(?<defR>\d{3})\s*/\s*(?<defD>" + D + @")\s+NM\s+(?<defN>" + Nav + @")\s+VOR/DME(?:\s+or\s+[^)]*)?\s*\)|\s*\([^)]*\))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex RxPezzo();

    // Quel che può restare fra i pezzi senza dire niente.
    // Segnalazioni 2611, B5: «until intercepting and following RDL …» = «until joining», «inbound to» = «bound to», «Over TOP
    // VOR proceed …» = «da TOP VOR».
    [GeneratedRegex(@"\b(?:then|and|proceed|on|to|bound|direct|until|till|point|via|join|joining|intercept|follow|at|from|after|take-off|continue|the|be|or|turn|left|right|crossing|maintain|From|Proceed|Turn|Join|At|Then|And|After|Crossing"
        // «Cross EKTOL» a fine riga, con la quota andata sulla riga delle quote (LIBF EKTOL 6A): passare sul punto.
        + @"|intercepting|following|Intercept|Intercepting|inbound|Inbound|Over|over|overflying|cross|climb|mantain)\b|[,;.\-()]",
        RegexOptions.CultureInvariant)]
    private static partial Regex RxRiempitivo();

    [GeneratedRegex(@"\b[A-Z][a-z]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxIniziale();

    [GeneratedRegex(@"\b(" + Nav + @")\s+VOR", RegexOptions.CultureInvariant)]
    private static partial Regex RxStazioneNelTesto();

    [GeneratedRegex(@"\b(" + Nav + @")\s+NDB\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxNdbNelTesto();

    // Una riga dopo il percorso: le quote, le note, la WAYPOINT LIST.
    // U2 (revisione col committente): anche «…: TRL(1)» e la riga che comincia con un punto «RDL 131/34 NM SES VOR/DME (or …)
    // – RDL 131/29 NM …» senza verbi, che va a capo prima dei due punti (LIMJ IDONA 2S), come «FL 70/TRL» della B8.
    [GeneratedRegex(@"^(?:REMARK|Waypoint|R\s?emain|Minimum|\(A\d|\*|(?:INT\s+)?RDL\s*\d{3}\s*/\s*\d)|\b\d{3,5}\s?FT\b|\bFL\s?\d{2,3}\b|:\s*(?:\d|FL|TRL\b)|^[A-Z0-9 /–-]+:", RegexOptions.CultureInvariant)]
    private static partial Regex RxRigaDopo();

    [GeneratedRegex(@"^(?:INT\s+)?RDL\s*\d{3}\s*/\s*\d", RegexOptions.CultureInvariant)]
    private static partial Regex RxRigaColPunto();

    private static Regex RxRadialeDi(string stazione) => new(@"\bRDL\s+\d{3}\s+" + Regex.Escape(stazione) + @"\b", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\b(?:turn|proceed|join|bound|until|then|direct|crossing|intercept)\b")]
    private static partial Regex RxVerbo();

    // La riga dopo un punto che continua il percorso: comincia con un verbo o con «At N NM …»; non con REMARK, Minimum, quote.
    // Passo 2: anche «Passing 4000 ft, anyway not further than …» (LIMF TOP 1B): prima il percorso finiva lì e la virata spariva.
    [GeneratedRegex(@"^(?:At\s+\d|Then\b|Turn\b|Proceed\b|Leave\b|Crossing\b|Continue\b|Join\b|Intercept|Passing\b|Overflying\b|Cross\b)")]
    private static partial Regex RxRigaCheContinua();

    // Revisione 2: anche «When at D3 DEC TACAN» (LIEE KOVAS 7A), che qui è già «D3 DEC VOR».
    [GeneratedRegex(@"\bD(?<d>\d+(?:\.\d+)?)\s+(?<n>[A-Z]{2,3})\s+(?:DME|TACAN|VORTAC|VOR)\b|\b(?<d>\d+(?:\.\d+)?)\s+NM\s+(?<n>[A-Z]{2,3})\s+TAC\b")]
    private static partial Regex RxDmeAbbreviato();

    [GeneratedRegex(@"\bV\s+OR\b")]
    private static partial Regex RxVorSpezzato();

    [GeneratedRegex(@"\bRDL\s+(\d{3})°")]
    private static partial Regex RxRadialeColGrado();

    [GeneratedRegex(@"\btrack\s+(\d{3})°")]
    private static partial Regex RxTrack();

    [GeneratedRegex(@"\((?:MCA|MCL)\s+(?<q>\d+)\s*(?:ft|FT)\s*\)|\(not\s+before[^)]*\)")]
    private static partial Regex RxMca();

    [GeneratedRegex(@"\s+-\s*|\s*-\s+")]
    private static partial Regex RxTrattino();

    [GeneratedRegex(@"\bTR\s?(\d{2})\s(\d)°")]
    private static partial Regex RxNumeroSpezzato();

    [GeneratedRegex(@"(\d)\s?NM\b")]
    private static partial Regex RxNm();

    [GeneratedRegex(@"\b(TR|RDL)(\d)")]
    private static partial Regex RxAttaccato();

    [GeneratedRegex(@"VOR\s*/\s*DME|VOR\s+DME")]
    private static partial Regex RxVorDme();

    [GeneratedRegex(@"(?<=\b(?:DME|TACAN)\s*)\(\s*(?:or\s+)?(?:ARC\s+)?\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,4}\s+(?:DME|TACAN)\s*\)")]
    private static partial Regex RxDistanzaAlternativa();

    [GeneratedRegex(@"\(\s*[A-Z]{2,4}\s*\)\s*(?=DME\b)")]
    private static partial Regex RxSiglaFraParentesiPrimaDiDme();

    // «R199 RCA», «R360/17 NM CAG»: la R da sola, attaccata a tre cifre, prima di una stazione o di una barra.
    [GeneratedRegex(@"\bR(\d{3})(?=\s*/|\s+[A-Z]{2,3}\b)")]
    private static partial Regex RxRadialeConLaR();

    [GeneratedRegex(@"\bDVOR\b")]
    private static partial Regex RxDvor();

    [GeneratedRegex(@"\b(\d+(?:\.\d+)?\s+NM\s+[A-Z]{2,3})\s+TACAN\b")]
    private static partial Regex RxTacanDistanza();

    [GeneratedRegex(@"\b([A-Z]{2,3})\s+TACAN\b")]
    private static partial Regex RxTacan();

    // «or QDR 019° TRP NDB (TR 199°)», «OR QDR 108° CAL NDB»: l'alternativa per l'NDB, intera.
    [GeneratedRegex(@"\s*\b(?:or|OR)\s+QDR\s+\d{3}°?\s+[A-Z]{2,3}\s+NDB\b(?:\s*\(\s*TR\s+\d{3}°\s*\))?")]
    private static partial Regex RxAlternativaNdb();

    [GeneratedRegex(@"\bRDL\s+\d{3}\s*\(\s*TR\s*-?\s*(\d{3})°\s*\)")]
    private static partial Regex RxRadialeSenzaStazione();

    // Anche «(D27 REG DME)» (LICR FOSQA 2A/2X: prima diventava un punto in più a un decimo di miglio).
    [GeneratedRegex(@"\(\s*(?:or\s+)?D?\d+(?:\.\d+)?\s*(?:NM\s+)?[A-Z]{2,4}\s+DME\s*\)")]
    private static partial Regex RxDefinizioneDme();

    // «TR 214)», «TR 285 (RDL …», «(TR-241°)»: la rotta senza i gradi o col trattino.
    [GeneratedRegex(@"\bTR\s*-\s*(\d{3})°?|\bTR\s*(\d{3})(?![\d°])")]
    private static partial Regex RxRottaSenzaGradi();

    [GeneratedRegex(@"\b([A-Z]{2,3})\s+VORTAC\b")]
    private static partial Regex RxVortac();

    [GeneratedRegex(@"\b([A-Z]{2,3})\s+(VOR(?:/DME)?)\s*,?\s+to\s+be\s+left\s+on\s+RDL\s+(\d{3})°?(?!\s*/|\s+[A-Z]{2,3}\b)")]
    private static partial Regex RxLasciataSullaRadiale();

    [GeneratedRegex(@",?\s*\bas\s+soon\s+as\s+(?:practicable|possible)\b,?", RegexOptions.IgnoreCase)]
    private static partial Regex RxAppenaPossibile();

    [GeneratedRegex(@"\bInitial\s+climb\s+(?:procedure\s+)?(?:completed|executed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RxFineSalitaIniziale();

    // «Leave AEA VOR on RDL 149», «Leave CAG VOR on RDL 360 CAG»; non la forma con «until … DME», che ha il suo pezzo.
    [GeneratedRegex(@"\b[Ll]eave\s+([A-Z]{2,3})\s+(VOR(?:/DME)?|VORTAC)\s+on\s+RDL\s+(\d{3})°?(?:\s+\1(?:\s+VOR(?:/DME)?)?)?(?!\s*(?:\([^)]*\)\s*)?until\b)")]
    private static partial Regex RxLasciaSullaRadiale();

    [GeneratedRegex(@"\bRDL\s*/\s*QDR\b")]
    private static partial Regex RxRadialeOQdr();

    [GeneratedRegex(@"\bVOR\s*/\s*NDB\b")]
    private static partial Regex RxVorONdb();

    // «(IAS MAX 210KT)», «(IAS MAX 200 kt during turn)»: una velocità, non un pezzo del percorso.
    [GeneratedRegex(@"\(\s*IAS\s+MAX\s+(?<v>\d+)\s*KT\b(?<resto>[^)]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex RxIas();

    [GeneratedRegex(@"\bRDL\s*(\d{3})\s*(\(\s*TR\s*\d{3}\s*°?\s*\))\s*([A-Z]{3})\s+(VOR|NDB)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxStazioneDopoLaRotta();

    [GeneratedRegex(@"\b(\d{3})\s*deg\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxGradiInParola();

    // «to be reached at or above 4000 ft», «(to be crossed at 2800 ft or above)»: una quota, non un pezzo del percorso.
    // Passo 8: anche «direct to COM VOR to reach at 3500ft or above» (LICB RWY 05); revisione 2: «at 4 NM CAG DME, to be reached
    // not below 1000 ft» (LIEE RWY 32).
    [GeneratedRegex(@",?\(?\s*\bto\s+(?:be\s+(?:reached|crossed)|reach)\s+(?:at\s+(?:or\s+above\s+)?|not\s+below\s+)(?<q>\d+)\s*(?:ft|FT)\b(?:\s+or\s+above)?\s*\)?\s*,?")]
    private static partial Regex RxQuotaDaRaggiungere();

    // Revisione 2 delle frasi non capite (committente, 24 ottobre).
    [GeneratedRegex(@"\b(joining|join|intercept(?:ing)?(?:\s+and\s+follow(?:ing)?)?|follow(?:ing)?)\s+(\d{3})\s+([A-Z]{3})\s+VOR\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeSenzaRdl();

    [GeneratedRegex(@"\bRDL\s*(\d{3})\s*\(\s*TR\s*(\d{3})\s*°?\s*\)\s*bound\s+to\s+([A-Z]{3})(?=\s+VOR)", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeVersoLaStazione();

    // Revisione 3 (LIBD BANAV 8B): «bound to BAR VOR. At 2 NM BAR DME turn left»: si vira 2 NM prima di BAR, sulla radiale su
    // cui si arriva, senza passarci.
    [GeneratedRegex(@"\bbound\s+to\s+([A-Z]{3})\s+VOR(?:/DME)?\s*\.\s*At\s+(?=\d+(?:\.\d+)?\s*NM\s+\1\s+DME\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxPrimaDellaStazione();

    [GeneratedRegex(@"\b([Tt]urn\s+(?:left|right))\s+immediately\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxSubito();

    [GeneratedRegex(@"\b[Pp]assing\s+RDL\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxPassandoLaRadiale();

    [GeneratedRegex(@"\(\s*IAS\s+MAX\s+(\d{3})\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxVelocitaSenzaKt();

    // «At 7 DME BAR» (LIBD): 7 NM BAR DME.
    [GeneratedRegex(@"\b(\d+(?:\.\d+)?)\s+DME\s+([A-Z]{3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDmePrimaDellaStazione();

    // «heading 079° (offset 14°)» (LIPH VIC 5S): una nota.
    [GeneratedRegex(@"\(\s*offset\s+\d+\s*°\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxScostamento();

    // Passo 2 (gruppo 5): «Cross EKTOL at FL 110 or above, as cleared», «Cross RATIR as cleared by ATC».
    [GeneratedRegex(@"\b[Cc]ross\s+([A-Z]{2,5})\s+at\s+(FL\s?\d{2,3}|\d{3,5}\s*(?:ft|FT))\s+or\s+above\b\s*,?\s*(as\s+cleared(?:\s+by\s+ATC)?)?", RegexOptions.CultureInvariant)]
    private static partial Regex RxIncrociaAUnaQuota();

    [GeneratedRegex(@"\b[Cc]ross\s+([A-Z]{2,5})\s+(as\s+cleared(?:\s+by\s+ATC)?)", RegexOptions.CultureInvariant)]
    private static partial Regex RxIncrociaComeAutorizzato();

    // «crossing point RDL 180/6NM CSL VOR/DME at 2100 ft or above» (LIMF TOP 1B).
    [GeneratedRegex(@"\bat\s+(FL\s?\d{2,3}|\d{3,5}\s*(?:ft|FT))\s+or\s+above\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxAUnaQuotaOSopra();

    [GeneratedRegex(@",?\s*\bif\s+cleared\s+by\s+ATC\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxSeAutorizzato();

    [GeneratedRegex(@"\bclimbing\s+to\s+the\s+assigned\s+level\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxLivelloAssegnato();

    [GeneratedRegex(@"\(\s*to\s+complete\s+the\s+turn\s+at\s+\d+\s*(?:ft|FT)\s+or\s+(?:more|above)\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxCompletareLaVirata();

    // Revisione 2: anche in testa alla frase, «Not below 1200 ft turn left» (LIMC RWY 35L).
    [GeneratedRegex(@",?\s*\b[Nn]ot\s+below\s+(\d{3,5})\s*(?:ft|FT)\b\s*,?", RegexOptions.CultureInvariant)]
    private static partial Regex RxNonSotto();

    [GeneratedRegex(@";?\s*\bmaintain\s+visual\s+conditions\s+until\s+passing\s+\d+\s*(?:ft|FT)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxCondizioniAVista();

    // N3: «then climb in the holding pattern until reaching MEL for next AWY segment or FL cleared by Milano ACC» (LIMP OSBUL 7N).
    [GeneratedRegex(@",?\s*\bthen\s+climb\s+in\s+the\s+holding\s+pattern\s+until\s+reaching\s+MEL\b[^.]*", RegexOptions.CultureInvariant)]
    private static partial Regex RxSalitaNellAttesa();

    [GeneratedRegex(@"\bsuggested\s+[Tt]rack\s+(\d{3})°", RegexOptions.CultureInvariant)]
    private static partial Regex RxRottaSuggerita();

    [GeneratedRegex(@"\b[Cc]rossing\s+RDL\s+(\d{3})\s+([A-Z]{2,3})\s+VOR(?:/DME)?\s*\(\s*in\s+any\s+case\s+within\s+(\d+(?:\.\d+)?)\s+NM\s+\2\s+DME\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxInOgniCasoEntro();

    // Il percorso finisce dove comincia una nota sulla stessa riga («bound to URIPI. Minimum climb gradient: …», LIBF URIPI 6A).
    // Passo 4, nelle salite iniziali: anche «. Turn and climb parameters: …», «. Turn as IAS MAX 220 kt», «… (or 7 NM FAL TACAN)
    // Remark: Minimum climb gradient …» senza il punto.
    // «bound to URIPI. Minimum» e sotto «climb gradient: 6.8% …» (LIBF URIPI 6A): la riga dopo è già fuori dal percorso.
    // Revisione 2: anche senza i due punti, che la salita iniziale divisa toglie (LIPY RWY 04, «Remark Minimum climb gradient»).
    [GeneratedRegex(@"(?:\.\s+(?:REMARKS?\b|Minimum\b|Turn\s+and\s+climb\s+parameters\b|Turns?\s+as\s+IAS\b)|\s+Remarks?\s*:|\s+Remarks?\s+(?=Minimum\b)).*$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex RxNotaInCoda();

    [GeneratedRegex(@"(\b\d+(?:\.\d+)?\s+NM\s+[A-Z]{2,3}\s+(?:DME|TACAN))\s+or\s+\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,4}\s+(?:DME|TACAN)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDistanzaAlternativaSenzaParentesi();

    // Passo 5: le quote e i vincoli delle virate a una quota.
    [GeneratedRegex(@"\bat\s+(\d{3,5})\s*ft\s+AGL\s*\(\s*(\d{3,5})\s*ft\s+AMSL\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxAglAmsl();

    // Revisione 2: anche la sola distanza, «until leaving 3600 ft not further than 10NM SME DME» (LIEO).
    [GeneratedRegex(@"(?<!\bto\s+be\s+(?:reached|crossed)\s*),?\s*\b(?:anyway\s+)?not\s+further\s+than\s+(?:point\s+)?(?:RDL\s+\d{3}\s*/\s*\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,3}\s+VOR(?:/DME)?|\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,3}\s+DME)(?:\s*\([^)]*\))?", RegexOptions.CultureInvariant)]
    private static partial Regex RxNonOltre();

    [GeneratedRegex(@"\(\s*(to\s+be\s+(?:reached|crossed)\s+not\s+further\s+than\s+\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,3}\s+DME[^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxNonOltreFraParentesi();

    [GeneratedRegex(@",?\s*\bnot\s+before\s+(?:than\s+)?(?:DER(?:\s+\d{2}[LRC]?)?|RDL\s+\d{3}\s*/\s*\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,3}\s+VOR(?:/DME)?|\d+(?:\.\d+)?\s*NM\s+[A-Z]{2,3}\s+DME|[A-Z]{2,3}\s+VOR(?:/DME)?)", RegexOptions.CultureInvariant)]
    private static partial Regex RxNonPrimaSenzaParentesi();

    [GeneratedRegex(@"\bnot\s+(?:before|further\s+than)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxVincolo();

    [GeneratedRegex(@"\bRDL\s+(?<r>\d{3})\s*/\s*(?<d>\d+(?:\.\d+)?)\s*NM\s+(?<s>[A-Z]{2,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxPuntoDelVincolo();

    [GeneratedRegex(@"\b(?<d>\d+(?:\.\d+)?)\s*NM\s+(?<s>[A-Z]{2,3})\s+DME\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDistanzaDelVincolo();

    [GeneratedRegex(@"\bnot\s+before\s+(?:than\s+)?(?<s>(?!DER\b)[A-Z]{2,3})\s+VOR", RegexOptions.CultureInvariant)]
    private static partial Regex RxStazioneDelVincolo();

    [GeneratedRegex(@"\s*\(([^()]*\bSIDs?\s+[A-Z][^()]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxParentesiPerUnaSid();

    [GeneratedRegex(@"\((?:ATC\s+discretion|to\s+be\s+used\s+only)[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex RxParentesiDiNota();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    // Revisione col committente (carta §6): le forme di Normalizza.
    [GeneratedRegex(@"\b(?:ARC|arc)\s*(\d)", RegexOptions.CultureInvariant)]
    private static partial Regex RxArcoAttaccato();

    [GeneratedRegex(@"\btr\s+(\d{3})", RegexOptions.CultureInvariant)]
    private static partial Regex RxRottaMinuscola();

    [GeneratedRegex(@"\b(?!VOR\b|NDB\b|RDL\b|QDR\b)([A-Z]{3})/(?!DME\b|VOR\b|NDB\b)[A-Z]{3}\s+(TACAN|DME)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDueStazioni();

    [GeneratedRegex(@"\b(\d+(?:\.\d+)?)\s+NM\s+ARC\s+([A-Z]{2,3})\s+DME\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxNumeroPrimaDiArc();

    [GeneratedRegex(@"\bARC\s+(\d+(?:\.\d+)?)\s+([A-Z]{2,3})\s+(?:DME|TACAN|VORTAC|VOR)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxArcoSenzaNm();

    [GeneratedRegex(@"\bARC\s+(\d+(?:\.\d+)?)\s+NM\s+([A-Z]{2,3})\b(?!\s*(?:DME|TACAN|VORTAC|VOR|TAC)\b|\s*[(/])", RegexOptions.CultureInvariant)]
    private static partial Regex RxArcoSenzaDme();

    [GeneratedRegex(@"(?<=\b(?:[Aa]t|until|till)\s+)(\d+(?:\.\d+)?)\s+([A-Z]{2,3})\s+DME\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDistanzaSenzaNm();

    [GeneratedRegex(@"(?<=\b(?:[Aa]t|until|till)\s+)(\d+(?:\.\d+)?)\s+NM\s+([A-Z]{2,3})\b(?!\s*(?:DME|TACAN|VORTAC|VOR|TAC|NDB)\b|\s*[(/])", RegexOptions.CultureInvariant)]
    private static partial Regex RxDistanzaSenzaDme();

    [GeneratedRegex(@"\bRDL\s*(\d{3})\s*/\s*D(\d+(?:\.\d+)?)\s+([A-Z]{2,3})\s+VOR\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeDDistanza();

    [GeneratedRegex(@"\bNM\s+before\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RxPrimaDi();

    [GeneratedRegex(@"(?<![\d./])(?<!/\s*)(\d+(?:\.\d+)?)\s+NM\s+([A-Z]{2,3})\s+VOR(?:/DME)?\b(?!\s*/)", RegexOptions.CultureInvariant)]
    private static partial Regex RxDistanzaDaUnVor();

    [GeneratedRegex(@"\b[Ww]hen\s+at\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxQuando();

    [GeneratedRegex(@"\bto\s+leave\s+on\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDaLasciare();

    [GeneratedRegex(@"\b[Ll]eave\s+([A-Z]{2,5})\s+on\s+TR\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxLasciaSullaRotta();

    [GeneratedRegex(@"\b([Tt])ake\s?off\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDecollo();

    [GeneratedRegex(@"(\(\s*RDL\s+\d{3}\s+[A-Z]{2,3}\s+VOR)\s+or\s+TR\s+\d{3}°\s*\(\s*QDR\s+\d{3}°?\s+[A-Z]{2,3}\s+NDB\s*\)(?!\s*\))", RegexOptions.CultureInvariant)]
    private static partial Regex RxQdrNonChiusa();

    [GeneratedRegex(@"\s*(?:\bor\s+TR\s+\d{3}°\s*)?\(\s*QDR\s+\d{3}°?\s+[A-Z]{2,3}\s+NDB\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxQdrAlternativa();

    [GeneratedRegex(@"\s*\(\s*[A-Z]{2,3}\s+NDB\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxNdbFraParentesi();

    [GeneratedRegex(@"\bQDR\s*/\s*RDL\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxQdrORdl();

    [GeneratedRegex(@"\bNDB\s*/\s*VOR\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxNdbOVor();

    [GeneratedRegex(@"\b[Ll]eave\s+([A-Z]{2,3})\s+NDB\s+on\s+QDR\s+(\d{3})°?", RegexOptions.CultureInvariant)]
    private static partial Regex RxLasciaLNdb();

    [GeneratedRegex(@"\bQDR\s+(\d{3})°?\s+([A-Z]{2,3})\s+NDB\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxQdr();

    [GeneratedRegex(@"\s*\(\s*\d{3}°\s+[A-Z]{3,5}\s+LOC\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxLocFraParentesi();

    [GeneratedRegex(@"\s+or\s+overhead\s+[A-Z]{2,3}\s+(?:TACAN|VORTAC|VOR(?:/DME)?|DME|NDB)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxSopraInAlternativa();

    [GeneratedRegex(@"\bDR\s+(\d{3})°", RegexOptions.CultureInvariant)]
    private static partial Regex RxRottaDr();

    [GeneratedRegex(@"\s*\(\s*TR\s+\d{3}°\s+in\s+case\s+of[^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxSeIlVorNonCe();

    // Revisione col committente (carta §6): le ricuciture del testo rotto dal PDF.
    [GeneratedRegex(@"^\s*(?:\([^)]*\)\s*)*(?<lettere>[A-Z](?:[A-Z]| (?=[A-Z]))*)", RegexOptions.CultureInvariant)]
    private static partial Regex RxLettereInTesta();

    [GeneratedRegex(@"(?<![A-Za-z])([A-Z])\s([a-z]{2,})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxParolaSpezzata();

    // Le coppie anche sovrapposte («take-off pro ceed»: «off pro» non deve nascondere «pro ceed»).
    [GeneratedRegex(@"\b(?=([a-z]{2,})\s([a-z]{2,})\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxParolaSpezzataMinuscola();

    [GeneratedRegex(@"\bRDL\s*(\d{3})([A-Z]{2,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxSiglaAttaccataAllaRadiale();

    [GeneratedRegex(@"\bRDL\s+(\d[\dA-Z ]{3,10}?)\s+VOR\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeSparsa();

    [GeneratedRegex(@"^(\d{3})([A-Z]{2,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex RxCifreESigla();

    [GeneratedRegex(@"\bR\s(\d{3})\s+([A-Z]{2,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeRConLoSpazio();

    [GeneratedRegex(@"\bR(\d{3})\s*\(\s*TR\s*(\d{3})°\s*\)\s*([A-Z]{2,3})\s+(D?VOR)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeRStazioneDopo();

    // Gruppo 7: gli errori di stampa che lo stesso testo corregge.
    [GeneratedRegex(@"\bTR\s*(\d{3})""", RegexOptions.CultureInvariant)]
    private static partial Regex RxGradiComeVirgolette();

    [GeneratedRegex(@"\bVOR//DME\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDoppiaBarra();

    [GeneratedRegex(@"\bRDL/\s*(\d{3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeBarra();

    [GeneratedRegex(@"\b([A-Z]{2,3})-\s+(D?VOR)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxTrattinoAttaccato();

    [GeneratedRegex(@"(\bTR\s+\d{3}°)\s*…\s*(?=\()", RegexOptions.CultureInvariant)]
    private static partial Regex RxPuntini();

    // «LIBRO – 105° (RDL 285 CTF VOR)»: la rotta senza TR, se la radiale fra parentesi dice la stessa cosa.
    [GeneratedRegex(@"(?<=[-–]\s*)(\d{3})°(\s*\(\s*RDL\s+(\d{3})\s+[A-Z]{2,3}\s+VOR)", RegexOptions.CultureInvariant)]
    private static partial Regex RxRottaSenzaTr();

    [GeneratedRegex(@"\b([A-Z]{2,3})\s+DNE\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDne();

    [GeneratedRegex(@"\s*\(\s*RDL\s+(\d{3})\s+VOR\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeSenzaStazioneFraParentesi();

    // «TR 199° (RDL 019 VOR) bound GRO VOR»: la radiale senza la sigla, che la frase nomina subito dopo.
    [GeneratedRegex(@"\bTR\s+(\d{3})°\s*\(\s*RDL\s+(\d{3})\s+VOR\s*\)(\s*bound\s+(?:to\s+)?)([A-Z]{2,3})(\s+VOR)", RegexOptions.CultureInvariant)]
    private static partial Regex RxRadialeSenzaSigla();
}

/// <summary>
/// Distanze, rotte e punti di arrivo sulla sfera, come <c>Aip.Aurora.Sphere</c>: copiate qui perché l'importer si porta via
/// da solo (carta F6 §3) e non dipende dal converter.
/// </summary>
internal static class Sphere
{
    private const double RaggioNm = 6371008.8 / 1852.0;
    private const double Rad = Math.PI / 180.0;

    public static double DistanceNm(Position a, Position b)
    {
        var dLat = (b.Lat - a.Lat) * Rad;
        var dLon = (b.Lon - a.Lon) * Rad;
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RaggioNm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static double BearingDeg(Position a, Position b)
    {
        var dLon = (b.Lon - a.Lon) * Rad;
        var y = Math.Sin(dLon) * Math.Cos(b.Lat * Rad);
        var x = Math.Cos(a.Lat * Rad) * Math.Sin(b.Lat * Rad) - Math.Sin(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Cos(dLon);
        return Normalize(Math.Atan2(y, x) / Rad);
    }

    public static Position Destination(Position from, double bearingDeg, double distanceNm)
    {
        var d = distanceNm / RaggioNm;
        var b = bearingDeg * Rad;
        var lat1 = from.Lat * Rad;
        var lon1 = from.Lon * Rad;
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(b));
        var lon2 = lon1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));
        return new Position(lat2 / Rad, (lon2 / Rad + 540) % 360 - 180);
    }

    public static double Normalize(double deg)
    {
        var r = deg % 360.0;
        return r < 0 ? r + 360.0 : r;
    }
}
