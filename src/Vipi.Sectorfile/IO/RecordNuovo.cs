using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Aggiungere e togliere un <b>record</b> da un file già letto (Sector Lab F3, slice 8).
/// <para>Non è una modifica come le altre: cambia il numero dei record, quindi il <see cref="ParseResult{T}"/>
/// nuovo si ottiene da quello di prima — i chunk degli altri record restano <b>gli stessi oggetti</b>, con le loro
/// righe grezze e le loro basi, e lo scrittore continua a riscriverli byte per byte.</para>
/// <para>Il record nuovo esce <b>nella forma dei vicini</b>: le sue righe passano dalla stessa strada di una riga
/// aggiunta a un record toccato (<see cref="FusioneDelRecord"/>), che scrive i punti nella forma del file —
/// puntata, compatta o decimale (F2 slice 2). Un fix nuovo in <c>itgeo.geo</c> non arriva puntato in mezzo a
/// righe compatte.</para>
/// </summary>
public static class RecordNuovo
{
    /// <summary>
    /// Mette <paramref name="record"/> subito dopo il record numero <paramref name="dopoIndice"/> (in fondo se è
    /// l'ultimo, in testa con -1). Il <see cref="ParseResult{T}"/> che torna ha un record in più.
    /// </summary>
    public static ParseResult<T> Aggiungi<T>(ParseResult<T> letto, IFileSaver<T> saver, T record, int dopoIndice)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(letto);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(record);
        if (dopoIndice < -1 || dopoIndice >= letto.Records.Count)
            throw new ArgumentOutOfRangeException(nameof(dopoIndice));

        var forma = FormaDelPunto.Di(letto.Chunks.SelectMany(Righe)) ?? FormaDelPunto.Forma.Puntata;
        var righe = FusioneDelRecord.Unisci([], [], saver.Serialize(record), forma);
        var nuovo = new RecordChunk<T>(record, righe, hasMarkers: false)
        {
            // La base di un record nuovo sono le sue stesse righe: dal prossimo cambiamento la fusione sa da dove
            // partire, esattamente come per i record letti dal disco.
            Base = righe.ToArray(),
        };

        var chunk = letto.Chunks.ToList();
        var record_ = letto.Records.ToList();
        chunk.Insert(DoveMetterlo(letto, dopoIndice), nuovo);
        record_.Insert(dopoIndice + 1, record);

        return letto with { Records = record_, Chunks = chunk };
    }

    /// <summary>
    /// Toglie il record numero <paramref name="indice"/> con le sue righe.
    /// <para>🔴 I <b>commenti sopra il record restano</b>, come righe grezze al loro posto. Nel sector un commento
    /// prima di un record è quasi sempre l'intestazione di una <b>sezione</b> (<c>//////COAST</c>, <c>//fence</c>,
    /// <c>///////PISTE</c>), non la descrizione di quella riga: portarselo via togliendo il primo record del blocco
    /// cancellerebbe l'intestazione di tutti gli altri. L'ha mostrato la misura sull'albero vero — in
    /// <c>itgeo.geo</c> togliere un segmento da una riga ne cancellava tre. Un commento davvero legato al record lo
    /// toglie l'AOD a mano, ed è un gesto che si vede nel diff.</para>
    /// </summary>
    public static ParseResult<T> Togli<T>(ParseResult<T> letto, int indice)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(letto);
        if (indice < 0 || indice >= letto.Records.Count)
            throw new ArgumentOutOfRangeException(nameof(indice));

        var chunk = letto.Chunks.ToList();
        int dove = PosizioneDelChunk(letto, indice);
        var suoi = ((RecordChunk<T>)chunk[dove]).LeadingComments;
        chunk.RemoveAt(dove);
        if (suoi.Length > 0)
            chunk.Insert(dove, new RawChunk<T>(suoi));

        var record = letto.Records.ToList();
        record.RemoveAt(indice);

        return letto with { Records = record, Chunks = chunk };
    }

    /// <summary>Dove infilare il chunk nuovo: subito dopo quello del vicino, o in testa a tutto.</summary>
    private static int DoveMetterlo<T>(ParseResult<T> letto, int dopoIndice)
    {
        if (dopoIndice < 0)
            return 0;
        return PosizioneDelChunk(letto, dopoIndice) + 1;
    }

    /// <summary>La posizione, fra i chunk, del record numero dato.</summary>
    private static int PosizioneDelChunk<T>(ParseResult<T> letto, int indice)
    {
        int quale = -1;
        for (int i = 0; i < letto.Chunks.Count; i++)
        {
            if (letto.Chunks[i] is RecordChunk<T> && ++quale == indice)
                return i;
        }

        throw new ArgumentOutOfRangeException(nameof(indice), $"Il record {indice} non ha un chunk.");
    }

    private static IEnumerable<string> Righe<T>(FileChunk<T> chunk) => chunk switch
    {
        RawChunk<T> grezzo => grezzo.Lines,
        RecordChunk<T> record => record.RawLines,
        _ => [],
    };
}
