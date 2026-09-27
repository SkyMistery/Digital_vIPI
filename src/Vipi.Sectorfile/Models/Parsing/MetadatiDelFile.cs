namespace Vipi.Sectorfile.Models;

/// <summary>
/// I tag <c>//@</c> di un <c>.sid</c> o di un <c>.str</c> letti da <c>Metadati.Leggi</c> (carta madre §8.2, carta F2
/// slice 7): le chiavi del file, i record che hanno una dichiarazione, e ciò che non torna.
/// </summary>
public sealed class MetadatiDelFile<T>
    where T : class
{
    private readonly Dictionary<T, MetadatiDelRecord<T>> _perRecord;

    internal MetadatiDelFile(
        IReadOnlyDictionary<string, string> delFile,
        IReadOnlyList<MetadatiDelRecord<T>> record,
        IReadOnlyList<ProblemaDeiMetadati> problemi,
        PosizioneDelTag? sorgente,
        IReadOnlyList<MetadatiDelPunto<T>>? punti = null)
    {
        DelFile = delFile;
        Record = record;
        Problemi = problemi;
        Sorgente = sorgente;
        Punti = punti ?? [];
        _perRecord = record.ToDictionary(r => r.Record, ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
    }

    /// <summary>
    /// I tag dei punti (<c>//@@"PUNTO" …</c>, «file per file» §M regola 4) agganciati al loro punto, in ordine di file.
    /// </summary>
    public IReadOnlyList<MetadatiDelPunto<T>> Punti { get; }

    /// <summary>I tag dei punti di <paramref name="record"/>, nell'ordine dei punti.</summary>
    public IReadOnlyList<MetadatiDelPunto<T>> PuntiDi(T record)
        => Punti.Where(p => ReferenceEquals(p.Record, record)).ToList();

    /// <summary>Le chiavi del file (<c>//@source=AIRAC2610</c> nelle prime righe).</summary>
    public IReadOnlyDictionary<string, string> DelFile { get; }

    /// <summary>I record che hanno una dichiarazione <c>//@NOME</c> col loro nome, in ordine di file.</summary>
    public IReadOnlyList<MetadatiDelRecord<T>> Record { get; }

    /// <summary>Ciò che non torna, in ordine di riga. Un errore vuol dire che almeno un tag non vale.</summary>
    public IReadOnlyList<ProblemaDeiMetadati> Problemi { get; }

    /// <summary>I metadati di <paramref name="record"/>, o null se non ne ha (un record senza tag si legge come oggi).</summary>
    public MetadatiDelRecord<T>? Di(T record) => _perRecord.GetValueOrDefault(record);

    /// <summary>Dove sta la riga <c>//@source=…</c>, se c'è.</summary>
    internal PosizioneDelTag? Sorgente { get; }
}

/// <summary>La dichiarazione di un record: <c>//@BANA6W fix=BANAV initialclimb=5000</c>, e se il blocco è delimitato.</summary>
public sealed class MetadatiDelRecord<T>
{
    internal MetadatiDelRecord(T record, string nome, IReadOnlyDictionary<string, string> chiavi, int riga, PosizioneDelTag dichiarazione)
    {
        Record = record;
        Nome = nome;
        Chiavi = chiavi;
        Riga = riga;
        Dichiarazione = dichiarazione;
    }

    public T Record { get; }

    public string Nome { get; }

    /// <summary>Le chiavi della dichiarazione (<c>fix</c>, <c>initialclimb</c>), valori come scritti.</summary>
    public IReadOnlyDictionary<string, string> Chiavi { get; }

    /// <summary>Vero se il record sta fra <c>//@START</c> e <c>//@END</c>.</summary>
    public bool Delimitato { get; internal set; }

    /// <summary>La riga della dichiarazione nel file (da 1).</summary>
    public int Riga { get; }

    internal PosizioneDelTag Dichiarazione { get; }
}

/// <summary>
/// Il tag di un punto dentro un record (<c>//@@"ELVAD" role=IAF alt=+FL80 spd=-210</c>): il nome del punto come è
/// scritto (il nome, o le due coordinate con il <c>;</c>), le chiavi, e dove sta il punto fra le righe del record.
/// </summary>
public sealed class MetadatiDelPunto<T>
{
    internal MetadatiDelPunto(T record, string punto, IReadOnlyDictionary<string, string> chiavi, int riga, int rigaDelPunto)
    {
        Record = record;
        Punto = punto;
        Chiavi = chiavi;
        Riga = riga;
        RigaDelPunto = rigaDelPunto;
    }

    public T Record { get; }

    /// <summary>Il punto come lo aggancia il tag: <c>ELVAD</c>, o <c>N041.49.12.000;E012.14.03.000</c>.</summary>
    public string Punto { get; }

    public IReadOnlyDictionary<string, string> Chiavi { get; }

    /// <summary>La riga del tag nel file (da 1).</summary>
    public int Riga { get; }

    /// <summary>L'indice della riga del punto fra le righe del record (la prima, 0, è l'intestazione).</summary>
    public int RigaDelPunto { get; }
}

/// <summary>Un tag <c>//@</c> che non vale, con la sua riga (da 1) e il suo testo.</summary>
public sealed record ProblemaDeiMetadati(TipoDiProblemaDeiMetadati Tipo, int Riga, string Testo)
{
    /// <summary>Errore = il tag non si applica o il blocco è rotto; avviso = si legge, ma fuori dal catalogo o dal posto.</summary>
    public bool EUnErrore => Tipo is not (TipoDiProblemaDeiMetadati.ChiaveSconosciuta or TipoDiProblemaDeiMetadati.ChiaveDiFileFuoriPosto);
}

public enum TipoDiProblemaDeiMetadati
{
    /// <summary>La dichiarazione <c>//@NOME</c> sta sopra un record con un altro nome (la guardia del 21 settembre).</summary>
    NomeNonCombacia,

    /// <summary>Una dichiarazione senza un record sotto: una riga vuota, un'altra dichiarazione o la fine del file prima.</summary>
    DichiarazioneOrfana,

    /// <summary><c>//@START</c> senza una dichiarazione sopra.</summary>
    StartSenzaDichiarazione,

    /// <summary>Un blocco aperto da <c>//@START</c> e mai chiuso.</summary>
    StartSenzaEnd,

    /// <summary><c>//@END</c> senza un blocco aperto.</summary>
    EndSenzaStart,

    /// <summary><c>//@END ALTRO</c> che chiude il blocco di un altro nome.</summary>
    EndConAltroNome,

    /// <summary>Una chiave fuori dal catalogo (il catalogo è il contratto Lab ↔ vIPI).</summary>
    ChiaveSconosciuta,

    /// <summary>Una chiave di file (<c>//@source=…</c>) dopo il primo record o dentro un blocco.</summary>
    ChiaveDiFileFuoriPosto,

    /// <summary>Un <c>//@</c> che non si legge: vuoto, chiave senza valore, chiave ripetuta, parola senza <c>=</c> dopo le chiavi.</summary>
    RigaIllegibile,

    /// <summary>Un <c>//@@</c> senza un punto subito sotto: una riga vuota, un commento o la fine del record prima.</summary>
    TagDiPuntoOrfano,

    /// <summary>Un <c>//@@"PUNTO"</c> sopra un punto con un altro nome o altre coordinate.</summary>
    PuntoNonCombacia,

    /// <summary>Un <c>//@@</c> fuori da un record: il tag di un punto sta solo dentro una procedura.</summary>
    TagDiPuntoFuoriDalRecord,
}

/// <summary>Dove sta una riga di tag fra i pezzi del file: nelle righe grezze o nei commenti di testa di un record.</summary>
internal readonly record struct PosizioneDelTag(int Pezzo, bool InTesta, int Indice);

/// <summary>
/// Una procedura dell'elenco di una mappa composta (F3-bis D5): il nome, e la pista se l'elenco la sceglie
/// (<c>25:NENI5A</c>); senza pista vale per tutte quelle che hanno quel nome.
/// </summary>
public sealed record ProceduraDellaComposta(string? Pista, string Nome);
