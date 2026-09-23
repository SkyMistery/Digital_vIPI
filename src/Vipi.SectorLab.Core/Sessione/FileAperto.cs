using Vipi.SectorLab.Core.Modifiche;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// Un file del sector com'era all'apertura: il percorso, l'impronta dei byte e — se il motore lo interpreta — i record
/// con le loro basi (F2 §9.5), pronti per essere mostrati, modificati e riscritti.
/// </summary>
public abstract class FileAperto
{
    private protected FileAperto(string relativo, Impronta impronta, IReadOnlyList<LoadWarning> avvisi)
    {
        Relativo = relativo;
        Impronta = impronta;
        Avvisi = avvisi;
    }

    /// <summary>Relativo alla radice del clone, barre dritte: <c>SectorFiles/Include/IT/GEO/itgeo.geo</c>.</summary>
    public string Relativo { get; }

    /// <summary>I byte com'erano all'apertura.</summary>
    public Impronta Impronta { get; }

    /// <summary>Quel che il lettore ha detto del file (righe che non capisce e simili).</summary>
    public IReadOnlyList<LoadWarning> Avvisi { get; }

    /// <summary>Quanti record ha letto il motore; zero per un file che non interpreta.</summary>
    public abstract int Record { get; }

    /// <summary>Le righe del file, così come le ha divise il lettore (per i file che non interpreta: nessuna).</summary>
    public abstract int Righe { get; }
}

/// <summary>
/// I record di un file, qualunque sia il loro tipo: <see cref="FileLetto{T}"/> è generico, e chi lavora su tutti i
/// formati insieme (il catalogo, la geometria, il validatore) non può nominare il suo T.
/// </summary>
public interface IFileConRecord
{
    IReadOnlyList<object> RecordDelModello { get; }

    /// <summary>
    /// Le righe da cui è stato letto un record, col loro numero VERO nel file (da 1), più qualche riga di contesto
    /// sopra e sotto. Sta qui e non nell'ispettore perché i <c>Chunks</c> sono generici: solo il file sa il suo T.
    /// </summary>
    IReadOnlyList<Ispezione.RigaGrezza> RigheDelRecord(int indice, int contesto);

    /// <summary>
    /// Le righe che il file avrebbe sul disco con quei record toccati (nessuno = il file com'è adesso). Le produce
    /// lo <b>scrittore vero</b> (F2 §9.5): il diff che si mostra è quello che uscirà, non una simulazione.
    /// </summary>
    IReadOnlyList<string> RigheDelFile(IEnumerable<object> sporchi);

    /// <summary>
    /// Aggiunge un record <b>come il vicino</b> (slice 8): si copia il record all'indice dato, così il nuovo nasce
    /// con la forma e i campi di struttura dei suoi vicini, e l'AOD cambia quel che deve. Torna l'indice del nuovo.
    /// </summary>
    int AggiungiComeIlVicino(int indice);

    /// <summary>Toglie un record, con le sue righe e i suoi commenti.</summary>
    void TogliIlRecord(int indice);

    /// <summary>
    /// Lo stato della <b>struttura</b> del file (quali record ci sono, e in che ordine), per rimetterlo com'era:
    /// i record restano gli stessi oggetti, quindi le modifiche ai loro campi non si perdono.
    /// </summary>
    object IstantaneaDellaStruttura();

    void RipristinaLaStruttura(object istantanea);

    /// <summary>
    /// Le righe che il file aveva con QUELLA struttura, senza nessun record toccato: è il «prima» del diff quando
    /// un record è stato aggiunto o tolto — il file di adesso non serve a confrontarsi con sé stesso.
    /// </summary>
    IReadOnlyList<string> RigheDi(object istantanea);
}

/// <summary>Un file che il motore interpreta: record, righe grezze, basi, e lo scrittore che lo riscriverà.</summary>
public sealed class FileLetto<T> : FileAperto, IFileConRecord
    where T : class
{
    internal FileLetto(string relativo, Impronta impronta, IReadOnlyList<LoadWarning> avvisi,
                       ParseResult<T> letto, IFileSaver<T> scrittore)
        : base(relativo, impronta, avvisi)
    {
        Letto = letto;
        Scrittore = scrittore;
    }

    /// <summary>
    /// Il file letto, con le basi fissate (<see cref="Basi.FissaLeBasi{T}"/>). Cambia solo quando si aggiunge o si
    /// toglie un record (slice 8): i chunk degli altri restano gli stessi oggetti, con le loro righe grezze.
    /// </summary>
    public ParseResult<T> Letto { get; private set; }

    public IFileSaver<T> Scrittore { get; }

    public IReadOnlyList<object> RecordDelModello => (IReadOnlyList<object>)Letto.Records;

    public override int Record => Letto.Records.Count;

    public override int Righe => Letto.Chunks.Sum(c => c switch
    {
        RawChunk<T> grezzo => grezzo.Lines.Length,
        RecordChunk<T> record => record.LeadingComments.Length + record.RawLines.Length,
        _ => 0,
    });

    /// <inheritdoc/>
    public int AggiungiComeIlVicino(int indice)
    {
        if (indice < 0 || indice >= Letto.Records.Count)
            throw new ArgumentOutOfRangeException(nameof(indice));

        var copia = (T)CopiaDelRecord.Di(Letto.Records[indice]);
        Letto = RecordNuovo.Aggiungi(Letto, Scrittore, copia, indice);
        return indice + 1;
    }

    /// <inheritdoc/>
    public void TogliIlRecord(int indice) => Letto = RecordNuovo.Togli(Letto, indice);

    /// <inheritdoc/>
    public object IstantaneaDellaStruttura() => Letto;

    /// <inheritdoc/>
    public void RipristinaLaStruttura(object istantanea)
    {
        if (istantanea is ParseResult<T> comEra)
            Letto = comEra;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> RigheDi(object istantanea)
        => istantanea is ParseResult<T> quella
            ? new FileSaverOrchestrator().Righe(quella, new HashSet<T>(), Scrittore)
            : [];

    /// <inheritdoc/>
    public IReadOnlyList<string> RigheDelFile(IEnumerable<object> sporchi)
    {
        ArgumentNullException.ThrowIfNull(sporchi);
        // Sporchi PER IDENTITA', come vuole lo scrittore: due record uguali campo per campo restano due record.
        var suoi = new HashSet<T>(sporchi.OfType<T>(), ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        return new FileSaverOrchestrator().Righe(Letto, suoi, Scrittore);
    }

    /// <inheritdoc/>
    public IReadOnlyList<Ispezione.RigaGrezza> RigheDelRecord(int indice, int contesto)
    {
        // Si contano le righe dall'inizio del file, nell'ordine dei chunk: il numero che ne esce è quello del disco,
        // lo stesso che cita il validatore. Un record è il chunk n-esimo fra quelli che portano un record.
        var righe = new List<(int Numero, string Testo)>();
        int numero = 0;
        int quale = -1;
        int primaDelRecord = -1;
        int dopoIlRecord = -1;

        foreach (var chunk in Letto.Chunks)
        {
            switch (chunk)
            {
                case RawChunk<T> grezzo:
                    foreach (string riga in grezzo.Lines)
                        righe.Add((++numero, riga));
                    break;

                case RecordChunk<T> record:
                    quale++;
                    bool eIlNostro = quale == indice;
                    foreach (string commento in record.LeadingComments)
                        righe.Add((++numero, commento));
                    if (eIlNostro)
                        primaDelRecord = numero;
                    foreach (string riga in record.RawLines)
                        righe.Add((++numero, riga));
                    if (eIlNostro)
                        dopoIlRecord = numero;
                    break;
            }
        }

        if (primaDelRecord < 0)
            return [];

        int da = Math.Max(1, primaDelRecord + 1 - contesto);
        int a = Math.Min(numero, dopoIlRecord + contesto);
        return [.. righe
            .Where(r => r.Numero >= da && r.Numero <= a)
            .Select(r => new Ispezione.RigaGrezza(r.Numero, r.Testo, r.Numero > primaDelRecord && r.Numero <= dopoIlRecord))];
    }
}

/// <summary>
/// Un file che il motore non interpreta (<c>.txt</c>, <c>.cpr</c>, <c>.isc</c>, <c>update.ini</c>…, carta F2 §4): se ne
/// tiene l'impronta, e si mostra come testo.
/// </summary>
public sealed class FileNonInterpretato : FileAperto
{
    internal FileNonInterpretato(string relativo, Impronta impronta)
        : base(relativo, impronta, [])
    {
    }

    public override int Record => 0;

    public override int Righe => 0;
}
