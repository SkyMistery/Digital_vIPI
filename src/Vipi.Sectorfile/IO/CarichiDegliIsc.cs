using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Che cosa carica un <c>.isc</c> (un «master»): il suo nome, la cartella dei dati e tutti i file che Aurora legge
/// aprendolo. In un posto solo, perché lo chiedono in due — il validatore dell'albero (carta F2 §3) e il catalogo dei
/// punti dell'app (carta F3, slice 3): due risposte diverse alla stessa domanda si separerebbero al primo cambiamento.
/// </summary>
/// <param name="Isc">Il percorso dell'<c>.isc</c>.</param>
/// <param name="CartellaDati">La cartella di <c>[INFO]</c> (di solito <c>IT</c>): da lì si risolvono i citati.</param>
/// <param name="Caricati">I percorsi dei file caricati, assoluti.</param>
/// <param name="Mancanti">I file citati e non trovati: dove li cita (file, riga, testo) e il nome citato.</param>
public sealed record CaricoDiUnIsc(
    string Isc,
    string CartellaDati,
    IReadOnlySet<string> Caricati,
    IReadOnlyList<(string File, int Riga, string Testo, string Citato)> Mancanti)
{
    /// <summary>Il nome del file <c>.isc</c>, com'è nei problemi del validatore.</summary>
    public string Nome => Path.GetFileName(Isc);
}

/// <summary>Le tre vie per cui un file entra in un <c>.isc</c>: <c>F;</c>, il codice di uno scalo, un <c>.frq</c>.</summary>
public static class CarichiDegliIsc
{
    // Aurora carica da sé, senza F;, i file che portano il codice di uno scalo di [AIRPORT] (SPECIFICA_FORMATI §3 di B).
    private static readonly string[] EstensioniPerIcao =
        ["gts", "txi", "sid", "str", "vfi", "vrt", "mva", "tfl", "geo", "atis", "pol"];

    /// <summary>
    /// Legge ogni <c>.isc</c> di <paramref name="cartellaSectorFiles"/> e dice che cosa carica.
    /// </summary>
    /// <param name="cartellaSectorFiles">La cartella con gli <c>.isc</c> e <c>Include/</c> (<c>SectorFiles</c>).</param>
    /// <param name="scaliDi">
    /// Gli ICAO degli scali dichiarati da un file di <c>[AIRPORT]</c>: lo chiede a chi ha già letto l'albero, così il
    /// motore non decide qui come si legge un file (il validatore passa il suo lettore, l'app i record che ha in mano).
    /// </param>
    public static IReadOnlyList<CaricoDiUnIsc> Leggi(string cartellaSectorFiles, Func<string, IEnumerable<string>> scaliDi)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartellaSectorFiles);
        ArgumentNullException.ThrowIfNull(scaliDi);

        string radice = Path.GetFullPath(cartellaSectorFiles);
        string include = Path.Combine(radice, "Include");

        // Tutti i file sotto Include, per percorso minuscolo con le barre dritte: Aurora gira su Windows.
        var indice = Directory.Exists(include)
            ? Directory.GetFiles(include, "*", SearchOption.AllDirectories)
                .ToDictionary(p => Chiave(Path.GetRelativePath(include, p)), StringComparer.Ordinal)
            : [];

        var carichi = new List<CaricoDiUnIsc>();
        foreach (string master in Directory.GetFiles(radice, "*.isc").Order(StringComparer.Ordinal))
        {
            carichi.Add(Uno(radice, master, indice, scaliDi));
        }

        return carichi;
    }

    /// <summary>Tutti i file che almeno un <c>.isc</c> carica.</summary>
    public static IReadOnlySet<string> Tutti(IEnumerable<CaricoDiUnIsc> carichi)
    {
        ArgumentNullException.ThrowIfNull(carichi);
        var tutti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var carico in carichi)
            tutti.UnionWith(carico.Caricati);
        return tutti;
    }

    private static CaricoDiUnIsc Uno(string radice, string master, Dictionary<string, string> indice,
                                     Func<string, IEnumerable<string>> scaliDi)
    {
        var righe = SectorFileReader.Read(master).Lines;
        var info = new List<string>();
        var daRisolvere = new List<(string Sezione, string Citato, int Riga, string Testo)>();
        string sezione = string.Empty;

        for (int i = 0; i < righe.Count; i++)
        {
            string t = righe[i].Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                sezione = t[1..^1].Trim().ToUpperInvariant();
            }
            else if (sezione == "INFO")
            {
                info.Add(t);
            }
            else if (t.StartsWith("F;", StringComparison.OrdinalIgnoreCase))
            {
                daRisolvere.Add((sezione, t[2..].Trim().TrimEnd(';'), i + 1, righe[i]));
            }
        }

        string cartellaDati = info.Count >= 6 ? info[5] : "IT";
        // Il percorso si unisce a mano: `\liml.atis` (itfreq.frq) è relativo alla cartella dei dati, ma per
        // Path.Combine sarebbe un percorso dalla radice, e su Linux la barra rovescia non separa niente.
        string? Risolvi(string citato)
            => indice.GetValueOrDefault(Chiave(cartellaDati + "/" + Chiave(citato)))
               ?? indice.GetValueOrDefault(Chiave(citato));

        string nomeMaster = Path.GetFileName(master);
        var mancanti = new List<(string File, int Riga, string Testo, string Citato)>();
        var citati = new List<(string Sezione, string Percorso)>();
        foreach (var (sez, citato, riga, testo) in daRisolvere)
        {
            if (Risolvi(citato) is { } trovato)
            {
                citati.Add((sez, trovato));
            }
            else
            {
                mancanti.Add((nomeMaster, riga, testo, citato));
            }
        }

        var caricati = new HashSet<string>(citati.Select(c => c.Percorso), StringComparer.Ordinal);

        // Per ICAO: i codici degli scali dei file di [AIRPORT].
        foreach (string icao in citati.Where(c => c.Sezione == "AIRPORT")
                     .SelectMany(c => scaliDi(c.Percorso))
                     .Select(c => c.Trim())
                     .Where(c => c.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string estensione in EstensioniPerIcao)
            {
                if (Risolvi(icao + "." + estensione) is { } automatico)
                {
                    caricati.Add(automatico);
                }
            }
        }

        // I file che un .frq caricato nomina: profili .cpr e testi ATIS.
        foreach (string frq in caricati.Where(p => p.EndsWith(".frq", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var righeFrq = SectorFileReader.Read(frq).Lines;
            for (int i = 0; i < righeFrq.Count; i++)
            {
                if (righeFrq[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string campo in righeFrq[i].Split(';').Select(c => c.Trim()))
                {
                    if (campo.EndsWith(".cpr", StringComparison.OrdinalIgnoreCase)
                        || campo.EndsWith(".datis", StringComparison.OrdinalIgnoreCase)
                        || campo.EndsWith(".atis", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Risolvi(campo) is { } nominato)
                        {
                            caricati.Add(nominato);
                        }
                        else
                        {
                            mancanti.Add((Path.GetRelativePath(radice, frq), i + 1, righeFrq[i], campo));
                        }
                    }
                }
            }
        }

        return new CaricoDiUnIsc(master, cartellaDati, caricati, mancanti);
    }

    private static string Chiave(string percorso) => percorso.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
