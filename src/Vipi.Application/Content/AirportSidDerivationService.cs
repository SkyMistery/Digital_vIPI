using Vipi.Application.Abstractions;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Riga SID render-ready (celle già formattate, "—" per i campi vuoti) — output della derivazione a view-time.</summary>
public sealed record AirportSidRowView(
    string Runway, string Fix, string Name, string Transition, string InitialClimb,
    string Type, string Cat, string Wtc, string Condition);

/// <summary>Sezione SID derivata dell'aeroporto (doc 10 §3e): merge editoriali+importate già filtrato/ordinato.</summary>
public sealed record AirportSidView(IReadOnlyList<AirportSidRowView> Rows)
{
    public static AirportSidView Empty { get; } = new(Array.Empty<AirportSidRowView>());
}

/// <summary>
/// Derivazione a VIEW-TIME della sezione SID dell'aeroporto (doc 10 §3e): la SID è stata la PRIMA sezione
/// d'aeroporto a smettere di essere «cotta» nel documento, e resta derivabile con default
/// <see cref="Vipi.Domain.RenderMode.Live"/>. Dalla carta 2026-08-26 lo sono tutte.
/// Un solo posto per il merge editoriali (<c>SidRow</c>) + importate (filtro AIRAC via <see cref="SidRow.IsPublicAt"/>)
/// e l'ordine per punto (FIX) + priorità manuale. Esposta al viewer e — se la sezione è Frozen — alla cattura di
/// release via <c>IFrozenSectionProvider</c>, come le altre derivate.
/// </summary>
public interface IAirportSidDerivationService
{
    /// <param name="atCycle">
    /// A che CICLO AIRAC si guarda la tabella. <c>null</c> = quello corrente, cioè «adesso»: è la vista
    /// pubblica e la bozza.
    /// <para>
    /// ⚠️ Serve perché una SID importata compare solo dal ciclo <b>successivo</b> al prelievo
    /// (<see cref="SidRow.IsPublicAt"/>, buffer di un ciclo). Chiedendo sempre il ciclo di oggi,
    /// l'ANTEPRIMA di una release programmata al 2608 mostrava le SID come sono adesso e non come saranno
    /// quando quella release entrerà in vigore: quelle prelevate nel ciclo in corso restavano fuori
    /// dall'anteprima e poi comparivano da sole in pubblico. Chi guarda un'anteprima chiede «come sarà»,
    /// non «come è».
    /// </para>
    /// <para>⚠️ Non sposta il confine, lo SPOSTA NEL TEMPO: il buffer di un ciclo resta: una SID prelevata
    /// <i>dentro</i> il 2608 non compare nemmeno nell'anteprima del 2608 — uscirà al 2609, ed è giusto che
    /// l'anteprima lo dica.</para>
    /// </param>
    Task<AirportSidView> DeriveAsync(string icao, string? atCycle = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportSidDerivationService"/>
public sealed class AirportSidDerivationService : IAirportSidDerivationService
{
    private readonly IAirportRepository _repo;
    private readonly IAiracService _airac;

    public AirportSidDerivationService(IAirportRepository repo, IAiracService airac)
    {
        _repo = repo;
        _airac = airac;
    }

    public async Task<AirportSidView> DeriveAsync(string icao, string? atCycle = null, CancellationToken ct = default)
    {
        var data = await _repo.LoadAsync((icao ?? "").Trim().ToUpperInvariant(), ct);
        if (data is null) return AirportSidView.Empty;

        // Il ciclo a cui si guarda: quello chiesto (anteprima/cattura di una release) o quello di adesso.
        var cycle = string.IsNullOrWhiteSpace(atCycle) ? _airac.GetCycle(DateTime.UtcNow) : atCycle!.Trim();
        // I Sids arrivano già ordinati per Order dal repo; l'OrderBy stabile lo preserva come ultimo criterio a parità
        // di FIX e priorità. Le importate compaiono solo dal ciclo successivo al prelievo (o se forzate): IsPublicAt.
        var rows = data.Sids
            .Where(s => s.IsPublicAt(cycle, _airac))
            .OrderBy(s => s.Fix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Priority ?? int.MaxValue)
            .Select(s => new AirportSidRowView(
                Dash(s.Runway), s.Fix, s.Name, Dash(s.Transition), Climb(s.InitialClimb, s.InitialClimbByApp),
                Dash(s.Type), Dash(s.Cat), Dash(s.Wtc), Dash(s.Condition)))
            .ToList();
        return new AirportSidView(rows);
    }

    private static string Dash(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v!.Trim();

    // Initial climb: se la quota è "da concordare con APP" lo si annota accanto al valore (o al posto del "—").
    // Testo in inglese come il resto del documento (Transition Altitude/Level, Initial climb, ...).
    private static string Climb(string? v, bool byApp)
    {
        var q = (v ?? "").Trim();
        if (!byApp) return q.Length == 0 ? "—" : q;
        return q.Length == 0 ? "to be coordinated with APP" : $"{q} (to be coordinated with APP)";
    }
}
