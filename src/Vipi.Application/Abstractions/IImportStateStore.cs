using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Traccia l'ultima esecuzione (riuscita o fallita) di ciascun import periodico: gating dei fetch all'avvio + osservabilità dei fallimenti.</summary>
public interface IImportStateStore
{
    /// <summary>Ultima esecuzione riuscita della categoria, o null se mai eseguita.</summary>
    Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default);

    /// <summary>Marca la categoria come eseguita con successo all'istante indicato (UTC): azzera l'eventuale errore precedente.</summary>
    Task MarkSuccessAsync(string category, DateTime utc, CancellationToken ct = default);

    /// <summary>Registra un fallimento del tentativo (aggiorna LastAttemptUtc + LastError, lascia intatto LastSuccessUtc).</summary>
    Task MarkFailureAsync(string category, DateTime utc, string error, CancellationToken ct = default);

    /// <summary>Tutte le righe di stato (per il report admin delle sorgenti).</summary>
    Task<IReadOnlyList<ImportState>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>Categorie note degli import periodici gated.</summary>
public static class ImportCategories
{
    public const string Acc = "Acc";
    public const string AirportSector = "AirportSector";
    public const string SpecialArea = "SpecialArea";
    public const string Sid = "Sid";
}
