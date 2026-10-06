using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Gli avvisi delle MVA di ACC e di scalo (lotto «Subito» slice 15b; «file per file» E3, E5, S3): l'etichetta fuori da
/// ogni zona, la zona senza etichetta, la quota che non è una quota o non è in centinaia, il gruppo che manca sulle
/// righe <c>T;</c> di una MVA di ACC, il nome di una MVA di scalo che non è quello dello scalo.
/// </summary>
/// <remarks>
/// <para>Misure sul fork del 6 ottobre 2026 (4 file di ACC in <c>ENRMVA/</c>, 24 di scalo). Etichette fuori da ogni
/// zona: 12 (9 di ACC, 3 di scalo), quasi tutte sole nel loro blocco. Zone chiuse senza etichetta, nei file con un nome
/// solo: 1 (<c>lipx.mva</c>). Quote piene: 13 (<c>libn</c>, <c>libv</c>, <c>lict</c>); non valide: nessuna. Separatori
/// <c>T;DUMMY</c> senza gruppo nelle MVA di ACC: 104. MVA di scalo che non si chiamano come lo scalo: tutte e 24.</para>
/// <para>Si legge dalle righe, non dal modello: gli avvisi vanno sulla riga, e la riga corretta è quella di prima con
/// un campo cambiato. Una riga commentata non conta. Il commento in coda lo dice già <see cref="Regola.CommentoInCoda"/>.</para>
/// </remarks>
public static partial class ControlloDelleMva
{
    // Primo e ultimo punto a meno di venti metri: la zona è chiusa.
    private const double ChiusaEntroMetri = 20;

    private sealed record Etichetta(int Riga, Coordinate Dove, string Quota);

    private sealed record Poligono(int Riga, List<Coordinate> Punti)
    {
        public bool Chiuso => Punti.Count >= 4 && Validatore.Metri(Punti[0], Punti[^1]) < ChiusaEntroMetri;
    }

    /// <summary>
    /// I problemi dei <c>.mva</c>. <paramref name="file"/>: il percorso da mostrare, le righe e se è di ACC (sta in
    /// <c>ENRMVA/</c>); <paramref name="punto"/>: la posizione di un fix, VOR o NDB per nome, o null.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe, bool DiAcc)> file,
                                                    Func<string, Coordinate?> punto)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(punto);

        var problemi = new List<ProblemaDelSector>();
        foreach (var (relativo, righe, diAcc) in file)
        {
            var etichette = new List<Etichetta>();
            var poligoni = new List<Poligono>();
            var nomi = new List<(int Riga, string Nome)>();
            var tracciate = new List<(int Riga, string[] Campi, int Quanti, string Coda)>();
            Poligono? aperto = null;

            for (int i = 0; i < righe.Count; i++)
            {
                string riga = righe[i].Trim();
                if (!riga.StartsWith("L;", StringComparison.Ordinal) && !riga.StartsWith("T;", StringComparison.Ordinal))
                {
                    // Una riga vuota o di testo chiude la zona; un commento (anche una riga commentata) no.
                    if (!riga.StartsWith("//", StringComparison.Ordinal))
                        aperto = null;
                    continue;
                }

                int taglio = riga.IndexOf("//", StringComparison.Ordinal);
                string coda = taglio > 0 ? riga[taglio..] : string.Empty;
                string[] campi = (taglio > 0 ? riga[..taglio] : riga).Split(';');
                int quanti = campi.Length;
                while (quanti > 0 && campi[quanti - 1].Trim().Length == 0)
                    quanti--;
                if (quanti < 4)
                    continue;

                string nome = campi[1].Trim();
                bool separatore = string.Equals(nome, "DUMMY", StringComparison.OrdinalIgnoreCase);
                if (!separatore)
                    nomi.Add((i + 1, nome));

                if (riga[0] == 'L')
                {
                    string quota = quanti >= 5 ? campi[4].Trim() : string.Empty;
                    if (Leggi(campi[2], campi[3], punto) is { } dove)
                        etichette.Add(new Etichetta(i + 1, dove, quota));
                    if (DellaQuota(quota, out string? inCentinaia) is { } regola)
                    {
                        string? proposta = null;
                        if (inCentinaia is not null)
                        {
                            string[] corretti = [.. campi];
                            corretti[4] = inCentinaia;
                            proposta = string.Join(';', corretti) + coda;
                        }

                        problemi.Add(new(regola, relativo, i + 1, righe[i], regola == Regola.QuotaNonInCentinaia
                            ? $"la quota «{quota}» è scritta {(quota.StartsWith("FL", StringComparison.OrdinalIgnoreCase) ? "come livello di volo" : "in piedi")}: le MVA si scrivono in centinaia"
                              + (inCentinaia is null ? string.Empty : $", {inCentinaia}")
                            : quota.Length == 0
                                ? "l'etichetta non ha la quota (il 5° campo)"
                                : $"«{quota}» non è una quota in centinaia né un valore speciale (TRL, NO MINIMA, 70/TRL)",
                            proposta));
                    }

                    continue;
                }

                tracciate.Add((i + 1, campi, quanti, coda));
                if (separatore)
                {
                    aperto = null;
                    continue;
                }

                if (aperto is null)
                {
                    aperto = new Poligono(i + 1, []);
                    poligoni.Add(aperto);
                }

                if (Leggi(campi[2], campi[3], punto) is { } vertice)
                    aperto.Punti.Add(vertice);
            }

            var distinti = nomi.Select(n => n.Nome).Distinct(StringComparer.Ordinal).ToList();
            var zone = poligoni.Where(p => p.Punti.Count >= 3).ToList();

            foreach (var etichetta in etichette.Where(e => !zone.Any(z => Dentro(e.Dove, z.Punti))))
            {
                problemi.Add(new(Regola.EtichettaFuoriDallaZona, relativo, etichetta.Riga, righe[etichetta.Riga - 1],
                    $"l'etichetta «{etichetta.Quota}» non sta dentro nessuna zona del file: non si sa di quale zona è la quota"));
            }

            // In un file con un nome per zona i tratti sono linee, cerchi, pezzi di confine: una zona non è un poligono.
            if (distinti.Count == 1)
            {
                foreach (var zona in zone.Where(z => z.Chiuso && !etichette.Any(e => Dentro(e.Dove, z.Punti))))
                {
                    problemi.Add(new(Regola.ZonaSenzaEtichetta, relativo, zona.Riga, righe[zona.Riga - 1],
                        $"la zona chiusa che comincia qui ({zona.Punti.Count - 1} punti) non ha un'etichetta dentro: non si sa che quota vale"));
                }
            }

            if (diAcc)
            {
                // Il gruppo del file: il nome che le sue righe portano più spesso (LIMM in limm.mva).
                string gruppo = nomi.GroupBy(n => n.Nome, StringComparer.Ordinal).OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault()?.Key
                    ?? Path.GetFileNameWithoutExtension(relativo).ToUpperInvariant();
                foreach (var (numero, campi, quanti, coda) in tracciate)
                {
                    string suo = quanti >= 5 ? campi[4].Trim() : string.Empty;
                    if (string.Equals(suo, gruppo, StringComparison.Ordinal))
                        continue;
                    bool separatore = string.Equals(campi[1].Trim(), "DUMMY", StringComparison.OrdinalIgnoreCase);
                    string proposta = string.Join(';', campi.Take(4)).TrimEnd() + $";{gruppo};" + (coda.Length > 0 ? " " + coda : string.Empty);
                    problemi.Add(new(Regola.GruppoMancanteNellaMva, relativo, numero, righe[numero - 1],
                        suo.Length > 0
                            ? $"il gruppo della riga è «{suo}», quello del file è {gruppo}: nella MVA Selection di Aurora finisce sotto un'altra voce"
                            : separatore
                                ? $"il separatore DUMMY non ha il gruppo ({gruppo}) nel 5° campo: nella MVA Selection di Aurora compare la voce DUMMY"
                                : $"la riga non ha il gruppo ({gruppo}) nel 5° campo: la MVA Selection di Aurora non la accende e spegne con le altre",
                        proposta));
                }
            }
            else if (Path.GetFileNameWithoutExtension(relativo).ToUpperInvariant() is var icao
                     && nomi.FirstOrDefault(n => !string.Equals(n.Nome, icao, StringComparison.Ordinal)) is { Riga: > 0 } primo)
            {
                problemi.Add(new(Regola.MvaNonDelloScalo, relativo, primo.Riga, righe[primo.Riga - 1],
                    distinti.Count == 1
                        ? $"la MVA si chiama «{distinti[0]}»: di scalo il nome è quello dello scalo, {icao}"
                        : $"{distinti.Count} nomi ({string.Join(", ", distinti.Take(4).Select(n => n.Length > 0 ? n : "uno vuoto"))}{(distinti.Count > 4 ? "…" : string.Empty)}), ognuno una voce nella MVA Selection di Aurora: di scalo il nome è uno, quello dello scalo, {icao}"));
            }
        }

        return problemi.OrderBy(p => p.File, StringComparer.Ordinal).ThenBy(p => p.Riga);
    }

    /// <summary>
    /// Che cosa non va nella quota di un'etichetta, o null. <paramref name="inCentinaia"/>: la stessa quota in centinaia,
    /// dove si ricava (<c>2500</c> → <c>25</c>, <c>FL85</c> → <c>85</c>).
    /// </summary>
    private static Regola? DellaQuota(string quota, out string? inCentinaia)
    {
        inCentinaia = null;
        string q = quota.Trim().ToUpperInvariant();
        if (Centinaia().IsMatch(q) || q is "TRL" or "NO MINIMA" || ConTransizione().IsMatch(q) || ConNota().IsMatch(q))
            return null;
        if (Livello().Match(q) is { Success: true } livello)
        {
            inCentinaia = int.Parse(livello.Groups["n"].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            return Regola.QuotaNonInCentinaia;
        }

        if (Piedi().Match(q) is { Success: true } piedi)
        {
            int numero = int.Parse(piedi.Groups["n"].Value, CultureInfo.InvariantCulture);
            if (numero % 100 == 0)
                inCentinaia = (numero / 100).ToString(CultureInfo.InvariantCulture);
            return Regola.QuotaNonInCentinaia;
        }

        return Regola.QuotaNonValida;
    }

    // Come le legge il motore: coordinate (anche in gradi decimali: `L;MM ES0;45.55756591;10.27902575;60;8;`, lipx.mva)
    // o il nome di un punto (`T;LIRR;UTENO;UTENO;LIRR;`).
    private static Coordinate? Leggi(string lat, string lon, Func<string, Coordinate?> punto)
        => !Punto.TryLeggi(lat, lon, out var letto) ? null : letto.Posizione ?? punto(lat.Trim());

    // Il punto nel poligono, col raggio orizzontale; il poligono si chiude da sé.
    private static bool Dentro(Coordinate p, List<Coordinate> poligono)
    {
        bool dentro = false;
        for (int i = 0, j = poligono.Count - 1; i < poligono.Count; j = i++)
        {
            var (a, b) = (poligono[i], poligono[j]);
            if (a.LatitudeDeg > p.LatitudeDeg != b.LatitudeDeg > p.LatitudeDeg
                && p.LongitudeDeg < (b.LongitudeDeg - a.LongitudeDeg) * (p.LatitudeDeg - a.LatitudeDeg) / (b.LatitudeDeg - a.LatitudeDeg) + a.LongitudeDeg)
            {
                dentro = !dentro;
            }
        }

        return dentro;
    }

    [GeneratedRegex(@"^\d{1,3}$")]
    private static partial Regex Centinaia();

    [GeneratedRegex(@"^\d{1,3}/TRL$")]
    private static partial Regex ConTransizione();

    [GeneratedRegex(@"^\*\d{1,3}(/\d{1,3})?$")]
    private static partial Regex ConNota();

    [GeneratedRegex(@"^FL(?<n>\d{1,3})$")]
    private static partial Regex Livello();

    [GeneratedRegex(@"^(?<n>\d{4,5})(FT)?$")]
    private static partial Regex Piedi();
}
