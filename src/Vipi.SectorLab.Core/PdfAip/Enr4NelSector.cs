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
    /// <summary>
    /// I file dove può andare un punto di ENR 4.4 (riordino R8a, «metti in questo file»): i <c>.fix</c> del sector, quello
    /// dei punti in rotta (<see cref="ConfrontoDiEnr4.FileDeiPunti"/>) per primo, gli altri per nome. La misura del 20
    /// ottobre: nove (itfix, APT, ESTERNI, secsi, VFR_NASCOSTI, MIL, e TERM, FRA, ENR vuoti). Le chiavi della sessione.
    /// </summary>
    public static IReadOnlyList<string> FileDeiPunti(IEnumerable<string> fileDelSector) =>
        [.. fileDelSector.Where(f => f.EndsWith(".fix", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => string.Equals(Path.GetFileName(f), ConfrontoDiEnr4.FileDeiPunti, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)];

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
    /// L'uso di un punto di ENR 4.4 nel 4° campo del <c>.fix</c> (0 in rotta, 1 terminale, 2 tutti e due): rotte ATS → in
    /// rotta, procedure → terminale. Null se l'AIP non dà né rotte né procedure. 🔴 Il significato del 4° e del 5° campo
    /// l'ha confermato il committente (19 ottobre) dopo la misura della slice 3b: 1 250 punti su 1 254 così.
    /// </summary>
    public static int? Uso(SignificantPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return (p.Routes.Count > 0, p.Procedures is not null) switch
        {
            (true, true) => 2,
            (true, false) => 0,
            (false, true) => 1,
            _ => null,
        };
    }

    /// <summary>Il 5° campo del <c>.fix</c>: 1 se ENR 4.4 dà al punto un interesse FRA, 0 se no.</summary>
    public static string Fra(SignificantPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return p.Fra is null ? "0" : "1";
    }

    /// <summary>La riga nuova di un punto di <c>itfix.fix</c>, con l'uso e il FRA; senza rotte né procedure l'uso è 0.</summary>
    public static string RigaNuova(SignificantPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return $"{p.Designator};{AuroraText.Coordinates(p.Position)};{Uso(p) ?? 0};{Fra(p)};";
    }

    /// <summary>
    /// La riga di un punto di <c>itfix.fix</c> col 4° e il 5° campo dell'AIP, dove sono diversi (l'uso solo se l'AIP lo dà);
    /// il resto — l'attesa nel 6°, quel che segue — resta. Null se la riga non ha cinque campi.
    /// </summary>
    public static string? ConLUso(string riga, SignificantPoint p, out IReadOnlyList<string> cambiati)
    {
        ArgumentNullException.ThrowIfNull(riga);
        ArgumentNullException.ThrowIfNull(p);
        var lista = new List<string>();
        cambiati = lista;
        var campi = riga.Split(';').ToList();
        if (campi.Count < 5) return null;
        if (Uso(p) is { } uso && campi[3].Trim() != uso.ToString(CultureInfo.InvariantCulture))
        {
            lista.Add($"uso {campi[3].Trim()} → {uso}");
            campi[3] = uso.ToString(CultureInfo.InvariantCulture);
        }
        if (campi[4].Trim() != Fra(p))
        {
            lista.Add($"FRA {campi[4].Trim()} → {Fra(p)}");
            campi[4] = Fra(p);
        }
        return string.Join(';', campi);
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
