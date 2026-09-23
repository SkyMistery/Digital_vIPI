using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Disco;

/// <summary>
/// Che cosa porterebbero nel sector le modifiche in sospeso (carta F3 §2.2 passo 8 e §9.6): gli errori e gli avvisi
/// che i file avrebbero DOPO e non hanno PRIMA, e i record che riletti dal disco non tornerebbero. Lo chiedono il
/// salvataggio (slice 9), prima di scrivere, e il pannello dei problemi (slice 10), dopo ogni gesto.
/// <para>In due tempi, e non per comodità: <see cref="Prepara"/> legge i record IN MEMORIA, che l'AOD cambia sul
/// posto, e va fatto sul filo del circuito (0,8 ms per file); <see cref="Prova"/> tocca solo il disco e le copie
/// temporanee, e può andare su un altro filo mentre l'AOD continua a lavorare.</para>
/// </summary>
public static class ControlloDelleModifiche
{
    /// <summary>I byte che un file avrebbe con le modifiche in sospeso, e quanti record ha in memoria.</summary>
    public sealed record DaProvare(string File, byte[] Dopo, int InMemoria);

    /// <summary>I byte di ogni file toccato (solo quelli che il motore interpreta). Legge la memoria: sul filo del circuito.</summary>
    public static IReadOnlyList<DaProvare> Prepara(SessioneAperta sessione, ModificheInSospeso modifiche)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(modifiche);
        return [.. modifiche.FileToccati
            .Where(f => sessione.File.TryGetValue(f, out var file) && file is IFileConRecord)
            .Select(f => Un(sessione.File[f], modifiche))];
    }

    internal static DaProvare Un(FileAperto file, ModificheInSospeso modifiche)
        => new(file.Relativo, ((IFileConRecord)file).ByteDelFile(modifiche.SporchiDi(file.Relativo)), file.Record);

    /// <summary>
    /// Prova i byte: problemi nuovi e record che si fondono. Tocca solo il disco (il file com'è ora, per il «prima») e
    /// le copie temporanee: può girare fuori dal circuito.
    /// </summary>
    public static (IReadOnlyList<ProblemaDelSector> Nuovi, IReadOnlyList<RecordCheSiFondono> Fusi) Prova(
        CartellaDelSector cartella, IEnumerable<DaProvare> daProvare)
    {
        ArgumentNullException.ThrowIfNull(cartella);
        ArgumentNullException.ThrowIfNull(daProvare);
        var nuovi = new List<ProblemaDelSector>();
        var fusi = new List<RecordCheSiFondono>();
        foreach (var file in daProvare)
        {
            var (problemi, riletti) = ProvaUnFile(file.File, cartella.Assoluto(file.File), file.Dopo);
            nuovi.AddRange(problemi);
            if (riletti is { } quanti && quanti != file.InMemoria)
                fusi.Add(new RecordCheSiFondono(file.File, file.InMemoria, quanti));
        }

        return (nuovi, fusi);
    }

    /// <summary>
    /// Il file coi byte nuovi, letto e validato PRIMA di scrivere (<see cref="RiletturaDiProva"/>: il clone non si
    /// tocca). Torna i problemi che il file avrebbe DOPO e non ha PRIMA, e quanti record ne rilegge il motore.
    /// <para>I problemi si confrontano per regola e testo della riga, non per numero di riga: un record aggiunto sopra
    /// fa scorrere i numeri di tutti gli errori vecchi, che non sono nuovi. Si contano, però: un record copiato da un
    /// vicino che ha un errore porta un errore in più, e quello è nuovo.</para>
    /// </summary>
    internal static (List<ProblemaDelSector> Nuovi, int? Riletti) ProvaUnFile(string relativo, string percorso, byte[] dopo)
    {
        var prima = File.Exists(percorso) ? Validatore.ValidaIlFile(percorso, relativo) : [];
        var (poi, riletti) = RiletturaDiProva.Con(relativo, dopo, copia =>
            (Validatore.ValidaIlFile(copia, relativo), RiletturaDiProva.QuantiRecord(copia)));

        var restano = prima.GroupBy(Chiave).ToDictionary(g => g.Key, g => g.Count());
        var nuovi = new List<ProblemaDelSector>();
        foreach (var problema in poi)
        {
            var chiave = Chiave(problema);
            if (restano.TryGetValue(chiave, out int quanti) && quanti > 0)
                restano[chiave] = quanti - 1;
            else
                nuovi.Add(problema);
        }

        return (nuovi, riletti);

        static (Regola, string) Chiave(ProblemaDelSector p) => (p.Regola, p.Testo.Trim());
    }
}
