using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// I flussi di trasferimento e le mappe di risoluzione **veri**, estratti dal <c>vipi.db</c> di sviluppo il
/// 16 agosto 2026 (37 flussi, 78 punti) e congelati in <c>Fixtures/real-flows.tsv</c> + <c>real-maps.tsv</c>.
/// <para>Serve alla rete di caratterizzazione che accompagna la sostituzione del modello
/// (<c>TransferFlow</c>/<c>TransferPoint</c> → Accordi): la derivazione di questi dati non deve cambiare di un
/// carattere. Un fixture inventato non avrebbe potuto dirlo — i casi che rompono sono quelli scritti dai
/// colleghi, non quelli scritti da chi rifà il modello.</para>
/// <para>La costruzione delle righe ricalca <c>EfLegacyFlowReader.MapFlow/MapPoint</c>: se quella cambia,
/// questa va cambiata con lei, ed è voluto che se ne accorga il compilatore.</para>
/// </summary>
internal static class RealCoordinationFixture
{
    /// <summary>Cartella dei fixture accanto all'assembly (copiati dal csproj).</summary>
    internal static string Dir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    internal sealed record Maps(
        IReadOnlyDictionary<string, SectorType> Types,
        IReadOnlyDictionary<string, string> Names,
        IReadOnlyDictionary<string, string> Codes,
        IReadOnlyDictionary<string, string> Airports,
        IReadOnlyDictionary<string, string> Atc,
        IReadOnlyDictionary<string, string> AccNames);

    internal static IReadOnlyList<TransferFlowRow> LoadFlows()
    {
        var flows = new List<TransferFlowRow>();
        var points = new Dictionary<int, List<TransferPointRow>>();
        var order = new List<(int Id, string Acc, string Owner, TransferFlowKind Kind, string? Icao, string? Name, int Order)>();

        foreach (var f in Rows(Path.Combine(Dir, "real-flows.tsv")))
        {
            switch (f[0])
            {
                case "F":
                    order.Add((Int(f[1])!.Value, f[2], f[3], Enum<TransferFlowKind>(f[4]), Null(f[5]), Null(f[6]), Int(f[7]) ?? 0));
                    break;
                case "P":
                    var flowId = Int(f[2])!.Value;
                    if (!points.TryGetValue(flowId, out var list)) points[flowId] = list = new List<TransferPointRow>();
                    list.Add(Point(f));
                    break;
            }
        }

        foreach (var (id, acc, owner, kind, icao, name, ord) in order)
            flows.Add(new TransferFlowRow
            {
                Id = id,
                AccCode = acc,
                OwningSectorId = id,          // gli id di settore non servono alla derivazione: conta il callsign
                OwningSectorCallsign = owner,
                Kind = kind,
                AirportIcao = icao,
                AirportName = name,
                Order = ord,
                Points = points.TryGetValue(id, out var ps)
                    ? ps.OrderBy(p => p.Order).ToList()
                    : Array.Empty<TransferPointRow>(),
            });

        return flows;
    }

    // Colonne, nell'ordine dell'intestazione del fixture:
    // 1 Id · 2 FlowId · 3 Cop · 4 LevelValue · 5 LevelUnit · 6 LevelConstraint · 7 LevelSpecial · 8 Parity ·
    // 9 VerticalState · 10 NextCallsign · 11 ConditionLabel · 12 ConditionRefId · 13 ConditionAreaLabel ·
    // 14 ConditionCustomLabel · 15 HandoffKind · 16 HandoffLabel · 17 HandoffLevelValue · 18 HandoffLevelUnit ·
    // 19 HandoffLevelConstraint · 20 CommsHandoffKind · 21 CommsHandoffLabel · 22 SpeedValue ·
    // 23 SpeedConstraint · 24 VariantGroup · 25 VariantDepth · 26 IsGroupWide · 27 Order
    private static TransferPointRow Point(string[] f)
    {
        var value = Int(f[4]);
        var unit = Enum<LevelUnit>(f[5]);
        var constraint = Enum<LevelConstraint>(f[6]);
        var special = Null(f[7]);
        var parity = Enum<LevelParity>(f[8]);
        var vstate = Enum<TransferVerticalState>(f[9]);
        var next = Null(f[10]);

        return new TransferPointRow
        {
            Id = Int(f[1])!.Value,
            Cop = f[3],
            LevelValue = value,
            LevelUnit = unit,
            LevelConstraint = constraint,
            LevelSpecial = special,
            Parity = parity,
            VerticalState = vstate,
            LevelText = LevelFormatting.Format(value, unit, constraint, special, parity, vstate),
            // L'id del ricevente non serve alla derivazione (che ragiona per callsign); serve solo a distinguere
            // «nessun ricevente» da «ricevente c'è», ed è la stessa distinzione che porta il callsign.
            NextSectorId = next is null ? null : Int(f[1]),
            NextSectorCallsign = next,
            ConditionLabel = Null(f[11]),
            ConditionRefId = Int(f[12]),
            ConditionAreaLabel = Null(f[13]),
            ConditionCustomLabel = Null(f[14]),
            HandoffKind = Enum<TransferHandoffKind>(f[15]),
            HandoffLabel = Null(f[16]),
            HandoffLevelValue = Int(f[17]),
            HandoffLevelUnit = Enum<LevelUnit>(f[18]),
            HandoffLevelConstraint = Enum<LevelConstraint>(f[19]),
            CommsHandoffKind = Enum<TransferHandoffKind>(f[20]),
            CommsHandoffLabel = Null(f[21]),
            SpeedValue = Int(f[22]),
            SpeedConstraint = Enum<SpeedConstraint>(f[23]),
            VariantGroup = Int(f[24]),
            VariantDepth = Int(f[25]) ?? 0,
            IsGroupWide = f[26] == "1",
            Order = Int(f[27]) ?? 0,
        };
    }

    internal static Maps LoadMaps(IReadOnlyList<TransferFlowRow> flows)
    {
        var types = New<SectorType>();
        var names = New<string>();
        var codes = New<string>();
        var atc = New<string>();
        var accNames = New<string>();
        var airports = New<string>();

        foreach (var r in Rows(Path.Combine(Dir, "real-maps.tsv")))
        {
            if (r[0] == "S")
            {
                var cs = r[1];
                if (r[2].Length > 0) types[cs] = Enum<SectorType>(r[2]);
                if (r[3].Length > 0) codes[cs] = r[3];
                if (r[4].Length > 0) atc[cs] = r[4];
                if (r[5].Length > 0) names[cs] = r[5];
                if (r[6].Length > 0) accNames[cs] = r[6];
            }
            else if (r[0] == "A")
            {
                airports[r[1]] = r[2];
            }
        }

        // Stessa fusione della derivazione vera: il catalogo vince, i nomi liberi sui flussi riempiono i buchi.
        return new Maps(types, names, codes,
            CoordinationDerivation.MergeAirportNames(airports, flows), atc, accNames);

        static Dictionary<string, T> New<T>() => new(StringComparer.OrdinalIgnoreCase);
    }

    // ---- lettura del TSV ----
    // Righe vuote e commenti («#») saltati; l'intestazione si riconosce perché la seconda colonna è «Id».
    private static IEnumerable<string[]> Rows(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split('\t');
            if (f.Length < 2 || f[1] is "Id" or "Callsign" or "Icao") continue;
            yield return f;
        }
    }

    private static string? Null(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static int? Int(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static T Enum<T>(string s) where T : struct, System.Enum =>
        System.Enum.TryParse<T>(s, ignoreCase: true, out var v) ? v : default;
}
