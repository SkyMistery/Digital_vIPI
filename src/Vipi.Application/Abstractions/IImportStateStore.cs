namespace Vipi.Application.Abstractions;

/// <summary>Traccia l'ultima esecuzione riuscita di ciascun import periodico (per saltare i fetch ridondanti all'avvio).</summary>
public interface IImportStateStore
{
    /// <summary>Ultima esecuzione riuscita della categoria, o null se mai eseguita.</summary>
    Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default);

    /// <summary>Marca la categoria come eseguita con successo all'istante indicato (UTC).</summary>
    Task MarkSuccessAsync(string category, DateTime utc, CancellationToken ct = default);
}

/// <summary>Categorie note degli import periodici gated.</summary>
public static class ImportCategories
{
    public const string Acc = "Acc";
    public const string AirportSector = "AirportSector";
    public const string SpecialArea = "SpecialArea";
    public const string Sid = "Sid";
}
