using System.Collections.Concurrent;
using System.Diagnostics;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// L'albero del sector APERTO (carta F3 §2.2, passo 1): ogni file di <c>Include/IT</c> letto dal motore con le basi
/// fissate, più gli <c>.isc</c>, <c>update.ini</c> e <c>changelog.md</c> come testo, ognuno con l'impronta dei suoi
/// byte. Misurato sul master del 22 settembre 2026 (701 file letti, 118 315 record): 1,4 s a freddo, 102 MB.
/// <para>Si apre tutto, non un file per volta: la mappa ha bisogno del contesto, i punti per nome dei cataloghi, il
/// validatore dell'albero intero (§1 della carta).</para>
/// </summary>
public sealed class SessioneAperta
{
    private readonly Dictionary<string, FileAperto> _file;

    private SessioneAperta(CartellaDelSector cartella, Dictionary<string, FileAperto> file,
                           IReadOnlyList<string> tmpOrfani, TimeSpan durata)
    {
        Cartella = cartella;
        _file = file;
        TmpOrfani = tmpOrfani;
        Durata = durata;
    }

    public CartellaDelSector Cartella { get; }

    /// <summary>I file aperti, per percorso relativo alla radice (maiuscole e minuscole indifferenti: è Windows).</summary>
    public IReadOnlyDictionary<string, FileAperto> File => _file;

    /// <summary>
    /// I <c>*.tmp</c> dentro i confini: il salvataggio atomico li crea accanto al file e li toglie; se ce ne sono,
    /// un salvataggio è morto a metà (carta F3 §7) e git li vedrebbe come file nuovi. Si segnalano, non si toccano.
    /// </summary>
    public IReadOnlyList<string> TmpOrfani { get; }

    /// <summary>Quanto è durata l'apertura.</summary>
    public TimeSpan Durata { get; }

    public int RecordTotali => _file.Values.Sum(f => f.Record);

    public int RigheTotali => _file.Values.Sum(f => f.Righe);

    /// <summary>
    /// Legge l'albero. Lento (un paio di secondi sull'albero vero): dall'interfaccia si chiama con
    /// <see cref="ApriAsync"/>, fuori dal filo del circuito.
    /// </summary>
    public static SessioneAperta Apri(CartellaDelSector cartella, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(cartella);
        var orologio = Stopwatch.StartNew();

        var daLeggere = Directory.EnumerateFiles(cartella.CartellaIt, "*", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var soloTesto = Directory.EnumerateFiles(cartella.SectorFiles, "*.isc")
            .Append(Path.Combine(cartella.SectorFiles, "update.ini"))
            .Append(Path.Combine(cartella.Radice, "changelog.md"))
            .Where(System.IO.File.Exists)
            .ToList();

        var letti = new ConcurrentDictionary<string, FileAperto>(StringComparer.OrdinalIgnoreCase);
        // In parallelo: ogni file ha il suo lettore (Formati ne crea uno per chiamata) e la sua raccolta d'avvisi.
        Parallel.ForEach(daLeggere, new ParallelOptions { CancellationToken = annulla }, percorso =>
        {
            var file = LeggiUnFile(cartella, percorso);
            letti[file.Relativo] = file;
        });
        foreach (string percorso in soloTesto)
        {
            string relativo = cartella.Relativo(percorso);
            letti[relativo] = new FileNonInterpretato(relativo, Impronta.Di(System.IO.File.ReadAllBytes(percorso)));
        }

        var tmp = Directory.EnumerateFiles(cartella.CartellaIt, "*.tmp", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(cartella.SectorFiles, "*.tmp"))
            .Select(cartella.Relativo)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new SessioneAperta(cartella, new Dictionary<string, FileAperto>(letti, StringComparer.OrdinalIgnoreCase),
                                  tmp, orologio.Elapsed);
    }

    public static Task<SessioneAperta> ApriAsync(CartellaDelSector cartella, CancellationToken annulla = default)
        => Task.Run(() => Apri(cartella, annulla), annulla);

    /// <summary>
    /// I file aperti che sul disco non sono più quelli dell'apertura (cambiati, o spariti), in ordine. È il controllo
    /// del conflitto (carta F3 §2.4 passo 2), qui sull'albero intero.
    /// </summary>
    public IReadOnlyList<string> CambiatiSulDisco()
        => _file.Values
            .Where(f => Impronta.DelFile(Cartella.Assoluto(f.Relativo)) != f.Impronta)
            .Select(f => f.Relativo)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Un file letto con la sua impronta. ⚠️ L'impronta si prende PRIMA e si ricontrolla DOPO la lettura: il lettore del
    /// motore apre il file per conto suo, e se nel frattempo qualcuno lo riscrive (un <c>git pull</c> a metà apertura)
    /// l'impronta direbbe una cosa e i record un'altra — e il salvataggio non vedrebbe il conflitto. Si riprova.
    /// </summary>
    private static FileAperto LeggiUnFile(CartellaDelSector cartella, string percorso)
    {
        const int tentativi = 3;
        string relativo = cartella.Relativo(percorso);
        for (int giro = 1; ; giro++)
        {
            var prima = Impronta.Di(System.IO.File.ReadAllBytes(percorso));
            var avvisi = new RaccoltaDiAvvisi();
            FileAperto file = Formati.Usa(percorso, avvisi, new Lettura(percorso, relativo, prima, avvisi), out var letto)
                ? letto
                : new FileNonInterpretato(relativo, prima);

            if (Impronta.DelFile(percorso) == prima)
                return file;
            if (giro == tentativi)
                throw new IOException($"«{relativo}» cambia mentre lo si legge ({tentativi} tentativi): chi lo sta scrivendo?");
        }
    }

    private sealed class Lettura(string percorso, string relativo, Impronta impronta, RaccoltaDiAvvisi avvisi)
        : IUsoDelFormato<FileAperto>
    {
        public FileAperto Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
            => new FileLetto<T>(relativo, impronta, avvisi.Snapshot(),
                                lettore.Parse(percorso, new ColorPalette()).FissaLeBasi(scrittore), scrittore);
    }
}
