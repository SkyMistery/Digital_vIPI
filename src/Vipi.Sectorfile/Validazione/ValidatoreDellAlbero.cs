using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

public static partial class Validatore
{
    // Aurora carica da sé, senza F;, i file che portano il codice di uno scalo di [AIRPORT] (SPECIFICA_FORMATI §3 di B).
    private static readonly string[] EstensioniPerIcao = { "gts", "txi", "sid", "str", "vfi", "vrt", "mva", "tfl", "geo", "atis", "pol" };

    // I cataloghi dove si cerca un nome, e quelli dove un nome deve essere unico.
    private static readonly string[] CataloghiUnici = { "fix", "vor", "ndb" };

    /// <summary>
    /// Tutte le regole su un albero del sector (carta F2 §3, slice 8): quelle di ogni file che il motore interpreta, e
    /// quelle che vogliono gli <c>.isc</c> — file citati e assenti, file mai citati, nomi non risolti, nomi doppi.
    /// </summary>
    /// <param name="cartellaSectorFiles">La cartella con gli <c>.isc</c> e <c>Include/</c> (<c>SectorFiles</c>).</param>
    /// <remarks>
    /// <para>Un file si carica se un <c>.isc</c> lo cita con <c>F;</c> (dalla cartella di <c>[INFO]</c>, <c>IT</c>; se lì
    /// non c'è, da <c>Include/</c>: <c>LIBB.isc</c> scrive <c>F;IT\colors\colors.def</c>), se porta il codice di uno scalo
    /// di <c>[AIRPORT]</c> (per ICAO), o se un <c>.frq</c> caricato lo nomina (profili <c>.cpr</c>, <c>.datis</c>).</para>
    /// <para>I nomi si risolvono, per ogni <c>.isc</c>, in tutto ciò che quell'<c>.isc</c> carica: fix, VOR, NDB, scali,
    /// VRP (nome e codice). I percorsi nei problemi sono relativi a <paramref name="cartellaSectorFiles"/>.</para>
    /// </remarks>
    public static IReadOnlyList<ProblemaDelSector> ValidaLAlbero(string cartellaSectorFiles)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartellaSectorFiles);
        string radice = Path.GetFullPath(cartellaSectorFiles);
        string include = Path.Combine(radice, "Include");

        // Tutti i file sotto Include, per percorso minuscolo con le barre dritte: Aurora gira su Windows.
        var indice = Directory.Exists(include)
            ? Directory.GetFiles(include, "*", SearchOption.AllDirectories)
                .ToDictionary(p => Chiave(Path.GetRelativePath(include, p)), StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        var esiti = new Dictionary<string, EsitoDelFile?>(StringComparer.Ordinal);
        EsitoDelFile? Esito(string percorso)
        {
            if (!esiti.TryGetValue(percorso, out var esito))
            {
                esito = LeggiIlFile(percorso, Relativo(percorso));
                esiti[percorso] = esito;
            }

            return esito;
        }

        string Relativo(string percorso) => Path.GetRelativePath(radice, percorso);

        var problemi = new List<ProblemaDelSector>();
        var caricatiDaQualcuno = new HashSet<string>(StringComparer.Ordinal);
        var nonRisolti = new Dictionary<(string File, int Riga, string Nome), (string Testo, List<string> Master)>();

        foreach (string master in Directory.GetFiles(radice, "*.isc").Order(StringComparer.Ordinal))
        {
            string nomeMaster = Path.GetFileName(master);
            var righe = SectorFileReader.Read(master).Lines;
            var info = new List<string>();
            var citati = new List<(string Sezione, string Percorso)>();
            string sezione = string.Empty;
            var daRisolvere = new List<(string Sezione, string Citato, int Riga, string Testo)>();

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

            foreach (var (sez, citato, riga, testo) in daRisolvere)
            {
                if (Risolvi(citato) is { } trovato)
                {
                    citati.Add((sez, trovato));
                }
                else
                {
                    problemi.Add(new(Regola.FileCitatoAssente, nomeMaster, riga, testo, $"«{citato}» non c'è sotto Include/{cartellaDati} né sotto Include"));
                }
            }

            var caricati = new HashSet<string>(citati.Select(c => c.Percorso), StringComparer.Ordinal);

            // Per ICAO: i codici degli scali dei file di [AIRPORT].
            var scali = citati.Where(c => c.Sezione == "AIRPORT")
                .SelectMany(c => Esito(c.Percorso)?.Record.OfType<AirportInfo>() ?? Enumerable.Empty<AirportInfo>())
                .Select(a => a.IcaoCode.Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string icao in scali)
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
                                problemi.Add(new(Regola.FileCitatoAssente, Relativo(frq), i + 1, righeFrq[i], $"«{campo}» non c'è"));
                            }
                        }
                    }
                }
            }

            caricatiDaQualcuno.UnionWith(caricati);

            // I cataloghi di questo .isc.
            var dichiarati = caricati.Order(StringComparer.Ordinal)
                .SelectMany(p => (Esito(p)?.Dichiarati ?? Array.Empty<NomeDichiarato>()).Select(d => (File: p, Nome: d)))
                .ToList();
            var noti = dichiarati.Select(d => d.Nome.Nome).ToHashSet(StringComparer.Ordinal);

            foreach (var gruppo in dichiarati.Where(d => CataloghiUnici.Contains(d.Nome.Catalogo))
                .GroupBy(d => (d.Nome.Catalogo, d.Nome.Nome))
                .Where(g => g.Count() > 1))
            {
                var primo = gruppo.First();
                foreach (var altro in gruppo.Skip(1))
                {
                    // Sotto un decimo di miglio è lo stesso punto scritto due volte (arrotondamenti: sull'albero del 22
                    // settembre 2026 fra 4 e 150 m), non un'ambiguità; sopra, Aurora ne prende uno e non si sa quale.
                    double metri = Metri(primo.Nome.Posizione, altro.Nome.Posizione);
                    string distanza = metri < 1
                        ? string.Empty
                        : ", a " + (metri / 1852).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " NM";
                    problemi.Add(new(metri < 185.2 ? Regola.NomeRipetuto : Regola.NomeDuplicato, Relativo(altro.File), altro.Nome.Riga,
                        altro.Nome.Testo, $"{gruppo.Key.Catalogo} «{gruppo.Key.Nome}» già in {Relativo(primo.File)}:{primo.Nome.Riga}{distanza}"));
                }
            }

            foreach (string file in caricati)
            {
                foreach (var usato in Esito(file)?.Usati ?? Array.Empty<NomeUsato>())
                {
                    if (!noti.Contains(usato.Nome))
                    {
                        var chiave = (Relativo(file), usato.Riga, usato.Nome);
                        if (!nonRisolti.TryGetValue(chiave, out var dove))
                        {
                            dove = (usato.Testo, new List<string>());
                            nonRisolti[chiave] = dove;
                        }

                        dove.Master.Add(nomeMaster);
                    }
                }
            }
        }

        problemi.AddRange(nonRisolti.Select(n => new ProblemaDelSector(Regola.NomeNonRisolto, n.Key.File, n.Key.Riga, n.Value.Testo,
            $"«{n.Key.Nome}» non è nei cataloghi di {string.Join(", ", n.Value.Master)}")));

        // I file mai caricati (fuori i testi: note, changelog).
        foreach (string percorso in indice.Values.Order(StringComparer.Ordinal))
        {
            string estensione = Path.GetExtension(percorso).ToLowerInvariant();
            if (!caricatiDaQualcuno.Contains(percorso) && estensione is not ("" or ".md" or ".txt"))
            {
                problemi.Add(new(Regola.FileMaiCitato, Relativo(percorso), 0, string.Empty, "nessun .isc lo carica: né F;, né per ICAO, né da un .frq"));
            }
        }

        // Le regole di ogni file che il motore interpreta.
        foreach (string percorso in indice.Values.Order(StringComparer.Ordinal))
        {
            problemi.AddRange(Esito(percorso)?.Problemi ?? Array.Empty<ProblemaDelSector>());
        }

        return problemi.Distinct().OrderBy(p => p.File, StringComparer.Ordinal).ThenBy(p => p.Riga).ThenBy(p => p.Regola).ToList();
    }

    // Distanza in metri, piana: basta per dire «stesso punto» o «a quante miglia».
    private static double Metri(Shared.Coordinate a, Shared.Coordinate b)
    {
        double dy = (a.LatitudeDeg - b.LatitudeDeg) * 111_320;
        double dx = (a.LongitudeDeg - b.LongitudeDeg) * 111_320 * Math.Cos(a.LatitudeDeg * Math.PI / 180);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static string Chiave(string percorso) => percorso.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
