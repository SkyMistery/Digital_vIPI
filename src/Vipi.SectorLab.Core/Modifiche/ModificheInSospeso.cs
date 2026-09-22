using System.Globalization;
using System.Reflection;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Una modifica fatta e non ancora salvata: si vede nel pannello, e si annulla.</summary>
/// <param name="Prima">Il valore com'era all'apertura, scritto come si legge: serve per annullare e per dirlo.</param>
public sealed record ModificaDiCampo(string File, int Record, string Etichetta, string Campo, string Prima, string Dopo);

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
    private readonly Dictionary<(string File, int Record, string Campo), ModificaDiCampo> _fatte = [];

    /// <summary>I record toccati, per file e per indice: dall'indice si arriva all'oggetto, che è ciò che vuole lo scrittore.</summary>
    private readonly Dictionary<string, Dictionary<int, object>> _sporchi = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ModificaDiCampo> Tutte => _fatte.Values;

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
        string primaScritto = _fatte.TryGetValue(chiave, out var gia) ? gia.Prima : Scrivi(prima);
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

    /// <summary>Annulla una modifica: rimette il valore che il campo aveva all'apertura.</summary>
    public bool Annulla(FileAperto file, ModificaDiCampo modifica)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(modifica);
        if (!_fatte.ContainsKey((modifica.File, modifica.Record, modifica.Campo)))
            return false;

        return Cambia(file, modifica.Record, modifica.Campo, modifica.Prima, modifica.Etichetta) is ModificaDiCampo;
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
