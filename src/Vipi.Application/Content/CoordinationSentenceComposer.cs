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
    public LevelConstraint? Constraint { get; init; }
    /// <summary>Livello già formattato (es. «FL130↓», «per aerovia», «—»).</summary>
    public required string LevelText { get; init; }
    public required string Point { get; init; }
}

/// <summary>Compone la frase di coordinamento sostituendo i placeholder del template. Funzione pura.</summary>
public static class CoordinationSentenceComposer
{
    public static string Compose(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        var stato = d.Constraint switch
        {
            LevelConstraint.AtOrBelow => tpl.Stato.AtOrBelow,
            LevelConstraint.AtOrAbove => tpl.Stato.AtOrAbove,
            LevelConstraint.Exact => tpl.Stato.Exact,
            _ => tpl.Stato.Special,
        };

        var fl = BuildFl(tpl, d);

        var code = (d.TargetCode ?? "").Trim();
        var target = (d.OmitTargetCode || code.Length == 0)
            ? tpl.TargetNoCode.Replace("{name}", d.TargetName)
            : tpl.TargetWithCode.Replace("{name}", d.TargetName).Replace("{code}", code);

        var airport = tpl.Airport.Replace("{name}", d.AirportName).Replace("{icao}", d.AirportIcao);
        var point = string.IsNullOrWhiteSpace(d.Point) ? tpl.FallbackMissingPoint : d.Point;

        var s = tpl.Template
            .Replace("{owner}", d.OwnerName)
            .Replace("{target}", target)
            .Replace("{airport}", airport)
            .Replace("{stato}", stato)
            .Replace("{fl}", fl)
            .Replace("{point}", point);

        return Normalize(s);
    }

    // FL senza glifo ↑/↓, con prefisso «per » quando è un livello numerico; il testo speciale resta com'è; «—»/vuoto → "".
    private static string BuildFl(CoordinationSentenceTemplate tpl, CoordinationSentenceData d)
    {
        var raw = (d.LevelText ?? "").Replace("↑", "").Replace("↓", "").Trim();
        if (raw.Length == 0 || raw == "—") return "";
        if (d.Constraint == LevelConstraint.Special) return raw;   // es. «per aerovia»
        return "per " + raw;
    }

    // Collassa spazi multipli (da placeholder vuoti) e toglie lo spazio prima della punteggiatura.
    private static string Normalize(string s) =>
        Regex.Replace(Regex.Replace(s, @"\s+([.,;:])", "$1"), @"\s{2,}", " ").Trim();
}

/// <summary>Risoluzione nomi/codici/aeroporto + composizione, condivisa da AccProfileService e AppProfileService.</summary>
public static class CoordinationSentences
{
    /// <summary>Compone la frase per una riga CoP; null se manca l'aeroporto destinazione (nessuna frase).</summary>
    public static string? Compose(
        CoordinationSentenceTemplate tpl,
        IReadOnlyDictionary<string, SectorType> types,
        IReadOnlyDictionary<string, string> nameMap,
        IReadOnlyDictionary<string, string> codeMap,
        IReadOnlyDictionary<string, string> airportMap,
        IReadOnlyDictionary<string, string> atcMap,
        string ownerCallsign, string targetCallsign, string? airportIcao,
        LevelConstraint constraint, string levelText, string cop)
    {
        if (string.IsNullOrWhiteSpace(airportIcao)) return null;

        var omit = types.TryGetValue(targetCallsign, out var tt)
                   && tt is SectorType.App or SectorType.Twr or SectorType.ITwr;

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
            TargetCode = omit ? null : codeMap.GetValueOrDefault(targetCallsign),
            OmitTargetCode = omit,
            AirportName = airportMap.GetValueOrDefault(airportIcao!, airportIcao!),
            AirportIcao = airportIcao!,
            Constraint = constraint,
            LevelText = levelText,
            Point = cop,
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
