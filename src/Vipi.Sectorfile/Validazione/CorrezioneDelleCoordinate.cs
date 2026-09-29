using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// La correzione proposta per una coordinata scritta male (lotto «Subito» slice 2c, carta «file per file» G3, L4, Q6,
/// R3, F4): il validatore la mette nel problema, il Lab la scrive solo se l'AOD la sceglie. Si propone soltanto quello
/// che la riga dice già, scritto nella forma giusta — mai un dato inventato: coi secondi a 75 o una cifra in più nel
/// compatto non si propone niente.
/// </summary>
/// <remarks>
/// <para>Le forme giuste: col punto <c>N041.48.01.000</c> (gradi a tre cifre, frazione a tre), compatta
/// <c>N0414801000</c> (dieci cifre). Il Lab scrive col punto (regola del lotto); una coordinata compatta resta
/// compatta, perché la forma del file è una scelta del file (R-7, rimandata).</para>
/// </remarks>
public static partial class CorrezioneDelleCoordinate
{
    /// <summary>
    /// Il token nella forma giusta, o null se è già giusto o non si sa correggere. Casi (tutti misurati sul fork del 27
    /// settembre 2026): emisfero minuscolo (<c>n045.44.52.080</c>), gradi senza lo zero davanti (<c>E12.30.18.200</c>,
    /// 12 in <c>lirz.gts</c>), trattino al posto del punto (<c>E008-11.31.443</c>), frazione di due o quattro cifre
    /// (<c>N043.49.49.00</c>, <c>E015.37.07.1000</c>, <c>N046.34.25.8735</c> arrotondata al millesimo), secondi a tre
    /// cifre col punto spostato (<c>E010.34.072.00</c> → <c>E010.34.07.200</c>), secondi o minuti a 60 (il minuto o il
    /// grado dopo: lo stesso punto), compatto con una cifra in meno (<c>E103441000</c> → <c>E0103441000</c>, come già
    /// lo legge il motore).
    /// </summary>
    public static string? DelToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        string t = token.Trim();
        if (t.Length < 2 || "NSEWnsew".IndexOf(t[0]) < 0 || !char.IsAsciiDigit(t[1]))
        {
            return null;
        }

        char emisfero = char.ToUpperInvariant(t[0]);
        string resto = t[1..];
        string? giusto = resto.All(char.IsAsciiDigit) ? Compatto(emisfero, resto) : ConIlPunto(emisfero, resto.Replace('-', '.'));
        return giusto is null || giusto == t ? null : giusto;
    }

    /// <summary>
    /// La riga con ogni coordinata corretta, o null se non cambia niente. Oltre ai token: lo spazio al posto del
    /// <c>;</c> fra due coordinate (86 righe di <c>italy.prohibit</c> e <c>italy.restrict</c>, G3) e la coppia decimale
    /// scritta in DMS (Q6, R3; non nei <c>.txi</c>, dove il decimale è la forma del file).
    /// </summary>
    public static string? DellaRiga(string riga, string estensione)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (riga.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var campi = riga.Split(';').ToList();
        for (int i = 0; i < campi.Count; i++)
        {
            string c = campi[i].Trim();
            if (PareDms(c) && c.Any(char.IsWhiteSpace))
            {
                string[] pezzi = c.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (pezzi.Length == 2 && pezzi.All(PareDms))
                {
                    campi[i] = DelToken(pezzi[0]) ?? pezzi[0];
                    campi.Insert(i + 1, DelToken(pezzi[1]) ?? pezzi[1]);
                    i++;
                }

                continue;
            }

            if (PareDms(c))
            {
                if (DelToken(c) is { } corretto)
                {
                    campi[i] = campi[i].Replace(c, corretto, StringComparison.Ordinal);
                }

                continue;
            }

            if (!estensione.Equals("txi", StringComparison.OrdinalIgnoreCase) && i + 1 < campi.Count
                && Decimale().IsMatch(c) && Decimale().IsMatch(campi[i + 1].Trim())
                && double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
                && double.TryParse(campi[i + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)
                && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180)
            {
                campi[i] = CoordinateConverter.LatitudeToDottedDms(lat);
                campi[i + 1] = CoordinateConverter.LongitudeToDottedDms(lon);
                i++;
            }
        }

        // Lotto «Subito» slice 11b (M4): una pista ha le rotte al grado tondo e il verso primario fra 01 e 18.
        if (estensione.Equals("rw", StringComparison.OrdinalIgnoreCase))
        {
            PistaInOrdine(campi);
        }

        string nuova = string.Join(';', campi);
        return nuova == riga ? null : nuova;
    }

    /// <summary>
    /// Una riga di pista messa in ordine (slice 11b): le rotte con decimali al grado tondo, con tre cifre (<c>065.49</c> →
    /// <c>065</c>, <c>345.9</c> → <c>346</c>); se il verso primario è oltre il 18, i due versi scambiati (numero, elevazione,
    /// rotta e soglia; la rotta che manca è la reciproca dell'altra). Le voci di menu (<c>MAPS</c>, <c>NE</c>) non hanno un
    /// numero e restano come sono.
    /// </summary>
    private static void PistaInOrdine(List<string> campi)
    {
        if (campi.Count < 11)
            return;
        if (Primaria(campi[1]) is > 18 && Primaria(campi[2]) is <= 18)
        {
            // La rotta del verso che diventa primario, se manca, è la reciproca dell'altra (LIMW 27/09: «261;;»): una
            // pista è una retta, e il 6° campo non può restare vuoto.
            if (campi[6].Trim().Length == 0
                && double.TryParse(campi[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double andata))
            {
                campi[6] = ((andata + 180) % 360).ToString("000.###", CultureInfo.InvariantCulture);
            }

            (campi[1], campi[2]) = (campi[2], campi[1]);
            (campi[3], campi[4]) = (campi[4], campi[3]);
            (campi[5], campi[6]) = (campi[6], campi[5]);
            (campi[7], campi[9]) = (campi[9], campi[7]);
            (campi[8], campi[10]) = (campi[10], campi[8]);
        }

        for (int i = 5; i <= 6; i++)
        {
            string c = campi[i].Trim();
            if (c.Contains('.', StringComparison.Ordinal)
                && double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out double gradi) && gradi is >= 0 and <= 360)
            {
                campi[i] = campi[i].Replace(c, Math.Round(gradi, MidpointRounding.AwayFromZero).ToString("000", CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Il numero di un verso di pista (<c>35R</c> → 35), null se non è un verso (<c>MAPS</c>).</summary>
    internal static int? Primaria(string verso)
    {
        string v = verso.Trim();
        int cifre = 0;
        while (cifre < v.Length && char.IsAsciiDigit(v[cifre]))
            cifre++;
        return cifre is 1 or 2 && (v.Length == cifre || (v.Length == cifre + 1 && v[cifre] is 'L' or 'R' or 'C'))
            ? int.Parse(v[..cifre], CultureInfo.InvariantCulture)
            : null;
    }

    // Dieci cifre (DDD MM SS mmm). Con meno: se non comincia con lo zero manca lo zero dei gradi (`E103441000`, il motore
    // lo legge già così); se comincia con lo zero i gradi ci sono tutti e manca una cifra in fondo (`N041131620` =
    // N041.13.16.20, MIL.fix:14). Con più di dieci non si sa quale cifra è di troppo.
    private static string? Compatto(char emisfero, string cifre)
    {
        if (cifre.Length is < 8 or > 10)
        {
            return null;
        }

        string dieci = cifre[0] == '0' ? cifre.PadRight(10, '0') : cifre.PadLeft(10, '0');
        return Valido(emisfero, dieci) ? emisfero + dieci : null;
    }

    private static bool Valido(char emisfero, string dieci)
        => int.Parse(dieci[..3], CultureInfo.InvariantCulture) <= (emisfero is 'N' or 'S' ? 90 : 180)
           && int.Parse(dieci[3..5], CultureInfo.InvariantCulture) < 60
           && int.Parse(dieci[5..7], CultureInfo.InvariantCulture) < 60;

    private static string? ConIlPunto(char emisfero, string resto)
    {
        string[] parti = resto.Split('.');
        if (parti.Length is not (3 or 4) || parti.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit)) || parti[0].Length > 3)
        {
            return null;
        }

        int gradi = int.Parse(parti[0], CultureInfo.InvariantCulture);
        int minuti = int.Parse(parti[1], CultureInfo.InvariantCulture);
        string sec = parti[2], fraz = parti.Length == 4 ? parti[3] : string.Empty;

        // I secondi a tre cifre con lo zero davanti e la frazione a due: il punto è scivolato di un posto (MIL.fix:96).
        if (sec.Length == 3 && sec[0] == '0' && fraz.Length == 2)
        {
            (sec, fraz) = (sec[..2], sec[2] + fraz);
        }

        if (parti[1].Length != 2 && parti[1].Length != 1 || sec.Length is not (1 or 2))
        {
            return null;
        }

        // La frazione dei secondi: a tre cifre, arrotondata al millesimo se ne ha di più.
        decimal secondi = decimal.Parse(sec + (fraz.Length > 0 ? "." + fraz : string.Empty), CultureInfo.InvariantCulture);
        secondi = Math.Round(secondi, 3, MidpointRounding.AwayFromZero);

        // 60 secondi sono il minuto dopo, 60 minuti il grado dopo: lo stesso punto. Oltre, non si sa cosa si voleva.
        if (secondi >= 60 && secondi < 61 && sec == "60")
        {
            secondi -= 60;
            minuti++;
        }

        if (minuti == 60)
        {
            minuti = 0;
            gradi++;
        }

        if (secondi >= 60 || minuti > 59 || gradi > (emisfero is 'N' or 'S' ? 90 : 180))
        {
            return null;
        }

        int interi = (int)Math.Floor(secondi);
        int millesimi = (int)((secondi - interi) * 1000);
        return string.Create(CultureInfo.InvariantCulture, $"{emisfero}{gradi:000}.{minuti:00}.{interi:00}.{millesimi:000}");
    }

    private static bool PareDms(string c)
        => c.Length > 1 && "NSEWnsew".Contains(c[0]) && char.IsAsciiDigit(c[1])
        && (c.Contains('.', StringComparison.Ordinal) || (c.Length >= 7 && c[1..].All(char.IsAsciiDigit)));

    // Un decimale da coordinata: almeno quattro cifre dopo il punto (come nel validatore: una frequenza ne ha tre).
    [GeneratedRegex(@"^[+-]?[0-9]{1,3}\.[0-9]{4,}$")]
    private static partial Regex Decimale();
}
