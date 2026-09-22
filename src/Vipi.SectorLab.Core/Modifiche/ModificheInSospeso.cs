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

    public IReadOnlyList<string> FileToccati => [.. _sporchi.Where(s => s.Value.Count > 0).Select(s => s.Key).Order(StringComparer.Ordinal)];

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

    // --- i vertici di una forma (slice 7) --------------------------------------------------------------------

    /// <summary>L'elenco dei vertici di un record, o null se quel campo non è un elenco di punti.</summary>
    public static System.Collections.IList? Vertici(FileAperto file, int indice, string campo)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return null;

        object record = conRecord.RecordDelModello[indice];
        var proprieta = record.GetType().GetProperty(campo, BindingFlags.Public | BindingFlags.Instance);
        if (proprieta?.GetValue(record) is not System.Collections.IList elenco)
            return null;

        var dentro = proprieta.PropertyType.IsGenericType ? proprieta.PropertyType.GetGenericArguments()[0] : null;
        return dentro == typeof(Punto) || dentro == typeof(Coordinate) ? elenco : null;
    }

    /// <summary>Cambia un vertice: il testo si legge come una coordinata, o come il NOME di un punto del catalogo.</summary>
    public object CambiaVertice(FileAperto file, int indice, string campo, int posizione, string? testo, string etichetta = "")
        => Gesto(file, indice, campo, etichetta, "vertice spostato", elenco =>
        {
            if (posizione < 0 || posizione >= elenco.Count)
                return new ModificaRifiutata("Quel vertice non c'è.");
            if (!LeggiIlPunto(elenco, testo, out object? punto, out string? perche))
                return new ModificaRifiutata(perche!);

            elenco[posizione] = punto;
            return null;
        });

    /// <summary>Aggiunge un vertice PRIMA della posizione data (o in fondo, se è quanti ce ne sono).</summary>
    public object AggiungiVertice(FileAperto file, int indice, string campo, int posizione, string? testo, string etichetta = "")
        => Gesto(file, indice, campo, etichetta, "vertice aggiunto", elenco =>
        {
            if (posizione < 0 || posizione > elenco.Count)
                return new ModificaRifiutata("Lì non si può aggiungere un vertice.");
            if (!LeggiIlPunto(elenco, testo, out object? punto, out string? perche))
                return new ModificaRifiutata(perche!);

            elenco.Insert(posizione, punto);
            return null;
        });

    public object TogliVertice(FileAperto file, int indice, string campo, int posizione, string etichetta = "")
        => Gesto(file, indice, campo, etichetta, "vertice tolto", elenco =>
        {
            if (posizione < 0 || posizione >= elenco.Count)
                return new ModificaRifiutata("Quel vertice non c'è.");
            // Una forma senza punti non è una forma: chi vuole togliere il record lo toglie (slice 8).
            if (elenco.Count == 1)
                return new ModificaRifiutata("È l'ultimo vertice: una forma senza punti non si disegna.");

            elenco.RemoveAt(posizione);
            return null;
        });

    /// <summary>
    /// «Incolla da testo» (carta §2.3): il testo dell'AIP con gli archi, o l'uscita del convertitore del sito, o
    /// delle coordinate una per riga — lo legge il <b>convertitore di F1</b>, lo stesso della vIPI, e diventa
    /// l'elenco dei vertici. Il testo con più di un'area si rifiuta: quale sarebbe questa forma?
    /// </summary>
    public object IncollaVertici(FileAperto file, int indice, string campo, string? testo, string etichetta = "",
                                 double puntiPerGrado = 1.0)
        => Gesto(file, indice, campo, etichetta, "vertici incollati", elenco =>
        {
            var letto = Vipi.Application.Coordinates.CoordinateParser.Parse(testo, puntiPerGrado);
            var aree = letto.Aree.Where(a => a.Punti.Count > 0).ToList();
            if (aree.Count == 0)
                return new ModificaRifiutata("In quel testo non c'è nessuna coordinata che si possa leggere.");
            if (aree.Count > 1)
                return new ModificaRifiutata($"Quel testo contiene {aree.Count} aree: incollane una sola.");

            var punti = aree[0].Punti;
            elenco.Clear();
            foreach (var (lat, lon) in punti)
                elenco.Add(PuntoDelTipoGiusto(elenco, new Coordinate(lat, lon)));

            return null;
        });

    /// <summary>Il giro comune dei gesti sui vertici: trova l'elenco, fotografa com'era, fa il gesto, registra.</summary>
    private object Gesto(FileAperto file, int indice, string campo, string etichetta, string cosa,
                         Func<System.Collections.IList, ModificaRifiutata?> fai)
    {
        if (Vertici(file, indice, campo) is not { } elenco)
            return new ModificaRifiutata($"Il campo «{campo}» non è un elenco di vertici.");

        var chiave = (file.Relativo, indice, campo);
        // La fotografia si prende UNA volta sola: è l'elenco dell'apertura, non quello di prima di questo gesto.
        if (!_verticiDiPartenza.ContainsKey(chiave))
            _verticiDiPartenza[chiave] = [.. elenco.Cast<object>()];

        int prima = _verticiDiPartenza[chiave].Count;
        if (fai(elenco) is { } rifiutata)
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
    private static bool LeggiIlPunto(System.Collections.IList elenco, string? testo, out object? punto, out string? perche)
    {
        punto = null;
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
            if (!AmmetteINomi(elenco))
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
            punto = PuntoDelTipoGiusto(elenco, CoordinateConverter.ParsePair(pezzi[0], pezzi[1]));
            return true;
        }
        catch (Exception e) when (e is CoordinateParseException or FormatException)
        {
            perche = $"«{scritto}» non è una coordinata che il sector sappia scrivere.";
            return false;
        }
    }

    private static bool AmmetteINomi(System.Collections.IList elenco)
        => elenco.GetType().IsGenericType && elenco.GetType().GetGenericArguments()[0] == typeof(Punto);

    /// <summary>
    /// La coordinata nel tipo che quell'elenco tiene: <see cref="Punto"/> dove i nomi si possono scrivere,
    /// <see cref="Coordinate"/> dove no.
    /// <para>🔴 I due <c>(object)</c> non sono decorazione: <see cref="Punto"/> ha una conversione IMPLICITA da
    /// <see cref="Coordinate"/>, e senza di loro il tipo comune del ternario diventa <c>Punto</c> — anche il ramo
    /// «no» usciva come Punto, e finiva in un <c>List&lt;Coordinate&gt;</c> con un'eccezione a tempo d'esecuzione.
    /// L'ha trovato la misura sull'albero vero, dove ogni <c>.pol</c> e ogni <c>.lairway</c> cadeva; i test non
    /// l'avevano visto perché toccavano i <c>.tfl</c>, che i Punto li tengono davvero.</para>
    /// </summary>
    private static object PuntoDelTipoGiusto(System.Collections.IList elenco, Coordinate coordinata)
        => AmmetteINomi(elenco) ? (object)Punto.Da(coordinata) : (object)coordinata;

    /// <summary>Il diff di un file: le righe di adesso contro quelle che uscirebbero, dallo scrittore vero.</summary>
    public Diff.Esito DiffDi(FileAperto file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord)
            return new Diff.Esito([], InBlocco: false);

        return Diff.Fra(conRecord.RigheDelFile([]), conRecord.RigheDelFile(SporchiDi(file.Relativo)));
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
