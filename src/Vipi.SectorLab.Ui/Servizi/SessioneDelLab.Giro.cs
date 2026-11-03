using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.SectorLab.Ui.Servizi;

public sealed partial class SessioneDelLab
{
    // --- i giri di prova (carte «Il kit di prova per gli AOD» e «I giri di prova dal fork», 3 novembre 2026) ------------

    private const string FileDelGiro = "giro-di-prova.json";

    /// <summary>P2: la copia dei giri letti dal fork, nei dati del Lab, per quando il server non risponde.</summary>
    private const string CartellaDeiGiri = "giri";

    /// <summary>
    /// K2: la cartella di lavoro che il primo avvio propone (<c>--lavoro-proposto</c>, il <c>.cmd</c> del kit passa la
    /// cartella <c>lavoro</c> accanto allo zip). Null: il campo nasce vuoto, com'era.
    /// </summary>
    public string? LavoroProposto { get; set; }

    private bool _giroLetto;
    private bool _giroAcceso;
    private string _giroScelto = GiroDiProva.IdDeiPrimiPassi;
    private IReadOnlyList<Giro> _giri = [GiroDiProva.PrimiPassi];
    private readonly Dictionary<string, StatoDiUnGiro> _statiDeiGiri = new(StringComparer.Ordinal);

    /// <summary>Gli esiti di un giro, la versione che l'AOD ha visto (P4, P5) e quando li ha mandati l'ultima volta (P7).</summary>
    private sealed class StatoDiUnGiro
    {
        public int Versione { get; set; }

        public DateTime? MandatoIl { get; set; }

        public Dictionary<string, EsitoDelGiro> Esiti { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>K3: il Giro di prova c'è (acceso dal kit con <c>--giro-di-prova</c>, poi ricordato nei dati).</summary>
    public bool GiroAcceso
    {
        get
        {
            LeggiIlGiro();
            return _giroAcceso;
        }
    }

    /// <summary>K5: il pannello del giro è aperto sopra la pagina.</summary>
    public bool GiroAperto { get; private set; }

    /// <summary>P2, P8: i giri che ci sono, nell'ordine dei file del fork; «Primi passi» dentro il Lab se il fork non ne ha.</summary>
    public IReadOnlyList<Giro> Giri
    {
        get
        {
            LeggiIlGiro();
            return _giri;
        }
    }

    /// <summary>Il giro aperto nel pannello.</summary>
    public Giro GiroScelto => Giri.FirstOrDefault(g => string.Equals(g.Id, _giroScelto, StringComparison.Ordinal)) ?? Giri[0];

    /// <summary>L'indice del compito che il pannello mostra, nel giro scelto.</summary>
    public int CompitoAperto { get; private set; }

    /// <summary>Gli esiti dati, per compito, del giro scelto.</summary>
    public IReadOnlyDictionary<string, EsitoDelGiro> EsitiDelGiro => EsitiDi(GiroScelto);

    /// <summary>Gli esiti dati ai compiti di un giro.</summary>
    public IReadOnlyDictionary<string, EsitoDelGiro> EsitiDi(Giro giro)
    {
        ArgumentNullException.ThrowIfNull(giro);
        LeggiIlGiro();
        return _statiDeiGiri.TryGetValue(giro.Id, out var stato) ? stato.Esiti : new Dictionary<string, EsitoDelGiro>();
    }

    /// <summary>Quanti compiti del giro scelto hanno la loro risposta.</summary>
    public int CompitiFatti => FattiDi(GiroScelto);

    /// <summary>Quanti compiti di un giro hanno la loro risposta (un compito tolto dal giro non conta più: P5).</summary>
    public int FattiDi(Giro giro)
    {
        var esiti = EsitiDi(giro);
        return giro.Compiti.Count(c => GiroDiProva.Fatto(c, esiti.GetValueOrDefault(c.Id)));
    }

    /// <summary>P4: un giro mai aperto, o con una versione più alta di quella che l'AOD ha visto.</summary>
    public bool GiroNuovo(Giro giro)
    {
        ArgumentNullException.ThrowIfNull(giro);
        LeggiIlGiro();
        return !_statiDeiGiri.TryGetValue(giro.Id, out var stato) || stato.Versione < giro.Versione;
    }

    /// <summary>Quanti giri sono nuovi (il tasto della barra lo dice).</summary>
    public int GiriNuovi => Giri.Count(GiroNuovo);

    /// <summary>P3: i file del fork che non si leggono, col perché («03-settori.json: manca «titolo»»).</summary>
    public IReadOnlyList<string> GiriNonLetti { get; private set; } = [];

    /// <summary>Sta chiedendo i giri al fork.</summary>
    public bool StaCercandoIGiri { get; private set; }

    /// <summary>Perché l'ultima ricerca dei giri sul fork non è andata (il server che non risponde, il ramo), o null.</summary>
    public string? PercheDeiGiri { get; private set; }

    /// <summary>Perché «Manda gli esiti» non è partito, o null.</summary>
    public string? PercheNonMandaGliEsiti { get; private set; }

    /// <summary>L'ultima pagina della issue aperta, per riaprirla; e se il testo era tagliato (K8).</summary>
    public Uri? PaginaDegliEsiti { get; private set; }

    /// <summary>K8: il testo intero non stava nel link: va trascinato nella issue il file degli esiti.</summary>
    public bool EsitiTagliati { get; private set; }

    /// <summary>P7: il giro scelto ha esiti dati dopo l'ultimo «Manda gli esiti» (o mai mandati).</summary>
    public bool EsitiNonMandati
    {
        get
        {
            LeggiIlGiro();
            return _statiDeiGiri.TryGetValue(GiroScelto.Id, out var stato) && stato.Esiti.Count > 0
                   && (stato.MandatoIl is not { } mandato || stato.Esiti.Values.Any(e => e.Quando > mandato));
        }
    }

    /// <summary>Il file con gli esiti interi, nei dati del Lab.</summary>
    public string FileDegliEsitiDelGiro => Path.Combine(_cartellaDeiDati, GiroDiProva.FileDegliEsiti);

    /// <summary>K3: accende il giro (all'avvio, dal kit). Già acceso, non cambia niente.</summary>
    public void AccendiIlGiro()
    {
        LeggiIlGiro();
        if (_giroAcceso)
            return;
        _giroAcceso = true;
        GiroAperto = true;
        Visto(GiroScelto);
        CompitoAperto = PrimoDaFare(GiroScelto);
        ScriviIlGiro();
        Registro.Scrivi("giro", "acceso");
        Avvisa();
    }

    /// <summary>Il tasto della barra: apre o chiude il pannello.</summary>
    public void ApriOChiudiIlGiro()
    {
        GiroAperto = !GiroAperto;
        Avvisa();
    }

    /// <summary>P4: apre un altro giro nel pannello, sul suo primo compito da fare; da adesso non è più «nuovo».</summary>
    public void ScegliIlGiro(string id)
    {
        if (Giri.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.Ordinal)) is not { } giro)
            return;
        _giroScelto = giro.Id;
        Visto(giro);
        CompitoAperto = PrimoDaFare(giro);
        PercheNonMandaGliEsiti = null;
        PaginaDegliEsiti = null;
        ScriviIlGiro();
        Registro.Scrivi("giro", $"scelto {giro.Id} v{giro.Versione}");
        Avvisa();
    }

    /// <summary>Il pannello mostra il compito <paramref name="indice"/> (i pallini, ‹ ›).</summary>
    public void VaiAlCompito(int indice)
    {
        CompitoAperto = Math.Clamp(indice, 0, GiroScelto.Compiti.Count - 1);
        Avvisa();
    }

    /// <summary>«Vai a …»: la pagina del compito.</summary>
    public void VaiAllaPaginaDelCompito(CompitoDelGiro compito)
    {
        ArgumentNullException.ThrowIfNull(compito);
        if (compito.Pagina is { } pagina)
            ScegliLaPagina(pagina);
    }

    /// <summary>
    /// K5, K7: l'esito di un compito del giro scelto, con la nota; si scrive subito nei dati, e il pannello passa al primo
    /// compito dopo che non ha ancora il suo (se sono fatti tutti, resta lì).
    /// </summary>
    public void DaiLEsito(string compito, EsitoDelCompito esito, string? nota)
    {
        var giro = GiroScelto;
        ScriviLEsito(giro, compito, esito, nota);
        var esiti = EsitiDi(giro);
        int qui = IndiceDel(giro, compito);
        int quanti = giro.Compiti.Count;
        CompitoAperto = qui;
        for (int passo = 1; passo < quanti; passo++)
        {
            var dopo = giro.Compiti[(qui + passo) % quanti];
            if (!GiroDiProva.Fatto(dopo, esiti.GetValueOrDefault(dopo.Id)))
            {
                CompitoAperto = (qui + passo) % quanti;
                break;
            }
        }

        Avvisa();
    }

    /// <summary>La nota di un compito, senza cambiarne l'esito (o la risposta di un compito che è solo una domanda).</summary>
    public void ScriviLaNota(string compito, string? nota)
    {
        ScriviLEsito(GiroScelto, compito, EsitiDelGiro.GetValueOrDefault(compito)?.Esito, nota);
        Avvisa();
    }

    /// <summary>P7: «Ricomincia questo giro»: i suoi esiti si svuotano (quelli mandati restano nella issue).</summary>
    public void RicominciaIlGiro()
    {
        var giro = GiroScelto;
        LeggiIlGiro();
        if (_statiDeiGiri.TryGetValue(giro.Id, out var stato))
        {
            Registro.Scrivi("giro", $"ricomincia {giro.Id}: via {stato.Esiti.Count} esiti{(EsitiNonMandati ? " non mandati" : "")}");
            stato.Esiti.Clear();
            stato.MandatoIl = null;
        }

        CompitoAperto = 0;
        PaginaDegliEsiti = null;
        ScriviIlGiro();
        Avvisa();
    }

    /// <summary>
    /// K8, P6: «Manda gli esiti» del giro scelto. Scrive il testo intero in <see cref="FileDegliEsitiDelGiro"/> e apre nel
    /// browser la pagina di GitHub «nuova issue» sul repo del sector, già compilata. Falso, col perché in
    /// <see cref="PercheNonMandaGliEsiti"/>: senza VID, o se il repo del sector non è GitHub.
    /// </summary>
    public bool MandaGliEsiti(DateTime? adesso = null)
    {
        PercheNonMandaGliEsiti = null;
        if (Vid is not { } vid)
            return NonMandaGliEsiti("Prima scrivi il tuo VID, nelle Impostazioni (il primo compito di «Primi passi»): la issue dice chi l'ha mandata.");
        if (Core.Git.Invio.RepoGitHub(IndirizzoDelSector) is not { } repo)
            return NonMandaGliEsiti($"Il repo del sector ({IndirizzoDelSector}) non è su GitHub: gli esiti stanno in {FileDegliEsitiDelGiro}.");

        var giro = GiroScelto;
        DateTime quando = adesso ?? DateTime.Now;
        var (titolo, testo) = GiroDiProva.Issue(giro, vid, VersioneDelLab, Environment.OSVersion.VersionString, quando,
            EsitiDi(giro), GiroDiProva.ErroriDelRegistro(Registro.Cartella));
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(FileDegliEsitiDelGiro, $"# {titolo}\n\n{testo}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("giro", e);
        }

        var (pagina, tagliato) = GiroDiProva.PaginaDellaIssue(repo, titolo, testo, FileDegliEsitiDelGiro);
        PaginaDegliEsiti = pagina;
        EsitiTagliati = tagliato;
        StatoDi(giro).MandatoIl = DateTime.Now > quando ? DateTime.Now : quando;
        ScriviIlGiro();
        Registro.Scrivi("giro", $"manda gli esiti di {giro.Id} a {repo}: {FattiDi(giro)} compiti su {giro.Compiti.Count}{(tagliato ? ", testo tagliato" : "")}");
        ApriNelBrowser(pagina);
        Avvisa();
        return true;
    }

    /// <summary>
    /// P2: chiede i giri al fork, dal ramo predefinito del sector aperto (<c>origin/master:giri/</c>), e ne tiene una copia
    /// nei dati. Con <paramref name="prendi"/> prima il <c>fetch</c>. Senza sector in un clone, o col server muto, restano
    /// i giri di prima (la copia, o «Primi passi» dentro il Lab), e <see cref="PercheDeiGiri"/> dice perché.
    /// </summary>
    public async Task CercaIGiriAsync(bool prendi = true)
    {
        // Una ricerca già in corso (quella dell'apertura) si aspetta: chi chiede vede i giri che ha portato.
        if (_ricercaDeiGiri is { } inCorso)
        {
            await inCorso.ConfigureAwait(false);
            return;
        }

        var ricerca = CercaAsync(prendi);
        _ricercaDeiGiri = ricerca;
        try
        {
            await ricerca.ConfigureAwait(false);
        }
        finally
        {
            if (_ricercaDeiGiri == ricerca)
                _ricercaDeiGiri = null;
        }
    }

    private Task? _ricercaDeiGiri;

    private async Task CercaAsync(bool prendi)
    {
        // Il ramo predefinito lo sa la lettura del ramo: quella dell'apertura si aspetta.
        while (_letturaDelRamo is { } inCorso)
            await inCorso.ConfigureAwait(false);
        if (Ramo?.Radice is not { } radice)
            return;

        StaCercandoIGiri = true;
        Avvisa();
        try
        {
            var (letti, perche) = await Core.Git.GiriDalServer.LeggiAsync(Git, radice, Predefinito, prendi).ConfigureAwait(false);
            PercheDeiGiri = perche;
            // Un ramo che non si legge (o il server muto senza niente di letto) non cancella la copia: si tiene quella.
            if (perche is null || letti.Count > 0)
            {
                CopiaIGiri(letti);
                UsaIGiri(letti);
            }

            Registro.Scrivi("giro", $"giri dal fork ({Predefinito}): {letti.Count} file{(perche is null ? "" : " — " + perche)}");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            PercheDeiGiri = "git non ha risposto: " + e.Message;
            Registro.Errore("giro", e);
        }
        finally
        {
            StaCercandoIGiri = false;
            Avvisa();
        }
    }

    /// <summary>
    /// P3, P8: i giri da questi file (quelli del fork, o la loro copia): quelli che si leggono, nell'ordine dei file, con
    /// «Primi passi» dentro il Lab in testa se il fork non ha un suo <c>primi-passi</c>. Un id già visto si salta.
    /// </summary>
    internal void UsaIGiri(IReadOnlyList<Core.Git.GiriDalServer.FileDelGiro> letti)
    {
        // Prima il file e la copia (una volta sola): letti dopo, coprirebbero questi giri con quelli di prima.
        LeggiIlGiro();
        var giri = new List<Giro>();
        var nonLetti = new List<string>();
        foreach (var file in letti)
        {
            if (GiroDiProva.Leggi(file.Nome, file.Testo, out string? perche) is not { } giro)
                nonLetti.Add($"{file.Nome}: {perche}");
            else if (giri.Any(g => string.Equals(g.Id, giro.Id, StringComparison.Ordinal)))
                nonLetti.Add($"{file.Nome}: l'id «{giro.Id}» c'è già in un altro file");
            else
                giri.Add(giro);
        }

        if (!giri.Any(g => string.Equals(g.Id, GiroDiProva.IdDeiPrimiPassi, StringComparison.Ordinal)))
            giri.Insert(0, GiroDiProva.PrimiPassi);
        _giri = giri;
        GiriNonLetti = nonLetti;
        if (!giri.Any(g => string.Equals(g.Id, _giroScelto, StringComparison.Ordinal)))
            _giroScelto = giri[0].Id;
        CompitoAperto = Math.Clamp(CompitoAperto, 0, GiroScelto.Compiti.Count - 1);
    }

    /// <summary>Apre la cartella dei dati del Lab, dove sta il file degli esiti (per trascinarlo nella issue).</summary>
    public void ApriLaCartellaDegliEsiti()
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{FileDegliEsitiDelGiro}\"")
                { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Registro.Errore("giro", e);
        }
    }

    /// <summary>La versione del Lab con l'impronta corta del commit («0.9.0+42153ee8»): dice quale eseguibile ha provato.</summary>
    public static string VersioneDelLab { get; } = Accorcia(Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?");

    private static string Accorcia(string versione) =>
        versione.IndexOf('+', StringComparison.Ordinal) is var piu and >= 0 && versione.Length > piu + 9
            ? versione[..(piu + 9)]
            : versione;

    private bool NonMandaGliEsiti(string perche)
    {
        PercheNonMandaGliEsiti = perche;
        Registro.Scrivi("giro", "esiti non mandati: " + perche);
        Avvisa();
        return false;
    }

    private StatoDiUnGiro StatoDi(Giro giro)
    {
        if (!_statiDeiGiri.TryGetValue(giro.Id, out var stato))
            _statiDeiGiri[giro.Id] = stato = new StatoDiUnGiro { Versione = giro.Versione };
        return stato;
    }

    private void Visto(Giro giro)
    {
        var stato = StatoDi(giro);
        stato.Versione = Math.Max(stato.Versione, giro.Versione);
    }

    private void ScriviLEsito(Giro giro, string compito, EsitoDelCompito? esito, string? nota)
    {
        if (IndiceDel(giro, compito) < 0)
            throw new ArgumentException($"Il compito «{compito}» non c'è nel giro «{giro.Id}».", nameof(compito));
        LeggiIlGiro();
        StatoDi(giro).Esiti[compito] = new EsitoDelGiro(compito, esito, string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(), DateTime.Now);
        ScriviIlGiro();
        Registro.Scrivi("giro", $"{giro.Id} {compito}: {esito?.ToString() ?? "nota"}{(string.IsNullOrWhiteSpace(nota) ? "" : " · " + nota.Trim())}");
    }

    private static int IndiceDel(Giro giro, string compito)
    {
        for (int i = 0; i < giro.Compiti.Count; i++)
        {
            if (string.Equals(giro.Compiti[i].Id, compito, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private int PrimoDaFare(Giro giro)
    {
        var esiti = EsitiDi(giro);
        for (int i = 0; i < giro.Compiti.Count; i++)
        {
            if (!GiroDiProva.Fatto(giro.Compiti[i], esiti.GetValueOrDefault(giro.Compiti[i].Id)))
                return i;
        }

        return 0;
    }

    /// <summary>
    /// Il file del giro nei dati, letto una volta (K7), e la copia dei giri del fork (P2). Acceso, il pannello nasce aperto
    /// sul primo compito da fare del giro aperto l'ultima volta. Il file di prima della carta dei giri (un giro solo, gli
    /// esiti in cima) diventa «Primi passi» (P5).
    /// </summary>
    private void LeggiIlGiro()
    {
        if (_giroLetto)
            return;
        _giroLetto = true;
        UsaIGiri(CopiaDeiGiri());
        if (Ricordata(FileDelGiro) is { } scritto)
        {
            try
            {
                if (JsonSerializer.Deserialize<FileDelGiroScritto>(scritto, OpzioniDelGiro) is { } letto)
                {
                    _giroAcceso = letto.Acceso;
                    foreach (var (id, suo) in letto.Giri ?? [])
                    {
                        var stato = new StatoDiUnGiro { Versione = suo.Versione, MandatoIl = suo.MandatoIl };
                        foreach (var esito in suo.Esiti ?? [])
                            stato.Esiti[esito.Compito] = esito;
                        _statiDeiGiri[id] = stato;
                    }

                    if (letto.Esiti is { Count: > 0 } diPrima && !_statiDeiGiri.ContainsKey(GiroDiProva.IdDeiPrimiPassi))
                    {
                        var primi = new StatoDiUnGiro { Versione = 1 };
                        foreach (var esito in diPrima)
                            primi.Esiti[esito.Compito] = esito;
                        _statiDeiGiri[GiroDiProva.IdDeiPrimiPassi] = primi;
                    }

                    if (letto.Aperto is { } aperto && _giri.Any(g => string.Equals(g.Id, aperto, StringComparison.Ordinal)))
                        _giroScelto = aperto;
                }
            }
            catch (JsonException e)
            {
                // Un file rovinato: il giro riparte, e il registro lo dice.
                Registro.Errore("giro", e);
            }
        }

        GiroAperto = _giroAcceso;
        CompitoAperto = PrimoDaFare(GiroScelto);
    }

    private IReadOnlyList<Core.Git.GiriDalServer.FileDelGiro> CopiaDeiGiri()
    {
        string cartella = Path.Combine(_cartellaDeiDati, CartellaDeiGiri);
        try
        {
            return Directory.Exists(cartella)
                ? [.. Directory.EnumerateFiles(cartella, "*.json").Order(StringComparer.Ordinal)
                    .Select(f => new Core.Git.GiriDalServer.FileDelGiro(Path.GetFileName(f), File.ReadAllText(f)))]
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("giro", e);
            return [];
        }
    }

    /// <summary>P2: la copia nei dati diventa uguale ai giri del fork (un file tolto dal fork si toglie anche qui).</summary>
    private void CopiaIGiri(IReadOnlyList<Core.Git.GiriDalServer.FileDelGiro> letti)
    {
        string cartella = Path.Combine(_cartellaDeiDati, CartellaDeiGiri);
        try
        {
            Directory.CreateDirectory(cartella);
            foreach (string vecchio in Directory.EnumerateFiles(cartella, "*.json"))
            {
                if (!letti.Any(l => string.Equals(l.Nome, Path.GetFileName(vecchio), StringComparison.OrdinalIgnoreCase)))
                    File.Delete(vecchio);
            }

            foreach (var file in letti)
                File.WriteAllText(Path.Combine(cartella, file.Nome), file.Testo);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("giro", e);
        }
    }

    // Non con Ricorda: scriverebbe nel registro il file intero a ogni esito (lo dice già ScriviLEsito, in una riga).
    private void ScriviIlGiro()
    {
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            var giri = _statiDeiGiri.ToDictionary(c => c.Key, c => new StatoScritto(c.Value.Versione, c.Value.MandatoIl, [.. c.Value.Esiti.Values]),
                StringComparer.Ordinal);
            File.WriteAllText(Path.Combine(_cartellaDeiDati, FileDelGiro),
                JsonSerializer.Serialize(new FileDelGiroScritto(_giroAcceso, _giroScelto, giri, null), OpzioniDelGiro));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Vale per questa sessione anche se il disco non lo ricorda; il registro lo dice.
            Registro.Errore("giro", e);
        }
    }

    // Gli esiti col nome («NonHoCapito»), non col numero: il file si legge a occhio, e regge un esito aggiunto in mezzo.
    // Niente null scritti: il file del giro di prima (con «Esiti» in cima) si riconosce perché li ha.
    private static readonly JsonSerializerOptions OpzioniDelGiro = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Il file del giro: acceso, il giro aperto, gli stati per giro; <c>Esiti</c> c'è solo nel file di prima (P5).</summary>
    private sealed record FileDelGiroScritto(bool Acceso, string? Aperto, Dictionary<string, StatoScritto>? Giri, List<EsitoDelGiro>? Esiti);

    private sealed record StatoScritto(int Versione, DateTime? MandatoIl, List<EsitoDelGiro>? Esiti);
}
