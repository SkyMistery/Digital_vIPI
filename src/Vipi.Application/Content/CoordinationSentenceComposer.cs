using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dati risolti per comporre una frase di coordinamento (una per riga CoP).
/// <para><c>record</c> e non <c>class</c>: la parte che non dipende dalla riga — mittente, destinatario,
/// aeroporto — si risolve una volta in <c>CoordinationSentences.BuildData</c> e poi si completa con <c>with</c>.
/// Ricopiarne otto campi a mano sarebbe otto occasioni di dimenticarne uno.</para></summary>
public sealed record CoordinationSentenceData
{
    public required string OwnerName { get; init; }
    public required string TargetName { get; init; }
    /// <summary>Codice settore (es. WS2/ES); omesso se <see cref="OmitTargetCode"/>.</summary>
    public string? TargetCode { get; init; }
    /// <summary>True per target APP/TWR: nessun codice nella frase.</summary>
    public bool OmitTargetCode { get; init; }
    public required string AirportName { get; init; }
    public required string AirportIcao { get; init; }
    /// <summary>Tipo di flusso: guida la relazione aeroporto (arrivo = «con destinazione», partenza = «in partenza da»).</summary>
    public TransferFlowKind Kind { get; init; } = TransferFlowKind.Arrival;
    public LevelConstraint? Constraint { get; init; }
    /// <summary>Stato verticale ({stato}): parola «stabile/in discesa/in salita». INDIPENDENTE dal vincolo di livello.
    /// Unspecified → nessuna parola.</summary>
    public TransferVerticalState VerticalState { get; init; } = TransferVerticalState.Unspecified;
    /// <summary>Livello strutturato: valore/unità/testo speciale/parità. La frase costruisce da sé la
    /// fraseologia («a livello 150 o livello inferiore dispari», «per un livello pari», «per aerovia»).</summary>
    public int? LevelValue { get; init; }
    public LevelUnit LevelUnit { get; init; } = LevelUnit.Fl;
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public required string Point { get; init; }
    /// <summary>La CATENA delle condizioni, dalla capofila dell'outline alla riga stessa: una riga senza
    /// varianti ha un elemento solo. Le clausole presenti sono unite da « e » nell'ordine della catena, così la
    /// frase dice l'intera condizione sotto cui l'accordo vale.</summary>
    public IReadOnlyList<ConditionClause> Conditions { get; init; } = Array.Empty<ConditionClause>();

    /// <summary>Faccetta trasferimento: quando c'è, la frase cambia forma («autorizza … e lo trasferisce»)
    /// invece di limitarsi ad allungarsi. <see cref="TransferHandoffFacet.None"/> ⇒ frase identica a prima,
    /// parola per parola.</summary>
    public TransferHandoffFacet Facet { get; init; } = TransferHandoffFacet.None;

    /// <summary>
    /// La frase si dice dalla parte di CHI RICEVE («{target} riceve da {owner} …») invece che di chi cede.
    /// <para>⚠️ Non ribalta gli slot: <see cref="OwnerName"/> resta chi cede e <see cref="TargetName"/> chi
    /// riceve. Sceglie solo un altro template, cioè un altro ordine delle parole.</para>
    /// </summary>
    public bool IsIncoming { get; init; }
}

/// <summary>Compone la frase di coordinamento sostituendo i placeholder del template. Funzione pura.</summary>
public static class CoordinationSentenceComposer
{
    /// <summary>
    /// La frase CAPOFILA di una tabella: chi trasferisce a chi, che traffico, e basta. Niente livello e niente
    /// punto — quelli sono ciò che la tabella dice riga per riga, e anticiparne uno vorrebbe dire eleggere una
    /// riga a rappresentante delle altre.
    /// <para>Passa dagli stessi slot della frase distesa (<c>{owner}</c>, <c>{target}</c>, <c>{airport}</c>),
    /// quindi la lingua resta una sola e le vLOA la ottengono in inglese senza codice dedicato.</para>
    /// </summary>
    public static string ComposeLead(CoordinationSentenceTemplate tpl, CoordinationSentenceData d) =>
        Normalize((d.IsIncoming ? tpl.TemplateLeadReceive : tpl.TemplateLead)
            .Replace("{owner}", d.OwnerName)
            .Replace("{target}", Target(tpl, d))
            .Replace("{airport}", Airport(tpl, d)));

    public static string Compose(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        // Stato verticale scelto a mano (indipendente dal vincolo di livello): «a 130 o inferiore» non è una discesa.
        var stato = d.VerticalState switch
        {
            TransferVerticalState.Descending => tpl.Stato.Descending,
            TransferVerticalState.Climbing => tpl.Stato.Climbing,
            TransferVerticalState.Level => tpl.Stato.Level,
            _ => "",
        };

        var fl = BuildFl(tpl, d);

        var target = Target(tpl, d);
        var airport = Airport(tpl, d);
        var point = ResolvePoint((d.Point ?? "").Trim(), tpl);

        // Due dimensioni indipendenti, quindi quattro template e non due `if` annidati:
        //   FACCETTA  — con un trasferimento distinto la frase cambia VERBO («autorizza … e lo trasferisce»);
        //   DIREZIONE — dalla parte di chi riceve la testa si rovescia («{target} riceve da {owner} …»).
        // La riga senza faccetta e uscente resta la forma storica: è ciò che tiene identiche, parola per parola,
        // le righe ACC↔ACC già scritte.
        var hasHandoff = d.Facet.Kind != TransferHandoffKind.Unspecified;
        var template = (hasHandoff, d.IsIncoming) switch
        {
            (true, true) => tpl.TemplateClearedReceive,
            (true, false) => tpl.TemplateCleared,
            (false, true) => tpl.TemplateReceive,
            (false, false) => tpl.Template,
        };
        var s = template
            .Replace("{owner}", d.OwnerName)
            .Replace("{target}", target)
            .Replace("{airport}", airport)
            .Replace("{stato}", stato)
            .Replace("{fl}", fl)
            .Replace("{point}", point)
            .Replace("{handoff}", hasHandoff ? TransferHandoffText.Place(tpl, d.Facet.Kind, d.Facet.Label) : "")
            .Replace("{handoffLevel}", hasHandoff
                ? TransferHandoffText.Level(tpl, d.Facet.LevelValue, d.Facet.LevelUnit, d.Facet.LevelConstraint)
                : "");

        // Condizione prima, poi le code separate da virgola: «… passando FL110 in discesa con pista 16R in uso,
        // a 250 kt o inferiore, comunicazioni su AVN.» La condizione resta dov'era, così le frasi senza faccetta
        // non si spostano di una virgola.
        var withCondition = AppendCondition(Normalize(s), BuildCondition(tpl, d));
        return AppendTail(withCondition,
            TransferHandoffText.Speed(tpl, d.Facet.SpeedValue, d.Facet.SpeedConstraint),
            BuildComms(tpl, d));
    }

    /// <summary>Il destinatario come lo scrive il template: col codice di posizione quando ne ha uno.</summary>
    private static string Target(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        var code = (d.TargetCode ?? "").Trim();
        return (d.OmitTargetCode || code.Length == 0)
            ? tpl.TargetNoCode.Replace("{name}", d.TargetName)
            : tpl.TargetWithCode.Replace("{name}", d.TargetName).Replace("{code}", code);
    }

    /// <summary>La relazione con l'aeroporto: arrivo = «con destinazione», partenza = «in partenza da», il resto
    /// neutro. Condivisa fra la frase distesa e la capofila, che devono dirlo allo stesso modo.</summary>
    private static string Airport(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        var t = d.Kind switch
        {
            TransferFlowKind.Departure => tpl.AirportDeparture,
            TransferFlowKind.Arrival => tpl.AirportArrival,
            _ => tpl.Airport,   // overflight/VFR/altro: relazione neutra
        };
        return t.Replace("{name}", d.AirportName).Replace("{icao}", d.AirportIcao);
    }

    // Le parole del trasferimento stanno in TransferHandoffText: le usa anche la derivazione per riempire le
    // colonne della tabella, e una seconda copia qui sarebbe la solita coppia da tenere d'accordo a mano.
    private static string BuildComms(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        var where = TransferHandoffText.CommsPlace(tpl, d.Facet);
        return where.Length == 0 ? "" : tpl.Handoff.Comms.Replace("{handoff}", where);
    }

    // Code separate da virgola, inserite prima del punto finale.
    private static string AppendTail(string s, params string[] clauses)
    {
        var tail = string.Join(", ", clauses.Where(c => c.Length > 0));
        if (tail.Length == 0) return s;
        return s.EndsWith(".") ? $"{s[..^1]}, {tail}." : $"{s}, {tail}";
    }

    /// <summary>
    /// Clausola condizione appesa a fine frase. Appesa qui e non via placeholder del Template, così vale anche
    /// per i template personalizzati caricati da file (che non hanno <c>{condition}</c>).
    /// <para><b>Cumula la CATENA</b>, non solo la riga: nell'outline delle varianti un'eccezione di «pista 07»
    /// vale solo dentro la pista 07, e la frase viaggia da sola nella prosa del documento — senza il rientro
    /// che in tabella dà il contesto. Dicendo la sola «R403B attiva» perderebbe la metà che la rende vera.</para>
    /// <para>Una riga che scavalca le alternative premette il proprio marcatore («in ogni caso, …»): senza,
    /// il lettore la scambierebbe per un'alternativa in più.</para>
    /// </summary>
    private static string BuildCondition(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        // La catena si FONDE in una clausola sola prima di diventare parole. Una condizione ereditata più la
        // propria sono una condizione unica in AND, e la fraseologia approvata sa già dirla: «con pista 07 in
        // uso e R403B attiva». Comporre un pezzo per livello e poi unirli ripeteva la preposizione — «con pista
        // 07 in uso E CON R403B attiva» — che è italiano storto e in inglese non va meglio.
        var merged = Merge(tpl, d.Conditions);
        if (merged.IsEmpty) return "";

        var text = string.Join($" {tpl.Condition.Join} ", ClausesOf(tpl, merged));
        // Virgola dopo il marcatore: senza, «in ogni caso in condizione traffico militare» accosta due
        // preposizioni e si legge male. Visto reso, non previsto.
        return d.Facet.IsGroupWide ? $"{tpl.GroupWide}, {text}" : text;
    }

    // Fonde i livelli dimensione per dimensione. Due piste (o due aree) su livelli diversi sono un caso
    // limite: si elencano unite dalla stessa congiunzione, perché restano un AND.
    private static ConditionClause Merge(CoordinationSentenceTemplate tpl, IReadOnlyList<ConditionClause> chain)
    {
        string? Join(Func<ConditionClause, string?> pick)
        {
            var parts = chain.Select(pick).Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToList();
            return parts.Count == 0 ? null : string.Join($" {tpl.Condition.Join} ", parts);
        }
        return new ConditionClause(Join(c => c.Runway), Join(c => c.Area), Join(c => c.Custom));
    }

    // Le tre dimensioni di UNA clausola. Pista+area insieme usano la forma dedicata «con pista X in uso e Y
    // attiva» (fraseologia approvata), che non è la semplice unione delle due.
    private static IEnumerable<string> ClausesOf(CoordinationSentenceTemplate tpl, ConditionClause c)
    {
        var rwy = (c.Runway ?? "").Trim();
        var area = (c.Area ?? "").Trim();
        var custom = (c.Custom ?? "").Trim();

        if (rwy.Length > 0 && area.Length > 0)
            yield return tpl.Condition.RunwayAndArea.Replace("{runway}", rwy).Replace("{area}", area).Trim();
        else if (rwy.Length > 0)
            yield return tpl.Condition.Runway.Replace("{label}", rwy).Trim();
        else if (area.Length > 0)
            yield return tpl.Condition.Area.Replace("{label}", area).Trim();

        if (custom.Length > 0)
            yield return tpl.Condition.Custom.Replace("{label}", custom).Trim();
    }

    // Inserisce la clausola prima del punto finale («… su VALMA con pista RWY 16 in uso.»); senza punto finale, appende.
    private static string AppendCondition(string s, string clause)
    {
        if (clause.Length == 0) return s;
        return s.EndsWith(".") ? $"{s[..^1]} {clause}." : $"{s} {clause}";
    }

    // Fraseologia del livello ({fl}): il vincolo è reso a parole («o livello inferiore/superiore»), la parità
    // come parola finale. Le parole vengono dal template (tpl.Level) → lingua-neutro (IT default, EN per le vLOA).
    // Special → testo grezzo («per aerovia»). Con valore → «a livello N [o livello …] [parità]».
    // Senza valore → «per un livello {parità}» se c'è parità, altrimenti "" (nessun livello da dire).
    private static string BuildFl(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        if (d.Constraint == LevelConstraint.Special)
            return string.IsNullOrWhiteSpace(d.LevelSpecial) ? "" : d.LevelSpecial.Trim();

        var lvl = tpl.Level;
        var parityWord = d.Parity switch
        {
            LevelParity.Even => lvl.ParityEven,
            LevelParity.Odd => lvl.ParityOdd,
            _ => "",
        };

        if (d.LevelValue is int v)
        {
            var body = (d.LevelUnit == LevelUnit.Fl ? lvl.FlBody : lvl.FtBody).Replace("{v}", v.ToString());
            var bound = d.Constraint switch
            {
                LevelConstraint.AtOrBelow => " " + lvl.OrBelow,
                LevelConstraint.AtOrAbove => " " + lvl.OrAbove,
                _ => "",
            };
            // L'ordine delle parole lo decide il TEMPLATE, non questa riga: in inglese la parità va fra
            // parentesi dopo il livello, in italiano segue come aggettivo. Concatenare qui produceva
            // «at level 260 even».
            return parityWord.Length == 0
                ? body + bound
                : lvl.WithParity.Replace("{body}", body + bound).Replace("{parity}", parityWord);
        }

        // Nessun valore numerico: solo la parità produce una frase.
        return parityWord.Length == 0 ? "" : lvl.ForLevelParity.Replace("{parity}", parityWord);
    }

    // Risolve il testo del punto dal CoP (case-insensitive). Tre casi distinti (NON condividono lo stesso
    // fallback: il CoP vuoto = «nessun punto indicato», «ALL» = istruzione esplicita «tutti i punti»):
    //   vuoto                → FallbackMissingPoint (es. «—»: l'editor non l'ha compilato);
    //   «ALL»                → FallbackAllPoints («tutti i punti»);
    //   «ALL to X»           → FallbackAllToward («tutti i punti verso X»), X = nazione/FIR come scritto;
    //   qualsiasi altro CoP  → il CoP letterale.
    private static readonly Regex AllPointsPattern =
        new(@"^ALL(?:\s+to\s+(?<dest>\S.*))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string ResolvePoint(string cop, CoordinationSentenceTemplate tpl)
    {
        if (cop.Length == 0) return tpl.FallbackMissingPoint;
        var m = AllPointsPattern.Match(cop);
        if (!m.Success) return cop;
        var dest = m.Groups["dest"].Value.Trim();
        return dest.Length == 0
            ? tpl.FallbackAllPoints
            : tpl.FallbackAllToward.Replace("{dest}", dest);
    }

    // Collassa spazi multipli (da placeholder vuoti) e toglie lo spazio prima della punteggiatura.
    private static string Normalize(string s) =>
        Regex.Replace(Regex.Replace(s, @"\s+([.,;:])", "$1"), @"\s{2,}", " ").Trim();
}

/// <summary>
/// La faccetta TRASFERIMENTO di una riga, passata in blocco alla composizione della frase.
/// <para>Raggruppata in un tipo e non spalmata in nove parametri: <see cref="CoordinationSentences.Compose"/>
/// ne aveva già quattordici, e nove code posizionali dello stesso tipo (due enum uguali, due stringhe, due
/// interi) sono un invito a scambiarne due senza che il compilatore fiati.</para>
/// <see cref="None"/> = riga senza faccetta, cioè il comportamento storico.
/// </summary>
public sealed record TransferHandoffFacet(
    TransferHandoffKind Kind,
    string? Label,
    int? LevelValue,
    LevelUnit LevelUnit,
    LevelConstraint LevelConstraint,
    TransferHandoffKind CommsKind,
    string? CommsLabel,
    int? SpeedValue,
    SpeedConstraint SpeedConstraint,
    bool IsGroupWide)
{
    /// <summary>Nessun trasferimento distinto, nessuna velocità: frase come prima.</summary>
    public static TransferHandoffFacet None { get; } = new(
        TransferHandoffKind.Unspecified, null, null, LevelUnit.Fl, LevelConstraint.Exact,
        TransferHandoffKind.Unspecified, null, null, SpeedConstraint.Unspecified, IsGroupWide: false);

    /// <summary>La faccetta di una riga letta dal repository.</summary>
    public static TransferHandoffFacet From(TransferPointRow p) => new(
        p.HandoffKind, p.HandoffLabel, p.HandoffLevelValue, p.HandoffLevelUnit, p.HandoffLevelConstraint,
        p.CommsHandoffKind, p.CommsHandoffLabel, p.SpeedValue, p.SpeedConstraint, p.IsGroupWide);
}

/// <summary>Risoluzione nomi/codici/aeroporto + composizione, condivisa da AccDerivationService e AppDocumentService.</summary>
public static class CoordinationSentences
{
    /// <summary>Compone la frase per una riga CoP. Per arrivi/partenze l'aeroporto è obbligatorio (senza →
    /// null, frase incompleta); per sorvoli/VFR/altro è opzionale (relazione neutra, l'aeroporto se c'è).</summary>
    public static string? Compose(
        CoordinationSentenceTemplate tpl,
        IReadOnlyDictionary<string, SectorType> types,
        IReadOnlyDictionary<string, string> nameMap,
        IReadOnlyDictionary<string, string> codeMap,
        IReadOnlyDictionary<string, string> airportMap,
        IReadOnlyDictionary<string, string> atcMap,
        string ownerCallsign, string targetCallsign, string? airportIcao,
        LevelConstraint constraint, int? levelValue, LevelUnit levelUnit, string? levelSpecial, LevelParity parity,
        string cop, TransferFlowKind kind = TransferFlowKind.Arrival,
        // La catena delle condizioni, dalla capofila dell'outline a questa riga. Una riga senza varianti ne ha
        // una sola; vuota = nessuna condizione. Non tre stringhe: un'eccezione deve poter dire anche quella
        // della riga che la ospita, o la frase perde metà del proprio significato.
        IReadOnlyList<ConditionClause>? conditions = null,
        TransferVerticalState verticalState = TransferVerticalState.Unspecified,
        TransferHandoffFacet? facet = null,
        // In coda e facoltativo: l'anteprima dell'editor e la vLOA compongono sempre dal lato di chi cede —
        // nell'editor si sta scrivendo l'accordo da quel lato, e la vLOA ha due alberi separati per verso.
        bool isIncoming = false)
    {
        facet ??= TransferHandoffFacet.None;
        // Chi trasferisce, a chi, su quale aeroporto: la parte che non dipende dalla riga. null = dati
        // incompleti, e il contratto e' «dati incompleti -> nessuna frase».
        var b = BuildData(tpl, types, nameMap, codeMap, airportMap, atcMap,
            ownerCallsign, targetCallsign, airportIcao, kind);
        if (b is null) return null;

        return CoordinationSentenceComposer.Compose(tpl, b with
        {
            Constraint = constraint,
            VerticalState = verticalState,
            LevelValue = levelValue,
            LevelUnit = levelUnit,
            LevelSpecial = levelSpecial,
            Parity = parity,
            Point = cop,
            Conditions = conditions ?? Array.Empty<ConditionClause>(),
            Facet = facet,
            IsIncoming = isIncoming,
        });
    }

    /// <summary>
    /// La parte della frase che NON dipende dalla riga: chi trasferisce, a chi, e su quale aeroporto. Estratta
    /// perche' la usano sia la frase distesa sia la capofila, e due copie potrebbero divergere proprio su chi
    /// porta il codice di posizione — che e' la regola meno ovvia di tutte.
    /// <para><c>null</c> = dati incompleti, nessuna frase: senza mittente o destinatario manca il soggetto, e un
    /// arrivo/partenza senza aeroporto avrebbe un «con destinazione» orfano.</para>
    /// </summary>
    private static CoordinationSentenceData? BuildData(
        CoordinationSentenceTemplate tpl,
        IReadOnlyDictionary<string, SectorType> types,
        IReadOnlyDictionary<string, string> nameMap,
        IReadOnlyDictionary<string, string> codeMap,
        IReadOnlyDictionary<string, string> airportMap,
        IReadOnlyDictionary<string, string> atcMap,
        string ownerCallsign, string targetCallsign, string? airportIcao, TransferFlowKind kind)
    {
        if (string.IsNullOrWhiteSpace(ownerCallsign) || string.IsNullOrWhiteSpace(targetCallsign)) return null;

        // Sorvoli/VFR/altro usano la relazione neutra e reggono anche senza aeroporto.
        var hasAirport = !string.IsNullOrWhiteSpace(airportIcao);
        if (!hasAirport && kind is TransferFlowKind.Arrival or TransferFlowKind.Departure) return null;

        // APP/TWR non portano un codice settore CTR; ma un APP consolidato (fornito dall'ACC, es. Napoli su
        // «Roma Radar») ha un MiddleIdentifier di posizione (es. US0) che va mostrato per disambiguare dal nome
        // generico. Quindi ometti il codice solo per i terminali che non ne hanno uno.
        var targetCode = codeMap.GetValueOrDefault(targetCallsign);
        var omit = types.TryGetValue(targetCallsign, out var tt)
                   && tt is SectorType.App or SectorType.Twr or SectorType.ITwr
                   && string.IsNullOrWhiteSpace(targetCode);

        // Mittente: nome base + codice di posizione quando ne ha uno (es. «Roma Radar» + «NE»). Il ricevente
        // porta il proprio codice nello slot del template, quindi qui il target e' senza.
        //
        // La regola era ristretta ai soli CTR, e reggeva finche' il mittente era sempre un CTR. Da quando la
        // sezione estesa mostra anche cio' che ENTRA da un APP (11 agosto 2026), un APP consolidato puo' essere
        // il mittente: senza codice la frase diventava «Roma Radar trasferisce a Roma Radar TS», due enti
        // diversi con lo stesso nome. Ora la regola e' la stessa dei due lati: il codice si mostra se c'e'.
        var ownerBase = BaseName(ownerCallsign, nameMap, atcMap);
        var ownerMid = codeMap.GetValueOrDefault(ownerCallsign) ?? "";
        var ownerName = (ownerMid.Length > 0 && ownerBase.IndexOf(ownerMid, StringComparison.OrdinalIgnoreCase) < 0)
            ? $"{ownerBase} {ownerMid}"
            : ownerBase;

        return new CoordinationSentenceData
        {
            OwnerName = ownerName,
            TargetName = BaseName(targetCallsign, nameMap, atcMap),
            TargetCode = omit ? null : targetCode,
            OmitTargetCode = omit,
            AirportName = hasAirport ? airportMap.GetValueOrDefault(airportIcao!, airportIcao!) : "",
            AirportIcao = airportIcao ?? "",
            Kind = kind,
            Point = "",
        };
    }

    /// <summary>
    /// La frase CAPOFILA per una tabella di coordinamenti. Stesse mappe e stesso template della frase distesa:
    /// e' la stessa cosa detta una volta per tutte invece che riga per riga, quindi mittente, destinatario e
    /// aeroporto devono uscire identici.
    /// </summary>
    public static string? ComposeLead(
        CoordinationSentenceTemplate tpl,
        IReadOnlyDictionary<string, SectorType> types,
        IReadOnlyDictionary<string, string> nameMap,
        IReadOnlyDictionary<string, string> codeMap,
        IReadOnlyDictionary<string, string> airportMap,
        IReadOnlyDictionary<string, string> atcMap,
        string ownerCallsign, string targetCallsign, string? airportIcao,
        TransferFlowKind kind, bool isIncoming = false)
    {
        var d = BuildData(tpl, types, nameMap, codeMap, airportMap, atcMap,
            ownerCallsign, targetCallsign, airportIcao, kind);
        return d is null ? null : CoordinationSentenceComposer.ComposeLead(tpl, d with { IsIncoming = isIncoming });
    }

    // Nome base: AtcCallsign IVAO (es. «Pisa Approach»), altrimenti Sector.Name se risolto (≠ callsign),
    // altrimenti il callsign grezzo.
    private static string BaseName(string cs,
        IReadOnlyDictionary<string, string> nameMap, IReadOnlyDictionary<string, string> atcMap)
    {
        var atc = atcMap.GetValueOrDefault(cs);
        if (!string.IsNullOrWhiteSpace(atc)) return atc;
        var n = nameMap.GetValueOrDefault(cs);
        return !string.IsNullOrWhiteSpace(n) && !string.Equals(n, cs, StringComparison.OrdinalIgnoreCase) ? n : cs;
    }
}
