using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Ui.Servizi;

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
            Scelta = null;
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
        Errore = null;
        Cambiata?.Invoke();
    }

    public void Scegli(string? file, int record)
    {
        Scelta = file is null ? null : (file, record);
        Cambiata?.Invoke();
    }

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
