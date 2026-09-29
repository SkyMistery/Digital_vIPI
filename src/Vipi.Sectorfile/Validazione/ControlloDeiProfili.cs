using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli dei profili <c>.cpr</c> (lotto «Subito» slice 11d, «file per file» N2, N3): la finestra PAR senza didascalia
/// o con una pista che il <c>.rw</c> non ha, radiale ed elevazione lontane dal <c>.rw</c>, il profilo con molte
/// impostazioni fuori dai PAR.
/// </summary>
/// <remarks>
/// Misure sul fork del 29 settembre: radiale entro ±0,2° dalla rotta del <c>.rw</c> ed elevazione entro 1 ft, tranne
/// <c>LIBV</c> (137 contro 138 in tutte e sei le finestre); <c>LIPI RWY06</c> in <c>LIPI.cpr</c> e <c>LIPA.cpr</c> (il
/// <c>.rw</c> ha 06L e 06R), <c>LIBN.cpr</c> INSET3 senza didascalia. I profili generici hanno 2-4 impostazioni: oltre 20
/// fuori dai PAR (scelta dell'agente) è un profilo che toglie all'utente le sue. NON sono avvisi (committente): un profilo
/// non usato, un PAR di un altro scalo, le righe prima delle sezioni.
/// </remarks>
public static partial class ControlloDeiProfili
{
    // Gli scarti ammessi, più un nulla per i decimali scritti (257.9 contro 258.1 è 0,2 esatto, non di più).
    private const double ScartoDellaRadiale = 0.2 + 1e-3;
    private const double ScartoDellElevazione = 1 + 1e-3;
    private const int MolteImpostazioni = 20;

    /// <summary>
    /// I problemi dei profili. <paramref name="profili"/>: per ogni <c>.cpr</c> il percorso da mostrare e le impostazioni;
    /// <paramref name="piste"/>: le piste di tutti i <c>.rw</c>; <paramref name="testoDellaRiga"/>: la riga del disco.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<ImpostazioneDelProfilo> Impostazioni)> profili,
                                                    IEnumerable<Runway> piste, Func<string, int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(profili);
        ArgumentNullException.ThrowIfNull(piste);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        // Per scalo e verso: elevazione della soglia e rotta, com'è scritta. La prima copia vale (i gemelli sono uguali).
        var versi = new Dictionary<(string Scalo, string Verso), (int Elevazione, float Rotta)>();
        foreach (var p in piste)
        {
            versi.TryAdd((p.IcaoCode.Trim().ToUpperInvariant(), p.Designator1.Trim().ToUpperInvariant()), (p.ElevThresh1Ft, p.TrueHeading1));
            if (p.TrueHeading2 is { } opposta)
                versi.TryAdd((p.IcaoCode.Trim().ToUpperInvariant(), p.Designator2.Trim().ToUpperInvariant()), (p.ElevThresh2Ft, opposta));
        }

        foreach (var (relativo, impostazioni) in profili)
        {
            int fuoriDaiPar = impostazioni.Count(i => !EUnaFinestra(i.Sezione) && !i.Chiave.Trim().StartsWith("PAR_", StringComparison.OrdinalIgnoreCase));
            if (fuoriDaiPar > MolteImpostazioni)
            {
                int prima = impostazioni[0].Source.LineNumber;
                yield return new(Regola.ProfiloConMolteImpostazioni, relativo, prima, testoDellaRiga(relativo, prima),
                    $"{fuoriDaiPar} impostazioni fuori dai PAR: ognuna sovrascrive quella dell'utente a ogni connessione — solo quelle che servono");
            }

            foreach (var tutta in impostazioni.Where(i => EUnaFinestra(i.Sezione)).GroupBy(i => i.Sezione.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                // Un INI si legge per sezione e chiave: in [INSET3] Aurora cerca INS3…, e un INS4… lì non lo legge (LIBN.cpr).
                string numero = tutta.Key[5..];
                var altrui = tutta.Where(i => Prefisso().Match(i.Chiave.Trim()) is { Success: true } m && !string.Equals(m.Value[3..], numero, StringComparison.Ordinal)).ToList();
                if (altrui.Count > 0)
                {
                    int riga = altrui[0].Source.LineNumber;
                    yield return new(Regola.ChiaveFuoriSezione, relativo, riga, testoDellaRiga(relativo, riga),
                        $"{altrui.Count} chiavi di un'altra finestra in [{tutta.Key}] ({altrui[0].Chiave.Trim()}…): Aurora le cerca nella loro sezione, e qui non le legge");
                }

                var finestra = tutta.Except(altrui).ToList();
                if (finestra.Count == 0)
                    continue;
                var perChiave = finestra.GroupBy(i => Suffisso(i.Chiave), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                // Una finestra PAR ha VIEW_TYPE=2 o le chiavi PAR_ (LIBN.cpr INSET3 ha solo VIEW_TYPE=2: vuota).
                if (!perChiave.Keys.Any(k => k.StartsWith("PAR_", StringComparison.OrdinalIgnoreCase))
                    && perChiave.GetValueOrDefault("VIEW_TYPE")?.Valore.Trim() != "2")
                    continue;

                var didascalia = perChiave.GetValueOrDefault("PAR_CAPTION");
                int rigaDellaFinestra = (didascalia ?? finestra[0]).Source.LineNumber;
                var letta = didascalia is null ? null : Didascalia().Match(didascalia.Valore.Trim());
                if (letta is not { Success: true })
                {
                    yield return new(Regola.ParSenzaPista, relativo, rigaDellaFinestra, testoDellaRiga(relativo, rigaDellaFinestra),
                        didascalia is null || didascalia.Valore.Trim().Length == 0
                            ? $"{tutta.Key}: la finestra PAR non ha didascalia (PAR_CAPTION): non si sa di che pista è"
                            : $"{tutta.Key}: la didascalia «{didascalia.Valore.Trim()}» non dice «ICAO RWYnn»");
                    continue;
                }

                string scalo = letta.Groups["scalo"].Value.ToUpperInvariant(), verso = letta.Groups["verso"].Value.ToUpperInvariant();
                if (!versi.TryGetValue((scalo, verso), out var dati))
                {
                    var suoi = versi.Keys.Where(k => k.Scalo == scalo).Select(k => k.Verso).Order(StringComparer.Ordinal).ToList();
                    // Uno scalo che il .rw non ha del tutto non si controlla: il PAR può essere di un campo fuori dal sector.
                    if (suoi.Count > 0)
                    {
                        yield return new(Regola.ParSenzaPista, relativo, rigaDellaFinestra, testoDellaRiga(relativo, rigaDellaFinestra),
                            $"{tutta.Key}: {scalo} non ha la pista {verso} nel .rw (ha {string.Join(", ", suoi)})");
                    }

                    continue;
                }

                if (perChiave.GetValueOrDefault("PAR_Radial") is { } radiale && Numero(radiale.Valore) is { } gradi
                    && Math.Abs(((gradi - dati.Rotta + 540) % 360) - 180) > ScartoDellaRadiale)
                {
                    yield return new(Regola.RadialeDelPar, relativo, radiale.Source.LineNumber, testoDellaRiga(relativo, radiale.Source.LineNumber),
                        $"{tutta.Key}: radiale {radiale.Valore.Trim()}, il .rw dà a {scalo} {verso} la rotta {Scritto(dati.Rotta)}",
                        radiale.Chiave + "=" + Scritto(dati.Rotta));
                }

                if (perChiave.GetValueOrDefault("Par_Elevation") is { } elevazione && Numero(elevazione.Valore) is { } piedi
                    && Math.Abs(piedi - dati.Elevazione) > ScartoDellElevazione)
                {
                    yield return new(Regola.ElevazioneDelPar, relativo, elevazione.Source.LineNumber, testoDellaRiga(relativo, elevazione.Source.LineNumber),
                        $"{tutta.Key}: elevazione {elevazione.Valore.Trim()} ft, il .rw dà alla soglia di {scalo} {verso} {dati.Elevazione} ft",
                        elevazione.Chiave + "=" + dati.Elevazione.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }

    private static bool EUnaFinestra(string sezione) => sezione.Trim().StartsWith("INSET", StringComparison.OrdinalIgnoreCase);

    // INS1PAR_Radial → PAR_Radial: la chiave senza il prefisso della finestra.
    private static string Suffisso(string chiave) => Prefisso().Replace(chiave.Trim(), string.Empty);

    private static double? Numero(string valore)
        => double.TryParse(valore.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : null;

    private static string Scritto(double valore) => valore.ToString("0.##", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^INS\d+", RegexOptions.IgnoreCase)]
    private static partial Regex Prefisso();

    [GeneratedRegex(@"^(?<scalo>[A-Z]{4})\s*RWY\s*(?<verso>\d{2}[LRC]?)", RegexOptions.IgnoreCase)]
    private static partial Regex Didascalia();
}
