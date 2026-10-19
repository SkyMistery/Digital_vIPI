using System.Globalization;
using Aip;
using Aip.Aurora;
using Vipi.Sectorfile.Shared;
using PistaDellAip = Aip.Runway;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le righe dei gesti di AD 2 (carta F6, slice 5c): la riga di uno scalo del <c>.ap</c> o di una pista del <c>.rw</c>
/// riscritta coi valori dell'AIP che sono diversi — con le soglie del confronto (<see cref="ConfrontoDiAd2"/>) —, e la riga
/// nuova di una pista che il sector non ha. Il resto della riga resta com'era.
/// </summary>
public static class Ad2NelSector
{
    /// <summary>Il file delle piste di tutta Italia: le piste nuove vanno lì, nella sezione <c>//PISTE</c>.</summary>
    public const string FileDellePiste = "itrw.rw";

    /// <summary>
    /// La riga di uno scalo del <c>.ap</c> (<c>ICAO;elevazione;transizione;lat;lon;NOME;</c>) coi valori dell'AIP diversi:
    /// il punto di riferimento oltre 0,1 NM, l'elevazione al piede, l'altitudine di transizione dove la riga la scrive (0 =
    /// non scritta, resta). Null se la riga non ha la forma attesa (meno di cinque campi).
    /// </summary>
    public static string? SostituitoLoScalo(string riga, Aerodrome scalo, string? transizione, out IReadOnlyList<string> cambiati)
    {
        ArgumentNullException.ThrowIfNull(riga);
        ArgumentNullException.ThrowIfNull(scalo);
        var lista = new List<string>();
        cambiati = lista;
        var campi = riga.Split(';').ToList();
        if (campi.Count < 5) return null;

        if (ConfrontoDiAd2.Piedi(scalo.Elevation) is { } e && Intero(campi[1]) is { } scritta && Math.Abs(e - scritta) >= 1)
        {
            var nuova = Piedi(e);
            lista.Add($"elevazione {campi[1].Trim()} → {nuova}");
            campi[1] = nuova;
        }
        if (ConfrontoDiAd2.Piedi(transizione) is { } t && Intero(campi[2]) is { } ta && ta > 0 && Math.Abs(t - ta) >= 1)
        {
            var nuova = Piedi(t);
            lista.Add($"transizione {campi[2].Trim()} → {nuova}");
            campi[2] = nuova;
        }
        if (scalo.Arp is { } arp && Coordinata(campi[3], campi[4]) is { } c
            && Sphere.DistanceNm(arp, new Position(c.LatitudeDeg, c.LongitudeDeg)) is var scarto && scarto > ConfrontoDiEnr3.Soglia)
        {
            lista.Add(string.Create(CultureInfo.InvariantCulture, $"punto di riferimento ({scarto:0.00} NM)"));
            campi[3] = AuroraText.Latitude(arp.Lat);
            campi[4] = AuroraText.Longitude(arp.Lon);
        }
        return string.Join(';', campi);
    }

    /// <summary>
    /// La riga di una pista del <c>.rw</c> (<c>ICAO;T1;T2;elev1;elev2;rotta1;rotta2;lat1;lon1;lat2;lon2;</c>) coi valori
    /// dell'AIP diversi, testata per testata: la soglia oltre <see cref="ConfrontoDiAd2.SogliaDellaSogliaM"/>, l'elevazione
    /// della soglia al piede, la rotta magnetica (vera meno <paramref name="declinazione"/>) oltre
    /// <see cref="ConfrontoDiAd2.SogliaDellaRotta"/>, scritta con un decimale. Null se la riga non ha undici campi o l'AIP
    /// non ha una delle due testate.
    /// </summary>
    public static string? SostituitaLaPista(string riga, IReadOnlyList<PistaDellAip> piste, double? declinazione, out IReadOnlyList<string> cambiati)
    {
        ArgumentNullException.ThrowIfNull(riga);
        ArgumentNullException.ThrowIfNull(piste);
        var lista = new List<string>();
        cambiati = lista;
        var campi = riga.Split(';').ToList();
        if (campi.Count < 11) return null;
        for (var lato = 0; lato < 2; lato++)
        {
            var testata = campi[1 + lato].Trim();
            if (piste.FirstOrDefault(p => ConfrontoDiAd2.Designatore(p.Designator) == ConfrontoDiAd2.Designatore(testata)) is not { } p)
                return null;
            int elevazione = 3 + lato, rotta = 5 + lato, lat = 7 + 2 * lato, lon = 8 + 2 * lato;

            if (p.Threshold is { } soglia && Coordinata(campi[lat], campi[lon]) is { } c
                && ConfrontoDiAd2.Metri(soglia, c) is var metri && metri > ConfrontoDiAd2.SogliaDellaSogliaM)
            {
                lista.Add(string.Create(CultureInfo.InvariantCulture, $"{testata}: soglia ({metri:0} m)"));
                campi[lat] = AuroraText.Latitude(soglia.Lat);
                campi[lon] = AuroraText.Longitude(soglia.Lon);
            }
            if (ConfrontoDiAd2.Piedi(p.ThresholdElevation) is { } e && Intero(campi[elevazione]) is { } scritta && Math.Abs(e - scritta) >= 1)
            {
                var nuova = Piedi(e);
                lista.Add($"{testata}: elevazione {campi[elevazione].Trim()} → {nuova}");
                campi[elevazione] = nuova;
            }
            if (ConfrontoDiAd2.Gradi(p.TrueBearing) is { } vera && declinazione is { } dec && Numero(campi[rotta]) is { } r
                && ConfrontoDiAd2.Magnetica(vera, dec) is var magnetica && ConfrontoDiAd2.ScartoDiRotta(r, magnetica) > ConfrontoDiAd2.SogliaDellaRotta)
            {
                var nuova = Rotta(magnetica);
                lista.Add($"{testata}: rotta {campi[rotta].Trim()} → {nuova}");
                campi[rotta] = nuova;
            }
        }
        return string.Join(';', campi);
    }

    /// <summary>La riga nuova di una pista per <c>itrw.rw</c>, con le due testate dell'AIP; null se manca una soglia.</summary>
    public static string? RigaNuovaDellaPista(string icao, PistaDellAip prima, PistaDellAip opposta, double? declinazione)
    {
        ArgumentNullException.ThrowIfNull(prima);
        ArgumentNullException.ThrowIfNull(opposta);
        if (prima.Threshold is not { } s1 || opposta.Threshold is not { } s2) return null;
        string Elevazione(PistaDellAip p) => ConfrontoDiAd2.Piedi(p.ThresholdElevation) is { } e ? Piedi(e) : "0";
        string RottaDi(PistaDellAip p) => ConfrontoDiAd2.Gradi(p.TrueBearing) is { } v && declinazione is { } d ? Rotta(ConfrontoDiAd2.Magnetica(v, d)) : "";
        return $"{icao};{prima.Designator};{opposta.Designator};{Elevazione(prima)};{Elevazione(opposta)};{RottaDi(prima)};{RottaDi(opposta)};"
               + $"{AuroraText.Coordinates(s1)};{AuroraText.Coordinates(s2)};";
    }

    /// <summary>I piedi al piede, il mezzo in su come li scrive il sector (284.5 → 285).</summary>
    private static string Piedi(double piedi) =>
        Math.Round(piedi, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>La rotta con un decimale e tre cifre intere, come <c>094.5</c>.</summary>
    private static string Rotta(double gradi)
    {
        var r = Math.Round(gradi, 1, MidpointRounding.AwayFromZero);
        return (r >= 360 ? r - 360 : r).ToString("000.0", CultureInfo.InvariantCulture);
    }

    private static int? Intero(string testo) =>
        int.TryParse(testo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static double? Numero(string testo) =>
        double.TryParse(testo.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static Coordinate? Coordinata(string lat, string lon)
    {
        try
        {
            return CoordinateConverter.ParsePair(lat, lon);
        }
        catch (CoordinateParseException)
        {
            return null;
        }
    }
}
