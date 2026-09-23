using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Disco;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Problemi;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>I quattro gesti sui vertici di una forma (slice 7).</summary>
public enum GestoDeiVertici
{
    Cambia,
    Aggiungi,
    Togli,
    Incolla,
}

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

    /// <summary>Perché l'apertura non è riuscita: si dice a schermo, non si nasconde.</summary>
    public string? Errore { get; private set; }

    /// <summary>Quanto è costata l'ultima apertura (lettura + cataloghi + geometria): va nella barra di stato.</summary>
    public TimeSpan Durata { get; private set; }

    public event Action? Cambiata;

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
        Cambiata?.Invoke();

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
            string? isc = cataloghi.OrderByDescending(c => c.Value.Punti).Select(c => c.Key).FirstOrDefault();
            var strati = await Task.Run(
                () => StratiDellaMappa.DiSessione(sessione, isc is null ? null : cataloghi[isc]), annulla).ConfigureAwait(false);

            Sessione = sessione;
            Cataloghi = cataloghi;
            IscScelto = isc;
            Strati = strati;
            Albero = AlberoDaSfogliare.Di(sessione);
            FuoriDaiDati = AlberoDaSfogliare.FuoriDaiDati(sessione);
            Scelta = null;
            FileScelto = null;
            _etichette.Clear();
            _densita.Clear();
            // Le modifiche e gli esiti di un'altra cartella non valgono per questa: nomi uguali, file diversi.
            Modifiche = new();
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
            Cambiata?.Invoke();
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
        // Le etichette dipendono dal master (un punto per nome che lì non si risolve si chiama diversamente).
        _etichette.Clear();
        Cambiata?.Invoke();
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
        Cambiata?.Invoke();
    }

    /// <summary>Chiude la cartella: si torna alla schermata d'apertura, e l'albero si lascia andare.</summary>
    public void Chiudi()
    {
        Stato = StatoDelLab.Chiusa;
        Sessione = null;
        Strati = [];
        Cataloghi = new Dictionary<string, CatalogoDeiPunti>();
        IscScelto = null;
        Scelta = null;
        Albero = null;
        FuoriDaiDati = [];
        FileScelto = null;
        _etichette.Clear();
        Errore = null;
        Modifiche = new();
        UltimoSalvataggio = null;
        _perse.Clear();
        ScordaIProblemi();
        Cambiata?.Invoke();
    }

    public void Scegli(string? file, int record)
    {
        Scelta = file is null ? null : (file, record);
        RigaSegnalata = null;
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

        Cambiata?.Invoke();
    }

    /// <summary>Quante volte si è chiesto di portare la mappa sul record scelto: la mappa lo confronta col suo.</summary>
    public int Inquadrature { get; private set; }

    /// <summary>L'ultimo file di cui si è rifatta la geometria: se è quello scelto, la mappa segue il record spostato.</summary>
    public string? FileRifatto { get; private set; }

    /// <summary>Apre (o chiude, con null) un file nell'elenco dei record. Non cambia la scelta.</summary>
    public void ApriFile(string? relativo)
    {
        FileScelto = relativo == FileScelto ? null : relativo;
        Cambiata?.Invoke();
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

    /// <summary>La ricerca per nome fra le forme della mappa (slice 5).</summary>
    public IReadOnlyList<Trovato> Cerca(string? testo) => Ricerca.Cerca(Strati, testo);

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
        Cambiata?.Invoke();
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
            Cambiata?.Invoke();
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
        StaValidando = true;
        ErroreDellaValidazione = null;
        Cambiata?.Invoke();
        return Validazione = Task.Run(async () =>
        {
            IReadOnlyList<ProblemaNelLab>? problemi = null;
            string? errore = null;
            var orologio = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                problemi = ProblemiDelLab.DellAlbero(sessione);
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
            Cambiata?.Invoke();
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
                Cambiata?.Invoke();
        }
    }

    /// <summary>Il clic su un problema: il suo file nell'albero, il suo record nell'ispettore e sulla mappa, la sua riga segnata.</summary>
    public void VaiAlProblema(ProblemaNelLab problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        if (Sessione is null || !Sessione.File.ContainsKey(problema.File))
            return;

        Registro.Scrivi("problema", $"{problema.File}:{problema.Problema.Riga} {problema.Problema.Regola}");
        if (problema.Record is { } record && record < Sessione.File[problema.File].Record)
        {
            Scegli(problema.File, record);
        }
        else
        {
            // Niente record: l'ispettore mostra le righe del disco intorno a quella del problema, non la scelta di prima.
            Scelta = null;
            FileScelto = problema.File;
        }

        RigaSegnalata = problema.Problema.Riga > 0 ? (problema.File, problema.Problema.Riga) : null;
        Cambiata?.Invoke();
    }

    /// <summary>Le righe del disco intorno alla riga segnalata: per i problemi che non stanno in un record.</summary>
    public IReadOnlyList<RigaGrezza> RigheIntornoAllaSegnalata()
    {
        if (Sessione is null || RigaSegnalata is not { } segnata)
            return [];
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
            Cambiata?.Invoke();
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
        Cambiata?.Invoke();
    }

    /// <summary>Toglie dallo schermo l'esito del salvataggio, e i diff delle modifiche perse.</summary>
    public void ChiudiIlSalvataggio()
    {
        UltimoSalvataggio = null;
        _perse.Clear();
        Cambiata?.Invoke();
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
        var (cataloghi, strati) = await Task.Run(() =>
        {
            var c = CatalogoDeiPunti.PerOgniIsc(sessione);
            return (c, StratiDellaMappa.DiSessione(sessione, isc is not null && c.TryGetValue(isc, out var scelto) ? scelto : null));
        }).ConfigureAwait(false);

        Cataloghi = cataloghi;
        Strati = strati;
        Albero = AlberoDaSfogliare.Di(sessione);
        _etichette.Clear();
        if (Scelta is { } scelta && (!sessione.File.TryGetValue(scelta.File, out var file) || scelta.Record >= file.Record))
            Scelta = null;
        VersioneDellaGeometria++;
        StratoDaRidisegnare = null;
    }

    /// <summary>L'ultimo rifiuto, da dire accanto al campo: sparisce alla modifica buona dopo.</summary>
    public string? Rifiuto { get; private set; }

    /// <summary>
    /// Cambia un campo del record scelto. Dopo una modifica la geometria del suo file si rifà: la mappa deve
    /// mostrare il punto DOV'È ADESSO, non dov'era all'apertura.
    /// </summary>
    public bool CambiaCampo(string fileRelativo, int record, string campo, string? valore)
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
        Cambiata?.Invoke();
        return esito is ModificaDiCampo;
    }

    /// <summary>«Composta da» per la scheda (F3-bis slice 5): null se il record non è una mappa MAPS di un .str.</summary>
    public SchedaDellaComposta? CompostaDi(string fileRelativo, int record)
        => Sessione is not null && Sessione.File.TryGetValue(fileRelativo, out var file) ? SchedaDellaComposta.Di(file, record) : null;

    /// <summary>
    /// Spunta o toglie una procedura dall'elenco di una mappa composta, o cambia se si disegnano intere: il tag si
    /// riscrive e la mappa si rigenera, tutto nelle modifiche in sospeso.
    /// </summary>
    public bool CambiaLaComposta(string fileRelativo, int record, IReadOnlyList<ProceduraDellaComposta> elenco, bool? intere = null)
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
        Cambiata?.Invoke();
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

    /// <summary>Un gesto sui vertici: cambia, aggiungi, togli, incolla. Torna vero se è andato.</summary>
    /// <summary>
    /// Quanto sono fitti gli archi di quel record — o, se lui non ne ha, di tutto il suo file; senza archi da misurare,
    /// un punto per grado (quello di F1). La usa «incolla da testo», e la scheda la mostra perché l'AOD la cambi.
    /// </summary>
    public double DensitaDegliArchiDi(string fileRelativo, int record)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return Vipi.Application.Coordinates.ArcGeometry.DensitaBase;
        if (_densita.TryGetValue((fileRelativo, record), out double gia))
            return gia;

        var delRecord = ElenchiDiVertici.Di(file, record).Select(e => e.Posizioni());
        double stima = DensitaDegliArchi.Stima(delRecord)
                       ?? DensitaDegliArchi.Stima(Enumerable.Range(0, file.Record)
                              .SelectMany(i => ElenchiDiVertici.Di(file, i)).Select(e => e.Posizioni()))
                       ?? Vipi.Application.Coordinates.ArcGeometry.DensitaBase;
        _densita[(fileRelativo, record)] = stima;
        return stima;
    }

    private readonly Dictionary<(string File, int Record), double> _densita = [];

    /// <param name="puntiPerGrado">Solo per «incolla»: quanto fitti gli archi; di base la stima del record o del file
    /// (<see cref="DensitaDegliArchiDi"/>).</param>
    public bool GestoSuiVertici(string fileRelativo, int record, string campo, GestoDeiVertici gesto,
                                int posizione = 0, string? testo = null, double? puntiPerGrado = null)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        string etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "";
        object esito = gesto switch
        {
            GestoDeiVertici.Cambia => Modifiche.CambiaVertice(file, record, campo, posizione, testo, etichetta),
            GestoDeiVertici.Aggiungi => Modifiche.AggiungiVertice(file, record, campo, posizione, testo, etichetta),
            GestoDeiVertici.Togli => Modifiche.TogliVertice(file, record, campo, posizione, etichetta),
            _ => Modifiche.IncollaVertici(file, record, campo, testo, etichetta,
                                          puntiPerGrado ?? DensitaDegliArchiDi(fileRelativo, record)),
        };
        Registro.Scrivi("vertici", $"{fileRelativo}#{record} {campo} {gesto} {posizione}"
                                   + (gesto == GestoDeiVertici.Incolla ? $" ({testo?.Length ?? 0} caratteri)" : $" «{testo}»")
                                   + $": {Descrivi(esito)}");

        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDeiVertici)
            RifaiLaGeometria(fileRelativo);

        RicontrollaLeModifiche();
        Cambiata?.Invoke();
        return esito is ModificaDeiVertici;
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
    public bool AggiungiRecord(string fileRelativo, int record)
        => GestoDiStruttura(fileRelativo, () => Sessione!.File[fileRelativo] is { } file
            ? Modifiche.AggiungiRecord(file, record)
            : new ModificaRifiutata("Questo file non è aperto."));

    public bool TogliRecord(string fileRelativo, int record)
        => GestoDiStruttura(fileRelativo, () => Sessione!.File[fileRelativo] is { } file
            ? Modifiche.TogliRecord(file, record)
            : new ModificaRifiutata("Questo file non è aperto."));

    private bool GestoDiStruttura(string fileRelativo, Func<object> fai)
    {
        if (Sessione is null || !Sessione.File.ContainsKey(fileRelativo))
            return false;

        object esito = fai();
        Registro.Scrivi("struttura", $"{fileRelativo}: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is not ModificaDiStruttura)
        {
            Cambiata?.Invoke();
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
        Cambiata?.Invoke();
        return true;
    }

    public void AnnullaModifica(Modifica modifica)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(modifica.File, out var file))
            return;

        // Con le copie gemelle i file toccati sono più d'uno: si rifà la geometria di tutti quelli che c'erano prima.
        var toccati = Modifiche.FileToccati.ToList();
        Modifiche.Annulla(file, modifica, f => Sessione.File.GetValueOrDefault(f));
        Registro.Scrivi("annulla", $"{modifica.File}#{modifica.Record} {modifica.Descrizione}");
        Rifiuto = null;
        foreach (string toccato in toccati.Except(Modifiche.FileToccati).Append(modifica.File).Distinct())
            RifaiLaGeometria(toccato);
        RicontrollaLeModifiche();
        Cambiata?.Invoke();
    }

    /// <summary>«Allinea anche questo» (F3-bis D2): il valore nuovo anche sulla copia gemella che era stata lasciata.</summary>
    public void AllineaLaCopia(Modifica principale, CopiaNonToccata copia)
    {
        if (Sessione is null)
            return;

        var esito = Modifiche.AllineaLaCopia(principale, copia, f => Sessione.File.GetValueOrDefault(f));
        Registro.Scrivi("allinea", $"{copia.File}#{copia.Record} {principale.Campo}: {Descrivi(esito)}");
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        RifaiLaGeometria(copia.File);
        RicontrollaLeModifiche();
        Cambiata?.Invoke();
    }

    public void AnnullaTutte(string? soloQuesto = null)
    {
        if (Sessione is null)
            return;

        var toccati = Modifiche.FileToccati.ToList();
        Registro.Scrivi("annulla", $"tutto ({Modifiche.Quante} modifiche" + (soloQuesto is null ? ")" : $" di {soloQuesto})"));
        Modifiche.AnnullaTutto(f => Sessione.File.GetValueOrDefault(f), soloQuesto);
        Rifiuto = null;
        foreach (string file in toccati)
            RifaiLaGeometria(file);
        RicontrollaLeModifiche();
        Cambiata?.Invoke();
    }

    /// <summary>Il diff di un file toccato: righe tolte e aggiunte, prodotte dallo scrittore vero.</summary>
    public Diff.Esito DiffDi(string fileRelativo)
        => Sessione is not null && Sessione.File.TryGetValue(fileRelativo, out var file)
            ? Modifiche.DiffDi(file)
            : new Diff.Esito([], InBlocco: false);

    /// <summary>
    /// Rifà le forme del solo file toccato: rifare tutto l'albero costerebbe 48 ms a ogni tasto, e non serve —
    /// una modifica sta in un file solo.
    /// </summary>
    private void RifaiLaGeometria(string fileRelativo)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return;

        var tipo = StratiDellaMappa.DiFile(fileRelativo);
        _etichette.Remove(fileRelativo);
        foreach (var chiave in _densita.Keys.Where(k => k.File == fileRelativo).ToList())
            _densita.Remove(chiave);
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

        // La mappa ha le coordinate in memoria: finché questo numero non cambia, non ha motivo di richiederle.
        VersioneDellaGeometria++;
        StratoDaRidisegnare = tipo.Id;
        FileRifatto = fileRelativo;
    }

    /// <summary>Quante volte la geometria è cambiata: la mappa se ne accorge e ridisegna lo strato toccato.</summary>
    public int VersioneDellaGeometria { get; private set; }

    /// <summary>Quale strato ha bisogno di essere ripreso dalla mappa.</summary>
    public string? StratoDaRidisegnare { get; private set; }

    private CatalogoDeiPunti? CatalogoScelto
        => IscScelto is not null && Cataloghi.TryGetValue(IscScelto, out var catalogo) ? catalogo : null;

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
        Cambiata?.Invoke();
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
