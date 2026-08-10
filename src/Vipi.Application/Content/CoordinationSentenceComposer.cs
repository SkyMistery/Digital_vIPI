using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dati risolti per comporre una frase di coordinamento (una per riga CoP).</summary>
public sealed class CoordinationSentenceData
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
    /// <summary>Condizione operativa: tre dimensioni indipendenti, ognuna opzionale. Le presenti sono unite da « e ».</summary>
    public string? ConditionLabel { get; init; }        // pista/e in uso («16R / 16L»)
    public string? ConditionAreaLabel { get; init; }    // area attiva
    public string? ConditionCustomLabel { get; init; }  // condizione personalizzata (testo libero)
}

/// <summary>Compone la frase di coordinamento sostituendo i placeholder del template. Funzione pura.</summary>
public static class CoordinationSentenceComposer
{
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

        var code = (d.TargetCode ?? "").Trim();
        var target = (d.OmitTargetCode || code.Length == 0)
            ? tpl.TargetNoCode.Replace("{name}", d.TargetName)
            : tpl.TargetWithCode.Replace("{name}", d.TargetName).Replace("{code}", code);

        // La relazione aeroporto dipende dal tipo di flusso: arrivo = «con destinazione», partenza = «in partenza da».
        var airportTpl = d.Kind switch
        {
            TransferFlowKind.Departure => tpl.AirportDeparture,
            TransferFlowKind.Arrival => tpl.AirportArrival,
            _ => tpl.Airport,   // overflight/VFR/altro: relazione neutra
        };
        var airport = airportTpl.Replace("{name}", d.AirportName).Replace("{icao}", d.AirportIcao);
        var point = ResolvePoint((d.Point ?? "").Trim(), tpl);

        var s = tpl.Template
            .Replace("{owner}", d.OwnerName)
            .Replace("{target}", target)
            .Replace("{airport}", airport)
            .Replace("{stato}", stato)
            .Replace("{fl}", fl)
            .Replace("{point}", point);

        return AppendCondition(Normalize(s), BuildCondition(tpl, d));
    }

    // Clausola condizione (pista/area/custom) appesa a fine frase. Vuota se None o senza etichetta. Appesa qui
    // e non via placeholder del Template così vale anche per i template custom caricati da file (che non hanno {condition}).
    private static string BuildCondition(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        var rwy = (d.ConditionLabel ?? "").Trim();
        var area = (d.ConditionAreaLabel ?? "").Trim();
        var custom = (d.ConditionCustomLabel ?? "").Trim();

        // Tre dimensioni indipendenti: compone la clausola di ciascuna presente e le unisce con « e » (EN « and »).
        // Pista+area insieme usano la forma dedicata «con pista X in uso e Y attiva» (fraseologia approvata).
        var clauses = new List<string>(3);
        if (rwy.Length > 0 && area.Length > 0)
            clauses.Add(tpl.Condition.RunwayAndArea.Replace("{runway}", rwy).Replace("{area}", area).Trim());
        else if (rwy.Length > 0)
            clauses.Add(tpl.Condition.Runway.Replace("{label}", rwy).Trim());
        else if (area.Length > 0)
            clauses.Add(tpl.Condition.Area.Replace("{label}", area).Trim());
        if (custom.Length > 0)
            clauses.Add(tpl.Condition.Custom.Replace("{label}", custom).Trim());

        return string.Join($" {tpl.Condition.Join} ", clauses.Where(c => c.Length > 0));
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
        string? conditionLabel = null, string? conditionAreaLabel = null, string? conditionCustomLabel = null,
        TransferVerticalState verticalState = TransferVerticalState.Unspecified)
    {
        // Senza mittente o destinatario la frase è priva di soggetto/oggetto → riga incompleta, nessuna frase
        // (coerente col contratto «dati incompleti → null»; evita anche lookup con chiave vuota più sotto).
        if (string.IsNullOrWhiteSpace(ownerCallsign) || string.IsNullOrWhiteSpace(targetCallsign)) return null;

        // Arrivi/partenze senza aeroporto = «con destinazione»/«in partenza da» orfano → nessuna frase.
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

        // Mittente: nome base + codice settore (es. «Roma Radar» + «NE») quando è un CTR. Il ricevente porta il
        // codice nel proprio slot del template (targetWithCode), quindi qui il target è senza codice.
        var ownerBase = BaseName(ownerCallsign, nameMap, atcMap);
        var ownerIsCtr = types.TryGetValue(ownerCallsign, out var ot) && ot == SectorType.Ctr;
        var ownerMid = codeMap.GetValueOrDefault(ownerCallsign) ?? "";
        var ownerName = (ownerIsCtr && ownerMid.Length > 0 && ownerBase.IndexOf(ownerMid, StringComparison.OrdinalIgnoreCase) < 0)
            ? $"{ownerBase} {ownerMid}"
            : ownerBase;

        return CoordinationSentenceComposer.Compose(tpl, new CoordinationSentenceData
        {
            OwnerName = ownerName,
            TargetName = BaseName(targetCallsign, nameMap, atcMap),
            TargetCode = omit ? null : targetCode,
            OmitTargetCode = omit,
            AirportName = hasAirport ? airportMap.GetValueOrDefault(airportIcao!, airportIcao!) : "",
            AirportIcao = airportIcao ?? "",
            Kind = kind,
            Constraint = constraint,
            VerticalState = verticalState,
            LevelValue = levelValue,
            LevelUnit = levelUnit,
            LevelSpecial = levelSpecial,
            Parity = parity,
            Point = cop,
            ConditionLabel = conditionLabel,
            ConditionAreaLabel = conditionAreaLabel,
            ConditionCustomLabel = conditionCustomLabel,
        });
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
