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

            // Lotto «Subito» slice 2b: il file che Aurora trova per nome (§C, M6), quello incluso due volte (D7), quello
            // sotto la sezione sbagliata (F6).
            foreach (var (riga, testo, citato, trovato) in carico.TrovatiPerNome)
            {
                problemi.Add(new(Regola.FileCitatoAssente, carico.Nome, riga, testo,
                    $"«{citato}» non c'è lì: Aurora lo trova per nome in {Relativo(trovato)}"));
            }

            foreach (var (riga, testo, percorso, primaRiga) in carico.Doppi)
            {
                problemi.Add(new(Regola.FileInclusoDueVolte, carico.Nome, riga, testo,
                    $"{Relativo(percorso)} è già alla riga {primaRiga}: Aurora lo carica due volte"));
            }

            foreach (var (riga, testo, percorso, _, perche) in carico.FuoriSezione)
            {
                problemi.Add(new(Regola.FileNellaSezioneSbagliata, carico.Nome, riga, testo, $"{Relativo(percorso)}: {perche}"));
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

            // Le attese in rotta e i nomi in più cataloghi (lotto «Subito» slice 10b).
            problemi.AddRange(ControlloDelleAttese.DelMaster(nomeMaster,
                caricati.Order(StringComparer.Ordinal).Where(p => Esito(p) is not null)
                    .Select(p => (p, Relativo(p), Esito(p)!.Record)).ToList(),
                TestoDellaRiga));

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

        // I file mai caricati (fuori i testi: note, changelog). Se le sue coordinate stanno in un file caricato, è una
        // copia rimasta indietro (V2, lotto «Subito» slice 2b: `limw.pol` e `GND_LAYOUT\mw_ad_gnd.pol`).
        Dictionary<string, List<string>>? doveStaUnaCoordinata = null;
        foreach (string percorso in indice.Values.Order(StringComparer.Ordinal))
        {
            string estensione = Path.GetExtension(percorso).ToLowerInvariant();
            if (!caricatiDaQualcuno.Contains(percorso) && estensione is not ("" or ".md" or ".txt"))
            {
                doveStaUnaCoordinata ??= IndiceDelleCoordinate(caricatiDaQualcuno);
                problemi.Add(new(Regola.FileMaiCitato, Relativo(percorso), 0, string.Empty,
                    "nessun .isc lo carica: né F;, né per ICAO, né da un .frq" + CopiaDi(percorso, doveStaUnaCoordinata, Relativo)));
            }
        }

        // I file senza una riga di dati (A9: `ACC\test.artcc`, incluso da ITALY.isc; i .fix vuoti di NAVAIDS). Solo quelli
        // che il motore legge: un testo vuoto non è un problema.
        foreach (string percorso in indice.Values.Order(StringComparer.Ordinal))
        {
            if (Esito(percorso) is not null && SectorFileReader.Read(percorso).Lines
                    .All(r => r.Trim().Length == 0 || r.TrimStart().StartsWith("//", StringComparison.Ordinal)))
            {
                problemi.Add(new(Regola.FileVuoto, Relativo(percorso), 0, string.Empty,
                    caricatiDaQualcuno.Contains(percorso) ? "incluso, ma senza una riga di dati" : "senza una riga di dati"));
            }
        }

        // Le regole di ogni file che il motore interpreta.
        foreach (string percorso in indice.Values.Order(StringComparer.Ordinal))
        {
            problemi.AddRange(Esito(percorso)?.Problemi ?? Array.Empty<ProblemaDelSector>());
        }

        // Le procedure (lotto «Subito» slice 9a): ripetute, 6° campo fuori posto, di un altro scalo, su piste che non ci sono.
        var versi = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string percorso in indice.Values.Where(p => p.EndsWith(".rw", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var pista in Esito(percorso)?.Record.OfType<Runway>() ?? [])
            {
                if (!versi.TryGetValue(pista.IcaoCode.Trim(), out var suoi))
                    versi[pista.IcaoCode.Trim()] = suoi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                suoi.Add(pista.Designator1.Trim());
                suoi.Add(pista.Designator2.Trim());
            }
        }

        var versiDelloScalo = versi.ToDictionary(v => v.Key, v => (IReadOnlySet<string>)v.Value, StringComparer.OrdinalIgnoreCase);
        foreach (string percorso in indice.Values.Where(p => p.EndsWith(".sid", StringComparison.OrdinalIgnoreCase)
                                                            || p.EndsWith(".str", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
        {
            if (Esito(percorso) is { } esito)
                problemi.AddRange(ControlloDelleProcedure.Di(Relativo(percorso), esito.Record, versiDelloScalo, r => TestoDellaRiga(percorso, r)));
        }

        // Le posizioni dei .frq (lotto «Subito» slice 11a): include dopo escluso, posizione citata e mai definita, ripetuta.
        var frq = indice.Values.Where(p => p.EndsWith(".frq", StringComparison.OrdinalIgnoreCase) && Esito(p) is not null)
            .Order(StringComparer.Ordinal).ToList();
        var percorsoDi = frq.ToDictionary(Relativo, p => p, StringComparer.Ordinal);
        problemi.AddRange(ControlloDellePosizioni.Di([.. frq.Select(p => (Relativo(p), Esito(p)!.Record))],
            (relativo, riga) => TestoDellaRiga(percorsoDi[relativo], riga)));

        // Il CPDLC (lotto «Subito» slice 11c): gruppi senza messaggi, messaggi senza risposta, valori, elenchi del manuale.
        var cpdlc = indice.Values.Where(p => (p.EndsWith(".cpdlc", StringComparison.OrdinalIgnoreCase)
                                              || p.EndsWith(".cpdlcnames", StringComparison.OrdinalIgnoreCase)) && Esito(p) is not null)
            .Order(StringComparer.Ordinal).ToList();
        var percorsoDelCpdlc = cpdlc.ToDictionary(Relativo, p => p, StringComparer.Ordinal);
        problemi.AddRange(ControlloDelCpdlc.Di([.. cpdlc.Select(p => (Relativo(p), Esito(p)!.Record))],
            (relativo, riga) => TestoDellaRiga(percorsoDelCpdlc[relativo], riga)));

        // I profili .cpr e i loro PAR, contro le piste di tutti i .rw (lotto «Subito» slice 11d).
        var profili = indice.Values.Where(p => p.EndsWith(".cpr", StringComparison.OrdinalIgnoreCase) && Esito(p) is not null)
            .Order(StringComparer.Ordinal).ToList();
        var percorsoDelProfilo = profili.ToDictionary(Relativo, p => p, StringComparer.Ordinal);
        problemi.AddRange(ControlloDeiProfili.Di(
            [.. profili.Select(p => (Relativo(p), (IReadOnlyList<ImpostazioneDelProfilo>)[.. Esito(p)!.Record.OfType<ImpostazioneDelProfilo>()]))],
            indice.Values.Where(p => p.EndsWith(".rw", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
                .SelectMany(p => Esito(p)?.Record.OfType<Runway>() ?? []),
            (relativo, riga) => TestoDellaRiga(percorsoDelProfilo[relativo], riga)));

        // La terra (lotto «Subito» slice 12a): stand ed etichette contro il loro scalo e le sue taxiway, tipi e colori.
        var terra = indice.Values.Where(p => Path.GetExtension(p).ToLowerInvariant() is ".txi" or ".gts" or ".geo" or ".pol"
                                                 or ".danger" or ".restrict" or ".prohibit" && Esito(p) is not null)
            .Order(StringComparer.Ordinal).ToList();
        var percorsoDellaTerra = terra.ToDictionary(Relativo, p => p, StringComparer.Ordinal);
        var coloriDefiniti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string def in indice.Values.Where(p => p.EndsWith(".def", StringComparison.OrdinalIgnoreCase)))
        {
            coloriDefiniti.UnionWith(SectorFileReader.Read(def).Lines.Select(r => r.Trim())
                .Where(r => r.Length > 0 && !r.StartsWith("//", StringComparison.Ordinal) && r.Contains(';'))
                .Select(r => r.Split(';')[0].Trim()));
        }

        problemi.AddRange(ControlloDellaTerra.Di([.. terra.Select(p => (Relativo(p), Esito(p)!.Record))],
            indice.Values.Where(p => p.EndsWith(".ap", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
                .SelectMany(p => Esito(p)?.Record.OfType<AirportInfo>() ?? []),
            coloriDefiniti, (relativo, riga) => TestoDellaRiga(percorsoDellaTerra[relativo], riga),
            record => terra.Select(p => Esito(p)!.Chiavi.GetValueOrDefault(record)).FirstOrDefault(c => c is not null)));

        // Le copie gemelle diverse (carta F3-bis §2.1): uno scalo, una pista, una posizione con un altro valore nel file
        // nazionale e in quello della FIR. Una per copia fuori posto, col valore che hanno le altre.
        var famiglie = indice.Values.Where(p => CopieGemelle.Famiglia(p) is not null && Esito(p) is not null)
            .Select(p => (File: p, Record: Esito(p)!.Record)).ToList();
        foreach (var gruppo in CopieGemelle.Trova(famiglie))
        {
            foreach (var (copia, campi) in CopieGemelle.Divergenti(gruppo))
            {
                var suoi = CopieGemelle.Campi(copia.Record).ToDictionary(c => c.Campo, c => c.Valore, StringComparer.Ordinal);
                var altrove = gruppo.Copie.Where(c => !ReferenceEquals(c, copia)).Select(altra =>
                {
                    var loro = CopieGemelle.Campi(altra.Record).ToDictionary(c => c.Campo, c => c.Valore, StringComparer.Ordinal);
                    var diversi = campi.Where(c => loro.GetValueOrDefault(c) != suoi.GetValueOrDefault(c)).ToList();
                    return diversi.Count == 0
                        ? null
                        : $"{Path.GetFileName(altra.File)}:{altra.Riga} " + string.Join(", ", diversi.Select(c => $"{c} {loro.GetValueOrDefault(c)}"));
                }).Where(t => t is not null);
                string qui = string.Join(", ", campi.Select(c => $"{c} {suoi.GetValueOrDefault(c)}"));
                string ordine = gruppo.PerOrdine ? string.Empty : " (la chiave si ripete in un file: le copie non si abbinano)";
                problemi.Add(new(Regola.CopieDiverse, Relativo(copia.File), copia.Riga, TestoDellaRiga(copia.File, copia.Riga),
                    $"«{gruppo.Chiave}»: qui {qui}; in {string.Join("; ", altrove)}{ordine}"));
            }
        }

        return problemi.Distinct().OrderBy(p => p.File, StringComparer.Ordinal).ThenBy(p => p.Riga).ThenBy(p => p.Regola).ToList();
    }

    // Le coordinate (lat;lon come sono scritte) di ogni file caricato: dove sta ognuna.
    private static Dictionary<string, List<string>> IndiceDelleCoordinate(IEnumerable<string> caricati)
    {
        var dove = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string file in caricati.Order(StringComparer.Ordinal))
        {
            foreach (string coppia in CoordinateDel(file))
            {
                if (!dove.TryGetValue(coppia, out var files))
                {
                    dove[coppia] = files = [];
                }

                if (files.Count == 0 || files[^1] != file)
                {
                    files.Add(file);
                }
            }
        }

        return dove;
    }

    // «, è una copia di X: le sue N coordinate ci stanno tutte» (o «M su N»), se almeno il 90% sta in un file solo.
    private static string CopiaDi(string orfano, Dictionary<string, List<string>> dove, Func<string, string> relativo)
    {
        var sue = CoordinateDel(orfano);
        if (sue.Count == 0)
        {
            return string.Empty;
        }

        var migliore = sue.SelectMany(c => dove.GetValueOrDefault(c) ?? []).GroupBy(f => f, StringComparer.Ordinal)
            .Select(g => (File: g.Key, Quante: g.Count())).OrderByDescending(g => g.Quante)
            // A pari coordinate, un file dello stesso formato: `limw.pol` è la copia di `mw_ad_gnd.pol`, non di `limw.geo`.
            .ThenByDescending(g => string.Equals(Path.GetExtension(g.File), Path.GetExtension(orfano), StringComparison.OrdinalIgnoreCase))
            .ThenBy(g => g.File, StringComparer.Ordinal)
            .FirstOrDefault();
        if (migliore.File is null || migliore.Quante * 10 < sue.Count * 9)
        {
            return string.Empty;
        }

        return $"; è una copia di {relativo(migliore.File)}: " + (migliore.Quante == sue.Count
            ? $"le sue {sue.Count} coordinate ci stanno tutte"
            : $"{migliore.Quante} delle sue {sue.Count} coordinate ci stanno");
    }

    // Le coppie latitudine;longitudine DMS di un file, distinte, come sono scritte (fuori dai commenti).
    private static HashSet<string> CoordinateDel(string file)
    {
        var coppie = new HashSet<string>(StringComparer.Ordinal);
        foreach (string riga in SectorFileReader.Read(file).Lines)
        {
            if (riga.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            string[] campi = riga.Split(';');
            for (int i = 0; i + 1 < campi.Length; i++)
            {
                string lat = campi[i].Trim(), lon = campi[i + 1].Trim();
                if (lat.Length > 1 && char.ToUpperInvariant(lat[0]) is 'N' or 'S' && char.IsAsciiDigit(lat[1])
                    && lon.Length > 1 && char.ToUpperInvariant(lon[0]) is 'E' or 'W' && char.IsAsciiDigit(lon[1]))
                {
                    coppie.Add(lat.ToUpperInvariant() + ";" + lon.ToUpperInvariant());
                    i++;
                }
            }
        }

        return coppie;
    }

    // Distanza in metri, piana: basta per dire «stesso punto» o «a quante miglia».
    internal static double Metri(Shared.Coordinate a, Shared.Coordinate b)
    {
        double dy = (a.LatitudeDeg - b.LatitudeDeg) * 111_320;
        double dx = (a.LongitudeDeg - b.LongitudeDeg) * 111_320 * Math.Cos(a.LatitudeDeg * Math.PI / 180);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static string TestoDellaRiga(string percorso, int riga)
    {
        var righe = SectorFileReader.Read(percorso).Lines;
        return riga >= 1 && riga <= righe.Count ? righe[riga - 1] : string.Empty;
    }

    private static string Chiave(string percorso) => percorso.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
