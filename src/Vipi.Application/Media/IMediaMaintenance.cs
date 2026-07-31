namespace Vipi.Application.Media;

/// <summary>Un'immagine che nessun documento e nessuna release cita più: candidata alla cancellazione.</summary>
public sealed record OrphanMedia(string Sha256, string? FileName, int ByteSize, DateTime CreatedUtc, int CreatedByUserId);

/// <summary>Quadro dello spazio occupato dalle immagini e di quanto se ne può recuperare.</summary>
public sealed record MediaUsageReport(
    int TotalCount,
    long TotalBytes,
    IReadOnlyList<OrphanMedia> Orphans)
{
    public long ReclaimableBytes => Orphans.Sum(o => (long)o.ByteSize);
}

/// <summary>
/// Manutenzione del deposito immagini (docs/feature/2026-07-31-pulizia-immagini-orfane.md). Azione di admin, mai
/// automatica: si guarda l'elenco e poi si cancella, perché una cancellazione sbagliata rompe un documento
/// pubblicato senza che nessuno se ne accorga.
/// </summary>
public interface IMediaMaintenance
{
    /// <summary>Conta gli asset, lo spazio, e quali non sono citati da nessuna parte. Non modifica nulla.</summary>
    Task<MediaUsageReport> AnalyzeAsync(CancellationToken ct = default);

    /// <summary>
    /// Cancella gli sha indicati, ma solo quelli ancora orfani: fra l'analisi e il clic possono passare minuti, e in
    /// mezzo qualcuno può aver pubblicato o incollato quell'immagine in una bozza. Ritorna quanti ne ha cancellati.
    /// </summary>
    Task<int> DeleteOrphansAsync(IReadOnlyList<string> sha256, CancellationToken ct = default);
}
