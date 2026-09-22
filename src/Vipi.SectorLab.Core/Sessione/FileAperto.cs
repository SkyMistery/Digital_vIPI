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

/// <summary>Un file che il motore interpreta: record, righe grezze, basi, e lo scrittore che lo riscriverà.</summary>
public sealed class FileLetto<T> : FileAperto
    where T : class
{
    internal FileLetto(string relativo, Impronta impronta, IReadOnlyList<LoadWarning> avvisi,
                       ParseResult<T> letto, IFileSaver<T> scrittore)
        : base(relativo, impronta, avvisi)
    {
        Letto = letto;
        Scrittore = scrittore;
    }

    /// <summary>Il file letto, con le basi fissate (<see cref="Basi.FissaLeBasi{T}"/>).</summary>
    public ParseResult<T> Letto { get; }

    public IFileSaver<T> Scrittore { get; }

    public override int Record => Letto.Records.Count;

    public override int Righe => Letto.Chunks.Sum(c => c switch
    {
        RawChunk<T> grezzo => grezzo.Lines.Length,
        RecordChunk<T> record => record.LeadingComments.Length + record.RawLines.Length,
        _ => 0,
    });
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
