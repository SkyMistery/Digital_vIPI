using System.Globalization;
using Aip;
using Aip.Aurora;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le righe dei gesti di ENR 4 (carta F6, slice 3c): una radioassistenza o un punto che il sector non ha, come riga nuova
/// di <c>.vor</c>, <c>.ndb</c> o <c>.fix</c>; una radioassistenza diversa, la sua riga riscritta coi valori dell'AIP.
/// <para>Le forme sono quelle del manuale IVAO e del sector: <c>ID;MHz;lat;lon;visibilità;tipo;canale;attesa;</c>,
/// <c>ID;kHz;lat;lon;</c>, <c>NOME;lat;lon;uso;fra;</c>.</para>
/// </summary>
public static class Enr4NelSector
{
    public const string FileDeiVor = "itvor.vor";
    public const string FileDegliNdb = "itndb.ndb";

    /// <summary>NDB e locator vanno nei <c>.ndb</c>; il resto (VOR, DME, TACAN) nei <c>.vor</c>.</summary>
    public static bool NeiNdb(Navaid n)
    {
        ArgumentNullException.ThrowIfNull(n);
        return n.Kind is NavaidKind.Ndb or NavaidKind.Locator;
    }

    /// <summary>
    /// Il 6° campo del <c>.vor</c> (l'editor del Lab: 0 VOR, 1 VOR/DME, 2 VORTAC, 3 TACAN, 4 DME), dal tipo dell'AIP; null se
    /// non ha un corrispondente.
    /// </summary>
    public static string? TipoDelVor(NavaidKind tipo) => tipo switch
    {
        NavaidKind.Vor or NavaidKind.Dvor => "0",
        NavaidKind.VorDme or NavaidKind.DvorDme or NavaidKind.TvorDme => "1",
        NavaidKind.Vortac or NavaidKind.Dvortac => "2",
        NavaidKind.Tacan => "3",
        NavaidKind.Dme => "4",
        _ => null,
    };

    /// <summary>La riga nuova di una radioassistenza: nel <c>.ndb</c> o nel <c>.vor</c> (mostrata, col tipo e il canale).</summary>
    public static string RigaNuova(Navaid n)
    {
        ArgumentNullException.ThrowIfNull(n);
        var frequenza = Frequenza(n.Frequency);
        if (NeiNdb(n))
            return $"{n.Designator};{frequenza};{AuroraText.Coordinates(n.Position)};";
        return $"{n.Designator};{frequenza};{AuroraText.Coordinates(n.Position)};0;{TipoDelVor(n.Kind)};{n.Channel};";
    }

    /// <summary>
    /// La riga nuova di un punto di <c>itfix.fix</c>: il 4° e il 5° campo come li usa il file (misura della slice 3b, 1 250
    /// punti su 1 254: rotte ATS → 0, procedure → 1, tutti e due → 2; interesse FRA → 1). Senza rotte né procedure, 0.
    /// </summary>
    public static string RigaNuova(SignificantPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var uso = (p.Routes.Count > 0, p.Procedures is not null) switch
        {
            (true, true) => 2,
            (false, true) => 1,
            _ => 0,
        };
        return $"{p.Designator};{AuroraText.Coordinates(p.Position)};{uso};{(p.Fra is null ? 0 : 1)};";
    }

    /// <summary>
    /// La riga di una radioassistenza del sector riscritta coi valori dell'AIP che sono diversi: la frequenza (se diversa),
    /// la posizione (oltre <paramref name="soglia"/> NM), il canale TACAN (dove la riga lo scrive). Gli altri campi —
    /// visibilità, tipo, attesa, quel che segue — restano com'erano. <paramref name="cambiati"/>: cosa è cambiato.
    /// Null se la riga non ha la forma attesa (meno di quattro campi).
    /// </summary>
    public static string? Sostituita(string riga, Navaid n, double scartoNm, double soglia, out IReadOnlyList<string> cambiati)
    {
        ArgumentNullException.ThrowIfNull(riga);
        ArgumentNullException.ThrowIfNull(n);
        var lista = new List<string>();
        cambiati = lista;
        var campi = riga.Split(';').ToList();
        if (campi.Count < 4) return null;

        if (Frequenza(n.Frequency) is { Length: > 0 } f && Numero(campi[1]) != Numero(f))
        {
            lista.Add($"frequenza {campi[1]} → {f}");
            campi[1] = f;
        }
        if (scartoNm > soglia)
        {
            lista.Add(string.Create(CultureInfo.InvariantCulture, $"posizione ({scartoNm:0.00} NM)"));
            var (lat, lon) = (AuroraText.Latitude(n.Position.Lat), AuroraText.Longitude(n.Position.Lon));
            campi[2] = lat;
            campi[3] = lon;
        }
        if (!NeiNdb(n) && n.Channel is { } canale && campi.Count > 6 && campi[6].Trim().Length > 0
            && !string.Equals(campi[6].Trim(), canale, StringComparison.OrdinalIgnoreCase))
        {
            lista.Add($"canale {campi[6].Trim()} → {canale}");
            campi[6] = canale;
        }
        return string.Join(';', campi);
    }

    /// <summary>La frequenza stampata come la scrive il sector: <c>113.80 MHZ</c> → <c>113.80</c>, <c>390.00 KHZ</c> → <c>390.0</c>.</summary>
    private static string Frequenza(string? stampata)
    {
        if (stampata is null || !decimal.TryParse(stampata.Split(' ')[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var f))
            return "";
        // I kHz del sector con un decimale (390.0, 317.5); i MHz con due (113.80).
        return stampata.EndsWith("KHZ", StringComparison.OrdinalIgnoreCase)
            ? f.ToString("0.0##", CultureInfo.InvariantCulture)
            : f.ToString("0.00#", CultureInfo.InvariantCulture);
    }

    private static decimal? Numero(string testo) =>
        decimal.TryParse(testo.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
}
