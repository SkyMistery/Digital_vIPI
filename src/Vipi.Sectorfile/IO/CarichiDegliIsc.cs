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

    /// <summary>
    /// I citati che non stanno dove dice il <c>F;</c> ma che Aurora trova per nome, perché sotto <c>Include</c> c'è un
    /// file solo con quel nome (<c>DYNAMIC_SEC\GCI.tfl</c> sta in <c>OTHER\</c>: carta «file per file» §C, M6). Stanno
    /// fra i <see cref="Caricati"/>. Lotto «Subito» slice 2b.
    /// </summary>
    public IReadOnlyList<(int Riga, string Testo, string Citato, string Trovato)> TrovatiPerNome { get; init; } = [];

    /// <summary>Lo stesso file citato due volte con <c>F;</c> (<c>lirrctr.tfl</c> in <c>ITALY.isc</c>, D7): dove, e la riga della prima.</summary>
    public IReadOnlyList<(int Riga, string Testo, string Percorso, int PrimaRiga)> Doppi { get; init; } = [];

    /// <summary>
    /// I file citati sotto una sezione che non ha la loro forma (F6): un <c>.fix</c> sotto <c>[GEO]</c>, i punti VFR
    /// dei <c>.vfi</c> di <c>ENRVFI</c> sotto <c>[VFRENR]</c>, che vuole le rotte. Con la sezione giusta, se c'è.
    /// </summary>
    public IReadOnlyList<(int Riga, string Testo, string Percorso, string Sezione, string Perche)> FuoriSezione { get; init; } = [];
}

/// <summary>Le tre vie per cui un file entra in un <c>.isc</c>: <c>F;</c>, il codice di uno scalo, un <c>.frq</c>.</summary>
public static class CarichiDegliIsc
{
    // Aurora carica da sé, senza F;, i file che portano il codice di uno scalo di [AIRPORT] (SPECIFICA_FORMATI §3 di B).
    // Il manuale IVAO elenca gts, txi, sid, str, vfi, vrt, mva, tfl: NON geo e pol (carta «file per file» §19, §21 —
    // per questo `limw.pol` è orfano). Gli .atis restano: la carta non ne dice niente (lotto «Subito» slice 2b).
    private static readonly string[] EstensioniPerIcao =
        ["gts", "txi", "sid", "str", "vfi", "vrt", "mva", "tfl", "atis"];

    // Che forma vuole ogni sezione degli .isc (manuale IVAO; carta «file per file» §1-§22): le estensioni ammesse. Una
    // sezione che non c'è qui non si controlla. [VFRENR] vuole .vfi, ma con la forma delle rotte (Numero;Lat;Lon;…).
    private static readonly Dictionary<string, string[]> FormeDelleSezioni = new(StringComparer.Ordinal)
    {
        ["AIRPORT"] = ["ap"], ["RUNWAY"] = ["rw"], ["FIXES"] = ["fix"], ["NDB"] = ["ndb"], ["VOR"] = ["vor"],
        ["ARTCC"] = ["artcc"], ["ARTCC HIGH"] = ["hartcc"], ["ARTCC LOW"] = ["lartcc"],
        ["LOW AIRWAY"] = ["lairway"], ["HIGH AIRWAY"] = ["hairway"],
        ["GEO"] = ["geo", "danger", "prohibit", "restrict"], ["FILLCOLOR"] = ["tfl", "pol"],
        ["MVA"] = ["mva"], ["MVAENR"] = ["mva"], ["VFRFIX"] = ["vfi"], ["VFRENR"] = ["vfi"],
        ["VFRROUTE"] = ["vrt"], ["VFRRTEENR"] = ["vrt"], ["SID"] = ["sid"], ["STAR"] = ["str"],
        ["ATC"] = ["frq"], ["ATIS"] = ["atis"], ["ATISFIELD"] = ["fds"], ["COLORSCHEME"] = ["clr"],
        ["CPDLC"] = ["cpdlc"], ["CPDLCNAMES"] = ["cpdlcnames"], ["DEFINE"] = ["def"], ["SYMBOLS"] = ["sym"],
        ["HOLDENR"] = ["hold"],
    };

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

        // Aurora trova un file anche per nome, se il percorso è sbagliato (committente, «file per file» §C): vale quando
        // sotto Include c'è UN file solo con quel nome.
        string? PerNome(string citato)
        {
            string nome = Path.GetFileName(Chiave(citato));
            var omonimi = indice.Where(f => Path.GetFileName(f.Key) == nome).Take(2).ToList();
            return omonimi.Count == 1 ? omonimi[0].Value : null;
        }

        string nomeMaster = Path.GetFileName(master);
        var mancanti = new List<(string File, int Riga, string Testo, string Citato)>();
        var perNome = new List<(int Riga, string Testo, string Citato, string Trovato)>();
        var doppi = new List<(int Riga, string Testo, string Percorso, int PrimaRiga)>();
        var fuori = new List<(int Riga, string Testo, string Percorso, string Sezione, string Perche)>();
        var citati = new List<(string Sezione, string Percorso)>();
        var primaVolta = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (sez, citato, riga, testo) in daRisolvere)
        {
            string? trovato = Risolvi(citato);
            if (trovato is null && PerNome(citato) is { } omonimo)
            {
                trovato = omonimo;
                perNome.Add((riga, testo, citato, omonimo));
            }

            if (trovato is null)
            {
                mancanti.Add((nomeMaster, riga, testo, citato));
                continue;
            }

            citati.Add((sez, trovato));
            if (!primaVolta.TryAdd(trovato, riga))
            {
                doppi.Add((riga, testo, trovato, primaVolta[trovato]));
            }

            if (FuoriDallaSezione(sez, trovato) is { } perche)
            {
                fuori.Add((riga, testo, trovato, sez, perche));
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

        return new CaricoDiUnIsc(master, cartellaDati, caricati, mancanti)
        {
            TrovatiPerNome = perNome,
            Doppi = doppi,
            FuoriSezione = fuori,
        };
    }

    // Perché il file non ha la forma della sua sezione, o null se ce l'ha (o la sezione non si controlla).
    private static string? FuoriDallaSezione(string sezione, string percorso)
    {
        if (!FormeDelleSezioni.TryGetValue(sezione, out var ammesse))
        {
            return null;
        }

        string estensione = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
        if (!ammesse.Contains(estensione))
        {
            var giuste = FormeDelleSezioni.Where(s => s.Value.Contains(estensione)).Select(s => $"[{s.Key}]").ToList();
            return $"un .{estensione} sotto [{sezione}], che vuole " + string.Join(" o ", ammesse.Select(e => "." + e))
                + (giuste.Count > 0 ? $": il suo posto è {string.Join(" o ", giuste)}" : string.Empty);
        }

        // [VFRENR] vuole le rotte (Numero;Lat;Lon;[Gruppo];[Militare]); i punti VFR (Nome;Codice;Lat;Lon;Tipo) vanno in
        // [VFRFIX]. Si guarda la prima riga di dati: il primo campo è il numero della rotta?
        if (sezione == "VFRENR"
            && SectorFileReader.Read(percorso).Lines.Select(r => r.Trim())
                .FirstOrDefault(r => r.Length > 0 && !r.StartsWith("//", StringComparison.Ordinal)) is { } prima
            && !prima.Split(';')[0].Trim().All(char.IsAsciiDigit))
        {
            return "ha la forma dei punti VFR (Nome;Codice;Lat;Lon;Tipo), che vanno in [VFRFIX]: [VFRENR] vuole le rotte " +
                   "(Numero;Lat;Lon;…) — carta «file per file» F5, da provare in Aurora";
        }

        return null;
    }

    private static string Chiave(string percorso) => percorso.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
