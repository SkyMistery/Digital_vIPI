using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>
/// Descrittore della vSOP MILITARE d'aeroporto (carta <c>2026-08-27-vsop-militari.md</c> §4).
/// Chiave di release = ICAO, come per il gemello civile: i due non collidono perché l'identità di una
/// release è <c>(TargetType, TargetKey)</c>, e questo è il fatto che ha reso possibile il documento
/// separato — cicli AIRAC indipendenti sullo stesso scalo, senza una riga di codice in più.
///
/// <para>
/// ⚠️ <b>La difesa contro il catch-all è a DUE MANI, e servono entrambe.</b>
/// <c>AirportReleaseTarget</c> accetta qualunque <c>Document</c> vIPI non riconosciuto come APP o ACC.
/// <list type="number">
///   <item><b>L'ordine</b>: questo descrittore ha <see cref="DescribeOrder"/> più basso di tutti, così
///   viene interrogato per primo. Da solo non basta: un documento militare che questo descrittore
///   <i>non</i> riconoscesse ricadrebbe comunque nel catch-all.</item>
///   <item><b>Il controllo su <c>Edition</c></b>, messo anche sui descrittori <b>civili</b> — ed è la metà
///   che si dimentica. Da solo non basta: senza l'ordine, il catch-all civile verrebbe interrogato prima e
///   il controllo non lo raggiungerebbe mai.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AirportMilReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AirportMilReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.AirportMil;

    /// <summary>Prima di tutti: vedi l'avviso sulla classe.</summary>
    public int DescribeOrder => 0;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == key).Select(a => a.MilDocumentId).FirstOrDefaultAsync(ct);

    /// <summary>L'ACC che autorizza è lo stesso del documento civile: l'edizione non cambia chi comanda.</summary>
    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == key).Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi || doc.Edition != DocumentEdition.Military) return false;

        // ⚠️ Richiede `.Include(d => d.MilAirport).ThenInclude(a => a.Acc)` a monte: senza, l'ICAO esce
        // vuoto e il documento diventa irraggiungibile invece di dare errore. Stessa trappola del gemello.
        var icao = doc.MilAirport?.Icao ?? "";
        if (icao.Length == 0) return false;   // è militare ma non è d'aeroporto: sarà un APP

        managed = new ManagedDoc(ReleaseTargetType.AirportMil, doc.Title, icao, doc.MilAirport?.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.AirportMil, icao, doc.Id);
        return true;
    }
}

/// <summary>
/// Descrittore della vSOP militare di un APP <b>non remotizzato</b> (carta §4). Chiave di release =
/// callsign del settore APP primario, come per il gemello civile.
/// </summary>
public sealed class AppMilReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AppMilReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.AppMil;

    /// <inheritdoc cref="AirportMilReleaseTarget.DescribeOrder"/>
    public int DescribeOrder => 0;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == key && s.Type == SectorType.App
                        && s.ApproachKind == ApproachKind.Standalone && s.MilDocumentId != null)
            .Select(s => s.MilDocumentId).FirstOrDefaultAsync(ct);

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == key).Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi || doc.Edition != DocumentEdition.Military) return false;

        // ⚠️ I settori dell'edizione militare puntano al documento con MilDocumentId, quindi NON stanno in
        // `doc.Sectors` (che è la collezione del legame civile). Si risolve per callsign dal descrittore.
        var primario = doc.MilSectors?.FirstOrDefault(s => s.IsPrimary) ?? doc.MilSectors?.FirstOrDefault();
        if (primario is not { Type: SectorType.App, ApproachKind: ApproachKind.Standalone }) return false;

        managed = new ManagedDoc(ReleaseTargetType.AppMil, doc.Title, primario.Callsign, primario.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.AppMil, primario.Callsign, doc.Id);
        return true;
    }
}
