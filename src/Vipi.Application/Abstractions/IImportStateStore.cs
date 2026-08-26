using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Traccia l'ultima esecuzione (riuscita o fallita) di ciascun import periodico: gating dei fetch all'avvio + osservabilità dei fallimenti.</summary>
public interface IImportStateStore
{
    /// <summary>Ultima esecuzione riuscita della categoria, o null se mai eseguita.</summary>
    Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Il <b>penultimo</b> giro riuscito della categoria, o null se non ce ne sono ancora due. È il metro
    /// di <see cref="Vipi.Application.Content.SogliaEliminazione"/>: si elimina solo ciò che la sorgente non
    /// manda da due giri.
    /// </summary>
    Task<DateTime?> GetPrevSuccessAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Marca la categoria come eseguita con successo all'istante indicato (UTC): azzera l'eventuale errore
    /// precedente e fa scorrere il penultimo timbro, salvo che il successo precedente sia troppo recente
    /// (<see cref="Vipi.Application.Content.SogliaEliminazione.IlPenultimoScorre"/>).
    /// </summary>
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
    /// Il riempimento retroattivo del traffico d'aeroporto (dal 24 agosto 2026). Stessa categoria di policy
    /// dello storico — <c>AtcSessions</c> — ma chiave di stato sua: è un altro giro, con un altro costo e
    /// un altro arretrato da smaltire.
    /// </summary>
    public const string AirportTrafficBackfill = "AirportTrafficBackfill";

    /// <summary>
    /// Il consolidamento del <b>traffico di ogni aeroporto</b>, giorno per giorno (dal 25 agosto 2026):
    /// quanti movimenti c'erano su un campo e quanti hanno trovato un controllore acceso.
    ///
    /// <para>⚠️ Non è il gemello di <see cref="AirportTrafficBackfill"/>. Quello riempie il traffico delle
    /// <b>nostre sessioni</b> passate; questo misura il traffico che c'era <b>anche quando non c'era
    /// nessuno</b> — che è l'unica metà da cui si ricava «quanto dell'Italia copriamo». Stessa categoria di
    /// policy (<c>AtcSessions</c>), chiave di stato sua: altro giro, altro arretrato.</para>
    /// </summary>
    public const string AirportTrafficRollup = "AirportTrafficRollup";

    /// <summary>
    /// La potatura del dettaglio traffico oltre i dodici mesi (dal 25 agosto 2026). ⚠️ Non è un import: non
    /// chiama nessuna sorgente. Sta fra queste chiavi perché usa lo stesso giro gestito — periodo, stato,
    /// ultimo esito — e senza una chiave sua non si saprebbe quando ha girato l'ultima volta.
    /// </summary>
    public const string TrafficRetention = "TrafficRetention";

    /// <summary>
    /// Il giro che confronta la copia pubblicata con quel che direbbe oggi (<c>IImpactDriftUseCase</c>, dal
    /// 25 agosto 2026). ⚠️ <b>Non è un import</b> e non compare nella pagina Sorgenti: quell'elenco si
    /// intitola «stato degli import», e una riga che non interroga nessuna sorgente lì dentro mentirebbe —
    /// è la stessa ragione per cui ne resta fuori <see cref="SpecialAreaForeignOptOut"/>. Si legge in
    /// Diagnostica. Ha una chiave sua perché il giro gestito (periodo, ultimo esito, errore) è quello.
    /// </summary>
    public const string ImpactDrift = "ImpactDrift";

    /// <summary>
    /// NON è un import: è il segnaposto della riconciliazione one-shot che ha marcato le righe di catalogo
    /// <b>aggiunte a mano</b> (<c>ISectorCatalogMaintenance.MarkManualCatalogRowsAsync</c>). Senza un
    /// registro «già fatto» persistente rifarebbe il giro a ogni riavvio, e marcherebbe a mano anche righe
    /// che nel frattempo la sorgente ha cominciato a mandare.
    /// </summary>
    public const string ManualCatalogRows = "ManualCatalogRows";

    /// <summary>
    /// NON è un import periodico: è il segnaposto della riconciliazione one-shot che ha spento le aree degli ACC
    /// esteri (<c>ISpecialAreaMaintenance.OptOutForeignAreasAsync</c>). Sta qui perché serve un registro «già fatto»
    /// persistente, e questa è la tabella che ce l'ha: senza, la riconciliazione ricancellerebbe a ogni riavvio le
    /// aree di un ACC estero che l'admin ha appena abilitato.
    /// </summary>
    public const string SpecialAreaForeignOptOut = "SpecialAreaForeignOptOut";
}
