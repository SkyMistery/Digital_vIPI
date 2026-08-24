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

    /// <summary>
    /// L'anagrafica aeroporti: assegnazione degli scali nuovi alla loro ACC (dal 22 agosto 2026 è un giro,
    /// prima solo il bottone «Assegna aeroporti noti»).
    ///
    /// <para>⚠️ È l'<b>unico</b> giro che <b>crea</b> entità. Additivo: non rimuove e non riassegna.</para>
    /// </summary>
    public const string AirportDirectory = "AirportDirectory";

    /// <summary>
    /// Il giro di <b>TA e piste</b> (dal 22 agosto 2026; prima arrivavano solo su richiesta).
    ///
    /// <para>⚠️ Una chiave sola per <b>due</b> categorie di policy, ed è voluto: il gate sta dentro
    /// <c>SourceMergeInputs</c>, quindi la categoria esclusa si racconta da sé («Esclusa» vince sullo stato,
    /// <see cref="Vipi.Application.Content.ImportOverviewService"/>) e ciò che resta da dire — l'ultimo giro
    /// riuscito, l'errore della sorgente — è per definizione comune a entrambe: è lo <b>stesso</b> giro sugli
    /// <b>stessi</b> aeroporti. Due chiavi sarebbero due letture della stessa cosa, cioè il modo in cui due
    /// racconti divergono.</para>
    /// </summary>
    public const string AirportData = "AirportData";
    public const string SpecialArea = "SpecialArea";
    public const string Sid = "Sid";

    /// <summary>
    /// Lo storico delle connessioni ATC per le statistiche (dal 24 agosto 2026). Il primo giro recupera i
    /// dodici mesi che la sorgente conserva; i successivi ripassano gli ultimi giorni per mettere la fine
    /// vera alle sessioni che il poller ha chiuso a occhio e recuperare quel che non ha visto.
    /// </summary>
    public const string AtcHistory = "AtcHistory";

    /// <summary>
    /// NON è un import periodico: è il segnaposto della riconciliazione one-shot che ha spento le aree degli ACC
    /// esteri (<c>ISpecialAreaMaintenance.OptOutForeignAreasAsync</c>). Sta qui perché serve un registro «già fatto»
    /// persistente, e questa è la tabella che ce l'ha: senza, la riconciliazione ricancellerebbe a ogni riavvio le
    /// aree di un ACC estero che l'admin ha appena abilitato.
    /// </summary>
    public const string SpecialAreaForeignOptOut = "SpecialAreaForeignOptOut";
}
