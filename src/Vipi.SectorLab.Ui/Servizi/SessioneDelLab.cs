using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Disco;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Problemi;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>I gesti sui vertici di una forma (slice 7; «inverti» dal lotto «Subito», slice 5a).</summary>
public enum GestoDeiVertici
{
    Cambia,
    Aggiungi,
    Togli,
    Incolla,
    Inverti,
}

/// <summary>L'anteprima di «incolla da testo»: per quale elenco, con che densità, e che cosa ne esce.</summary>
public sealed record AnteprimaDiIncolla(string File, int Record, string Campo, double PuntiPerGrado, TestoDaIncollare Letto);

/// <summary>Dov'è la sessione: chiusa, in apertura, aperta, o fallita con un motivo.</summary>
public enum StatoDelLab
{
    Chiusa,
    InApertura,
    Aperta,
    Errore,
}

/// <summary>
/// Le pagine del Lab (riordino delle pagine, D1): una per modo di lavorare. Sector e AIP hanno la mappa; Consegna e
/// Impostazioni no.
/// </summary>
public enum PaginaDelLab
{
    Sector,
    /// <summary>Il mondo AIP: l'Indice come nell'AIP, con la mappa e la scheda della voce (D9).</summary>
    AipIndice,
    /// <summary>Il mondo AIP: PDF, lettura, git dell'AIP espanso e, fino alla R4, i confronti.</summary>
    Aip,
    /// <summary>Lo spazio Confronti (R6): le sezioni, uguale / diversa / manca, i due lati sulla stessa mappa.</summary>
    Confronti,
    Consegna,
    Impostazioni,
    /// <summary>La pagina Testo dello spazio Sector (D12, D21): l'albero dei file e l'editor.</summary>
    Testo,
}

/// <summary>
/// La cartella aperta, per tutta l'app (carta F3 §2.2). È un <b>singleton</b>, non uno scoped: la sessione è
/// dell'applicazione, non della pagina — l'albero costa 0,35-0,48 s e 102 MB, e un Ctrl+F5 non deve rileggerlo.
/// La finestra è una sola e apre una cartella sola (istanza unica, §3).
/// <para>Le pagine si iscrivono a <see cref="Cambiata"/>; chi la scatena è sempre un gesto dell'AOD, o la fine di un
/// lavoro che un gesto ha fatto partire (la validazione, il controllo delle modifiche: slice 10), mai un timer.
/// ⚠️ Quindi può arrivare da un filo che non è quello del circuito: i componenti ridisegnano con InvokeAsync.</para>
/// </summary>
public sealed class SessioneDelLab
{
    private readonly string _cartellaDeiDati;

    public SessioneDelLab(string? cartellaDeiDati = null, Registro? registro = null)
    {
        _cartellaDeiDati = cartellaDeiDati ?? CartellaDeiDatiDiBase;
        Registro = registro ?? new Registro(_cartellaDeiDati);
        (_schemaRicordato, ColoriDiAurora) = ColoriRicordati();
    }

    /// <summary><c>%LOCALAPPDATA%\VipiSectorLab</c>: l'ultima cartella, i backup, il registro.</summary>
    public static string CartellaDeiDatiDiBase { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VipiSectorLab");

    /// <summary>La cartella dell'autoprova, nella cartella temporanea: la prova d'avvio non ricorda niente nei dati di chi
    /// usa il Lab (prima la copia provata diventava «ultima cartella» e andava in cima ai recenti, 20 ottobre).</summary>
    public static string CartellaDellAutoprova { get; } = Path.Combine(Path.GetTempPath(), "VipiSectorLab-autoprova");

    /// <summary>
    /// Dove il Lab tiene i suoi dati per questa apertura: quella scelta con <c>--dati &lt;cartella&gt;</c> (un primo avvio
    /// simulato, un secondo Lab che non tocca il primo); se no quella dell'autoprova; se no <see cref="CartellaDeiDatiDiBase"/>.
    /// </summary>
    public static string CartellaDeiDati(string? scelta, bool autoprova) =>
        !string.IsNullOrWhiteSpace(scelta) ? Path.GetFullPath(scelta)
        : autoprova ? CartellaDellAutoprova
        : CartellaDeiDatiDiBase;

    /// <summary>Il registro dei gesti e degli errori (vedi <see cref="Servizi.Registro"/>).</summary>
    public Registro Registro { get; }

    /// <summary>
    /// Apre la cartella del registro in Esplora risorse: a un AOD che dice «ha fatto una cosa strana» si chiede di
    /// allegare il file di oggi. Solo su Windows (i test girano su Ubuntu, e lì non si apre niente).
    /// </summary>
    public void ApriIlRegistro()
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            Directory.CreateDirectory(Registro.Cartella);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{Registro.Cartella}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Registro.Errore("apri il registro", e);
        }
    }

    /// <summary>
    /// Apre in Esplora risorse la cartella dei PDF o dell'AIP espanso (committente, 20 ottobre: con le cartelle fisse
    /// dell'ambiente serve aprirle al volo per metterci i PDF). Solo quelle due: il Lab non apre cartelle a caso.
    /// </summary>
    public void ApriLaCartellaDellAip(bool pdf)
    {
        if (!OperatingSystem.IsWindows() || (pdf ? CartellaDeiPdf : CartellaDellAipEspanso) is not { } cartella)
            return;
        try
        {
            Directory.CreateDirectory(cartella);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{cartella}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Registro.Errore("apri la cartella", e);
        }
    }

    public StatoDelLab Stato { get; private set; }

    public SessioneAperta? Sessione { get; private set; }

    /// <summary>Un catalogo dei punti per ogni master (slice 3a): il nome risolto dipende da quale <c>.isc</c> si guarda.</summary>
    public IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi { get; private set; } =
        new Dictionary<string, CatalogoDeiPunti>();

    /// <summary>Il master con cui si risolvono i nomi: quello che carica più punti, finché l'AOD non ne sceglie un altro.</summary>
    public string? IscScelto { get; private set; }

    public IReadOnlyList<StratoDellaMappa> Strati { get; private set; } = [];

    /// <summary>Gli strati accesi della mappa dello spazio attivo: lo sfondo lo è sempre, gli altri li accende l'AOD (carta §2.2 passo 4).</summary>
    public IReadOnlySet<string> Accesi => _accesi;

    private HashSet<string> _accesi => MappaAttiva.Accesi;

    // --- gli spazi di lavoro e le loro mappe (riordino R4, D14 e D15) ------------------------------------------------

    private readonly Dictionary<SpazioDiLavoro, StatoDellaMappa> _mappe =
        Enum.GetValues<SpazioDiLavoro>().ToDictionary(s => s, s => new StatoDellaMappa(s));

    /// <summary>Lo spazio di lavoro della pagina scelta; Consegna e Impostazioni (senza mappa) lasciano quello di prima.</summary>
    public SpazioDiLavoro SpazioAttivo { get; private set; } = SpazioDiLavoro.Sector;

    /// <summary>Lo stato della mappa di uno spazio: ognuna ha il suo (D15).</summary>
    public StatoDellaMappa Mappa(SpazioDiLavoro spazio) => _mappe[spazio];

    private StatoDellaMappa MappaAttiva => _mappe[SpazioAttivo];

    /// <summary>Lo spazio di una pagina; null per quelle senza mappa (Consegna, Impostazioni).</summary>
    public static SpazioDiLavoro? SpazioDi(PaginaDelLab pagina) => pagina switch
    {
        PaginaDelLab.AipIndice or PaginaDelLab.Aip => SpazioDiLavoro.Aip,
        PaginaDelLab.Confronti => SpazioDiLavoro.Confronti,
        PaginaDelLab.Sector => SpazioDiLavoro.Sector,
        _ => null,
    };

    /// <summary>
    /// Gli strati della mappa di uno spazio: l'AIP espanso ha le coste e lo strato dell'AIP; il sector i suoi; il
    /// Confronti tutti e due, sovrapposti (D18).
    /// </summary>
    public IReadOnlyList<StratoDellaMappa> StratiDi(SpazioDiLavoro spazio) => spazio switch
    {
        SpazioDiLavoro.Aip => [.. Strati.Where(s => s.Tipo.Sfondo), .. StratoDellAip is { } aip ? [aip.Strato] : Array.Empty<StratoDellaMappa>()],
        SpazioDiLavoro.Sector => Strati,
        _ => StratoDellAip is { } aip ? [.. Strati, aip.Strato] : Strati,
    };

    /// <summary>Gli strati accesi della mappa di uno spazio.</summary>
    public IReadOnlySet<string> AccesiDi(SpazioDiLavoro spazio) => _mappe[spazio].Accesi;

    /// <summary>La vista della mappa di uno spazio.</summary>
    public IReadOnlyList<string> InVistaDi(SpazioDiLavoro spazio) => _mappe[spazio].InVista;

    /// <summary>Le voci e i generi spenti della mappa di uno spazio.</summary>
    public IReadOnlyCollection<string> SpentiDi(SpazioDiLavoro spazio) => _mappe[spazio].Spenti;

    /// <summary>
    /// La forma da evidenziare sulla mappa di uno spazio: nell'AIP la voce dell'AIP scelta, nel sector il record scelto,
    /// nel Confronti l'una o l'altro.
    /// </summary>
    public (string File, int Record)? SceltaDi(SpazioDiLavoro spazio) => spazio switch
    {
        SpazioDiLavoro.Aip => SceltaDellAip is { } i && StratoDellAip is { } s ? (s.File, i) : null,
        SpazioDiLavoro.Sector => Scelta,
        _ => SceltaSullaMappa,
    };

    /// <summary>Il record scelto sulla mappa: file e indice, l'aggancio con l'ispettore della slice 5.</summary>
    public (string File, int Record)? Scelta { get; private set; }

    /// <summary>L'albero delle cartelle da sfogliare (slice 5), costruito una volta all'apertura.</summary>
    public CartellaDaSfogliare? Albero { get; private set; }

    /// <summary>I file che stanno fuori da <c>Include/IT</c>: gli <c>.isc</c>, <c>update.ini</c>, <c>changelog.md</c>.</summary>
    public IReadOnlyList<FileDaSfogliare> FuoriDaiDati { get; private set; } = [];

    /// <summary>Il file aperto nell'elenco dei record; la scelta di un record lo apre da sé.</summary>
    public string? FileScelto { get; private set; }

    /// <summary>
    /// Vero mentre Sfoglia, Problemi, la scheda e le modifiche stanno in un'altra finestra (due schermi, chiesto dal
    /// committente il 23 settembre): la finestra principale allora mostra solo mappa e strati. Lo dice il guscio, che
    /// apre e chiude quella finestra; lo stato di tutto il resto è questo stesso servizio, e le due finestre lo vedono
    /// uguale.
    /// </summary>
    public bool PannelliInUnAltraFinestra { get; private set; }

    /// <summary>Il guscio ha aperto (o chiuso) la finestra dei pannelli.</summary>
    public void PannelliAperti(bool aperti)
    {
        if (PannelliInUnAltraFinestra == aperti)
            return;
        PannelliInUnAltraFinestra = aperti;
        Registro.Scrivi("finestre", aperti ? "pannelli in un'altra finestra" : "pannelli di nuovo qui");
        Avvisa();
    }

    /// <summary>
    /// La pagina scelta con le linguette della barra (riordino, D1 e D3): una sola per le due finestre, così con due
    /// schermi i pannelli seguono la pagina e la mappa resta nella principale. Chiudendo la cartella non cambia.
    /// </summary>
    public PaginaDelLab Pagina { get; private set; } = PaginaDelLab.Sector;

    public void ScegliLaPagina(PaginaDelLab pagina)
    {
        if (Pagina == pagina)
            return;
        Pagina = pagina;
        if (SpazioDi(pagina) is { } spazio)
            SpazioAttivo = spazio;
        Avvisa();
    }

    private bool _problemiChiesti;

    /// <summary>«N errori · M avvisi» nella barra: la pagina Sector, con la colonna sui Problemi.</summary>
    public void MostraIProblemi()
    {
        Pagina = PaginaDelLab.Sector;
        SpazioAttivo = SpazioDiLavoro.Sector;
        _problemiChiesti = true;
        Avvisa();
    }

    /// <summary>
    /// La colonna chiede se la barra ha voluto i Problemi, e la richiesta si consuma: così vale anche per una colonna
    /// che nasce adesso, passando dalla pagina AIP o Consegna a Sector.
    /// </summary>
    public bool PrendiLaRichiestaDeiProblemi()
    {
        bool chiesti = _problemiChiesti;
        _problemiChiesti = false;
        return chiesti;
    }

    /// <summary>Perché l'apertura non è riuscita: si dice a schermo, non si nasconde.</summary>
    public string? Errore { get; private set; }

    /// <summary>Quanto è costata l'ultima apertura (lettura + cataloghi + geometria): va nella barra di stato.</summary>
    public TimeSpan Durata { get; private set; }

    public event Action? Cambiata;

    /// <summary>Dice alle pagine che qualcosa è cambiato; mentre si rigiocano i gesti (annulla, ripeti) si tace: lo dice una volta alla fine.</summary>
    private void Avvisa()
    {
        if (!_rigioco)
            Cambiata?.Invoke();
    }

    /// <summary>L'ultima cartella aperta, ricordata fra un avvio e l'altro. Null se non c'è, o se il file non si legge.</summary>
    public string? UltimaCartella()
    {
        try
        {
            string file = Path.Combine(_cartellaDeiDati, "ultima-cartella.txt");
            return File.Exists(file) ? File.ReadAllText(file).Trim() is { Length: > 0 } t ? t : null : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Apre la cartella scelta: risale alla radice del clone, legge l'albero, costruisce i cataloghi e la geometria.
    /// Torna vero se è aperta; se no <see cref="Errore"/> dice perché, con le parole di <see cref="CartellaDelSector"/>.
    /// </summary>
    public async Task<bool> ApriAsync(string? percorso, CancellationToken annulla = default)
    {
        if (string.IsNullOrWhiteSpace(percorso))
        {
            Fallita("Nessuna cartella scelta.");
            return false;
        }

        // Il Lab lavora nel suo ambiente (20 ottobre): con la cartella di lavoro scelta, un sector fuori non si apre.
        if (CartellaDiLavoro is { } lavoro && !Core.Git.PrimoAvvio.NellAmbiente(lavoro, percorso))
        {
            Fallita($"Il Lab lavora nel suo ambiente ({lavoro}): questo sector sta fuori. Prendilo nell'ambiente.");
            return false;
        }

        Stato = StatoDelLab.InApertura;
        Errore = null;
        Avvisa();

        var orologio = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var cartella = CartellaDelSector.Riconosci(percorso.Trim(), out string? motivo);
            if (cartella is null)
            {
                Fallita(motivo ?? "Non è la cartella di un sector.");
                return false;
            }

            // La lettura dell'albero e la geometria sono lavoro lungo: fuori dal filo del circuito (carta §7).
            var sessione = await SessioneAperta.ApriAsync(cartella, annulla).ConfigureAwait(false);
            var cataloghi = await Task.Run(() => CatalogoDeiPunti.PerOgniIsc(sessione), annulla).ConfigureAwait(false);
            var chiLoUsa = await Task.Run(() => ChiLoUsa.Di(sessione), annulla).ConfigureAwait(false);
            string? isc = cataloghi.OrderByDescending(c => c.Value.Punti).Select(c => c.Key).FirstOrDefault();
            var strati = await Task.Run(
                () => StratiDellaMappa.DiSessione(sessione, isc is null ? null : cataloghi[isc]), annulla).ConfigureAwait(false);

            Sessione = sessione;
            Cataloghi = cataloghi;
            _chiLoUsa = chiLoUsa;
            ScordaGliUsi();
            IscScelto = isc;
            Strati = strati;
            RifaiIColori();
            Albero = AlberoDaSfogliare.Di(sessione);
            FuoriDaiDati = AlberoDaSfogliare.FuoriDaiDati(sessione);
            Scelta = null;
            FileScelto = null;
            _etichette.Clear();
            _stime.Clear();
            // Le modifiche e gli esiti di un'altra cartella non valgono per questa: nomi uguali, file diversi.
            Modifiche = new();
            _gestiDaRegistrare.Clear();
            ScordaLaStoria();
            TogliLAnteprima();
            foreach (var mappa in _mappe.Values)
                mappa.Riparti();
            ScordaIConfronti();
            _voci.Clear();
            _nascosti.Clear();
            UltimoSalvataggio = null;
            _perse.Clear();
            ScordaIProblemi();
            Stato = StatoDelLab.Aperta;
            Errore = null;
            Registro.Scrivi("apertura", $"{cartella.Radice}: {sessione.File.Count} file, {sessione.RecordTotali} record, "
                                        + $"{strati.Sum(s => s.Forme.Count)} forme, {orologio.ElapsedMilliseconds} ms");
            Ricorda(cartella.Radice);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Fallita(e.Message);
            return false;
        }
        finally
        {
            Durata = orologio.Elapsed;
            Avvisa();
        }

        // Il validatore dell'albero costa 1,7-2,0 s: l'app è già aperta, i numeri arrivano quando ci sono (slice 10).
        _ = ValidaLAlberoAsync();
        // Il ramo git (carta F4, slice 1): senza chiedere al server, che può non rispondere; «Prendi» lo chiede.
        _ = RileggiIlRamoAsync();
        return true;
    }

    // --- git (carta F4) ---------------------------------------------------------------------------------------------

    /// <summary>Il git dell'AOD (Git for Windows): i test ne passano uno loro solo per dire che non c'è.</summary>
    public Core.Git.Git Git { get; init; } = new();

    /// <summary>Il ramo del sector e il suo stato (carta F4, slice 1); null se la cartella non sta in un clone git.</summary>
    public Core.Git.StatoDelRamo? Ramo { get; private set; }

    /// <summary>Perché il ramo non c'è (git che manca, cartella fuori da git), o il server che non ha risposto.</summary>
    public string? PercheDelRamo { get; private set; }

    /// <summary>Sta leggendo il ramo, o chiedendo le novità al server.</summary>
    public bool StaLeggendoIlRamo { get; private set; }

    /// <summary>
    /// Rilegge lo stato del ramo. Con <paramref name="prendi"/> prima chiede le novità al server (<c>git fetch</c>):
    /// solo così «novità sul server» è vero. Non cambia niente nei file.
    /// </summary>
    public async Task RileggiIlRamoAsync(bool prendi = false)
    {
        // Una lettura già in corso (quella dell'apertura, o di un salvataggio) si aspetta, poi si rilegge: chi chiede
        // dopo un gesto deve vedere lo stato DOPO il gesto.
        while (_letturaDelRamo is { } inCorso)
            await inCorso.ConfigureAwait(false);
        if (Sessione is null)
            return;
        var lettura = LeggiIlRamoAsync(prendi);
        _letturaDelRamo = lettura;
        try
        {
            await lettura.ConfigureAwait(false);
        }
        finally
        {
            if (_letturaDelRamo == lettura)
                _letturaDelRamo = null;
        }
    }

    private Task? _letturaDelRamo;

    /// <summary>I rami di qui (carta F4, slice 3), in ordine.</summary>
    public IReadOnlyList<string> RamiLocali { get; private set; } = [];

    /// <summary>I rami di lavoro del server che qui non ci sono (carta F5, slice 5): ci si passa per guardarli in Aurora.</summary>
    public IReadOnlyList<string> RamiDaGuardare { get; private set; } = [];

    /// <summary>Il ramo predefinito del server (<c>master</c> sul repo vero).</summary>
    public string Predefinito { get; private set; } = "master";

    /// <summary>Il ramo del ciclo AIRAC dopo quello in vigore oggi (<c>airac/2611</c>), e se il server ce l'ha.</summary>
    public string CicloDopo { get; private set; } = Core.Git.RamiDelSector.RamoDelCicloDopo(DateTime.UtcNow);

    /// <summary>Vero se il server ha già il ramo del ciclo dopo (dall'ultimo <c>fetch</c>).</summary>
    public bool CicloDopoSulServer { get; private set; }

    /// <summary>Il ramo da cui è nato il ramo di adesso (<c>master</c>, <c>airac/2611</c>), se è un lavoro del Lab.</summary>
    public string? BaseDelRamo { get; private set; }

    private async Task LeggiIlRamoAsync(bool prendi)
    {
        // Si ridisegna solo per quel che l'AOD vede cambiare: il «…» quando ha chiesto al server, e alla fine se il ramo
        // o il suo stato sono diversi. Una lettura in background che ridisegna sempre arrivava in mezzo ai clic (due test
        // rossi intermittenti del 17 ottobre); fuori da git non cambia mai niente.
        string? prima = Impronta(Ramo, PercheDelRamo, RamiLocali, CicloDopoSulServer, $"{BaseDelRamo}|{IndietroRispettoAllaBase}|{DaRiportare}|{RiportoInCorso}|{string.Join(",", RamiDaGuardare)}");
        StaLeggendoIlRamo = true;
        if (prendi)
            Avvisa();
        try
        {
            string radice = Sessione!.Cartella.Radice;
            var (stato, perche) = await Core.Git.RepoDelSector.LeggiAsync(Git, radice, prendi).ConfigureAwait(false);
            if (stato is not null)
            {
                RamiLocali = await Core.Git.RamiDelSector.LocaliAsync(Git, stato.Radice).ConfigureAwait(false);
                Predefinito = await Core.Git.RamiDelSector.PredefinitoAsync(Git, stato.Radice).ConfigureAwait(false);
                RamiDaGuardare = await Core.Git.RamiDelSector.DaGuardareAsync(Git, stato.Radice, Predefinito).ConfigureAwait(false);
                CicloDopo = Core.Git.RamiDelSector.RamoDelCicloDopo(DateTime.UtcNow);
                CicloDopoSulServer = await Core.Git.RamiDelSector.SulServerAsync(Git, stato.Radice, CicloDopo).ConfigureAwait(false);
                BaseDelRamo = stato.Ramo is null ? null : await Core.Git.RamiDelSector.BaseDiAsync(Git, stato.Radice, stato.Ramo).ConfigureAwait(false);
                RiportoInCorso = await Core.Git.Riporto.InCorsoAsync(Git, stato.Radice).ConfigureAwait(false);
                FusioneAMeta = RiportoInCorso ? await MessaggioDellaFusioneAsync(stato.Radice).ConfigureAwait(false) : null;
                IndietroRispettoAllaBase = stato.Ramo?.StartsWith(Core.Git.RamiDelSector.PrefissoDiLavoro, StringComparison.Ordinal) == true && !RiportoInCorso
                    ? await Core.Git.Riporto.IndietroRispettoAsync(Git, stato.Radice, BaseDelRamo ?? Predefinito).ConfigureAwait(false)
                    : 0;
                DaRiportare = CicloDopoSulServer ? await Core.Git.Riporto.DaRiportareAsync(Git, stato.Radice, Predefinito, CicloDopo).ConfigureAwait(false) : 0;
            }

            Ramo = stato;
            PercheDelRamo = perche;
            Registro.Scrivi("git", stato is null ? perche ?? "nessun ramo" : $"{stato.Ramo ?? "(staccato)"}: {stato.InParole()}{(perche is null ? "" : " — " + perche)}");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            Ramo = null;
            PercheDelRamo = "git non ha risposto: " + e.Message;
            Registro.Errore("git", e);
        }
        finally
        {
            StaLeggendoIlRamo = false;
            if (prendi || Impronta(Ramo, PercheDelRamo, RamiLocali, CicloDopoSulServer, $"{BaseDelRamo}|{IndietroRispettoAllaBase}|{DaRiportare}|{RiportoInCorso}|{string.Join(",", RamiDaGuardare)}") != prima)
                Avvisa();
        }
    }

    private async Task<string?> MessaggioDellaFusioneAsync(string radice)
    {
        var dove = await Git.EseguiAsync(radice, ["rev-parse", "--git-path", "MERGE_MSG"]).ConfigureAwait(false);
        if (!dove.Riuscito)
            return null;
        string file = Path.IsPathRooted(dove.Uscita.Trim()) ? dove.Uscita.Trim() : Path.Combine(radice, dove.Uscita.Trim());
        try
        {
            return File.Exists(file) ? File.ReadLines(file).FirstOrDefault(r => r.Trim().Length > 0)?.Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string Impronta(Core.Git.StatoDelRamo? ramo, string? perche, IReadOnlyList<string> rami, bool ciclo, string? base_)
        => ramo is null
            ? "-|" + perche
            : string.Join('|', ramo.Ramo, ramo.Remoto, ramo.Monte, ramo.Avanti, ramo.Indietro, perche, ciclo, base_,
                string.Join(',', rami), string.Join(',', ramo.Cambiati.Select(c => c.Percorso + ":" + c.Come)));

    /// <summary>Cambia il master con cui si risolvono i nomi: la geometria si rifà, perché i punti risolti cambiano.</summary>
    public async Task ScegliIscAsync(string isc, CancellationToken annulla = default)
    {
        if (Sessione is null || !Cataloghi.ContainsKey(isc) || isc == IscScelto)
            return;

        IscScelto = isc;
        var sessione = Sessione;
        var catalogo = Cataloghi[isc];
        Strati = await Task.Run(() => StratiDellaMappa.DiSessione(sessione, catalogo), annulla).ConfigureAwait(false);
        // I nomi di [DEFINE] sono del master: con un altro master i riempimenti possono cambiare colore.
        RifaiIColori();
        TuttiGliStratiCambiati();
        // Le etichette dipendono dal master (un punto per nome che lì non si risolve si chiama diversamente).
        _etichette.Clear();
        Avvisa();
    }

    public void Accendi(string strato, bool acceso)
    {
        // Lo sfondo non si spegne: senza coste la mappa è un foglio bianco e non si capisce più dove si è.
        if (strato == StratiDellaMappa.Sfondo.Id)
            return;
        if (acceso)
            _accesi.Add(strato);
        else
            _accesi.Remove(strato);
        Avvisa();
    }

    /// <summary>Chiude la cartella: si torna alla schermata d'apertura, e l'albero si lascia andare.</summary>
    public void Chiudi()
    {
        Stato = StatoDelLab.Chiusa;
        Sessione = null;
        Strati = [];
        StratoDellAip = null;
        SceltaDellAip = null;
        Cataloghi = new Dictionary<string, CatalogoDeiPunti>();
        _chiLoUsa = null;
        ScordaGliUsi();
        IscScelto = null;
        Scelta = null;
        Albero = null;
        FuoriDaiDati = [];
        FileScelto = null;
        _etichette.Clear();
        Errore = null;
        Modifiche = new();
        ScordaLaStoria();
        TogliLAnteprima();
        foreach (var mappa in _mappe.Values)
            mappa.Riparti();
        ScordaIConfronti();
        _voci.Clear();
        _nascosti.Clear();
        UltimoSalvataggio = null;
        _perse.Clear();
        ScaloScelto = null;
        _scali = null;
        _vista = null;
        ScordaIProblemi();
        Avvisa();
    }

    public void Scegli(string? file, int record)
    {
        // Una forma dell'AIP espanso (F6): la sua scheda, non un record del sector — e nessun file si apre.
        if (Core.PdfAip.AipSullaMappa.EDellAip(file))
        {
            Scelta = null;
            SceltaDellAip = record;
            Registro.Scrivi("scelta", $"{file}#{record}");
            Inquadrature++;
            Avvisa();
            return;
        }

        SceltaDellAip = null;
        Scelta = file is null ? null : (file, record);
        RigaSegnalata = null;
        // La nota «forma portata anche su…» (slice 8c) era del gesto di prima, su un altro record.
        FormaPortata = null;
        // L'anteprima era di un altro record: la sua textarea non c'è più.
        if (Anteprima is { } anteprima && (anteprima.File != file || anteprima.Record != record))
        {
            Anteprima = null;
            VersioneDellAnteprima++;
        }
        Registro.Scrivi("scelta", file is null ? "nessuna" : $"{file}#{record}");
        // Scegliere un record apre il suo file nell'elenco: chi clicca una forma sulla mappa si ritrova nel posto
        // giusto dell'albero, senza cercarselo.
        if (file is not null)
        {
            FileScelto = file;
            // E accende il suo strato: i punti sono spenti di base, e un fix scelto dall'elenco non si vedeva — sulla
            // mappa non c'era niente da evidenziare (prove a mano del committente, 23 settembre).
            if (StratiDellaMappa.DiFile(file) is { } tipo && Strati.Any(s => s.Tipo.Id == tipo.Id))
            {
                // R4: sulla mappa che mostra il sector — dallo spazio dell'AIP, quella del Sector.
                var mappa = SpazioAttivo is SpazioDiLavoro.Aip ? _mappe[SpazioDiLavoro.Sector] : MappaAttiva;
                mappa.Accesi.Add(tipo.Id);
                // Slice 12c: e il suo genere, se era spento — un asse di taxiway scelto dall'elenco deve vedersi.
                if (Sessione?.File.GetValueOrDefault(file) is IFileConRecord conRecord && record >= 0 && record < conRecord.RecordDelModello.Count
                    && (conRecord.RecordDelModello[record] is Line linea ? GeneriDellaMappa.DelGeo(file, linea.Color) : GeneriDellaMappa.DelRecord(conRecord.RecordDelModello[record])) is { } genere
                    && mappa.Spenti.Remove(ChiaveDelGenere(tipo.Id, genere)))
                    mappa.VersioneDeiSpenti++;
            }

            // Ogni scelta chiede di inquadrare, anche quella dello stesso record: il secondo clic sull'elenco riporta lì.
            Inquadrature++;
        }

        Avvisa();
    }

    /// <summary>Quante volte si è chiesto di portare la mappa sul record scelto: la mappa lo confronta col suo.</summary>
    public int Inquadrature { get; private set; }

    /// <summary>L'ultimo file di cui si è rifatta la geometria: se è quello scelto, la mappa segue il record spostato.</summary>
    public string? FileRifatto { get; private set; }

    /// <summary>Il file dell'albero che un campo cita (<c>PREFS\TWR.cpr</c>), se c'è (slice 11a, N4).</summary>
    public string? FileCitato(string? citato) => Sessione is null ? null : VociDegliElenchi.FileCitato(Sessione, citato);

    /// <summary>Apre un file citato da una scheda (il profilo di una posizione): lascia la scelta e mostra il file.</summary>
    public void ApriIlFileCitato(string relativo)
    {
        Scelta = null;
        FileScelto = relativo;
        Registro.Scrivi("scelta", $"file citato {relativo}");
        Avvisa();
    }

    /// <summary>Apre (o chiude, con null) un file nell'elenco dei record. Non cambia la scelta.</summary>
    public void ApriFile(string? relativo)
    {
        FileScelto = relativo == FileScelto ? null : relativo;
        Avvisa();
    }

    /// <summary>Le etichette dei record di un file, calcolate la prima volta che il file si apre e poi tenute.</summary>
    public IReadOnlyList<string> EtichetteDi(string relativo)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(relativo, out var file))
            return [];
        if (_etichette.TryGetValue(relativo, out var gia))
            return gia;

        var etichette = Ispettore.Etichette(file, CatalogoScelto);
        _etichette[relativo] = etichette;
        return etichette;
    }

    /// <summary>La scheda del record scelto: i campi e le righe grezze del file, coi numeri di riga veri.</summary>
    public SchedaDelRecord? Scheda()
        => Scelta is { } scelta && Sessione is not null && Sessione.File.TryGetValue(scelta.File, out var file)
            ? Ispettore.Scheda(file, scelta.Record, CatalogoScelto)
            : null;

    /// <summary>
    /// Le voci degli editor a elenco (lotto «Subito», slice 3b): scali, piste, posizioni come sono adesso. Si rifanno
    /// dopo ogni modifica (<see cref="RifaiLaGeometria"/> le butta, anche per i file che sulla mappa non ci sono) e
    /// non a ogni disegno della scheda.
    /// </summary>
    public VociDegliElenchi? Elenchi()
    {
        if (Sessione is null)
            return null;
        if (_elenchi is not { } fatti || !ReferenceEquals(fatti.Sessione, Sessione))
            _elenchi = fatti = (Sessione, VociDegliElenchi.Di(Sessione));
        return fatti.Voci;
    }

    private (SessioneAperta Sessione, VociDegliElenchi Voci)? _elenchi;

    /// <summary>La ricerca per nome fra le forme della mappa (slice 5).</summary>
    public IReadOnlyList<Trovato> Cerca(string? testo) => Ricerca.Cerca(Strati, testo);

    /// <summary>I file della sessione per nome (anche quelli tenuti come testo), relativi alla radice.</summary>
    public IReadOnlyList<string> CercaFile(string? testo) => Sessione is null ? [] : Ricerca.CercaFile(Sessione.File.Keys, testo);

    /// <summary>Le modifiche fatte e non salvate (slice 6): sul disco vanno solo col salvataggio (slice 9).</summary>
    public ModificheInSospeso Modifiche { get; private set; } = new();

    // --- il salvataggio (slice 9) ------------------------------------------------------------------------------

    /// <summary>Vero mentre si salva: il tasto si spegne, due clic non fanno due salvataggi.</summary>
    public bool StaSalvando { get; private set; }

    /// <summary>
    /// Com'è andato l'ultimo salvataggio: resta a schermo finché l'AOD non lo chiude o non salva di nuovo. Se è
    /// <see cref="StatoDelSalvataggio.DaConfermare"/>, il pannello chiede la conferma con gli errori nuovi davanti.
    /// </summary>
    public EsitoDelSalvataggio? UltimoSalvataggio { get; private set; }

    /// <summary>
    /// Le modifiche perse ricaricando un file cambiato sul disco (carta §2.4 passo 2): il loro diff resta leggibile
    /// finché la finestra è aperta, così l'AOD può rifarle a mano sul file nuovo.
    /// </summary>
    public IReadOnlyDictionary<string, Diff.Esito> ModifichePerse => _perse;

    private readonly Dictionary<string, Diff.Esito> _perse = new(StringComparer.Ordinal);

    /// <summary>
    /// Salva tutte le modifiche in sospeso (carta §2.4). Senza <paramref name="confermato"/>, se ci sono errori
    /// nuovi non scrive niente e lascia l'esito «da confermare»: la conferma la dà l'AOD, con gli errori davanti.
    /// </summary>
    public async Task SalvaAsync(bool confermato = false)
    {
        if (Sessione is null || StaSalvando)
            return;

        StaSalvando = true;
        Avvisa();
        try
        {
            var salvataggio = new Salvataggio(Sessione, Modifiche, Path.Combine(_cartellaDeiDati, "backup"));
            // Leggere, validare e scrivere è lavoro sul disco: fuori dal filo del circuito (carta §7).
            var esito = await Task.Run(() => salvataggio.Salva(confermato, DateTime.Now)).ConfigureAwait(false);
            UltimoSalvataggio = esito;
            Registro.Scrivi("salvataggio", Racconta(esito, confermato));
            // La storia si scorda con la rilettura: i suoi gesti si prendono prima (carta F4, slice 4).
            var gestiSalvati = _storia.Take(_fatti).ToList();
            if (esito.Salvati.Count + esito.Invariati.Count > 0)
                await DopoLaRiletturaAsync().ConfigureAwait(false);
            // Il sector sul disco è cambiato: le regole dell'albero (nomi non risolti, file mai citati) si rifanno.
            if (esito.Salvati.Count > 0)
            {
                // I gesti salvati fanno il messaggio della prossima registrazione (carta F4, slice 4).
                _gestiDaRegistrare.AddRange(gestiSalvati.Select(g => g.Cosa));
                // E il lotto di Aurora (carta F5, slice 2): ogni gesto coi suoi file salvati, da portare in vetrina.
                NelLotto(gestiSalvati, esito.Salvati);
                _ = ValidaLAlberoAsync();
                // E git vede i file salvati da registrare (carta F4, slice 1).
                _ = RileggiIlRamoAsync();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            UltimoSalvataggio = null;
            Rifiuto = "Salvataggio non riuscito: " + e.Message;
            Registro.Errore("salvataggio", e);
        }
        finally
        {
            StaSalvando = false;
            RicontrollaLeModifiche();
            Avvisa();
        }
    }

    // --- i problemi del sector (slice 10) ----------------------------------------------------------------------

    /// <summary>I problemi del validatore sull'albero com'è sul DISCO: all'apertura e dopo ogni salvataggio.</summary>
    public IReadOnlyList<ProblemaNelLab> ProblemiDellAlbero { get; private set; } = [];

    /// <summary>Vero mentre il validatore gira (1,7-2,0 s sull'albero vero): il pannello lo dice invece di mostrare zero.</summary>
    public bool StaValidando { get; private set; }

    /// <summary>Perché la validazione non è riuscita, se non è riuscita.</summary>
    public string? ErroreDellaValidazione { get; private set; }

    /// <summary>
    /// Errori e avvisi che le modifiche in sospeso AGGIUNGEREBBERO (carta §2.2 passo 8): «la tua modifica introduce 1
    /// errore». Si rifanno dopo ogni gesto, fuori dal circuito; sono le regole di un file, non quelle dell'albero.
    /// </summary>
    public IReadOnlyList<ProblemaNelLab> ProblemiDelleModifiche { get; private set; } = [];

    /// <summary>I file che, con le modifiche in sospeso, riletti avrebbero meno record (slice 9).</summary>
    public IReadOnlyList<RecordCheSiFondono> RecordCheSiFondono { get; private set; } = [];

    /// <summary>L'ultimo controllo delle modifiche partito: chi deve aspettarlo (un test) lo aspetta qui.</summary>
    public Task ControlloDelleModifiche { get; private set; } = Task.CompletedTask;

    /// <summary>L'ultima validazione dell'albero partita.</summary>
    public Task Validazione { get; private set; } = Task.CompletedTask;

    /// <summary>La riga di un problema scelto nel pannello: l'ispettore la segna fra le righe grezze.</summary>
    public (string File, int Riga)? RigaSegnalata { get; private set; }

    private int _giroDellaValidazione;
    private int _giroDelControllo;

    /// <summary>
    /// Rifà la validazione dell'albero, fuori dal circuito. Se nel frattempo ne parte un'altra (un salvataggio subito
    /// dopo l'apertura), vale l'ultima: un giro vecchio che finisce tardi non sovrascrive i numeri nuovi.
    /// </summary>
    public Task ValidaLAlberoAsync()
    {
        if (Sessione is not { } sessione)
            return Task.CompletedTask;

        int giro = ++_giroDellaValidazione;
        // Le famiglie di forme (slice 8b) le calcola il Lab sulle forme della mappa, coi nomi già risolti.
        var strati = Strati;
        string modelloDelleConfigurazioni = ModelloDelleConfigurazioni;
        StaValidando = true;
        ErroreDellaValidazione = null;
        Avvisa();
        return Validazione = Task.Run(async () =>
        {
            IReadOnlyList<ProblemaNelLab>? problemi = null;
            string? errore = null;
            var orologio = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var forme = FormeUguali.Di(strati);
                problemi = [.. ProblemiDelLab.DellAlbero(sessione),
                    .. ProblemiDelLab.Aggancia(sessione, Core.Copie.FamiglieDichiarate.Problemi(Core.Copie.FamiglieDichiarate.Di(sessione, forme), sessione)),
                    .. ProblemiDelLab.Aggancia(sessione, UsciteDellaForma.Problemi(sessione, strati, forme)),
                    // Slice 13e (J5): un confine con la forma di un settore dell'altro tipo.
                    .. ProblemiDelLab.Aggancia(sessione, PostoDelConfine.Problemi(sessione, forme)),
                    // Slice 13f (K1): i nomi delle configurazioni, col modello scelto nelle impostazioni.
                    .. ProblemiDelLab.Aggancia(sessione, NomiDelleConfigurazioni.Problemi(sessione, modelloDelleConfigurazioni)),
                    // Slice 14e (B14): i tratti che dicono il verso e non le quote.
                    .. ProblemiDelLab.Aggancia(sessione, TrattiDelleAerovie.Problemi(sessione))];
                Registro.Scrivi("validazione", $"{problemi.Count} problemi ({problemi.Count(p => p.Gravita == Vipi.Sectorfile.Validazione.Gravita.Errore)} errori) in {orologio.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                // Un giro in background che cade non deve portarsi via l'app: si dice nel pannello, e nel registro.
                errore = e.Message;
                Registro.Errore("validazione", e);
            }

            await Task.Yield();
            if (giro != _giroDellaValidazione || !ReferenceEquals(sessione, Sessione))
                return;
            ProblemiDellAlbero = problemi ?? [];
            ErroreDellaValidazione = errore;
            StaValidando = false;
            Avvisa();
        });
    }

    /// <summary>
    /// Dopo ogni gesto: che cosa porterebbero le modifiche in sospeso. I byte si preparano QUI, sul filo del gesto,
    /// perché i record cambiano sul posto; la validazione va su un altro filo (<see cref="Core.Disco.ControlloDelleModifiche"/>).
    /// </summary>
    private void RicontrollaLeModifiche()
    {
        int giro = ++_giroDelControllo;
        if (Sessione is not { } sessione || !Modifiche.CEQualcosa)
        {
            ProblemiDelleModifiche = [];
            RecordCheSiFondono = [];
            ControlloDelleModifiche = Task.CompletedTask;
            return;
        }

        IReadOnlyList<Core.Disco.ControlloDelleModifiche.DaProvare> daProvare;
        try
        {
            daProvare = Core.Disco.ControlloDelleModifiche.Prepara(sessione, Modifiche);
        }
        catch (InvalidOperationException)
        {
            // Un record senza base (non dovrebbe succedere): il controllo salta, il salvataggio lo dirà.
            return;
        }

        ControlloDelleModifiche = Task.Run(() =>
        {
            try
            {
                var (nuovi, fusi) = Core.Disco.ControlloDelleModifiche.Prova(sessione.Cartella, daProvare);
                if (giro != _giroDelControllo || !ReferenceEquals(sessione, Sessione))
                    return;
                Aggiorna(ProblemiDelLab.Aggancia(sessione, nuovi), fusi);
            }
            catch (Exception e)
            {
                // Un giro in background: nessuno aspetta la sua eccezione, e senza questo sparirebbe.
                Registro.Errore("controllo delle modifiche", e);
                if (giro == _giroDelControllo)
                    Aggiorna([], []);
            }
        });

        // Si ridisegna solo se l'esito è cambiato: quasi tutti i gesti non portano problemi, e un ridisegno che arriva
        // da un altro filo a metà di un gesto dell'AOD non serve a nessuno (nei test bUnit rompeva il clic successivo).
        void Aggiorna(IReadOnlyList<ProblemaNelLab> nuovi, IReadOnlyList<RecordCheSiFondono> fusi)
        {
            bool uguale = nuovi.Select(p => p.Problema).SequenceEqual(ProblemiDelleModifiche.Select(p => p.Problema))
                          && fusi.SequenceEqual(RecordCheSiFondono);
            ProblemiDelleModifiche = nuovi;
            RecordCheSiFondono = fusi;
            if (!uguale)
                Avvisa();
        }
    }

    /// <summary>Il clic su un problema: il suo file nell'albero, il suo record nell'ispettore e sulla mappa, la sua riga segnata.</summary>
    public void VaiAlProblema(ProblemaNelLab problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        if (Sessione is null || !Sessione.File.ContainsKey(problema.File))
            return;

        Registro.Scrivi("problema", $"{problema.File}:{problema.Problema.Riga} {problema.Problema.Regola}");
        var file = Sessione.File[problema.File];

        // 🔴 I problemi dell'albero hanno il numero della riga SUL DISCO; quelli delle modifiche, del file di adesso.
        // Con un record aggiunto o tolto sopra, i due numeri differiscono: il clic apriva la riga di sopra
        // (committente, 24 settembre). Si porta tutto al file di adesso, che è quello che la scheda mostra.
        int? riga = problema.Problema.Riga > 0 ? problema.Problema.Riga : null;
        if (riga is { } delDisco && ProblemiDellAlbero.Contains(problema))
            riga = RigaDiAdesso(problema.File, delDisco);
        if (problema.Problema.Riga > 0 && riga is null)
            Rifiuto = $"La riga {problema.Problema.Riga} del disco è già cambiata fra le modifiche in sospeso: guarda il diff.";

        if (riga is { } adesso && file is IFileConRecord conRecord && conRecord.RecordDellaRiga(adesso) is { } record)
        {
            Scegli(problema.File, record);
        }
        else
        {
            // Niente record: l'ispettore mostra le righe intorno a quella del problema, non la scelta di prima.
            Scelta = null;
            FileScelto = problema.File;
        }

        RigaSegnalata = riga is { } segnata ? (problema.File, segnata) : null;
        Avvisa();
    }

    /// <summary>
    /// Il numero, nel file di ADESSO, della riga che all'apertura (sul disco) era la numero <paramref name="delDisco"/>;
    /// null se quella riga è stata tolta o cambiata. Senza modifiche strutturali i due numeri sono uguali.
    /// </summary>
    public int? RigaDiAdesso(string fileRelativo, int delDisco)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file)
            return delDisco;
        var apertura = Modifiche.RigheDellApertura((FileAperto)file);
        var allineate = Diff.Allinea(apertura, file.RigheDelFile(Modifiche.SporchiDi(fileRelativo)));
        return delDisco < allineate.Length ? allineate[delDisco] : null;
    }

    /// <summary>Le righe del disco intorno alla riga segnalata: per i problemi che non stanno in un record.</summary>
    public IReadOnlyList<RigaGrezza> RigheIntornoAllaSegnalata()
    {
        if (Sessione is null || RigaSegnalata is not { } segnata)
            return [];

        // Le righe del file di ADESSO (la segnalata ha già il suo numero di adesso, VaiAlProblema): sono quelle che
        // l'editor della riga a mano cambierebbe. Dal disco solo per i file che il motore non interpreta.
        var adesso = RigheDiAdesso(segnata.File);
        if (adesso.Count > 0)
        {
            int da = Math.Max(1, segnata.Riga - 3);
            int a = Math.Min(adesso.Count, segnata.Riga + 3);
            return [.. Enumerable.Range(da, Math.Max(0, a - da + 1)).Select(n => new RigaGrezza(n, adesso[n - 1], n == segnata.Riga))];
        }

        try
        {
            return RigheDelDisco.Intorno(Sessione.Cartella.Assoluto(segnata.File), segnata.Riga, contesto: 3);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Com'è andato un gesto, in una riga di registro.</summary>
    private static string Descrivi(object esito) => esito switch
    {
        ModificaRifiutata r => "rifiutata — " + r.Motivo,
        Modifica m => m.Descrizione,
        _ => esito.ToString() ?? "",
    };

    /// <summary>Un salvataggio in una riga: stato, file, conflitti, problemi nuovi, backup, errore.</summary>
    private static string Racconta(EsitoDelSalvataggio esito, bool confermato)
    {
        static string Elenco(IEnumerable<string> file) => "[" + string.Join(", ", file) + "]";
        var c = esito.Controllo;
        return $"{esito.Stato}{(confermato ? " (confermato)" : "")}: salvati {Elenco(esito.Salvati)}, invariati {Elenco(esito.Invariati)}, "
               + $"non salvati {Elenco(esito.NonSalvati)}, conflitti {Elenco(c.Conflitti)}, fuori dai confini {Elenco(c.FuoriDaiConfini)}, "
               + $"errori nuovi {c.ErroriNuovi.Count}, avvisi nuovi {c.ProblemiNuovi.Count - c.ErroriNuovi.Count}, "
               + $"record che non tornano {c.RecordCheSiFondono.Count}, backup {esito.CartellaDelBackup ?? "-"}"
               + (esito.Errore is { } errore ? $", errore: {errore}" : "");
    }

    private void ScordaIProblemi()
    {
        _giroDellaValidazione++;
        _giroDelControllo++;
        ProblemiDellAlbero = [];
        ProblemiDelleModifiche = [];
        RecordCheSiFondono = [];
        StaValidando = false;
        ErroreDellaValidazione = null;
        RigaSegnalata = null;
    }

    /// <summary>
    /// Prende dal disco un file cambiato dopo l'apertura (un conflitto): le sue modifiche si perdono, ma il loro diff
    /// resta in <see cref="ModifichePerse"/>. Gli altri file non si toccano.
    /// </summary>
    public async Task RicaricaDalDiscoAsync(string fileRelativo)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return;

        var diff = Modifiche.DiffDi(file);
        if (diff.Pezzi.Count > 0)
            _perse[fileRelativo] = diff;

        try
        {
            var sessione = Sessione;
            await Task.Run(() => sessione.Rileggi(fileRelativo)).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sparito: si dice, e le modifiche restano dove sono (non c'è un file nuovo a cui rinunciare per lui).
            _perse.Remove(fileRelativo);
            Rifiuto = $"«{fileRelativo}» non si rilegge: {e.Message}";
            Avvisa();
            return;
        }

        Modifiche.Dimentica(fileRelativo);
        Registro.Scrivi("ricarica", $"{fileRelativo} riletto dal disco, modifiche lasciate" + (_perse.ContainsKey(fileRelativo) ? " (diff tenuto)" : ""));
        // Ripresi tutti i file in conflitto, l'avviso del salvataggio fermo non ha più niente da dire.
        if (UltimoSalvataggio is { Stato: StatoDelSalvataggio.Fermo } fermo
            && !fermo.Controllo.Conflitti.Intersect(Sessione.CambiatiSulDisco(), StringComparer.Ordinal).Any())
            UltimoSalvataggio = null;

        await DopoLaRiletturaAsync().ConfigureAwait(false);
        RicontrollaLeModifiche();
        Avvisa();
    }

    /// <summary>Toglie dallo schermo l'esito del salvataggio, e i diff delle modifiche perse.</summary>
    public void ChiudiIlSalvataggio()
    {
        UltimoSalvataggio = null;
        _perse.Clear();
        Avvisa();
    }

    /// <summary>
    /// Dopo che dei file sono stati riletti dal disco: i loro record sono oggetti nuovi, quindi cataloghi, strati e
    /// albero si rifanno — un fix salvato in un altro punto deve spostare anche le forme che lo citano per nome.
    /// Costa quanto l'apertura senza la lettura (cataloghi 39 ms, geometria 42 ms sull'albero vero).
    /// </summary>
    private async Task DopoLaRiletturaAsync()
    {
        if (Sessione is null)
            return;

        var sessione = Sessione;
        string? isc = IscScelto;
        var (cataloghi, strati, chiLoUsa) = await Task.Run(() =>
        {
            var c = CatalogoDeiPunti.PerOgniIsc(sessione);
            return (c, StratiDellaMappa.DiSessione(sessione, isc is not null && c.TryGetValue(isc, out var scelto) ? scelto : null),
                ChiLoUsa.Di(sessione));
        }).ConfigureAwait(false);

        Cataloghi = cataloghi;
        _chiLoUsa = chiLoUsa;
        ScordaGliUsi();
        Strati = strati;
        Albero = AlberoDaSfogliare.Di(sessione);
        _etichette.Clear();
        if (Scelta is { } scelta && (!sessione.File.TryGetValue(scelta.File, out var file) || scelta.Record >= file.Record))
            Scelta = null;
        TuttiGliStratiCambiati();
        // Rigiocare dei gesti su record riletti dal disco vorrebbe dire rifare modifiche già salvate, o già perse.
        ScordaLaStoria();
    }

    /// <summary>L'ultimo rifiuto, da dire accanto al campo: sparisce alla modifica buona dopo.</summary>
    public string? Rifiuto { get; private set; }

    /// <summary>
    /// Cambia un campo del record scelto. Dopo una modifica la geometria del suo file si rifà: la mappa deve
    /// mostrare il punto DOV'È ADESSO, non dov'era all'apertura.
    /// </summary>
    public bool CambiaCampo(string fileRelativo, int record, string campo, string? valore)
        => NellaStoria($"{NomeDelCampo(fileRelativo, record, campo)} di {EtichettaDi(fileRelativo, record)}",
            () => CambiaCampoAdesso(fileRelativo, record, campo, valore));

    /// <summary>Il campo col nome dell'AOD, per la storia (slice 3): «Tipo di BC404», non «DisplayType di BC404».</summary>
    private string NomeDelCampo(string fileRelativo, int record, string campo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord conRecord && record >= 0 && record < conRecord.RecordDelModello.Count
           && DescrizioniDeiCampi.Di(conRecord.RecordDelModello[record], fileRelativo)?.Campi.FirstOrDefault(c => c.Proprieta == campo) is { } descritto
            ? descritto.Nome
            : campo;

    private bool CambiaCampoAdesso(string fileRelativo, int record, string campo, string? valore)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        string etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "";
        if (NomeCheNonVa(fileRelativo, record, campo, valore) is { } nonVa)
        {
            Registro.Scrivi("modifica", $"{fileRelativo}#{record} {campo} = «{valore}»: rifiutata, {nonVa}");
            Rifiuto = nonVa;
            Avvisa();
            return false;
        }

        // Il cambio va anche sulle copie gemelle che avevano lo stesso valore (carta F3-bis §2.1, slice 2).
        var esito = Modifiche.CambiaAncheLeCopie(file, record, campo, valore, GemelliDellaSessione.Di(Sessione),
            f => Sessione.File.GetValueOrDefault(f), etichetta);
        Registro.Scrivi("modifica", $"{fileRelativo}#{record} {campo} = «{valore}»: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDiCampo fatta)
        {
            RifaiLaGeometria(fileRelativo);
            foreach (var copia in Modifiche.CopieDi(fatta))
            {
                Registro.Scrivi("modifica", $"  anche {copia.File}#{copia.Record}");
                RifaiLaGeometria(copia.File);
            }

            foreach (var lasciata in fatta.NonToccate)
                Registro.Scrivi("modifica", $"  non {lasciata.File}#{lasciata.Record}: ha {lasciata.Valore}");
        }

        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDiCampo;
    }

    /// <summary>
    /// Il nome di un fix, VOR, NDB o attesa scritto nel suo campo della scheda (lotto «Subito» slice 10c, L3): se il
    /// punto è citato, il nome si cambia con «Rinomina» (che riscrive anche le citazioni); un nome che c'è già non va.
    /// Null se va bene, o se il campo non è il nome di uno di questi. Un record nuovo, che nessuno cita, si chiama qui.
    /// </summary>
    private string? NomeCheNonVa(string fileRelativo, int record, string campo, string? valore)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file || record < 0 || record >= file.RecordDelModello.Count)
            return null;
        object punto = file.RecordDelModello[record];
        (string Campo, string Nome)? delNome = punto switch
        {
            Fix f => (nameof(Fix.Name), f.Name),
            Vor v => (nameof(Vor.Ident), v.Ident),
            Ndb n => (nameof(Ndb.Ident), n.Ident),
            Attesa a => (nameof(Attesa.Nome), a.Nome),
            // Slice 11a: una posizione la citano i trasferimenti e le teste dei settori dinamici.
            AtcPosition p => (nameof(AtcPosition.Code), p.Code),
            _ => null,
        };
        string nuovo = valore?.Trim() ?? string.Empty;
        if (delNome is not { } nome || nome.Campo != campo || nuovo.Length == 0
            || string.Equals(nuovo, nome.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;
        if (UsiDi(fileRelativo, record) is { Citazioni.Count: > 0 and var quante })
            return $"{nome.Nome.Trim()} è citato in {(quante == 1 ? "1 riga" : $"{quante} righe")}: il nome si cambia con «Rinomina», in «Chi lo usa», che riscrive anche le citazioni.";
        // Una posizione ripetuta la dice il validatore (PosizioneRipetuta); i cataloghi dei punti non la riguardano.
        return punto is AtcPosition ? null : Rinomina.NomeGiaUsato(Sessione, Cataloghi, punto is Attesa, nuovo);
    }

    /// <summary>
    /// Tipo e slot proposti per uno stand dai suoi metadati (slice 12b, R2b: codice, uso, compagnie); null se il record
    /// non è uno stand, se i metadati non dicono niente o se lo stand è già così.
    /// </summary>
    public TipoESlotProposti? PropostaDelloStandDi(string fileRelativo, int record)
        => StandDi(fileRelativo, record) is { } stand && Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file
            ? PropostaDelloStand.Di(stand, file.ChiaviDi(record))
            : null;

    /// <summary>Il posto di un riempimento nell'ordine di disegno del suo .pol (slice 12c, I3); null se non lo è.</summary>
    public PostoNelDisegno? PostoNelDisegnoDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file ? OrdineDiDisegno.Di(file, record) : null;

    /// <summary>Lo stand del record, o null se il record non è uno stand.</summary>
    public Stand? StandDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file && record >= 0 && record < file.RecordDelModello.Count
            ? file.RecordDelModello[record] as Stand
            : null;

    /// <summary>Scrive tipo e slot proposti nello stand, in una voce sola della storia.</summary>
    public bool ApplicaLaPropostaDelloStand(string fileRelativo, int record)
        => PropostaDelloStandDi(fileRelativo, record) is { } proposta
           && NellaStoria($"tipo e slot proposti di {EtichettaDi(fileRelativo, record)}", () =>
           {
               bool tipo = proposta.Tipo is not null && CambiaCampoAdesso(fileRelativo, record, nameof(Stand.Type), proposta.Tipo);
               bool slot = proposta.Slot is not null && CambiaCampoAdesso(fileRelativo, record, nameof(Stand.Slot), proposta.Slot);
               return tipo || slot;
           });

    /// <summary>I metadati di §M del record, per la scheda (lotto «Subito», slice 3d); vuoto se il file non porta tag.</summary>
    public IReadOnlyList<MetadatoDellaScheda> MetadatiDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file ? MetadatiDellaScheda.Di(file, record) : [];

    /// <summary>
    /// Il fix proposto dal nome di una SID o di una procedura .str (slice 9c, P7), coi punti del master scelto; null se
    /// il record non è una procedura, se ha già il fix, o se il nome non dice un punto.
    /// </summary>
    public FixProposto? FixPropostoDi(string fileRelativo, int record)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file || record < 0 || record >= file.RecordDelModello.Count
            || CatalogoScelto is not { } catalogo || file.ChiaviDi(record)?.ContainsKey("fix") == true)
            return null;
        return file.RecordDelModello[record] switch
        {
            Vipi.Sectorfile.Models.SidProcedure sid => FixDalNome.Di(sid.Name, sid.IcaoCode, catalogo),
            Vipi.Sectorfile.Models.StrRecord voce when !DescrizioniDeiCampi.EUnaMappa(voce.RunwaySpec) => FixDalNome.Di(voce.ProcedureId, voce.IcaoCode, catalogo),
            _ => null,
        };
    }

    /// <summary>
    /// Le altre procedure della voce del record (la sua pista e il suo tipo, slice 9b), alle quali «a tutta la voce»
    /// porta un suo metadato (slice 9c, P6: «valori di gruppo per pista come gesto»). Vuoto se non è una procedura.
    /// </summary>
    public IReadOnlyList<int> AltreDellaVoce(string fileRelativo, int record)
        => VoceDi(fileRelativo, record) is { Pista: not null } voce ? [.. voce.Record.Where(r => r != record)] : [];

    /// <summary>
    /// Porta il valore di una chiave del record a tutte le altre procedure della sua voce (P6): un gesto solo nella
    /// storia, una modifica per record. Le procedure che hanno già quel valore non si toccano.
    /// </summary>
    public bool MetadatoATuttaLaVoce(string fileRelativo, int record, string chiave)
    {
        if (VoceDi(fileRelativo, record) is not { Pista: not null } voce
            || MetadatiDi(fileRelativo, record).FirstOrDefault(m => m.Chiave == chiave)?.Valore is not { } valore)
            return false;
        return NellaStoria($"{chiave}={valore} a tutta la voce {voce.Nome} di {NomeDelFile(fileRelativo)}", () =>
        {
            bool fatto = false;
            foreach (int altro in AltreDellaVoce(fileRelativo, record))
            {
                if (MetadatiDi(fileRelativo, altro).FirstOrDefault(m => m.Chiave == chiave)?.Valore != valore)
                    fatto |= CambiaIlMetadatoAdesso(fileRelativo, altro, chiave, valore);
            }

            return fatto;
        });
    }

    /// <summary>
    /// I legami della voce di un .str con le altre della stessa pista (slice 9e, Q2c): prima quelle da cui si arriva,
    /// poi quelle dove si va. Vuoto per le voci senza legami e per gli altri file.
    /// </summary>
    public IReadOnlyList<LegameDellaScheda> LegamiDi(string fileRelativo, int record)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file || file.RecordDelModello.Count == 0
            || file.RecordDelModello[0] is not Vipi.Sectorfile.Models.StrRecord)
            return [];
        var voci = file.RecordDelModello;
        string Nome(int i) => ((Vipi.Sectorfile.Models.StrRecord)voci[i]).ProcedureId.Trim();
        string Tipo(int i) => ((Vipi.Sectorfile.Models.StrRecord)voci[i]).RecordType switch
        {
            Vipi.Sectorfile.Models.StrRecordType.Star => "STAR",
            Vipi.Sectorfile.Models.StrRecordType.Holding => "attesa",
            Vipi.Sectorfile.Models.StrRecordType.Iap => "avvicinamento",
            Vipi.Sectorfile.Models.StrRecordType.GoAround => "mancato avvicinamento",
            var altro => altro.ToString(),
        };
        var legami = Vipi.Sectorfile.Validazione.LegamiDelleProcedure.Di(voci);
        return [.. legami.Where(l => l.A == record).Select(l => new LegameDellaScheda(true, l.Da, Nome(l.Da), Tipo(l.Da), l.Pista, l.Punto))
            .Concat(legami.Where(l => l.Da == record).Select(l => new LegameDellaScheda(false, l.A, Nome(l.A), Tipo(l.A), l.Pista, l.Punto)))];
    }

    /// <summary>
    /// L'attesa in rotta citata dal fix, VOR o NDB scelto (slice 10c): dove sta in <c>[HOLDENR]</c>, o che non c'è; null
    /// se il record non è un navaid o non cita un'attesa.
    /// </summary>
    public AttesaDellaScheda? AttesaDi(string fileRelativo, int record)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file || record < 0 || record >= file.RecordDelModello.Count)
            return null;
        var (nomeDelPunto, citata) = file.RecordDelModello[record] switch
        {
            Fix f => (f.Name, f.NomeDellAttesa),
            Vor v => (v.Ident, v.NomeDellAttesa),
            Ndb n => (n.Ident, n.NomeDellAttesa),
            _ => (string.Empty, null),
        };
        if (citata?.Trim() is not { Length: > 0 } nome)
            return null;

        (string File, int Indice, Attesa Attesa)? Cerca(string cercato)
        {
            foreach (var (relativo, altro) in Sessione.File.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                if (altro is not IFileConRecord conRecord)
                    continue;
                for (int i = 0; i < conRecord.RecordDelModello.Count; i++)
                {
                    if (conRecord.RecordDelModello[i] is Attesa a && string.Equals(a.Nome.Trim(), cercato, StringComparison.Ordinal))
                        return (relativo, i, a);
                }
            }

            return null;
        }

        if (Cerca(nome) is { } trovata)
            return new AttesaDellaScheda(nome, trovata.File, trovata.Indice, trovata.Attesa.Descrizione, null);
        string col = "HLD-" + nomeDelPunto.Trim();
        return new AttesaDellaScheda(nome, null, -1, null, col != nome && Cerca(col) is not null ? col : null);
    }

    /// <summary>
    /// I punti di una procedura coi loro vincoli (slice 9d, Q2): SID col tracciato e voci .str su una pista; vuoto per
    /// le mappe del MAPS e per i file i cui punti non portano tag.
    /// </summary>
    public IReadOnlyList<PuntoConTag> PuntiDellaProceduraDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file && record >= 0 && record < file.RecordDelModello.Count
           && EUnaProcedura(file.RecordDelModello[record])
            ? file.PuntiConTag(record)
            : [];

    /// <summary>
    /// I tratti di un pezzo di aerovia col loro verso e le loro quote (slice 14c, B2); vuoto per gli altri record. Si
    /// scrivono con <see cref="CambiaIlTagDelPunto"/>, sul punto che apre il tratto.
    /// </summary>
    public IReadOnlyList<TrattoDiAerovia> TrattiDellAeroviaDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file ? TrattiDelleAerovie.Di(file, record) : [];

    private static bool EUnaProcedura(object record) => record switch
    {
        Vipi.Sectorfile.Models.SidProcedure => true,
        Vipi.Sectorfile.Models.StrRecord voce => !DescrizioniDeiCampi.EUnaMappa(voce.RunwaySpec),
        _ => false,
    };

    /// <summary>Scrive, cambia o toglie (vuoto) una chiave del tag di un punto della procedura (slice 9d, Q2).</summary>
    public bool CambiaIlTagDelPunto(string fileRelativo, int record, int ordinale, string chiave, string? valore)
        => NellaStoria($"{chiave} del punto {ordinale + 1} di {EtichettaDi(fileRelativo, record)}", () =>
        {
            if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
                return false;
            if (!ValoriDeiMetadati.Normalizza(chiave, valore, null, out string? normale, out string? perche))
            {
                Rifiuto = perche;
                Registro.Scrivi("modifica", $"{fileRelativo}#{record} punto {ordinale} {chiave} = «{valore}»: rifiutata, {perche}");
                Avvisa();
                return false;
            }

            var esito = Modifiche.CambiaIlTagDelPunto(file, record, ordinale, chiave, normale, EtichettaDi(fileRelativo, record));
            Registro.Scrivi("modifica", $"{fileRelativo}#{record} punto {ordinale} {chiave} = «{normale}»: {Descrivi(esito)}");
            Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
            // I vincoli viaggiano con la forma della mappa (il passaggio del mouse): la si rifà.
            if (esito is ModificaDelMetadato)
                RifaiLaGeometria(fileRelativo);
            RicontrollaLeModifiche();
            Avvisa();
            return esito is ModificaDelMetadato;
        });

    /// <summary>Scrive, cambia o toglie (vuoto) una chiave dei metadati del record: il tag sopra il record.</summary>
    public bool CambiaIlMetadato(string fileRelativo, int record, string chiave, string? valore)
        => NellaStoria($"{chiave} di {EtichettaDi(fileRelativo, record)}", () => CambiaIlMetadatoAdesso(fileRelativo, record, chiave, valore));

    private bool CambiaIlMetadatoAdesso(string fileRelativo, int record, string chiave, string? valore)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        // Prova 68: fix e transizione sono punti del master scelto, la salita è una quota, le categorie lettere in ordine.
        var master = IscScelto is null ? null : Cataloghi.GetValueOrDefault(IscScelto);
        if (!ValoriDeiMetadati.Normalizza(MetadatiDellaScheda.Senzaverso(chiave), valore, master is null ? null : master.Risolve,
                                           out string? normale, out string? perche))
        {
            Rifiuto = perche;
            Registro.Scrivi("modifica", $"{fileRelativo}#{record} tag {chiave} = «{valore}»: rifiutata, {perche}");
            Avvisa();
            return false;
        }

        valore = normale;
        var esito = Modifiche.CambiaIlMetadato(file, record, chiave, valore, EtichettaDi(fileRelativo, record));
        Registro.Scrivi("modifica", $"{fileRelativo}#{record} tag {chiave} = «{valore}»: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        // Slice 13g: limiti e classe viaggiano con la forma della mappa (il passaggio del mouse), come i vincoli dei punti.
        if (esito is ModificaDelMetadato && chiave is "lower" or "upper" or "class")
            RifaiLaGeometria(fileRelativo);
        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDelMetadato;
    }

    /// <summary>«Composta da» per la scheda (F3-bis slice 5): null se il record non è una mappa MAPS di un .str o di un .sid (F7 slice 7f).</summary>
    public SchedaDellaComposta? CompostaDi(string fileRelativo, int record)
        => Sessione is not null && Sessione.File.TryGetValue(fileRelativo, out var file) ? SchedaDellaComposta.Di(file, record) : null;

    /// <summary>
    /// Spunta o toglie una procedura dall'elenco di una mappa composta, o cambia se si disegnano intere: il tag si
    /// riscrive e la mappa si rigenera, tutto nelle modifiche in sospeso.
    /// </summary>
    public bool CambiaLaComposta(string fileRelativo, int record, IReadOnlyList<ProceduraDellaComposta> elenco, bool? intere = null)
        => NellaStoria($"composizione di {EtichettaDi(fileRelativo, record)}", () => CambiaLaCompostaAdesso(fileRelativo, record, elenco, intere));

    private bool CambiaLaCompostaAdesso(string fileRelativo, int record, IReadOnlyList<ProceduraDellaComposta> elenco, bool? intere)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        var esito = Modifiche.CambiaLaComposta(file, record, elenco, intere);
        Registro.Scrivi("composta", $"{fileRelativo}#{record} = «{string.Join(",", elenco.Select(v => (v.Pista is null ? "" : v.Pista + ":") + v.Nome))}»" +
            (intere is { } i ? $" intere={i}" : "") + $": {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is Modifica)
            RifaiLaGeometria(fileRelativo);

        RicontrollaLeModifiche();
        Avvisa();
        return esito is Modifica;
    }

    /// <summary>I vertici di una forma, per l'elenco dell'ispettore (slice 7). Vuoto se quel campo non è vertici.</summary>
    public IReadOnlyList<string> VerticiDi(string fileRelativo, int record, string campo)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return [];

        return ElenchiDiVertici.Uno(file, record, campo) is { } elenco
            ? [.. Enumerable.Range(0, elenco.Quanti).Select(elenco.Scrivi)]
            : [];
    }

    /// <summary>
    /// Tutti gli elenchi di vertici del record scelto (slice 7-bis): uno per una forma semplice, uno per TRATTO
    /// in una zona a più tratti.
    /// </summary>
    public IReadOnlyList<ElencoDiVertici> ElenchiDiVerticiDi(string fileRelativo, int record)
        => Sessione is not null && Sessione.File.TryGetValue(fileRelativo, out var file)
            ? ElenchiDiVertici.Di(file, record)
            : [];

    /// <summary>
    /// Gli archi di «incolla da testo», di base: un punto ogni 5 gradi (committente, 24 settembre). L'AOD lo cambia nella
    /// scheda; prima era la stima dal file, che ora la scheda dice accanto (<see cref="StimaDeiGradiDi"/>).
    /// </summary>
    public const double GradiPerPuntoDiBase = 5;

    /// <summary>La densità di base, in punti per grado: <see cref="GradiPerPuntoDiBase"/> rovesciato.</summary>
    public static double DensitaDiBase => 1 / GradiPerPuntoDiBase;

    /// <summary>
    /// Quanti gradi fra un punto e l'altro hanno gli archi che quel record — o, se lui non ne ha, il suo file — ha già;
    /// null se non ci sono archi da misurare. La scheda lo dice accanto al campo: è un'indicazione, non il valore.
    /// </summary>
    public double? StimaDeiGradiDi(string fileRelativo, int record)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return null;
        if (_stime.TryGetValue((fileRelativo, record), out double? gia))
            return gia;

        var delRecord = ElenchiDiVertici.Di(file, record).Select(e => e.Posizioni());
        double? densita = DensitaDegliArchi.Stima(delRecord)
                          ?? DensitaDegliArchi.Stima(Enumerable.Range(0, file.Record)
                                 .SelectMany(i => ElenchiDiVertici.Di(file, i)).Select(e => e.Posizioni()));
        double? gradi = densita is { } d && d > 0 ? Math.Round(1 / d, 1) : null;
        _stime[(fileRelativo, record)] = gradi;
        return gradi;
    }

    private readonly Dictionary<(string File, int Record), double?> _stime = [];

    /// <param name="puntiPerGrado">Solo per «incolla»: quanto fitti gli archi; di base un punto ogni 5 gradi
    /// (<see cref="GradiPerPuntoDiBase"/>).</param>
    public bool GestoSuiVertici(string fileRelativo, int record, string campo, GestoDeiVertici gesto,
                                int posizione = 0, string? testo = null, double? puntiPerGrado = null, bool? chiudi = null)
    {
        // La densità si fissa ADESSO: rigiocato più tardi, il gesto deve dare gli stessi punti anche se la stima è cambiata.
        double? densita = gesto == GestoDeiVertici.Incolla ? puntiPerGrado ?? DensitaDiBase : null;
        // Anche la chiusura si fissa adesso: rigiocato dopo altri gesti, l'elenco di partenza potrebbe essere un altro.
        if (gesto == GestoDeiVertici.Incolla)
            chiudi ??= ElencoChiuso(fileRelativo, record, campo);
        string cosa = gesto switch
        {
            GestoDeiVertici.Cambia => "vertice spostato",
            GestoDeiVertici.Aggiungi => "vertice aggiunto",
            GestoDeiVertici.Togli => "vertice tolto",
            GestoDeiVertici.Inverti => "ordine invertito",
            _ => "vertici incollati",
        };
        // Slice 8c: le copie della stessa forma si fissano ADESSO, come la densità: rigiocato più tardi (annulla, ripeti)
        // il gesto porta la forma sulle stesse copie, e gli strati di quel momento non sono ancora rifatti.
        var copie = CopieDellElenco(fileRelativo, record, campo);
        bool fatto = NellaStoria($"{cosa} in {EtichettaDi(fileRelativo, record)}",
            () => GestoSuiVerticiAdesso(fileRelativo, record, campo, gesto, posizione, testo, densita, chiudi, copie));
        if (fatto && gesto == GestoDeiVertici.Incolla)
            TogliLAnteprima();
        return fatto;
    }

    private bool GestoSuiVerticiAdesso(string fileRelativo, int record, string campo, GestoDeiVertici gesto,
                                       int posizione, string? testo, double? puntiPerGrado, bool? chiudi,
                                       IReadOnlyList<ParteDiForma> copie)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        // Le copie che ADESSO sono uguali a questa forma: dopo il gesto la ricevono (D5). Quelle già diverse no.
        FormaPortata = null;
        var uguali = ElenchiDiVertici.Uno(file, record, campo) is { } prima && PortaLaForma.Da(prima, CatalogoScelto) is { } forma
            ? AncoraUguali(forma, copie)
            : [];

        string etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "";
        object esito = gesto switch
        {
            GestoDeiVertici.Cambia => Modifiche.CambiaVertice(file, record, campo, posizione, testo, etichetta),
            GestoDeiVertici.Aggiungi => Modifiche.AggiungiVertice(file, record, campo, posizione, testo, etichetta),
            GestoDeiVertici.Togli => Modifiche.TogliVertice(file, record, campo, posizione, etichetta),
            GestoDeiVertici.Inverti => Modifiche.InvertiVertici(file, record, campo, etichetta),
            _ => Modifiche.IncollaVertici(file, record, campo, testo, etichetta,
                                          puntiPerGrado ?? DensitaDiBase, chiudi),
        };
        Registro.Scrivi("vertici", $"{fileRelativo}#{record} {campo} {gesto} {posizione}"
                                   + (gesto == GestoDeiVertici.Incolla ? $" ({testo?.Length ?? 0} caratteri)" : $" «{testo}»")
                                   + $": {Descrivi(esito)}");

        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDeiVertici)
        {
            RifaiLaGeometria(fileRelativo);
            if (uguali.Count > 0 && ElenchiDiVertici.Uno(file, record, campo) is { } dopo)
                FormaPortata = PortaSulleCopie(PortaLaForma.Da(dopo, CatalogoScelto), $"{NomeDelFile(fileRelativo)} {etichetta}", uguali);
        }

        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDeiVertici;
    }

    // --- la forma portata sulle copie (lotto «Subito» slice 8c, D5) --------------------------------------------------

    /// <summary>
    /// Dopo l'ultimo gesto sui vertici: le copie che hanno ricevuto la stessa forma, e quelle che non la sanno scrivere
    /// (col perché). Null se il gesto non aveva copie uguali.
    /// </summary>
    public FormaPortataSulleCopie? FormaPortata { get; private set; }

    /// <summary>Le altre forme (per parte) che hanno la stessa forma di quest'elenco: le candidate a riceverla.</summary>
    private IReadOnlyList<ParteDiForma> CopieDellElenco(string fileRelativo, int record, string campo)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not { } file)
            return [];
        return [.. StessaFormaDi(fileRelativo, record)
            .Where(p => PortaLaForma.ElencoDellaParte(file, record, p.Parte.Parte)?.Chiave == campo)
            .SelectMany(p => p.Copie.Where(c => c.Uguale).Select(c => c.Dove))];
    }

    /// <summary>Le copie uguali di una linea di un .geo (8d): quelle della sua forma.</summary>
    private IReadOnlyList<ParteDiForma> CopieDellaLinea(string fileRelativo, int record)
        => [.. StessaFormaDi(fileRelativo, record).SelectMany(p => p.Copie.Where(c => c.Uguale).Select(c => c.Dove))];

    /// <summary>Fra le copie candidate, quelle che nel modello di adesso sono ancora uguali alla forma.</summary>
    private List<ParteDiForma> AncoraUguali(FormaDiPartenza forma, IReadOnlyList<ParteDiForma> copie)
        => [.. copie.Where(c => FormaDi(c) is { } sua && PortaLaForma.Uguali(forma, sua))];

    private ElencoDiVertici? ElencoDellaCopia(ParteDiForma copia)
        => Sessione?.File.GetValueOrDefault(copia.File) is { } file ? PortaLaForma.ElencoDellaParte(file, copia.Record, copia.Parte) : null;

    /// <summary>La forma di una parte com'è adesso: un elenco di vertici, o una linea di un .geo (8d).</summary>
    private FormaDiPartenza? FormaDi(ParteDiForma parte)
    {
        if (Sessione?.File.GetValueOrDefault(parte.File) is not { } file)
            return null;
        if (PortaLaForma.ElencoDellaParte(file, parte.Record, parte.Parte) is { } elenco)
            return PortaLaForma.Da(elenco, CatalogoScelto);
        return Modifiche.LineaDi(file, parte.Record) is { } linea ? PortaLaForma.Da(linea) : null;
    }

    /// <summary>
    /// Porta la forma sulle copie: una voce di vertici per copia che è un elenco, una voce nel testo per una linea di un
    /// .geo (8d), ognuna col suo file.
    /// </summary>
    private FormaPortataSulleCopie PortaSulleCopie(FormaDiPartenza? forma, string da, IReadOnlyList<ParteDiForma> copie)
    {
        var portate = new List<ParteDiForma>();
        var no = new List<(ParteDiForma Copia, string Perche)>();
        // 🔴 Una linea di un .geo riscritta può avere un segmento in più o in meno, e i record dopo di lei nel suo file
        // slittano (misura 8d: liaa.geo ha la stessa linea due volte). Nello stesso file si scrive dall'ultima copia alla
        // prima, così gli indici di quelle ancora da scrivere restano giusti; la scelta, se sta dopo, segue il suo record.
        foreach (var copia in copie.OrderBy(c => c.File, StringComparer.Ordinal).ThenByDescending(c => c.Record))
        {
            int primaDelGesto = Sessione?.File.GetValueOrDefault(copia.File)?.Record ?? 0;
            if (forma is null)
            {
                no.Add((copia, "La forma di partenza ha un nome che non si risolve."));
                continue;
            }

            if (Sessione?.File.GetValueOrDefault(copia.File) is not { } suoFile)
                continue;

            object esito;
            if (ElencoDellaCopia(copia) is { } suo)
            {
                if (PortaLaForma.VociPer(forma, suo, CatalogoScelto, out string? perche) is not { } voci)
                {
                    no.Add((copia, perche!));
                    continue;
                }

                esito = Modifiche.SostituisciIVertici(suoFile, copia.Record, suo.Chiave, voci, $"forma portata da {da}",
                    EtichettaDi(copia.File, copia.Record));
            }
            else if (Modifiche.LineaDi(suoFile, copia.Record) is not null)
            {
                esito = Modifiche.RiscriviLaLinea(suoFile, copia.Record, forma, $"{EtichettaDi(copia.File, copia.Record)}: forma portata da {da}");
            }
            else
            {
                no.Add((copia, "Questa copia non è né un elenco di vertici né una linea di un .geo."));
                continue;
            }

            Registro.Scrivi("vertici", $"  anche {copia.File}#{copia.Record}: {Descrivi(esito)}");
            if (esito is ModificaRifiutata rifiutata)
            {
                no.Add((copia, rifiutata.Motivo));
                continue;
            }

            portate.Add(copia);
            int slittati = suoFile.Record - primaDelGesto;
            if (slittati != 0 && Scelta is { } scelta && scelta.File == copia.File && scelta.Record > copia.Record)
                Scelta = (scelta.File, scelta.Record + slittati);
            RifaiLaGeometria(copia.File);
        }

        return new FormaPortataSulleCopie(portate, no);
    }

    /// <summary>
    /// «Allinea questa» e «prendi la sua» (slice 8c, D5): la forma della parte <paramref name="da"/> portata sulla parte
    /// <paramref name="a"/>, che tiene la sua partenza, il suo verso e la scrittura dei suoi vertici. Tutte e due possono
    /// essere una linea di un .geo (8d).
    /// </summary>
    public bool CopiaLaForma(ParteDiForma da, ParteDiForma a)
        => NellaStoria($"forma di {da.Etichetta} portata su {a.Etichetta}", () => CopiaLaFormaAdesso(da, a));

    private bool CopiaLaFormaAdesso(ParteDiForma da, ParteDiForma a)
    {
        if (FormaDi(da) is not { } forma)
        {
            Rifiuto = "La forma di partenza non si legge (un nome che non si risolve?).";
            Avvisa();
            return false;
        }

        FormaPortata = PortaSulleCopie(forma, $"{NomeDelFile(da.File)} {EtichettaDi(da.File, da.Record)}", [a]);
        Rifiuto = FormaPortata.NonPortate.Count > 0 ? FormaPortata.NonPortate[0].Perche : null;
        RicontrollaLeModifiche();
        Avvisa();
        return FormaPortata.Portate.Count > 0;
    }

    private static string ComeSiLegge(object punto) => punto switch
    {
        Coordinate c => CoordinateConverter.ToDottedDms(c),
        Punto p when p.PerNome => p.Nome == p.NomeLongitudine ? p.Nome! : $"{p.Nome} {p.NomeLongitudine}",
        Punto p => CoordinateConverter.ToDottedDms(p.Posizione!.Value),
        _ => punto.ToString() ?? "",
    };

    /// <summary>
    /// Aggiunge un record copiando quello scelto (slice 8) e ci si sposta sopra: il nuovo è già nella forma dei
    /// vicini, e l'AOD cambia quel che deve.
    /// </summary>
    /// <param name="nome">Per i record col nome (fix, VOR, NDB, punti VFR): il nome del nuovo, che va al suo posto in
    /// ordine alfabetico nella sezione dove lo mette il nome, anche se il modello sta in un'altra (prove 6 e 40-41 del
    /// committente). Null = subito sotto il modello.</param>
    /// <param name="tipo">Il tipo fisso scelto per il nuovo (slice 3e, <see cref="TipoDelNuovoDi"/>); null = quello del modello.</param>
    /// <param name="codice">Per un punto VFR col nome (slice 16c, F3): il suo codice, che lo mette anche in ordine nel
    /// file; null = quello che il Lab propone (<see cref="CodiceProposto"/>). Un codice già preso si rifiuta.</param>
    public bool AggiungiRecord(string fileRelativo, int record, string? nome = null, string? tipo = null, string? codice = null)
        => NellaStoria(nome is null ? $"record aggiunto in {NomeDelFile(fileRelativo)}" : $"{nome} aggiunto in {NomeDelFile(fileRelativo)}",
            () => GestoDiStruttura(fileRelativo,
                () =>
                {
                    if (Sessione!.File[fileRelativo] is not { } file)
                        return new ModificaRifiutata("Questo file non è aperto.");
                    if (nome is not null && ChiedeIlCodice(fileRelativo))
                    {
                        codice = string.IsNullOrWhiteSpace(codice) ? CodiceProposto(fileRelativo, record) : codice.Trim();
                        if (ControlloDeiVfr.EUnCodice(codice) && CodiciVfr.ChiLoHa(Sessione, codice!) is { } chi)
                            return new ModificaRifiutata($"Il codice {codice} è già di {chi}: un codice vale in tutto il sector, è il nome del fix nascosto.");
                    }

                    return Modifiche.AggiungiRecord(file, record, nome, tipo, codice);
                }));

    /// <summary>Vero se il record nuovo di quel file chiede anche un codice: i punti VFR dei <c>.vfi</c> (slice 16c).</summary>
    public bool ChiedeIlCodice(string fileRelativo)
        => fileRelativo.EndsWith(".vfi", StringComparison.OrdinalIgnoreCase)
           && Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord { RecordDelModello: [VfrPoint, ..] };

    /// <summary>
    /// Il codice che il Lab propone a un punto VFR nuovo (slice 16c, F3): le lettere del punto da cui si parte (null:
    /// l'ultimo del file) e il primo numero libero dopo il suo, in tutto il sector.
    /// </summary>
    public string? CodiceProposto(string fileRelativo, int? modello)
        => Sessione is null ? null : CodiciVfr.Proposto(Sessione, fileRelativo, modello);

    /// <summary>
    /// Una procedura nuova in una voce della vista per pista e tipo (slice 9b, P2): copia l'ultima di quella pista e va
    /// sotto di lei, nel gruppo della pista e col tipo della voce.
    /// </summary>
    public bool AggiungiNellaVoce(string fileRelativo, VoceDellaSelezione voce)
    {
        ArgumentNullException.ThrowIfNull(voce);
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file || voce.ModelloDelNuovo(file.RecordDelModello) is not { } modello)
            return false;
        return AggiungiRecord(fileRelativo, modello);
    }

    /// <summary>Il tipo che «+ Nuovo record» chiede prima di tutto in quel file (slice 3e, A1), o null.</summary>
    public SceltaDelTipo? TipoDelNuovoDi(string fileRelativo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file ? TipoDelNuovo.Di(file) : null;

    /// <summary>Il tipo del record scelto come lo scrive la domanda del nuovo: il nuovo «come questo» parte da lì.</summary>
    public string? TipoDelRecord(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord conRecord && record >= 0 && record < conRecord.RecordDelModello.Count
           && TipoDelNuovoDi(fileRelativo) is { } scelta
            ? scelta.Valori.FirstOrDefault(v => TipoDelNuovo.EDelTipo(conRecord.RecordDelModello[record], scelta, v.Valore))?.Valore
            : null;

    /// <summary>
    /// Un record nuovo dal FILE, senza sceglierne uno prima (prova 6 del committente). Il modello è il vicino per nome
    /// (quello che in ordine alfabetico viene subito prima), o l'ultimo record per i file senza nomi da ordinare.
    /// </summary>
    public bool AggiungiAlFile(string fileRelativo, string? nome = null, string? tipo = null, string? codice = null)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord { RecordDelModello.Count: > 0 } file)
            return false;

        // Un punto VFR sta in ordine di codice (slice 16c): il vicino si cerca per codice, quello detto o quello proposto.
        if (nome is not null && ChiedeIlCodice(fileRelativo))
            codice = string.IsNullOrWhiteSpace(codice) ? CodiceProposto(fileRelativo, null) : codice.Trim();
        int modello = nome is not null && NomeDelRecordNuovo(fileRelativo) is not null
            ? OrdineAlfabetico.IlVicino(file, codice ?? nome)
            : file.RecordDelModello.Count - 1;
        return AggiungiRecord(fileRelativo, modello, NomeDelRecordNuovo(fileRelativo) is null ? null : nome, tipo, codice);
    }

    /// <summary>Il campo che il nuovo record di quel file chiede per primo (il nome), o null se non ne chiede.</summary>
    public string? NomeDelRecordNuovo(string fileRelativo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord { RecordDelModello.Count: > 0 } file
            ? OrdineAlfabetico.CampoDelNome(file.RecordDelModello[0])
            : null;

    // --- una riga scritta a mano (chiesta dal committente il 23 settembre) ---------------------------------------

    /// <summary>Le righe del file com'è adesso, modifiche comprese: il testo da cui parte la riga scritta a mano.</summary>
    public IReadOnlyList<string> RigheDiAdesso(string fileRelativo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file
            ? file.RigheDelFile(Modifiche.SporchiDi(fileRelativo))
            : [];

    // --- la pagina Testo (riordino, D12, D13, D21) -------------------------------------------------------------------

    /// <summary>Il file aperto nella pagina Testo (relativo alla radice), o nessuno.</summary>
    public string? FileDelTesto { get; private set; }

    /// <summary>I file del sector, per l'albero del Testo: tutti, in ordine di cartella e di nome.</summary>
    public IReadOnlyList<string> FileDelSector =>
        Sessione is { } s ? [.. s.File.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)] : [];

    /// <summary>I file che hanno modifiche in sospeso: nell'albero del Testo si segnano.</summary>
    public IReadOnlySet<string> FileConModifiche => Modifiche.Voci.Select(v => v.File).ToHashSet(StringComparer.Ordinal);

    /// <summary>La riga dove portare il cursore del Testo (T3: dalla Scheda), finché la pagina non la prende.</summary>
    public int? RigaDelTesto { get; private set; }

    /// <summary>Apre un file nel Testo e ci va (T3: dalla Scheda, dal Confronti), alla riga data se c'è.</summary>
    public void ApriNelTesto(string fileRelativo, int? riga = null)
    {
        if (Sessione?.File.ContainsKey(fileRelativo) != true)
            return;
        FileDelTesto = fileRelativo;
        RigaDelTesto = riga;
        if (Pagina == PaginaDelLab.Testo)
            Avvisa();
        else
            ScegliLaPagina(PaginaDelLab.Testo);
    }

    /// <summary>La riga dove portare il cursore, una volta sola (la pagina Testo la prende quando ha il file).</summary>
    public int? PrendiLaRigaDelTesto()
    {
        var riga = RigaDelTesto;
        RigaDelTesto = null;
        return riga;
    }

    /// <summary>La prima riga (da 1) di un record nel file com'è adesso, o null (T3: la Scheda apre il Testo lì).</summary>
    public int? RigaDelRecord(string fileRelativo, int record) =>
        Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord conRecord
        && conRecord.PostiDeiRecord(Modifiche.SporchiDi(fileRelativo)) is var posti && record >= 0 && record < posti.Count
            ? posti[record].Da + 1
            : null;

    /// <summary>
    /// «Sulla mappa» dal Testo (T3): il record che ha la riga <paramref name="riga"/> (da 1) del file com'è adesso diventa
    /// la scelta, e si va alla mappa del sector. Rifiuta una riga che non è di un record (un commento, una riga vuota).
    /// </summary>
    public bool MostraSullaMappa(string fileRelativo, int riga)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord conRecord)
            return Rifiuta("Questo file il motore non lo interpreta: non ha record sulla mappa.");
        var posti = conRecord.PostiDeiRecord(Modifiche.SporchiDi(fileRelativo));
        for (int i = 0; i < posti.Count; i++)
        {
            if (riga < posti[i].Da + 1 || riga > posti[i].Da + posti[i].Quante)
                continue;
            ScegliLaPagina(PaginaDelLab.Sector);
            Scegli(fileRelativo, i);
            return true;
        }
        return Rifiuta($"La riga {riga} non è di un record (un commento, una riga vuota, un tag): non c'è niente da mostrare.");
    }

    /// <summary>
    /// Il testo scritto nel Testo e non applicato, quando l'AOD lascia la pagina (T3): la pagina lo riprende tornando su quel
    /// file. <see cref="BozzaDelTesto.Base"/> è il file com'era quando l'AOD ha cominciato a scrivere: se nel frattempo un
    /// gesto lo ha cambiato, la pagina lo dice.
    /// </summary>
    public BozzaDelTesto? Bozza { get; set; }

    public sealed record BozzaDelTesto(string File, string Base, string Testo);

    /// <summary>
    /// Il testo di un file per la pagina Testo: com'è adesso, modifiche dei gesti comprese, e se si può scrivere. Un file
    /// che il motore non interpreta si legge dal disco e si guarda soltanto (<paramref name="perche"/> dice perché).
    /// </summary>
    public IReadOnlyList<string>? TestoDelFile(string fileRelativo, out bool modificabile, out string? perche)
    {
        modificabile = false;
        perche = null;
        if (Sessione is not { } sessione || !sessione.File.TryGetValue(fileRelativo, out var file))
            return null;
        if (file is IFileConRecord)
        {
            modificabile = true;
            return RigheDiAdesso(fileRelativo);
        }
        perche = "Questo file il motore non lo interpreta: nel Testo si guarda soltanto.";
        try
        {
            var percorso = Path.Combine(sessione.Cartella.Radice, fileRelativo.Replace('/', Path.DirectorySeparatorChar));
            return Vipi.Sectorfile.IO.SectorFileReader.Decode(File.ReadAllBytes(percorso)).Lines;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("testo", e);
            perche = $"Il file non si legge: {e.Message}";
            return [];
        }
    }

    /// <summary>
    /// «Scrivi» (D12): il testo scritto nel Testo diventa una modifica in sospeso del file, come le altre — diff, Annulla,
    /// problemi nuovi, «Salva». Si cambiano solo le righe toccate (<see cref="Core.Testo.RigheCambiate"/>). Le righe dei
    /// metadati <c>//@</c> sono bloccate (D13): si cambiano solo con <paramref name="metadatiSbloccati"/>, e allora il file
    /// riletto deve avere i tag validi come prima.
    /// <para>T2 (D13, «cancellare a mano un record che ha metadati chiede prima»): se il testo non ha più un record che aveva
    /// i metadati, si rifiuta finché l'AOD non dice <paramref name="togliAncheIMetadati"/>; allora si tolgono anche i suoi
    /// tag (la dichiarazione e il suo blocco), anche con i metadati bloccati.</para>
    /// </summary>
    public bool ApplicaIlTesto(string fileRelativo, string testo, bool metadatiSbloccati, bool togliAncheIMetadati = false)
    {
        ArgumentNullException.ThrowIfNull(testo);
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord)
            return Rifiuta("Questo file il motore non lo interpreta: nel Testo si guarda soltanto.");
        var prima = RigheDiAdesso(fileRelativo);
        IReadOnlyList<string> dopo = RigheDelTesto(testo);
        var tolti = Core.Testo.MetadatiNelTesto.Tolti(prima, dopo);
        var primaSenza = prima;
        if (tolti.Count > 0)
        {
            if (!togliAncheIMetadati)
                return Rifiuta($"{string.Join(", ", tolti)}: {(tolti.Count == 1 ? "ha" : "hanno")} i metadati //@, che resterebbero senza record. «Togli anche i metadati», o rimetti le righe.");
            dopo = Core.Testo.MetadatiNelTesto.SenzaIMetadatiDi(dopo, tolti);
            primaSenza = Core.Testo.MetadatiNelTesto.SenzaIMetadatiDi(prima, tolti);
        }
        static bool EUnTag(string riga) => Vipi.Sectorfile.IO.Metadati.EUnTag(riga.TrimStart());
        var sostituzioni = Core.Testo.RigheCambiate.Di(prima, dopo, EUnTag);
        if (sostituzioni.Count == 0)
            return Rifiuta("Il testo è quello del file: non c'è niente da applicare.");
        // D13: bloccati, i tag devono essere gli stessi e nello stesso ordine (una riga nuova fra due tag va bene; quelli
        // dei record tolti se ne vanno con loro). Il motore poi rilegge il file e vuole i tag validi come prima.
        if (!metadatiSbloccati && !primaSenza.Where(EUnTag).SequenceEqual(dopo.Where(EUnTag)))
            return Rifiuta("Le righe //@ sono i metadati del Lab e sono bloccate: per cambiarle, «Sblocca i metadati».");
        bool tagBuoni = ((IFileConRecord)Sessione!.File[fileRelativo]).TagRotti() is null;
        return NellaStoria($"{NomeDelFile(fileRelativo)} scritto nel Testo",
            () => GestoSulTesto(fileRelativo, "testo", f => Modifiche.CambiaRighe(f, sostituzioni,
                verifica: riletto => !tagBuoni || riletto.TagRotti() is not { } rotti ? null : $"Riletto, i metadati //@ non tornano: {rotti}",
                anchiITag: true)));
    }

    private static string[] RigheDelTesto(string testo) =>
        testo.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    /// <summary>
    /// I record coi metadati che il testo scritto non ha più (T2, D13): la pagina lo chiede prima di «Scrivi». Vuoto se
    /// non ce ne sono.
    /// </summary>
    public IReadOnlyList<string> RecordConMetadatiTolti(string fileRelativo, string testo)
    {
        ArgumentNullException.ThrowIfNull(testo);
        return Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord
            ? Core.Testo.MetadatiNelTesto.Tolti(RigheDiAdesso(fileRelativo), RigheDelTesto(testo))
            : [];
    }

    /// <summary>Un problema del validatore su una riga del file aperto nel Testo (T2).</summary>
    public sealed record ProblemaDelTesto(int Riga, Gravita Gravita, string Testo);

    /// <summary>
    /// I problemi del validatore sulle righe di un file, per il Testo (T2): quelli dell'albero se il file non ha modifiche
    /// (i numeri di riga sono quelli del disco, che è com'è adesso); se le ha, quelli che le modifiche porterebbero, coi
    /// numeri del file modificato (<paramref name="soloNuovi"/>): quelli di prima avrebbero le righe spostate.
    /// </summary>
    public IReadOnlyList<ProblemaDelTesto> ProblemiDelTesto(string fileRelativo, out bool soloNuovi)
    {
        soloNuovi = FileConModifiche.Contains(fileRelativo);
        var problemi = soloNuovi ? ProblemiDelleModifiche : ProblemiDellAlbero;
        return [.. problemi.Where(p => p.File == fileRelativo && p.Problema.Riga > 0)
            .Select(p => new ProblemaDelTesto(p.Problema.Riga, p.Gravita, $"{p.Problema.Regola}: {p.Problema.Dettaglio}"))];
    }

    /// <summary>
    /// Dove è dichiarato un nome (T2, Ctrl+clic nel Testo): il file del catalogo del master scelto e la riga del suo record
    /// nel file com'è adesso. Null se il catalogo non lo conosce.
    /// </summary>
    public (string File, int Riga)? DefinizioneDi(string nome)
    {
        if (Sessione is not { } sessione || CatalogoScelto?.Cerca(nome) is not { } punto
            || sessione.File.GetValueOrDefault(punto.File) is not IFileConRecord conRecord)
            return null;
        var posti = conRecord.PostiDeiRecord(Modifiche.SporchiDi(punto.File));
        for (int i = 0; i < conRecord.RecordDelModello.Count && i < posti.Count; i++)
        {
            var r = conRecord.RecordDelModello[i];
            var suo = OrdineAlfabetico.Nome(r) is { Length: > 0 } n ? n
                : r is Vipi.Sectorfile.Models.AirportInfo a ? a.IcaoCode : Vipi.Sectorfile.IO.Metadati.NomeDelRecord(r);
            if (string.Equals(suo?.Trim(), punto.Nome, StringComparison.OrdinalIgnoreCase))
                return (punto.File, posti[i].Da + 1);
        }
        return (punto.File, 1);
    }

    /// <summary>
    /// Scrive a mano la riga numero <paramref name="numero"/> (confermata dall'AOD) e rilegge il file. Se la riga ora
    /// appartiene a un record, la scheda va su di lui: chi correggeva una riga illeggibile vede il record che ne è uscito.
    /// </summary>
    public bool CambiaRigaAMano(string fileRelativo, int numero, string testo)
        => NellaStoria($"riga {numero} di {NomeDelFile(fileRelativo)} scritta a mano", () => CambiaRigaAManoAdesso(fileRelativo, numero, testo));

    private bool CambiaRigaAManoAdesso(string fileRelativo, int numero, string testo)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        var esito = Modifiche.CambiaRiga(file, numero, testo);
        Registro.Scrivi("a mano", $"{fileRelativo}:{numero} «{testo}»: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDelTesto)
        {
            RifaiLaGeometria(fileRelativo);
            if (file is IFileConRecord conRecord && conRecord.RecordDellaRiga(numero) is { } record)
            {
                Scelta = (fileRelativo, record);
                RigaSegnalata = (fileRelativo, numero);
            }
            else if (Scelta is { } scelta && scelta.File == fileRelativo && scelta.Record >= file.Record)
            {
                Scelta = null;
            }
        }

        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDelTesto;
    }

    /// <summary>
    /// Scrive la correzione che il validatore propone per un problema (lotto «Subito» slice 2c: coordinate scritte male)
    /// al posto della sua riga, come una riga scritta a mano. La riga si cerca nel file di ADESSO; se nel frattempo è
    /// cambiata, la correzione non si scrive: era per quella di prima.
    /// </summary>
    public bool Correggi(ProblemaNelLab problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        if (problema.Problema.Proposta is not { } proposta)
        {
            Rifiuto = "Per questo problema non c'è una correzione proposta.";
            Avvisa();
            return false;
        }

        int? riga = ProblemiDellAlbero.Contains(problema)
            ? RigaDiAdesso(problema.File, problema.Problema.Riga)
            : problema.Problema.Riga;
        var adesso = RigheDiAdesso(problema.File);
        if (riga is not { } numero || numero < 1 || numero > adesso.Count || adesso[numero - 1] != problema.Problema.Testo)
        {
            Rifiuto = $"La riga {problema.Problema.Riga} è già cambiata fra le modifiche in sospeso: la correzione era per quella di prima.";
            Avvisa();
            return false;
        }

        return NellaStoria($"riga {numero} di {NomeDelFile(problema.File)} corretta ({problema.Problema.Regola})",
            () => CambiaRigaAManoAdesso(problema.File, numero, proposta));
    }

    /// <summary>
    /// Quanti problemi della stessa regola, nello stesso file, hanno una correzione proposta (slice 11b: «arrotonda al grado»
    /// su riga o su file, M4).
    /// </summary>
    /// <remarks>Zero per tutti tranne il primo del gruppo: il gesto si offre una volta sola, non sotto ognuna delle 44 voci.</remarks>
    public int ConLaStessaCorrezione(ProblemaNelLab problema)
    {
        var stessi = ProblemiDellAlbero.Where(p => p.File == problema.File && p.Problema.Regola == problema.Problema.Regola && p.Problema.Proposta is not null).ToList();
        return stessi.Count > 0 && ReferenceEquals(stessi[0], problema) ? stessi.Count : 0;
    }

    /// <summary>
    /// Applica le correzioni proposte di tutti i problemi della stessa regola nello stesso file, in una voce sola della
    /// storia (slice 11b, M4). Le righe già cambiate fra le modifiche in sospeso restano come sono, e lo dice.
    /// </summary>
    public bool CorreggiTutte(ProblemaNelLab problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        var stessi = ProblemiDellAlbero.Where(p => p.File == problema.File && p.Problema.Regola == problema.Problema.Regola
                                                    && p.Problema.Proposta is not null).ToList();
        if (stessi.Count == 0)
        {
            Rifiuto = "Per questo problema non c'è una correzione proposta.";
            Avvisa();
            return false;
        }

        int saltate = 0;
        bool fatto = NellaStoria($"{stessi.Count} righe di {NomeDelFile(problema.File)} corrette ({problema.Problema.Regola})", () =>
        {
            bool almenoUna = false;
            // Una riga con più problemi della stessa regola si corregge una volta.
            foreach (var p in stessi.DistinctBy(p => p.Problema.Riga))
            {
                var adesso = RigheDiAdesso(p.File);
                if (RigaDiAdesso(p.File, p.Problema.Riga) is not { } numero || numero < 1 || numero > adesso.Count
                    || adesso[numero - 1] != p.Problema.Testo)
                {
                    saltate++;
                    continue;
                }

                almenoUna |= CambiaRigaAManoAdesso(p.File, numero, p.Problema.Proposta!);
            }

            return almenoUna;
        });
        if (saltate > 0)
        {
            Rifiuto = $"{saltate} righe erano già cambiate fra le modifiche in sospeso: quelle restano come sono.";
            Avvisa();
        }

        return fatto;
    }

    // --- le voci della selezione e le parti, accese e spente (lotto «Subito» slice 6) -----------------------------

    private readonly Dictionary<string, (IReadOnlyList<string> Righe, IReadOnlyList<VoceDellaSelezione>? Voci)> _voci = [];

    /// <summary>
    /// Le voci della finestra di selezione del file (A3, J1, B1, E1, H3), o null se il file non ne ha. Si rifanno quando
    /// il testo del file cambia.
    /// </summary>
    public IReadOnlyList<VoceDellaSelezione>? VociDi(string fileRelativo)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file)
            return null;
        var righe = RigheDiAdesso(fileRelativo);
        if (_voci.TryGetValue(fileRelativo, out var tenute) && tenute.Righe.SequenceEqual(righe, StringComparer.Ordinal))
            return tenute.Voci;

        var voci = VociDellaSelezione.Di((FileAperto)file, righe, file.PostiDeiRecord(Modifiche.SporchiDi(fileRelativo)));
        _voci[fileRelativo] = (righe, voci);
        return voci;
    }

    // --- la stessa forma (lotto «Subito» slice 8, «file per file» D5, I2) ----------------------------------------------

    // L'indice delle forme si rifà quando cambiano gli strati (una modifica rifà le forme del suo file): costruirlo costa
    // un paio di decimi di secondo sull'albero vero, e la scheda si ridisegna a ogni tasto.
    private (IReadOnlyList<StratoDellaMappa> Strati, FormeUguali Indice)? _formeUguali;

    /// <summary>
    /// Le parti del record che hanno la stessa forma in un altro record (D5: confronto come anello), con le copie uguali
    /// e quelle simili. Vuoto se non ne ha, o se il record non si disegna.
    /// </summary>
    public IReadOnlyList<(ParteDiForma Parte, IReadOnlyList<CopiaDellaForma> Copie)> StessaFormaDi(string fileRelativo, int record)
    {
        if (Sessione is null)
            return [];
        if (_formeUguali is not { } fatto || !ReferenceEquals(fatto.Strati, Strati))
            _formeUguali = fatto = (Strati, FormeUguali.Di(Strati));
        return fatto.Indice.Di(fileRelativo, record);
    }

    /// <summary>
    /// Dove va il confine di un settore dinamico (slice 13e, J5: aerovia → <c>HI_AIRSPACE</c>, avvicinamento →
    /// <c>LOW_AIRSPACE</c>) e i confini che hanno la sua forma; null se il record non è un settore di quei tipi.
    /// </summary>
    public PostoDelSettore? PostoDelConfineDi(string fileRelativo, int record)
    {
        if (Sessione is null)
            return null;
        if (_formeUguali is not { } fatto || !ReferenceEquals(fatto.Strati, Strati))
            _formeUguali = fatto = (Strati, FormeUguali.Di(Strati));
        return PostoDelConfine.Di(Sessione, fatto.Indice, fileRelativo, record);
    }

    // Le famiglie dichiarate (slice 8b) si rileggono quando cambiano gli strati o un metadato: leggere i tag di tutti i
    // file costa qualche decina di millisecondi, troppo per ogni disegno della scheda.
    private (IReadOnlyList<StratoDellaMappa> Strati, string Firma, IReadOnlyList<FamigliaDichiarata> Famiglie)? _famiglie;

    /// <summary>Le famiglie di forme dichiarate col tag <c>form=</c> (slice 8b), come sono adesso.</summary>
    public IReadOnlyList<FamigliaDichiarata> FamiglieDichiarate()
    {
        if (Sessione is null)
            return [];
        string firma = string.Join("|", Modifiche.Tutte.OfType<ModificaDelMetadato>()
            .Select(m => $"{m.File}#{m.Record}:{m.Chiave}={m.Dopo}").Order(StringComparer.Ordinal));
        if (_famiglie is not { } fatte || !ReferenceEquals(fatte.Strati, Strati) || fatte.Firma != firma)
        {
            if (_formeUguali is not { } indice || !ReferenceEquals(indice.Strati, Strati))
                _formeUguali = indice = (Strati, FormeUguali.Di(Strati));
            _famiglie = fatte = (Strati, firma, Core.Copie.FamiglieDichiarate.Di(Sessione, indice.Indice));
        }

        return fatte.Famiglie;
    }

    /// <summary>La famiglia che il record dichiara (<c>form=</c>), o null.</summary>
    public FamigliaDichiarata? FamigliaDi(string fileRelativo, int record)
        => Sessione is null ? null : Core.Copie.FamiglieDichiarate.Del(FamiglieDichiarate(), Sessione, fileRelativo, record);

    /// <summary>
    /// Scrive <c>form=NOME</c> sul record e sulle copie date (slice 8b, D5): dichiara la famiglia, o ci aggiunge delle
    /// copie. Un gesto solo nella storia; un record che il tag non lo può portare lo dice il rifiuto, gli altri si scrivono.
    /// </summary>
    public bool ScriviLaFamiglia(string fileRelativo, int record, string? nome, IReadOnlyList<(string File, int Record)> anche)
        => NellaStoria($"famiglia di forme {nome?.Trim()} di {EtichettaDi(fileRelativo, record)}",
            () => ScriviLaFamigliaAdesso(fileRelativo, record, nome, anche));

    private bool ScriviLaFamigliaAdesso(string fileRelativo, int record, string? nome, IReadOnlyList<(string File, int Record)> anche)
    {
        if (Sessione is null)
            return false;
        string pulito = (nome ?? "").Trim();
        if (pulito.Length == 0)
        {
            Rifiuto = "Serve il nome della famiglia.";
            Avvisa();
            return false;
        }

        int scritti = 0;
        var rifiuti = new List<string>();
        foreach (var (file, indice) in new[] { (fileRelativo, record) }.Concat(anche))
        {
            if (!Sessione.File.TryGetValue(file, out var aperto))
                continue;
            if (FamigliaDi(file, indice)?.Nome == pulito)
                continue;
            var esito = Modifiche.CambiaIlMetadato(aperto, indice, Core.Copie.FamiglieDichiarate.Chiave, pulito, EtichettaDi(file, indice));
            Registro.Scrivi("modifica", $"{file}#{indice} tag form = «{pulito}»: {Descrivi(esito)}");
            if (esito is ModificaDelMetadato)
                scritti++;
            else if (esito is ModificaRifiutata rifiutata)
                rifiuti.Add($"{NomeDelFile(file)} {EtichettaDi(file, indice)}: {rifiutata.Motivo}");
        }

        Rifiuto = rifiuti.Count > 0 ? "Famiglia non scritta in " + string.Join("; ", rifiuti) : null;
        RicontrollaLeModifiche();
        Avvisa();
        return scritti > 0;
    }

    // --- «chi lo usa» (lotto «Subito» slice 7, «file per file» L2) -----------------------------------------------------

    private ChiLoUsa? _chiLoUsa;

    // Le risposte già date, finché un file non cambia: la scheda si ridisegna a ogni tasto, e rileggere le righe dei
    // file che citano un VOR (decine) ogni volta si sentirebbe.
    private readonly Dictionary<(string File, int Record), UsiDelPunto?> _usi = [];

    /// <summary>
    /// Chi usa il record, se è un punto che si cita per nome (fix, VOR, NDB, scalo, punto VFR, attesa): le righe dei file
    /// che lo citano e che Aurora risolve in lui, nel file com'è adesso. Null per gli altri record.
    /// </summary>
    public UsiDelPunto? UsiDi(string fileRelativo, int record)
    {
        if (Sessione is null || _chiLoUsa is null)
            return null;
        if (!_usi.TryGetValue((fileRelativo, record), out var usi))
        {
            usi = _chiLoUsa.Di(Sessione, Cataloghi, fileRelativo, record, Modifiche.SporchiDi);
            _usi[(fileRelativo, record)] = usi;
        }

        return usi;
    }

    /// <summary>
    /// Rinomina il punto (lotto «Subito» slice 7b, L2): la sua dichiarazione, le sue copie, e tutte le righe che lo
    /// citano, in una voce sola (più diff). Con un VOR e un NDB omonimi <paramref name="omonimi"/> dice se le righe che
    /// valgono per tutti e due si rinominano: finché non è deciso, la rinomina non si fa e <see cref="DaDecidere"/> lo
    /// chiede (committente, 28 settembre).
    /// </summary>
    public bool RinominaIlPunto(string fileRelativo, int record, string vecchio, string? nuovo, bool? omonimi = null)
    {
        DaDecidere = null;
        DaCambiareAMano = [];
        return NellaStoria($"{vecchio} rinominato {nuovo}", () =>
        {
            if (Sessione is null || _chiLoUsa is null)
                return false;
            ScordaGliUsi();
            var esito = Rinomina.Prepara(Sessione, _chiLoUsa, Cataloghi, fileRelativo, record, vecchio, nuovo, omonimi, Modifiche.SporchiDi);
            if (esito is RinominaDaDecidere domanda)
            {
                DaDecidere = domanda;
                Rifiuto = null;
                Avvisa();
                return false;
            }

            if (esito is not RinominaPronta pronta)
            {
                Rifiuto = (esito as ModificaRifiutata)?.Motivo;
                Avvisa();
                return false;
            }

            var fatto = Modifiche.CambiaInPiuFile(
                [.. pronta.PerFile.Select(f => (Sessione.File[f.File], f.Righe))],
                $"rinomina {pronta.Vecchio} → {pronta.Nuovo} ({pronta.Righe} righe in {pronta.PerFile.Count} file)");
            Registro.Scrivi("rinomina", $"{fileRelativo}#{record} {pronta.Vecchio} → {pronta.Nuovo}: {Descrivi(fatto)}");
            Rifiuto = fatto is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
            if (fatto is not ModificaDelTesto)
            {
                Avvisa();
                return false;
            }

            // Quel che il Lab non riscrive (i PAR dei .cpr, i commenti dei disegni): resta da dire, sotto la scheda.
            DaCambiareAMano = pronta.AMano;
            // I nomi sono cambiati: i cataloghi si rifanno, poi la geometria dei file toccati (i punti per nome).
            RifaiICataloghi();
            foreach (var (toccato, _) in pronta.PerFile)
                RifaiLaGeometria(toccato);
            RigaSegnalata = null;
            RicontrollaLeModifiche();
            Avvisa();
            return true;
        });
    }

    /// <summary>
    /// Le righe che citavano il nome e che l'ultima rinomina non ha potuto riscrivere (slice 7d: i PAR dei .cpr, i
    /// commenti dei disegni di una pista): vanno cambiate a mano.
    /// </summary>
    public IReadOnlyList<Citazione> DaCambiareAMano { get; private set; } = [];

    /// <summary>La domanda di una rinomina ferma (VOR e NDB omonimi): null se non ce n'è.</summary>
    public RinominaDaDecidere? DaDecidere { get; private set; }

    /// <summary>I cataloghi dei master, rifatti dai record di adesso: dopo una rinomina, o un annulla che la toglie.</summary>
    private void RifaiICataloghi()
    {
        if (Sessione is null)
            return;
        Cataloghi = CatalogoDeiPunti.PerOgniIsc(Sessione);
        ScordaGliUsi();
    }

    // Le risposte per file (slice 7e): chi usa un file, chi usa i colori di un .def. Si scordano con quelle dei record.
    private readonly Dictionary<string, IReadOnlyList<Citazione>> _usiDeiFile = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<UsoDelColore>?> _colori = new(StringComparer.Ordinal);

    private void ScordaGliUsi()
    {
        _usi.Clear();
        _usiDeiFile.Clear();
        _colori.Clear();
    }

    /// <summary>
    /// Chi usa il FILE (lotto «Subito» slice 7e, R-1): gli .isc che lo caricano (e come), i .frq che lo citano come
    /// profilo, ATIS o D-ATIS.
    /// </summary>
    public IReadOnlyList<Citazione> UsiDelFile(string fileRelativo)
    {
        if (Sessione is null)
            return [];
        if (!_usiDeiFile.TryGetValue(fileRelativo, out var usi))
            _usiDeiFile[fileRelativo] = usi = ChiUsaIlFile.Di(Sessione, fileRelativo, Modifiche.SporchiDi);
        return usi;
    }

    /// <summary>I nomi di un colors.def e chi li usa (slice 7e); null per gli altri file.</summary>
    public IReadOnlyList<UsoDelColore>? ColoriDi(string fileRelativo)
    {
        if (Sessione is null)
            return null;
        if (!_colori.TryGetValue(fileRelativo, out var colori))
            _colori[fileRelativo] = colori = ChiUsaIlFile.Colori(Sessione, fileRelativo);
        return colori;
    }

    /// <summary>Il clic su una citazione: il suo record nella scheda e sulla mappa, la sua riga segnata.</summary>
    public void VaiAllaCitazione(Citazione citazione)
    {
        ArgumentNullException.ThrowIfNull(citazione);
        if (Sessione?.File.ContainsKey(citazione.File) != true)
            return;
        // Una riga fuori da un record (un .cpr, un commento): l'ispettore mostra le righe intorno, come per un problema.
        if (citazione.Record < 0)
        {
            Scelta = null;
            FileScelto = citazione.File;
        }
        else
        {
            Scegli(citazione.File, citazione.Record);
        }

        RigaSegnalata = citazione.Riga > 0 ? (citazione.File, citazione.Riga) : null;
        Avvisa();
    }

    /// <summary>Le chiavi della mappa di una voce: le sue parti, o i suoi record se non ne ha.</summary>
    public static IReadOnlyList<string> PartiDi(VoceDellaSelezione voce)
    {
        ArgumentNullException.ThrowIfNull(voce);
        return voce.Parti.Count > 0
            ? [.. voce.Parti.Select(p => p.Chiave)]
            : [.. voce.Record.Select(r => r.ToString(System.Globalization.CultureInfo.InvariantCulture))];
    }

    /// <summary>La voce che contiene il record, o null.</summary>
    public VoceDellaSelezione? VoceDi(string fileRelativo, int record)
        => VociDi(fileRelativo)?.FirstOrDefault(v => v.Record.Contains(record));

    /// <summary>
    /// Cambia il nome di una parte (H3): il commento sopra; nelle zone MVA col soprannome del blocco (E1), il
    /// metadato <c>zone</c>.
    /// </summary>
    public bool CambiaIlNomeDellaParte(string fileRelativo, ParteDellaVoce parte, string? nuovo)
    {
        ArgumentNullException.ThrowIfNull(parte);
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file && parte.RigaDelNome is null
            && file.RecordDelModello[parte.Record] is Vipi.Sectorfile.Models.MvaSector
            && file.ChiaviDi(parte.Record)?.ContainsKey("zone") == true)
            return CambiaIlMetadato(fileRelativo, parte.Record, "zone", nuovo);

        return NellaStoria($"nome di una parte di {NomeDelFile(fileRelativo)} cambiato",
            () => GestoSulTesto(fileRelativo, "nome", f => Modifiche.CambiaIlNome(f, parte.RigaDelNome, parte.PrimaRiga, nuovo,
                EtichetteDi(fileRelativo).ElementAtOrDefault(parte.Record) ?? "")));
    }

    /// <summary>Cambia il nome di una voce che lo prende dal commento sopra (i gruppi dei .geo e dei .pol, H3).</summary>
    public bool CambiaIlNomeDellaVoce(string fileRelativo, VoceDellaSelezione voce, string? nuovo)
    {
        ArgumentNullException.ThrowIfNull(voce);
        if (voce.PrimaRiga is not { } prima)
        {
            Rifiuto = "Il nome di questa voce non è un commento: si cambia nelle righe del record.";
            Avvisa();
            return false;
        }

        bool fatto = NellaStoria($"nome del gruppo «{voce.Nome}» di {NomeDelFile(fileRelativo)} cambiato",
            () => GestoSulTesto(fileRelativo, "nome", f => Modifiche.CambiaIlNome(f, voce.RigaDelNome, prima, nuovo)));
        // La scheda resta sul record di prima: il commento nuovo non cambia i numeri dei record.
        if (fatto && Scelta is null)
            Scegli(fileRelativo, voce.Record[0]);
        return fatto;
    }

    /// <summary>Vero se il nome della voce sta nelle righe di dati e si rinomina (slice 7f): confini, MVA, aerovie, aree.</summary>
    public bool SiRinominaLaVoce(string fileRelativo, VoceDellaSelezione voce)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file && RinominaDellaVoce.Tipo(file, voce) is not null;

    /// <summary>
    /// Rinomina una voce il cui nome sta nelle righe di dati (lotto «Subito» slice 7f): il 2° campo dei confini, delle
    /// MVA e delle aerovie (e le parole delle etichette delle aerovie, il 5° campo delle MVA), il 6° delle aree P/R/D,
    /// nel suo file, righe nascoste e tag del blocco compresi. Una voce nel testo del file.
    /// </summary>
    public bool RinominaLaVoce(string fileRelativo, VoceDellaSelezione voce, string? nuovo)
    {
        ArgumentNullException.ThrowIfNull(voce);
        string testo = (nuovo ?? "").Trim();
        return NellaStoria($"voce «{voce.Nome}» di {NomeDelFile(fileRelativo)} rinominata «{testo}»",
            () => GestoSulTesto(fileRelativo, "rinomina", f =>
            {
                if (f is not IFileConRecord conRecord || RinominaDellaVoce.Tipo(conRecord, voce) is not { } tipo)
                    return new ModificaRifiutata("Il nome di questa voce non sta nelle righe di dati.");
                var altre = (VociDi(fileRelativo) ?? []).Where(v => !string.Equals(v.Nome, voce.Nome, StringComparison.OrdinalIgnoreCase));
                if (RinominaDellaVoce.PercheNonVa(testo, tipo, altre) is { } perche)
                    return new ModificaRifiutata(perche);
                if (string.Equals(testo, voce.Nome, StringComparison.Ordinal))
                    return new ModificaRifiutata("Il nome è già questo.");
                var sostituzioni = RinominaDellaVoce.Sostituzioni(RigheDiAdesso(fileRelativo), tipo, voce.Nome, testo);
                if (sostituzioni.Count == 0)
                    return new ModificaRifiutata("Non trovo il nome della voce nelle righe del file.");
                int quanti = conRecord.RecordDelModello.Count;
                bool tagBuoni = conRecord.TagRotti() is null;
                return Modifiche.CambiaRighe(f, sostituzioni, $"voce «{voce.Nome}» → «{testo}» ({sostituzioni.Count} righe)",
                    riletto => riletto.RecordDelModello.Count == quanti && (!tagBuoni || riletto.TagRotti() is null)
                        ? null
                        : "Col nome nuovo il file non si rilegge coi record di prima: si fa a mano, dalle righe del file.",
                    anchiITag: true);
            }));
    }

    private HashSet<string> _spenti => MappaAttiva.Spenti;

    /// <summary>Le forme spente sulla mappa dello spazio attivo: <c>file#3</c> un record, <c>file#3.1</c> un poligono.</summary>
    public IReadOnlyCollection<string> Spenti => _spenti;

    public int VersioneDeiSpenti
    {
        get => MappaAttiva.VersioneDeiSpenti;
        private set => MappaAttiva.VersioneDeiSpenti = value;
    }

    // --- la vista per scalo (lotto «Subito» slice 12d, I6, O1) -----------------------------------------------------

    /// <summary>Lo scalo scelto nella vista per scalo, o null.</summary>
    public string? ScaloScelto { get; private set; }

    /// <summary>Gli scali fra cui scegliere: quelli degli <c>.ap</c> dell'albero.</summary>
    public IReadOnlyList<ScaloDellaVista> ScaliDellaVista()
    {
        if (Sessione is null)
            return [];
        if (_scali is null || !ReferenceEquals(_scaliDi, Strati) || _scaliAlla != _versioneDeiFile)
            (_scali, _scaliDi, _scaliAlla) = (VistaPerScalo.Scali(Sessione), Strati, _versioneDeiFile);
        return _scali;
    }

    /// <summary>Sceglie lo scalo della vista (null: nessuno).</summary>
    public void ScegliLoScalo(string? icao)
    {
        ScaloScelto = string.IsNullOrWhiteSpace(icao) ? null : icao.Trim().ToUpperInvariant();
        Registro.Scrivi("scalo", ScaloScelto ?? "nessuno");
        Avvisa();
    }

    /// <summary>
    /// Tutto quello che riguarda lo scalo scelto, da qualunque file, in sezioni. Si tiene finché i file non cambiano:
    /// le marcature per pista rileggono le righe dei <c>.geo</c>, e la pagina si ridisegna a ogni scelta.
    /// </summary>
    public IReadOnlyList<SezioneDelloScalo> VistaDelloScalo()
    {
        if (Sessione is null || ScaloScelto is null)
            return [];
        if (_vista is null || _vistaDi != (ScaloScelto, _versioneDeiFile) || !ReferenceEquals(_vistaCon, Strati))
            (_vista, _vistaDi, _vistaCon) = (VistaPerScalo.Di(Sessione, ScaloScelto), (ScaloScelto, _versioneDeiFile), Strati);
        return _vista;
    }

    private IReadOnlyList<ScaloDellaVista>? _scali;
    private object? _scaliDi;
    private int _scaliAlla;
    private IReadOnlyList<SezioneDelloScalo>? _vista;
    private (string Scalo, int Versione) _vistaDi;
    private object? _vistaCon;

    /// <summary>Cresce a ogni file rifatto (<see cref="RifaiLaGeometria"/>): chi tiene qualcosa calcolato dai file lo butta.</summary>
    private int _versioneDeiFile;

    // --- i generi di uno strato (lotto «Subito» slice 12c, H1) -----------------------------------------------------

    /// <summary>
    /// I generi dentro uno strato (assi e bordi delle taxiway, edifici, marcature…; riempimenti, etichette e stand),
    /// con le loro forme; vuoto se lo strato ne ha meno di due.
    /// </summary>
    public IReadOnlyList<GenereDelloStrato> GeneriDi(string strato)
        => StratiDaMostrare.FirstOrDefault(s => s.Id == strato) is { } suo ? GeneriDellaMappa.Di(suo) : [];

    /// <summary>Vero se il genere di quello strato si vede (di base sì: si spegne, non si accende).</summary>
    public bool GenereAcceso(string strato, string genere) => !_spenti.Contains(ChiaveDelGenere(strato, genere));

    /// <summary>
    /// Accende o spegne un genere di uno strato sulla mappa. Passa dalle voci spente (<see cref="Spenti"/>), con la
    /// chiave <c>§strato:genere</c>: la mappa lo toglie senza riprendere le coordinate.
    /// </summary>
    public void AccendiIlGenere(string strato, string genere, bool acceso)
    {
        if (acceso)
            _spenti.Remove(ChiaveDelGenere(strato, genere));
        else
            _spenti.Add(ChiaveDelGenere(strato, genere));
        // Toccare un genere vuol dire guardare il suo strato: se è spento, si accende.
        if (acceso && StratiDaMostrare.Any(s => s.Id == strato))
            _accesi.Add(strato);
        Registro.Scrivi("generi", $"{strato}: {genere} {(acceso ? "acceso" : "spento")}");
        VersioneDeiSpenti++;
        Avvisa();
    }

    private static string ChiaveDelGenere(string strato, string genere) => "§" + GeneriDellaMappa.Chiave(strato, genere);

    /// <summary>Vero se nessuna di quelle parti del file è spenta.</summary>
    public bool Acceso(string fileRelativo, IEnumerable<string> parti)
        => parti.All(p => !_spenti.Contains($"{fileRelativo}#{p}"));

    /// <summary>Accende o spegne sulla mappa quelle parti del file (una voce intera, o una parte sola).</summary>
    public void Accendi(string fileRelativo, IEnumerable<string> parti, bool acceso)
    {
        ArgumentNullException.ThrowIfNull(parti);
        foreach (string parte in parti)
        {
            if (acceso)
                _spenti.Remove($"{fileRelativo}#{parte}");
            else
                _spenti.Add($"{fileRelativo}#{parte}");
        }

        // Spegnere una voce di un file vuol dire vederne lo strato: se è spento, si accende.
        if (StratiDellaMappa.DiFile(fileRelativo) is { } tipo && Strati.Any(s => s.Tipo.Id == tipo.Id))
            _accesi.Add(tipo.Id);
        Registro.Scrivi("voci", $"{fileRelativo}: {(acceso ? "accese" : "spente")} {string.Join(", ", parti.Take(5))} ({_spenti.Count} spente in tutto)");
        VersioneDeiSpenti++;
        Avvisa();
    }

    // --- nascondi e mostra (lotto «Subito» slice 5c) --------------------------------------------------------------

    /// <summary>
    /// I nascosti di ogni file, col testo da cui sono stati calcolati: trovarli costa una rilettura del file, e l'elenco
    /// si ridisegna a ogni clic. Si rifanno quando il testo del file cambia.
    /// </summary>
    private readonly Dictionary<string, (IReadOnlyList<string> Righe, AnalisiDeiNascosti Analisi)> _nascosti = [];

    /// <summary>Quel che il file ha di nascosto: fra gli altri, dentro, in parte.</summary>
    public AnalisiDeiNascosti NascostiDi(string fileRelativo)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not { } file)
            return AnalisiDeiNascosti.Vuota;
        var righe = RigheDiAdesso(fileRelativo);
        if (_nascosti.TryGetValue(fileRelativo, out var tenuti) && tenuti.Righe.SequenceEqual(righe, StringComparer.Ordinal))
            return tenuti.Analisi;

        var analisi = Modifiche.NascostiDi(file);
        _nascosti[fileRelativo] = (righe, analisi);
        return analisi;
    }

    /// <summary>Il nascosto scelto nell'elenco: quello che comincia alla riga segnalata.</summary>
    public BloccoNascosto? NascostoScelto
        => RigaSegnalata is { } segnata ? NascostiDi(segnata.File).Fra.FirstOrDefault(b => b.Riga == segnata.Riga) : null;

    /// <summary>Sceglie un record nascosto fra gli altri: l'ispettore mostra le sue righe e il tasto «Mostra».</summary>
    public void ScegliNascosto(string fileRelativo, BloccoNascosto blocco)
    {
        ArgumentNullException.ThrowIfNull(blocco);
        Scelta = null;
        FileScelto = fileRelativo;
        RigaSegnalata = (fileRelativo, blocco.Riga);
        Registro.Scrivi("scelta", $"{fileRelativo}:{blocco.Riga} nascosto «{blocco.Etichetta}»");
        Avvisa();
    }

    /// <summary>Nasconde il record: una voce nel testo del file.</summary>
    public bool Nascondi(string fileRelativo, int record)
        => NellaStoria($"{EtichettaDi(fileRelativo, record)} nascosto",
            () => GestoSulTesto(fileRelativo, "nascondi", file => Modifiche.Nascondi(file, record, EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "")));

    /// <summary>Mostra le righe nascoste dentro il record (tutte, o quelle commentate).</summary>
    public bool MostraNelRecord(string fileRelativo, int record)
        => NellaStoria($"{EtichettaDi(fileRelativo, record)}: righe nascoste mostrate",
            () => GestoSulTesto(fileRelativo, "mostra", file => Modifiche.MostraNelRecord(file, record, EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "")));

    /// <summary>Mostra un record nascosto fra gli altri: la scheda va su di lui.</summary>
    public bool Mostra(string fileRelativo, BloccoNascosto blocco)
    {
        ArgumentNullException.ThrowIfNull(blocco);
        bool fatto = NellaStoria($"{blocco.Etichetta} di {NomeDelFile(fileRelativo)} mostrato",
            () => GestoSulTesto(fileRelativo, "mostra", file => Modifiche.Mostra(file, blocco)));
        if (fatto)
            Scegli(fileRelativo, blocco.DopoIlRecord + 1);
        return fatto;
    }

    /// <summary>Il giro comune dei gesti che scrivono il testo del file (nascondi, mostra): registro, geometria, avvisi.</summary>
    private bool GestoSulTesto(string fileRelativo, string cosa, Func<FileAperto, object> fai)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        var esito = fai(file);
        Registro.Scrivi(cosa, $"{fileRelativo}: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDelTesto)
        {
            RifaiLaGeometria(fileRelativo);
            RigaSegnalata = null;
            if (Scelta is { } scelta && scelta.File == fileRelativo && scelta.Record >= file.Record)
                Scelta = null;
        }

        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDelTesto;
    }

    // --- bordo e riempimento (lotto «Subito» slice 8d, I2) ------------------------------------------------------------

    /// <summary>
    /// L'uscita che manca alla forma del record (il bordo in un .geo per un poligono di un .pol, il riempimento in un .pol
    /// per una linea chiusa di un .geo), o null; <paramref name="perche"/> dice perché non si può aggiungere, se manca.
    /// </summary>
    public UscitaDellaForma? UscitaDi(string fileRelativo, int record, out string? perche)
    {
        perche = null;
        if (Sessione is null)
            return null;
        if (_formeUguali is not { } indice || !ReferenceEquals(indice.Strati, Strati))
            _formeUguali = indice = (Strati, FormeUguali.Di(Strati));
        var delFile = Strati.SelectMany(s => s.Forme).Where(f => f.File == fileRelativo).ToList();
        return UsciteDellaForma.Di(Sessione, indice.Indice, delFile, fileRelativo, record, out perche);
    }

    /// <summary>
    /// Aggiunge l'uscita che manca (I2) in fondo al suo file, legata alla forma come famiglia: da lì cambiare l'una cambia
    /// l'altra. Le righe si fissano adesso (annulla e ripeti rifanno le stesse); il record nuovo diventa la scelta.
    /// </summary>
    public bool AggiungiLUscita(string fileRelativo, int record)
    {
        if (UscitaDi(fileRelativo, record, out string? perche) is not { } uscita)
        {
            Rifiuto = perche ?? "Questa forma ha già il suo bordo e il suo riempimento.";
            Avvisa();
            return false;
        }

        return NellaStoria($"{(uscita.Bordo ? "bordo" : "riempimento")} di {EtichettaDi(fileRelativo, record)} aggiunto in {NomeDelFile(uscita.File)}",
            () => AggiungiLUscitaAdesso(uscita));
    }

    private bool AggiungiLUscitaAdesso(UscitaDellaForma uscita)
    {
        if (Sessione?.File.GetValueOrDefault(uscita.File) is not { } file || file is not IFileConRecord conRecord)
            return false;

        int prima = conRecord.RecordDelModello.Count;
        int nuovi = uscita.Bordo ? uscita.Righe.Count - 2 : 1;
        var righe = conRecord.RigheDelFile(Modifiche.SporchiDi(uscita.File));
        // In fondo al file, dopo l'ultima riga; una riga vuota in più se il file non ne ha già una in fondo.
        var aggiunte = righe.Count > 0 && righe[^1].Trim().Length == 0 ? uscita.Righe.Skip(1).ToList() : uscita.Righe.ToList();
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>> { [righe.Count] = [righe[^1], .. aggiunte] };
        string gesto = uscita.Bordo ? $"bordo aggiunto ({uscita.Tipo})" : $"riempimento aggiunto ({uscita.Tipo})";
        bool fatto = GestoSulTesto(uscita.File, gesto, f => Modifiche.CambiaRighe(f, sostituzioni, gesto,
            riletto => riletto.RecordDelModello.Count == prima + nuovi ? null : "Riletto, il file non ha i record che dovrebbe: si aggiunge a mano."));
        if (fatto)
            Scegli(uscita.File, prima);
        return fatto;
    }

    // --- la vista a linea dei .geo (lotto «Subito» slice 5d) ------------------------------------------------------

    /// <summary>La linea del segmento .geo, o null.</summary>
    public LineaDelGeo? LineaDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file ? Modifiche.LineaDi(file, record) : null;

    /// <summary>Sposta un punto della linea: i due segmenti che lo toccano, in un gesto solo della storia.</summary>
    public bool CambiaPuntoDellaLinea(string fileRelativo, int record, int punto, string? testo)
    {
        // Slice 8d: come per i vertici, le copie della stessa forma si fissano adesso (annulla e ripeti le ritrovano).
        var copie = CopieDellaLinea(fileRelativo, record);
        return NellaStoria($"punto {punto + 1} della linea di {EtichettaDi(fileRelativo, record)} spostato",
            () => GestoSulPunto(fileRelativo, record, punto, testo, copie));
    }

    private bool GestoSulPunto(string fileRelativo, int record, int punto, string? testo, IReadOnlyList<ParteDiForma> copie)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        FormaPortata = null;
        var uguali = Modifiche.LineaDi(file, record) is { } prima ? AncoraUguali(PortaLaForma.Da(prima), copie) : [];

        string etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "";
        var esito = Modifiche.CambiaPuntoDellaLinea(file, record, punto, testo, etichetta);
        Registro.Scrivi("linea", $"{fileRelativo}#{record} punto {punto} «{testo}»: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is not ModificaRifiutata)
        {
            RifaiLaGeometria(fileRelativo);
            if (uguali.Count > 0 && Modifiche.LineaDi(file, record) is { } dopo)
                FormaPortata = PortaSulleCopie(PortaLaForma.Da(dopo), $"{NomeDelFile(fileRelativo)} {etichetta}", uguali);
        }
        RicontrollaLeModifiche();
        Avvisa();
        return esito is not ModificaRifiutata;
    }

    /// <summary>Spezza la linea in un punto in mezzo (una riga vuota).</summary>
    public bool SpezzaLaLinea(string fileRelativo, int record, int punto)
        => NellaStoria($"linea di {EtichettaDi(fileRelativo, record)} spezzata al punto {punto + 1}",
            () => GestoSulTesto(fileRelativo, "spezza linea", file => Modifiche.SpezzaLaLinea(file, record, punto, EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "")));

    /// <summary>Riunisce la linea con quella dopo (o prima), staccata da righe vuote.</summary>
    public bool UnisciLaLinea(string fileRelativo, int record, bool dopo)
        => NellaStoria($"linea di {EtichettaDi(fileRelativo, record)} riunita",
            () => GestoSulTesto(fileRelativo, "unisci linea", file => Modifiche.UnisciLaLinea(file, record, dopo, EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "")));

    // --- spezza e unisci (lotto «Subito» slice 5b) ---------------------------------------------------------------

    /// <summary>Come si interrompe quell'elenco (riga vuota, &lt;br&gt;, DUMMY, BREAK), o null se lì non si spezza.</summary>
    public FormaDellInterruzione? FormaDellInterruzioneDi(string fileRelativo, int record, string campo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file ? Interruzioni.Forma(file, record, campo) : null;

    /// <summary>Le posizioni dopo le quali, dentro l'elenco, la linea è interrotta.</summary>
    public IReadOnlyList<int> InterruzioniDentro(string fileRelativo, int record, string campo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file && ElenchiDiVertici.Uno(file, record, campo) is { } elenco
            ? Interruzioni.Dentro(elenco)
            : [];

    /// <summary>Dove continua la linea dopo l'ultimo punto dell'elenco, oltre un'interruzione.</summary>
    public Continuazione? ContinuaDopo(string fileRelativo, int record, string campo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file ? Interruzioni.Dopo(file, record, campo) : null;

    /// <summary>Spezza (o riunisce) la linea dopo il punto <paramref name="dopo"/>: una voce nel testo del file.</summary>
    public bool SpezzaOUnisci(string fileRelativo, int record, string campo, int dopo, bool spezza)
        => NellaStoria($"{EtichettaDi(fileRelativo, record)} {(spezza ? "spezzata" : "riunita")} dopo il punto {dopo + 1}",
            () => SpezzaOUnisciAdesso(fileRelativo, record, campo, dopo, spezza));

    private bool SpezzaOUnisciAdesso(string fileRelativo, int record, string campo, int dopo, bool spezza)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        string etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "";
        var esito = Modifiche.SpezzaOUnisci(file, record, campo, dopo, spezza, etichetta);
        Registro.Scrivi(spezza ? "spezza" : "unisci", $"{fileRelativo}#{record} {campo} dopo {dopo}: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDelTesto)
        {
            RifaiLaGeometria(fileRelativo);
            // La scheda resta sul pezzo che si stava guardando: il primo, che non cambia numero.
            if (Scelta is { } scelta && scelta.File == fileRelativo && scelta.Record >= file.Record)
                Scelta = null;
        }

        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDelTesto;
    }

    /// <summary>
    /// Sposta sopra la sua riga il commento in coda (lotto «Subito» slice 2a, «file per file» §C): quello della riga
    /// <paramref name="riga"/> (da 1, nel file com'è adesso), o tutti quelli del file se è null. Una voce sola nelle
    /// modifiche, che si annulla come una riga scritta a mano.
    /// </summary>
    public bool SpostaICommentiSopra(string fileRelativo, int? riga = null)
        => NellaStoria(riga is { } n
                ? $"commento della riga {n} di {NomeDelFile(fileRelativo)} spostato sopra"
                : $"commenti in coda di {NomeDelFile(fileRelativo)} spostati sopra",
            () => SpostaICommentiSopraAdesso(fileRelativo, riga));

    private bool SpostaICommentiSopraAdesso(string fileRelativo, int? riga)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        var righe = RigheDiAdesso(fileRelativo);
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        foreach (int numero in CommentiInCoda.Righe(righe).Where(n => riga is null || n == riga))
        {
            var (commento, dati) = CommentiInCoda.Separa(righe[numero - 1])!.Value;
            sostituzioni[numero] = [commento, dati];
        }

        object esito = sostituzioni.Count == 0
            ? new ModificaRifiutata(riga is null ? "Il file non ha commenti in coda." : $"La riga {riga} non ha un commento in coda.")
            : Modifiche.CambiaRighe(file, sostituzioni);
        Registro.Scrivi("commenti in coda", $"{fileRelativo}{(riga is { } r ? ":" + r : "")}: {sostituzioni.Count} spostati, {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDelTesto)
        {
            RifaiLaGeometria(fileRelativo);
            if (riga is { } spostata && file is IFileConRecord conRecord && conRecord.RecordDellaRiga(spostata + 1) is { } record)
            {
                Scelta = (fileRelativo, record);
                RigaSegnalata = (fileRelativo, spostata + 1);
            }
            else if (Scelta is { } scelta && scelta.File == fileRelativo && scelta.Record >= file.Record)
            {
                Scelta = null;
            }
        }

        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDelTesto;
    }

    public bool TogliRecord(string fileRelativo, int record)
    {
        // Slice 8e (F2): togliere un punto .vfi PROPONE di togliere il suo fix nascosto; non lo toglie da solo.
        var gemello = Sessione is null ? null : GemelliVfr.Di(Sessione, fileRelativo, record);
        string etichetta = EtichettaDi(fileRelativo, record);
        bool tolto = TogliRecordEBasta(fileRelativo, record);
        GemelloDaTogliere = tolto && fileRelativo.EndsWith(".vfi", StringComparison.OrdinalIgnoreCase) && gemello is { Gemelli: [var unico] }
            ? new GemelloDaTogliere(etichetta, gemello.Codice, unico.File)
            : null;
        if (GemelloDaTogliere is not null)
            Avvisa();
        return tolto;
    }

    /// <summary>
    /// Dopo aver tolto un punto .vfi che aveva il gemello (slice 8e): la domanda «togli anche il fix nascosto?». Null quando
    /// non c'è niente da chiedere; la risposta (o un altro gesto) la toglie.
    /// </summary>
    public GemelloDaTogliere? GemelloDaTogliere { get; private set; }

    /// <summary>La risposta alla domanda: sì toglie il fix nascosto (un gesto suo nella storia), no lo lascia.</summary>
    public bool RispondiSulGemello(bool togli)
    {
        var domanda = GemelloDaTogliere;
        GemelloDaTogliere = null;
        if (!togli || domanda is null || Sessione?.File.GetValueOrDefault(domanda.File) is not IFileConRecord conRecord)
        {
            Avvisa();
            return false;
        }

        // Il record si cerca per nome adesso: fra la domanda e la risposta il file può essere cambiato.
        int indice = conRecord.RecordDelModello.ToList().FindIndex(r => r is Fix f && f.Name.Trim() == domanda.Codice);
        return indice >= 0 && TogliRecordEBasta(domanda.File, indice);
    }

    // --- i modelli ATIS e D-ATIS (slice 18) -------------------------------------------------------------------

    /// <summary>La voce di «Ascolta» (slice 18c, W2): di base nessuna; il Lab vero mette quella di Windows.</summary>
    public IVoce Voce { get; set; } = new VoceMuta();

    private Dictionary<string, string>? _valoriDellAtis;

    /// <summary>I valori con cui si riempie l'anteprima di un modello: quelli d'esempio, e quelli scritti dall'AOD.</summary>
    public IReadOnlyDictionary<string, string> ValoriDellAtis
        => _valoriDellAtis ??= Sessione is null ? [] : ModelliAtisDelLab.Esempio(Sessione);

    /// <summary>Cambia un valore dell'anteprima (vuoto: la parte facoltativa di quel segnaposto sparisce).</summary>
    public void CambiaIlValoreDellAtis(string nome, string? valore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        _ = ValoriDellAtis;
        _valoriDellAtis![nome] = (valore ?? "").Trim();
        Avvisa();
    }

    /// <summary>Il testo che esce da un modello coi valori dell'anteprima.</summary>
    public string AnteprimaDellAtis(string modello)
        => ModelloAtis.Riempi(ModelloAtis.Leggi(modello ?? ""), ValoriDellAtis);

    /// <summary>
    /// Un simbolo di un <c>.sym</c> per l'editor a pixel della scheda (slice 17c, T1), col suo numero nella tavolozza di
    /// Aurora e quanti ce ne sono nel file. Null se il record non è un simbolo.
    /// </summary>
    public (Simbolo Simbolo, int Numero, int Quanti)? SimboloDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file
           && record >= 0 && record < file.RecordDelModello.Count && file.RecordDelModello[record] is Simbolo simbolo
            ? (simbolo, record + 1, file.RecordDelModello.Count)
            : null;

    /// <summary>
    /// Sposta un simbolo di un posto nella tavolozza (slice 17d, chiesto dal committente il 17 ottobre): su con
    /// <paramref name="verso"/> -1, giù con +1. Si scambiano le righe dei due simboli (nome e pixel); i commenti davanti
    /// (<c>////ALTRI</c>) e le righe vuote restano dove sono. È un gesto sul testo: si annulla come gli altri.
    /// </summary>
    public bool SpostaIlSimbolo(string fileRelativo, int record, int verso)
    {
        if (verso is not (-1 or 1) || SimboloDi(fileRelativo, record) is not { } simbolo)
            return false;
        int altro = record + verso;
        if (altro < 0 || altro >= simbolo.Quanti)
        {
            Rifiuto = verso < 0 ? "È già il primo della tavolozza." : "È già l'ultimo della tavolozza.";
            Avvisa();
            return false;
        }

        string nome = simbolo.Simbolo.Nome ?? "simbolo senza nome";
        return NellaStoria($"{nome} spostato al {altro + 1}° posto", () =>
        {
            var file = (IFileConRecord)Sessione!.File[fileRelativo]!;
            var posti = file.PostiDeiRecord(Modifiche.SporchiDi(fileRelativo));
            var righe = RigheDiAdesso(fileRelativo);
            var (primo, secondo) = (posti[Math.Min(record, altro)], posti[Math.Max(record, altro)]);
            var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
            void Metti((int Da, int Quante) dove, (int Da, int Quante) da)
            {
                sostituzioni[dove.Da + 1] = [.. righe.Skip(da.Da).Take(da.Quante)];
                for (int k = 1; k < dove.Quante; k++)
                    sostituzioni[dove.Da + 1 + k] = [];
            }

            Metti(primo, secondo);
            Metti(secondo, primo);
            string gesto = $"{nome} spostato al {altro + 1}° posto";
            bool fatto = GestoSulTesto(fileRelativo, gesto, f => Modifiche.CambiaRighe(f, sostituzioni, gesto,
                riletto => riletto.RecordDelModello.Count == simbolo.Quanti ? null : "Riletto, il file non ha più gli stessi simboli: non si sposta."));
            if (fatto)
                Scegli(fileRelativo, altro);
            return fatto;
        });
    }

    /// <summary>Accende o spegne un pixel di un simbolo (slice 17c): una modifica del campo Pixel, che si annulla.</summary>
    public bool CambiaIlPixel(string fileRelativo, int record, int x, int y, bool acceso)
        => SimboloDi(fileRelativo, record) is { } s
           && CambiaCampo(fileRelativo, record, nameof(Simbolo.Pixel), Simbolo.ConIlPixel(s.Simbolo.Pixel, x, y, acceso));

    /// <summary>
    /// Il modello di un <c>.atis</c> o di un <c>.datis</c> come lo mostra la scheda (slice 18b-18d; W1, W3): com'è
    /// fatto, i segnaposto da offrire, quelli da riempire nell'anteprima e i modelli che gli stanno accanto. Null se il
    /// record non è un modello.
    /// </summary>
    public ModelloNellaScheda? ModelloAtisDi(string fileRelativo, int record)
    {
        if (Sessione is null || !ModelliAtisDelLab.EUnModello(fileRelativo)
            || Sessione.File.GetValueOrDefault(fileRelativo) is not IFileConRecord file
            || record < 0 || record >= file.RecordDelModello.Count || file.RecordDelModello[record] is not AtisData dato)
        {
            return null;
        }

        var letto = ModelloAtis.Leggi(dato.Template);
        var offerti = ModelliAtisDelLab.Segnaposto(Sessione, dato.Template);
        var compagni = ModelliAtisDelLab.Compagni(Sessione, fileRelativo, dato.Template);
        // Da riempire: i segnaposto di questo modello e dei suoi compagni, nell'ordine in cui si offrono; in coda
        // quelli che nessuno conosce.
        var usati = letto.Segnaposto.Concat(compagni.SelectMany(c => ModelloAtis.Leggi(c.Modello).Segnaposto)).ToHashSet(StringComparer.Ordinal);
        List<string> daRiempire = [.. offerti.Select(o => o.Nome).Where(usati.Contains),
                                   .. usati.Where(n => offerti.All(o => o.Nome != n)).Order(StringComparer.Ordinal)];
        return new ModelloNellaScheda(dato.Template, ModelliAtisDelLab.EUnDatis(fileRelativo), letto, offerti, daRiempire, compagni);
    }

    /// <summary>«Ascolta» (slice 18c, W2): la voce legge il testo. Falso, col perché in <see cref="Rifiuto"/>, se non può.</summary>
    public bool Ascolta(string testo, string? voce = null)
    {
        bool letto = Voce.Leggi(testo, voce);
        Registro.Scrivi("voce", letto ? $"letti {testo.Length} caratteri con {voce ?? Voce.DiBase}" : "non letta: " + (Voce.PercheNo ?? "niente da leggere"));
        Rifiuto = letto ? null : Voce.PercheNo ?? "Non c'è niente da leggere.";
        Avvisa();
        return letto;
    }

    /// <summary>Il gemello di un punto .vfi o di un fix nascosto (slice 8e, F2), o null se il record non ne chiede.</summary>
    public GemelloVfr? GemelloDi(string fileRelativo, int record)
        => Sessione is null ? null : GemelliVfr.Di(Sessione, fileRelativo, record);

    /// <summary>
    /// Crea il fix nascosto di un punto .vfi che non l'ha (slice 8e, F2): nome = il codice, al suo posto in ordine
    /// alfabetico nel file dei nascosti (come il modello: tipo 3, nascosto), nella posizione del punto. Un gesto solo.
    /// </summary>
    public bool CreaIlGemello(string fileRelativo, int record)
        => NellaStoria($"gemello di {EtichettaDi(fileRelativo, record)} creato", () => CreaIlGemelloAdesso(fileRelativo, record));

    private bool CreaIlGemelloAdesso(string fileRelativo, int record)
    {
        if (Sessione is null || GemelliVfr.Di(Sessione, fileRelativo, record) is not { Gemelli.Count: 0 } gemello
            || ((IFileConRecord)Sessione.File[fileRelativo]).RecordDelModello[record] is not VfrPoint punto)
            return false;
        if (GemelliVfr.FileDeiNascosti(Sessione) is not { } nascosti || Sessione.File[nascosti] is not IFileConRecord conRecord
            || conRecord.RecordDelModello.Count == 0)
        {
            Rifiuto = "Non c'è un NAVAIDS/VFR_NASCOSTI.fix con dei fix da prendere come modello.";
            Avvisa();
            return false;
        }

        var scelta = Scelta;
        if (!GestoDiStruttura(nascosti, () => Modifiche.AggiungiRecord(Sessione.File[nascosti], 0, gemello.Codice)))
            return false;
        int nuovo = Modifiche.UltimoAggiunto ?? -1;
        Scelta = scelta;
        var esito = Modifiche.Cambia(Sessione.File[nascosti], nuovo, GemelliVfr.Campo, CoordinateConverter.ToDottedDms(punto.Position),
            gemello.Codice);
        Registro.Scrivi("gemello", $"{nascosti}#{nuovo} {gemello.Codice}: {Descrivi(esito)}");
        RifaiLaGeometria(nascosti);
        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDiCampo;
    }

    private bool TogliRecordEBasta(string fileRelativo, int record)
        => NellaStoria($"{EtichettaDi(fileRelativo, record)} tolto", () => GestoDiStruttura(fileRelativo,
            () => Sessione!.File[fileRelativo] is not { } file ? new ModificaRifiutata("Questo file non è aperto.")
                // Slice 7 (L2): un punto che qualcuno cita non si toglie — le sue citazioni resterebbero senza punto.
                : UsiDi(fileRelativo, record) is { Citazioni.Count: > 0 } usi
                  // Una posizione vale in tutto l'albero (slice 7c): se un altro .frq la dichiara ancora, si toglie.
                  && !(usi.Catalogo == "posizione" && _chiLoUsa!.Copie(Sessione!, fileRelativo, record, usi.Nomi[0]).Count > 0)
                    ? new ModificaRifiutata($"È usato: lo citano {usi.Citazioni.Count} righe in {usi.File.Count} file (vedi «Chi lo usa»). "
                                            + "Toglilo prima da lì, o rinominalo.")
                    : Modifiche.TogliRecord(file, record)));

    private bool GestoDiStruttura(string fileRelativo, Func<object> fai)
    {
        if (Sessione is null || !Sessione.File.ContainsKey(fileRelativo))
            return false;

        object esito = fai();
        Registro.Scrivi("struttura", $"{fileRelativo}: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is not ModificaDiStruttura)
        {
            Avvisa();
            return false;
        }

        // I numeri dei record sono scorsi: le etichette si rifanno, e la scelta va dove è finita.
        RifaiLaGeometria(fileRelativo);
        Scelta = Modifiche.UltimoAggiunto is { } nuovo
            ? (fileRelativo, nuovo)
            : Scelta is { } vecchia && vecchia.File == fileRelativo && vecchia.Record >= Sessione.File[fileRelativo].Record
                ? null
                : Scelta;

        RicontrollaLeModifiche();
        Avvisa();
        return true;
    }

    public void AnnullaModifica(Modifica modifica)
    {
        ArgumentNullException.ThrowIfNull(modifica);
        var (file, record, campo) = (modifica.File, modifica.Record, modifica.Campo);
        // Rigiocata, la modifica è un altro oggetto (quello delle modifiche rifatte): si ritrova per chiave.
        NellaStoria($"annullata: {modifica.Descrizione} ({EtichettaDi(file, record)})",
            () => Modifiche.Tutte.FirstOrDefault(m => m.File == file && m.Record == record && m.Campo == campo) is { } adesso
                  && AnnullaModificaAdesso(adesso));
    }

    private bool AnnullaModificaAdesso(Modifica modifica)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(modifica.File, out var file))
            return false;

        // Con le copie gemelle i file toccati sono più d'uno: si rifà la geometria di tutti quelli che c'erano prima.
        var toccati = Modifiche.FileToccati.ToList();
        if (!Modifiche.Annulla(file, modifica, f => Sessione.File.GetValueOrDefault(f)))
            return false;
        Registro.Scrivi("annulla", $"{modifica.File}#{modifica.Record} {modifica.Descrizione}");
        Rifiuto = null;
        if (modifica is ModificaDelTesto)
            RifaiICataloghi();
        foreach (string toccato in toccati.Except(Modifiche.FileToccati).Append(modifica.File).Distinct())
            RifaiLaGeometria(toccato);
        RicontrollaLeModifiche();
        Avvisa();
        return true;
    }

    /// <summary>«Allinea anche questo» (F3-bis D2): il valore nuovo anche sulla copia gemella che era stata lasciata.</summary>
    public void AllineaLaCopia(Modifica principale, CopiaNonToccata copia)
    {
        ArgumentNullException.ThrowIfNull(principale);
        var (file, record, campo) = (principale.File, principale.Record, principale.Campo);
        NellaStoria($"{campo} allineato anche in {NomeDelFile(copia.File)}",
            () => Modifiche.Tutte.FirstOrDefault(m => m.File == file && m.Record == record && m.Campo == campo) is { } adesso
                  && AllineaLaCopiaAdesso(adesso, copia));
    }

    private bool AllineaLaCopiaAdesso(Modifica principale, CopiaNonToccata copia)
    {
        if (Sessione is null)
            return false;

        var esito = Modifiche.AllineaLaCopia(principale, copia, f => Sessione.File.GetValueOrDefault(f));
        Registro.Scrivi("allinea", $"{copia.File}#{copia.Record} {principale.Campo}: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        RifaiLaGeometria(copia.File);
        RicontrollaLeModifiche();
        Avvisa();
        return esito is Modifica;
    }

    public void AnnullaTutte(string? soloQuesto = null)
        => NellaStoria(soloQuesto is null ? "annullate tutte le modifiche" : $"annullate le modifiche di {NomeDelFile(soloQuesto)}",
            () => AnnullaTutteAdesso(soloQuesto));

    private bool AnnullaTutteAdesso(string? soloQuesto)
    {
        if (Sessione is null || !Modifiche.FileToccati.Any(f => soloQuesto is null || f == soloQuesto))
            return false;

        var toccati = Modifiche.FileToccati.ToList();
        Registro.Scrivi("annulla", $"tutto ({Modifiche.Quante} modifiche" + (soloQuesto is null ? ")" : $" di {soloQuesto})"));
        Modifiche.AnnullaTutto(f => Sessione.File.GetValueOrDefault(f), soloQuesto);
        Rifiuto = null;
        RifaiICataloghi();
        foreach (string file in toccati)
            RifaiLaGeometria(file);
        RicontrollaLeModifiche();
        RifaiIConfrontiDellAip();
        Avvisa();
        return true;
    }

    /// <summary>Il diff di un file toccato: righe tolte e aggiunte, prodotte dallo scrittore vero.</summary>
    public Diff.Esito DiffDi(string fileRelativo)
        => Sessione is not null && Sessione.File.TryGetValue(fileRelativo, out var file)
            ? Modifiche.DiffDi(file)
            : new Diff.Esito([], InBlocco: false);

    // --- le sezioni che si chiudono (chieste dal committente il 24 settembre) --------------------------------------

    /// <summary>Le sezioni chiuse, per chiave (<c>scheda-righe</c>, <c>vertici:Vertices</c>, <c>strati</c>…).</summary>
    private readonly HashSet<string> _sezioniChiuse = new(StringComparer.Ordinal);

    public bool SezioneAperta(string chiave) => !_sezioniChiuse.Contains(chiave);

    public void ApriOChiudi(string chiave)
    {
        if (!_sezioniChiuse.Remove(chiave))
            _sezioniChiuse.Add(chiave);
        Avvisa();
    }

    // --- la vista: solo alcuni elementi sulla mappa (chiesta dal committente il 23 settembre) ----------------------

    /// <summary>
    /// Gli elementi che la mappa mostra quando non si vuole vedere tutto (solo l'ATZ di LIRN, ATZ e CTR, un'aerovia…):
    /// un record (<c>file#3</c>) o un file intero (<c>file#*</c>). Vuota = la mappa mostra gli strati accesi, come
    /// sempre. Le coste restano sempre: senza, non si capisce dove si è.
    /// </summary>
    public IReadOnlyList<string> InVista => _inVista;

    private List<string> _inVista => MappaAttiva.InVista;

    /// <summary>Quante volte la vista è cambiata: la mappa la riprende quando il numero non è più il suo.</summary>
    public int VersioneDellaVista
    {
        get => MappaAttiva.VersioneDellaVista;
        private set => MappaAttiva.VersioneDellaVista = value;
    }

    public static string ChiaveDellaVista(string file, int? record) => $"{file}#{(record is { } r ? r.ToString(System.Globalization.CultureInfo.InvariantCulture) : "*")}";

    public bool EInVista(string file, int? record) => _inVista.Contains(ChiaveDellaVista(file, record));

    /// <summary>Mette o toglie un record (o un file intero, con <paramref name="record"/> null) dalla vista.</summary>
    public void CambiaLaVista(string file, int? record)
    {
        string chiave = ChiaveDellaVista(file, record);
        if (!_inVista.Remove(chiave))
        {
            _inVista.Add(chiave);
            // Lo strato del file si accende: senza, sulla mappa non ci sarebbe niente da mostrare.
            if (StratiDellaMappa.DiFile(file) is { } tipo && Strati.Any(s => s.Tipo.Id == tipo.Id))
                _accesi.Add(tipo.Id);
        }

        Registro.Scrivi("vista", $"{(EInVista(file, record) ? "+" : "−")} {chiave} ({_inVista.Count} in vista)");
        VersioneDellaVista++;
        Avvisa();
    }

    /// <summary>Di nuovo tutto quel che è acceso.</summary>
    public void SvuotaLaVista()
    {
        if (_inVista.Count == 0)
            return;
        _inVista.Clear();
        VersioneDellaVista++;
        Avvisa();
    }

    /// <summary>Come si chiama a schermo un elemento della vista.</summary>
    public string NomeInVista(string chiave)
    {
        int cancelletto = chiave.LastIndexOf('#');
        string file = chiave[..cancelletto];
        string dopo = chiave[(cancelletto + 1)..];
        return dopo == "*" ? $"{NomeDelFile(file)} (tutto)" : EtichettaDi(file, int.Parse(dopo, System.Globalization.CultureInfo.InvariantCulture));
    }

    // --- l'anteprima di «incolla da testo» (chiesta dal committente il 23 settembre) -----------------------------

    /// <summary>
    /// Il testo che l'AOD sta per incollare, già letto, e per quale elenco: la scheda ne mostra il disegno, la mappa lo
    /// sovrappone alla forma di oggi (tratteggiato). Sta qui e non nella scheda perché scheda e mappa possono stare in
    /// due finestre diverse (due schermi).
    /// </summary>
    public AnteprimaDiIncolla? Anteprima { get; private set; }

    /// <summary>Quante volte l'anteprima è cambiata: la mappa la ridisegna quando il numero non è più il suo.</summary>
    public int VersioneDellAnteprima { get; private set; }

    /// <summary>Legge il testo e ne fa l'anteprima; un testo vuoto la toglie.</summary>
    public void MostraLAnteprima(string fileRelativo, int record, string campo, string? testo, double puntiPerGrado, bool? chiudi = null)
    {
        if (string.IsNullOrWhiteSpace(testo) || puntiPerGrado <= 0)
        {
            TogliLAnteprima();
            return;
        }

        Anteprima = new AnteprimaDiIncolla(fileRelativo, record, campo, puntiPerGrado, TestoDaIncollare.Leggi(testo, puntiPerGrado, chiudi ?? ElencoChiuso(fileRelativo, record, campo)));
        VersioneDellAnteprima++;
        Avvisa();
    }

    /// <summary>Se l'elenco di vertici di oggi è chiuso (l'ultimo punto ripete il primo): la casella «Chiudi la forma» parte da qui.</summary>
    public bool ElencoChiuso(string fileRelativo, int record, string campo)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is { } file && ElenchiDiVertici.Uno(file, record, campo) is { } elenco
           && ModificheInSospeso.ElencoChiuso(elenco);

    public void TogliLAnteprima()
    {
        if (Anteprima is null)
            return;
        Anteprima = null;
        VersioneDellAnteprima++;
        Avvisa();
    }

    // --- annulla e ripeti (chiesti dal committente il 23 settembre) ---------------------------------------------

    /// <summary>
    /// I gesti fatti dall'apertura (o dall'ultimo salvataggio), in ordine, ognuno col modo di rifarlo. Annullare NON è
    /// «fare il contrario» di un gesto — per le copie gemelle, le mappe composte e i record aggiunti il contrario non è
    /// uno solo —: si rimette tutto com'era all'apertura (lo sanno già fare le modifiche in sospeso, 754 file identici
    /// sull'albero vero) e si rigiocano i gesti tranne l'ultimo. Ripetere rigioca il gesto annullato. Un gesto costa
    /// millisecondi: anche cento, rigiocati, non si sentono.
    /// </summary>
    private readonly List<GestoNellaStoria> _storia = [];

    /// <summary>Quanti gesti della storia sono fatti: gli altri, dopo, sono quelli annullati che si possono ripetere.</summary>
    private int _fatti;

    /// <summary>Vero mentre si rigiocano i gesti: niente storia nuova, niente avvisi, la geometria si rifà alla fine.</summary>
    private bool _rigioco;

    private readonly HashSet<string> _daRifareDopoIlRigioco = new(StringComparer.Ordinal);

    /// <summary>
    /// Un gesto della storia; <paramref name="File"/> e <paramref name="Record"/> = i file e i record che ha toccato (vanno
    /// nel lotto di Aurora, F5 slice 2 e 3).
    /// </summary>
    private sealed record GestoNellaStoria(string Cosa, Func<bool> Rifai, IReadOnlyList<string> File, IReadOnlyList<Core.Aurora.RecordDelLotto> Record);

    public bool SiPuoAnnullare => _fatti > 0;

    public bool SiPuoRipetere => _fatti < _storia.Count;

    /// <summary>Che cosa annullerebbe «Annulla»: va nel suggerimento del tasto.</summary>
    public string? DaAnnullare => _fatti > 0 ? _storia[_fatti - 1].Cosa : null;

    public string? DaRipetere => _fatti < _storia.Count ? _storia[_fatti].Cosa : null;

    /// <summary>Fa un gesto e, se è andato, lo mette nella storia: i gesti annullati dopo di lui non si ripetono più.</summary>
    private bool NellaStoria(string cosa, Func<bool> gesto)
    {
        // La domanda sul gemello (8e) vale per il gesto appena fatto: il prossimo la toglie.
        if (!_rigioco)
            GemelloDaTogliere = null;
        var prima = _rigioco ? null : Modifiche.Tutte.ToHashSet();
        bool fatto = gesto();
        if (!fatto || _rigioco)
            return fatto;

        // I file del gesto: quelli le cui modifiche in sospeso sono cambiate (dove guardarlo in Aurora, F5 slice 2).
        var dopo = Modifiche.Tutte.ToHashSet();
        var cambiate = dopo.Except(prima!).Concat(prima!.Except(dopo)).ToList();
        IReadOnlyList<string> file = [.. cambiate.Select(m => m.File).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        // I record col loro nome di adesso (dopo una rinomina, quello nuovo): la nota di un ❌ li ritrova per nome (slice 3).
        IReadOnlyList<Core.Aurora.RecordDelLotto> record = [.. cambiate
            .Where(m => m.Record >= 0 && Sessione?.File.TryGetValue(m.File, out var aperto) == true && m.Record < aperto.Record)
            .Select(m => new Core.Aurora.RecordDelLotto(m.File, EtichettaDi(m.File, m.Record)))
            .Distinct()];
        _storia.RemoveRange(_fatti, _storia.Count - _fatti);
        _storia.Add(new GestoNellaStoria(cosa, gesto, file, record));
        _fatti = _storia.Count;
        Avvisa();
        return true;
    }

    /// <summary>Annulla l'ultimo gesto (Ctrl+Z).</summary>
    public void Annulla()
    {
        if (Sessione is null || _fatti == 0)
            return;

        Registro.Scrivi("storia", $"annulla: {_storia[_fatti - 1].Cosa}");
        Rigioca(_fatti - 1);
    }

    /// <summary>Ripete il gesto annullato (Ctrl+Y).</summary>
    public void Ripeti()
    {
        if (Sessione is null || _fatti >= _storia.Count)
            return;

        Registro.Scrivi("storia", $"ripeti: {_storia[_fatti].Cosa}");
        Rigioca(_fatti + 1);
    }

    /// <summary>Tutto com'era all'apertura, poi i primi <paramref name="quanti"/> gesti della storia, di nuovo.</summary>
    private void Rigioca(int quanti)
    {
        GemelloDaTogliere = null;
        var sessione = Sessione!;
        var scelta = Scelta;
        _daRifareDopoIlRigioco.UnionWith(Modifiche.FileToccati);
        _rigioco = true;
        try
        {
            Modifiche.AnnullaTutto(f => sessione.File.GetValueOrDefault(f));
            // Quel che le modifiche ricordano oltre ai valori (fotografie, l'ultimo aggiunto) riparte pulito.
            Modifiche = new();
            for (int i = 0; i < quanti; i++)
            {
                if (!_storia[i].Rifai())
                    Registro.Scrivi("storia", $"  non rifatto: {_storia[i].Cosa}");
            }
        }
        finally
        {
            _rigioco = false;
            _fatti = quanti;
        }

        // I nomi possono essere tornati quelli di prima (una rinomina annullata, slice 7b): i cataloghi si rifanno.
        RifaiICataloghi();
        // La scelta resta dov'era, se quel record c'è ancora.
        Scelta = scelta is { } s && sessione.File.TryGetValue(s.File, out var file) && s.Record < file.Record ? s : null;
        Rifiuto = null;
        _daRifareDopoIlRigioco.UnionWith(Modifiche.FileToccati);
        foreach (string toccato in _daRifareDopoIlRigioco.ToList())
            RifaiLaGeometria(toccato);
        _daRifareDopoIlRigioco.Clear();
        RicontrollaLeModifiche();
        RifaiIConfrontiDellAip();
        Avvisa();
    }

    /// <summary>La storia vale per i record di adesso: dopo un salvataggio o una rilettura dal disco sono altri oggetti.</summary>
    private void ScordaLaStoria()
    {
        _storia.Clear();
        _fatti = 0;
    }

    private string EtichettaDi(string fileRelativo, int record)
        => EtichetteDi(fileRelativo).ElementAtOrDefault(record) is { Length: > 0 } etichetta
            ? etichetta
            : $"{NomeDelFile(fileRelativo)} #{record}";

    private static string NomeDelFile(string fileRelativo) => fileRelativo[(fileRelativo.LastIndexOf('/') + 1)..];

    /// <summary>
    /// Rifà le forme del solo file toccato: rifare tutto l'albero costerebbe 48 ms a ogni tasto, e non serve —
    /// una modifica sta in un file solo.
    /// </summary>
    private void RifaiLaGeometria(string fileRelativo)
    {
        if (_rigioco)
        {
            _daRifareDopoIlRigioco.Add(fileRelativo);
            return;
        }

        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return;

        // «Chi lo usa» (slice 7): i nomi citati da questo file si rifanno, anche se il file non ha uno strato (.hold).
        _chiLoUsa?.RifaiIlFile(file);
        ScordaGliUsi();

        var tipo = StratiDellaMappa.DiFile(fileRelativo);
        _etichette.Remove(fileRelativo);
        _versioneDeiFile++;
        _elenchi = null;
        foreach (var chiave in _stime.Keys.Where(k => k.File == fileRelativo).ToList())
            _stime.Remove(chiave);
        if (tipo is null)
            return;

        var rifatte = Geometria.DelFile(file, CatalogoScelto);
        Strati = [.. Strati.Select(s => s.Tipo.Id != tipo.Id
            ? s
            : s with
            {
                // Nell'ordine di sempre (file, poi record): la mappa e l'elenco non si riordinano sotto le mani.
                Forme = [.. s.Forme.Where(f => f.File != fileRelativo).Concat(rifatte)
                    .OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Record)],
            })];

        // La mappa ha le coordinate in memoria: finché il numero del suo strato non cambia, non ha motivo di
        // richiederle. Un numero PER STRATO: un gesto con le copie gemelle tocca file di strati diversi, e con un
        // numero solo la mappa riprendeva solo l'ultimo.
        _versioniDegliStrati[tipo.Id] = ++VersioneDellaGeometria;
        FileRifatto = fileRelativo;
    }

    /// <summary>Tutti gli strati sono da riprendere: i cataloghi sono rifatti (salvataggio, rilettura, master cambiato).</summary>
    private void TuttiGliStratiCambiati()
    {
        VersioneDellaGeometria++;
        foreach (var strato in Strati)
            _versioniDegliStrati[strato.Tipo.Id] = VersioneDellaGeometria;
        // R5: lo strato dell'AIP porta i colori dello schema e i simboli come quelli del sector, e si riprende con loro.
        if (StratoDellAip is not null)
            _versioniDegliStrati[Core.PdfAip.AipSullaMappa.Tipo.Id] = VersioneDellaGeometria;
    }

    /// <summary>Quante volte la geometria è cambiata, in tutto.</summary>
    public int VersioneDellaGeometria { get; private set; }

    /// <summary>La versione della geometria di uno strato: la mappa lo riprende quando non è più quella che ha disegnato.</summary>
    public int VersioneDelloStrato(string strato) => _versioniDegliStrati.GetValueOrDefault(strato);

    private readonly Dictionary<string, int> _versioniDegliStrati = new(StringComparer.Ordinal);

    private CatalogoDeiPunti? CatalogoScelto
        => IscScelto is not null && Cataloghi.TryGetValue(IscScelto, out var catalogo) ? catalogo : null;

    /// <summary>
    /// I punti da proporre mentre si scrive un punto o un navaid (lotto «Subito», slice 3c): dai nomi che il master
    /// scelto conosce — quelli che Aurora risolverebbe — filtrati col testo scritto fin qui.
    /// </summary>
    public IReadOnlyList<PuntoDelCatalogo> Suggerimenti(string? testo) => CatalogoScelto?.Suggerisci(testo) ?? [];

    /// <summary>
    /// I punti da proporre mentre si scrive un punto in QUEL file (slice 16e, S4): in una rotta VFR prima i punti del
    /// <c>.vfi</c> dello scalo e poi quelli degli scali vicini; negli altri file come <see cref="Suggerimenti"/>.
    /// </summary>
    public IReadOnlyList<PuntoDelCatalogo> SuggerimentiPer(string? fileRelativo, string? testo)
        => fileRelativo is not null && fileRelativo.EndsWith(".vrt", StringComparison.OrdinalIgnoreCase) && CatalogoScelto is { } catalogo
            ? catalogo.SuggerisciPerUnaRottaVfr(testo, fileRelativo[..^4] + ".vfi")
            : Suggerimenti(testo);

    private readonly Dictionary<string, IReadOnlyList<string>> _etichette = new(StringComparer.Ordinal);

    /// <summary>La forma scelta, se c'è ancora fra gli strati (cambiando master una forma può sparire).</summary>
    public FormaDellaMappa? FormaScelta()
        => Scelta is not { } scelta
            ? null
            : Strati.SelectMany(s => s.Forme).FirstOrDefault(f => f.File == scelta.File && f.Record == scelta.Record);

    private void Fallita(string motivo)
    {
        Registro.Scrivi("apertura", "fallita: " + motivo);
        Stato = StatoDelLab.Errore;
        Errore = motivo;
        Sessione = null;
        Strati = [];
        Cataloghi = new Dictionary<string, CatalogoDeiPunti>();
        IscScelto = null;
        Scelta = null;
        Avvisa();
    }

    // --- I colori della mappa (lotto «Subito» slice 4, D3) ------------------------------------------------------

    /// <summary>
    /// La cartella di Aurora quando l'AOD non ne ha scelta una: nell'app <c>%APPDATA%\IVAO\AuroraWindows</c> (la passa
    /// l'eseguibile a <see cref="Server.ServerDelLab.Crea"/>); nei test nessuna, così non leggono l'Aurora vera del PC.
    /// </summary>
    public string? CartellaDiAuroraDiBase { get; init; }

    private string? _cartellaDiAurora;
    private bool _cartellaDiAuroraLetta;

    /// <summary>
    /// Dove il Lab prende gli schemi di Aurora (<c>ColourSchemes</c>): quella scelta nelle Impostazioni, se no
    /// <see cref="CartellaDiAuroraDiBase"/>. Se lì non ci sono schemi si cercano nel clone (<see cref="SchemiDiAurora"/>).
    /// </summary>
    public string? CartellaDiAurora
    {
        get
        {
            if (!_cartellaDiAuroraLetta)
            {
                _cartellaDiAurora = Ricordata("cartella-di-aurora.txt");
                _cartellaDiAuroraLetta = true;
            }

            return _cartellaDiAurora ?? CartellaDiAuroraDiBase;
        }
    }

    /// <summary>Vero se la cartella di Aurora è quella scelta dall'AOD e non quella di base.</summary>
    public bool CartellaDiAuroraScelta => CartellaDiAurora is not null && _cartellaDiAurora is not null;

    /// <summary>Cambia la cartella di Aurora e rilegge gli schemi. Vuoto = quella di base.</summary>
    public bool CambiaLaCartellaDiAurora(string? cartella)
    {
        string? scritta = string.IsNullOrWhiteSpace(cartella) ? null : cartella.Trim().Trim('"');
        if (scritta is not null && !Path.IsPathFullyQualified(scritta))
            return Rifiuta("La cartella di Aurora va scritta per intero (C:\\Users\\…\\AppData\\Roaming\\IVAO\\AuroraWindows).");
        Rifiuto = null;
        _cartellaDiAurora = scritta is null ? null : Path.GetFullPath(scritta);
        _cartellaDiAuroraLetta = true;
        Ricorda("cartella-di-aurora.txt", _cartellaDiAurora, "cartella di Aurora");
        if (Sessione is not null)
            RifaiIColori();
        RileggiISimboli();
        return true;
    }

    /// <summary>Gli schemi di Aurora (<c>*.clr</c>) della cartella di Aurora o, se lì non ce n'è, del clone aperto.</summary>
    public IReadOnlyList<string> SchemiDisponibili { get; private set; } = [];

    /// <summary>Lo schema con cui si colora la mappa: scelto una volta, ricordato fra un avvio e l'altro.</summary>
    public string? SchemaScelto { get; private set; }

    /// <summary>Vero: la mappa ha i colori di Aurora (quelli dello schema e di <c>colors.def</c>); falso: quelli del Lab, uno per strato.</summary>
    public bool ColoriDiAurora { get; private set; } = true;

    /// <summary>I colori di Aurora calcolati per la sessione aperta; null senza sessione o senza schema.</summary>
    public ColoriDellaMappa? Colori { get; private set; }

    /// <summary>I nomi di <c>[DEFINE]</c> del master scelto (<c>colors.def</c>): la mappa e il selettore della scheda.</summary>
    public ColorPalette Definiti { get; private set; } = new();

    /// <summary>I simboli dei punti: quelli di base di Aurora, uno per ruolo (slice 17b; prima dal <c>.sym</c>, slice 4d).</summary>
    public SimboliDellaMappa Simboli => _simboli ??= SimboliDellaMappa.DallaCartellaDiAurora(CartellaDiAurora, ProfiloRicordato);

    private SimboliDellaMappa? _simboli;
    private string? _profiloRicordato;
    private bool _profiloLetto;

    private string? ProfiloRicordato
    {
        get
        {
            if (!_profiloLetto)
            {
                _profiloRicordato = Ricordata("profilo-di-aurora.txt");
                _profiloLetto = true;
            }

            return _profiloRicordato;
        }
    }

    /// <summary>I profili della cartella di Aurora, fra cui si sceglie quello dei simboli.</summary>
    public IReadOnlyList<string> ProfiliDiAurora => SimboliDellaMappa.ProfiliDi(CartellaDiAurora);

    /// <summary>
    /// Sceglie il profilo di Aurora da cui si prendono i simboli dei punti (committente, 20 ottobre: la mappa deve
    /// disegnarli come Aurora). La scelta si ricorda; la mappa si ridisegna.
    /// </summary>
    public bool ScegliIlProfiloDiAurora(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || !ProfiliDiAurora.Contains(nome, StringComparer.OrdinalIgnoreCase))
            return Rifiuta($"«{nome}» non è un profilo della cartella di Aurora.");
        Rifiuto = null;
        _profiloRicordato = nome;
        _profiloLetto = true;
        Ricorda("profilo-di-aurora.txt", nome, "profilo di Aurora");
        RileggiISimboli();
        return true;
    }

    /// <summary>I simboli si rileggono (cartella di Aurora o profilo cambiati): la mappa li riprende coi colori.</summary>
    private void RileggiISimboli()
    {
        _simboli = null;
        VersioneDeiColori++;
        if (Sessione is not null)
            TuttiGliStratiCambiati();
        Registro.Scrivi("simboli", $"dal profilo {Simboli.Profilo ?? "di base"}");
        Avvisa();
    }

    /// <summary>Perché i colori di Aurora non ci sono, da dire accanto alla scelta. Null se ci sono.</summary>
    public string? ColoriMancanti { get; private set; }

    /// <summary>Cresce a ogni cambio del modo o dello schema: la mappa si ricolora.</summary>
    public int VersioneDeiColori { get; private set; }

    /// <summary>Sceglie lo schema: la mappa si riprende (i colori viaggiano con le forme) e la scelta si ricorda.</summary>
    public void ScegliSchema(string nome)
    {
        if (!SchemiDisponibili.Contains(nome, StringComparer.OrdinalIgnoreCase)
            || string.Equals(nome, SchemaScelto, StringComparison.OrdinalIgnoreCase))
            return;
        SchemaScelto = nome;
        RicordaIColori();
        RifaiIColori();
        TuttiGliStratiCambiati();
        Registro.Scrivi("colori", "schema " + nome);
        Avvisa();
    }

    /// <summary>Colori di Aurora o del Lab: la mappa si ricolora senza riprendere le coordinate.</summary>
    public void UsaIColoriDiAurora(bool aurora)
    {
        if (ColoriDiAurora == aurora)
            return;
        ColoriDiAurora = aurora;
        VersioneDeiColori++;
        RicordaIColori();
        Registro.Scrivi("colori", aurora ? "di Aurora" : "del Lab");
        Avvisa();
    }

    /// <summary>Legge lo schema scelto e i nomi di <c>[DEFINE]</c> del master; se qualcosa manca lo dice.</summary>
    private void RifaiIColori()
    {
        VersioneDeiColori++;
        Colori = null;
        ColoriMancanti = null;
        Definiti = new ColorPalette();
        if (Sessione is null)
            return;

        var avvisi = new RaccoltaDiAvvisi();
        var master = IscScelto is null ? null : Cataloghi.GetValueOrDefault(IscScelto);
        try
        {
            Definiti = SchemiDiAurora.Definiti(Sessione, master, avvisi);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("colors.def", e);
        }

        SchemiDisponibili = SchemiDiAurora.Disponibili(Sessione.Cartella, CartellaDiAurora);
        SchemaScelto = SchemiDiAurora.Scegli(SchemiDisponibili, SchemaScelto ?? _schemaRicordato);
        if (SchemaScelto is null)
        {
            ColoriMancanti = $"Nessuno schema di Aurora in «{SchemiDiAurora.Cartella(Sessione.Cartella, CartellaDiAurora)}».";
            return;
        }

        try
        {
            var schema = SchemiDiAurora.Leggi(Sessione.Cartella, SchemaScelto, avvisi, CartellaDiAurora);
            Colori = new ColoriDellaMappa(schema, Definiti);
            if (avvisi.Count > 0)
                Registro.Scrivi("colori", $"{SchemaScelto}: {avvisi.Count} righe illeggibili negli schemi o nei .def");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ColoriMancanti = $"Lo schema «{SchemaScelto}» non si legge: {e.Message}";
            Registro.Errore("colori", e);
        }
    }

    private readonly List<string> _gestiDaRegistrare = [];

    /// <summary>
    /// I gesti salvati e non ancora registrati (carta F4, slice 4): le voci della storia del Lab al momento di ogni
    /// salvataggio. Fanno il messaggio proposto. Riaperto il Lab non ci sono più: resta l'elenco dei file.
    /// </summary>
    public IReadOnlyList<string> GestiDaRegistrare => _gestiDaRegistrare;

    /// <summary>Il messaggio che il Lab propone per registrare i file del sector salvati, o null se non c'è niente.</summary>
    public string? MessaggioProposto
        => Ramo is { } ramo && Core.Git.Registrazione.DaRegistrare(ramo) is { Count: > 0 } file
            ? Core.Git.Registrazione.MessaggioProposto(_gestiDaRegistrare, file)
            : null;

    /// <summary>Sta registrando.</summary>
    public bool StaRegistrando { get; private set; }

    /// <summary>
    /// «Registra» (carta F4, slice 4): il commit dei file del sector salvati, col messaggio dato. Mai su <c>master</c> né
    /// su un <c>airac/</c>; i file fuori dai confini restano fuori. Falso, col perché in <see cref="Rifiuto"/>.
    /// </summary>
    public async Task<bool> RegistraAsync(string? messaggio)
    {
        if (StaRegistrando)
            return false;
        if (Modifiche.CEQualcosa)
            return Rifiuta("Ci sono modifiche non salvate: salvale (o annullale) prima di registrare.");
        await RileggiIlRamoAsync().ConfigureAwait(false);
        if (Ramo is not { } ramo)
            return Rifiuta(PercheDelRamo ?? "La cartella non sta in un clone git.");
        if (RiportoInCorso)
            return Rifiuta("C'è un riporto a metà: risolvi i conflitti e «Concludi il riporto» (o annullalo) prima di registrare altro.");

        StaRegistrando = true;
        Avvisa();
        string? perche;
        try
        {
            perche = await Core.Git.Registrazione.RegistraAsync(Git, ramo, Predefinito, messaggio).ConfigureAwait(false);
            Registro.Scrivi("git", $"registra su {ramo.Ramo}: {perche ?? "fatto — " + messaggio?.Split('\n')[0]}");
        }
        finally
        {
            StaRegistrando = false;
        }

        if (perche is null)
        {
            _gestiDaRegistrare.Clear();
            Rifiuto = null;
        }

        await RileggiIlRamoAsync().ConfigureAwait(false);
        return perche is null || Rifiuta(perche);
    }

    // --- riporto, fine del ciclo, cancellazioni (carta F4, slice 7) ------------------------------------------------

    /// <summary>C'è un riporto a metà (una fusione coi conflitti da risolvere).</summary>
    public bool RiportoInCorso { get; private set; }

    /// <summary>Quante registrazioni del ramo predefinito il ramo del ciclo dopo non ha ancora.</summary>
    public int DaRiportare { get; private set; }

    /// <summary>Il ciclo che dice l'intestazione del changelog del ramo di adesso, o null.</summary>
    public string? CicloNelChangelog
        => PercorsoDelChangelog is { } file && File.Exists(file) ? Core.Git.Changelog.Intestazione(File.ReadAllLines(file))?.Ciclo : null;

    /// <summary>Comincia il riporto del ramo predefinito nel ramo del ciclo dopo, in un ramo di lavoro nuovo.</summary>
    public Task<bool> CominciaIlRiportoAsync()
    {
        if (Vid is not { } vid)
            return Task.FromResult(Rifiuta("Manca il tuo VID: scrivilo nelle Impostazioni (va nel nome del ramo)."));
        string ciclo = CicloDopo;
        return GestoDiGitAsync($"riporto di {Predefinito} in {ciclo}", true, async radice =>
        {
            string? perche = await Core.Git.Riporto.CominciaAsync(Git, radice, vid, Predefinito, ciclo).ConfigureAwait(false);
            return perche == "conflitti" ? (null, "Il riporto ha dei conflitti: per ogni file scegli quale versione tenere, poi «Concludi il riporto».") : (perche, null);
        });
    }

    /// <summary>Quante registrazioni del ramo d'origine il ramo di lavoro di adesso non ha (dall'ultimo <c>fetch</c>).</summary>
    public int IndietroRispettoAllaBase { get; private set; }

    /// <summary>La prima riga del messaggio della fusione a metà («Riporto di master in airac/2611», «Allineo il ramo a master»).</summary>
    public string? FusioneAMeta { get; private set; }

    /// <summary>
    /// Allinea il ramo di lavoro al ramo da cui è nato (carta F4, slice 8): ci fonde le PR arrivate nel frattempo, coi
    /// conflitti risolti dall'AOD come nel riporto; poi «Manda» aggiorna la PR.
    /// </summary>
    public Task<bool> AllineaIlRamoAsync()
    {
        if (Ramo?.Ramo?.StartsWith(Core.Git.RamiDelSector.PrefissoDiLavoro, StringComparison.Ordinal) != true)
            return Task.FromResult(Rifiuta("Si allinea un ramo di lavoro (aod/…)."));
        string base_ = BaseDelRamo ?? Predefinito;
        return GestoDiGitAsync($"allinea {Ramo.Ramo} a {base_}", true, async radice =>
        {
            string? perche = await Core.Git.Riporto.AllineaAsync(Git, radice, base_).ConfigureAwait(false);
            return perche == "conflitti" ? (null, "L'allineamento ha dei conflitti: per ogni file scegli quale versione tenere, poi «Concludi».") : (perche, null);
        });
    }

    /// <summary>Risolve un file in conflitto come ha scelto l'AOD.</summary>
    public Task<bool> RisolviIlConflittoAsync(string percorso, Core.Git.SceltaDelConflitto scelta)
        => GestoDiGitAsync($"conflitto {percorso}: {scelta}", false,
            async radice => (await Core.Git.Riporto.RisolviAsync(Git, radice, percorso, scelta).ConfigureAwait(false), null));

    /// <summary>Conclude il riporto (la registrazione della fusione), quando non resta niente in conflitto.</summary>
    public Task<bool> ConcludiIlRiportoAsync()
        => GestoDiGitAsync("riporto concluso", false,
            async _ => (Ramo is { } ramo ? await Core.Git.Riporto.ConcludiAsync(Git, ramo).ConfigureAwait(false) : "Nessun ramo.", null));

    /// <summary>Annulla il riporto a metà (<c>git merge --abort</c>): i file tornano come nel ciclo.</summary>
    public Task<bool> AnnullaIlRiportoAsync()
        => GestoDiGitAsync("riporto annullato", false,
            async radice => (await Core.Git.Riporto.AnnullaAsync(Git, radice).ConfigureAwait(false), null));

    /// <summary>
    /// La fine del ciclo nel changelog (C3): nel ramo di lavoro nato dal ciclo nuovo, archivia il changelog del ciclo di
    /// prima in <c>CHANGELOG/</c> e lo riapre vuoto col ciclo del ramo.
    /// </summary>
    public Task<bool> ApriIlNuovoCicloAsync()
    {
        string ciclo = CicloDelChangelog;
        if (BaseDelRamo?.StartsWith(Core.Git.RamiDelSector.PrefissoDelCiclo, StringComparison.Ordinal) != true)
            return Task.FromResult(Rifiuta("Il changelog del ciclo nuovo si apre in un lavoro nato dal ramo del ciclo (airac/…)."));
        var dal = DateOnly.FromDateTime(new Vipi.Domain.Services.AiracService().EffectiveUtcForCycle(ciclo));
        return GestoDiGitAsync($"changelog del ciclo A{ciclo}", false, radice =>
        {
            Core.Git.FineDelCiclo.Apri(radice, ciclo, dal, out string? perche);
            return Task.FromResult<(string?, string?)>((perche, null));
        });
    }

    /// <summary>
    /// Le cancellazioni (C4): i file del sector tolti e non ancora registrati vanno in <c>delete.upd</c>, e le righe che li
    /// caricano escono dagli <c>.isc</c>. Poi si registrano insieme.
    /// </summary>
    public Task<bool> PreparaLeCancellazioniAsync()
    {
        var tolti = Ramo?.Cambiati.Where(c => c.Come == "tolto").Select(c => c.Percorso).ToList() ?? [];
        if (tolti.Count == 0)
            return Task.FromResult(Rifiuta("Nessun file tolto da preparare."));
        return GestoDiGitAsync($"cancellazioni di {tolti.Count} file", false, radice =>
        {
            var cambiati = Core.Git.Cancellazioni.Applica(radice, tolti);
            return Task.FromResult<(string?, string?)>((cambiati.Count == 0 ? "delete.upd e gli .isc erano già a posto." : null, null));
        });
    }

    /// <summary>
    /// Un gesto di git che cambia i file sul disco: niente modifiche del Lab in sospeso (con <paramref name="pulito"/>,
    /// neanche file non registrati), poi il gesto, poi l'albero si rilegge. Il gesto torna il perché (fallito) o una nota.
    /// </summary>
    private async Task<bool> GestoDiGitAsync(string cosa, bool pulito, Func<string, Task<(string? Perche, string? Nota)>> gesto)
    {
        if (StaCambiandoRamo)
            return false;
        if (Modifiche.CEQualcosa)
            return Rifiuta("Ci sono modifiche non salvate: salvale (o annullale) prima.");
        if (Ramo is not { } ramo)
            return Rifiuta(PercheDelRamo ?? "La cartella non sta in un clone git.");
        if (pulito && PercheNonSiCambiaRamo() is { } no)
            return Rifiuta(no);

        StaCambiandoRamo = true;
        Rifiuto = null;
        Avvisa();
        (string? Perche, string? Nota) esito;
        try
        {
            esito = await gesto(ramo.Radice).ConfigureAwait(false);
            Registro.Scrivi("git", $"{cosa}: {esito.Perche ?? esito.Nota ?? "fatto"}");
        }
        finally
        {
            StaCambiandoRamo = false;
        }

        // I file sul disco possono essere cambiati: l'albero si rilegge comunque.
        await ApriAsync(Sessione!.Cartella.Radice).ConfigureAwait(false);
        await RileggiIlRamoAsync().ConfigureAwait(false);
        ControllaIlChangelog();
        if (esito.Perche is not null)
            return Rifiuta(esito.Perche);
        Rifiuto = esito.Nota;
        Avvisa();
        return true;
    }

    /// <summary>Le righe del changelog proposte per il ramo di lavoro (carta F4, slice 6), da correggere prima di scriverle.</summary>
    public IReadOnlyList<Core.Git.RigaDelChangelog> RigheDelChangelog { get; private set; } = [];

    /// <summary>I controlli del changelog del ramo (C6), dall'ultima proposta o scrittura.</summary>
    public IReadOnlyList<Core.Git.AvvisoDelChangelog> AvvisiDelChangelog { get; private set; } = [];

    /// <summary>Il ciclo che il changelog del ramo deve dire: quello del ramo <c>airac/</c> da cui è nato, o quello in vigore.</summary>
    public string CicloDelChangelog
        => BaseDelRamo is { } base_ && base_.StartsWith(Core.Git.RamiDelSector.PrefissoDelCiclo, StringComparison.Ordinal)
            ? base_[Core.Git.RamiDelSector.PrefissoDelCiclo.Length..]
            : Core.Git.RamiDelSector.Cicli(DateTime.UtcNow).InVigore;

    private string? PercorsoDelChangelog
        => Ramo is { } ramo ? Path.Combine(ramo.Radice, Core.Git.Changelog.Percorso.Replace('/', Path.DirectorySeparatorChar)) : null;

    /// <summary>
    /// Propone le righe del changelog dalle registrazioni del ramo (C1) e controlla il file (C6). Solo da un ramo di
    /// lavoro: le righe sono quelle del lavoro rispetto al ramo da cui è nato.
    /// </summary>
    public async Task<bool> ProponiIlChangelogAsync()
    {
        await RileggiIlRamoAsync().ConfigureAwait(false);
        if (Ramo is not { Ramo: { } nome } ramo || !nome.StartsWith(Core.Git.RamiDelSector.PrefissoDiLavoro, StringComparison.Ordinal))
            return Rifiuta("Le righe del changelog si propongono da un ramo di lavoro (aod/…).");
        RigheDelChangelog = await Core.Git.RigheProposte.ProponiAsync(Git, ramo.Radice, BaseDelRamo ?? Predefinito, nome).ConfigureAwait(false);
        ControllaIlChangelog();
        Registro.Scrivi("git", $"changelog di {nome}: {RigheDelChangelog.Count} righe proposte, {AvvisiDelChangelog.Count} avvisi");
        Rifiuto = RigheDelChangelog.Count == 0 ? "Nessun file del sector registrato in questo ramo: niente da proporre." : null;
        Avvisa();
        return RigheDelChangelog.Count > 0;
    }

    private void ControllaIlChangelog()
    {
        AvvisiDelChangelog = PercorsoDelChangelog is { } file && File.Exists(file)
            ? Core.Git.Changelog.Controlla(File.ReadAllLines(file), CicloDelChangelog)
            : [new Core.Git.AvvisoDelChangelog($"Manca {Core.Git.Changelog.Percorso}.")];
    }

    /// <summary>
    /// Scrive le righe (corrette dall'AOD) nel <c>changelog.md</c> del ramo, ognuna in coda alla sua sezione (C2), poi
    /// rilegge l'albero: il file è da registrare come gli altri. Le altre righe del file restano byte per byte.
    /// </summary>
    public Task<bool> ScriviNelChangelogAsync(IReadOnlyList<(string Sezione, string Testo)> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        if (righe.All(r => string.IsNullOrWhiteSpace(r.Testo)))
            return Task.FromResult(Rifiuta("Nessuna riga da scrivere."));
        return CambiaIlChangelogAsync(vecchie =>
        {
            var nuove = Core.Git.Changelog.Aggiungi(vecchie, righe, out var scartate);
            string? rifiuto = scartate.Count > 0 ? "Senza la loro sezione nel file, non scritte: " + string.Join(" | ", scartate) : null;
            return (nuove, $"{nuove.Count - vecchie.Count} righe in più{(scartate.Count > 0 ? ", scartate: " + string.Join(" | ", scartate) : "")}", rifiuto);
        });
    }

    /// <summary>Le righe scritte nel <c>changelog.md</c> (non <c>*NIL</c>), sezione per sezione: per toglierle (C2).</summary>
    public IReadOnlyList<(string Sezione, int Riga, string Testo)> RigheScritteNelChangelog
        => PercorsoDelChangelog is { } file && File.Exists(file) ? Core.Git.Changelog.Scritte(File.ReadAllLines(file)) : [];

    /// <summary>
    /// Toglie una riga dal <c>changelog.md</c> (C2, carta F4): se era l'ultima della sua sezione, torna <c>*NIL</c>. Poi,
    /// come la scrittura, l'albero si rilegge e il file è da registrare.
    /// </summary>
    public Task<bool> TogliDalChangelogAsync(int riga)
        => CambiaIlChangelogAsync(vecchie => Core.Git.Changelog.Togli(vecchie, riga) is { } nuove
            ? (nuove, $"tolta la riga {riga + 1} «{vecchie[riga].Trim()}»", null)
            : (null, "", $"La riga {riga + 1} non è una riga scritta del changelog."));

    /// <summary>
    /// Il giro comune del changelog: lo legge (UTF-8, col BOM se c'era, l'a capo che aveva), lo cambia, lo riscrive uguale
    /// nel resto, rilegge l'albero e il ramo. <paramref name="cambia"/> torna le righe nuove (null = niente da fare, col
    /// perché), cosa scrivere nel registro, e un avviso per l'AOD.
    /// </summary>
    private async Task<bool> CambiaIlChangelogAsync(Func<List<string>, (List<string>? Nuove, string Registro, string? Rifiuto)> cambia)
    {
        if (Modifiche.CEQualcosa)
            return Rifiuta("Ci sono modifiche non salvate: salvale (o annullale) prima di cambiare il changelog.");
        if (PercorsoDelChangelog is not { } file || !File.Exists(file))
            return Rifiuta($"Manca {Core.Git.Changelog.Percorso}.");

        try
        {
            byte[] grezzi = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
            bool bom = grezzi.Length >= 3 && grezzi[0] == 0xEF && grezzi[1] == 0xBB && grezzi[2] == 0xBF;
            string testo = new System.Text.UTF8Encoding(false).GetString(grezzi, bom ? 3 : 0, grezzi.Length - (bom ? 3 : 0));
            string capo = testo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            bool finale = testo.EndsWith('\n');
            var vecchie = testo.Split(capo).ToList();
            if (finale)
                vecchie.RemoveAt(vecchie.Count - 1);
            var (nuove, registro, rifiuto) = cambia(vecchie);
            if (nuove is null)
                return Rifiuta(rifiuto ?? "Niente da cambiare nel changelog.");
            string scritto = string.Join(capo, nuove) + (finale ? capo : "");
            await File.WriteAllBytesAsync(file, [.. (bom ? (byte[])[0xEF, 0xBB, 0xBF] : []), .. new System.Text.UTF8Encoding(false).GetBytes(scritto)]).ConfigureAwait(false);
            Registro.Scrivi("git", "changelog: " + registro);
            Rifiuto = rifiuto;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("git", e);
            return Rifiuta("Il changelog non si è lasciato scrivere: " + e.Message);
        }

        RigheDelChangelog = [];
        // Il file è cambiato sul disco: l'albero si rilegge, e git lo vede da registrare.
        bool aperto = await ApriAsync(Sessione!.Cartella.Radice).ConfigureAwait(false);
        await RileggiIlRamoAsync().ConfigureAwait(false);
        ControllaIlChangelog();
        Avvisa();
        return aperto;
    }

    /// <summary>Sta mandando il ramo al server.</summary>
    public bool StaMandando { get; private set; }

    /// <summary>
    /// Come il Lab apre una pagina nel browser dell'AOD (la PR, D2). Di base il browser di Windows; i test ne mettono
    /// uno loro, che si ricorda l'indirizzo.
    /// </summary>
    public Action<Uri> ApriNelBrowser { get; init; } = ApriNelBrowserDiWindows;

    /// <summary>L'ultima pagina della PR aperta (carta F4, slice 5), per riaprirla.</summary>
    public Uri? PaginaDellaPr { get; private set; }

    /// <summary>
    /// «Manda» (carta F4, slice 5): il push del ramo di lavoro, poi la pagina di GitHub «nuova PR» già compilata, verso
    /// il ramo da cui il lavoro è nato. Falso, col perché in <see cref="Rifiuto"/>.
    /// </summary>
    public async Task<bool> MandaAsync()
    {
        if (StaMandando)
            return false;
        await RileggiIlRamoAsync().ConfigureAwait(false);
        if (Ramo is not { } ramo)
            return Rifiuta(PercheDelRamo ?? "La cartella non sta in un clone git.");

        StaMandando = true;
        Avvisa();
        string? perche;
        try
        {
            perche = await Core.Git.Invio.MandaAsync(Git, ramo).ConfigureAwait(false);
            Registro.Scrivi("git", $"manda {ramo.Ramo}: {perche ?? "fatto"}");
        }
        finally
        {
            StaMandando = false;
        }

        await RileggiIlRamoAsync().ConfigureAwait(false);
        if (perche is not null)
            return Rifiuta(perche);
        Rifiuto = null;
        await ApriLaPrAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>Apre (o riapre) la pagina della PR del ramo di lavoro di adesso, già compilata.</summary>
    public async Task<bool> ApriLaPrAsync()
    {
        if (Ramo is not { Ramo: { } nome } ramo || !nome.StartsWith(Core.Git.RamiDelSector.PrefissoDiLavoro, StringComparison.Ordinal))
            return Rifiuta("La PR si apre da un ramo di lavoro (aod/…).");
        string base_ = BaseDelRamo ?? Predefinito;
        var (titolo, testo) = await Core.Git.Invio.TitoloETestoAsync(Git, ramo.Radice, base_, nome).ConfigureAwait(false);
        if (Core.Git.Invio.PaginaDellaPr(ramo.Remoto, base_, nome, titolo, testo) is not { } pagina)
            return Rifiuta("Il server non è GitHub: la PR si apre a mano.");
        PaginaDellaPr = pagina;
        Registro.Scrivi("git", $"PR di {nome} verso {base_}: {pagina}");
        ApriNelBrowser(pagina);
        Avvisa();
        return true;
    }

    private static void ApriNelBrowserDiWindows(Uri pagina)
    {
        if (!OperatingSystem.IsWindows())
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pagina.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }

    /// <summary>Sta facendo un gesto di git che cambia i file (un ramo nuovo, un passaggio): il resto aspetta.</summary>
    public bool StaCambiandoRamo { get; private set; }

    /// <summary>
    /// Perché adesso non si può cambiare ramo, o null: modifiche del Lab non salvate, o file salvati e non registrati
    /// (carta F4 §3: prima di cambiare ramo, niente in sospeso).
    /// </summary>
    public string? PercheNonSiCambiaRamo()
    {
        if (Ramo is null)
            return PercheDelRamo ?? "La cartella non sta in un clone git.";
        if (Modifiche.CEQualcosa)
            return "Ci sono modifiche non salvate: salva o annulla prima di cambiare ramo.";
        if (Ramo.Cambiati.Count > 0)
            return "Ci sono file salvati e non registrati: registrali prima di cambiare ramo.";
        return null;
    }

    /// <summary>
    /// Un lavoro nuovo (carta F4, slice 3): il ramo <c>aod/&lt;VID&gt;/&lt;argomento&gt;</c> dal ramo predefinito
    /// (immediato) o dal ramo del ciclo dopo, poi l'albero si rilegge (i file sul disco sono quelli del ramo).
    /// </summary>
    public Task<bool> NuovoLavoroAsync(string? argomento, Core.Git.BaseDelLavoro base_)
    {
        if (Vid is not { } vid)
            return Task.FromResult(Rifiuta("Manca il tuo VID: scrivilo nelle Impostazioni (va nel nome del ramo)."));
        if (Core.Git.RamiDelSector.NomeDiLavoro(vid, argomento) is not { } nome)
            return Task.FromResult(Rifiuta("Scrivi di che lavoro si tratta (diventa il nome del ramo)."));
        string da = base_ == Core.Git.BaseDelLavoro.Immediato ? Predefinito : CicloDopo;
        if (base_ == Core.Git.BaseDelLavoro.CicloDopo && !CicloDopoSulServer)
            return Task.FromResult(Rifiuta($"Sul server non c'è ancora il ramo {CicloDopo}: fallo nascere prima."));
        return CambiaRamoAsync($"nuovo lavoro {nome} da {da}",
            radice => Core.Git.RamiDelSector.NuovoLavoroAsync(Git, radice, nome, da));
    }

    /// <summary>Passa a un altro ramo di qui (carta F4, slice 3), poi l'albero si rilegge.</summary>
    public Task<bool> PassaAlRamoAsync(string ramo)
        => ramo == Ramo?.Ramo
            ? Task.FromResult(true)
            : RamiDaGuardare.Contains(ramo, StringComparer.Ordinal)
                ? CambiaRamoAsync($"guarda {ramo} dal server", radice => Core.Git.RamiDelSector.GuardaAsync(Git, radice, ramo))
                : CambiaRamoAsync($"passa a {ramo}", radice => Core.Git.RamiDelSector.PassaAsync(Git, radice, ramo));

    /// <summary>Fa nascere sul server il ramo del ciclo dopo, uguale al ramo predefinito (carta F4, slice 3).</summary>
    public async Task<bool> FaiNascereIlCicloAsync()
    {
        if (Ramo is null || StaCambiandoRamo)
            return false;
        StaCambiandoRamo = true;
        Avvisa();
        try
        {
            string? perche = await Core.Git.RamiDelSector.FaiNascereIlCicloAsync(Git, Ramo.Radice, CicloDopo).ConfigureAwait(false);
            Registro.Scrivi("git", $"nasce {CicloDopo}: {perche ?? "fatto"}");
            Rifiuto = perche;
            return perche is null;
        }
        finally
        {
            StaCambiandoRamo = false;
            await RileggiIlRamoAsync().ConfigureAwait(false);
        }
    }

    private bool Rifiuta(string perche)
    {
        Rifiuto = perche;
        Registro.Scrivi("git", "rifiutato: " + perche);
        Avvisa();
        return false;
    }

    private async Task<bool> CambiaRamoAsync(string cosa, Func<string, Task<string?>> gesto)
    {
        if (StaCambiandoRamo)
            return false;
        if (PercheNonSiCambiaRamo() is { } no)
            return Rifiuta(no);

        StaCambiandoRamo = true;
        Rifiuto = null;
        Avvisa();
        string radice = Ramo!.Radice;
        string? perche;
        try
        {
            perche = await gesto(radice).ConfigureAwait(false);
            Registro.Scrivi("git", $"{cosa}: {perche ?? "fatto"}");
        }
        finally
        {
            StaCambiandoRamo = false;
        }

        if (perche is not null)
        {
            await RileggiIlRamoAsync().ConfigureAwait(false);
            return Rifiuta(perche);
        }

        // I file sul disco ora sono quelli dell'altro ramo: l'albero si rilegge da capo, come una cartella nuova.
        return await ApriAsync(Sessione!.Cartella.Radice).ConfigureAwait(false);
    }

    /// <summary>
    /// La finestra di Windows per scegliere una cartella (chiesto dal committente il 18 ottobre: «sfogliare l'explorer, non
    /// solo scrivere il percorso»). La mette il guscio dell'app (WinForms); senza guscio (i test, il banco) è null e i tasti
    /// «Sfoglia…» non ci sono. Riceve il titolo e la cartella da cui partire; torna quella scelta, o null.
    /// </summary>
    public Func<string, string?, Task<string?>>? ScegliCartella { get; set; }

    // ── Aurora: la vetrina (carta F5, slice 1) ─────────────────────────────────────────────────────────────────

    /// <summary>Dove stanno le impostazioni di Aurora e il suo sector pubblicato: di base quelli veri, i test ne danno altri.</summary>
    public (string ProfileIni, string Pubblicata) Aurora { get; init; } = Core.Aurora.Vetrina.DiAurora();

    private string? _vetrina;
    private bool _vetrinaLetta;

    /// <summary>La cartella della vetrina, scelta nelle Impostazioni; null finché non c'è.</summary>
    public string? CartellaDellaVetrina
    {
        get
        {
            if (!_vetrinaLetta)
            {
                _vetrina = Ricordata("vetrina.txt");
                _vetrinaLetta = true;
            }

            return _vetrina;
        }
    }

    /// <summary>Cambia la cartella della vetrina. Vuoto = nessuna. Falso, col perché in <see cref="Rifiuto"/>, se non può esserlo.</summary>
    public bool CambiaLaVetrina(string? cartella)
    {
        string? scritta = string.IsNullOrWhiteSpace(cartella) ? null : cartella.Trim();
        if (scritta is not null && Core.Aurora.Vetrina.PercheNonVa(scritta, Aurora.Pubblicata) is { } perche)
            return Rifiuta(perche);
        Rifiuto = null;
        _vetrina = scritta is null ? null : Path.GetFullPath(scritta);
        _vetrinaLetta = true;
        Ricorda("vetrina.txt", _vetrina, "vetrina");
        Avvisa();
        return true;
    }

    /// <summary>Che cosa c'è in vetrina adesso, o null.</summary>
    public Core.Aurora.InVetrina? InVetrina => CartellaDellaVetrina is { } v ? Core.Aurora.Vetrina.Contenuto(v) : null;

    /// <summary>Il «secondo sector» di Aurora: la sola riga che il Lab legge delle sue impostazioni.</summary>
    public string? SecondoSectorDiAurora => Core.Aurora.Vetrina.SecondoSector(Aurora.ProfileIni);

    /// <summary>Vero se Aurora legge la vetrina come secondo sector.</summary>
    public bool AuroraGuardaLaVetrina => CartellaDellaVetrina is { } v && Core.Aurora.Vetrina.AuroraGuarda(SecondoSectorDiAurora, v);

    /// <summary>Com'è andato l'ultimo «Porta in vetrina».</summary>
    public Core.Aurora.EsitoDellaVetrina? UltimoPortaInAurora { get; private set; }

    /// <summary>Sta copiando in vetrina.</summary>
    public bool StaPortandoInAurora { get; private set; }

    /// <summary>
    /// «Porta in vetrina» (carta F5, slice 1): la <c>SectorFiles</c> del sector aperto in vetrina, come specchio. Le
    /// modifiche non salvate non ci vanno: si salva prima.
    /// </summary>
    public async Task<bool> PortaInAuroraAsync()
    {
        if (Sessione is null || StaPortandoInAurora)
            return false;
        if (CartellaDellaVetrina is not { } vetrina)
            return Rifiuta("Manca la cartella della vetrina: sceglila nelle Impostazioni.");
        if (Modifiche.CEQualcosa)
            return Rifiuta("Ci sono modifiche non salvate: in Aurora andrebbe il sector senza di loro. Salva prima.");

        StaPortandoInAurora = true;
        Avvisa();
        try
        {
            string radice = Sessione.Cartella.Radice;
            string? commit = null;
            if (Ramo is { } ramo && (await Git.EseguiAsync(ramo.Radice, ["rev-parse", "--short=8", "HEAD"]).ConfigureAwait(false)) is { Riuscito: true } testa)
                commit = testa.Uscita.Trim();
            string? perche = null;
            var primaInVetrina = InVetrina;
            var adesso = DateTimeOffset.Now;
            var esito = await Task.Run(() => Core.Aurora.Vetrina.Porta(radice, vetrina, Aurora.Pubblicata, Ramo?.Ramo, commit, adesso, out perche)).ConfigureAwait(false);
            Registro.Scrivi("aurora", esito is null ? $"porta in Aurora: {perche}" : $"porta in Aurora {radice} ({Ramo?.Ramo} {commit}): {esito.Copiati} copiati, {esito.Tolti} tolti, {esito.Uguali} uguali");
            if (esito is null)
                return Rifiuta(perche ?? "Non è andata.");
            UltimoPortaInAurora = esito;
            Rifiuto = null;
            await LottoPortatoAsync(primaInVetrina, radice, Ramo?.Ramo, commit, esito, adesso).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("aurora", e);
            return Rifiuta("La vetrina non si è lasciata scrivere: " + e.Message);
        }
        finally
        {
            StaPortandoInAurora = false;
            Avvisa();
        }
    }

    // ── Aurora: le prove come lotti (carta F5, slice 4) ─────────────────────────────────────────────────────────

    /// <summary>La prova del ramo di adesso, se il ramo è un <c>prova/…</c> che il Lab conosce.</summary>
    public Core.Aurora.ProvaInAurora? ProvaInCorso => Core.Aurora.ProveInAurora.DelRamo(Ramo?.Ramo);

    /// <summary>
    /// Prepara una prova (carta F5, slice 4): un ramo <c>prova/…</c> dal ramo di adesso, con la variante registrata solo
    /// qui, e le sue domande nel lotto, da portare in Aurora. Falso, col perché in <see cref="Rifiuto"/>.
    /// </summary>
    public async Task<bool> PreparaLaProvaAsync(string argomento)
    {
        if (Core.Aurora.ProveInAurora.Tutte.FirstOrDefault(p => p.Argomento == argomento) is not { } prova)
            return Rifiuta($"Il Lab non conosce la prova «{argomento}».");
        // Si decide sullo stato di adesso: la barra può essere ferma a una lettura di prima (file registrati, ignorati).
        await RileggiIlRamoAsync().ConfigureAwait(false);
        if (ProvaInCorso is { } inCorso)
            return Rifiuta($"Sei già sulla prova «{inCorso.Titolo}»: buttala prima di prepararne un'altra.");

        Core.Aurora.ProvaPreparata? preparata = null;
        bool fatto = await CambiaRamoAsync($"prepara la prova {prova.Ramo}", async radice =>
        {
            var (pronta, perche) = await Core.Aurora.ProveInAurora.PreparaAsync(Git, radice, prova).ConfigureAwait(false);
            preparata = pronta;
            return perche;
        }).ConfigureAwait(false);
        if (!fatto || preparata is null || Lotto is not { } lotto)
            return fatto;

        // La barra deve dire subito «sei sulla prova»: il ramo si rilegge adesso, non in background.
        await RileggiIlRamoAsync().ConfigureAwait(false);
        lotto.Domande(prova.Argomento, preparata.Domande, [.. preparata.File.Select(f => f.Percorso)], DateTimeOffset.Now);
        ScriviIlLotto(lotto, $"prova {prova.Ramo}: {preparata.Domande.Count} domande — {string.Join(" ", preparata.Fatto)}");
        Avvisa();
        return true;
    }

    /// <summary>
    /// Butta la prova di adesso: torna al ramo da cui era partita e cancella il ramo <c>prova/…</c> (mai mandato). Le
    /// risposte già date restano negli esiti; le domande non ancora viste escono dal lotto.
    /// </summary>
    public async Task<bool> ButtaLaProvaAsync()
    {
        await RileggiIlRamoAsync().ConfigureAwait(false);
        if (ProvaInCorso is not { } prova)
            return Rifiuta("Non sei su una prova.");
        bool fatto = await CambiaRamoAsync($"butta la prova {prova.Ramo}",
            radice => Core.Aurora.ProveInAurora.ButtaAsync(Git, radice, prova.Ramo)).ConfigureAwait(false);
        if (!fatto || Lotto is not { } lotto)
            return fatto;

        await RileggiIlRamoAsync().ConfigureAwait(false);
        int tolte = lotto.TogliLaProva(prova.Argomento);
        ScriviIlLotto(lotto, $"prova {prova.Ramo} buttata: {tolte} domande non viste tolte");
        Avvisa();
        return true;
    }

    // ── Aurora: il lotto (carta F5, slice 2) ───────────────────────────────────────────────────────────────────

    private Core.Aurora.Lotto? _lotto;

    /// <summary>
    /// Il lotto del sector aperto: i gesti salvati da quando l'AOD ha detto «visto in Aurora», con dove guardarli. Sta nei
    /// dati del Lab, uno per cartella del sector, e sopravvive a un riavvio. Null senza sector aperto.
    /// </summary>
    public Core.Aurora.Lotto? Lotto
    {
        get
        {
            if (Sessione is null)
                return null;
            string radice = Path.GetFullPath(Sessione.Cartella.Radice);
            if (_lotto is null || !string.Equals(_lotto.Sector, radice, StringComparison.OrdinalIgnoreCase))
                _lotto = Core.Aurora.Lotto.Leggi(_cartellaDeiDati, radice);
            return _lotto;
        }
    }

    /// <summary>Vero se in vetrina c'è il sector aperto adesso: allora le voci portate sono davvero da vedere in Aurora.</summary>
    public bool InVetrinaCeQuesto
        => Sessione is { } s && InVetrina is { } dentro
           && string.Equals(Path.GetFullPath(s.Cartella.Radice), dentro.Sorgente, StringComparison.OrdinalIgnoreCase);

    /// <summary>«Visto in Aurora»: le voci portate in vetrina escono dal lotto; quelle non ancora portate restano.</summary>
    public void VistoInAurora()
    {
        if (Lotto is not { } lotto)
            return;
        int viste = lotto.DaVedere;
        lotto.VistoInAurora(DateTimeOffset.Now);
        ScriviIlLotto(lotto, $"visto in Aurora: {viste} voci");
        Avvisa();
    }

    /// <summary>
    /// Segna com'è andata in Aurora una voce del lotto (slice 3): ✅, ❌ con due parole, o null per toglierlo. Solo le voci
    /// in vetrina, e solo se in vetrina c'è questo sector.
    /// </summary>
    public bool SegnaNelLotto(int indice, Core.Aurora.EsitoInAurora? esito, string? nota = null)
    {
        if (Lotto is not { } lotto || !InVetrinaCeQuesto || !lotto.Segna(indice, esito, nota))
            return false;
        var voce = lotto.Voci[indice];
        ScriviIlLotto(lotto, $"«{voce.Cosa}»: {voce.Esito?.ToString() ?? "non segnata"}{(voce.Nota is null ? "" : " — " + voce.Nota)}");
        Avvisa();
        return true;
    }

    /// <summary>Le note dei ❌ rimaste, per tutto il sector aperto.</summary>
    public IReadOnlyList<Core.Aurora.NotaDaAurora> NoteDaAurora => Lotto?.Note ?? [];

    /// <summary>Le note dei ❌ che riguardano quel record (per nome) o, se la nota non ha record, il suo file.</summary>
    public IReadOnlyList<Core.Aurora.NotaDaAurora> NoteDaAuroraDi(string fileRelativo, int record)
    {
        if (NoteDaAurora.Count == 0)
            return [];
        string? etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record);
        return [.. NoteDaAurora.Where(n => n.Riguarda(fileRelativo, etichetta))];
    }

    /// <summary>Toglie la nota di un ❌ (l'AOD l'ha sistemata): se ne va solo così.</summary>
    public void TogliLaNotaDaAurora(string id)
    {
        if (Lotto is not { } lotto || !lotto.TogliLaNota(id))
            return;
        ScriviIlLotto(lotto, $"tolta la nota {id}");
        Avvisa();
    }

    /// <summary>Sceglie il record di una nota (il primo che si trova per nome), o il file se non ha record.</summary>
    public void VaiAllaNota(Core.Aurora.NotaDaAurora nota)
    {
        foreach (var r in nota.Record)
        {
            int indice = EtichetteDi(r.File).ToList().IndexOf(r.Etichetta);
            if (indice >= 0)
            {
                Scegli(r.File, indice);
                return;
            }
        }

        if (nota.File.FirstOrDefault(f => Sessione?.File.ContainsKey(f) == true) is { } file)
            ApriFile(file);
    }

    private void NelLotto(IReadOnlyList<GestoNellaStoria> gesti, IReadOnlyList<string> salvati)
    {
        if (Lotto is not { } lotto)
            return;
        var voci = new List<(string, IReadOnlyList<string>, IReadOnlyList<Core.Aurora.RecordDelLotto>)>();
        foreach (var gesto in gesti)
        {
            // Un gesto di cui non si sa il file (raro) prende i file del salvataggio; uno i cui file non sono stati salvati
            // (salvataggio interrotto) resta fuori: le sue modifiche sono ancora in sospeso.
            IReadOnlyList<string> file = gesto.File.Count == 0 ? salvati : [.. gesto.File.Where(f => salvati.Contains(f, StringComparer.Ordinal))];
            if (file.Count > 0)
                voci.Add((gesto.Cosa, file, [.. gesto.Record.Where(r => file.Contains(r.File, StringComparer.Ordinal))]));
        }

        // Salvato senza gesti nella storia (il Lab riaperto nel frattempo): una voce per file.
        if (gesti.Count == 0)
            voci.AddRange(salvati.Select(f => ($"{NomeDelFile(f)} salvato", (IReadOnlyList<string>)[f], (IReadOnlyList<Core.Aurora.RecordDelLotto>)[])));
        lotto.Salvati(voci, DateTimeOffset.Now);
        ScriviIlLotto(lotto, $"{voci.Count} voci salvate");
    }

    private async Task LottoPortatoAsync(Core.Aurora.InVetrina? prima, string radice, string? ramo, string? commit,
        Core.Aurora.EsitoDellaVetrina esito, DateTimeOffset quando)
    {
        if (Lotto is not { } lotto)
            return;
        bool stessoSector = prima is not null && string.Equals(prima.Sorgente, Path.GetFullPath(radice), StringComparison.OrdinalIgnoreCase);
        // Passando a una prova le sue domande dicono già che cosa guardare (slice 4).
        bool prova = Core.Aurora.ProveInAurora.DelRamo(ramo) is not null;
        // Stesso sector, un altro commit: che cosa è cambiato lo dicono le registrazioni fra i due, se quello di prima sta
        // nella storia di adesso (slice 5) — un ramo di lavoro preso dal server, una PR fusa e presa, un «Allinea».
        IReadOnlyList<Core.Aurora.RegistrazioneNelLotto>? registrate = null;
        if (stessoSector && !prova && prima!.Commit is { } da && commit is not null && da != commit && Ramo is { } stato)
            registrate = await Core.Aurora.Registrazioni.FraAsync(Git, stato.Radice, da).ConfigureAwait(false);
        string? cambiato = prima is null
            ? "il sector in vetrina per la prima volta: tutto da vedere"
            : !stessoSector
                ? $"in vetrina prima c'era {Path.GetFileName(prima.Sorgente)}: tutto da rivedere"
                : prova || registrate is not null
                    ? null
                    : !string.Equals(prima.Ramo, ramo, StringComparison.Ordinal)
                        ? $"cambiato ramo ({prima.Ramo ?? "nessuno"} → {ramo ?? "nessuno"}): tutto da rivedere"
                        : prima.Commit is not null && commit is not null && prima.Commit != commit
                            ? $"{ramo} è cambiato da {prima.Commit} a {commit} in un modo che il Lab non sa ripercorrere: tutto da rivedere"
                            : null;
        lotto.Portato(quando, cambiato);
        int dalleRegistrazioni = registrate is null ? 0 : lotto.Registrate(registrate, esito.Cambiati, quando);
        ScriviIlLotto(lotto, $"portato in Aurora: {lotto.DaVedere} da vedere{(registrate is null ? "" : $", {dalleRegistrazioni} dalle registrazioni ({registrate.Count} commit dopo {prima!.Commit})")}");
    }

    private void ScriviIlLotto(Core.Aurora.Lotto lotto, string cosa)
    {
        try
        {
            lotto.Scrivi(_cartellaDeiDati);
            Registro.Scrivi("lotto", $"{lotto.Sector}: {cosa}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Vale per questa sessione anche se il disco non lo ricorda; il registro lo dice.
            Registro.Errore("lotto", e);
        }
    }

    // ── I PDF dell'AIP e l'AIP espanso (carta F6, slice 0c) ─────────────────────────────────────────────────

    /// <summary>La cartella Download da cui «Prendi i PDF da Download» sposta: di base quella di Windows, i test ne danno un'altra.</summary>
    public string Download { get; init; } = Core.PdfAip.PdfDellAip.Download();

    private string? _cartellaDeiPdf;
    private bool _cartellaDeiPdfLetta;
    private string? _aipEspanso;
    private bool _aipEspansoLetto;

    /// <summary>La cartella dei PDF dell'AIP, scelta nelle Impostazioni; null finché non c'è.</summary>
    public string? CartellaDeiPdf
    {
        get
        {
            if (FissaNellAmbiente(Core.Git.PrimoAvvio.CartellaDeiPdf) is { } fissa)
                return fissa;
            if (!_cartellaDeiPdfLetta)
            {
                _cartellaDeiPdf = Ricordata("cartella-dei-pdf.txt");
                _cartellaDeiPdfLetta = true;
            }

            return _cartellaDeiPdf;
        }
    }

    /// <summary>
    /// Vero se le cartelle dei PDF e dell'AIP espanso sono quelle dell'ambiente di lavoro e non si cambiano
    /// (committente, 20 ottobre: «blocca anche le cartelle di PDF e AIP espanso»).
    /// </summary>
    public bool CartelleDellAipFisse => CartellaDiLavoro is not null;

    /// <summary>La cartella <paramref name="nome"/> dentro l'ambiente di lavoro; null senza ambiente.</summary>
    private string? FissaNellAmbiente(string nome) => CartellaDiLavoro is { } lavoro ? Path.Combine(lavoro, nome) : null;

    /// <summary>Vero se <paramref name="scritta"/> è la cartella fissa (o non c'è ambiente); se no rifiuta col perché.</summary>
    private bool AccettaNellAmbiente(string? scritta, string nome, string cosa)
    {
        if (FissaNellAmbiente(nome) is not { } fissa)
            return true;
        if (scritta is not null && Path.IsPathFullyQualified(scritta)
            && string.Equals(Path.GetFullPath(scritta).TrimEnd('\\', '/'), fissa.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return true;
        return Rifiuta($"Il Lab lavora nel suo ambiente: {cosa} è {fissa} e non si cambia.");
    }

    /// <summary>La cartella dell'AIP espanso, scelta nelle Impostazioni; null finché non c'è.</summary>
    public string? CartellaDellAipEspanso
    {
        get
        {
            if (FissaNellAmbiente(Core.Git.PrimoAvvio.CartellaDellAipEspanso) is { } fissa)
                return fissa;
            if (!_aipEspansoLetto)
            {
                _aipEspanso = Ricordata("aip-espanso.txt");
                _aipEspansoLetto = true;
            }

            return _aipEspanso;
        }
    }

    /// <summary>Cambia la cartella dei PDF. Vuoto = nessuna.</summary>
    public bool CambiaLaCartellaDeiPdf(string? cartella)
    {
        string? scritta = string.IsNullOrWhiteSpace(cartella) ? null : cartella.Trim();
        if (!AccettaNellAmbiente(scritta, Core.Git.PrimoAvvio.CartellaDeiPdf, "la cartella dei PDF"))
            return false;
        if (scritta is not null && !Path.IsPathFullyQualified(scritta))
            return Rifiuta("La cartella dei PDF va scritta per intero (D:\\…\\AIP).");
        Rifiuto = null;
        _cartellaDeiPdf = scritta is null ? null : Path.GetFullPath(scritta);
        _cartellaDeiPdfLetta = true;
        Ricorda("cartella-dei-pdf.txt", _cartellaDeiPdf, "cartella dei PDF");
        _righeDeiCicli = null;
        Avvisa();
        return true;
    }

    /// <summary>Cambia la cartella dell'AIP espanso. Vuoto = nessuna.</summary>
    public bool CambiaLAipEspanso(string? cartella)
    {
        string? scritta = string.IsNullOrWhiteSpace(cartella) ? null : cartella.Trim();
        if (!AccettaNellAmbiente(scritta, Core.Git.PrimoAvvio.CartellaDellAipEspanso, "la cartella dell'AIP espanso"))
            return false;
        if (scritta is not null && Core.PdfAip.AipEspanso.PercheNonVa(scritta, CartellaDeiPdf) is { } perche)
            return Rifiuta(perche);
        Rifiuto = null;
        _aipEspanso = scritta is null ? null : Path.GetFullPath(scritta);
        _aipEspansoLetto = true;
        Ricorda("aip-espanso.txt", _aipEspanso, "AIP espanso");
        _righeDeiCicli = null;
        Avvisa();
        return true;
    }

    /// <summary>I PDF dell'AIP nella cartella, adesso.</summary>
    public IReadOnlyList<string> PdfDellAip => CartellaDeiPdf is { } c ? Core.PdfAip.PdfDellAip.Elenco(c) : [];

    // --- le due consegne come giri (riordino R5) ----------------------------------------------------------------------

    /// <summary>Il giro del sector: modifiche → Salva → Registra il sector → Porta in vetrina → Manda il sector.</summary>
    public IReadOnlyList<Core.Git.PassoDelGiro> GiroDelSector =>
        Core.Git.GiroDellaConsegna.DelSector(Modifiche.Quante, ProblemiDelleModifiche.Count, Ramo, CartellaDellaVetrina is not null,
            Lotto?.DaPortare ?? 0, InVetrinaCeQuesto ? Lotto?.DaVedere ?? 0 : 0, Predefinito);

    /// <summary>Il giro dell'AIP espanso: i PDF → Leggi → le segnalazioni → Registra l'AIP → Manda l'AIP.</summary>
    public IReadOnlyList<Core.Git.PassoDelGiro> GiroDellAip =>
        Core.Git.GiroDellaConsegna.DellAip(PdfDellAip.Count, CicloLettoDellAip, UltimoAipEspanso?.Problemi.Count ?? 0, StatoDellAip);

    /// <summary>
    /// Il ciclo che l'AIP espanso ha già letto: quello dell'ultima lettura, o l'ultimo che ha il modello nella cartella (dopo
    /// un riavvio la lettura di prima non c'è più, il modello sì). Null se nessuno.
    /// </summary>
    private string? CicloLettoDellAip =>
        UltimoAipEspanso?.Ciclo
        ?? (CartellaDellAipEspanso is { } espanso && Directory.Exists(espanso)
            ? Directory.EnumerateDirectories(espanso).Select(Path.GetFileName).OfType<string>()
                .Where(c => Core.PdfAip.CicloAirac.Valido(c) && File.Exists(Core.PdfAip.AipEspanso.File(espanso, c)))
                .Order(StringComparer.Ordinal).LastOrDefault()
            : null);

    /// <summary>Com'è andato l'ultimo «Prendi i PDF da Download».</summary>
    public Core.PdfAip.PresaDaDownload? UltimaPresaDaDownload { get; private set; }

    /// <summary>Com'è andata l'ultima lettura nell'AIP espanso.</summary>
    public Core.PdfAip.EsitoDellAipEspanso? UltimoAipEspanso { get; internal set; }

    /// <summary>Sta leggendo i PDF.</summary>
    public bool StaLeggendoLAip { get; private set; }

    /// <summary>Il ciclo AIRAC in vigore oggi.</summary>
    public string CicloDiOggi => Core.PdfAip.CicloAirac.Di(GiornoDeiCicli()).Nome;

    /// <summary>Il giorno per i cicli dell'AIP (carta «L'AIP intero», passo 8); i test lo spostano.</summary>
    internal Func<DateOnly> GiornoDeiCicli { get; set; } = () => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>I cicli dell'AIP che il Lab ha (le cartelle dei PDF e i modelli dell'AIP espanso), rispetto a oggi (passo 8).</summary>
    public Core.PdfAip.StatoDeiCicli StatoDeiCicliDellAip =>
        Core.PdfAip.CicliDellAip.Di(Core.PdfAip.CicliDellAip.Presenti(CartellaDeiPdf, CartellaDellAipEspanso), GiornoDeiCicli());

    /// <summary>
    /// Il ciclo che «Leggi» propone (passo 8): il ciclo di lavoro, se ha la cartella dei PDF; se no, fra le cartelle dei
    /// cicli dei PDF quello in vigore, se no il successivo; senza cartelle dei cicli, quello di oggi.
    /// </summary>
    public string CicloDaLeggere
    {
        get
        {
            if (CartellaDeiPdf is not { } pdf)
                return CicloDiOggi;
            var cicli = Core.PdfAip.EmendamentoDellAip.CicliNellaCartella(pdf);
            if (CicloDiLavoro is { } lavoro && cicli.Contains(lavoro, StringComparer.Ordinal))
                return lavoro;
            return Core.PdfAip.CicliDellAip.Di(cicli, GiornoDeiCicli()).DaAprire ?? CicloDiOggi;
        }
    }

    /// <summary>I cicli che hanno il modello nell'AIP espanso (passo 8): quelli su cui si può lavorare.</summary>
    public IReadOnlyList<string> CicliConIlModello => Core.PdfAip.CicliDellAip.ConIlModello(CartellaDellAipEspanso);

    private string? _cicloScelto;
    private bool _cicloSceltoLetto;

    /// <summary>Il ciclo che il committente ha scelto con «Lavora su questo», ricordato fra un avvio e l'altro; null se nessuno.</summary>
    private string? CicloScelto
    {
        get
        {
            if (!_cicloSceltoLetto)
            {
                _cicloScelto = Ricordata("ciclo-di-lavoro.txt");
                _cicloSceltoLetto = true;
            }

            return _cicloScelto;
        }
    }

    /// <summary>
    /// Il ciclo di lavoro (26 ottobre, decisione del committente: lo sceglie lui, il Lab dice solo quale è in vigore): Indice,
    /// mappa, correzioni e confronti col sector lavorano su questo. Quello scelto; se nessuno è scelto e nell'AIP espanso c'è
    /// un ciclo solo, quello; altrimenti null, e il Lab chiede di sceglierlo.
    /// </summary>
    public string? CicloDiLavoro => Core.PdfAip.CicliDellAip.DiLavoro(CicloScelto, CicliConIlModello);

    private const string ScegliIlCiclo =
        "Scegli il ciclo su cui lavorare: «Lavora su questo» sulla sua riga, in Lettura e consegna.";

    /// <summary>Il ciclo di lavoro con il suo modello, o il perché no (per i gesti che ne hanno bisogno).</summary>
    private string? CicloDiLavoroCon(string espanso, out string? perche)
    {
        perche = null;
        if (CicloDiLavoro is { } ciclo && File.Exists(Core.PdfAip.AipEspanso.File(espanso, ciclo)))
            return ciclo;
        perche = CicliConIlModello.Count == 0
            ? "Nell'AIP espanso non c'è ancora un ciclo letto: «Leggi nell'AIP espanso» prima."
            : ScegliIlCiclo;
        return null;
    }

    /// <summary>
    /// «Vai alla voce» di una segnalazione (26 ottobre, committente: «le segnalazioni come si correggono?»): apre l'Indice
    /// sulla voce di cui parla, dove si corregge. Solo sul ciclo di lavoro: le correzioni vanno lì.
    /// </summary>
    public async Task<bool> VaiAllaSegnalazioneAsync(string ciclo, Aip.Pdf.Italia.ImportIssue problema)
    {
        if (PercheNonSulCiclo(ciclo) is { } perche)
            return Rifiuta(perche);
        if (StratoDellAip?.Ciclo != ciclo && !await MostraLAipAsync(true).ConfigureAwait(false))
            return false;
        if (StratoDellAip is not { } strato || Core.PdfAip.SegnalazioniDellAip.VoceDi(problema, strato.Voci) is not { } i)
            return Rifiuta($"Nell'Indice del {ciclo} non c'è una voce «{problema.Key}»: apri il PDF.");
        Rifiuto = null;
        ScegliLaPagina(PaginaDelLab.AipIndice);
        Scegli(strato.File, i);
        return true;
    }

    /// <summary>
    /// Il dato che una segnalazione chiede di aggiungere (26 ottobre: «dove inserisco le informazioni mancanti tipo le coordinate
    /// di AJO?»): il tipo, il nome e il perché già scritti. L'Indice apre «Aggiungi» con questi, e lo prende con
    /// <see cref="AggiuntaPresa"/>.
    /// </summary>
    public (global::Aip.CorrectionKind Tipo, string Nome, string Perche)? AggiuntaRichiesta { get; private set; }

    /// <summary>«Aggiungi AJO» su una segnalazione: porta all'Indice col modulo già aperto e la sigla scritta.</summary>
    public bool AggiungiIlDatoMancante(string ciclo, Aip.Pdf.Italia.ImportIssue problema)
    {
        if (PercheNonSulCiclo(ciclo) is { } perche)
            return Rifiuta(perche);
        if (Core.PdfAip.SegnalazioniDellAip.DatoMancante(problema) is not var (tipo, nome))
            return Rifiuta("La segnalazione non dice un dato che manca.");
        var scali = (UltimoAipEspanso?.Problemi ?? [])
            .Where(p => Core.PdfAip.SegnalazioniDellAip.DatoMancante(p.Problema)?.Nome == nome)
            .Select(p => p.Problema.Key?.Split(' ')[0]).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var chi = tipo == global::Aip.CorrectionKind.Navaid ? "stazione" : "punto";
        AggiuntaRichiesta = (tipo, nome, $"{chi} nominat{(chi == "punto" ? "o" : "a")} dalle procedure{(scali.Count > 0 ? " di " + string.Join(", ", scali) : "")}, che l'AIP italiano non ha");
        Rifiuto = null;
        ScegliLaPagina(PaginaDelLab.AipIndice);
        Avvisa();
        return true;
    }

    /// <summary>L'Indice ha aperto il modulo dell'aggiunta richiesta.</summary>
    public void AggiuntaPresa() => AggiuntaRichiesta = null;

    private string? PercheNonSulCiclo(string ciclo) => CicloDiLavoro == ciclo ? null
        : CicloDiLavoro is null ? ScegliIlCiclo
        : $"Questa segnalazione è del {ciclo}, ma lavori sul {CicloDiLavoro}: «Lavora su questo» sulla riga del {ciclo}.";

    /// <summary>
    /// «Lavora su questo» (26 ottobre): il committente sceglie il ciclo di lavoro; resta scelto anche dopo un riavvio, finché
    /// non ne sceglie un altro. Indice e mappa si rifanno sul ciclo nuovo, se l'AIP espanso era già caricato.
    /// </summary>
    public async Task<bool> LavoraSulCicloAsync(string ciclo)
    {
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (!File.Exists(Core.PdfAip.AipEspanso.File(espanso, ciclo)))
            return Rifiuta($"Nell'AIP espanso il {ciclo} non c'è: «Leggi» prima.");
        _cicloScelto = ciclo;
        _cicloSceltoLetto = true;
        Ricorda("ciclo-di-lavoro.txt", ciclo, "ciclo di lavoro");
        Registro.Scrivi("aip", $"ciclo di lavoro: {ciclo}");
        _righeDeiCicli = null;
        Rifiuto = null;
        if (StratoDellAip is not null && StratoDellAip.Ciclo != ciclo)
            return await MostraLAipAsync(true).ConfigureAwait(false);
        Avvisa();
        return true;
    }

    /// <summary>
    /// «Togli il 2611» (passo 8): toglie un ciclo che non è più in vigore — la sua cartella dei PDF e, se l'AIP espanso non è
    /// un repo, il suo modello. Solo su questo gesto, confermato: il Lab non toglie niente da solo.
    /// </summary>
    public bool TogliIlCiclo(string ciclo)
    {
        if (CartellaDeiPdf is not { } pdf)
            return Rifiuta("Manca la cartella dei PDF dell'AIP: sceglila nelle Impostazioni.");
        try
        {
            if (Core.PdfAip.CicliDellAip.Togli(pdf, CartellaDellAipEspanso, ciclo, GiornoDeiCicli()) is { } perche)
                return Rifiuta(perche);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"Non sono riuscito a togliere il {ciclo}: {e.Message}");
        }
        Registro.Scrivi("aip", $"tolto il ciclo {ciclo}: non più in vigore");
        if (UltimoAipEspanso?.Ciclo == ciclo) UltimoAipEspanso = null;
        _righeDeiCicli = null;
        Rifiuto = null;
        Avvisa();
        return true;
    }

    /// <summary>
    /// «Prendi i PDF da Download» (carta F6 §3): sposta i PDF dell'AIP dalla cartella Download in quella dei PDF. Solo su
    /// questo tasto, mai da solo.
    /// </summary>
    public async Task<bool> PrendiIPdfDaDownloadAsync()
    {
        if (CartellaDeiPdf is not { } cartella)
            return Rifiuta("Manca la cartella dei PDF dell'AIP: sceglila nelle Impostazioni.");
        try
        {
            var presa = await Task.Run(() => Core.PdfAip.PdfDellAip.PrendiDaDownload(Download, cartella)).ConfigureAwait(false);
            Registro.Scrivi("aip", $"presi da Download ({Download}): {presa.Nuovi.Count} nuovi, {presa.Sostituiti.Count} sostituiti");
            UltimaPresaDaDownload = presa;
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"Non sono riuscito a spostarli: {e.Message}");
        }
    }

    /// <summary>
    /// «Leggi» (pagina «Lettura e consegna», 22 ottobre): i PDF di un ciclo — la sua cartella <c>AIP 2611</c> — nel modello,
    /// scritto nella cartella del ciclo dell'AIP espanso. Senza ciclo, i PDF sciolti (<see cref="LeggiIPdfScioltiAsync"/>).
    /// <para>Il ciclo lo dicono le date delle pagine, non il nome della cartella né il giorno di oggi (committente, 22
    /// ottobre: il 2611 copiato fuori dalla sua cartella si era letto come 2610): pagine di un ciclo DOPO quello della
    /// cartella fermano la lettura prima di scrivere; nessuna pagina del ciclo (i PDF che hai non sono cambiati) si dice in
    /// <see cref="AvvisoDellaLettura"/> e si va avanti.</para>
    /// </summary>
    public async Task<bool> LeggiLAipAsync(string? ciclo)
    {
        if (string.IsNullOrWhiteSpace(ciclo))
            return await LeggiIPdfScioltiAsync().ConfigureAwait(false);
        if (StaLeggendoLAip)
            return false;
        if (CartellaDeiPdf is not { } pdf)
            return Rifiuta("Manca la cartella dei PDF dell'AIP: sceglila nelle Impostazioni.");
        if (Core.PdfAip.AipEspanso.PercheNonVa(CartellaDellAipEspanso, pdf) is { } perche)
            return Rifiuta(perche);
        var nome = ciclo.Trim();
        if (!Core.PdfAip.CicloAirac.Valido(nome))
            return Rifiuta($"«{nome}» non è un ciclo AIRAC: quattro cifre, l'anno e il numero (2611).");
        var cartella = Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, nome);
        if (!Directory.Exists(cartella))
            return Rifiuta(Core.PdfAip.EmendamentoDellAip.CicliNellaCartella(pdf) is { Count: > 0 } cicli
                ? $"Nella cartella dei PDF ci sono i cicli {string.Join(", ", cicli)}, non il {nome}."
                : $"Nella cartella dei PDF non c'è «AIP {nome}».");

        return await LeggiAsync(Core.PdfAip.PdfDellAip.Elenco(cartella), nome, sciolti: false).ConfigureAwait(false);
    }

    /// <summary>
    /// «Leggi e metti nel loro ciclo»: i PDF messi nella cartella dei PDF fuori dalle cartelle dei cicli. Il ciclo lo dicono
    /// le date delle pagine; letti e scritti nel modello, vanno nella loro cartella <c>AIP 2611</c>.
    /// </summary>
    public async Task<bool> LeggiIPdfScioltiAsync()
    {
        if (StaLeggendoLAip)
            return false;
        if (CartellaDeiPdf is not { } pdf)
            return Rifiuta("Manca la cartella dei PDF dell'AIP: sceglila nelle Impostazioni.");
        if (Core.PdfAip.AipEspanso.PercheNonVa(CartellaDellAipEspanso, pdf) is { } perche)
            return Rifiuta(perche);
        var sciolti = Core.PdfAip.CicliDellAip.PdfSciolti(pdf);
        if (sciolti.Count == 0)
            return Rifiuta("Nella cartella dei PDF non ci sono PDF fuori dalle cartelle dei cicli.");
        return await LeggiAsync(sciolti, null, sciolti: true).ConfigureAwait(false);
    }

    private async Task<bool> LeggiAsync(IReadOnlyList<string> file, string? ciclo, bool sciolti)
    {
        StaLeggendoLAip = true;
        AvvisoDellaLettura = null;
        Avanzamento = new Core.PdfAip.AvanzamentoDellaLettura(0, file.Count, "");
        InizioDellaLettura = DateTime.UtcNow;
        Avvisa();
        try
        {
            var espanso = CartellaDellAipEspanso!;
            var repo = Core.PdfAip.RepoDellAip.EUnRepo(espanso);
            // Il ramo prima della lettura lunga, quando si sa già il ciclo; per i PDF sciolti il ciclo si sa dopo, e allora
            // si guarda almeno che non ci sia niente di non registrato (non si cambierebbe ramo).
            if (repo && ciclo is not null)
            {
                if (await Core.PdfAip.RepoDellAip.SulRamoDelCicloAsync(Git, espanso, ciclo).ConfigureAwait(false) is { } noRamo)
                    return Rifiuta(noRamo);
            }
            else if (repo && (await Core.Git.RepoDelSector.LeggiAsync(Git, espanso, prendi: false).ConfigureAwait(false)).Stato is { Cambiati.Count: > 0 } sporco)
            {
                return Rifiuta($"Ci sono file cambiati e non registrati su {sporco.Ramo ?? "un commit staccato"}: registrali prima di leggere.");
            }

            var avanzamento = new RiferisciSubito<Core.PdfAip.AvanzamentoDellaLettura>(a =>
            {
                Avanzamento = a;
                AvvisaOgniTanto();
            });
            var lettura = await Task.Run(() => Core.PdfAip.AipEspanso.LeggiIPdf(file, avanzamento)).ConfigureAwait(false);

            var delle = lettura.CicloDellePagine;
            var ultima = lettura.UltimaData?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            if (ciclo is null)
            {
                if (delle is null)
                    return Rifiuta("Nessuna pagina di questi PDF ha una data: il ciclo non si riconosce. Mettili tu in una cartella «AIP <ciclo>» (AIP 2611).");
                if (Directory.Exists(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(CartellaDeiPdf!, delle)))
                    return Rifiuta($"Questi PDF sono del {delle} (la pagina più recente è del {ultima}), ma c'è già la cartella «AIP {delle}»: mettili lì dentro tu, o togli quella cartella. Non ho scritto niente.");
                ciclo = delle;
                if (repo && await Core.PdfAip.RepoDellAip.SulRamoDelCicloAsync(Git, espanso, ciclo).ConfigureAwait(false) is { } noRamo)
                    return Rifiuta(noRamo);
            }
            else if (delle is not null && string.CompareOrdinal(delle, ciclo) > 0)
            {
                return Rifiuta($"Le pagine di «AIP {ciclo}» arrivano al {ultima}, che è del ciclo {delle}: la cartella va chiamata «AIP {delle}». Non ho scritto niente.");
            }
            else if (delle is not null && string.CompareOrdinal(delle, ciclo) < 0)
            {
                AvvisoDellaLettura = $"Nessuna pagina è del {ciclo}: la più recente è del {ultima} ({delle}). Va bene se in questo ciclo i PDF che hai non sono cambiati.";
            }

            if (repo)
                Core.PdfAip.RepoDellAip.MettiIlGitIgnore(espanso);
            var nome = ciclo;
            var esito = await Task.Run(() => Core.PdfAip.AipEspanso.Componi(lettura, espanso, nome)).ConfigureAwait(false);
            Registro.Scrivi("aip", $"AIP espanso {nome}: {esito.Letti.Count} PDF letti su {esito.Pdf}, {esito.Quanti()}, {esito.Problemi.Count} segnalazioni{(esito.CorrezioniInParole() is { } c ? ", correzioni: " + c : "")} → {esito.Scritto}");
            if (sciolti)
            {
                if (Core.PdfAip.CicliDellAip.MettiNelCiclo(CartellaDeiPdf!, nome, file) is { } nonSpostati)
                    AvvisoDellaLettura = nonSpostati;
                else
                    Registro.Scrivi("aip", $"{file.Count} PDF sciolti messi in AIP {nome}");
            }
            UltimoAipEspanso = esito;
            MessaggioDellAip = Core.PdfAip.RepoDellAip.MessaggioProposto(esito);
            // La mappa e l'Indice si rifanno sulla lettura nuova, accesi o spenti com'erano (prova a schermo del 23 ottobre:
            // dopo «Rileggi» restavano quelli della lettura prima, finché una correzione non li ricaricava).
            if (StratoDellAip is not null)
            {
                var spento = !_mappe[SpazioDiLavoro.Aip].Accesi.Contains(Core.PdfAip.AipSullaMappa.Tipo.Id);
                if (await MostraLAipAsync(true).ConfigureAwait(false) && spento)
                    await MostraLAipAsync(false).ConfigureAwait(false);
            }
            Rifiuto = null;
            await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"Non sono riuscito a scrivere l'AIP espanso: {e.Message}");
        }
        catch (InvalidDataException e)
        {
            Registro.Errore("aip", e);
            return Rifiuta(e.Message);
        }
        finally
        {
            StaLeggendoLAip = false;
            Avanzamento = null;
            _righeDeiCicli = null;
            Avvisa();
        }
    }

    /// <summary>A che punto è la lettura dei PDF; null se non si sta leggendo.</summary>
    public Core.PdfAip.AvanzamentoDellaLettura? Avanzamento { get; private set; }

    /// <summary>Quando è cominciata la lettura di adesso (UTC), per stimare quanto manca.</summary>
    public DateTime InizioDellaLettura { get; private set; }

    /// <summary>
    /// Quanto manca, stimato dai PDF già letti; null all'inizio. I PDF non pesano tutti uguale (ENR 5.4 più di una carta):
    /// è un'idea, non una promessa.
    /// </summary>
    public TimeSpan? Manca => Avanzamento is { Fatti: > 4 } a && a.Fatti < a.Tutti
        ? TimeSpan.FromSeconds((DateTime.UtcNow - InizioDellaLettura).TotalSeconds / a.Fatti * (a.Tutti - a.Fatti))
        : null;

    /// <summary>Quel che l'ultima lettura ha da dire oltre all'esito: il ciclo senza pagine sue, i PDF non spostati.</summary>
    public string? AvvisoDellaLettura { get; private set; }

    private IReadOnlyList<Core.PdfAip.RigaDelCiclo>? _righeDeiCicli;
    private DateOnly _giornoDelleRighe;

    /// <summary>Una riga per ciclo (pagina «Lettura e consegna»); si rifà dopo una lettura, un emendamento, un ciclo tolto.</summary>
    public IReadOnlyList<Core.PdfAip.RigaDelCiclo> RigheDeiCicli
    {
        get
        {
            var oggi = GiornoDeiCicli();
            if (_righeDeiCicli is null || _giornoDelleRighe != oggi)
            {
                _righeDeiCicli = Core.PdfAip.CicliDellAip.Righe(CartellaDeiPdf, CartellaDellAipEspanso, oggi, CicloDiLavoro);
                _giornoDelleRighe = oggi;
            }
            return _righeDeiCicli;
        }
    }

    /// <summary>I PDF fuori dalle cartelle dei cicli (da leggere e mettere nel loro ciclo).</summary>
    public IReadOnlyList<string> PdfSciolti => Core.PdfAip.CicliDellAip.PdfSciolti(CartellaDeiPdf);

    private long _ultimoAvviso;

    /// <summary>Avvisa la pagina al massimo cinque volte al secondo: una lettura manda un avanzamento per PDF.</summary>
    private void AvvisaOgniTanto()
    {
        var ora = Environment.TickCount64;
        if (ora - Interlocked.Read(ref _ultimoAvviso) < 200)
            return;
        Interlocked.Exchange(ref _ultimoAvviso, ora);
        Avvisa();
    }

    /// <summary>Un IProgress che chiama subito, sul thread di chi riferisce (Progress&lt;T&gt; passerebbe da un contesto).</summary>
    private sealed class RiferisciSubito<T>(Action<T> riferisci) : IProgress<T>
    {
        public void Report(T value) => riferisci(value);
    }

    /// <summary>
    /// Apre un PDF dell'AIP nel browser di Windows alla pagina data (pagina «Lettura e consegna»: da dove viene una voce).
    /// Il PDF si cerca nella cartella del ciclo per nome; falso col perché se non c'è.
    /// </summary>
    public bool ApriIlPdf(string ciclo, string nomeDelFile, int pagina = 1)
    {
        if (CartellaDeiPdf is not { } pdf)
            return Rifiuta("Manca la cartella dei PDF dell'AIP.");
        var file = Core.PdfAip.PdfDellAip.Elenco(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, ciclo))
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), nomeDelFile, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return Rifiuta($"«{nomeDelFile}» non è nella cartella «AIP {ciclo}».");
        ApriIlPdfAllaPagina(new Uri($"{new Uri(file).AbsoluteUri}#page={Math.Max(1, pagina)}"));
        Registro.Scrivi("aip", $"aperto {nomeDelFile} a pagina {pagina}");
        return true;
    }

    /// <summary>
    /// Apre la pagina di una voce dell'AIP espanso («AD 2 LIRN 5-11»): il file e la posizione dall'indice delle pagine del
    /// ciclo. Falso col perché se la pagina non c'è.
    /// </summary>
    public bool ApriLaPaginaDellAip(string ciclo, string riferimento)
    {
        if (CartellaDellAipEspanso is not { } espanso || !File.Exists(Core.PdfAip.IndiceDellePagine.File(espanso, ciclo)))
            return Rifiuta($"Del {ciclo} non c'è l'indice delle pagine: leggi il ciclo.");
        var pagina = Core.PdfAip.IndiceDellePagine.Leggi(File.ReadAllText(Core.PdfAip.IndiceDellePagine.File(espanso, ciclo)))
            .FirstOrDefault(p => p.Reference == riferimento);
        return pagina is null
            ? Rifiuta($"La pagina {riferimento} non è nell'indice del {ciclo}.")
            : ApriIlPdf(ciclo, pagina.File, pagina.Position);
    }

    /// <summary>
    /// Come si apre un PDF a una pagina: di base con Edge (c'è su ogni Windows, e capisce <c>#page=</c>); i test ne mettono
    /// uno loro.
    /// </summary>
    public Action<Uri> ApriIlPdfAllaPagina { get; init; } = ApriConEdge;

    private static void ApriConEdge(Uri pdf)
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("msedge", $"\"{pdf.AbsoluteUri}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Senza Edge: il lettore di PDF di Windows, dalla prima pagina.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdf.LocalPath) { UseShellExecute = true })?.Dispose();
        }
    }

    /// <summary>Com'è andato l'ultimo «Applica l'emendamento».</summary>
    public Core.PdfAip.EsitoDellEmendamento? UltimoEmendamento { get; private set; }

    /// <summary>Sta applicando un emendamento.</summary>
    public bool StaApplicandoLEmendamento { get; private set; }

    /// <summary>Il PDF di un emendamento in Download (il più recente col nome «AMDT»), da proporre nel campo; null se non c'è.</summary>
    public string? EmendamentoInDownload =>
        Directory.Exists(Download)
            ? new DirectoryInfo(Download).EnumerateFiles("*.pdf", SearchOption.TopDirectoryOnly)
                .Where(f => f.Name.Contains("AMDT", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName
            : null;

    /// <summary>
    /// «Applica l'emendamento» (carta «L'AIP intero e gli emendamenti», passo 7): la cartella del ciclo nuovo dal PDF
    /// dell'AMDT AIRAC, accanto a quella del ciclo prima; poi il ciclo nuovo nell'AIP espanso, con le pagine cambiate.
    /// </summary>
    public async Task<bool> ApplicaLEmendamentoAsync(string? percorso)
    {
        if (StaApplicandoLEmendamento || StaLeggendoLAip)
            return false;
        if (CartellaDeiPdf is not { } pdf)
            return Rifiuta("Manca la cartella dei PDF dell'AIP: sceglila nelle Impostazioni.");
        var amdt = percorso?.Trim().Trim('"');
        if (string.IsNullOrEmpty(amdt) || !File.Exists(amdt))
            return Rifiuta("Scrivi il percorso del PDF dell'emendamento: l'AIP AIRAC AMDT scaricato da ENAV.");

        StaApplicandoLEmendamento = true;
        Avvisa();
        Core.PdfAip.EsitoDellEmendamento esito;
        try
        {
            esito = await Task.Run(() => Core.PdfAip.EmendamentoDellAip.Applica(pdf, amdt)).ConfigureAwait(false);
            UltimoEmendamento = esito;
            _righeDeiCicli = null;
            Registro.Scrivi("aip", esito.Fatto
                ? $"emendamento {esito.Numero}: AIP {esito.CicloPrima} → AIP {esito.CicloNuovo}, {esito.PagineTolte} pagine tolte, {esito.PagineMesse} messe; GEN 0.4: mancano {esito.Mancano.Count}, data diversa {esito.DataDiversa.Count}, in più {esito.InPiu.Count}"
                : $"emendamento {esito.Numero ?? amdt}: fermo, {esito.Bloccanti.Count} motivi; il primo: {esito.Bloccanti[0]}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'emendamento non si applica: {e.Message}");
        }
        finally
        {
            StaApplicandoLEmendamento = false;
            Avvisa();
        }

        if (!esito.Fatto)
            return Rifiuta(esito.Bloccanti[0]);
        Rifiuto = null;
        // Il ciclo nuovo nell'AIP espanso, subito: «Rispetto al <ciclo prima>» dice le pagine cambiate.
        return await LeggiLAipAsync(esito.CicloNuovo).ConfigureAwait(false);
    }

    /// <summary>
    /// L'ultimo confronto fra l'AIP espanso e il sector aperto (slice 1b e 1c): il ciclo da cui viene, che cosa si è
    /// confrontato (i CTR, le ATZ negli <c>.str</c>, le ATZ in <c>twrs.tfl</c>) e le righe.
    /// </summary>
    public (string Ciclo, Core.PdfAip.FamigliaDelConfronto Famiglia, IReadOnlyList<Core.PdfAip.DifferenzaDiForma> Esiti)? UltimoConfronto { get; private set; }

    /// <summary>
    /// «Confronta» (carta F6, slice 1b): gli spazi dell'AIP espanso di quella famiglia — il ciclo letto per ultimo, o il più
    /// recente nella cartella — contro le voci del sector aperto che la tengono. In sola lettura: il sector non si tocca.
    /// </summary>
    public async Task<bool> ConfrontaAsync(Core.PdfAip.FamigliaDelConfronto famiglia = Core.PdfAip.FamigliaDelConfronto.Ctr)
    {
        if (Sessione is null)
            return Rifiuta("Apri un sector prima: il confronto è fra l'AIP espanso e il sector aperto.");
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (CicloDiLavoroCon(espanso, out var noCiclo) is not { } ciclo)
            return Rifiuta(noCiclo!);
        try
        {
            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            var forme = Strati.SelectMany(s => s.Forme).ToList();
            var esiti = await Task.Run(() => Core.PdfAip.ConfrontoDelleForme.Di(famiglia, modello.Airspaces, forme)).ConfigureAwait(false);
            UltimoConfronto = (ciclo, famiglia, esiti);
            _aipDelConfronto = modello;
            Registro.Scrivi("aip", $"confronto {famiglia}, ciclo {ciclo}: " + string.Join(", ", esiti.GroupBy(e => e.Esito).Select(g => $"{g.Key} {g.Count()}")));
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'AIP espanso del ciclo {ciclo} non si legge: {e.Message}");
        }
    }

    /// <summary>
    /// L'AIP espanso sulla mappa (F6, prima della 2b): lo strato del ciclo più recente, sopra quelli del sector. Null finché
    /// non lo si chiede («Mostra sulla mappa» nel riquadro AIP).
    /// </summary>
    public Core.PdfAip.StratoDellAip? StratoDellAip { get; private set; }

    /// <summary>La voce dell'AIP scelta col clic sulla mappa (l'indice nello strato), o null.</summary>
    public int? SceltaDellAip { get; private set; }

    /// <summary>La voce scelta, per la scheda.</summary>
    public Core.PdfAip.VoceDellAip? VoceDellAipScelta =>
        SceltaDellAip is { } i && StratoDellAip is { } s && i >= 0 && i < s.Voci.Count ? s.Voci[i] : null;

    /// <summary>Quel che la mappa evidenzia: il record scelto, o la voce dell'AIP.</summary>
    public (string File, int Record)? SceltaSullaMappa =>
        Scelta ?? (SceltaDellAip is { } i && StratoDellAip is { } s ? (s.File, i) : null);

    /// <summary>Gli strati delle caselle: quelli della mappa dello spazio attivo (<see cref="StratiDi"/>).</summary>
    public IReadOnlyList<StratoDellaMappa> StratiDaMostrare => StratiDi(SpazioAttivo);

    /// <summary>
    /// «Mostra sulla mappa» (F6): legge l'AIP espanso del ciclo — quello in vigore, o quello scelto (passo 8) — e lo
    /// mette nello strato «AIP espanso», acceso. Le attese stanno dove il sector ha il loro punto. Spento, lo strato resta in
    /// memoria fino alla prossima lettura.
    /// </summary>
    public async Task<bool> MostraLAipAsync(bool mostra)
    {
        var id = Core.PdfAip.AipSullaMappa.Tipo.Id;
        if (!mostra)
        {
            // R4 (D15): lo strato dell'AIP sta nelle mappe dell'AIP espanso e del Confronti, non in quella del sector.
            _mappe[SpazioDiLavoro.Aip].Accesi.Remove(id);
            _mappe[SpazioDiLavoro.Confronti].Accesi.Remove(id);
            SceltaDellAip = null;
            Avvisa();
            return true;
        }
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (CicloDiLavoroCon(espanso, out var noCiclo) is not { } ciclo)
            return Rifiuta(noCiclo!);
        // In fila (22 ottobre): l'Indice carica lo strato entrando, e la sua casella lo ricarica; due caricamenti insieme
        // toccavano le stesse collezioni da due thread («Operations that change non-concurrent collections…», 2 volte su 5).
        await _caricaLAip.WaitAsync().ConfigureAwait(false);
        try
        {
            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            var catalogo = CatalogoScelto;
            var confini = ConfiniDelSector();
            StratoDellAip = await Task.Run(() => Core.PdfAip.AipSullaMappa.Di(modello, ciclo, nome => catalogo?.Cerca(nome)?.Posizione, confini)).ConfigureAwait(false);
            _modelloSullaMappa = modello;
            _correzioniSullaMappa = LeCorrezioniOppureNiente(espanso, modello.State);
            SceltaDellAip = null;
            _versioniDegliStrati[id] = ++VersioneDellaGeometria;
            _mappe[SpazioDiLavoro.Aip].Accesi.Add(id);
            _mappe[SpazioDiLavoro.Confronti].Accesi.Add(id);
            Registro.Scrivi("aip", $"AIP espanso {ciclo} sulla mappa: {StratoDellAip.Strato.Forme.Count} forme");
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'AIP espanso del ciclo {ciclo} non si legge: {e.Message}");
        }
        finally
        {
            _caricaLAip.Release();
        }
    }

    private readonly SemaphoreSlim _caricaLAip = new(1, 1);

    // --- lo spazio Confronti (riordino R6, D17 e D18) -------------------------------------------------------------------

    private readonly Dictionary<Core.PdfAip.SezioneDelConfronto, (string Ciclo, IReadOnlyList<Core.PdfAip.RigaDelConfronto> Righe)> _confronti = [];

    /// <summary>Le sezioni già confrontate, col ciclo e le righe: restano finché la cartella non si chiude o non si rifanno.</summary>
    public IReadOnlyDictionary<Core.PdfAip.SezioneDelConfronto, (string Ciclo, IReadOnlyList<Core.PdfAip.RigaDelConfronto> Righe)> Confronti => _confronti;

    /// <summary>La sezione aperta nel Confronto.</summary>
    public Core.PdfAip.SezioneDelConfronto? SezioneDelConfronto { get; private set; }

    /// <summary>La riga scelta della sezione aperta.</summary>
    public int? RigaDelConfrontoScelta { get; private set; }

    /// <summary>Sta confrontando una sezione.</summary>
    public bool StaConfrontando { get; private set; }

    /// <summary>
    /// La coppia della riga scelta sulla mappa del Confronto: il record del sector e la voce dell'AIP (D18), ognuno nel suo
    /// colore. Cresce <see cref="VersioneDellaCoppia"/> a ogni scelta.
    /// </summary>
    public ((string File, int Record)? Sector, (string File, int Record)? Aip) CoppiaDelConfronto { get; private set; }

    public int VersioneDellaCoppia { get; private set; }

    /// <summary>Le righe erano del sector di prima: un altro sector (o nessuno) si confronta da capo.</summary>
    private void ScordaIConfronti()
    {
        // Anche il Testo: il file aperto, la riga e la bozza erano di quel sector.
        FileDelTesto = null;
        RigaDelTesto = null;
        Bozza = null;
        _confronti.Clear();
        SezioneDelConfronto = null;
        RigaDelConfrontoScelta = null;
        CoppiaDelConfronto = default;
        VersioneDellaCoppia++;
    }

    /// <summary>
    /// Confronta una sezione (R6): il confronto di oggi di quella sezione (F6, F7), in sola lettura, e le sue righe portate
    /// a una forma sola. Lo strato dell'AIP serve alla mappa e alla scheda della voce: se non c'è, si carica.
    /// </summary>
    public async Task<bool> ConfrontaLaSezioneAsync(Core.PdfAip.SezioneDelConfronto sezione)
    {
        if (StaConfrontando)
            return false;
        StaConfrontando = true;
        SezioneDelConfronto = sezione;
        RigaDelConfrontoScelta = null;
        Avvisa();
        try
        {
            if (StratoDellAip is null && !await MostraLAipAsync(true).ConfigureAwait(false))
                return false;
            bool fatto = sezione switch
            {
                Core.PdfAip.SezioneDelConfronto.Ctr => await ConfrontaAsync(Core.PdfAip.FamigliaDelConfronto.Ctr).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Atz => await ConfrontaAsync(Core.PdfAip.FamigliaDelConfronto.Atz).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.AtzDelleTorri => await ConfrontaAsync(Core.PdfAip.FamigliaDelConfronto.AtzDelleTorri).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.AreeEnr5 => await ConfrontaAsync(Core.PdfAip.FamigliaDelConfronto.AreeEnr5).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Rotte => await ConfrontaEnr3Async(rotte: true).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Attese => await ConfrontaEnr3Async(rotte: false).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Radioassistenze => await ConfrontaEnr4Async(radioassistenze: true).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Punti => await ConfrontaEnr4Async(radioassistenze: false).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Scali => await ConfrontaAd2Async(Core.PdfAip.ParteDiAd2.Scali).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Piste => await ConfrontaAd2Async(Core.PdfAip.ParteDiAd2.Piste).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Frequenze => await ConfrontaAd2Async(Core.PdfAip.ParteDiAd2.Frequenze).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.RadioassistenzeAd2 => await ConfrontaAd2Async(Core.PdfAip.ParteDiAd2.Radioassistenze).ConfigureAwait(false),
                Core.PdfAip.SezioneDelConfronto.Confini => ConfrontaIConfini(),
                _ => await ConfrontaLeProcedureAsync().ConfigureAwait(false),
            };
            if (!fatto)
                return false;
            if (RigheDalleUltime(sezione) is not { } righe)
                return Rifiuta("Il confronto non ha dato righe.");
            _confronti[sezione] = righe;
            MostraSoloLaSezione(sezione);
            return true;
        }
        finally
        {
            StaConfrontando = false;
            Avvisa();
        }
    }

    /// <summary>
    /// Le righe di una sezione dall'ultimo confronto, se l'ultimo confronto di quel genere è proprio di questa sezione
    /// (R7: i gesti lo rifanno, e le righe devono essere quelle di adesso); null se no.
    /// </summary>
    private (string Ciclo, IReadOnlyList<Core.PdfAip.RigaDelConfronto> Righe)? RigheDalleUltime(Core.PdfAip.SezioneDelConfronto sezione)
    {
        Core.PdfAip.FamigliaDelConfronto? famiglia = sezione switch
        {
            Core.PdfAip.SezioneDelConfronto.Ctr => Core.PdfAip.FamigliaDelConfronto.Ctr,
            Core.PdfAip.SezioneDelConfronto.Atz => Core.PdfAip.FamigliaDelConfronto.Atz,
            Core.PdfAip.SezioneDelConfronto.AtzDelleTorri => Core.PdfAip.FamigliaDelConfronto.AtzDelleTorri,
            Core.PdfAip.SezioneDelConfronto.AreeEnr5 => Core.PdfAip.FamigliaDelConfronto.AreeEnr5,
            _ => null,
        };
        if (famiglia is { } f)
            return UltimoConfronto is { } c && c.Famiglia == f
                ? (c.Ciclo, Core.PdfAip.ConfrontiDelleSezioni.Da(c.Esiti, aree: f == Core.PdfAip.FamigliaDelConfronto.AreeEnr5))
                : null;
        Core.PdfAip.ParteDiAd2? parte = sezione switch
        {
            Core.PdfAip.SezioneDelConfronto.Scali => Core.PdfAip.ParteDiAd2.Scali,
            Core.PdfAip.SezioneDelConfronto.Piste => Core.PdfAip.ParteDiAd2.Piste,
            Core.PdfAip.SezioneDelConfronto.Frequenze => Core.PdfAip.ParteDiAd2.Frequenze,
            Core.PdfAip.SezioneDelConfronto.RadioassistenzeAd2 => Core.PdfAip.ParteDiAd2.Radioassistenze,
            _ => null,
        };
        if (parte is { } p)
            return UltimoConfrontoDiAd2 is { } a2 && a2.Parte == p ? (a2.Ciclo, Core.PdfAip.ConfrontiDelleSezioni.Da(a2.Esiti)) : null;
        return sezione switch
        {
            Core.PdfAip.SezioneDelConfronto.Confini =>
                UltimoConfrontoDeiConfini is { } cf ? (cf.Ciclo, Core.PdfAip.ConfrontiDelleSezioni.Da(cf.Esiti)) : null,
            Core.PdfAip.SezioneDelConfronto.Rotte or Core.PdfAip.SezioneDelConfronto.Attese =>
                UltimoConfrontoDiEnr3 is { } e3 && e3.Rotte == (sezione == Core.PdfAip.SezioneDelConfronto.Rotte)
                    ? (e3.Ciclo, Core.PdfAip.ConfrontiDelleSezioni.Da(e3.Esiti)) : null,
            Core.PdfAip.SezioneDelConfronto.Radioassistenze or Core.PdfAip.SezioneDelConfronto.Punti =>
                UltimoConfrontoDiEnr4 is { } e4 && e4.Radioassistenze == (sezione == Core.PdfAip.SezioneDelConfronto.Radioassistenze)
                    ? (e4.Ciclo, Core.PdfAip.ConfrontiDelleSezioni.Da(e4.Esiti)) : null,
            _ => UltimoConfrontoDelleProcedure is { } pr ? (pr.Ciclo, Core.PdfAip.ConfrontiDelleSezioni.Da(pr.Esiti)) : null,
        };
    }

    /// <summary>Vero se l'ultimo confronto del suo genere è di questa sezione: allora i gesti si possono fare.</summary>
    public bool SezioneViva(Core.PdfAip.SezioneDelConfronto sezione) => _confronti.ContainsKey(sezione) && RigheDalleUltime(sezione) is not null;

    /// <summary>
    /// Le righe di una sezione: quelle di adesso se il suo confronto è l'ultimo (dopo un gesto cambiano), se no quelle
    /// ricordate. Null se la sezione non è stata confrontata.
    /// </summary>
    public (string Ciclo, IReadOnlyList<Core.PdfAip.RigaDelConfronto> Righe)? RigheDellaSezione(Core.PdfAip.SezioneDelConfronto sezione)
    {
        if (!_confronti.TryGetValue(sezione, out var ricordate))
            return null;
        if (RigheDalleUltime(sezione) is { } vive)
        {
            _confronti[sezione] = vive;
            return vive;
        }
        return ricordate;
    }

    /// <summary>Riapre una sezione già confrontata, con le sue righe di prima.</summary>
    public void ApriLaSezioneDelConfronto(Core.PdfAip.SezioneDelConfronto sezione)
    {
        if (!_confronti.ContainsKey(sezione) || SezioneDelConfronto == sezione)
            return;
        SezioneDelConfronto = sezione;
        RigaDelConfrontoScelta = null;
        MostraSoloLaSezione(sezione);
        Avvisa();
    }

    /// <summary>
    /// Sulla mappa del Confronto solo la sezione aperta (committente, 20 ottobre: con tutto acceso la mappa non si legge):
    /// le coste, gli strati del sector che la tengono, e dello strato dell'AIP i soli generi della sezione. Il resto si
    /// riaccende dalle caselle degli strati.
    /// </summary>
    private void MostraSoloLaSezione(Core.PdfAip.SezioneDelConfronto sezione)
    {
        var mappa = _mappe[SpazioDiLavoro.Confronti];
        var aip = Core.PdfAip.AipSullaMappa.Tipo.Id;
        mappa.Accesi.Clear();
        mappa.Accesi.Add(StratiDellaMappa.Sfondo.Id);
        foreach (var strato in Core.PdfAip.ConfrontiDelleSezioni.StratiDelSector(sezione))
            mappa.Accesi.Add(strato);
        mappa.Spenti.RemoveWhere(s => s.StartsWith(ChiaveDelGenere(aip, ""), StringComparison.Ordinal));
        if (StratoDellAip is { } stratoDellAip)
        {
            mappa.Accesi.Add(aip);
            // I confini (R9) stanno in aree di ogni genere: si tengono quelli delle aree della sezione.
            var tenuti = sezione == Core.PdfAip.SezioneDelConfronto.Confini && UltimoConfrontoDeiConfini is { } confini
                ? [.. confini.Esiti.Select(e => Core.PdfAip.AipSullaMappa.Genere(e.Tipo)).Distinct()]
                : Core.PdfAip.ConfrontiDelleSezioni.GeneriDellAip(sezione);
            foreach (var genere in GeneriDellaMappa.Di(stratoDellAip.Strato).Where(g => !tenuti.Contains(g.Id)))
                mappa.Spenti.Add(ChiaveDelGenere(aip, genere.Id));
        }
        mappa.VersioneDeiSpenti++;
        mappa.VersioneDellaVista++;
    }

    /// <summary>
    /// Sceglie una riga del Confronto: il record del sector nella Scheda e la voce dell'AIP nella sua, tutti e due sulla
    /// mappa del Confronto, nei loro colori, inquadrati insieme (D18).
    /// </summary>
    public void ScegliLaRigaDelConfronto(int indice)
    {
        if (SezioneDelConfronto is not { } sezione || RigheDellaSezione(sezione) is not { } confronto
            || indice < 0 || indice >= confronto.Righe.Count)
            return;
        var riga = confronto.Righe[indice];
        RigaDelConfrontoScelta = indice;
        (string File, int Record)? delSector = riga.File is { } file && riga.Record is { } record ? (file, record) : null;
        if (delSector is { } s)
            Scegli(s.File, s.Record);
        else
            Scelta = null;
        int? voce = StratoDellAip is { } strato ? Core.PdfAip.ConfrontiDelleSezioni.VoceDelNome(strato.Voci, riga.Aip) : null;
        SceltaDellAip = voce;
        CoppiaDelConfronto = (delSector, voce is { } v && StratoDellAip is { } aip ? (aip.File, v) : null);
        VersioneDellaCoppia++;
        Avvisa();
    }

    // --- dal sector all'AIP espanso (riordino R9, D20) -----------------------------------------------------------------

    /// <summary>L'ultimo confronto dei confini (R9): le aree dell'AIP espanso coi tratti di confine o di costa, del ciclo sulla mappa.</summary>
    public (string Ciclo, IReadOnlyList<Core.PdfAip.ConfineDellAip> Esiti)? UltimoConfrontoDeiConfini { get; private set; }

    /// <summary>
    /// I confini (R9): le aree dell'AIP espanso sulla mappa (già corretto) coi tratti che l'AIP nomina senza disegnarli,
    /// contro il tracciato di <c>itgeo.geo</c>. In sola lettura.
    /// </summary>
    private bool ConfrontaIConfini()
    {
        if (StratoDellAip is not { } strato || _modelloSullaMappa is not { } modello)
            return Rifiuta("Accendi l'AIP espanso sulla mappa prima.");
        UltimoConfrontoDeiConfini = (strato.Ciclo, Core.PdfAip.ConfiniNellAip.Righe(modello, _correzioniSullaMappa, ConfiniDelSector()));
        Registro.Scrivi("aip", $"confini dell'AIP {strato.Ciclo}: {UltimoConfrontoDeiConfini.Value.Esiti.Count} aree");
        return true;
    }

    /// <summary>
    /// «Porta nell'AIP espanso» (R9, D20): le aree date coi tratti di confine e di costa fatti dei punti di <c>itgeo.geo</c>,
    /// come correzioni dell'AIP espanso (una per area, nel file delle correzioni, un salvataggio solo). Si ritirano come le
    /// altre, dalla scheda dell'area. Le aree che non si tracciano si dicono e restano.
    /// </summary>
    public async Task<bool> PortaIConfiniNellAipAsync(IReadOnlyList<string> chiavi)
    {
        ArgumentNullException.ThrowIfNull(chiavi);
        if (ConfiniDelSector() is not { } tracciatore)
            return Rifiuta("Il sector non ha GEO/itgeo.geo: non c'è da dove prendere i confini.");
        var saltate = new List<string>();
        bool fatto = await CorreggiLAipAsync((modello, file, ciclo) =>
        {
            Core.PdfAip.CorrezioneFatta? fatta = null;
            var portate = new List<string>();
            saltate.Clear();
            foreach (var chiave in chiavi)
            {
                var corrente = fatta?.Modello ?? modello;
                if (corrente.Airspaces.FirstOrDefault(a => a.Key == chiave) is not { } area)
                {
                    saltate.Add($"{chiave}: non è nell'AIP espanso del ciclo {ciclo}");
                    continue;
                }
                var (tracciata, perche) = Core.PdfAip.ConfiniNellAip.Tracciata(area, tracciatore);
                if (tracciata is null)
                {
                    saltate.Add(perche!);
                    continue;
                }
                var (questa, no) = Core.PdfAip.CorrezioniDellAip.Correggi(corrente, fatta?.Correzioni ?? file, chiave, tracciata,
                    Core.PdfAip.ConfiniNellAip.Perche(area), Core.PdfAip.ConfiniNellAip.Fonte, ciclo, Oggi);
                if (questa is null)
                {
                    saltate.Add(no!);
                    continue;
                }
                fatta = questa;
                portate.Add(chiave);
            }
            if (fatta is null)
                return (null, saltate.Count > 0 ? string.Join(" · ", saltate) : "Non c'è nessun confine da portare.");
            // Una sola: il messaggio della correzione (chiave, perché, fonte); più d'una: quante e da dove.
            return (portate.Count == 1 ? fatta : fatta with { Messaggio = $"Confini dal sector nell'AIP espanso: {portate.Count} aree ({Core.PdfAip.ConfiniNellAip.Fonte})" }, null);
        }).ConfigureAwait(false);
        if (!fatto)
            return false;
        ConfrontaIConfini();
        if (saltate.Count > 0)
            Rifiuto = string.Join(" · ", saltate);
        Avvisa();
        return true;
    }

    /// <summary>Il modello da cui viene <see cref="StratoDellAip"/>: le voci dello strato hanno il suo ordine (prima le aree).</summary>
    private global::Aip.AipDocument? _modelloSullaMappa;

    /// <summary>Il file delle correzioni letto con lo strato, per dire nella scheda se un'area è corretta.</summary>
    private global::Aip.AipCorrectionFile? _correzioniSullaMappa;

    private global::Aip.AipCorrectionFile? LeCorrezioniOppureNiente(string espanso, string stato)
    {
        try
        {
            return Core.PdfAip.CorrezioniDellAip.Leggi(espanso, stato);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Registro.Errore("aip", e);
            return null;
        }
    }

    /// <summary>L'area dell'AIP espanso scelta sulla mappa (le aree sono le prime voci dello strato), o null.</summary>
    public global::Aip.Airspace? AreaDellAipScelta =>
        SceltaDellAip is { } i && _modelloSullaMappa is { } m && i >= 0 && i < m.Airspaces.Count ? m.Airspaces[i] : null;

    /// <summary>La correzione in piedi che ha fatto l'area scelta, o null se è com'è nel PDF.</summary>
    public global::Aip.AipCorrection? CorrezioneDellAreaScelta =>
        AreaDellAipScelta is { } area && _correzioniSullaMappa is { } file ? Core.PdfAip.CorrezioniDellAip.Attiva(file, area.Key) : null;

    /// <summary>
    /// La voce del modello scelta sulla mappa che non è un'area (slice 3: punti, radioassistenze, luci, pericoli, scali, rotte,
    /// attese), col suo tipo; null se non c'è o se è un'area (quella ha <see cref="AreaDellAipScelta"/>).
    /// </summary>
    public (global::Aip.CorrectionKind Tipo, object Voce)? VoceDelModelloScelta =>
        VoceDellAipScelta is { Tipo: { } tipo and not global::Aip.CorrectionKind.Airspace, Chiave: { } chiave } && _modelloSullaMappa is { } m
        && Core.PdfAip.CorrezioniDellAip.Voci(m, tipo).FirstOrDefault(v => global::Aip.AipCorrections.KeyOf(v) == chiave) is { } voce
            ? (tipo, voce)
            : null;

    /// <summary>La correzione in piedi che ha fatto la voce scelta (non un'area), o null.</summary>
    public global::Aip.AipCorrection? CorrezioneDellaVoceScelta =>
        VoceDelModelloScelta is var (tipo, voce) && _correzioniSullaMappa is { } file
            ? Core.PdfAip.CorrezioniDellAip.Attiva(file, tipo, global::Aip.AipCorrections.KeyOf(voce))
            : null;

    /// <summary>
    /// «Correggi» (con <paramref name="chiave"/>) o «Aggiungi» (senza) una voce che non è un'area (slice 3): i campi del
    /// modulo (<see cref="Core.PdfAip.ModuloDellaVoce"/>) diventano la voce.
    /// </summary>
    public Task<bool> CorreggiLaVoceAsync(global::Aip.CorrectionKind tipo, string? chiave, IReadOnlyDictionary<string, string?> valori, string? perche, string? fonte)
    {
        string? nuova = null;
        return CorreggiLAipAsync((modello, file, ciclo) =>
        {
            var prima = chiave is null ? null : Core.PdfAip.CorrezioniDellAip.Voci(modello, tipo).FirstOrDefault(v => global::Aip.AipCorrections.KeyOf(v) == chiave);
            if (chiave is not null && prima is null)
                return (null, $"{chiave}: non è nell'AIP espanso del ciclo {ciclo}.");
            var (voce, no) = Core.PdfAip.ModuloDellaVoce.Voce(tipo, valori, prima);
            if (voce is null)
                return (null, no);
            nuova = global::Aip.AipCorrections.KeyOf(voce);
            return chiave is null
                ? Core.PdfAip.CorrezioniDellAip.Aggiungi(modello, file, tipo, voce, perche, fonte, ciclo, Oggi)
                : Core.PdfAip.CorrezioniDellAip.Correggi(modello, file, tipo, chiave, voce, perche, fonte, ciclo, Oggi);
        }, sceglie: () => nuova is null ? null : (tipo, nuova));
    }

    /// <summary>«Togli» una voce che non è un'area (slice 3).</summary>
    public Task<bool> TogliLaVoceAsync(global::Aip.CorrectionKind tipo, string chiave, string? perche, string? fonte) =>
        CorreggiLAipAsync((modello, file, ciclo) => Core.PdfAip.CorrezioniDellAip.Togli(modello, file, tipo, chiave, perche, fonte, ciclo, Oggi));

    /// <summary>Vero mentre il Lab scrive una correzione nell'AIP espanso.</summary>
    public bool StaCorreggendoLAip { get; private set; }

    /// <summary>
    /// «Correggi» (con <paramref name="chiave"/>) o «Aggiungi un'area» (senza), dalla scheda della mappa (carta «Le correzioni
    /// dell'AIP espanso», slice 2): il modulo diventa un'area, la correzione va nel file e l'area nel modello del ciclo.
    /// </summary>
    public Task<bool> CorreggiLAreaAsync(string? chiave, Core.PdfAip.ModuloDellArea modulo, string? perche, string? fonte) =>
        CorreggiLAipAsync((modello, file, ciclo) =>
        {
            var prima = chiave is null ? null : modello.Airspaces.FirstOrDefault(a => a.Key == chiave);
            if (chiave is not null && prima is null)
                return (null, $"{chiave}: non è nell'AIP espanso del ciclo {ciclo}.");
            var (area, no) = Core.PdfAip.CorrezioniDellAip.Area(modulo, prima);
            if (area is null)
                return (null, no);
            return chiave is null
                ? Core.PdfAip.CorrezioniDellAip.Aggiungi(modello, file, area, perche, fonte, ciclo, Oggi)
                : Core.PdfAip.CorrezioniDellAip.Correggi(modello, file, chiave, area, perche, fonte, ciclo, Oggi);
        }, sceglie: () => (global::Aip.CorrectionKind.Airspace, string.IsNullOrWhiteSpace(modulo.Nome) ? modulo.Identificativo.Trim() : $"{modulo.Identificativo.Trim()} {modulo.Nome.Trim()}"));

    /// <summary>«Togli»: l'area esce dall'AIP espanso, con una correzione (o ritirando l'aggiunta che l'aveva messa).</summary>
    public Task<bool> TogliLAreaAsync(string chiave, string? perche, string? fonte) =>
        CorreggiLAipAsync((modello, file, ciclo) => Core.PdfAip.CorrezioniDellAip.Togli(modello, file, chiave, perche, fonte, ciclo, Oggi));

    /// <summary>«Ritira la correzione»: la voce torna com'è nel PDF.</summary>
    public Task<bool> RitiraLaCorrezioneAsync(string id) =>
        CorreggiLAipAsync((modello, file, _) => Core.PdfAip.CorrezioniDellAip.Ritira(modello, file, id), sceglieLaStessa: true);

    private static DateOnly Oggi => DateOnly.FromDateTime(DateTime.Now);

    private async Task<bool> CorreggiLAipAsync(Func<global::Aip.AipDocument, global::Aip.AipCorrectionFile, string, (Core.PdfAip.CorrezioneFatta? Fatta, string? Perche)> gesto,
        Func<(global::Aip.CorrectionKind Tipo, string Chiave)?>? sceglie = null, bool sceglieLaStessa = false)
    {
        if (StaCorreggendoLAip)
            return false;
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (StratoDellAip is not { } strato)
            return Rifiuta("Accendi l'AIP espanso sulla mappa prima: le correzioni si fanno dalla sua scheda.");
        var ciclo = strato.Ciclo;
        var sceltaDiPrima = VoceDellAipScelta is { Tipo: { } tipoDiPrima, Chiave: { } chiaveDiPrima } ? (tipoDiPrima, chiaveDiPrima) : ((global::Aip.CorrectionKind, string)?)null;

        StaCorreggendoLAip = true;
        Avvisa();
        try
        {
            // Come «Leggi»: nel repo si scrive sul ramo del ciclo, mai su main.
            if (Core.PdfAip.RepoDellAip.EUnRepo(espanso)
                && await Core.PdfAip.RepoDellAip.SulRamoDelCicloAsync(Git, espanso, ciclo).ConfigureAwait(false) is { } noRamo)
                return Rifiuta(noRamo);

            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            var file = Core.PdfAip.CorrezioniDellAip.Leggi(espanso, modello.State);
            var (fatta, perche) = gesto(modello, file, ciclo);
            if (fatta is null)
                return Rifiuta(perche ?? "La correzione non si può fare.");

            await Task.Run(() => Core.PdfAip.CorrezioniDellAip.Scrivi(espanso, ciclo, fatta)).ConfigureAwait(false);
            Registro.Scrivi("aip", fatta.Messaggio);
            MessaggioDellAip = fatta.Messaggio;
            await MostraLAipAsync(true).ConfigureAwait(false);
            // La scheda resta sull'area: quella nuova (Correggi, Aggiungi), o la stessa tornata com'era (Ritira).
            var daScegliere = sceglie?.Invoke() ?? (sceglieLaStessa ? sceltaDiPrima : null);
            if (daScegliere is var (tipoScelto, chiaveScelta) && StratoDellAip is { } nuovo
                && nuovo.Voci.ToList().FindIndex(v => v.Tipo == tipoScelto && v.Chiave == chiaveScelta) is var i and >= 0)
                SceltaDellAip = i;
            Rifiuto = null;
            await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"Non sono riuscito a scrivere la correzione: {e.Message}");
        }
        finally
        {
            StaCorreggendoLAip = false;
            Avvisa();
        }
    }

    /// <summary>L'ultimo confronto di ENR 3 (slice 2b): il ciclo, se erano le rotte o le attese, e le righe.</summary>
    public (string Ciclo, bool Rotte, IReadOnlyList<Core.PdfAip.DifferenzaDiEnr3> Esiti)? UltimoConfrontoDiEnr3 { get; private set; }

    /// <summary>
    /// «Confronta le rotte» / «le attese» (carta F6, slice 2b): le rotte di ENR 3.2 dell'AIP espanso con le aerovie di
    /// <c>itawlow.lairway</c>, o le attese di ENR 3.4.1 con <c>HOLDENR.hold</c>. In sola lettura.
    /// </summary>
    public async Task<bool> ConfrontaEnr3Async(bool rotte)
    {
        if (Sessione is not { } sessione)
            return Rifiuta("Apri un sector prima: il confronto è fra l'AIP espanso e il sector aperto.");
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (CicloDiLavoroCon(espanso, out var noCiclo) is not { } ciclo)
            return Rifiuta(noCiclo!);
        try
        {
            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            if ((rotte ? modello.Routes?.Count : modello.Holdings?.Count) is not > 0)
                return Rifiuta($"L'AIP espanso del ciclo {ciclo} non ha {(rotte ? "rotte" : "attese")}: servono i PDF di ENR 3 letti.");
            var catalogo = CatalogoScelto;
            var esiti = await Task.Run(() => rotte
                ? Core.PdfAip.ConfrontoDiEnr3.Rotte(modello.Routes!, sessione, nome => catalogo?.Cerca(nome)?.Posizione)
                : Core.PdfAip.ConfrontoDiEnr3.Attese(modello.Holdings!, sessione)).ConfigureAwait(false);
            UltimoConfrontoDiEnr3 = (ciclo, rotte, esiti);
            _aipDiEnr3 = modello;
            if (!rotte)
                ProposteDelleAttese = Core.PdfAip.AtteseNelSector.Proposte(modello.Holdings!, sessione, nome => catalogo?.Cerca(nome)?.Posizione);
            Registro.Scrivi("aip", $"confronto {(rotte ? "rotte" : "attese")}, ciclo {ciclo}: " + string.Join(", ", esiti.GroupBy(e => e.Esito).Select(g => $"{g.Key} {g.Count()}")));
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'AIP espanso del ciclo {ciclo} non si legge: {e.Message}");
        }
    }

    /// <summary>L'ultimo confronto delle procedure (carta F7, slice 7d): il ciclo e le righe.</summary>
    public (string Ciclo, IReadOnlyList<Core.PdfAip.DifferenzaDiProcedura> Esiti)? UltimoConfrontoDelleProcedure { get; private set; }

    /// <summary>
    /// «Confronta SID e STAR» (carta F7, slice 7d): le procedure di AD 2 dell'AIP espanso contro i <c>.sid</c> e gli
    /// <c>.str</c> degli scali, per nome e per percorso. In sola lettura.
    /// </summary>
    public async Task<bool> ConfrontaLeProcedureAsync()
    {
        if (Sessione is not { } sessione)
            return Rifiuta("Apri un sector prima: il confronto è fra l'AIP espanso e il sector aperto.");
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (CicloDiLavoroCon(espanso, out var noCiclo) is not { } ciclo)
            return Rifiuta(noCiclo!);
        try
        {
            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            if (modello.Procedures is not { Count: > 0 } procedure)
                return Rifiuta($"L'AIP espanso del ciclo {ciclo} non ha procedure: servono i PDF di AD 2 (i file 4 e 6) letti.");
            var catalogo = CatalogoScelto;
            var punti = PuntiDellAip(modello).ToDictionary(x => x.Key, x => x.Value.Position, StringComparer.Ordinal);
            var abbinamenti = new Dictionary<(string File, int Record), global::Aip.Procedure?[]>();
            var esiti = await Task.Run(() => Core.PdfAip.ConfrontoDelleProcedure.Di(procedure, sessione,
                nome => catalogo?.Cerca(nome)?.Posizione, nome => punti.TryGetValue(nome, out var p) ? p : null, abbinamenti)).ConfigureAwait(false);
            UltimoConfrontoDelleProcedure = (ciclo, esiti);
            _aipDelleProcedure = modello;
            _abbinamentiDelleProcedure = abbinamenti;
            Registro.Scrivi("aip", $"confronto procedure, ciclo {ciclo}: " + string.Join(", ", esiti.GroupBy(e => e.Esito).Select(g => $"{g.Key} {g.Count()}")));
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'AIP espanso del ciclo {ciclo} non si legge: {e.Message}");
        }
    }

    /// <summary>Il modello da cui viene <see cref="UltimoConfrontoDelleProcedure"/>: i gesti della 7e ne prendono le procedure.</summary>
    private global::Aip.AipDocument? _aipDelleProcedure;

    /// <summary>Per ogni voce del sector del confronto, le procedure dell'AIP di ogni parte del nome (la SID e la transizione).</summary>
    private IReadOnlyDictionary<(string File, int Record), global::Aip.Procedure?[]> _abbinamentiDelleProcedure =
        new Dictionary<(string File, int Record), global::Aip.Procedure?[]>();

    /// <summary>Dopo un gesto della 7e il confronto delle procedure si rifà sul sector di adesso.</summary>
    private void RifaiIlConfrontoDelleProcedure()
    {
        if (Sessione is not { } sessione || UltimoConfrontoDelleProcedure is not { } confronto || _aipDelleProcedure is not { } aip)
            return;
        var catalogo = CatalogoScelto;
        var punti = PuntiDellAip(aip);
        var abbinamenti = new Dictionary<(string File, int Record), global::Aip.Procedure?[]>();
        UltimoConfrontoDelleProcedure = (confronto.Ciclo, Core.PdfAip.ConfrontoDelleProcedure.Di(aip.Procedures ?? [], sessione,
            nome => catalogo?.Cerca(nome)?.Posizione, nome => punti.TryGetValue(nome, out var p) ? p.Position : null, abbinamenti));
        _abbinamentiDelleProcedure = abbinamenti;
        Avvisa();
    }

    /// <summary>Dove sta un punto o una radioassistenza dell'AIP espanso: il centro di un arco DME della prosa (7b).</summary>
    private static Func<string, global::Aip.Position?> DoveNellAip(global::Aip.AipDocument aip)
    {
        var punti = PuntiDellAip(aip);
        return n => punti.TryGetValue(n, out var x) ? x.Position : null;
    }

    /// <summary>I punti con nome dell'AIP espanso (punti di ENR 4.4 e di scalo; le radioassistenze), con la loro provenienza.</summary>
    private static Dictionary<string, (global::Aip.Position Position, global::Aip.SignificantPoint? Punto)> PuntiDellAip(global::Aip.AipDocument aip)
    {
        var punti = new Dictionary<string, (global::Aip.Position, global::Aip.SignificantPoint?)>(StringComparer.Ordinal);
        foreach (var x in aip.Points ?? []) punti.TryAdd(x.Designator, (x.Position, x));
        foreach (var x in aip.Navaids ?? []) punti.TryAdd(x.Designator, (x.Position, null));
        foreach (var x in (aip.Aerodromes ?? []).SelectMany(a => a.NavigationAids)) punti.TryAdd(x.Designator, (x.Position, null));
        return punti;
    }

    /// <summary>
    /// I punti della procedura che il sector non ha, portati prima della procedura (carta F7 §4): quelli di scalo nella sezione
    /// dello scalo di <c>APT.fix</c>, quelli di ENR 4.4 in <c>itfix.fix</c>. Una radioassistenza che manca, o un punto che
    /// l'AIP espanso non ha (stampato male: RF407), fermano il gesto: false, e il perché in <paramref name="avvisi"/>.
    /// </summary>
    private bool PortaIPuntiDellaProcedura(SessioneAperta sessione, string icao, IReadOnlyList<string> nomi, global::Aip.AipDocument aip, List<string> avvisi)
    {
        var catalogo = CatalogoScelto;
        var mancanti = nomi.Distinct().Where(n => catalogo?.Cerca(n) is null).ToList();
        if (mancanti.Count == 0) return true;
        var dellAip = PuntiDellAip(aip);
        var nonSiPortano = mancanti.Where(n => !dellAip.TryGetValue(n, out var x) || x.Punto is null).ToList();
        if (nonSiPortano.Count > 0)
        {
            avvisi.Add($"punti che il sector non ha e che non si portano da qui: {string.Join(", ", nonSiPortano)} "
                       + "(una radioassistenza dal confronto di ENR 4; un punto che l'AIP espanso non ha, con le correzioni)");
            return false;
        }
        var diScalo = mancanti.Select(n => dellAip[n].Punto!).Where(p => p.Section.StartsWith("AD 2", StringComparison.Ordinal)).ToList();
        if (diScalo.Count > 0)
        {
            var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + Core.PdfAip.ProcedureNelSector.FileDeiPuntiDiScalo, StringComparison.OrdinalIgnoreCase));
            if (file is null || sessione.File[file] is not IFileConRecord apt)
            {
                avvisi.Add($"nel sector non c'è {Core.PdfAip.ProcedureNelSector.FileDeiPuntiDiScalo}");
                return false;
            }
            var quanti = apt.RecordDelModello.Count;
            var righe = apt.RigheDelFile(Modifiche.SporchiDi(file));
            var nuove = diScalo.Select(Core.PdfAip.ProcedureNelSector.RigaDelPuntoDiScalo).ToList();
            if (!GestoSulTesto(file, "punti di scalo", f => Modifiche.CambiaRighe(f, Core.PdfAip.ProcedureNelSector.NellaSezioneDelloScalo(righe, icao, nuove),
                    $"{string.Join(", ", diScalo.Select(p => p.Designator))} dall'AIP",
                    riletto => riletto.RecordDelModello.Count == quanti + nuove.Count ? null : "Riletto, APT.fix non ha i punti nuovi: si fa a mano.")))
            {
                avvisi.Add($"{Core.PdfAip.ProcedureNelSector.FileDeiPuntiDiScalo}: i punti {string.Join(", ", diScalo.Select(p => p.Designator))} non si scrivono");
                return false;
            }
            RifaiLaGeometria(file);
        }
        foreach (var p in mancanti.Select(n => dellAip[n].Punto!).Where(p => !p.Section.StartsWith("AD 2", StringComparison.Ordinal)))
        {
            var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + Core.PdfAip.ConfrontoDiEnr4.FileDeiPunti, StringComparison.OrdinalIgnoreCase));
            if (!PortaUnaRiga(sessione, p.Designator, file, Core.PdfAip.Enr4NelSector.RigaNuova(p), p.Designator, avvisi))
                return false;
            RifaiLaGeometria(file!);
        }
        // I punti nuovi nel catalogo: la procedura che li cita si disegna, e il confronto rifatto li trova.
        RifaiICataloghi();
        return true;
    }

    /// <summary>
    /// «Porta nel sector» (carta F7, slice 7e) una procedura che il sector non ha: una STAR come voce nuova dello <c>.str</c>
    /// dello scalo, una SID come voce nuova del <c>.sid</c> col suo tracciato (D2), una transizione come voce «SID-TRANS» per
    /// ogni SID del sector che finisce dove la transizione comincia. Il nome nella convenzione del file (D1), i tag di quota
    /// e velocità sui punti (D3), i punti che mancano portati prima. Le convenzionali in sola prosa che la 7b non ha letto no.
    /// </summary>
    public bool PortaLaProcedura(Core.PdfAip.DifferenzaDiProcedura riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione || UltimoConfrontoDelleProcedure is not { } confronto || _aipDelleProcedure is not { } aip)
            return Rifiuta("Confronta SID e STAR prima: si porta una riga del confronto.");
        if (riga.Esito != Core.PdfAip.EsitoDelConfronto.MancaNelSector
            || aip.Procedures?.FirstOrDefault(p => p.Aerodrome == riga.Scalo && p.Kind == riga.Genere && p.Designator == riga.Nome && p.Runways.SequenceEqual(riga.Piste)) is not { } proc)
            return Rifiuta("Si porta una procedura che il sector non ha.");
        if (proc.Legs.Count == 0)
            return Rifiuta($"{proc.Designator} è in sola prosa e la prosa non si è letta (le segnalazioni di «Leggi» dicono perché): si porta a mano.");
        var star = proc.Kind == global::Aip.ProcedureKind.Star;
        var estensione = star ? ".str" : ".sid";
        var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + proc.Aerodrome.ToLowerInvariant() + estensione, StringComparison.OrdinalIgnoreCase));
        if (file is null || sessione.File[file] is not IFileConRecord conRecord)
            return Rifiuta($"Nel sector non c'è {proc.Aerodrome.ToLowerInvariant()}{estensione}.");

        // Le voci da scrivere: (nome, testata, pezzi).
        var nota = $"AIP {confronto.Ciclo}";
        var coppie = _abbinamentiDelleProcedure.Where(a => a.Key.File == file)
            .SelectMany(a => a.Value.Select((p, k) => (a.Key.Record, k, p)))
            .Where(x => x.p is not null && conRecord.RecordDelModello[x.Record] is var r
                        && NomeDellaVoce(r)?.Split('-') is { } parti && x.k < parti.Length)
            .Select(x => (NomeDellaVoce(conRecord.RecordDelModello[x.Record])!.Split('-')[x.k], x.p!.Designator)).ToList();
        var lettere = Core.PdfAip.ProcedureNelSector.Lettere(coppie);
        var voci = new List<(string Nome, string Testata, IReadOnlyList<global::Aip.Procedure> Pezzi)>();
        if (star)
        {
            if (proc.Runways.Count == 0) return Rifiuta($"{proc.Designator}: l'AIP non dice le piste, si porta a mano.");
            var nome = Core.PdfAip.ProcedureNelSector.Nome(proc.Designator, lettere);
            voci.Add((nome, Core.PdfAip.ProcedureNelSector.TestataDellaStar(proc.Aerodrome, proc.Runways, nome, Core.PdfAip.ProcedureNelSector.Rnav([proc])), [proc]));
        }
        else if (proc.Kind == global::Aip.ProcedureKind.Sid && proc.Runways.Count > 0)
        {
            var nome = Core.PdfAip.ProcedureNelSector.Nome(proc.Designator, lettere);
            var punti = Core.PdfAip.ProcedureNelSector.Punti([proc]);
            if (punti.Count == 0) return Rifiuta($"{proc.Designator}: nessun punto con nome per il campo 7, si porta a mano.");
            voci.Add((nome, Core.PdfAip.ProcedureNelSector.TestataDellaSid(proc.Aerodrome, proc.Runways, nome, punti[^1], Core.PdfAip.ProcedureNelSector.Rnav([proc]), Core.PdfAip.ProcedureNelSector.CampiVuoti(conRecord.RecordDelModello)), [proc]));
        }
        else
        {
            // Una transizione (o una SID senza pista, come LIRF le stampa): una voce per ogni SID del sector che finisce dove comincia.
            var inizio = Core.PdfAip.ProcedureNelSector.Punti([proc]).FirstOrDefault();
            var trans = Core.PdfAip.ProcedureNelSector.Nome(proc.Designator, lettere);
            foreach (var ((f, record), parti) in _abbinamentiDelleProcedure.Where(a => a.Key.File == file))
            {
                if (parti.Length == 0 || parti[0] is not { } sid || Core.PdfAip.ProcedureNelSector.Punti([sid]).LastOrDefault() != inizio || sid.Runways.Count == 0) continue;
                var nomeSid = NomeDellaVoce(conRecord.RecordDelModello[record])!.Split('-')[0];
                if (voci.Any(v => v.Nome.StartsWith(nomeSid + "-", StringComparison.Ordinal))) continue;
                var nome = $"{nomeSid}-{trans}";
                var punti = Core.PdfAip.ProcedureNelSector.Punti([sid, proc]);
                voci.Add((nome, Core.PdfAip.ProcedureNelSector.TestataDellaSid(proc.Aerodrome, sid.Runways, nome, punti[^1], Core.PdfAip.ProcedureNelSector.Rnav([sid, proc]), Core.PdfAip.ProcedureNelSector.CampiVuoti(conRecord.RecordDelModello)), [sid, proc]));
            }
            if (voci.Count == 0)
                return Rifiuta($"{proc.Designator}: nel sector non c'è una SID che finisce a {inizio}, dove la transizione comincia.");
        }
        var giaNelFile = conRecord.RecordDelModello.Select(NomeDellaVoce).OfType<string>().ToHashSet(StringComparer.Ordinal);
        // Di una transizione si scrivono le combinazioni che mancano: quelle che il file ha già (con un'altra edizione della SID
        // o della transizione) restano, e si sistemano con «Sostituisci».
        var gia = voci.Where(v => giaNelFile.Contains(v.Nome)).Select(v => v.Nome).ToList();
        if (gia.Count > 0 && (star || proc.Kind == global::Aip.ProcedureKind.Sid && proc.Runways.Count > 0 || gia.Count == voci.Count))
            return Rifiuta($"{string.Join(", ", gia)}: c'è già nel file con questo nome.");
        voci.RemoveAll(v => gia.Contains(v.Nome));

        var avvisi = new List<string>();
        var dove = DoveNellAip(aip);
        var gesto = $"{proc.Designator} ({string.Join(", ", voci.Select(v => v.Nome))}) dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () =>
        {
            avvisi.Clear();
            if (!PortaIPuntiDellaProcedura(sessione, proc.Aerodrome, voci.SelectMany(v => Core.PdfAip.ProcedureNelSector.Punti(v.Pezzi)).ToList(), aip, avvisi))
                return false;
            foreach (var v in voci)
            {
                var c = (IFileConRecord)sessione.File[file];
                var quanti = c.RecordDelModello.Count;
                var righe = c.RigheDelFile(Modifiche.SporchiDi(file));
                var blocco = Core.PdfAip.ProcedureNelSector.Blocco(v.Nome, v.Testata, v.Pezzi, nota, star ? Core.PdfAip.ProcedureNelSector.Coda(proc.Designator) : null, dove);
                var ultima = righe.Count;
                var sostituzioni = new Dictionary<int, IReadOnlyList<string>>
                {
                    [ultima] = string.IsNullOrWhiteSpace(righe[^1]) ? [righe[^1], .. blocco] : [righe[^1], "", .. blocco],
                };
                if (!GestoSulTesto(file, "procedura", f => Modifiche.CambiaRighe(f, sostituzioni, $"{v.Nome} dall'AIP",
                        riletto => riletto.RecordDelModello.Count == quanti + 1 ? null : "Riletto, il file non ha la voce nuova: si fa a mano.", anchiITag: true)))
                {
                    avvisi.Add($"{v.Nome}: non si scrive");
                    return false;
                }
            }
            RifaiLaGeometria(file);
            Registro.Scrivi("aip", gesto);
            RicontrollaLeModifiche();
            Avvisa();
            return true;
        });
        if (!fatto && avvisi.Count > 0)
            return Rifiuta(string.Join(" · ", avvisi));
        if (fatto)
            RifaiIlConfrontoDelleProcedure();
        return fatto;
    }

    /// <summary>
    /// «Sostituisci» (carta F7, slice 7e) su una procedura diversa: ogni voce del sector che le corrisponde riscritta coi punti
    /// dell'AIP — per una SID col tracciato (D2) della SID e della sua transizione, e il campo 7 al punto finale —, i tag di
    /// quota e velocità (D3), la testata che resta. Le voci <c>locked=si</c> e quelle che hanno già dei tag non si toccano.
    /// </summary>
    public bool SostituisciLaProcedura(Core.PdfAip.DifferenzaDiProcedura riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is null || UltimoConfrontoDelleProcedure is not { } confronto || _aipDelleProcedure is null)
            return Rifiuta("Confronta SID e STAR prima: si sostituisce una riga del confronto.");
        var sid = riga.Genere != global::Aip.ProcedureKind.Star;
        if (riga.File is null || !(riga.Esito == Core.PdfAip.EsitoDelConfronto.FormaDiversa || sid && riga.VociSenzaTracciato > 0))
            return Rifiuta("Si sostituisce una procedura diversa da quella dell'AIP (o una SID senza tracciato).");
        var saltate = new List<string>();
        var voci = VociDaRiscrivere(riga, saltate);
        var gesto = voci.Count == 1 ? $"{voci[0].Nome} sostituita dall'AIP {confronto.Ciclo}" : $"{voci.Count} voci di {riga.Nome} sostituite dall'AIP {confronto.Ciclo}";
        return RiscriviLeVoci(voci, saltate, gesto);
    }

    /// <summary>
    /// Dopo un gesto che riscrive procedure nel testo («Sostituisci», «Scrivi i tracciati»): le mappe composte del file
    /// (le <c>STAR RNAV(ALL)</c> dei <c>.str</c>, le mappe delle SID della 7f) che non sono più come le rigenererebbe il Lab
    /// si riscrivono anche loro, nello stesso gesto. I gesti sui campi le rigenerano già da sé (F3-bis); quelli sul testo
    /// no, e una STAR sostituita lasciava indietro la sua mappa. Solo le mappe che elencano una delle procedure
    /// <paramref name="riscritte"/>: una già disallineata all'apertura resta com'è (F3-bis, lo dice il validatore). Vero se
    /// non c'era niente da fare o se è fatto.
    /// </summary>
    private bool AllineaLeComposte(string file, IReadOnlyCollection<string> riscritte)
    {
        if (Sessione is not { } sessione || sessione.File.GetValueOrDefault(file) is not IFileConRecord c)
            return true;
        var (letto, record, scrivi) = sessione.File[file] switch
        {
            FileLetto<StrRecord> str => ((object)str.Letto, (IReadOnlyList<object>)str.Letto.Records, (Func<object, IReadOnlyList<string>>)(r => new StrSaver().Serialize((StrRecord)r))),
            FileLetto<SidProcedure> sid => (sid.Letto, sid.Letto.Records, r => new SidSaver().Serialize((SidProcedure)r)),
            _ => (null!, [], null!),
        };
        if (letto is null) return true;
        var sporchi = Modifiche.SporchiDi(file);
        var righe = c.RigheDelFile(sporchi);
        var posti = c.PostiDeiRecord(sporchi);
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        var nomi = new List<string>();
        foreach (var composta in MappeComposte.DiQualunque(letto))
        {
            if (composta.Elenco is null || !MappeComposte.ProcedureElencate(composta.Elenco, record).Any(p => riscritte.Contains(MappeComposte.Nome(p))))
                continue;
            var rigenerata = composta.Componi(record);
            if (MappeComposte.Uguale(composta.Mappa, rigenerata.Punti)) continue;
            int indice = -1;
            for (var i = 0; i < record.Count; i++)
                if (ReferenceEquals(record[i], composta.Mappa)) indice = i;
            if (indice < 0) continue;
            var (da, quante) = posti[indice];
            var fine = da + quante;
            while (fine > da + 1 && (string.IsNullOrWhiteSpace(righe[fine - 1]) || righe[fine - 1].TrimStart().StartsWith("//", StringComparison.Ordinal)))
                fine--;
            // Il modello riletto si può cambiare: il gesto qui sotto rilegge il file.
            MappeComposte.Applica(composta.Mappa, rigenerata.Punti);
            sostituzioni[da + 1] = scrivi(composta.Mappa);
            for (var i = da + 1; i < fine; i++) sostituzioni[i + 1] = [];
            nomi.Add(MappeComposte.Nome(composta.Mappa));
        }
        if (sostituzioni.Count == 0) return true;
        var quanti = c.RecordDelModello.Count;
        return GestoSulTesto(file, "procedura", f => Modifiche.CambiaRighe(f, sostituzioni, $"{string.Join(", ", nomi)} riallineate",
            riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più le stesse voci: si fa a mano.", anchiITag: true));
    }

    /// <summary>
    /// Gli scali il cui <c>.sid</c> può avere la mappa di gruppo (carta F7 slice 7f, P10): almeno due SID col tracciato, e
    /// ancora nessuna voce <c>MAPS</c>. Con quante SID la raccoglierebbe.
    /// </summary>
    public IReadOnlyList<(string Scalo, string File, int Sid)> ScaliPerLaMappaDelleSid()
    {
        var scali = new List<(string, string, int)>();
        if (Sessione is not { } sessione) return scali;
        foreach (var (file, aperto) in sessione.File.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (aperto is not FileLetto<SidProcedure> sid || !file.EndsWith(".sid", StringComparison.OrdinalIgnoreCase)) continue;
            var icao = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
            if (Core.PdfAip.MappaDelleSid.Righe(sid.Letto.Records, icao) is not null)
                scali.Add((icao, file, Core.PdfAip.MappaDelleSid.Quante(sid.Letto.Records, icao)));
        }
        return scali;
    }

    /// <summary>
    /// «Mappa delle SID» (carta F7 slice 7f, P10): in testa al <c>.sid</c> dello scalo una voce <c>MAPS</c>, <c>SID
    /// RNAV(ALL)</c>, composta dalle SID col tracciato (le RNAV se ce ne sono). È una mappa composta: da qui in poi il Lab la
    /// rigenera quando una SID cambia, e l'elenco si cambia dalla sua scheda («Composta da»).
    /// </summary>
    public bool CreaLaMappaDelleSid(string icao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);
        if (Sessione is not { } sessione) return false;
        var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + icao.ToLowerInvariant() + ".sid", StringComparison.OrdinalIgnoreCase));
        if (file is null || sessione.File[file] is not FileLetto<SidProcedure> sid)
            return Rifiuta($"Nel sector non c'è {icao.ToLowerInvariant()}.sid.");
        var nuove = Core.PdfAip.MappaDelleSid.Righe(sid.Letto.Records, icao);
        if (nuove is null)
            return Rifiuta($"{icao}: servono almeno due SID col tracciato, e il file non deve avere già la sua mappa MAPS.");
        var gesto = $"mappa delle SID di {icao}";
        return NellaStoria(gesto, () =>
        {
            var c = (IFileConRecord)sessione.File[file];
            var righe = c.RigheDelFile(Modifiche.SporchiDi(file));
            var quanti = c.RecordDelModello.Count;
            var sostituzioni = new Dictionary<int, IReadOnlyList<string>> { [1] = [.. nuove, "", righe[0]] };
            if (!GestoSulTesto(file, "procedura", f => Modifiche.CambiaRighe(f, sostituzioni, gesto,
                    riletto => riletto.RecordDelModello.Count == quanti + 1 ? null : "Riletto, il file non ha la mappa nuova: si fa a mano.", anchiITag: true)))
                return false;
            Registro.Scrivi("aip", gesto);
            RicontrollaLeModifiche();
            Avvisa();
            return true;
        });
    }

    /// <summary>
    /// «Scrivi i tracciati» (carta F7, slice 7e, D2): le SID uguali all'AIP per nome, piste e punto finale, ma che nel sector
    /// non hanno tracciato, ricevono quello dell'AIP — tutte insieme, un gesto solo.
    /// </summary>
    public bool ScriviITracciatiDelleSid()
    {
        if (Sessione is null || UltimoConfrontoDelleProcedure is not { } confronto || _aipDelleProcedure is null)
            return Rifiuta("Confronta SID e STAR prima.");
        var saltate = new List<string>();
        var voci = VociSenzaTracciato(saltate);
        if (voci.Count == 0 && saltate.Count == 0)
            return Rifiuta("Nessuna SID uguale senza tracciato.");
        return RiscriviLeVoci(voci, saltate, $"tracciati di {voci.Count} SID dall'AIP {confronto.Ciclo}");
    }

    /// <summary>
    /// Quante voci «Scrivi i tracciati» riscriverebbe adesso: non quelle che salterebbe (l'altra parte del nome in sola prosa
    /// o senza una procedura sola, i tag già scritti) — il numero sul tasto è quello che il gesto fa.
    /// </summary>
    public int TracciatiDaScrivere => UltimoConfrontoDelleProcedure is null || _aipDelleProcedure is null ? 0 : VociSenzaTracciato([]).Count;

    private List<(string File, int Record, string Nome, bool Star, IReadOnlyList<global::Aip.Procedure> Pezzi)> VociSenzaTracciato(List<string> saltate) =>
        UltimoConfrontoDelleProcedure is not { } confronto ? [] :
        [.. confronto.Esiti.Where(e => e.Esito == Core.PdfAip.EsitoDelConfronto.Uguale && e.Genere != global::Aip.ProcedureKind.Star && e.VociSenzaTracciato > 0)
            .SelectMany(r => VociDaRiscrivere(r, saltate)).DistinctBy(v => (v.File, v.Record))];

    /// <summary>
    /// Le voci del sector di una riga del confronto, con le procedure dell'AIP di ogni parte del nome (la SID e la sua
    /// transizione). Quelle che non si toccano vanno in <paramref name="saltate"/>, col perché: <c>locked=si</c>, già coi tag,
    /// una parte del nome senza una procedura sola, la sola prosa.
    /// </summary>
    private List<(string File, int Record, string Nome, bool Star, IReadOnlyList<global::Aip.Procedure> Pezzi)> VociDaRiscrivere(
        Core.PdfAip.DifferenzaDiProcedura riga, List<string> saltate)
    {
        var voci = new List<(string, int, string, bool, IReadOnlyList<global::Aip.Procedure>)>();
        if (Sessione is not { } sessione || riga.File is not { } file || sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord)
            return voci;
        foreach (var ((_, record), parti) in _abbinamentiDelleProcedure.Where(a => a.Key.File == file).OrderBy(a => a.Key.Record))
        {
            var nome = NomeDellaVoce(conRecord.RecordDelModello[record]);
            if (nome is null || !riga.Nomi.Contains(nome)) continue;
            if (parti.Any(p => p is null))
            {
                saltate.Add($"{nome} (una parte del nome non ha una procedura sola)");
                continue;
            }
            if (conRecord.ChiaviDi(record) is { Count: > 0 } chiavi)
            {
                saltate.Add(chiavi.ContainsKey("locked") ? $"{nome} (locked=si)" : $"{nome} (ha già dei tag: si cambia dalla scheda)");
                continue;
            }
            IReadOnlyList<global::Aip.Procedure> pezzi = [.. parti.OfType<global::Aip.Procedure>()];
            if (pezzi.Any(p => p.Legs.Count == 0))
            {
                saltate.Add($"{nome} (in sola prosa, non letta)");
                continue;
            }
            voci.Add((file, record, nome, conRecord.RecordDelModello[record] is StrRecord, pezzi));
        }
        return voci;
    }

    /// <summary>
    /// Riscrive le voci col corpo dell'AIP (i punti coi loro tag; per una SID il campo 7 al punto finale), la testata che resta,
    /// in un blocco §M; prima i punti che il sector non ha. Un gesto solo, anche su più file.
    /// </summary>
    private bool RiscriviLeVoci(List<(string File, int Record, string Nome, bool Star, IReadOnlyList<global::Aip.Procedure> Pezzi)> voci,
        List<string> saltate, string gesto)
    {
        if (Sessione is not { } sessione || UltimoConfrontoDelleProcedure is not { } confronto || _aipDelleProcedure is not { } aip)
            return false;
        if (voci.Count == 0)
            return Rifiuta("Nessuna voce da riscrivere" + (saltate.Count > 0 ? ": " + string.Join(", ", saltate) : "."));
        var nota = $"AIP {confronto.Ciclo}";
        var avvisi = new List<string>();
        var dove = DoveNellAip(aip);
        bool fatto = NellaStoria(gesto, () =>
        {
            avvisi.Clear();
            foreach (var scalo in voci.GroupBy(v => v.Pezzi[0].Aerodrome))
                if (!PortaIPuntiDellaProcedura(sessione, scalo.Key, scalo.SelectMany(v => Core.PdfAip.ProcedureNelSector.Punti(v.Pezzi)).ToList(), aip, avvisi))
                    return false;
            foreach (var perFile in voci.GroupBy(v => v.File))
            {
                var file = perFile.Key;
                // Dal fondo: le righe di una voce non spostano quelle delle voci prima.
                foreach (var v in perFile.OrderByDescending(v => v.Record))
                {
                    var c = (IFileConRecord)sessione.File[file];
                    var sporchi = Modifiche.SporchiDi(file);
                    var righe = c.RigheDelFile(sporchi);
                    var (da, quante) = c.PostiDeiRecord(sporchi)[v.Record];
                    var fine = da + quante;
                    while (fine > da + 1 && (string.IsNullOrWhiteSpace(righe[fine - 1]) || righe[fine - 1].TrimStart().StartsWith("//", StringComparison.Ordinal)))
                        fine--;
                    var testata = righe[da];
                    if (!v.Star)
                    {
                        // Il campo 7 (il punto finale) all'ultimo punto dell'AIP, il resto della testata com'è.
                        var campi = testata.Split(';');
                        var punti = Core.PdfAip.ProcedureNelSector.Punti(v.Pezzi);
                        if (campi.Length > 6 && punti.Count > 0) campi[6] = punti[^1];
                        testata = string.Join(';', campi);
                    }
                    var corpo = Core.PdfAip.ProcedureNelSector.Corpo(v.Pezzi, v.Star ? Core.PdfAip.ProcedureNelSector.Coda(v.Pezzi[0].Designator) : null, dove);
                    var sostituzioni = new Dictionary<int, IReadOnlyList<string>>
                    {
                        [da + 1] = [Core.PdfAip.ProcedureNelSector.Dichiarazione(v.Nome, v.Pezzi, nota), "//@START", testata, .. corpo, $"//@END \"{v.Nome}\""],
                    };
                    for (var i = da + 1; i < fine; i++) sostituzioni[i + 1] = [];
                    var quanti = c.RecordDelModello.Count;
                    if (!GestoSulTesto(file, "procedura", f => Modifiche.CambiaRighe(f, sostituzioni, $"{v.Nome} dall'AIP",
                            riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più le stesse voci: si fa a mano.", anchiITag: true)))
                    {
                        avvisi.Add($"{v.Nome}: non si scrive");
                        return false;
                    }
                }
                if (!AllineaLeComposte(file, [.. perFile.Select(v => v.Nome)]))
                {
                    avvisi.Add($"{NomeDelFile(file)}: le mappe composte non si riallineano");
                    return false;
                }
                RifaiLaGeometria(file);
            }
            Registro.Scrivi("aip", gesto + (saltate.Count > 0 ? "; saltate: " + string.Join(", ", saltate) : ""));
            Rifiuto = saltate.Count > 0 ? "Non riscritte: " + string.Join(", ", saltate) : null;
            RicontrollaLeModifiche();
            Avvisa();
            return true;
        });
        if (!fatto && avvisi.Count > 0)
            return Rifiuta(string.Join(" · ", avvisi));
        if (fatto)
            RifaiIlConfrontoDelleProcedure();
        return fatto;
    }

    /// <summary>Il nome di una voce di SID o di STAR (il terzo campo), o null se il record non è una procedura.</summary>
    private static string? NomeDellaVoce(object record) => record switch
    {
        SidProcedure sid => sid.Name.Trim(),
        // Anche una STAR con le virate disegnate a coordinate, che il parser legge come HoldingStrRecord (LICA ARBU4F).
        ProcedureStrRecord or HoldingStrRecord => ((StrRecord)record).ProcedureId.Trim(),
        _ => null,
    };

    /// <summary>L'ultimo confronto di ENR 4 (slice 3b): il ciclo, se erano le radioassistenze o i punti, e le righe.</summary>
    public (string Ciclo, bool Radioassistenze, IReadOnlyList<Core.PdfAip.DifferenzaDiEnr4> Esiti)? UltimoConfrontoDiEnr4 { get; private set; }

    /// <summary>
    /// «Confronta le radioassistenze» / «i punti» (carta F6, slice 3b): ENR 4.1 dell'AIP espanso coi <c>.vor</c> e i
    /// <c>.ndb</c>, o ENR 4.4 coi <c>.fix</c>. In sola lettura.
    /// </summary>
    public async Task<bool> ConfrontaEnr4Async(bool radioassistenze)
    {
        if (Sessione is not { } sessione)
            return Rifiuta("Apri un sector prima: il confronto è fra l'AIP espanso e il sector aperto.");
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (CicloDiLavoroCon(espanso, out var noCiclo) is not { } ciclo)
            return Rifiuta(noCiclo!);
        try
        {
            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            if ((radioassistenze ? modello.Navaids?.Count : modello.Points?.Count) is not > 0)
                return Rifiuta($"L'AIP espanso del ciclo {ciclo} non ha {(radioassistenze ? "radioassistenze" : "punti")}: servono i PDF di ENR 4 letti.");
            var esiti = await Task.Run(() => radioassistenze
                ? Core.PdfAip.ConfrontoDiEnr4.Radioassistenze(modello.Navaids!, sessione)
                : Core.PdfAip.ConfrontoDiEnr4.Punti(modello.Points!, sessione)).ConfigureAwait(false);
            UltimoConfrontoDiEnr4 = (ciclo, radioassistenze, esiti);
            _aipDiEnr4 = modello;
            Registro.Scrivi("aip", $"confronto {(radioassistenze ? "radioassistenze" : "punti")}, ciclo {ciclo}: "
                + string.Join(", ", esiti.GroupBy(e => e.Esito).Select(g => $"{g.Key} {g.Count()}")) + $", uso diverso {esiti.Count(e => e.UsoDiverso)}");
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'AIP espanso del ciclo {ciclo} non si legge: {e.Message}");
        }
    }

    /// <summary>Il modello da cui viene <see cref="UltimoConfrontoDiEnr4"/>: i gesti della slice 3c ne prendono radioassistenze e punti.</summary>
    private global::Aip.AipDocument? _aipDiEnr4;

    /// <summary>
    /// «Sostituisci» su una radioassistenza diversa (carta F6, slice 3c): la sua riga del <c>.vor</c> o del <c>.ndb</c>
    /// riscritta coi valori dell'AIP che sono diversi — frequenza, posizione oltre 0,1 NM, canale TACAN dove la riga lo
    /// scrive —; gli altri campi (visibilità, tipo, attesa) restano. Un gesto: diff, annulla, lotto di Aurora.
    /// </summary>
    public bool SostituisciLaRadioassistenza(Core.PdfAip.DifferenzaDiEnr4 riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr4 is not { Radioassistenze: true } confronto || _aipDiEnr4?.Navaids is not { } dellAip)
            return Rifiuta("Confronta le radioassistenze prima.");
        if (riga is not { Esito: Core.PdfAip.EsitoDelConfronto.FormaDiversa, File: { } file, Record: { } record }
            || sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record >= conRecord.RecordDelModello.Count)
            return Rifiuta("Si sostituisce una radioassistenza del sector diversa da quella dell'AIP.");
        if (dellAip.FirstOrDefault(n => n.Key == riga.Nome) is not { } n)
            return Rifiuta($"{riga.Nome}: non è nell'AIP espanso del confronto.");
        bool fatto = SostituisciLaRigaDellaRadioassistenza(conRecord, file, record, riga.Nome, n, confronto.Ciclo);
        if (fatto)
            RifaiIlConfrontoDiEnr4();
        return fatto;
    }

    /// <summary>
    /// La riga del <c>.vor</c> o del <c>.ndb</c> riscritta coi valori di <paramref name="n"/> che sono diversi (slice 3c; la
    /// usa anche AD 2.19, slice 5f). Un gesto: diff, annulla, lotto di Aurora.
    /// </summary>
    private bool SostituisciLaRigaDellaRadioassistenza(IFileConRecord conRecord, string file, int record, string nome, global::Aip.Navaid n, string ciclo)
    {
        Coordinate? dove = conRecord.RecordDelModello[record] switch { Vor v => v.Position, Ndb d => d.Position, _ => null };
        if (dove is not { } posizione)
            return Rifiuta($"{nome}: la voce del sector non è un VOR né un NDB.");
        var scarto = global::Aip.Aurora.Sphere.DistanceNm(n.Position, new global::Aip.Position(posizione.LatitudeDeg, posizione.LongitudeDeg));
        var gesto = $"{nome} dall'AIP {ciclo}";
        return NellaStoria(gesto, () =>
        {
            var righe = RigheDiAdesso(file);
            var (da, _) = conRecord.PostiDeiRecord(Modifiche.SporchiDi(file))[record];
            if (Core.PdfAip.Enr4NelSector.Sostituita(righe[da], n, scarto, Core.PdfAip.ConfrontoDiEnr3.Soglia, out var cambiati) is not { } nuova || cambiati.Count == 0)
                return Rifiuta($"{nome}: nella riga non c'è niente da cambiare.");
            var quanti = conRecord.RecordDelModello.Count;
            bool scritto = GestoSulTesto(file, "aip", f => Modifiche.CambiaRighe(f, new Dictionary<int, IReadOnlyList<string>> { [da + 1] = [nuova] }, gesto,
                riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più le stesse voci: si fa a mano."));
            Registro.Scrivi("aip", $"{gesto}: {string.Join(", ", cambiati)}");
            return scritto;
        });
    }

    /// <summary>I file dove può andare un punto (R8a): i <c>.fix</c> del sector, <c>itfix.fix</c> per primo.</summary>
    public IReadOnlyList<string> FileDeiPunti => Sessione is { } s ? Core.PdfAip.Enr4NelSector.FileDeiPunti(s.File.Keys) : [];

    /// <summary>
    /// L'anteprima di «metti in questo file» per i punti (R8a, D19): per ogni punto scelto la riga che si scriverebbe e
    /// dove, nel file dato. Niente si scrive. Vuota se i punti non si possono portare.
    /// </summary>
    public IReadOnlyList<(string Nome, string Riga, string Dove)> AnteprimaDeiPunti(IReadOnlyList<string> nomi, string? file, bool inFondo)
    {
        ArgumentNullException.ThrowIfNull(nomi);
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr4 is not { Radioassistenze: false } || _aipDiEnr4?.Points is not { } punti
            || file is null || sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord)
            return [];
        var anteprima = new List<(string, string, string)>();
        foreach (var nome in nomi)
        {
            if (punti.FirstOrDefault(p => p.Designator.Equals(nome, StringComparison.OrdinalIgnoreCase)) is not { } p)
                continue;
            int posto = PostoDellaRiga(conRecord, p.Designator, inFondo);
            string dove = posto >= conRecord.RecordDelModello.Count
                ? "in fondo"
                : $"prima di {NomeDiEnr4(conRecord.RecordDelModello[posto])?.Trim()}";
            anteprima.Add((p.Designator, Core.PdfAip.Enr4NelSector.RigaNuova(p), dove));
        }
        return anteprima;
    }

    /// <summary>
    /// «Porta nel sector» (carta F6, slice 3c): le radioassistenze o i punti scelti fra quelli che il sector non ha, come righe
    /// nuove di <c>itvor.vor</c>, <c>itndb.ndb</c> o <c>itfix.fix</c>, in ordine di nome. Un gesto solo.
    /// <para>R8a («metti in questo file»): i punti vanno nel <paramref name="fileDeiPunti"/> scelto fra i <c>.fix</c> del
    /// sector (null = <c>itfix.fix</c>, com'era), in ordine di nome o <paramref name="inFondo"/>.</para>
    /// </summary>
    public bool PortaNelSectorDiEnr4(IReadOnlyList<string> nomi, string? fileDeiPunti = null, bool inFondo = false)
    {
        ArgumentNullException.ThrowIfNull(nomi);
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr4 is not { } confronto || _aipDiEnr4 is not { } aip)
            return Rifiuta("Confronta le radioassistenze o i punti prima: si portano le righe del confronto.");
        if (fileDeiPunti is not null && (confronto.Radioassistenze || !FileDeiPunti.Contains(fileDeiPunti, StringComparer.Ordinal)))
            return Rifiuta($"{fileDeiPunti} non è un file dei punti (.fix) del sector.");
        var mancanti = confronto.Esiti.Where(e => e.Esito == Core.PdfAip.EsitoDelConfronto.MancaNelSector).Select(e => e.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scelti = nomi.Where(mancanti.Contains).ToList();
        if (scelti.Count == 0)
            return Rifiuta("Si portano le righe che il sector non ha.");
        string? Chiave(string nome) => sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + nome, StringComparison.OrdinalIgnoreCase));
        var gesto = scelti.Count == 1 ? $"{scelti[0]} dall'AIP {confronto.Ciclo}" : $"{scelti.Count} {(confronto.Radioassistenze ? "radioassistenze" : "punti")} dall'AIP {confronto.Ciclo}";
        var avvisi = new List<string>();
        bool fatto = NellaStoria(gesto, () =>
        {
            avvisi.Clear();
            int portati = 0;
            var toccati = new HashSet<string>(StringComparer.Ordinal);
            foreach (var nome in scelti)
            {
                string riga, chiave;
                string? file;
                if (confronto.Radioassistenze)
                {
                    if (aip.Navaids?.FirstOrDefault(n => n.Key.Equals(nome, StringComparison.OrdinalIgnoreCase)) is not { } n) continue;
                    file = Chiave(Core.PdfAip.Enr4NelSector.NeiNdb(n) ? Core.PdfAip.Enr4NelSector.FileDegliNdb : Core.PdfAip.Enr4NelSector.FileDeiVor);
                    (riga, chiave) = (Core.PdfAip.Enr4NelSector.RigaNuova(n), n.Designator);
                }
                else
                {
                    if (aip.Points?.FirstOrDefault(p => p.Designator.Equals(nome, StringComparison.OrdinalIgnoreCase)) is not { } p) continue;
                    file = fileDeiPunti ?? Chiave(Core.PdfAip.ConfrontoDiEnr4.FileDeiPunti);
                    (riga, chiave) = (Core.PdfAip.Enr4NelSector.RigaNuova(p), p.Designator);
                }
                if (PortaUnaRiga(sessione, nome, file, riga, chiave, avvisi, inFondo))
                {
                    portati++;
                    toccati.Add(file!);
                }
            }
            foreach (var f in toccati) RifaiLaGeometria(f);
            Registro.Scrivi("aip", $"{gesto}: {portati} portati" + (avvisi.Count > 0 ? "; " + string.Join("; ", avvisi) : ""));
            Rifiuto = avvisi.Count > 0 ? string.Join(" · ", avvisi) : null;
            RicontrollaLeModifiche();
            Avvisa();
            return portati > 0;
        });
        if (fatto)
            RifaiIlConfrontoDiEnr4();
        return fatto;
    }

    /// <summary>
    /// «Correggi l'uso» (carta F6, slice 3c, committente 19 ottobre: il 4° campo del <c>.fix</c> è l'uso, il 5° l'interesse
    /// FRA): le righe di <c>itfix.fix</c> dei punti scelti fra quelli «uso diverso», col 4° e il 5° campo dell'AIP. Un gesto
    /// solo, una voce nel testo del file.
    /// </summary>
    public bool CorreggiLUso(IReadOnlyList<string> nomi)
    {
        ArgumentNullException.ThrowIfNull(nomi);
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr4 is not { Radioassistenze: false } confronto || _aipDiEnr4?.Points is not { } punti)
            return Rifiuta("Confronta i punti prima: si correggono le righe «uso diverso» del confronto.");
        var righeScelte = confronto.Esiti.Where(e => e.UsoDiverso && e.File is not null && e.Record is not null
            && nomi.Contains(e.Nome, StringComparer.OrdinalIgnoreCase)).ToList();
        if (righeScelte.Count == 0)
            return Rifiuta("Si corregge l'uso delle righe «uso diverso».");
        var file = righeScelte[0].File!;
        if (sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord)
            return Rifiuta("Il file dei punti non è aperto.");
        var gesto = righeScelte.Count == 1 ? $"uso di {righeScelte[0].Nome} dall'AIP {confronto.Ciclo}" : $"uso di {righeScelte.Count} punti dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () =>
        {
            var righe = RigheDiAdesso(file);
            var posti = conRecord.PostiDeiRecord(Modifiche.SporchiDi(file));
            var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
            var cosa = new List<string>();
            foreach (var r in righeScelte.Where(r => r.File == file))
            {
                if (punti.FirstOrDefault(p => p.Designator.Equals(r.Nome, StringComparison.OrdinalIgnoreCase)) is not { } p) continue;
                var (da, _) = posti[r.Record!.Value];
                if (Core.PdfAip.Enr4NelSector.ConLUso(righe[da], p, out var cambiati) is not { } nuova || cambiati.Count == 0) continue;
                sostituzioni[da + 1] = [nuova];
                cosa.Add($"{r.Nome} ({string.Join(", ", cambiati)})");
            }
            if (sostituzioni.Count == 0)
                return Rifiuta("Nelle righe non c'è niente da cambiare.");
            var quanti = conRecord.RecordDelModello.Count;
            bool scritto = GestoSulTesto(file, "aip", f => Modifiche.CambiaRighe(f, sostituzioni, gesto,
                riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più gli stessi punti: si fa a mano."));
            Registro.Scrivi("aip", $"{gesto}: {string.Join("; ", cosa)}");
            return scritto;
        });
        if (fatto)
            RifaiIlConfrontoDiEnr4();
        return fatto;
    }

    private static string? NomeDiEnr4(object r) => r switch { Fix f => f.Name, Vor v => v.Ident, Ndb d => d.Ident, _ => null };

    /// <summary>Dove va una riga nuova: prima della prima voce col nome che viene dopo <paramref name="chiave"/>, o in fondo.</summary>
    private static int PostoDellaRiga(IFileConRecord conRecord, string chiave, bool inFondo) => inFondo
        ? conRecord.RecordDelModello.Count
        : conRecord.RecordDelModello.Select((r, i) => (r, i))
            .FirstOrDefault(x => NomeDiEnr4(x.r) is { } k && string.CompareOrdinal(k.Trim(), chiave) > 0, (null!, conRecord.RecordDelModello.Count)).i;

    /// <summary>
    /// Una riga nuova in un <c>.vor</c>, <c>.ndb</c> o <c>.fix</c>, in ordine di nome: prima della prima voce col nome che viene
    /// dopo <paramref name="chiave"/> (slice 3c; la usa anche AD 2.19, slice 5f). False, con l'avviso, se non si può.
    /// </summary>
    private bool PortaUnaRiga(SessioneAperta sessione, string nome, string? file, string riga, string chiave, List<string> avvisi, bool inFondo = false)
    {
        if (file is null || sessione.File[file] is not IFileConRecord conRecord)
        {
            avvisi.Add($"{nome}: nel sector non c'è il file dove va");
            return false;
        }
        int prima = PostoDellaRiga(conRecord, chiave, inFondo);
        var esito = Modifiche.AggiungiLeRighe(sessione.File[file], prima, [riga], $"{nome} dall'AIP", rigaVuota: false);
        if (esito is ModificaRifiutata rifiutata)
            avvisi.Add($"{nome}: {rifiutata.Motivo}");
        return esito is ModificaDelTesto;
    }

    /// <summary>Dopo un gesto della slice 3c il confronto di ENR 4 si rifà sul sector di adesso.</summary>
    private void RifaiIlConfrontoDiEnr4()
    {
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr4 is not { } confronto || _aipDiEnr4 is not { } aip)
            return;
        UltimoConfrontoDiEnr4 = (confronto.Ciclo, confronto.Radioassistenze, confronto.Radioassistenze
            ? Core.PdfAip.ConfrontoDiEnr4.Radioassistenze(aip.Navaids ?? [], sessione)
            : Core.PdfAip.ConfrontoDiEnr4.Punti(aip.Points ?? [], sessione));
        Avvisa();
    }

    /// <summary>L'ultimo confronto di AD 2 (slice 5b): il ciclo, la parte (scali, piste, frequenze) e le righe.</summary>
    public (string Ciclo, Core.PdfAip.ParteDiAd2 Parte, IReadOnlyList<Core.PdfAip.DifferenzaDiAd2> Esiti)? UltimoConfrontoDiAd2 { get; private set; }

    /// <summary>Il modello da cui viene <see cref="UltimoConfrontoDiAd2"/>, per rifarlo dopo un «Annulla».</summary>
    private global::Aip.AipDocument? _aipDiAd2;

    /// <summary>Quanti ILS ha l'AIP espanso dell'ultimo confronto di AD 2: il sector non li tiene, si dicono e basta.</summary>
    public int IlsDellAipDiAd2 => _aipDiAd2 is { } aip ? Core.PdfAip.ConfrontoDiAd2.Ils(aip) : 0;

    /// <summary>
    /// «Confronta gli scali» / «le piste» / «le frequenze» (carta F6, slice 5b): il testo di AD 2 dell'AIP espanso coi
    /// <c>.ap</c>, i <c>.rw</c> o i <c>.frq</c>, per gli scali di cui ci sono i PDF. In sola lettura.
    /// </summary>
    public async Task<bool> ConfrontaAd2Async(Core.PdfAip.ParteDiAd2 parte)
    {
        if (Sessione is not { } sessione)
            return Rifiuta("Apri un sector prima: il confronto è fra l'AIP espanso e il sector aperto.");
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.");
        if (CicloDiLavoroCon(espanso, out var noCiclo) is not { } ciclo)
            return Rifiuta(noCiclo!);
        try
        {
            var modello = await Task.Run(() => Core.PdfAip.AipEspanso.Modello(espanso, ciclo)).ConfigureAwait(false);
            if (modello.Aerodromes is not { Count: > 0 })
                return Rifiuta($"L'AIP espanso del ciclo {ciclo} non ha scali: servono i PDF di AD 2 letti.");
            if (parte == Core.PdfAip.ParteDiAd2.Radioassistenze && !modello.Aerodromes.Any(s => s.NavigationAids.Count > 0))
                return Rifiuta($"L'AIP espanso del ciclo {ciclo} non ha le radioassistenze di AD 2.19: rileggi i PDF di AD 2.");
            var esiti = await Task.Run(() => Core.PdfAip.ConfrontoDiAd2.Di(parte, modello, sessione)).ConfigureAwait(false);
            UltimoConfrontoDiAd2 = (ciclo, parte, esiti);
            _aipDiAd2 = modello;
            Registro.Scrivi("aip", $"confronto AD 2 {parte}, ciclo {ciclo}: " + string.Join(", ", esiti.GroupBy(e => e.Esito).Select(g => $"{g.Key} {g.Count()}"))
                + (parte == Core.PdfAip.ParteDiAd2.Radioassistenze ? $", diverse in ENR 4.1 {esiti.Count(e => e.DiversaInEnr41)}" : ""));
            Rifiuto = null;
            Avvisa();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or global::Aip.AipJsonException)
        {
            Registro.Errore("aip", e);
            return Rifiuta($"L'AIP espanso del ciclo {ciclo} non si legge: {e.Message}");
        }
    }

    /// <summary>
    /// «Sostituisci» su uno scalo o una pista diversi (carta F6, slice 5c): la riga del <c>.ap</c> o del <c>.rw</c> riscritta
    /// coi valori dell'AIP che sono diversi, con le soglie del confronto; il resto della riga resta. Una riga per file: le
    /// FIR si sostituiscono una per una. Un gesto: diff, annulla, lotto di Aurora.
    /// </summary>
    public bool SostituisciInAd2(Core.PdfAip.DifferenzaDiAd2 riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione || UltimoConfrontoDiAd2 is not { Parte: Core.PdfAip.ParteDiAd2.Scali or Core.PdfAip.ParteDiAd2.Piste } confronto || _aipDiAd2 is not { } aip)
            return Rifiuta("Confronta gli scali o le piste prima.");
        if (riga is not { Esito: Core.PdfAip.EsitoDelConfronto.FormaDiversa, File: { } file, Record: { } record }
            || sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record >= conRecord.RecordDelModello.Count)
            return Rifiuta("Si sostituisce una voce del sector diversa da quella dell'AIP.");
        if (aip.Aerodromes?.FirstOrDefault(a => a.Icao.Equals(riga.Scalo, StringComparison.OrdinalIgnoreCase)) is not { } scalo)
            return Rifiuta($"{riga.Scalo}: non è nell'AIP espanso del confronto.");
        var transizione = TransizioneDiAd2(aip, scalo.Icao);
        var nomeDelFile = file[(file.LastIndexOf('/') + 1)..];
        var gesto = $"{riga.Nome} ({nomeDelFile}) dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () =>
        {
            var righe = RigheDiAdesso(file);
            var (da, _) = conRecord.PostiDeiRecord(Modifiche.SporchiDi(file))[record];
            IReadOnlyList<string> cambiati;
            var nuova = confronto.Parte == Core.PdfAip.ParteDiAd2.Scali
                ? Core.PdfAip.Ad2NelSector.SostituitoLoScalo(righe[da], scalo, transizione, out cambiati)
                : Core.PdfAip.Ad2NelSector.SostituitaLaPista(righe[da], scalo.Runways, Core.PdfAip.ConfrontoDiAd2.Declinazione(scalo.MagneticVariation), out cambiati);
            if (nuova is null || cambiati.Count == 0)
                return Rifiuta($"{riga.Nome}: nella riga non c'è niente da cambiare.");
            var quanti = conRecord.RecordDelModello.Count;
            bool scritto = GestoSulTesto(file, "aip", f => Modifiche.CambiaRighe(f, new Dictionary<int, IReadOnlyList<string>> { [da + 1] = [nuova] }, gesto,
                riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più le stesse voci: si fa a mano."));
            Registro.Scrivi("aip", $"{gesto}: {string.Join(", ", cambiati)}");
            return scritto;
        });
        if (fatto)
            RifaiIlConfrontoDiAd2();
        return fatto;
    }

    /// <summary>L'altitudine di transizione di AD 2.17 di uno scalo, come la stampa l'AIP, o null.</summary>
    private static string? TransizioneDiAd2(global::Aip.AipDocument aip, string icao) =>
        aip.Airspaces.FirstOrDefault(a => a.Section == "AD 2.17" && a.TransitionAltitude is not null
            && string.Equals(Core.PdfAip.ConfrontoDiAd2.ScaloDellaPagina(a.Page), icao, StringComparison.OrdinalIgnoreCase))?.TransitionAltitude;

    /// <summary>
    /// «Sostituisci ovunque» (riordino R8c): la stessa voce diversa — uno scalo, una pista — in tutti i file che la hanno
    /// (<c>OTHER/it…</c> e le copie delle FIR), ognuna coi valori dell'AIP diversi dalla sua riga. Un gesto solo.
    /// </summary>
    public bool SostituisciOvunqueInAd2(Core.PdfAip.DifferenzaDiAd2 riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione || UltimoConfrontoDiAd2 is not { Parte: Core.PdfAip.ParteDiAd2.Scali or Core.PdfAip.ParteDiAd2.Piste } confronto || _aipDiAd2 is not { } aip)
            return Rifiuta("Confronta gli scali o le piste prima.");
        if (aip.Aerodromes?.FirstOrDefault(a => a.Icao.Equals(riga.Scalo, StringComparison.OrdinalIgnoreCase)) is not { } scalo)
            return Rifiuta($"{riga.Scalo}: non è nell'AIP espanso del confronto.");
        var copie = CopieDiversaInAd2(riga);
        if (copie.Count == 0)
            return Rifiuta("Si sostituisce una voce del sector diversa da quella dell'AIP.");
        var transizione = TransizioneDiAd2(aip, scalo.Icao);
        var declinazione = Core.PdfAip.ConfrontoDiAd2.Declinazione(scalo.MagneticVariation);
        var gesto = $"{riga.Nome} in {copie.Count} file dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () =>
        {
            var fatti = new List<string>();
            foreach (var copia in copie)
            {
                if (sessione.File.GetValueOrDefault(copia.File!) is not IFileConRecord conRecord || copia.Record!.Value >= conRecord.RecordDelModello.Count)
                    continue;
                var righe = RigheDiAdesso(copia.File!);
                var (da, _) = conRecord.PostiDeiRecord(Modifiche.SporchiDi(copia.File!))[copia.Record.Value];
                IReadOnlyList<string> cambiati;
                var nuova = confronto.Parte == Core.PdfAip.ParteDiAd2.Scali
                    ? Core.PdfAip.Ad2NelSector.SostituitoLoScalo(righe[da], scalo, transizione, out cambiati)
                    : Core.PdfAip.Ad2NelSector.SostituitaLaPista(righe[da], scalo.Runways, declinazione, out cambiati);
                if (nuova is null || cambiati.Count == 0)
                    continue;
                var quanti = conRecord.RecordDelModello.Count;
                if (!GestoSulTesto(copia.File!, "aip", f => Modifiche.CambiaRighe(f, new Dictionary<int, IReadOnlyList<string>> { [da + 1] = [nuova] }, gesto,
                        riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più le stesse voci: si fa a mano.")))
                    return false;
                fatti.Add($"{Path.GetFileName(copia.File!)}: {string.Join(", ", cambiati)}");
            }
            if (fatti.Count == 0)
                return Rifiuta($"{riga.Nome}: nelle righe non c'è niente da cambiare.");
            Registro.Scrivi("aip", $"{gesto}: {string.Join("; ", fatti)}");
            return true;
        });
        if (fatto)
            RifaiIlConfrontoDiAd2();
        return fatto;
    }

    /// <summary>Le righe del confronto di AD 2 con la stessa voce diversa di <paramref name="riga"/>, una per file (R8c).</summary>
    public IReadOnlyList<Core.PdfAip.DifferenzaDiAd2> CopieDiversaInAd2(Core.PdfAip.DifferenzaDiAd2 riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        return UltimoConfrontoDiAd2 is { } confronto
            ? [.. confronto.Esiti.Where(e => e is { Esito: Core.PdfAip.EsitoDelConfronto.FormaDiversa, File: not null, Record: not null }
                && string.Equals(e.Nome, riga.Nome, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Scalo, riga.Scalo, StringComparison.OrdinalIgnoreCase))]
            : [];
    }

    /// <summary>I file dove può andare uno scalo (<c>.ap</c>) o una pista (<c>.rw</c>) nuovi (R8c): quello di tutta Italia per primo.</summary>
    public IReadOnlyList<string> FileInAd2(bool piste) => Sessione is { } s ? Core.PdfAip.Ad2NelSector.FileDi(s.File.Keys, piste) : [];

    /// <summary>
    /// I file proposti per uno scalo o una pista nuovi (R8c): quello di tutta Italia, e le copie delle FIR che hanno già lo
    /// scalo (in un <c>.ap</c> per una pista, nello stesso genere di file per lo scalo).
    /// </summary>
    public IReadOnlyList<string> FileProposteInAd2(Core.PdfAip.DifferenzaDiAd2 riga, bool piste)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione)
            return [];
        var tutti = FileInAd2(piste);
        bool HaLoScalo(string file, bool deiPiste) => sessione.File.GetValueOrDefault(file) is IFileConRecord f && f.RecordDelModello.Any(r => r switch
        {
            Vipi.Sectorfile.Models.Runway p => !deiPiste ? false : p.IcaoCode.Trim().Equals(riga.Scalo, StringComparison.OrdinalIgnoreCase),
            Vipi.Sectorfile.Models.AirportInfo a => deiPiste ? false : a.IcaoCode.Trim().Equals(riga.Scalo, StringComparison.OrdinalIgnoreCase),
            _ => false,
        });
        // La FIR di uno scalo è quella del .ap che lo tiene: lirr.ap → lirr.rw.
        var firDelloScalo = FileInAd2(piste: false).Skip(1).Where(f => HaLoScalo(f, deiPiste: false))
            .Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. tutti.Where((f, i) => i == 0 || HaLoScalo(f, piste) || firDelloScalo.Contains(Path.GetFileNameWithoutExtension(f)))];
    }

    /// <summary>
    /// L'anteprima di «metti in questo file» per uno scalo o una pista nuovi (R8c, D19): per ogni file scelto la riga e dove
    /// va. Niente si scrive. Vuota, col perché in <paramref name="perche"/>, se la voce non si può portare.
    /// </summary>
    public IReadOnlyList<(string File, string Riga, string Dove)> AnteprimaInAd2(Core.PdfAip.DifferenzaDiAd2 riga, IReadOnlyCollection<string> file, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(riga);
        ArgumentNullException.ThrowIfNull(file);
        perche = null;
        if (Sessione is not { } sessione || RigaNuovaInAd2(riga, out perche) is not { } nuova)
            return [];
        var anteprima = new List<(string, string, string)>();
        foreach (var f in file)
        {
            if (sessione.File.GetValueOrDefault(f) is not IFileConRecord conRecord)
                continue;
            var (dopo, _) = PostoInAd2(conRecord, riga.Scalo);
            string dove = dopo >= 0 ? $"dopo {IcaoDi(conRecord.RecordDelModello[dopo])}" : conRecord.RecordDelModello.Count > 0 ? $"prima di {IcaoDi(conRecord.RecordDelModello[0])}" : "nel file vuoto";
            anteprima.Add((f, nuova, dove));
        }
        return anteprima;
    }

    private static string? IcaoDi(object record) => record switch
    {
        Vipi.Sectorfile.Models.Runway p => $"{p.IcaoCode.Trim()} {p.Designator1.Trim()}/{p.Designator2.Trim()}",
        Vipi.Sectorfile.Models.AirportInfo a => a.IcaoCode.Trim(),
        _ => null,
    };

    /// <summary>La riga nuova di una pista o di uno scalo che il sector non ha, dall'AIP del confronto; null col perché.</summary>
    private string? RigaNuovaInAd2(Core.PdfAip.DifferenzaDiAd2 riga, out string? perche)
    {
        perche = null;
        if (UltimoConfrontoDiAd2 is not { Parte: Core.PdfAip.ParteDiAd2.Scali or Core.PdfAip.ParteDiAd2.Piste } confronto || _aipDiAd2 is not { } aip)
        {
            perche = "Confronta gli scali o le piste prima.";
            return null;
        }
        if (riga.Esito != Core.PdfAip.EsitoDelConfronto.MancaNelSector
            || aip.Aerodromes?.FirstOrDefault(a => a.Icao.Equals(riga.Scalo, StringComparison.OrdinalIgnoreCase)) is not { } scalo)
        {
            perche = confronto.Parte == Core.PdfAip.ParteDiAd2.Piste ? "Si porta una pista che il sector non ha." : "Si porta uno scalo che il sector non ha.";
            return null;
        }
        if (confronto.Parte == Core.PdfAip.ParteDiAd2.Scali)
        {
            var scritta = Core.PdfAip.Ad2NelSector.RigaNuovaDelloScalo(scalo, TransizioneDiAd2(aip, scalo.Icao));
            perche = scritta is null ? $"{riga.Scalo}: l'AIP non dà il punto di riferimento; si scrive a mano." : null;
            return scritta;
        }
        var testate = riga.Nome[(riga.Scalo.Length + 1)..].Split('/');
        var piste = testate.Select(t => scalo.Runways.FirstOrDefault(r => Core.PdfAip.ConfrontoDiAd2.Designatore(r.Designator) == Core.PdfAip.ConfrontoDiAd2.Designatore(t))).ToList();
        if (piste.Count != 2 || piste[0] is not { } prima || piste[1] is not { } opposta)
        {
            perche = $"{riga.Nome}: l'AIP non ha le due testate; si scrive a mano.";
            return null;
        }
        var nuova = Core.PdfAip.Ad2NelSector.RigaNuovaDellaPista(scalo.Icao, prima, opposta, Core.PdfAip.ConfrontoDiAd2.Declinazione(scalo.MagneticVariation));
        perche = nuova is null ? $"{riga.Nome}: l'AIP non dà le soglie; si scrive a mano." : null;
        return nuova;
    }

    /// <summary>
    /// Dove va una riga nuova di <paramref name="icao"/> in un <c>.ap</c> o in un <c>.rw</c>: dopo l'ultima voce con
    /// l'indicativo che non viene dopo (null se nessuna: prima della prima). Le voci di un <c>.rw</c> sono solo le piste
    /// della sezione <c>//PISTE</c>: le mappe e le ACC del file sono testo, una riga nuova non ci finisce.
    /// </summary>
    private static (int Dopo, int Record) PostoInAd2(IFileConRecord conRecord, string icao)
    {
        var dopo = conRecord.RecordDelModello.Select((r, i) => (r, i))
            .LastOrDefault(x => IcaoDi(x.r) is { } k && string.CompareOrdinal(k.Split(' ')[0], icao) <= 0, (null!, -1)).i;
        return (dopo, dopo >= 0 ? dopo : 0);
    }

    /// <summary>
    /// «Porta nel sector» su una pista che nessun <c>.rw</c> ha (slice 5c): la riga nuova, nella sezione <c>//PISTE</c>, dopo
    /// le piste dello scalo (o in ordine di indicativo), con le due testate dell'AIP. Un gesto.
    /// <para>R8c: nei <paramref name="file"/> scelti fra i <c>.rw</c> (null = <c>itrw.rw</c>, com'era), anche le copie delle FIR.</para>
    /// </summary>
    public bool PortaLaPista(Core.PdfAip.DifferenzaDiAd2 riga, IReadOnlyCollection<string>? file = null) => PortaInAd2(riga, file, piste: true);

    /// <summary>
    /// «Porta nel sector» su uno scalo che nessun <c>.ap</c> ha (riordino R8c): la riga nuova coi valori dell'AIP, in ordine di
    /// indicativo, nei <paramref name="file"/> scelti fra i <c>.ap</c> (null = <c>itap.ap</c>). Un gesto.
    /// </summary>
    public bool PortaLoScalo(Core.PdfAip.DifferenzaDiAd2 riga, IReadOnlyCollection<string>? file = null) => PortaInAd2(riga, file, piste: false);

    private bool PortaInAd2(Core.PdfAip.DifferenzaDiAd2 riga, IReadOnlyCollection<string>? scelti, bool piste)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione || UltimoConfrontoDiAd2 is not { } confronto
            || confronto.Parte != (piste ? Core.PdfAip.ParteDiAd2.Piste : Core.PdfAip.ParteDiAd2.Scali))
            return Rifiuta(piste ? "Confronta le piste prima." : "Confronta gli scali prima.");
        if (RigaNuovaInAd2(riga, out var perche) is not { } nuova)
            return Rifiuta(perche!);
        var ammessi = FileInAd2(piste);
        var file = scelti is null ? ammessi.Take(1).ToList() : [.. ammessi.Where(scelti.Contains)];
        if (scelti is not null && scelti.Any(f => !ammessi.Contains(f, StringComparer.Ordinal)))
            return Rifiuta($"{scelti.First(f => !ammessi.Contains(f, StringComparer.Ordinal))} non è un file {(piste ? "delle piste (.rw)" : "degli scali (.ap)")} del sector.");
        var nazionale = piste ? Core.PdfAip.Ad2NelSector.FileDellePiste : Core.PdfAip.Ad2NelSector.FileDegliScali;
        if (file.Count == 0 || file.Any(f => sessione.File[f] is not IFileConRecord { RecordDelModello.Count: > 0 }))
            return Rifiuta(file.Count == 0 ? "Scegli almeno un file." : $"Nel sector non c'è {nazionale} con le voci.");
        var gesto = $"{riga.Nome} dall'AIP {confronto.Ciclo}" + (scelti is null ? "" : $" in {string.Join(", ", file.Select(Path.GetFileName))}");
        bool fatto = NellaStoria(gesto, () =>
        {
            foreach (var f in file)
            {
                var conRecord = (IFileConRecord)sessione.File[f];
                var righe = RigheDiAdesso(f);
                var posti = conRecord.PostiDeiRecord(Modifiche.SporchiDi(f));
                var (dopo, _) = PostoInAd2(conRecord, riga.Scalo);
                var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
                if (dopo >= 0)
                {
                    var a = posti[dopo].Da + posti[dopo].Quante - 1;
                    sostituzioni[a + 1] = [righe[a], nuova];
                }
                else
                {
                    var da = posti[0].Da;
                    sostituzioni[da + 1] = [nuova, righe[da]];
                }
                var quanti = conRecord.RecordDelModello.Count;
                if (!GestoSulTesto(f, "aip", m => Modifiche.CambiaRighe(m, sostituzioni, gesto,
                        riletto => riletto.RecordDelModello.Count == quanti + 1 ? null : "Riletto, il file non ha la voce nuova: si fa a mano.")))
                    return false;
            }
            Registro.Scrivi("aip", $"{gesto}: {nuova}");
            return true;
        });
        if (fatto)
            RifaiIlConfrontoDiAd2();
        return fatto;
    }

    /// <summary>La radioassistenza di AD 2.19 di una riga del confronto (la stessa sigla e tipo, del primo scalo che l'ha).</summary>
    private global::Aip.Navaid? RadioassistenzaDiAd2(string chiave) =>
        _aipDiAd2?.Aerodromes?.SelectMany(s => s.NavigationAids).FirstOrDefault(n => n.Key.Equals(chiave, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// «Sostituisci» su una radioassistenza di AD 2.19 diversa (carta F6, slice 5f): la riga del <c>.vor</c> o del <c>.ndb</c>
    /// coi valori di AD 2.19 che sono diversi, come il gesto di ENR 4 (slice 3c). Un gesto.
    /// </summary>
    public bool SostituisciLaRadioassistenzaDiAd2(Core.PdfAip.DifferenzaDiAd2 riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is not { } sessione || UltimoConfrontoDiAd2 is not { Parte: Core.PdfAip.ParteDiAd2.Radioassistenze } confronto)
            return Rifiuta("Confronta le radioassistenze di AD 2.19 prima.");
        if (riga is not { Esito: Core.PdfAip.EsitoDelConfronto.FormaDiversa, File: { } file, Record: { } record }
            || sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record >= conRecord.RecordDelModello.Count)
            return Rifiuta("Si sostituisce una radioassistenza del sector diversa da quella dell'AIP.");
        if (RadioassistenzaDiAd2(riga.Nome) is not { } n)
            return Rifiuta($"{riga.Nome}: non è nell'AD 2.19 del confronto.");
        bool fatto = SostituisciLaRigaDellaRadioassistenza(conRecord, file, record, riga.Nome, n, confronto.Ciclo);
        if (fatto)
            RifaiIlConfrontoDiAd2();
        return fatto;
    }

    /// <summary>
    /// «Porta nel sector» (slice 5f): le radioassistenze di AD 2.19 scelte fra quelle che il sector non ha, come righe nuove di
    /// <c>itvor.vor</c> o <c>itndb.ndb</c> in ordine di nome, come il gesto di ENR 4. Un gesto solo.
    /// </summary>
    public bool PortaLeRadioassistenzeDiAd2(IReadOnlyList<string> nomi)
    {
        ArgumentNullException.ThrowIfNull(nomi);
        if (Sessione is not { } sessione || UltimoConfrontoDiAd2 is not { Parte: Core.PdfAip.ParteDiAd2.Radioassistenze } confronto)
            return Rifiuta("Confronta le radioassistenze di AD 2.19 prima: si portano le righe del confronto.");
        var mancanti = confronto.Esiti.Where(e => e.Esito == Core.PdfAip.EsitoDelConfronto.MancaNelSector).Select(e => e.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scelte = nomi.Where(mancanti.Contains).Select(RadioassistenzaDiAd2).OfType<global::Aip.Navaid>().ToList();
        if (scelte.Count == 0)
            return Rifiuta("Si portano le radioassistenze che il sector non ha.");
        string? Chiave(string nome) => sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + nome, StringComparison.OrdinalIgnoreCase));
        var gesto = scelte.Count == 1 ? $"{scelte[0].Key} dall'AIP {confronto.Ciclo}" : $"{scelte.Count} radioassistenze di AD 2.19 dall'AIP {confronto.Ciclo}";
        var avvisi = new List<string>();
        bool fatto = NellaStoria(gesto, () =>
        {
            avvisi.Clear();
            var toccati = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in scelte)
            {
                var file = Chiave(Core.PdfAip.Enr4NelSector.NeiNdb(n) ? Core.PdfAip.Enr4NelSector.FileDegliNdb : Core.PdfAip.Enr4NelSector.FileDeiVor);
                if (PortaUnaRiga(sessione, n.Key, file, Core.PdfAip.Enr4NelSector.RigaNuova(n), n.Designator, avvisi))
                    toccati.Add(file!);
            }
            foreach (var f in toccati) RifaiLaGeometria(f);
            Registro.Scrivi("aip", $"{gesto}: {toccati.Count} file" + (avvisi.Count > 0 ? "; " + string.Join("; ", avvisi) : ""));
            Rifiuto = avvisi.Count > 0 ? string.Join(" · ", avvisi) : null;
            RicontrollaLeModifiche();
            Avvisa();
            return toccati.Count > 0;
        });
        if (fatto)
            RifaiIlConfrontoDiAd2();
        return fatto;
    }

    /// <summary>Dopo un gesto o un «Annulla» il confronto di AD 2 si rifà sul sector di adesso.</summary>
    private void RifaiIlConfrontoDiAd2()
    {
        if (Sessione is not { } sessione || UltimoConfrontoDiAd2 is not { } confronto || _aipDiAd2 is not { } aip)
            return;
        UltimoConfrontoDiAd2 = (confronto.Ciclo, confronto.Parte, Core.PdfAip.ConfrontoDiAd2.Di(confronto.Parte, aip, sessione));
        Avvisa();
    }

    /// <summary>Il modello da cui viene <see cref="UltimoConfrontoDiEnr3"/>: i gesti della slice 2c ne prendono rotte e attese.</summary>
    private global::Aip.AipDocument? _aipDiEnr3;

    /// <summary>Per le attese dell'ultimo confronto: dove e come portarle (slice 2c), per punto.</summary>
    public IReadOnlyDictionary<string, Core.PdfAip.PropostaDiAttesa> ProposteDelleAttese { get; private set; } =
        new Dictionary<string, Core.PdfAip.PropostaDiAttesa>();

    /// <summary>
    /// «Scrivi i tratti» (carta F6, slice 2c, B2): sulle rotte date, i tratti che l'AIP e il sector hanno tutti e due
    /// ricevono nei tag <c>//@@</c> del punto che li apre il verso (rispetto all'ordine del file) e le quote dell'AIP. Le
    /// aerovie <c>locked=si</c> non si toccano (B5). Un gesto solo: diff, annulla, lotto di Aurora. Dalla R8b le rotte
    /// stanno nelle aerovie basse o alte: una modifica per file.
    /// </summary>
    public bool ScriviITratti(IReadOnlyList<string> rotte)
    {
        ArgumentNullException.ThrowIfNull(rotte);
        if (Sessione is not { } sessione || _aipDiEnr3?.Routes is not { } dellAip || UltimoConfrontoDiEnr3 is not { } confronto)
            return Rifiuta("Confronta le rotte prima: si scrivono i tratti di una riga del confronto.");
        string gesto = rotte.Count == 1 ? $"tratti di {rotte[0]} dall'AIP {confronto.Ciclo}" : $"tratti di {rotte.Count} rotte dall'AIP {confronto.Ciclo}";
        var avvisi = new List<string>();
        int tratti = 0;
        bool fatto = NellaStoria(gesto, () =>
        {
            avvisi.Clear();
            tratti = 0;
            // Tutte le righe calcolate sul testo di adesso, e una modifica sola per file: una voce nel testo del file.
            var perFile = new Dictionary<string, Dictionary<int, IReadOnlyList<string>>>(StringComparer.Ordinal);
            foreach (var nome in rotte)
            {
                if (dellAip.FirstOrDefault(r => r.Designator.Equals(nome, StringComparison.OrdinalIgnoreCase)) is not { } rotta) continue;
                if (Core.PdfAip.ConfrontoDiEnr3.TrattiDaScrivere(rotta, sessione, out var file, out var bloccata) is not { } daScrivere || file is null)
                {
                    avvisi.Add($"{nome}: il sector non ha l'aerovia");
                    continue;
                }
                if (bloccata)
                {
                    avvisi.Add($"{nome}: è locked=si, l'import non la tocca");
                    continue;
                }
                if (!perFile.TryGetValue(file, out var sostituzioni))
                    perFile[file] = sostituzioni = [];
                foreach (var (riga, nuove) in Core.PdfAip.ConfrontoDiEnr3.RigheDeiTratti(RigheDiAdesso(file), rotta.Designator, daScrivere))
                    sostituzioni[riga] = nuove;
                tratti += daScrivere.Count;
            }
            if (perFile.Values.All(s => s.Count == 0))
            {
                Rifiuto = avvisi.Count > 0 ? string.Join(" · ", avvisi) : "Non c'è nessun tratto da scrivere.";
                Avvisa();
                return false;
            }
            bool scritto = true;
            foreach (var (file, sostituzioni) in perFile.Where(f => f.Value.Count > 0))
            {
                var quanti = ((IFileConRecord)sessione.File[file]).RecordDelModello.Count;
                scritto &= GestoSulTesto(file, "aip", f => Modifiche.CambiaRighe(f, sostituzioni, gesto,
                    riletto => riletto.RecordDelModello.Count == quanti ? null : "Riletto, il file non ha più le stesse aerovie: si fa a mano.", anchiITag: true));
            }
            Registro.Scrivi("aip", $"{gesto}: {tratti} tratti" + (avvisi.Count > 0 ? "; " + string.Join("; ", avvisi) : ""));
            if (scritto && avvisi.Count > 0)
            {
                Rifiuto = string.Join(" · ", avvisi);
                Avvisa();
            }
            return scritto;
        });
        if (fatto)
            RifaiIlConfrontoDiEnr3();
        return fatto;
    }

    /// <summary>Come portare un'attesa scelta: il punto, lo scalo (null = in rotta) e se disegnare l'ovale.</summary>
    public sealed record SceltaDiAttesa(string Fix, string? Scalo, bool Ovale);

    /// <summary>
    /// «Porta le selezionate» (carta F6, slice 2c, committente 19 ottobre): le attese scelte nel sector — di procedura come
    /// voce <c>HLD-PUNTO</c> nello <c>.str</c> dello scalo (la scritta, e l'ovale se scelto), in rotta come riga di
    /// <c>HOLDENR.hold</c> in ordine di nome (la scritta; l'ovale in rotta lo decide il committente più avanti). Un gesto solo.
    /// </summary>
    public bool PortaLeAttese(IReadOnlyList<SceltaDiAttesa> scelte)
    {
        ArgumentNullException.ThrowIfNull(scelte);
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr3 is not { Rotte: false } confronto)
            return Rifiuta("Confronta le attese prima: si portano le righe scelte del confronto.");
        if (scelte.Count == 0)
            return Rifiuta("Scegli almeno un'attesa da portare.");
        var avvisi = new List<string>();
        string gesto = scelte.Count == 1 ? $"attesa {scelte[0].Fix} dall'AIP {confronto.Ciclo}" : $"{scelte.Count} attese dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () =>
        {
            int portate = 0;
            avvisi.Clear();
            var toccati = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scelta in scelte)
            {
                if (!ProposteDelleAttese.TryGetValue(scelta.Fix, out var proposta))
                {
                    avvisi.Add($"{scelta.Fix}: non è fra le attese del confronto");
                    continue;
                }
                if (proposta.Punto is not { } punto)
                {
                    avvisi.Add($"{scelta.Fix}: il sector non ha il punto");
                    continue;
                }
                object esito;
                string? file;
                if (scelta.Scalo is null)
                {
                    if (scelta.Ovale)
                    {
                        avvisi.Add($"{scelta.Fix}: l'ovale in rotta non si scrive ancora (dove, lo decide il committente)");
                        continue;
                    }
                    file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + Core.PdfAip.ConfrontoDiEnr3.FileDelleAttese, StringComparison.OrdinalIgnoreCase));
                    if (file is null || sessione.File[file] is not IFileConRecord hold)
                    {
                        avvisi.Add($"{scelta.Fix}: nel sector non c'è HOLDENR.hold");
                        continue;
                    }
                    if (Core.PdfAip.AtteseNelSector.RigaInRotta(proposta.Attesa, punto) is not { } riga)
                    {
                        avvisi.Add($"{scelta.Fix}: la quota «{proposta.Attesa.Minimum.Text}» non ha la forma di HOLDENR.hold");
                        continue;
                    }
                    // In ordine di nome, come il file.
                    var nome = $"HLD-{scelta.Fix}";
                    int prima = hold.RecordDelModello.Select((r, i) => (r, i))
                        .FirstOrDefault(x => x.r is Attesa a && string.CompareOrdinal(a.Nome, nome) > 0, (null!, hold.RecordDelModello.Count)).i;
                    esito = Modifiche.AggiungiLeRighe(sessione.File[file], prima, [riga], $"{nome} dall'AIP", rigaVuota: false);
                }
                else
                {
                    if (proposta.Scali.FirstOrDefault(s => s.Icao.Equals(scelta.Scalo, StringComparison.OrdinalIgnoreCase)) is not { } scalo)
                    {
                        avvisi.Add($"{scelta.Fix}: {scelta.Scalo} non ha procedure che passano per il punto");
                        continue;
                    }
                    file = scalo.File;
                    // Già lì come attesa di scalo: un doppione no (105 delle 191 del 2610 sono così).
                    if (((IFileConRecord)sessione.File[file]).RecordDelModello.OfType<StrRecord>()
                        .Any(r => r.ProcedureId.Trim().Equals($"HLD-{scelta.Fix}", StringComparison.OrdinalIgnoreCase)))
                    {
                        avvisi.Add($"{scelta.Fix}: {NomeDelFile(file)} ha già HLD-{scelta.Fix}");
                        continue;
                    }
                    var a = proposta.Attesa;
                    var ovale = scelta.Ovale ? Core.PdfAip.AtteseNelSector.Ovale(punto, a.InboundTrack, a.Turn, proposta.TrattoNm, Core.PdfAip.AtteseNelSector.Ias(a)) : null;
                    var str = (IFileConRecord)sessione.File[file];
                    esito = Modifiche.AggiungiLeRighe(sessione.File[file], str.RecordDelModello.Count, Core.PdfAip.AtteseNelSector.RigheDiProcedura(a, scalo, ovale), $"HLD-{a.Fix} dall'AIP");
                }
                if (esito is ModificaDelTesto)
                {
                    portate++;
                    toccati.Add(file);
                }
                else if (esito is ModificaRifiutata rifiutata)
                    avvisi.Add($"{scelta.Fix}: {rifiutata.Motivo}");
            }
            foreach (var f in toccati) RifaiLaGeometria(f);
            Registro.Scrivi("aip", $"{gesto}: {portate} portate" + (avvisi.Count > 0 ? "; " + string.Join("; ", avvisi) : ""));
            Rifiuto = avvisi.Count > 0 ? string.Join(" · ", avvisi) : null;
            RicontrollaLeModifiche();
            Avvisa();
            return portate > 0;
        });
        if (fatto)
            RifaiIlConfrontoDiEnr3();
        return fatto;
    }

    /// <summary>
    /// «Sostituisci» su un'attesa diversa (slice 2c): la descrizione di <c>HOLDENR.hold</c> riscritta con rotta
    /// d'avvicinamento, virata e quota dell'attesa dell'AIP più somigliante (le tre del 19 ottobre: un grado di rotta).
    /// </summary>
    public bool SostituisciLAttesa(Core.PdfAip.DifferenzaDiEnr3 riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is null || UltimoConfrontoDiEnr3 is not { Rotte: false } confronto || _aipDiEnr3?.Holdings is not { } attese)
            return Rifiuta("Confronta le attese prima.");
        if (riga is not { Esito: Core.PdfAip.EsitoDelConfronto.FormaDiversa, File: { } file, Record: { } record }
            || Sessione.File.GetValueOrDefault(file) is not IFileConRecord hold || record >= hold.RecordDelModello.Count
            || hold.RecordDelModello[record] is not Attesa suo)
            return Rifiuta("Si sostituisce un'attesa di HOLDENR.hold diversa da quella dell'AIP.");
        var vicina = attese.Where(a => a.Fix.Equals(riga.Nome, StringComparison.OrdinalIgnoreCase))
            .MinBy(a => suo.Rotta is { } r ? Math.Min(Math.Abs(a.InboundTrack - r), 360 - Math.Abs(a.InboundTrack - r)) : 999);
        if (vicina is null || Core.PdfAip.AtteseNelSector.Descrizione(vicina) is not { } descrizione)
            return Rifiuta($"{riga.Nome}: l'attesa dell'AIP non ha una quota nella forma di HOLDENR.hold.");
        bool fatto = CambiaCampo(file, record, nameof(Attesa.Descrizione), descrizione);
        if (fatto)
            RifaiIlConfrontoDiEnr3();
        return fatto;
    }

    /// <summary>
    /// «Porta nel sector» una rotta che il sector non ha (carta F6, slice 2c): il suo blocco in <c>itawlow.lairway</c>, in
    /// ordine di nome, coi punti dell'AIP e un tag per tratto col verso e le quote dell'AIP, le sue etichette. Il blocco porta
    /// la nota del ciclo e non <c>locked=si</c>: non è fatta a mano. I punti devono essere nel sector (fix, VOR, NDB).
    /// <para>R8b («metti in questo file»): nel <paramref name="fileDelleRotte"/> scelto fra le aerovie basse e alte del
    /// sector (null = <c>itawlow.lairway</c>, com'era).</para>
    /// </summary>
    public bool PortaLaRotta(string nome, string? fileDelleRotte = null)
    {
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr3 is not { Rotte: true } confronto
            || _aipDiEnr3?.Routes?.FirstOrDefault(r => r.Designator.Equals(nome, StringComparison.OrdinalIgnoreCase)) is not { } rotta)
            return Rifiuta("Confronta le rotte prima: si porta una riga del confronto.");
        if (rotta.Segments.Any(s => s.Discontinuity))
            return Rifiuta($"{rotta.Designator} ha un'interruzione: si porta a mano, in due pezzi.");
        if (fileDelleRotte is not null && !FileDelleRotte.Contains(fileDelleRotte, StringComparer.Ordinal))
            return Rifiuta($"{fileDelleRotte} non è un file delle aerovie (.lairway, .hairway) del sector.");
        var file = fileDelleRotte
            ?? sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + Core.PdfAip.ConfrontoDiEnr3.FileDelleAerovie, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return Rifiuta("Nel sector non c'è itawlow.lairway.");
        var n = rotta.Designator;
        bool fatto = NellaStoria($"{n} portata dall'AIP {confronto.Ciclo}" + (fileDelleRotte is null ? "" : $" in {Path.GetFileName(file)}"), () =>
        {
            var gesto = BloccoDellaRotta(rotta, file, confronto.Ciclo);
            if (!GestoSulTesto(file, "aerovia", f => gesto.Sostituzioni is null
                    ? new ModificaRifiutata(gesto.Perche!)
                    : Modifiche.CambiaRighe(f, gesto.Sostituzioni, $"{n} dall'AIP", anchiITag: true)))
                return false;
            var piano = EtichetteDelleAerovie.Piano(RigheDiAdesso(file), PuntoPerNome, SogliaDelleEtichette, solo: n);
            if (!piano.Vuoto)
                GestoSulTesto(file, "etichette", f => Modifiche.CambiaRighe(f, piano.Sostituzioni(RigheDiAdesso(file)), $"etichette di {n}"));
            return true;
        });
        if (fatto)
            RifaiIlConfrontoDiEnr3();
        return fatto;
    }

    /// <summary>I file dove può andare una rotta (R8b): le aerovie basse e alte del sector, <c>itawlow.lairway</c> per primo.</summary>
    public IReadOnlyList<string> FileDelleRotte => Sessione is { } s ? Core.PdfAip.ConfrontoDiEnr3.FileDelleRotte(s.File.Keys) : [];

    /// <summary>
    /// L'anteprima di «metti in questo file» per una rotta (R8b, D19): le righe del blocco che si scriverebbe nel file dato e
    /// dove (dopo quale riga, o in testa). Niente si scrive. Null se la rotta non si può portare lì, col perché in
    /// <paramref name="perche"/>.
    /// </summary>
    public (IReadOnlyList<string> Righe, string Dove)? AnteprimaDellaRotta(string nome, string? file, out string? perche)
    {
        perche = null;
        if (UltimoConfrontoDiEnr3 is not { Rotte: true } confronto || file is null || !FileDelleRotte.Contains(file, StringComparer.Ordinal)
            || _aipDiEnr3?.Routes?.FirstOrDefault(r => r.Designator.Equals(nome, StringComparison.OrdinalIgnoreCase)) is not { } rotta)
            return null;
        if (rotta.Segments.Any(s => s.Discontinuity))
        {
            perche = $"{rotta.Designator} ha un'interruzione: si porta a mano, in due pezzi.";
            return null;
        }
        var gesto = BloccoDellaRotta(rotta, file, confronto.Ciclo);
        if (gesto.Sostituzioni is not { Count: 1 } sostituzioni)
        {
            perche = gesto.Perche;
            return null;
        }
        var righe = RigheDiAdesso(file);
        var (riga, nuove) = sostituzioni.Single();
        // La riga che c'era resta: in testa al blocco se il blocco va dopo, in coda se va prima.
        bool dopo = nuove.Count > 0 && riga - 1 < righe.Count && nuove[0] == righe[riga - 1];
        var blocco = dopo ? nuove.Skip(1).ToList() : nuove.Take(nuove.Count - 1).ToList();
        string dove = dopo ? $"dopo la riga {riga} («{righe[riga - 1].Trim()}»)" : riga == 1 ? "in testa al file" : $"prima della riga {riga} («{righe[riga - 1].Trim()}»)";
        return (blocco, dove);
    }

    /// <summary>Il blocco di una rotta dell'AIP nel file delle aerovie dato, sul testo di adesso (PortaLaRotta, l'anteprima).</summary>
    private GestoSullAerovia BloccoDellaRotta(global::Aip.Route rotta, string file, string ciclo) =>
        AerovieAMano.Nuova(RigheDiAdesso(file), rotta.Designator, [.. rotta.Points.Select(p => p.Designator)], PuntoPerNome,
            [.. rotta.Segments.Select(Core.PdfAip.ConfrontoDiEnr3.ChiaviDelTratto)], $"AIP {ciclo}");

    /// <summary>Dopo un gesto della slice 2c il confronto di ENR 3 si rifà sul sector di adesso.</summary>
    private void RifaiIlConfrontoDiEnr3()
    {
        if (Sessione is not { } sessione || UltimoConfrontoDiEnr3 is not { } confronto || _aipDiEnr3 is not { } aip)
            return;
        var catalogo = CatalogoScelto;
        UltimoConfrontoDiEnr3 = (confronto.Ciclo, confronto.Rotte, confronto.Rotte
            ? Core.PdfAip.ConfrontoDiEnr3.Rotte(aip.Routes ?? [], sessione, nome => catalogo?.Cerca(nome)?.Posizione)
            : Core.PdfAip.ConfrontoDiEnr3.Attese(aip.Holdings ?? [], sessione));
        Avvisa();
    }

    /// <summary>
    /// I tratti di confine e di costa dell'AIP sul tracciato di <c>itgeo.geo</c> (lo sfondo della mappa): l'eccezione del
    /// committente, 19 ottobre. Null se il sector non ha lo sfondo.
    /// </summary>
    private global::Aip.Aurora.BorderTracer? ConfiniDelSector()
    {
        var confini = new Core.PdfAip.ConfiniDelSector(Strati.FirstOrDefault(s => s.Id == StratiDellaMappa.Sfondo.Id)?.Forme.SelectMany(f => f.Tratti) ?? []);
        return confini.Punti > 0 ? confini.Tracciatore : null;
    }

    /// <summary>Il modello dell'AIP espanso da cui viene <see cref="UltimoConfronto"/>: i gesti della slice 1c ne prendono le zone.</summary>
    private global::Aip.AipDocument? _aipDelConfronto;

    /// <summary>
    /// «Sostituisci» (carta F6, slice 1c): la voce del sector con la forma diversa — un CTR o un'ATZ del <c>MAPS</c>, un
    /// settore di <c>twrs.tfl</c> — riscritta per intero con le zone dell'AIP del confronto. La testata resta (coi tag
    /// sopra); gli archi alla densità che la voce ha già, o a quella di base; i tratti di confine presi dalla linea di
    /// adesso. Un gesto come gli altri: diff, annulla, lotto di Aurora.
    /// </summary>
    public bool SostituisciLaVoce(Core.PdfAip.DifferenzaDiForma riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is null || UltimoConfronto is not { } confronto || _aipDelConfronto is not { } aip)
            return Rifiuta("Confronta prima: si sostituisce una riga del confronto.");
        if (confronto.Famiglia == Core.PdfAip.FamigliaDelConfronto.AreeEnr5)
            return Rifiuta("Le aree di ENR 5 si scrivono coi loro gesti (Sostituisci, Porta nel sector delle aree di ENR 5).");
        if (riga is not { Esito: Core.PdfAip.EsitoDelConfronto.FormaDiversa, Aip: { } nome, File: { } file, Record: { } record, Sector: { } voce })
            return Rifiuta("Si sostituisce una voce del sector con la forma diversa da quella dell'AIP.");
        if (Core.PdfAip.ConfrontoDelleForme.DaGuardare(riga))
            return Rifiuta($"{nome}: più di {Core.PdfAip.ConfrontoDelleForme.ScartoDaGuardare:0} NM fra l'AIP e il sector, quasi certo un errore di stampa. Guarda il PDF: si corregge a mano.");
        // La linea di adesso, coi punti per nome già risolti: deve essere ancora quella voce.
        if (Strati.SelectMany(s => s.Forme).FirstOrDefault(f => f.File == file && f.Record == record && f.Etichetta == voce) is not { } forma)
            return Rifiuta($"La voce {voce} non è più dov'era quando hai confrontato: confronta di nuovo.");

        var vecchia = forma.Tratti;
        var poligono = confronto.Famiglia == Core.PdfAip.FamigliaDelConfronto.AtzDelleTorri;
        var densita = Core.PdfAip.ZoneNelSector.DensitaDi(vecchia, DensitaDiBase);
        if (Core.PdfAip.ZoneNelSector.Corpo(Core.PdfAip.ZoneNelSector.ZoneDi(aip.Airspaces, nome, confronto.Famiglia), vecchia, densita, out var perche, poligono, ConfiniDelSector()) is not { } corpo)
            return Rifiuta(perche!);

        string gesto = $"{voce}: la forma di {nome} dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () => GestoSulTesto(file, "aip", f => Modifiche.SostituisciIlCorpo(f, record, corpo, gesto)));
        if (fatto)
            RifaiIlConfronto();
        return fatto;
    }

    /// <summary>
    /// «Porta nel sector» (carta F6, slice 1c): un CTR o un'ATZ che nel sector manca, scritto come voce nuova del
    /// <c>MAPS</c> nello <c>.str</c> dello scalo <paramref name="icao"/> (lo dice l'AOD: l'AIP nomina lo spazio per città),
    /// prima delle altre voci del <c>MAPS</c>. Lo <c>.str</c> deve esserci e non avere già quella voce. I settori di
    /// <c>twrs.tfl</c> non si portano: un settore nuovo è una posizione nuova, si fa a mano.
    /// </summary>
    public bool PortaNelSector(Core.PdfAip.DifferenzaDiForma riga, string? icao)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is null || UltimoConfronto is not { } confronto || _aipDelConfronto is not { } aip)
            return Rifiuta("Confronta prima: si porta una riga del confronto.");
        if (confronto.Famiglia == Core.PdfAip.FamigliaDelConfronto.AreeEnr5)
            return Rifiuta("Le aree di ENR 5 si scrivono coi loro gesti (Sostituisci, Porta nel sector delle aree di ENR 5).");
        if (riga is not { Esito: Core.PdfAip.EsitoDelConfronto.MancaNelSector, Aip: { } nome })
            return Rifiuta("Si porta nel sector uno spazio che nel sector manca.");
        var scalo = icao?.Trim().ToUpperInvariant() ?? "";
        if (Core.PdfAip.ZoneNelSector.Testata(scalo, confronto.Famiglia) is not { } testata)
            return Rifiuta("Un settore nuovo della torre è una posizione nuova: si scrive a mano in twrs.tfl.");
        if (scalo.Length != 4 || !scalo.All(char.IsAsciiLetter))
            return Rifiuta("Scrivi lo scalo, quattro lettere (LIPE): la voce va nel suo .str.");
        var file = Sessione.File.Keys.FirstOrDefault(k => string.Equals(NomeDelFile(k), scalo + ".str", StringComparison.OrdinalIgnoreCase));
        if (file is null || Sessione.File[file] is not IFileConRecord conRecord)
            return Rifiuta($"Nel sector non c'è {scalo.ToLowerInvariant()}.str: prima va creato lo scalo.");
        var etichetta = testata.Split(';')[2];
        if (conRecord.RecordDelModello.OfType<StrRecord>().Any(v => v.RunwaySpec == "MAPS" && v.ProcedureId.Equals(etichetta, StringComparison.OrdinalIgnoreCase)))
            return Rifiuta($"{NomeDelFile(file)} ha già la voce {etichetta}: se la forma è diversa, si sostituisce.");
        if (Core.PdfAip.ZoneNelSector.Corpo(Core.PdfAip.ZoneNelSector.ZoneDi(aip.Airspaces, nome, confronto.Famiglia), [], DensitaDiBase, out var perche, riserva: ConfiniDelSector()) is not { } corpo)
            return Rifiuta(perche!);

        // Prima della prima voce del MAPS; in un file senza MAPS, prima del primo record; in un file vuoto di record, in fondo.
        int prima = Math.Max(0, conRecord.RecordDelModello.ToList().FindIndex(r => r is StrRecord { RunwaySpec: "MAPS" }));
        IReadOnlyList<string> righe = [testata, .. corpo];
        string gesto = $"{etichetta} portato dall'AIP {confronto.Ciclo} ({nome})";
        bool fatto = NellaStoria(gesto, () => GestoSulTesto(file, "aip", f => Modifiche.AggiungiLeRighe(f, prima, righe, gesto)));
        if (fatto)
        {
            Scelta = (file, prima);
            RifaiIlConfronto();
        }
        return fatto;
    }

    /// <summary>
    /// Dopo «Annulla», «Ripeti» o «Annulla tutto» i confronti con l'AIP aperti (CTR e ATZ, ENR 3, ENR 4) si rifanno sul sector
    /// di adesso: prima il riassunto restava quello del gesto annullato (visto sul banco nella slice 3c, «uguali 55» dopo
    /// aver annullato i due «Sostituisci»).
    /// </summary>
    private void RifaiIConfrontiDellAip()
    {
        RifaiIlConfronto();
        RifaiIlConfrontoDiEnr3();
        RifaiIlConfrontoDiEnr4();
        RifaiIlConfrontoDiAd2();
    }

    /// <summary>
    /// «Sostituisci» per le aree di ENR 5 (carta F6, slice 4c): le righe della voce del sector (i lati col suo nome vicini
    /// all'area, e il commento sopra) al posto loro un blocco per area dell'AIP — una voce che vale più aree (<c>R300AB</c>)
    /// si divide in una voce per area (committente, 19 ottobre: «dividi»). Vale anche per una voce uguale che vale più aree.
    /// I cerchi col raggio dell'AIP (committente: «porta 0,5»). Un gesto: diff, annulla, lotto di Aurora.
    /// </summary>
    public bool SostituisciAreaEnr5(Core.PdfAip.DifferenzaDiForma riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (Sessione is null || UltimoConfronto is not { Famiglia: Core.PdfAip.FamigliaDelConfronto.AreeEnr5 } confronto || _aipDelConfronto is not { } aip)
            return Rifiuta("Confronta le aree di ENR 5 prima.");
        if (riga is not { Aip: { } chiavi, File: { } file, Sector: { } voce }
            || !(riga.Esito == Core.PdfAip.EsitoDelConfronto.FormaDiversa || (riga.Esito == Core.PdfAip.EsitoDelConfronto.Uguale && riga.Zone > 1)))
            return Rifiuta("Si sostituisce una voce diversa dall'AIP, o una che vale più aree.");
        if (Core.PdfAip.ConfrontoDelleForme.DaGuardare(riga))
            return Rifiuta($"{chiavi}: più di {Core.PdfAip.ConfrontoDelleForme.ScartoDaGuardare:0} NM fra l'AIP e il sector, quasi certo un'altra area col numero sbagliato. Guarda il PDF: si corregge a mano.");
        var aree = chiavi.Split(" + ").Select(k => aip.Airspaces.FirstOrDefault(a => a.Key == k)).OfType<global::Aip.Airspace>().ToList();
        if (aree.Count == 0)
            return Rifiuta($"{chiavi}: non è nell'AIP espanso del confronto.");
        var nomeDelFile = file[(file.LastIndexOf('/') + 1)..];
        var confini = ConfiniDelSector();
        var gesto = aree.Count > 1 ? $"{voce} diviso in {aree.Count} dall'AIP {confronto.Ciclo}" : $"{voce} dall'AIP {confronto.Ciclo}";
        bool fatto = NellaStoria(gesto, () =>
        {
            var righe = RigheDiAdesso(file);
            var sue = Core.PdfAip.AreeEnr5NelSector.RigheDellaVoce(righe, voce, Core.PdfAip.AreeEnr5NelSector.Centro(aree));
            if (sue.Count == 0)
                return Rifiuta($"{voce}: nel file non si ritrovano le sue righe.");
            var nuove = new List<string>();
            foreach (var a in aree)
            {
                if (Core.PdfAip.AreeEnr5NelSector.Blocco(a, nomeDelFile, confini, out var perche) is not { } blocco)
                    return Rifiuta(perche!);
                nuove.AddRange(blocco);
            }
            var sostituzioni = new Dictionary<int, IReadOnlyList<string>> { [sue[0] + 1] = nuove };
            foreach (var i in sue.Skip(1)) sostituzioni[i + 1] = [];
            bool scritto = GestoSulTesto(file, "aip", f => Modifiche.CambiaRighe(f, sostituzioni, gesto));
            Registro.Scrivi("aip", $"{gesto}: {sue.Count} righe → {nuove.Count}");
            return scritto;
        });
        if (fatto)
            RifaiIlConfronto();
        return fatto;
    }

    /// <summary>
    /// «Porta nel sector» per le aree di ENR 5 (slice 4c): un blocco per area in fondo al suo file (P in
    /// <c>italy.prohibit</c>, D in <c>italy.danger</c>, il resto e le militari in <c>italy.restrict</c>). Un'area il cui nome
    /// il file ha già in righe scritte male (R107A-D nel 2610) non si scrive due volte: si dice dove sono. Un gesto solo.
    /// </summary>
    public bool PortaAreeEnr5(IReadOnlyList<string> chiavi)
    {
        ArgumentNullException.ThrowIfNull(chiavi);
        if (Sessione is not { } sessione || UltimoConfronto is not { Famiglia: Core.PdfAip.FamigliaDelConfronto.AreeEnr5 } confronto || _aipDelConfronto is not { } aip)
            return Rifiuta("Confronta le aree di ENR 5 prima.");
        var mancanti = confronto.Esiti.Where(e => e.Esito == Core.PdfAip.EsitoDelConfronto.MancaNelSector).Select(e => e.Aip).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var aree = chiavi.Where(mancanti.Contains).Select(k => aip.Airspaces.FirstOrDefault(a => a.Key == k)).OfType<global::Aip.Airspace>().ToList();
        if (aree.Count == 0)
            return Rifiuta("Si portano le aree che il sector non ha.");
        var confini = ConfiniDelSector();
        var gesto = aree.Count == 1 ? $"{aree[0].Key} dall'AIP {confronto.Ciclo}" : $"{aree.Count} aree di ENR 5 dall'AIP {confronto.Ciclo}";
        var avvisi = new List<string>();
        bool fatto = NellaStoria(gesto, () =>
        {
            avvisi.Clear();
            int portate = 0;
            var toccati = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in aree)
            {
                var nomeDelFile = Core.PdfAip.AreeEnr5NelSector.File(a);
                var file = sessione.File.Keys.FirstOrDefault(k => k.EndsWith("/" + nomeDelFile, StringComparison.OrdinalIgnoreCase));
                if (file is null || sessione.File[file] is not IFileConRecord conRecord)
                {
                    avvisi.Add($"{a.Key}: nel sector non c'è {nomeDelFile}");
                    continue;
                }
                var nome = Core.PdfAip.AreeEnr5NelSector.Nome(a);
                if (Core.PdfAip.AreeEnr5NelSector.ScritteMale(RigheDiAdesso(file), nome) is { Count: > 0 } male)
                {
                    avvisi.Add($"{a.Key}: {nomeDelFile} ha già {nome}, scritta male (righe {string.Join(", ", male.Take(5))}{(male.Count > 5 ? "…" : "")}): si corregge a mano");
                    continue;
                }
                if (Core.PdfAip.AreeEnr5NelSector.Blocco(a, nomeDelFile, confini, out var perche) is not { } blocco)
                {
                    avvisi.Add(perche!);
                    continue;
                }
                // In fondo al file, dopo una riga vuota. Riletto, il file ha un record per lato in più (AggiungiLeRighe ne vuole uno solo).
                var righe = RigheDiAdesso(file);
                if (righe.Count == 0)
                {
                    avvisi.Add($"{a.Key}: {nomeDelFile} è vuoto");
                    continue;
                }
                var quanti = conRecord.RecordDelModello.Count;
                var lati = blocco.Count - 1;
                var esito = Modifiche.CambiaRighe(sessione.File[file],
                    new Dictionary<int, IReadOnlyList<string>> { [righe.Count] = string.IsNullOrWhiteSpace(righe[^1]) ? [righe[^1], .. blocco] : [righe[^1], "", .. blocco] },
                    $"{nome} dall'AIP",
                    riletto => riletto.RecordDelModello.Count == quanti + lati ? null : "Riletto, il file non ha i lati nuovi: si fa a mano, dalle righe del file.");
                if (esito is ModificaDelTesto)
                {
                    portate++;
                    toccati.Add(file);
                }
                else if (esito is ModificaRifiutata rifiutata)
                    avvisi.Add($"{a.Key}: {rifiutata.Motivo}");
            }
            foreach (var f in toccati) RifaiLaGeometria(f);
            Registro.Scrivi("aip", $"{gesto}: {portate} portate" + (avvisi.Count > 0 ? "; " + string.Join("; ", avvisi) : ""));
            Rifiuto = avvisi.Count > 0 ? string.Join(" · ", avvisi) : null;
            RicontrollaLeModifiche();
            Avvisa();
            return portate > 0;
        });
        if (fatto)
            RifaiIlConfronto();
        return fatto;
    }

    /// <summary>Dopo un gesto della slice 1c il confronto si rifà sulle forme di adesso: la riga sistemata diventa «uguale».</summary>
    private void RifaiIlConfronto()
    {
        if (UltimoConfronto is not { } confronto || _aipDelConfronto is not { } aip)
            return;
        UltimoConfronto = (confronto.Ciclo, confronto.Famiglia, Core.PdfAip.ConfrontoDelleForme.Di(confronto.Famiglia, aip.Airspaces, Strati.SelectMany(s => s.Forme).ToList()));
        Avvisa();
    }

    /// <summary>Il repo da cui «Prendi l'AIP espanso» clona: di base quello di prova del committente (D9).</summary>
    public string IndirizzoDellAipEspanso => _indirizzoDellAip ??= Ricordata("indirizzo-aip-espanso.txt") ?? Core.PdfAip.RepoDellAip.IndirizzoDiBase;

    private string? _indirizzoDellAip;

    /// <summary>Cambia il repo dell'AIP espanso: il link di GitHub copiato dal browser va bene. Vuoto = quello di base.</summary>
    public void CambiaLIndirizzoDellAipEspanso(string? indirizzo)
    {
        _indirizzoDellAip = Core.Git.IndirizzoDelRepo.Pulito(indirizzo);
        Ricorda("indirizzo-aip-espanso.txt", _indirizzoDellAip, "repo dell'AIP espanso");
        Avvisa();
    }

    /// <summary>Lo stato git dell'AIP espanso, se la sua cartella è un clone; null altrimenti.</summary>
    public Core.Git.StatoDelRamo? StatoDellAip { get; private set; }

    /// <summary>Il messaggio proposto per registrare l'ultimo ciclo letto (l'AOD lo corregge).</summary>
    public string? MessaggioDellAip { get; set; }

    /// <summary>La pagina della PR dell'ultimo invio dell'AIP espanso.</summary>
    public Uri? PaginaDellaPrDellAip { get; private set; }

    /// <summary>Sta parlando con git per l'AIP espanso.</summary>
    public bool StaLavorandoSulRepoDellAip { get; private set; }

    /// <summary>Rilegge lo stato git dell'AIP espanso (solo se la cartella è la radice di un clone).</summary>
    public async Task RileggiIlRepoDellAipAsync()
    {
        var nuovo = CartellaDellAipEspanso is { } c && Core.PdfAip.RepoDellAip.EUnRepo(c)
            ? (await Core.Git.RepoDelSector.LeggiAsync(Git, c).ConfigureAwait(false)).Stato
            : null;
        // ⚠️ Senza repo non si avvisa: il riquadro rilegge all'apertura della pagina, e un ridisegno per niente in quel
        // momento rinumera i gestori sotto un clic (DueSchermiTests, rosso nella suite intera del 18 ottobre).
        bool cambiato = nuovo is not null || StatoDellAip is not null;
        // Un altro ramo è un'altra copia di lavoro: altri cicli col modello.
        if (nuovo?.Ramo != StatoDellAip?.Ramo)
            _righeDeiCicli = null;
        StatoDellAip = nuovo;
        if (cambiato)
            Avvisa();
    }

    /// <summary>
    /// «Prendi l'AIP espanso»: clona il repo nella cartella (nuova o vuota) e la fa diventare la cartella dell'AIP espanso.
    /// </summary>
    public async Task<bool> PrendiLAipEspansoAsync(string? cartella)
    {
        if (StaLavorandoSulRepoDellAip)
            return false;
        // Con l'ambiente di lavoro l'AIP espanso va sempre in <ambiente>\aip-espanso: il «Dove» non si sceglie.
        if (FissaNellAmbiente(Core.Git.PrimoAvvio.CartellaDellAipEspanso) is { } fissa && string.IsNullOrWhiteSpace(cartella))
            cartella = fissa;
        if (!AccettaNellAmbiente(cartella?.Trim(), Core.Git.PrimoAvvio.CartellaDellAipEspanso, "la cartella dell'AIP espanso"))
            return false;
        if (string.IsNullOrWhiteSpace(cartella))
            return Rifiuta("Scegli dove mettere l'AIP espanso: una cartella nuova (o vuota).");
        if (Core.PdfAip.AipEspanso.PercheNonVa(cartella, CartellaDeiPdf) is { } perche)
            return Rifiuta(perche);

        StaLavorandoSulRepoDellAip = true;
        Avvisa();
        try
        {
            var esito = await Core.PdfAip.RepoDellAip.PrendiAsync(Git, IndirizzoDellAipEspanso, cartella).ConfigureAwait(false);
            Registro.Scrivi("aip", $"prendi l'AIP espanso da {IndirizzoDellAipEspanso} in {cartella}: {esito ?? "fatto"}");
            if (esito is not null)
                return Rifiuta(esito);
            CambiaLAipEspanso(cartella);
            await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            StaLavorandoSulRepoDellAip = false;
            Avvisa();
        }
    }

    /// <summary>«Registra» l'AIP espanso: il modello del ciclo (e il .gitignore) sul ramo del ciclo, col messaggio dell'AOD.</summary>
    public async Task<bool> RegistraLAipAsync(string? messaggio)
    {
        if (StaLavorandoSulRepoDellAip)
            return false;
        await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
        if (StatoDellAip is not { } stato)
            return Rifiuta("La cartella dell'AIP espanso non è il clone del suo repo: prendilo prima («Prendi l'AIP espanso»).");

        StaLavorandoSulRepoDellAip = true;
        Avvisa();
        try
        {
            var perche = await Core.PdfAip.RepoDellAip.RegistraAsync(Git, stato, messaggio).ConfigureAwait(false);
            Registro.Scrivi("aip", $"registra l'AIP espanso su {stato.Ramo}: {perche ?? "fatto"}");
            await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
            if (perche is not null)
                return Rifiuta(perche);
            Rifiuto = null;
            return true;
        }
        finally
        {
            StaLavorandoSulRepoDellAip = false;
            Avvisa();
        }
    }

    /// <summary>
    /// «Manda» l'AIP espanso: il ramo del ciclo al server, poi la pagina della PR verso il ramo predefinito, già compilata,
    /// che l'AOD conferma su GitHub. La fusione la fa il committente.
    /// </summary>
    public async Task<bool> MandaLAipAsync()
    {
        if (StaLavorandoSulRepoDellAip)
            return false;
        await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
        if (StatoDellAip is not { } stato)
            return Rifiuta("La cartella dell'AIP espanso non è il clone del suo repo.");

        StaLavorandoSulRepoDellAip = true;
        Avvisa();
        try
        {
            var perche = await Core.PdfAip.RepoDellAip.MandaAsync(Git, stato).ConfigureAwait(false);
            Registro.Scrivi("aip", $"manda l'AIP espanso {stato.Ramo}: {perche ?? "fatto"}");
            await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
            if (perche is not null)
                return Rifiuta(perche);

            var predefinito = await Core.Git.RamiDelSector.PredefinitoAsync(Git, stato.Radice).ConfigureAwait(false);
            var (titolo, testo) = await Core.Git.Invio.TitoloETestoAsync(Git, stato.Radice, predefinito, stato.Ramo!).ConfigureAwait(false);
            if (Core.Git.Invio.PaginaDellaPr(stato.Remoto, predefinito, stato.Ramo!, titolo, testo) is { } pagina)
            {
                PaginaDellaPrDellAip = pagina;
                Registro.Scrivi("aip", $"PR dell'AIP espanso {stato.Ramo} verso {predefinito}: {pagina}");
                ApriNelBrowser(pagina);
            }
            Rifiuto = null;
            return true;
        }
        finally
        {
            StaLavorandoSulRepoDellAip = false;
            Avvisa();
        }
    }

    // ── Impostazioni dell'app (slice 13f) ────────────────────────────────────────────────────────────────────

    /// <summary>Un'impostazione di testo ricordata in un file dei dati del Lab; vuota o illeggibile = null.</summary>
    private string? Ricordata(string file)
    {
        try
        {
            string percorso = Path.Combine(_cartellaDeiDati, file);
            return File.Exists(percorso) && File.ReadAllText(percorso).Trim() is { Length: > 0 } scritta ? scritta : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Ricorda(string file, string? valore, string nome)
    {
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(Path.Combine(_cartellaDeiDati, file), valore ?? "");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Vale per questa sessione anche se il disco non lo ricorda; il registro lo dice.
            Registro.Errore("impostazioni", e);
        }

        Registro.Scrivi("impostazioni", $"{nome} = «{valore}»");
    }

    private string? _indirizzoDelSector;

    /// <summary>
    /// Il repo da cui «Prendi il sector» scarica (carta F4, slice 2): di base il fork di prova
    /// (<see cref="Core.Git.PresaDelSector.IndirizzoDiBase"/>), finché il committente non dice di passare al repo vero.
    /// </summary>
    public string IndirizzoDelSector => _indirizzoDelSector ??= Ricordata("indirizzo-del-sector.txt") ?? Core.Git.PresaDelSector.IndirizzoDiBase;

    /// <summary>Cambia il repo da cui si prende il sector: il link di GitHub copiato dal browser va bene
    /// (<see cref="Core.Git.IndirizzoDelRepo.Pulito"/>). Vuoto = quello di base.</summary>
    public void CambiaLIndirizzoDelSector(string? indirizzo)
    {
        string scritto = Core.Git.IndirizzoDelRepo.Pulito(indirizzo) ?? Core.Git.PresaDelSector.IndirizzoDiBase;
        if (scritto == IndirizzoDelSector)
            return;
        _indirizzoDelSector = scritto;
        Ricorda("indirizzo-del-sector.txt", scritto, "indirizzo del sector");
        Avvisa();
    }

    private string? _vid;
    private bool _vidLetto;

    /// <summary>
    /// Il VID IVAO dell'AOD, per il nome dei rami di lavoro <c>aod/&lt;VID&gt;/…</c> (carta F4, D1): il Lab non ha un login
    /// IVAO, lo si scrive una volta nelle Impostazioni. Null finché non c'è.
    /// </summary>
    public string? Vid
    {
        get
        {
            if (!_vidLetto)
            {
                _vid = Ricordata("vid.txt");
                _vidLetto = true;
            }

            return _vid;
        }
    }

    /// <summary>Cambia il VID: solo cifre (da 4 a 8). Vuoto = nessuno. Falso, col perché in <see cref="Rifiuto"/>, se non va.</summary>
    public bool CambiaIlVid(string? vid)
    {
        string? scritto = string.IsNullOrWhiteSpace(vid) ? null : vid.Trim();
        if (scritto is not null && (scritto.Length is < 4 or > 8 || !scritto.All(char.IsAsciiDigit)))
        {
            Rifiuto = "Il VID è il numero IVAO: solo cifre (per esempio 704798).";
            Registro.Scrivi("impostazioni", $"VID = «{vid}»: rifiutato");
            Avvisa();
            return false;
        }

        Rifiuto = null;
        _vid = scritto;
        _vidLetto = true;
        Ricorda("vid.txt", scritto, "VID");
        Avvisa();
        return true;
    }

    /// <summary>Sta prendendo il sector dal server.</summary>
    public bool StaPrendendo { get; private set; }

    /// <summary>
    /// «Prendi il sector» (carta F4, slice 2): la copia parziale da <see cref="IndirizzoDelSector"/> nella cartella data,
    /// poi la apre. Falso, col perché in <see cref="Errore"/>, se non è andata.
    /// </summary>
    public async Task<bool> PrendiIlSectorAsync(string? cartella, CancellationToken annulla = default)
    {
        if (StaPrendendo)
            return false;
        if (CartellaDiLavoro is { } lavoro && !string.IsNullOrWhiteSpace(cartella) && Path.IsPathFullyQualified(cartella.Trim())
            && !Core.Git.PrimoAvvio.NellAmbiente(lavoro, cartella))
        {
            Errore = $"Il Lab lavora nel suo ambiente ({lavoro}): il sector si prende lì dentro.";
            Avvisa();
            return false;
        }
        StaPrendendo = true;
        Errore = null;
        Avvisa();
        string? perche;
        try
        {
            perche = await Core.Git.PresaDelSector.PrendiAsync(Git, IndirizzoDelSector, cartella ?? "", annulla).ConfigureAwait(false);
            Registro.Scrivi("git", $"prendi il sector da {IndirizzoDelSector} in {cartella}: {perche ?? "fatto"}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            perche = e.Message;
            Registro.Errore("git", e);
        }
        finally
        {
            StaPrendendo = false;
        }

        if (perche is not null)
        {
            Errore = perche;
            Avvisa();
            return false;
        }

        return await ApriAsync(cartella, annulla).ConfigureAwait(false);
    }

    private string? _cartellaDiLavoro;
    private bool _cartellaDiLavoroLetta;

    /// <summary>
    /// L'ambiente di lavoro (committente, 20 ottobre: «più è isolato l'ambiente di lavoro meno casino facciamo»): la
    /// cartella che «Prepara tutto» ha preparato. Null finché non c'è: allora la schermata d'apertura è il primo avvio.
    /// Con l'ambiente, il Lab apre e prende sector solo lì dentro.
    /// </summary>
    public string? CartellaDiLavoro
    {
        get
        {
            if (!_cartellaDiLavoroLetta)
            {
                _cartellaDiLavoro = Ricordata("cartella-di-lavoro.txt");
                _cartellaDiLavoroLetta = true;
            }

            return _cartellaDiLavoro;
        }
    }

    /// <summary>I sector dell'ambiente di lavoro, <c>sector</c> per primo; vuoto senza ambiente.</summary>
    public IReadOnlyList<string> SectorDellAmbiente() => Core.Git.PrimoAvvio.SectorDellAmbiente(CartellaDiLavoro);

    /// <summary>
    /// Il sector da aprire all'avvio: l'ultimo aperto se sta nell'ambiente ed è ancora un sector, se no il primo
    /// dell'ambiente. Null senza ambiente o senza sector.
    /// </summary>
    public string? SectorDaRiaprire()
    {
        var sector = SectorDellAmbiente();
        string? ultima = UltimaCartella();
        return sector.FirstOrDefault(s => ultima is not null
                   && string.Equals(Path.GetFullPath(s).TrimEnd('\\', '/'), Path.GetFullPath(ultima).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
               ?? sector.FirstOrDefault();
    }

    /// <summary>«Prendi un altro sector» (il ciclo dopo, una prova): dal repo del sector in <c>&lt;ambiente&gt;\&lt;nome&gt;</c>, poi lo apre.</summary>
    public async Task<bool> PrendiUnAltroSectorAsync(string? nome, CancellationToken annulla = default)
    {
        if (CartellaDiLavoro is not { } lavoro)
        {
            Errore = "Prima il primo avvio: «Prepara tutto».";
            Avvisa();
            return false;
        }
        if (Core.Git.PrimoAvvio.PercheNonVaUnAltroSector(lavoro, nome) is { } perche)
        {
            Errore = perche;
            Avvisa();
            return false;
        }
        return await PrendiIlSectorAsync(Path.Combine(lavoro, nome!.Trim()), annulla).ConfigureAwait(false);
    }

    /// <summary>A che punto è «Prepara tutto» (il primo avvio), per la schermata d'apertura; null quando non lavora.</summary>
    public string? PassoDelPrimoAvvio { get; private set; }

    /// <summary>
    /// Il primo avvio (<see cref="Core.Git.PrimoAvvio"/>): nella cartella di lavoro prende il sector da
    /// <see cref="IndirizzoDelSector"/> e l'AIP espanso da <see cref="IndirizzoDellAipEspanso"/>, crea la cartella dei PDF,
    /// le mette nelle impostazioni e apre il sector. Falso, col perché in <see cref="Errore"/>, se non è andata.
    /// </summary>
    public async Task<bool> PreparaIlPrimoAvvioAsync(string? lavoro, CancellationToken annulla = default)
    {
        if (StaPrendendo)
            return false;
        StaPrendendo = true;
        Errore = null;
        Avvisa();
        Core.Git.PrimoAvvio.Cartelle? cartelle;
        string? perche;
        try
        {
            (cartelle, perche) = await Core.Git.PrimoAvvio.PreparaAsync(Git, lavoro ?? "", IndirizzoDelSector, IndirizzoDellAipEspanso,
                passo => { PassoDelPrimoAvvio = passo; Avvisa(); }, annulla).ConfigureAwait(false);
            Registro.Scrivi("git", $"primo avvio in {lavoro} (sector {IndirizzoDelSector}, AIP {IndirizzoDellAipEspanso}): {perche ?? "fatto"}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            (cartelle, perche) = (null, e.Message);
            Registro.Errore("git", e);
        }
        finally
        {
            StaPrendendo = false;
            PassoDelPrimoAvvio = null;
        }

        if (cartelle is null)
        {
            Errore = perche;
            Avvisa();
            return false;
        }

        CambiaLaCartellaDeiPdf(cartelle.Pdf);
        CambiaLAipEspanso(cartelle.AipEspanso);
        _cartellaDiLavoro = Path.GetDirectoryName(cartelle.Sector);
        _cartellaDiLavoroLetta = true;
        Ricorda("cartella-di-lavoro.txt", _cartellaDiLavoro, "cartella di lavoro");
        if (!await ApriAsync(cartelle.Sector, annulla).ConfigureAwait(false))
            return false;
        await RileggiIlRepoDellAipAsync().ConfigureAwait(false);
        return true;
    }

    private string FileDelleConfigurazioni => Path.Combine(_cartellaDeiDati, "nomi-delle-configurazioni.txt");

    private string? _modelloDelleConfigurazioni;

    /// <summary>
    /// Come si scrive il nome di una configurazione nei confini (K1): un modello con <c>{ACC}</c> e <c>{N}</c>, scelto
    /// dall'AOD nelle impostazioni e ricordato fra un avvio e l'altro — non sta nel codice (committente, 5 ottobre 2026).
    /// </summary>
    public string ModelloDelleConfigurazioni => _modelloDelleConfigurazioni ??= ModelloRicordato();

    private string ModelloRicordato()
    {
        try
        {
            return File.Exists(FileDelleConfigurazioni)
                ? NomiDelleConfigurazioni.Pulito(File.ReadAllText(FileDelleConfigurazioni))
                : NomiDelleConfigurazioni.DiBase;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return NomiDelleConfigurazioni.DiBase;
        }
    }

    /// <summary>
    /// Cambia il modello dei nomi delle configurazioni: lo ricorda e rifà i problemi dell'albero. Falso, col perché in
    /// <see cref="Rifiuto"/>, se il modello non ha sigla, parola e numero. Vuoto = quello di base.
    /// </summary>
    public bool CambiaIlModelloDelleConfigurazioni(string? modello)
    {
        string scritto = string.IsNullOrWhiteSpace(modello) ? NomiDelleConfigurazioni.DiBase : modello.Trim();
        if (NomiDelleConfigurazioni.PercheNonVa(scritto) is { } perche)
        {
            Rifiuto = perche;
            Registro.Scrivi("impostazioni", $"nomi delle configurazioni = «{modello}»: rifiutato, {perche}");
            Avvisa();
            return false;
        }

        Rifiuto = null;
        string pulito = NomiDelleConfigurazioni.Pulito(scritto);
        if (pulito == ModelloDelleConfigurazioni)
            return true;
        _modelloDelleConfigurazioni = pulito;
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(FileDelleConfigurazioni, pulito);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Vale per questa sessione anche se il disco non lo ricorda; il registro lo dice.
            Registro.Errore("impostazioni", e);
        }

        Registro.Scrivi("impostazioni", $"nomi delle configurazioni = «{pulito}»");
        _ = ValidaLAlberoAsync();
        Avvisa();
        return true;
    }

    private string FileDellaSogliaDelleEtichette => Path.Combine(_cartellaDeiDati, "etichette-delle-aerovie.txt");

    private double? _sogliaDelleEtichette;

    /// <summary>
    /// Il tratto più corto che riceve un'etichetta, in miglia (slice 14d, B4): di base 10, la soglia che il file segue già.
    /// Un'impostazione dell'app (committente, 6 ottobre 2026), ricordata fra un avvio e l'altro.
    /// </summary>
    public double SogliaDelleEtichette => _sogliaDelleEtichette ??= SogliaRicordata();

    private double SogliaRicordata()
    {
        try
        {
            return File.Exists(FileDellaSogliaDelleEtichette)
                   && double.TryParse(File.ReadAllText(FileDellaSogliaDelleEtichette).Trim(), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out double nm) && nm is >= 0 and <= 500
                ? nm
                : EtichetteDelleAerovie.SogliaDiBaseNm;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return EtichetteDelleAerovie.SogliaDiBaseNm;
        }
    }

    /// <summary>Cambia la soglia delle etichette delle aerovie (NM, da 0 a 500; vuoto = 10). Falso col perché in <see cref="Rifiuto"/>.</summary>
    public bool CambiaLaSogliaDelleEtichette(string? testo)
    {
        double nm = EtichetteDelleAerovie.SogliaDiBaseNm;
        if (!string.IsNullOrWhiteSpace(testo)
            && (!double.TryParse(testo.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out nm)
                || nm is < 0 or > 500))
        {
            Rifiuto = $"«{testo}» non è una lunghezza in miglia: un numero da 0 a 500 (0 = un'etichetta su ogni tratto).";
            Registro.Scrivi("impostazioni", $"soglia delle etichette = «{testo}»: rifiutata");
            Avvisa();
            return false;
        }

        Rifiuto = null;
        if (nm.Equals(SogliaDelleEtichette))
            return true;
        _sogliaDelleEtichette = nm;
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(FileDellaSogliaDelleEtichette, nm.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("impostazioni", e);
        }

        Registro.Scrivi("impostazioni", $"soglia delle etichette delle aerovie = {nm.ToString(System.Globalization.CultureInfo.InvariantCulture)} NM");
        Avvisa();
        return true;
    }

    // ── Le aerovie: etichette calcolate, aggiungere e togliere (slice 14d-14f) ───────────────────────────────

    /// <summary>Vero per un file di aerovie.</summary>
    public static bool EUnFileDiAerovie(string fileRelativo)
        => Path.GetExtension(fileRelativo).ToLowerInvariant() is ".lairway" or ".hairway";

    // La posizione di un punto per nome, nel master scelto.
    private Vipi.Sectorfile.Shared.Coordinate? PuntoPerNome(string nome)
        => IscScelto is not null && Cataloghi.GetValueOrDefault(IscScelto) is { } master && master.TryResolve(nome.Trim(), out var dove) ? dove : null;

    /// <summary>
    /// Che cosa cambierebbe nelle etichette del file per metterle a posto (B4): tratti senza etichetta, nomi sbagliati,
    /// etichette lontane. Null se il file non è di aerovie.
    /// </summary>
    public PianoDelleEtichette? PianoDelleEtichetteDi(string fileRelativo)
        => Sessione is not null && EUnFileDiAerovie(fileRelativo) && Sessione.File.ContainsKey(fileRelativo)
            ? EtichetteDelleAerovie.Piano(RigheDiAdesso(fileRelativo), PuntoPerNome, SogliaDelleEtichette)
            : null;

    /// <summary>Applica il piano delle etichette al file, in una voce sola.</summary>
    public bool SistemaLeEtichette(string fileRelativo)
    {
        if (PianoDelleEtichetteDi(fileRelativo) is not { Vuoto: false } piano)
        {
            Rifiuto = "Le etichette di questo file sono già a posto.";
            Avvisa();
            return false;
        }

        string cosa = $"etichette di {NomeDelFile(fileRelativo)}: {piano.DaAggiungere.Count} nuove, {piano.DaRinominare.Count} rinominate, {piano.DaTogliere.Count} tolte";
        return NellaStoria(cosa, () => GestoSulTesto(fileRelativo, "etichette",
            f => Modifiche.CambiaRighe(f, piano.Sostituzioni(RigheDiAdesso(fileRelativo)), cosa)));
    }

    /// <summary>
    /// Aggiunge un'aerovia a mano (B14): il suo blocco coi tag, in ordine di nome, e le sue etichette. Le quote dei
    /// tratti restano da scrivere nella scheda.
    /// </summary>
    public bool AggiungiUnAerovia(string fileRelativo, string? nome, string? punti)
    {
        if (Sessione is null || !EUnFileDiAerovie(fileRelativo) || !Sessione.File.ContainsKey(fileRelativo))
            return false;
        string n = (nome ?? "").Trim();
        bool fatto = NellaStoria($"aerovia {n} aggiunta a {NomeDelFile(fileRelativo)}", () =>
        {
            var gesto = AerovieAMano.Nuova(RigheDiAdesso(fileRelativo), n, AerovieAMano.Punti(punti), PuntoPerNome);
            if (!GestoSulTesto(fileRelativo, "aerovia", f => gesto.Sostituzioni is null
                    ? new ModificaRifiutata(gesto.Perche!)
                    : Modifiche.CambiaRighe(f, gesto.Sostituzioni, $"aerovia {n} aggiunta", anchiITag: true)))
            {
                return false;
            }

            // Le sue etichette (e il suo nome su quelle dei tratti che condivide), col file com'è adesso.
            var piano = EtichetteDelleAerovie.Piano(RigheDiAdesso(fileRelativo), PuntoPerNome, SogliaDelleEtichette, solo: n);
            if (!piano.Vuoto)
                GestoSulTesto(fileRelativo, "etichette", f => Modifiche.CambiaRighe(f, piano.Sostituzioni(RigheDiAdesso(fileRelativo)), $"etichette di {n}"));
            return true;
        });
        if (fatto && Sessione.File[fileRelativo] is IFileConRecord conRecord
            && conRecord.RecordDelModello.Select((r, i) => (r, i)).FirstOrDefault(v => v.r is Vipi.Sectorfile.Models.Airway { FixLabels.Count: > 0 } a && a.Name == n) is { r: not null } nuovo)
        {
            Scegli(fileRelativo, nuovo.i);
        }

        return fatto;
    }

    /// <summary>Toglie un'aerovia dal file (B15): tracciati, tag, e il suo nome dalle etichette.</summary>
    public bool TogliLAerovia(string fileRelativo, string? nome)
    {
        if (Sessione is null || !EUnFileDiAerovie(fileRelativo) || !Sessione.File.ContainsKey(fileRelativo))
            return false;
        string n = (nome ?? "").Trim();
        bool fatto = NellaStoria($"aerovia {n} tolta da {NomeDelFile(fileRelativo)}", () =>
        {
            var gesto = AerovieAMano.Togli(RigheDiAdesso(fileRelativo), n, PuntoPerNome);
            return GestoSulTesto(fileRelativo, "aerovia", f => gesto.Sostituzioni is null
                ? new ModificaRifiutata(gesto.Perche!)
                : Modifiche.CambiaRighe(f, gesto.Sostituzioni, $"aerovia {n} tolta", anchiITag: true));
        });
        // Il record scelto era suo e non c'è più: al suo numero ora ce n'è un altro (visto a schermo: la scheda
        // mostrava ancora il nome dell'aerovia tolta).
        if (fatto && Scelta is { } scelta && scelta.File == fileRelativo)
            Scegli(null, 0);
        return fatto;
    }

    /// <summary>L'aerovia di un record (un pezzo di tracciato, o un'etichetta che ne nomina una sola), o null.</summary>
    public string? AeroviaDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file && record >= 0 && record < file.RecordDelModello.Count
           && file.RecordDelModello[record] is Vipi.Sectorfile.Models.Airway { Name: var nome }
           && !string.Equals(nome.Trim(), Vipi.Sectorfile.IO.RigheDelleAerovie.Interruzione, StringComparison.OrdinalIgnoreCase)
           && !nome.Contains('-', StringComparison.Ordinal)
            ? nome.Trim()
            : null;

    /// <summary>Lo schema scelto nell'avvio di prima: vale finché questo clone lo ha.</summary>
    private readonly string? _schemaRicordato;

    private string FileDeiColori => Path.Combine(_cartellaDeiDati, "colori-della-mappa.txt");

    /// <summary>Lo schema e il modo ricordati: due righe, il nome dello schema e «aurora» o «lab».</summary>
    private (string? Schema, bool Aurora) ColoriRicordati()
    {
        try
        {
            if (!File.Exists(FileDeiColori))
                return (null, true);
            string[] righe = File.ReadAllLines(FileDeiColori);
            return (righe.Length > 0 && righe[0].Trim().Length > 0 ? righe[0].Trim() : null,
                    righe.Length < 2 || !string.Equals(righe[1].Trim(), "lab", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, true);
        }
    }

    private void RicordaIColori()
    {
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllLines(FileDeiColori, [SchemaScelto ?? string.Empty, ColoriDiAurora ? "aurora" : "lab"]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Come l'ultima cartella: ricordarlo è una comodità.
        }
    }

    /// <summary>
    /// I sector aperti di recente, dal più recente (carta F5, slice 1: si lavora su più sector — il ciclo dopo, una prova —
    /// e si passa dall'uno all'altro). Al più otto, solo quelli che ci sono ancora.
    /// </summary>
    public IReadOnlyList<string> SectorRecenti()
    {
        try
        {
            string file = Path.Combine(_cartellaDeiDati, "sector-recenti.txt");
            return File.Exists(file) ? [.. File.ReadAllLines(file).Where(r => r.Length > 0 && Directory.Exists(r)).Take(8)] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Ricorda(string radice)
    {
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(Path.Combine(_cartellaDeiDati, "ultima-cartella.txt"), radice);
            var recenti = SectorRecenti().Where(r => !string.Equals(r, radice, StringComparison.OrdinalIgnoreCase)).Prepend(radice).Take(8);
            File.WriteAllLines(Path.Combine(_cartellaDeiDati, "sector-recenti.txt"), recenti);
        }
        catch (IOException)
        {
            // Ricordare la cartella è una comodità: se il disco non collabora, l'app si apre lo stesso.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// L'attesa in rotta di un fix, VOR o NDB (slice 10c, L1/U1): <see cref="File"/> e <see cref="Indice"/> dove sta la sua
/// definizione, null se non c'è; <see cref="Proposta"/> l'attesa col nome del punto (<c>HLD-EKLAP</c>) quando quella
/// citata non c'è.
/// </summary>
/// <summary>
/// Il modello di un ATIS o di un D-ATIS per la scheda (slice 18): il testo, com'è fatto, i segnaposto offerti, quelli
/// da riempire nell'anteprima e i modelli accanto.
/// </summary>
public sealed record ModelloNellaScheda(string Testo, bool Datis, ModelloAtisLetto Letto, IReadOnlyList<SegnapostoOfferto> Offerti,
                                        IReadOnlyList<string> DaRiempire, IReadOnlyList<CompagnoDelModello> Compagni);

public sealed record AttesaDellaScheda(string Nome, string? File, int Indice, string? Info, string? Proposta);

/// <summary>Un legame della voce scelta con un'altra dello stesso .str (slice 9e): <see cref="Arriva"/> = si arriva da lei.</summary>
public sealed record LegameDellaScheda(bool Arriva, int Indice, string Nome, string Tipo, string Pista, string Punto);
