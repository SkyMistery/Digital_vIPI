using System.Globalization;
using System.Reflection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Una modifica fatta e non ancora salvata: si vede nel pannello, e si annulla.</summary>
public abstract record Modifica(string File, int Record, string Etichetta, string Campo)
{
    /// <summary>Come si legge nel pannello, in una riga.</summary>
    public abstract string Descrizione { get; }
}

/// <param name="Prima">Il valore com'era all'apertura, scritto come si legge: serve per annullare e per dirlo.</param>
public sealed record ModificaDiCampo(string File, int Record, string Etichetta, string Campo, string Prima, string Dopo)
    : Modifica(File, Record, Etichetta, Campo)
{
    public override string Descrizione => $"{Campo}: {Prima} → {Dopo}";
}

/// <summary>
/// I record aggiunti e tolti in un file (slice 8): cambia <b>quanti</b> record ci sono, non il contenuto di uno.
/// Sono una voce sola per file, come i vertici sono una voce sola per forma: annullarla rimette i record com'erano.
/// </summary>
public sealed record ModificaDiStruttura(string File, int Aggiunti, int Tolti)
    : Modifica(File, Record: -1, Etichetta: "", Campo: Chiave)
{
    internal const string Chiave = "§struttura";

    public override string Descrizione => (Aggiunti, Tolti) switch
    {
        (> 0, 0) => $"{Aggiunti} record aggiunti",
        (0, > 0) => $"{Tolti} record tolti",
        _ => $"{Aggiunti} record aggiunti, {Tolti} tolti",
    };
}

/// <summary>
/// I vertici di una forma, cambiati tutti insieme (slice 7): uno spostato, uno aggiunto, uno tolto, o l'elenco
/// intero incollato da un testo. Si tiene l'elenco <b>com'era all'apertura</b>, e annullare lo rimette.
/// </summary>
public sealed record ModificaDeiVertici(
    string File, int Record, string Etichetta, string Campo, int Prima, int Dopo, string Cosa)
    : Modifica(File, Record, Etichetta, Campo)
{
    public override string Descrizione => Prima == Dopo
        ? $"{Campo}: {Cosa} ({Dopo} vertici)"
        : $"{Campo}: {Cosa} ({Prima} → {Dopo} vertici)";
}

/// <summary>Perché una modifica non si è potuta fare. Il campo resta com'era.</summary>
public sealed record ModificaRifiutata(string Motivo);

/// <summary>
/// Le modifiche in sospeso (carta F3 §2.2 passo 6 e §2.3, slice 6): che cosa l'AOD ha cambiato, e che cosa
/// uscirebbe sul disco. In F3 slice 6 si cambiano i <b>campi</b> di un record; i vertici sono la slice 7, il
/// salvataggio la 9 — qui non si scrive niente.
/// <para>Il record del motore si modifica <b>in posto</b> (i modelli hanno i setter), e il file da cui viene sa
/// riscriversi col suo scrittore: la <b>base</b> fissata all'apertura fa sì che di un record toccato si riscriva
/// solo il campo cambiato (F2 §9.5). Perciò il diff che si mostra è quello vero, non una simulazione.</para>
/// <para>Annullare non è «rifare il contrario»: si rimette il valore di prima, che si tiene da quando la modifica
/// è stata fatta. Un record senza più modifiche torna pulito, e il file senza più record toccati sparisce dal
/// pannello — il suo diff torna vuoto da sé, perché lo scrittore riscrive i byte grezzi.</para>
/// </summary>
public sealed class ModificheInSospeso
{
    private readonly Dictionary<(string File, int Record, string Campo), Modifica> _fatte = [];

    /// <summary>Gli elenchi di vertici com'erano all'apertura: annullare li rimette, senza rifare i gesti al contrario.</summary>
    private readonly Dictionary<(string File, int Record, string Campo), List<object>> _verticiDiPartenza = [];

    /// <summary>I record toccati, per file e per indice: dall'indice si arriva all'oggetto, che è ciò che vuole lo scrittore.</summary>
    private readonly Dictionary<string, Dictionary<int, object>> _sporchi = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Modifica> Tutte => _fatte.Values;

    public int Quante => _fatte.Count;

    /// <summary>
    /// I file con qualcosa in sospeso. Si legge dalle MODIFICHE, non dai record toccati: un file dove si è solo
    /// aggiunto o tolto un record non ha nessun record sporco, e sparirebbe dal pannello pur essendo cambiato.
    /// </summary>
    public IReadOnlyList<string> FileToccati => [.. _fatte.Keys.Select(k => k.File).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public bool CEQualcosa => _fatte.Count > 0;

    /// <summary>I record toccati di un file: li passa lo scrittore, e il salvataggio della slice 9.</summary>
    public IReadOnlyCollection<object> SporchiDi(string file)
        => _sporchi.TryGetValue(file, out var suoi) ? suoi.Values : [];

    /// <summary>
    /// Cambia un campo di un record. Torna la modifica fatta, o il motivo del rifiuto — e in quel caso il record
    /// non è stato toccato affatto.
    /// </summary>
    public object Cambia(FileAperto file, int indice, string campo, string? valore, string etichetta = "")
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return new ModificaRifiutata("Questo record non c'è.");

        object record = conRecord.RecordDelModello[indice];
        var proprieta = record.GetType().GetProperty(campo, BindingFlags.Public | BindingFlags.Instance);
        if (proprieta is null || !proprieta.CanWrite || proprieta.SetMethod is null || !proprieta.SetMethod.IsPublic)
            return new ModificaRifiutata($"Il campo «{campo}» non si scrive.");

        object? prima = proprieta.GetValue(record);
        if (!Converti(proprieta.PropertyType, valore, out object? dopo, out string? perche))
            return new ModificaRifiutata(perche!);

        if (Equals(prima, dopo))
            return new ModificaRifiutata("Il valore è già questo.");

        proprieta.SetValue(record, dopo);

        var chiave = (file.Relativo, indice, campo);
        // Se il campo era già stato cambiato, il «prima» resta quello DELL'APERTURA: sennò annullare due modifiche
        // di fila riporterebbe a un valore intermedio che sul disco non è mai esistito.
        string primaScritto = _fatte.TryGetValue(chiave, out var gia) && gia is ModificaDiCampo giaFatta
            ? giaFatta.Prima
            : Scrivi(prima);
        var modifica = new ModificaDiCampo(file.Relativo, indice, etichetta, campo, primaScritto, Scrivi(dopo));

        if (primaScritto == modifica.Dopo)
        {
            // Rimesso a mano il valore di partenza: non è una modifica, è un ritorno. (È anche come si annulla.)
            _fatte.Remove(chiave);
            Ripulisci(file.Relativo);
            return modifica;
        }

        _fatte[chiave] = modifica;
        if (!_sporchi.TryGetValue(file.Relativo, out var suoi))
            _sporchi[file.Relativo] = suoi = [];
        suoi[indice] = record;
        return modifica;
    }

    /// <summary>Annulla una modifica: rimette com'era all'apertura il campo, o l'elenco dei vertici.</summary>
    public bool Annulla(FileAperto file, Modifica modifica)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(modifica);
        var chiave = (modifica.File, modifica.Record, modifica.Campo);
        if (!_fatte.ContainsKey(chiave))
            return false;

        if (modifica is ModificaDiCampo campo)
            return Cambia(file, modifica.Record, modifica.Campo, campo.Prima, modifica.Etichetta) is ModificaDiCampo;

        // 🔴 La struttura si annulla per ULTIMA: rimettere i record com'erano rinumera tutto, e le modifiche che
        // pendono sugli altri record di questo file non avrebbero più un indice buono. Si annullano prima loro —
        // così i valori tornano quelli dell'apertura — e poi si rimette la struttura.
        if (modifica is ModificaDiStruttura)
        {
            foreach (var altra in _fatte.Values.Where(m => m.File == modifica.File && m is not ModificaDiStruttura).ToList())
                Annulla(file, altra);

            if (_strutturaDiPartenza.Remove(modifica.File, out object? comEra))
                ((IFileConRecord)file).RipristinaLaStruttura(comEra);

            _fatte.Remove(chiave);
            _sporchi.Remove(modifica.File);
            UltimoAggiunto = null;
            return true;
        }

        // I vertici non si annullano rifacendo i gesti al contrario: si rimette l'elenco com'era all'apertura.
        if (!_verticiDiPartenza.TryGetValue(chiave, out var comErano) || Vertici(file, modifica.Record, modifica.Campo) is not { } elenco)
            return false;

        elenco.Clear();
        foreach (object punto in comErano)
            elenco.Add(punto);

        _fatte.Remove(chiave);
        _verticiDiPartenza.Remove(chiave);
        Ripulisci(modifica.File);
        return true;
    }

    /// <summary>Annulla tutto quel che è stato fatto su un file (o su tutti, senza file).</summary>
    public void AnnullaTutto(Func<string, FileAperto?> cercaIlFile, string? soloQuesto = null)
    {
        ArgumentNullException.ThrowIfNull(cercaIlFile);
        foreach (var modifica in _fatte.Values.Where(m => soloQuesto is null || m.File == soloQuesto).ToList())
        {
            if (cercaIlFile(modifica.File) is { } file)
                Annulla(file, modifica);
        }
    }

    /// <summary>
    /// Lascia andare tutto quel che pende su un file, SENZA rimettere niente (slice 9): il file è stato salvato, o
    /// riletto dal disco dopo un conflitto, e i suoi record sono oggetti nuovi — le modifiche parlavano di quelli
    /// vecchi. Annullare invece rimette i valori dell'apertura: qui non c'è più niente da rimettere.
    /// </summary>
    public void Dimentica(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        foreach (var chiave in _fatte.Keys.Where(k => k.File == file).ToList())
            _fatte.Remove(chiave);
        foreach (var chiave in _verticiDiPartenza.Keys.Where(k => k.File == file).ToList())
            _verticiDiPartenza.Remove(chiave);
        _sporchi.Remove(file);
        _strutturaDiPartenza.Remove(file);
        UltimoAggiunto = null;
    }

    // --- aggiungere e togliere un record (slice 8) -----------------------------------------------------------

    /// <summary>La struttura di ogni file com'era prima del primo record aggiunto o tolto: annullare la rimette.</summary>
    private readonly Dictionary<string, object> _strutturaDiPartenza = new(StringComparer.Ordinal);

    /// <summary>
    /// Aggiunge un record <b>copiando il vicino</b> (carta §2.3): nasce già nella forma del file e coi campi di
    /// struttura dei suoi vicini, e l'AOD cambia quel che deve. Torna la modifica, con l'indice del nuovo in coda.
    /// </summary>
    public object AggiungiRecord(FileAperto file, int indice)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord)
            return new ModificaRifiutata("Questo file il motore non lo interpreta: non ci sono record da aggiungere.");
        if (indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return new ModificaRifiutata("Questo record non c'è.");

        Fotografa(file, conRecord);
        int nuovo = conRecord.AggiungiComeIlVicino(indice);
        SpostaGliIndici(file.Relativo, daIncluso: nuovo, scarto: +1);
        UltimoAggiunto = nuovo;
        return Registra(file, aggiunti: 1, tolti: 0);
    }

    /// <summary>Toglie un record, con le sue righe e i suoi commenti, e le modifiche che aveva in sospeso.</summary>
    public object TogliRecord(FileAperto file, int indice)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord)
            return new ModificaRifiutata("Questo file il motore non lo interpreta.");
        if (indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return new ModificaRifiutata("Questo record non c'è.");
        // L'ultimo record di un file no: un file senza record non è più quel file. Si cancella il file, non il record.
        if (conRecord.RecordDelModello.Count == 1)
            return new ModificaRifiutata("È l'ultimo record del file: toglierlo lascerebbe un file senza niente.");

        Fotografa(file, conRecord);
        ScordaQuelChePendeSu(file.Relativo, indice);
        conRecord.TogliIlRecord(indice);
        SpostaGliIndici(file.Relativo, daIncluso: indice, scarto: -1);
        UltimoAggiunto = null;
        return Registra(file, aggiunti: 0, tolti: 1);
    }

    /// <summary>L'indice dell'ultimo record aggiunto in quel file, per andarci subito. Null se non ce n'è.</summary>
    public int? UltimoAggiunto { get; private set; }

    private void Fotografa(FileAperto file, IFileConRecord conRecord)
    {
        if (!_strutturaDiPartenza.ContainsKey(file.Relativo))
            _strutturaDiPartenza[file.Relativo] = conRecord.IstantaneaDellaStruttura();
    }

    private object Registra(FileAperto file, int aggiunti, int tolti)
    {
        var chiave = (file.Relativo, -1, ModificaDiStruttura.Chiave);
        var prima = _fatte.GetValueOrDefault(chiave) as ModificaDiStruttura;
        var modifica = new ModificaDiStruttura(
            file.Relativo, (prima?.Aggiunti ?? 0) + aggiunti, (prima?.Tolti ?? 0) + tolti);

        _fatte[chiave] = modifica;
        // Il file entra nel pannello anche senza record toccati: il diff lo fa la struttura, non un record sporco.
        if (!_sporchi.ContainsKey(file.Relativo))
            _sporchi[file.Relativo] = [];
        return modifica;
    }

    /// <summary>
    /// Un record aggiunto o tolto fa scorrere i numeri degli altri: le modifiche in sospeso che ne avevano uno
    /// vanno rinumerate, o punterebbero al record sbagliato. 🔴 Vale anche per i record toccati e per le fotografie
    /// dei vertici.
    /// </summary>
    private void SpostaGliIndici(string file, int daIncluso, int scarto)
    {
        Rinumera(_fatte, file, daIncluso, scarto, (chiave, modifica) => modifica switch
        {
            ModificaDiCampo m => m with { Record = chiave.Record },
            ModificaDeiVertici m => m with { Record = chiave.Record },
            _ => modifica,
        });
        Rinumera(_verticiDiPartenza, file, daIncluso, scarto, (_, elenco) => elenco);

        if (!_sporchi.TryGetValue(file, out var suoi))
            return;
        var rifatti = suoi
            .Select(s => (Indice: s.Key >= daIncluso ? s.Key + scarto : s.Key, s.Value))
            .Where(s => s.Indice >= 0)
            .ToDictionary(s => s.Indice, s => s.Value);
        _sporchi[file] = rifatti;
    }

    private static void Rinumera<T>(Dictionary<(string File, int Record, string Campo), T> dove, string file,
                                    int daIncluso, int scarto, Func<(string File, int Record, string Campo), T, T> rifai)
    {
        var daSpostare = dove.Keys.Where(k => k.File == file && k.Record >= daIncluso).OrderBy(k => k.Record * -scarto).ToList();
        foreach (var chiave in daSpostare)
        {
            var nuova = chiave with { Record = chiave.Record + scarto };
            dove[nuova] = rifai(nuova, dove[chiave]);
            dove.Remove(chiave);
        }
    }

    /// <summary>Le modifiche in sospeso di un record che sta per sparire: spariscono con lui.</summary>
    private void ScordaQuelChePendeSu(string file, int indice)
    {
        foreach (var chiave in _fatte.Keys.Where(k => k.File == file && k.Record == indice).ToList())
        {
            _fatte.Remove(chiave);
            _verticiDiPartenza.Remove(chiave);
        }

        if (_sporchi.TryGetValue(file, out var suoi))
            suoi.Remove(indice);
    }

    // --- i vertici di una forma (slice 7) --------------------------------------------------------------------

    /// <summary>
    /// L'elenco dei vertici con quella chiave, o null. La chiave è il nome del campo (<c>Vertices</c>,
    /// <c>Track</c>) o, per una zona a più tratti, <c>Segments[2].Points</c> (slice 7-bis).
    /// </summary>
    public static System.Collections.IList? Vertici(FileAperto file, int indice, string campo)
        => ElenchiDiVertici.Uno(file, indice, campo)?.Elenco;

    /// <summary>Cambia un vertice: il testo si legge come una coordinata, o come il NOME di un punto del catalogo.</summary>
    public object CambiaVertice(FileAperto file, int indice, string campo, int posizione, string? testo, string etichetta = "")
        => Gesto(file, indice, campo, etichetta, "vertice spostato", vertici =>
        {
            if (posizione < 0 || posizione >= vertici.Quanti)
                return new ModificaRifiutata("Quel vertice non c'è.");
            if (!LeggiIlPunto(vertici, testo, out Punto punto, out string? perche))
                return new ModificaRifiutata(perche!);

            // L'involucro che c'era si tiene: l'etichetta di una SID non si perde spostando il suo punto.
            vertici.Elenco[posizione] = vertici.Fabbrica(punto, vertici.Elenco[posizione]);
            return null;
        });

    /// <summary>Aggiunge un vertice PRIMA della posizione data (o in fondo, se è quanti ce ne sono).</summary>
    public object AggiungiVertice(FileAperto file, int indice, string campo, int posizione, string? testo, string etichetta = "")
        => Gesto(file, indice, campo, etichetta, "vertice aggiunto", vertici =>
        {
            if (posizione < 0 || posizione > vertici.Quanti)
                return new ModificaRifiutata("Lì non si può aggiungere un vertice.");
            if (!LeggiIlPunto(vertici, testo, out Punto punto, out string? perche))
                return new ModificaRifiutata(perche!);

            vertici.Elenco.Insert(posizione, vertici.Fabbrica(punto, vecchio: null));
            return null;
        });

    public object TogliVertice(FileAperto file, int indice, string campo, int posizione, string etichetta = "")
        => Gesto(file, indice, campo, etichetta, "vertice tolto", vertici =>
        {
            if (posizione < 0 || posizione >= vertici.Quanti)
                return new ModificaRifiutata("Quel vertice non c'è.");
            // Una forma senza punti non è una forma: chi vuole togliere il record lo toglie (slice 8).
            if (vertici.Quanti == 1)
                return new ModificaRifiutata("È l'ultimo vertice: una forma senza punti non si disegna.");

            vertici.Elenco.RemoveAt(posizione);
            return null;
        });

    /// <summary>
    /// «Incolla da testo» (carta §2.3): il testo dell'AIP con gli archi, o l'uscita del convertitore del sito, o
    /// delle coordinate una per riga — lo legge il <b>convertitore di F1</b>, lo stesso della vIPI, e diventa
    /// l'elenco dei vertici. Il testo con più di un'area si rifiuta: quale sarebbe questa forma?
    /// </summary>
    public object IncollaVertici(FileAperto file, int indice, string campo, string? testo, string etichetta = "",
                                 double puntiPerGrado = 1.0)
        => Gesto(file, indice, campo, etichetta, "vertici incollati", vertici =>
        {
            var letto = Vipi.Application.Coordinates.CoordinateParser.Parse(testo, puntiPerGrado);
            var aree = letto.Aree.Where(a => a.Punti.Count > 0).ToList();
            if (aree.Count == 0)
                return new ModificaRifiutata("In quel testo non c'è nessuna coordinata che si possa leggere.");
            if (aree.Count > 1)
                return new ModificaRifiutata($"Quel testo contiene {aree.Count} aree: incollane una sola.");

            var punti = aree[0].Punti;
            vertici.Elenco.Clear();
            foreach (var (lat, lon) in punti)
                vertici.Elenco.Add(vertici.Fabbrica(Punto.Da(new Coordinate(lat, lon)), vecchio: null));

            return null;
        });

    /// <summary>Il giro comune dei gesti sui vertici: trova l'elenco, fotografa com'era, fa il gesto, registra.</summary>
    private object Gesto(FileAperto file, int indice, string campo, string etichetta, string cosa,
                         Func<ElencoDiVertici, ModificaRifiutata?> fai)
    {
        if (ElenchiDiVertici.Uno(file, indice, campo) is not { } vertici)
            return new ModificaRifiutata($"Il campo «{campo}» non è un elenco di vertici.");
        var elenco = vertici.Elenco;

        var chiave = (file.Relativo, indice, campo);
        // La fotografia si prende UNA volta sola: è l'elenco dell'apertura, non quello di prima di questo gesto.
        if (!_verticiDiPartenza.ContainsKey(chiave))
            _verticiDiPartenza[chiave] = [.. elenco.Cast<object>()];

        int prima = _verticiDiPartenza[chiave].Count;
        if (fai(vertici) is { } rifiutata)
        {
            if (!_fatte.ContainsKey(chiave))
                _verticiDiPartenza.Remove(chiave);
            return rifiutata;
        }

        if (elenco.Cast<object>().SequenceEqual(_verticiDiPartenza[chiave]))
        {
            // Tornato com'era all'apertura: non è una modifica.
            _fatte.Remove(chiave);
            _verticiDiPartenza.Remove(chiave);
            Ripulisci(file.Relativo);
            return new ModificaDeiVertici(file.Relativo, indice, etichetta, campo, prima, elenco.Count, cosa);
        }

        var modifica = new ModificaDeiVertici(file.Relativo, indice, etichetta, campo, prima, elenco.Count, cosa);
        _fatte[chiave] = modifica;
        if (!_sporchi.TryGetValue(file.Relativo, out var suoi))
            _sporchi[file.Relativo] = suoi = [];
        suoi[indice] = ((IFileConRecord)file).RecordDelModello[indice];
        return modifica;
    }

    /// <summary>Legge un vertice scritto: una coppia di coordinate, o il nome di un punto (dove il file lo ammette).</summary>
    private static bool LeggiIlPunto(ElencoDiVertici vertici, string? testo, out Punto punto, out string? perche)
    {
        punto = default;
        perche = null;
        string scritto = (testo ?? "").Trim();
        if (scritto.Length == 0)
        {
            perche = "Un vertice non può essere vuoto.";
            return false;
        }

        string[] pezzi = scritto.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool perNome = pezzi.Length is 1 or 2 && pezzi.All(p => !char.IsAsciiDigit(p[0]) && !"NSEW+-".Contains(p[0]));

        if (perNome)
        {
            // Un punto per NOME lo ammettono solo i file che lo sanno scrivere (i .tfl, le SID…): dove l'elenco è di
            // Coordinate, il nome non si potrebbe riscrivere e va rifiutato subito.
            if (!vertici.AmmetteNomi)
            {
                perche = "Qui un vertice si scrive per coordinate: questo file non sa scrivere i nomi.";
                return false;
            }

            punto = Punto.Nominato(pezzi[0], pezzi.Length == 2 ? pezzi[1] : null);
            return true;
        }

        if (pezzi.Length != 2)
        {
            perche = "Un vertice si scrive in due pezzi: latitudine e longitudine (N041.53.00.000 E012.29.00.000).";
            return false;
        }

        try
        {
            punto = Punto.Da(CoordinateConverter.ParsePair(pezzi[0], pezzi[1]));
            return true;
        }
        catch (Exception e) when (e is CoordinateParseException or FormatException)
        {
            perche = $"«{scritto}» non è una coordinata che il sector sappia scrivere.";
            return false;
        }
    }

    /// <summary>Il diff di un file: le righe di adesso contro quelle che uscirebbero, dallo scrittore vero.</summary>
    public Diff.Esito DiffDi(FileAperto file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord)
            return new Diff.Esito([], InBlocco: false);

        // Il «prima» è il file com'era all'APERTURA. Se un record è stato aggiunto o tolto, il file di adesso ha
        // già la struttura nuova: confrontarlo con sé stesso direbbe che non è cambiato niente.
        var prima = _strutturaDiPartenza.TryGetValue(file.Relativo, out object? comEra)
            ? conRecord.RigheDi(comEra)
            : conRecord.RigheDelFile([]);

        return Diff.Fra(prima, conRecord.RigheDelFile(SporchiDi(file.Relativo)));
    }

    /// <summary>
    /// Un record senza più modifiche torna pulito, e un file senza più record toccati sparisce dal pannello: il suo
    /// diff torna vuoto da sé, perché lo scrittore di un record non toccato riscrive i byte grezzi.
    /// </summary>
    private void Ripulisci(string file)
    {
        if (!_sporchi.TryGetValue(file, out var suoi))
            return;

        var restano = _fatte.Keys.Where(k => k.File == file).Select(k => k.Record).ToHashSet();
        foreach (int indice in suoi.Keys.Where(i => !restano.Contains(i)).ToList())
            suoi.Remove(indice);

        if (suoi.Count == 0)
            _sporchi.Remove(file);
    }

    /// <summary>Come si scrive un valore nel pannello e nel campo: la stessa forma che l'ispettore fa vedere.</summary>
    private static string Scrivi(object? valore) => valore switch
    {
        null => "",
        Coordinate c => CoordinateConverter.ToDottedDms(c),
        bool b => b ? "sì" : "no",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valore.ToString() ?? "",
    };

    /// <summary>
    /// Legge il testo scritto dall'AOD nel tipo del campo. Le coordinate le legge il motore, che accetta tutte le
    /// forme del sector (puntata, compatta, decimale) — e il file le riscriverà nella SUA forma (F2 slice 2).
    /// </summary>
    private static bool Converti(Type tipo, string? testo, out object? valore, out string? perche)
    {
        valore = null;
        perche = null;
        Type vero = Nullable.GetUnderlyingType(tipo) ?? tipo;
        string scritto = (testo ?? "").Trim();

        if (scritto.Length == 0)
        {
            if (vero == typeof(string))
            {
                valore = string.Empty;
                return true;
            }
            if (Nullable.GetUnderlyingType(tipo) is not null)
                return true;
            perche = "Questo campo non può restare vuoto.";
            return false;
        }

        if (vero == typeof(string))
        {
            valore = scritto;
            return true;
        }

        if (vero == typeof(Coordinate))
        {
            // Una coordinata sono DUE campi nel file (latitudine e longitudine), e qui si scrivono su una riga
            // sola: si separano su spazi, virgole o punti e virgola. Le forme le legge il motore — puntata,
            // compatta, decimale — e il file la riscriverà nella SUA (F2 slice 2).
            string[] pezzi = scritto.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pezzi.Length != 2)
            {
                perche = "Una coordinata si scrive in due pezzi: latitudine e longitudine (N041.53.00.000 E012.29.00.000).";
                return false;
            }

            try
            {
                valore = CoordinateConverter.ParsePair(pezzi[0], pezzi[1]);
                return true;
            }
            catch (Exception e) when (e is CoordinateParseException or FormatException)
            {
                perche = $"«{scritto}» non è una coordinata che il sector sappia scrivere.";
                return false;
            }
        }

        if (vero == typeof(bool))
        {
            if (scritto is "sì" or "si" or "vero" or "true" or "1")
            {
                valore = true;
                return true;
            }
            if (scritto is "no" or "falso" or "false" or "0")
            {
                valore = false;
                return true;
            }
            perche = "Si scrive «sì» o «no».";
            return false;
        }

        if (vero.IsEnum)
        {
            if (Enum.TryParse(vero, scritto, ignoreCase: true, out object? letto))
            {
                valore = letto;
                return true;
            }
            perche = $"I valori ammessi sono: {string.Join(", ", Enum.GetNames(vero))}.";
            return false;
        }

        try
        {
            valore = Convert.ChangeType(scritto, vero, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            perche = $"«{scritto}» non va bene per un campo {vero.Name}.";
            return false;
        }
    }
}
