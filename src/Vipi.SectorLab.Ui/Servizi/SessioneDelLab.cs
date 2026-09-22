using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
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
/// <para>Le pagine si iscrivono a <see cref="Cambiata"/>; chi la scatena è sempre un gesto dell'AOD, mai un timer.</para>
/// </summary>
public sealed class SessioneDelLab
{
    private readonly string _cartellaDeiDati;

    public SessioneDelLab(string? cartellaDeiDati = null)
        => _cartellaDeiDati = cartellaDeiDati ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VipiSectorLab");

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
            Stato = StatoDelLab.Aperta;
            Errore = null;
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
        Cambiata?.Invoke();
    }

    public void Scegli(string? file, int record)
    {
        Scelta = file is null ? null : (file, record);
        // Scegliere un record apre il suo file nell'elenco: chi clicca una forma sulla mappa si ritrova nel posto
        // giusto dell'albero, senza cercarselo.
        if (file is not null)
            FileScelto = file;
        Cambiata?.Invoke();
    }

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

    /// <summary>Le modifiche fatte e non salvate (slice 6). Sul disco non si scrive niente fino alla slice 9.</summary>
    public ModificheInSospeso Modifiche { get; } = new();

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
        var esito = Modifiche.Cambia(file, record, campo, valore, etichetta);
        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDiCampo)
            RifaiLaGeometria(fileRelativo);

        Cambiata?.Invoke();
        return esito is ModificaDiCampo;
    }

    /// <summary>I vertici di una forma, per l'elenco dell'ispettore (slice 7). Vuoto se quel campo non è vertici.</summary>
    public IReadOnlyList<string> VerticiDi(string fileRelativo, int record, string campo)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return [];

        return ModificheInSospeso.Vertici(file, record, campo) is { } elenco
            ? [.. elenco.Cast<object>().Select(ComeSiLegge)]
            : [];
    }

    /// <summary>Un gesto sui vertici: cambia, aggiungi, togli, incolla. Torna vero se è andato.</summary>
    public bool GestoSuiVertici(string fileRelativo, int record, string campo, GestoDeiVertici gesto,
                                int posizione = 0, string? testo = null)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(fileRelativo, out var file))
            return false;

        string etichetta = EtichetteDi(fileRelativo).ElementAtOrDefault(record) ?? "";
        object esito = gesto switch
        {
            GestoDeiVertici.Cambia => Modifiche.CambiaVertice(file, record, campo, posizione, testo, etichetta),
            GestoDeiVertici.Aggiungi => Modifiche.AggiungiVertice(file, record, campo, posizione, testo, etichetta),
            GestoDeiVertici.Togli => Modifiche.TogliVertice(file, record, campo, posizione, etichetta),
            _ => Modifiche.IncollaVertici(file, record, campo, testo, etichetta),
        };

        Rifiuto = esito is ModificaRifiutata rifiutata ? rifiutata.Motivo : null;
        if (esito is ModificaDeiVertici)
            RifaiLaGeometria(fileRelativo);

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

    public void AnnullaModifica(Modifica modifica)
    {
        if (Sessione is null || !Sessione.File.TryGetValue(modifica.File, out var file))
            return;

        Modifiche.Annulla(file, modifica);
        Rifiuto = null;
        RifaiLaGeometria(modifica.File);
        Cambiata?.Invoke();
    }

    public void AnnullaTutte(string? soloQuesto = null)
    {
        if (Sessione is null)
            return;

        var toccati = Modifiche.FileToccati.ToList();
        Modifiche.AnnullaTutto(f => Sessione.File.GetValueOrDefault(f), soloQuesto);
        Rifiuto = null;
        foreach (string file in toccati)
            RifaiLaGeometria(file);
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
