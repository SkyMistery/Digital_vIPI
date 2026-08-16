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

    /// <summary>
    /// NON è un import periodico: è il segnaposto della riconciliazione one-shot che ha spento le aree degli ACC
    /// esteri (<c>ISpecialAreaMaintenance.OptOutForeignAreasAsync</c>). Sta qui perché serve un registro «già fatto»
    /// persistente, e questa è la tabella che ce l'ha: senza, la riconciliazione ricancellerebbe a ogni riavvio le
    /// aree di un ACC estero che l'admin ha appena abilitato.
    /// </summary>
    public const string SpecialAreaForeignOptOut = "SpecialAreaForeignOptOut";

    /// <summary>
    /// Nemmeno questo è un import periodico: è il segnaposto del travaso one-shot dei flussi di trasferimento
    /// negli accordi di coordinamento (<c>IAgreementMaintenance.MigrateFlowsToAgreementsAsync</c>). Serve un
    /// registro «già fatto» persistente, e non basta guardare se la tabella degli accordi è vuota: chi li
    /// cancellasse tutti da editor si ritroverebbe l'archivio vecchio rimesso dentro al riavvio.
    /// </summary>
    public const string TransferFlowsToAgreements = "TransferFlowsToAgreements";
}
