using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

public static partial class Validatore
{
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

        // Che cosa carica ogni .isc lo dice il motore in un posto solo (CarichiDegliIsc): lo chiede anche il catalogo
        // dei punti dell'app (carta F3, slice 3), e due risposte diverse alla stessa domanda si separerebbero.
        var carichi = CarichiDegliIsc.Leggi(radice, percorso =>
            (Esito(percorso)?.Record.OfType<AirportInfo>() ?? Enumerable.Empty<AirportInfo>()).Select(a => a.IcaoCode));

        foreach (var carico in carichi)
        {
            string nomeMaster = carico.Nome;
            foreach (var (file, riga, testo, citato) in carico.Mancanti)
            {
                problemi.Add(new(Regola.FileCitatoAssente, file, riga, testo,
                    file.EndsWith(".isc", StringComparison.OrdinalIgnoreCase)
                        ? $"«{citato}» non c'è sotto Include/{carico.CartellaDati} né sotto Include"
                        : $"«{citato}» non c'è"));
            }

            var caricati = carico.Caricati;
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
