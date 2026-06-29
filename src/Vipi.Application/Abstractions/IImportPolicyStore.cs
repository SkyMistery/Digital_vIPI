using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Stato immutabile della policy di import globale. <c>true</c> = la categoria è importata dalla sorgente
/// e bloccata (sola lettura); <c>false</c> = esclusa (manuale, l'import non la tocca).
/// </summary>
public sealed record ImportPolicySnapshot(bool TransitionAltitude, bool Runways, bool Sectors)
{
    /// <summary>Tutto importato e bloccato: default opt-out.</summary>
    public static ImportPolicySnapshot AllImported { get; } = new(true, true, true);

    /// <summary>Vero se la categoria è importata dalla sorgente (quindi sola lettura per l'utente).</summary>
    public bool IsImported(ImportCategory category) => category switch
    {
        ImportCategory.TransitionAltitude => TransitionAltitude,
        ImportCategory.Runways => Runways,
        ImportCategory.Sectors => Sectors,
        _ => true,
    };
}

/// <summary>Persistenza della policy di import globale (riga singola, get-or-create). Impl. EF.</summary>
public interface IImportPolicyStore
{
    /// <summary>Legge la policy corrente; crea la riga di default (tutto importato) se assente.</summary>
    Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>Salva la policy, marcando autore e timestamp.</summary>
    Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default);
}
