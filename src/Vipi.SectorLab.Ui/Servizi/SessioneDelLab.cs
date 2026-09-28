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

    public StatoDelLab Stato { get; private set; }

    public SessioneAperta? Sessione { get; private set; }

    /// <summary>Un catalogo dei punti per ogni master (slice 3a): il nome risolto dipende da quale <c>.isc</c> si guarda.</summary>
    public IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi { get; private set; } =
        new Dictionary<string, CatalogoDeiPunti>();

    /// <summary>Il master con cui si risolvono i nomi: quello che carica più punti, finché l'AOD non ne sceglie un altro.</summary>
    public string? IscScelto { get; private set; }

    public IReadOnlyList<StratoDellaMappa> Strati { get; private set; } = [];

    /// <summary>Gli strati accesi: lo sfondo lo è sempre, gli altri li accende l'AOD (carta §2.2 passo 4).</summary>
    public IReadOnlySet<string> Accesi => _accesi;

    private readonly HashSet<string> _accesi = [StratiDellaMappa.Sfondo.Id];

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
            ScordaLaStoria();
            TogliLAnteprima();
            _inVista.Clear();
            VersioneDellaVista++;
            _spenti.Clear();
            _voci.Clear();
            _nascosti.Clear();
            VersioneDeiSpenti++;
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
        return true;
    }

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
        _inVista.Clear();
        VersioneDellaVista++;
        _spenti.Clear();
        _voci.Clear();
        _nascosti.Clear();
        VersioneDeiSpenti++;
        UltimoSalvataggio = null;
        _perse.Clear();
        ScordaIProblemi();
        Avvisa();
    }

    public void Scegli(string? file, int record)
    {
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
                _accesi.Add(tipo.Id);
            // Ogni scelta chiede di inquadrare, anche quella dello stesso record: il secondo clic sull'elenco riporta lì.
            Inquadrature++;
        }

        Avvisa();
    }

    /// <summary>Quante volte si è chiesto di portare la mappa sul record scelto: la mappa lo confronta col suo.</summary>
    public int Inquadrature { get; private set; }

    /// <summary>L'ultimo file di cui si è rifatta la geometria: se è quello scelto, la mappa segue il record spostato.</summary>
    public string? FileRifatto { get; private set; }

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
            if (esito.Salvati.Count + esito.Invariati.Count > 0)
                await DopoLaRiletturaAsync().ConfigureAwait(false);
            // Il sector sul disco è cambiato: le regole dell'albero (nomi non risolti, file mai citati) si rifanno.
            if (esito.Salvati.Count > 0)
                _ = ValidaLAlberoAsync();
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
                    .. ProblemiDelLab.Aggancia(sessione, UsciteDellaForma.Problemi(sessione, strati, forme))];
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
    /// I punti di una procedura coi loro vincoli (slice 9d, Q2): SID col tracciato e voci .str su una pista; vuoto per
    /// le mappe del MAPS e per i file i cui punti non portano tag.
    /// </summary>
    public IReadOnlyList<PuntoConTag> PuntiDellaProceduraDi(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord file && record >= 0 && record < file.RecordDelModello.Count
           && EUnaProcedura(file.RecordDelModello[record])
            ? file.PuntiConTag(record)
            : [];

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
        RicontrollaLeModifiche();
        Avvisa();
        return esito is ModificaDelMetadato;
    }

    /// <summary>«Composta da» per la scheda (F3-bis slice 5): null se il record non è una mappa MAPS di un .str.</summary>
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
    public bool AggiungiRecord(string fileRelativo, int record, string? nome = null, string? tipo = null)
        => NellaStoria(nome is null ? $"record aggiunto in {NomeDelFile(fileRelativo)}" : $"{nome} aggiunto in {NomeDelFile(fileRelativo)}",
            () => GestoDiStruttura(fileRelativo,
                () => Sessione!.File[fileRelativo] is { } file
                    ? Modifiche.AggiungiRecord(file, record, nome, tipo)
                    : new ModificaRifiutata("Questo file non è aperto.")));

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
    public bool AggiungiAlFile(string fileRelativo, string? nome = null, string? tipo = null)
    {
        if (Sessione?.File.GetValueOrDefault(fileRelativo) is not IFileConRecord { RecordDelModello.Count: > 0 } file)
            return false;

        int modello = nome is not null && NomeDelRecordNuovo(fileRelativo) is not null
            ? OrdineAlfabetico.IlVicino(file, nome)
            : file.RecordDelModello.Count - 1;
        return AggiungiRecord(fileRelativo, modello, NomeDelRecordNuovo(fileRelativo) is null ? null : nome, tipo);
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

    private readonly HashSet<string> _spenti = new(StringComparer.Ordinal);

    /// <summary>Le forme spente sulla mappa: <c>file#3</c> un record, <c>file#3.1</c> un poligono.</summary>
    public IReadOnlyCollection<string> Spenti => _spenti;

    public int VersioneDeiSpenti { get; private set; }

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

    private readonly List<string> _inVista = [];

    /// <summary>Quante volte la vista è cambiata: la mappa la riprende quando il numero non è più il suo.</summary>
    public int VersioneDellaVista { get; private set; }

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

    private sealed record GestoNellaStoria(string Cosa, Func<bool> Rifai);

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
        bool fatto = gesto();
        if (!fatto || _rigioco)
            return fatto;

        _storia.RemoveRange(_fatti, _storia.Count - _fatti);
        _storia.Add(new GestoNellaStoria(cosa, gesto));
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

    /// <summary>Gli schemi di Aurora del clone aperto (<c>ColorSchemes\*.clr</c>, fuori dal sector).</summary>
    public IReadOnlyList<string> SchemiDisponibili { get; private set; } = [];

    /// <summary>Lo schema con cui si colora la mappa: scelto una volta, ricordato fra un avvio e l'altro.</summary>
    public string? SchemaScelto { get; private set; }

    /// <summary>Vero: la mappa ha i colori di Aurora (quelli dello schema e di <c>colors.def</c>); falso: quelli del Lab, uno per strato.</summary>
    public bool ColoriDiAurora { get; private set; } = true;

    /// <summary>I colori di Aurora calcolati per la sessione aperta; null senza sessione o senza schema.</summary>
    public ColoriDellaMappa? Colori { get; private set; }

    /// <summary>I nomi di <c>[DEFINE]</c> del master scelto (<c>colors.def</c>): la mappa e il selettore della scheda.</summary>
    public ColorPalette Definiti { get; private set; } = new();

    /// <summary>I simboli dei punti dal <c>.sym</c> del master scelto (slice 4d); null se il master non ne carica.</summary>
    public SimboliDellaMappa? Simboli { get; private set; }

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
        Simboli = null;
        if (Sessione is null)
            return;

        var avvisi = new RaccoltaDiAvvisi();
        var master = IscScelto is null ? null : Cataloghi.GetValueOrDefault(IscScelto);
        try
        {
            Definiti = SchemiDiAurora.Definiti(Sessione, master, avvisi);
            Simboli = SimboliDellaMappa.DelMaster(Sessione, master, avvisi);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("colors.def e simboli", e);
        }

        SchemiDisponibili = SchemiDiAurora.Disponibili(Sessione.Cartella);
        SchemaScelto = SchemiDiAurora.Scegli(SchemiDisponibili, SchemaScelto ?? _schemaRicordato);
        if (SchemaScelto is null)
        {
            ColoriMancanti = $"Nessuno schema di Aurora in «{SchemiDiAurora.Cartella(Sessione.Cartella)}».";
            return;
        }

        try
        {
            var schema = SchemiDiAurora.Leggi(Sessione.Cartella, SchemaScelto, avvisi);
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

    private void Ricorda(string radice)
    {
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(Path.Combine(_cartellaDeiDati, "ultima-cartella.txt"), radice);
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
