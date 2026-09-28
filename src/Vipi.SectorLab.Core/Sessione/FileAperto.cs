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
    /// Dove sta ogni record nelle righe di <see cref="RigheDelFile"/> con quei record toccati (lotto «Subito» slice 5b):
    /// l'indice (da 0) della sua prima riga — dopo i commenti che lo precedono — e quante righe ha. Servono ai gesti che
    /// lavorano sul testo del file com'è adesso (spezza, unisci, nascondi) a trovare la riga di un punto.
    /// </summary>
    IReadOnlyList<(int Da, int Quante)> PostiDeiRecord(IEnumerable<object> sporchi);

    /// <summary>
    /// Il record che ha la riga numero <paramref name="riga"/> (da 1, come le cita il validatore) fra le sue righe
    /// di dati; nullo per un commento, una riga vuota, una riga che il lettore non ha capito (slice 10).
    /// </summary>
    int? RecordDellaRiga(int riga);

    /// <summary>
    /// Le righe che il file avrebbe sul disco con quei record toccati (nessuno = il file com'è adesso). Le produce
    /// lo <b>scrittore vero</b> (F2 §9.5): il diff che si mostra è quello che uscirà, non una simulazione.
    /// </summary>
    IReadOnlyList<string> RigheDelFile(IEnumerable<object> sporchi);

    /// <summary>
    /// I BYTE che il salvataggio scriverebbe (slice 9): le righe di <see cref="RigheDelFile"/> coi fine riga, la
    /// codifica e il BOM del file. Li produce lo scrittore vero; si validano prima e si confrontano col disco dopo.
    /// </summary>
    byte[] ByteDelFile(IEnumerable<object> sporchi);

    /// <summary>
    /// Aggiunge un record <b>come il vicino</b> (slice 8): si copia il record all'indice dato, così il nuovo nasce
    /// con la forma e i campi di struttura dei suoi vicini, e l'AOD cambia quel che deve. Torna l'indice del nuovo.
    /// <para>Con <paramref name="dopo"/> il nuovo va dopo quel record invece che dopo il modello (-1 = in testa), e
    /// <paramref name="prepara"/> cambia la copia PRIMA che si scriva: un fix nuovo nasce col suo nome, già al suo posto
    /// in ordine alfabetico (committente, prova 6 del 23 settembre).</para>
    /// </summary>
    /// <para>Con <paramref name="primaDi"/> invece va subito prima di quel record, nella sua sezione (RecordNuovo.AggiungiPrimaDi).</para>
    int AggiungiComeIlVicino(int indice, int? dopo = null, Action<object>? prepara = null, int? primaDi = null);

    /// <summary>
    /// Per ogni record, il numero della sua SEZIONE: si cambia sezione a ogni commento fra due record (le intestazioni
    /// <c>//LIBC</c> di <c>APT.fix</c>). L'ordine alfabetico di un record nuovo vale dentro la sua sezione.
    /// </summary>
    IReadOnlyList<int> Sezioni();

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

    /// <summary>
    /// Il file riletto dal motore da queste righe (una riga cambiata a mano, chiesto dal committente il 23 settembre),
    /// SENZA metterlo al posto di quello di adesso: torna una struttura da dare a <see cref="RipristinaLaStruttura"/>.
    /// Le righe diventano byte con la codifica e i fine riga del file, e si leggono col percorso intero (il lettore si
    /// sceglie anche dalla cartella, <see cref="RiletturaDiProva"/>).
    /// </summary>
    object LeggiLeRighe(IReadOnlyList<string> righe);

    // --- i metadati di §M nella scheda (lotto «Subito», slice 3d) -------------------------------------------------

    /// <summary>Il catalogo delle chiavi dei record di questo file, o null se i suoi record non portano tag.</summary>
    CatalogoDeiTag? CatalogoDeiTag { get; }

    /// <summary>Le chiavi del record come sono scritte (valori con le virgolette), o null se il record non ha tag.</summary>
    IReadOnlyDictionary<string, string>? ChiaviDi(int indice);

    /// <summary>Il primo errore dei tag del file (riga e testo), o null: sopra un tag rotto non si scrive.</summary>
    string? TagRotti();

    /// <summary>Vero se il record ha un nome suo da dichiarare, o sta già in un blocco: i suoi tag si scrivono.</summary>
    bool SiDichiara(int indice);

    /// <summary>
    /// La struttura del file col record che porta quelle chiavi (nessuna = il tag tolto), da dare a
    /// <see cref="RipristinaLaStruttura"/>. Lo scrive <see cref="Metadati"/>: le altre righe restano com'erano.
    /// </summary>
    /// <exception cref="InvalidOperationException">Il record non si può dichiarare, o il file ha tag rotti.</exception>
    /// <exception cref="ArgumentException">Una chiave fuori catalogo, o un valore che non si scrive.</exception>
    object ConLeChiavi(int indice, IReadOnlyDictionary<string, string> chiavi);

    /// <summary>
    /// La struttura del file con un blocco nuovo <c>//@"NOME" …</c> <c>//@START</c> … <c>//@END</c> che tiene i record da
    /// <paramref name="primo"/> a <paramref name="ultimo"/> (lotto «Subito» slice 6c: il gruppo di un <c>.geo</c>, che non
    /// ha un nome nelle righe di dati, prende quello del suo commento, §M).
    /// </summary>
    object ConIlBlocco(int primo, int ultimo, string nome, IReadOnlyDictionary<string, string> chiavi);
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
    public int AggiungiComeIlVicino(int indice, int? dopo = null, Action<object>? prepara = null, int? primaDi = null)
    {
        if (indice < 0 || indice >= Letto.Records.Count)
            throw new ArgumentOutOfRangeException(nameof(indice));
        int dove = dopo ?? indice;
        if (dove < -1 || dove >= Letto.Records.Count)
            throw new ArgumentOutOfRangeException(nameof(dopo));

        var copia = (T)CopiaDelRecord.Di(Letto.Records[indice]);
        prepara?.Invoke(copia);
        if (primaDi is { } prima)
        {
            Letto = RecordNuovo.AggiungiPrimaDi(Letto, Scrittore, copia, prima);
            return prima;
        }

        var accostato = RecordNuovo.Aggiungi(Letto, Scrittore, copia, dove);

        // 🔴 Nei file dove un record è un BLOCCO chiuso dalla riga vuota (.artcc, .mva…) la copia accostata al vicino,
        // riletta, è un pezzo di lui (slice 9: 63 file su 695 dell'albero vero). Se succede, e in questo file i record
        // non stanno MAI accostati, si mette fra i due la riga vuota che separa anche gli altri: è la forma dei vicini.
        // Dove invece i record si accostano (.vrt, aerovie: li separa la CHIAVE) si lascia com'è — l'AOD cambia il
        // numero o il nome, e finché non lo fa il salvataggio lo dice.
        // Prima la domanda che non costa niente: la rilettura di prova rilegge il file intero (itgeo.geo: 13 560 record).
        if (!CiSonoRecordAccostati(Letto)
            && RiletturaDiProva.Record(Relativo, ByteDi(accostato)) is { } riletti && riletti < accostato.Records.Count)
        {
            var separato = RecordNuovo.Aggiungi(Letto, Scrittore, copia, dove, separatore: [""]);
            if (RiletturaDiProva.Record(Relativo, ByteDi(separato)) == separato.Records.Count)
                accostato = separato;
        }

        Letto = accostato;
        return dove + 1;
    }

    private byte[] ByteDi(ParseResult<T> quello) => new FileSaverOrchestrator().Byte(quello, new HashSet<T>(), Scrittore);

    /// <summary>Due record uno dopo l'altro, senza righe fra loro: il formato li distingue senza riga vuota.</summary>
    private static bool CiSonoRecordAccostati(ParseResult<T> letto)
        => letto.Chunks.Zip(letto.Chunks.Skip(1)).Any(c => c.First is RecordChunk<T> && c.Second is RecordChunk<T>);

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
    public IReadOnlyList<int> Sezioni()
    {
        var sezioni = new List<int>(Letto.Records.Count);
        int sezione = 0;
        foreach (var chunk in Letto.Chunks)
        {
            switch (chunk)
            {
                case RawChunk<T> grezzo when grezzo.Lines.Any(r => r.TrimStart().StartsWith("//", StringComparison.Ordinal)):
                    sezione++;
                    break;
                case RecordChunk<T> record:
                    if (record.LeadingComments.Length > 0)
                        sezione++;
                    sezioni.Add(sezione);
                    break;
            }
        }

        return sezioni;
    }

    /// <inheritdoc/>
    public CatalogoDeiTag? CatalogoDeiTag => CatalogoDeiTag.Di<T>();

    private MetadatiDelFile<T>? MetadatiDelFile()
        => CatalogoDeiTag is { } catalogo ? Metadati.Leggi(Letto, r => Metadati.NomeDelRecord(r), catalogo) : null;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? ChiaviDi(int indice)
        => indice >= 0 && indice < Letto.Records.Count ? MetadatiDelFile()?.Di(Letto.Records[indice])?.Chiavi : null;

    /// <inheritdoc/>
    public string? TagRotti()
        => MetadatiDelFile()?.Problemi.FirstOrDefault(p => p.EUnErrore) is { } rotto ? $"riga {rotto.Riga}: {rotto.Testo}" : null;

    /// <inheritdoc/>
    public bool SiDichiara(int indice)
        => indice >= 0 && indice < Letto.Records.Count && CatalogoDeiTag is not null
           && (MetadatiDelFile()?.Di(Letto.Records[indice]) is not null
               || Metadati.NomeDelRecord(Letto.Records[indice]) is { } nome && Metadati.NomeElencabile(nome));

    /// <inheritdoc/>
    public object ConLeChiavi(int indice, IReadOnlyDictionary<string, string> chiavi)
    {
        ArgumentNullException.ThrowIfNull(chiavi);
        var record = Letto.Records[indice];
        return chiavi.Count == 0
            ? Metadati.Togli(Letto, record, r => Metadati.NomeDelRecord(r))
            : Metadati.Scrivi(Letto, record, r => Metadati.NomeDelRecord(r), chiavi);
    }

    /// <inheritdoc/>
    public object ConIlBlocco(int primo, int ultimo, string nome, IReadOnlyDictionary<string, string> chiavi)
    {
        ArgumentNullException.ThrowIfNull(chiavi);
        return Metadati.ScriviIlBlocco(Letto, Letto.Records[primo], Letto.Records[ultimo], r => Metadati.NomeDelRecord(r), nome, chiavi);
    }

    /// <inheritdoc/>
    public object LeggiLeRighe(IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        // Le righe passano dallo scrittore come UN pezzo grezzo: così i byte hanno la codifica, il BOM, i fine riga e
        // la riga finale del file, come quelli che il salvataggio scriverebbe.
        var soloTesto = Letto with { Records = [], Chunks = [new RawChunk<T>(righe.ToArray())] };
        byte[] byteDelFile = new FileSaverOrchestrator().Byte(soloTesto, new HashSet<T>(), Scrittore);
        return RiletturaDiProva.Con(Relativo, byteDelFile, percorso =>
            Formati.Usa(percorso, new RaccoltaDiAvvisi(), new Rilettura(percorso), out var letto) && letto is ParseResult<T> nuovo
                ? nuovo
                : throw new InvalidOperationException($"«{Relativo}» riletto non è più un file dello stesso tipo."));
    }

    private sealed class Rilettura(string percorso) : IUsoDelFormato<object>
    {
        public object Usa<TR>(IFileParser<TR> lettore, IFileSaver<TR> scrittore)
            where TR : class
            => lettore.Parse(percorso, new Vipi.Sectorfile.Shared.ColorPalette()).FissaLeBasi(scrittore);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> RigheDelFile(IEnumerable<object> sporchi)
    {
        ArgumentNullException.ThrowIfNull(sporchi);
        // Sporchi PER IDENTITA', come vuole lo scrittore: due record uguali campo per campo restano due record.
        var suoi = new HashSet<T>(sporchi.OfType<T>(), ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        return new FileSaverOrchestrator().Righe(Letto, suoi, Scrittore);
    }

    /// <inheritdoc/>
    public IReadOnlyList<(int Da, int Quante)> PostiDeiRecord(IEnumerable<object> sporchi)
    {
        ArgumentNullException.ThrowIfNull(sporchi);
        var suoi = new HashSet<T>(sporchi.OfType<T>(), ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        var posti = new List<(int, int)>(Letto.Records.Count);
        int riga = 0;
        foreach (var chunk in Letto.Chunks)
        {
            switch (chunk)
            {
                case RawChunk<T> grezzo:
                    riga += grezzo.Lines.Length;
                    break;

                case RecordChunk<T> record:
                    riga += record.LeadingComments.Length;
                    // Un record toccato ha le righe che lo scrittore vero gli dà: si chiede allo scrittore quel record
                    // da solo, com'è scritto dentro il file intero (la forma dei punti può cambiare, il numero no).
                    int quante = suoi.Contains(record.Record)
                        ? new FileSaverOrchestrator().Righe(Letto with { Records = [record.Record], Chunks = [record] },
                              new HashSet<T>([record.Record], ReferenceEqualityComparer.Instance as IEqualityComparer<T>), Scrittore).Count
                          - record.LeadingComments.Length
                        : record.RawLines.Length;
                    posti.Add((riga, quante));
                    riga += quante;
                    break;
            }
        }

        return posti;
    }

    /// <inheritdoc/>
    public byte[] ByteDelFile(IEnumerable<object> sporchi)
    {
        ArgumentNullException.ThrowIfNull(sporchi);
        var suoi = new HashSet<T>(sporchi.OfType<T>(), ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        return new FileSaverOrchestrator().Byte(Letto, suoi, Scrittore);
    }

    /// <inheritdoc/>
    public int? RecordDellaRiga(int riga)
    {
        // Si contano le righe come RigheDelRecord: nell'ordine dei chunk, i commenti in testa a un record compresi.
        int numero = 0;
        int quale = -1;
        foreach (var chunk in Letto.Chunks)
        {
            switch (chunk)
            {
                case RawChunk<T> grezzo:
                    numero += grezzo.Lines.Length;
                    break;

                case RecordChunk<T> record:
                    quale++;
                    numero += record.LeadingComments.Length;
                    if (riga > numero && riga <= numero + record.RawLines.Length)
                        return quale;
                    numero += record.RawLines.Length;
                    break;
            }

            if (numero >= riga)
                return null;
        }

        return null;
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
