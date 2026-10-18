using System.Text;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>
/// Gli attrezzi comuni delle prove di organizzazione dei file (I7, K2): leggere gli <c>.isc</c>, togliere o sostituire
/// righe <c>F;…</c>, aggiungere una cartella alla riga 6 di <c>[INFO]</c> (le cartelle da cui Aurora carica da sé i file
/// col nome dello scalo — manuale IVAO, «SectorFile Definition»).
/// </summary>
internal static class OrganizzazioneDeiFile
{
    /// <summary>Gli <c>.isc</c> del sector, letti, per nome.</summary>
    public static SortedDictionary<string, RigheDelFile> Isc(string radice)
    {
        var tutti = new SortedDictionary<string, RigheDelFile>(StringComparer.Ordinal);
        string cartella = Path.Combine(radice, "SectorFiles");
        if (Directory.Exists(cartella))
        {
            foreach (string isc in Directory.EnumerateFiles(cartella, "*.isc"))
                tutti["SectorFiles/" + Path.GetFileName(isc)] = RigheDelFile.Leggi(isc);
        }

        return tutti;
    }

    /// <summary>Toglie le righe <c>F;percorso</c> (senza badare a maiuscole); torna quante.</summary>
    public static int Togli(RigheDelFile isc, string percorso)
        => isc.Righe.RemoveAll(r => EIlFile(r, percorso));

    /// <summary>Sostituisce le righe <c>F;</c> di quei file con una sola (al posto della prima); torna se c'erano.</summary>
    public static bool Sostituisci(RigheDelFile isc, IReadOnlyCollection<string> vecchi, string nuovo)
    {
        int prima = isc.Righe.FindIndex(r => vecchi.Any(v => EIlFile(r, v)));
        if (prima < 0)
            return false;
        isc.Righe[prima] = "F;" + nuovo;
        for (int i = isc.Righe.Count - 1; i > prima; i--)
        {
            if (vecchi.Any(v => EIlFile(isc.Righe[i], v)))
                isc.Righe.RemoveAt(i);
        }

        return true;
    }

    /// <summary>Aggiunge una cartella alla riga 6 di <c>[INFO]</c> (la settima del file, dopo <c>[INFO]</c>), se non c'è.</summary>
    public static bool AggiungiLaCartella(RigheDelFile isc, string cartella)
    {
        int info = isc.Righe.FindIndex(r => r.Trim().Equals("[INFO]", StringComparison.OrdinalIgnoreCase));
        if (info < 0 || info + 6 >= isc.Righe.Count)
            return false;
        string riga = isc.Righe[info + 6].Trim();
        if (riga.Split(';').Contains(cartella, StringComparer.OrdinalIgnoreCase))
            return true;
        isc.Righe[info + 6] = riga.Length == 0 ? cartella : riga + ";" + cartella;
        return true;
    }

    private static bool EIlFile(string riga, string percorso)
        => riga.Trim().Equals("F;" + percorso, StringComparison.OrdinalIgnoreCase);

    /// <summary>Il testo di un file del sector, così com'è (un byte, un carattere).</summary>
    public static string Leggi(string radice, string relativo)
        => File.ReadAllText(Path.Combine(radice, relativo.Replace('/', Path.DirectorySeparatorChar)), Encoding.Latin1);

    /// <summary>Le domande di ogni prova di organizzazione dello scalo (carta «file per file», I7: cosa si verifica).</summary>
    public static List<DomandaDellaProva> DomandeDelloScalo(string scalo) =>
    [
        new($"Il layout di terra di {scalo} è identico a prima: stessi colori, stesso ordine di disegno, i riempimenti sotto le linee?", $"{scalo}, ingrandito sul piazzale e sulle piste"),
        new("Si accende e si spegne per tipo come prima (TAXI_CENTER, STOPLINE, BUILDING…)?", $"i tasti o il menu della terra, scelto {scalo}"),
        new($"I tipi di terra si caricano solo per lo scalo scelto («toggled per airport»)?", $"{scalo} e un altro scalo, scelti uno alla volta"),
        new("Il caricamento del sector e la mappa sono veloci come prima?", "il tempo dalla ricarica alla mappa pronta, e lo spostamento della mappa"),
        new("Il sector si carica senza errori nuovi?", "i messaggi di Aurora al caricamento del sector (SectorError.log)"),
    ];
}

/// <summary>
/// La prova I7-C della carta «file per file» (§9): <b>una cartella per scalo</b>. Tutti i file di LIRF in
/// <c>IT\AD\LIRF\</c> — quelli che Aurora già carica da sé (<c>.sid</c>, <c>.str</c>, <c>.gts</c>, <c>.txi</c>,
/// <c>.vfi</c>…) e quelli di terra, che oggi stanno in <c>GEO\</c> e <c>GND_LAYOUT\</c> con una riga negli <c>.isc</c>:
/// diventano <c>lirf.geo</c> e <c>lirf.tfl</c>, che per il manuale si caricano da soli. La cartella entra nella riga 6
/// di <c>[INFO]</c>, le righe di LIRF escono dagli <c>.isc</c>. (Con ~100 scali la riga 6 avrebbe ~100 cartelle: qui
/// se ne prova una.)
/// </summary>
public sealed class CartellaPerScalo : ProvaInAurora
{
    public const string Scalo = "LIRF";

    public override string Argomento => "file-di-scalo-cartella-per-scalo";

    public override string Titolo => "i file di scalo in una cartella per scalo (I7-C)";

    public override string Perche
        => "«File per file» I7, proposta C: tutto LIRF in IT\\AD\\LIRF, caricato da sé (riga 6 di [INFO]); via le sue righe dagli .isc.";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string icao = Scalo.ToLowerInvariant(), it = Path.Combine(radice, "SectorFiles", "Include", "IT");
        string cartella = $@"IT\AD\{Scalo}", dove = $"SectorFiles/Include/IT/AD/{Scalo}/";
        var terra = TerraDelloScalo.Trova(radice, Scalo);
        if (terra is null)
        {
            perche = $"Negli .isc non trovo il .geo e il .pol di {Scalo}: la prova si fa su uno scalo che li ha.";
            return null;
        }

        var file = new List<FileDellaProva>();
        var spostati = new List<string>();
        foreach (string proprio in Directory.EnumerateFiles(it, icao + ".*").Order(StringComparer.Ordinal))
        {
            string nome = Path.GetFileName(proprio);
            file.Add(new FileDellaProva(dove + nome, OrganizzazioneDeiFile.Leggi(radice, "SectorFiles/Include/IT/" + nome)));
            file.Add(new FileDellaProva("SectorFiles/Include/IT/" + nome, null));
            spostati.Add(nome);
        }

        file.Add(new FileDellaProva(dove + icao + ".geo", OrganizzazioneDeiFile.Leggi(radice, terra.Geo)));
        file.Add(new FileDellaProva(terra.Geo, null));
        file.Add(new FileDellaProva(dove + icao + ".tfl", OrganizzazioneDeiFile.Leggi(radice, terra.Pol)));
        file.Add(new FileDellaProva(terra.Pol, null));
        int righe = 0;
        foreach (var (nome, isc) in OrganizzazioneDeiFile.Isc(radice))
        {
            int tolte = OrganizzazioneDeiFile.Togli(isc, terra.RigaGeo) + OrganizzazioneDeiFile.Togli(isc, terra.RigaPol);
            righe += tolte;
            if (OrganizzazioneDeiFile.AggiungiLaCartella(isc, cartella) || tolte > 0)
                file.Add(new FileDellaProva(nome, isc.Testo()));
        }

        perche = null;
        var domande = OrganizzazioneDeiFile.DomandeDelloScalo(Scalo);
        domande.Insert(1, new($"Le SID, le STAR, i parcheggi, le etichette delle taxiway e i punti VFR di {Scalo} ci sono ancora (ora da {cartella})?", $"{Scalo}: tasti SID/STAR, i parcheggi, i VFR"));
        var fatto = new List<string>
        {
            $"In {cartella}: {string.Join(", ", spostati)} (spostati da IT\\), {icao}.geo (era {terra.RigaGeo}), {icao}.tfl (era {terra.RigaPol}).",
            $"{cartella} aggiunta alla riga 6 di [INFO] di ogni .isc; tolte {righe} righe F; di {Scalo}.",
        };
        return new ProvaPreparata(file, domande, fatto);
    }
}

/// <summary>
/// La prova I7-B della carta «file per file» (§9): <b>una cartella per tipo</b>, <c>IT\GND\</c>, coi file di terra di
/// LIRF divisi come li prevede il manuale (<c>lirf.rwy</c> piste, <c>.txo</c> bordi taxiway, <c>.txc</c> centro,
/// <c>.stp</c> stop, <c>.apr</c> piazzali, <c>.pier</c>, <c>.bld</c> edifici, dalle righe del <c>.geo</c> secondo il loro
/// colore) e <c>lirf.tfl</c> per i riempimenti (il <c>.pol</c>). Caricati da sé (riga 6), le righe di LIRF escono dagli
/// <c>.isc</c>. Risponde anche alla O4: le marcature delle piste (colore RUNWAY) starebbero nel <c>.rwy</c>.
/// </summary>
public sealed class CartellaPerTipo : ProvaInAurora
{
    public const string Scalo = "LIRF";

    /// <summary>Il colore della riga del <c>.geo</c> → il file per tipo del manuale.</summary>
    private static readonly Dictionary<string, string> Tipi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RUNWAY"] = "rwy", ["TAXIWAY"] = "txo", ["TAXI_CENTER"] = "txc", ["STOPLINE"] = "stp", ["STOPBAR"] = "stp",
        ["APRON"] = "apr", ["APPRON"] = "apr", ["PARKING"] = "apr", ["PIER"] = "pier", ["BUILDING"] = "bld",
    };

    public override string Argomento => "file-di-scalo-cartella-per-tipo";

    public override string Titolo => "i file di terra in una cartella per tipo (I7-B, O4)";

    public override string Perche
        => "«File per file» I7, proposta B: la terra di LIRF in IT\\GND, un file per tipo (.rwy .txo .txc .stp .apr .pier .bld .tfl), caricati da sé; via le sue righe dagli .isc.";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string icao = Scalo.ToLowerInvariant();
        var terra = TerraDelloScalo.Trova(radice, Scalo);
        if (terra is null)
        {
            perche = $"Negli .isc non trovo il .geo e il .pol di {Scalo}: la prova si fa su uno scalo che li ha.";
            return null;
        }

        var geo = RigheDelFile.Leggi(Path.Combine(radice, terra.Geo.Replace('/', Path.DirectorySeparatorChar)));
        var perTipo = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var commenti = new List<string>();
        foreach (string riga in geo.Righe)
        {
            string pulita = riga.Trim();
            if (pulita.Length == 0 || pulita.StartsWith("//", StringComparison.Ordinal))
            {
                // Un commento va col prossimo dato, nel suo file: i nomi dei gruppi restano sopra le loro righe.
                if (pulita.Length > 0)
                    commenti.Add(riga);
                continue;
            }

            var campi = pulita.Split(';');
            string colore = campi.Length >= 5 ? campi[4].Trim() : "";
            if (!Tipi.TryGetValue(colore, out string? tipo))
            {
                perche = $"{terra.Geo}: la riga «{pulita}» ha il colore «{colore}», che non ha un file per tipo nel manuale.";
                return null;
            }

            var sue = perTipo.TryGetValue(tipo, out var l) ? l : perTipo[tipo] = [];
            sue.AddRange(commenti);
            commenti.Clear();
            sue.Add(riga);
        }

        string dove = "SectorFiles/Include/IT/GND/";
        var file = perTipo.Select(t => new FileDellaProva($"{dove}{icao}.{t.Key}", string.Join(geo.Acapo, t.Value) + geo.Acapo)).ToList();
        file.Add(new FileDellaProva($"{dove}{icao}.tfl", OrganizzazioneDeiFile.Leggi(radice, terra.Pol)));
        file.Add(new FileDellaProva(terra.Geo, null));
        file.Add(new FileDellaProva(terra.Pol, null));
        int righe = 0;
        foreach (var (nome, isc) in OrganizzazioneDeiFile.Isc(radice))
        {
            int tolte = OrganizzazioneDeiFile.Togli(isc, terra.RigaGeo) + OrganizzazioneDeiFile.Togli(isc, terra.RigaPol);
            righe += tolte;
            if (OrganizzazioneDeiFile.AggiungiLaCartella(isc, @"IT\GND") || tolte > 0)
                file.Add(new FileDellaProva(nome, isc.Testo()));
        }

        perche = null;
        var domande = OrganizzazioneDeiFile.DomandeDelloScalo(Scalo);
        domande.Insert(2, new("Le piste (RUNWAY) si vedono e si accendono come prima, dal file .rwy? (È dove andrebbero anche le marcature, O4.)", $"le piste di {Scalo}"));
        var fatto = new List<string>
        {
            $"IT\\GND: {string.Join(", ", perTipo.Select(t => $"{icao}.{t.Key} ({t.Value.Count(r => !r.TrimStart().StartsWith("//", StringComparison.Ordinal))})"))} dalle righe di {terra.RigaGeo}; {icao}.tfl da {terra.RigaPol}.",
            $"IT\\GND aggiunta alla riga 6 di [INFO] di ogni .isc; tolte {righe} righe F; di {Scalo}.",
        };
        return new ProvaPreparata(file, domande, fatto);
    }
}

/// <summary>
/// La prova K2 della carta «file per file» (§11): <b>un file per ACC anche per gli APP</b> — i settori bassi di un ACC
/// (<c>limm_es0_app.lartcc</c>, <c>limm_tma.lartcc</c>…) uniti in <c>limm.lartcc</c>, uno dopo l'altro, e negli <c>.isc</c>
/// una riga sola al posto delle loro. Gli ACC con un file solo non cambiano.
/// </summary>
public sealed class UnFilePerAcc : ProvaInAurora
{
    public override string Argomento => "spazi-bassi-un-file-per-acc";

    public override string Titolo => "gli spazi bassi degli APP in un file per ACC (K2)";

    public override string Perche
        => "«File per file» K2: limm.lartcc coi settori di Milano invece di un file per settore. Si vedono e si scelgono come prima?";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string cartella = Path.Combine(radice, "SectorFiles", "Include", "IT", "LOW_AIRSPACE");
        if (!Directory.Exists(cartella))
        {
            perche = "Nel sector non c'è LOW_AIRSPACE.";
            return null;
        }

        // Per ACC (le prime quattro lettere): i suoi file di settore, non le STAR (lirf_star, limc_star sono di scalo).
        var perAcc = Directory.EnumerateFiles(cartella, "*.lartcc").Select(Path.GetFileName).OfType<string>()
            .Where(n => n.Length > 5 && n[4] == '_' && !n.Contains("_star", StringComparison.OrdinalIgnoreCase))
            .GroupBy(n => n[..4].ToLowerInvariant()).Where(g => g.Count() > 1).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        if (perAcc.Count == 0)
        {
            perche = "In LOW_AIRSPACE nessun ACC ha più di un file di settore: non c'è niente da unire.";
            return null;
        }

        var file = new List<FileDellaProva>();
        var iscs = OrganizzazioneDeiFile.Isc(radice);
        var cambiati = new HashSet<string>(StringComparer.Ordinal);
        var fatto = new List<string>();
        foreach (var acc in perAcc)
        {
            var nomi = acc.Order(StringComparer.Ordinal).ToList();
            var interi = nomi.Select(n => OrganizzazioneDeiFile.Leggi(radice, "SectorFiles/Include/IT/LOW_AIRSPACE/" + n)).ToList();
            // L'a capo dal file com'è: tolto quello in fondo, un file di una riga non ne avrebbe più.
            string acapo = interi.Any(t => t.Contains("\r\n", StringComparison.Ordinal)) ? "\r\n" : "\n";
            var testi = interi.Select(t => t.TrimEnd('\r', '\n')).ToList();
            string unito = string.Join(acapo + acapo, testi.Select((t, i) => $"//{nomi[i]}{acapo}{t}")) + acapo;
            file.Add(new FileDellaProva($"SectorFiles/Include/IT/LOW_AIRSPACE/{acc.Key}.lartcc", unito));
            file.AddRange(nomi.Select(n => new FileDellaProva("SectorFiles/Include/IT/LOW_AIRSPACE/" + n, null)));
            var vecchi = nomi.Select(n => @"LOW_AIRSPACE\" + n).ToList();
            foreach (var (nome, isc) in iscs)
            {
                if (OrganizzazioneDeiFile.Sostituisci(isc, vecchi, $@"LOW_AIRSPACE\{acc.Key}.lartcc"))
                    cambiati.Add(nome);
            }

            fatto.Add($"LOW_AIRSPACE\\{acc.Key}.lartcc: {string.Join(", ", nomi)}, uno dopo l'altro col nome del file sopra.");
        }

        file.AddRange(cambiati.Order(StringComparer.Ordinal).Select(n => new FileDellaProva(n, iscs[n].Testo())));
        perche = null;
        string esempi = string.Join(", ", perAcc.Select(a => a.Key.ToUpperInvariant()));
        var domande = new List<DomandaDellaProva>
        {
            new($"Gli spazi aerei bassi di {esempi} si vedono come prima?", "il tasto degli spazi bassi (ARTCC LOW), per esempio MM CONF 1"),
            new("Nella loro selezione ci sono le stesse voci, e si accendono una per una come prima?", "la selezione degli spazi bassi (Shift+clic sul tasto)"),
            new("Il sector si carica senza errori nuovi?", "i messaggi di Aurora al caricamento del sector (SectorError.log)"),
        };
        fatto.Add($"Negli .isc ({string.Join(", ", cambiati.Select(c => c[(c.LastIndexOf('/') + 1)..]))}) una riga per ACC al posto di quelle dei suoi file.");
        return new ProvaPreparata(file, domande, fatto);
    }
}

/// <summary>I file di terra di uno scalo come li dichiarano oggi gli <c>.isc</c>: il <c>.geo</c> e il <c>.pol</c>.</summary>
internal sealed record TerraDelloScalo(string Geo, string RigaGeo, string Pol, string RigaPol)
{
    /// <summary>
    /// Il <c>GEO\icao.geo</c> e il <c>GND_LAYOUT\xx_ad_gnd.pol</c> (xx = le ultime due lettere) dello scalo, se ci sono
    /// e gli <c>.isc</c> li dichiarano.
    /// </summary>
    public static TerraDelloScalo? Trova(string radice, string scalo)
    {
        string icao = scalo.ToLowerInvariant();
        string rigaGeo = $@"GEO\{icao}.geo", rigaPol = $@"GND_LAYOUT\{icao[2..]}_ad_gnd.pol";
        string geo = "SectorFiles/Include/IT/" + rigaGeo.Replace('\\', '/'), pol = "SectorFiles/Include/IT/" + rigaPol.Replace('\\', '/');
        bool dichiarati = OrganizzazioneDeiFile.Isc(radice).Values.Any(i => i.Righe.Any(r => r.Trim().Equals("F;" + rigaGeo, StringComparison.OrdinalIgnoreCase)));
        return dichiarati && File.Exists(Path.Combine(radice, geo.Replace('/', Path.DirectorySeparatorChar))) && File.Exists(Path.Combine(radice, pol.Replace('/', Path.DirectorySeparatorChar)))
            ? new TerraDelloScalo(geo, rigaGeo, pol, rigaPol)
            : null;
    }
}
