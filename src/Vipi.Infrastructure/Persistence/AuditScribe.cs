using System.Text.Encodings.Web;
using System.Text.Json;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Un solo punto da cui si scrive nel registro di audit (<see cref="AuditLog"/>).
///
/// <para><b>Perché un helper e non la riga ripetuta.</b> La stessa `_db.AuditLogs.Add(new AuditLog { … })`
/// stava in quattro punti e ognuno decideva per conto suo: uno serializzava i dettagli con
/// <see cref="JsonSerializer"/>, un altro li componeva a mano con l'interpolazione (e con la chiave
/// <c>acc</c> minuscola mentre gli altri scrivevano in PascalCase); l'ora la prendeva chi se la ricordava.
/// Con sette siti di scrittura la divergenza non è più un dettaglio: chi legge il registro vede due
/// vocabolari.</para>
///
/// <para>⚠️ Non chiama <c>SaveChanges</c>: la riga entra nella <b>stessa</b> transazione dell'atto che
/// descrive. Un audit salvato per conto suo racconterebbe anche i fatti che poi non sono avvenuti — e per
/// gli atti distruttivi va scritto <b>prima</b> della cancellazione, quando il nome è ancora leggibile.</para>
/// </summary>
internal static class AuditScribe
{
    /// <summary>
    /// ⚠️ Encoder rilassato di proposito. Con quello di serie «vIPI — Roma ACC» finisce nel registro come
    /// <c>vIPI — Roma ACC</c>: il registro di audit lo si legge anche in SQL, davanti a un incidente e di
    /// fretta, e un titolo scappato a metà è un titolo che chi cerca non trova (il <c>LIKE</c> non lo pesca).
    /// Non è un rischio d'iniezione: il valore torna fuori da un parser JSON e lo rende Blazor, che scappa da sé.
    /// </summary>
    private static readonly JsonSerializerOptions Opzioni = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Accoda una riga di audit al contesto. <paramref name="details"/> è un oggetto qualsiasi:
    /// viene serializzato in JSON (null = nessun dettaglio).</summary>
    public static void Write(VipiDbContext db, int actorUserId, AuditAction action,
        string entityType, string entityId, object? details = null, DateTime? whenUtc = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TimestampUtc = whenUtc ?? DateTime.UtcNow,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details, Opzioni),
        });
}
