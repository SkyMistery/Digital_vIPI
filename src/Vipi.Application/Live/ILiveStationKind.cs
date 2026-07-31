namespace Vipi.Application.Live;

/// <summary>
/// Descrittore per tipo di ente della vista live (FEATURE-PROCESS §2, «regola del 2»: lo stesso switch
/// su CTR/APP viveva in due pagine). Una implementazione per tipo; il motore consulta il registry.
/// Aggiungere un tipo = registrare un descrittore, nessuno switch da toccare.
/// </summary>
public interface ILiveStationKind
{
    /// <summary>Ordine di consultazione: il PRIMO che accetta vince. Serve a mettere i casi specifici
    /// davanti a quelli generali.</summary>
    int Priority { get; }

    /// <summary>Questo descrittore sa rendere la postazione?</summary>
    bool Matches(LiveStationContext ctx);

    /// <summary>Compone il modello uniforme reso dalla pagina.</summary>
    Task<LiveView> BuildAsync(LiveStationContext ctx, CancellationToken ct = default);
}

/// <summary>Registro dei descrittori, gemello di <c>IReleaseTargetRegistry</c> (doc 09).</summary>
public interface ILiveStationRegistry
{
    /// <summary>Descrittore competente per la postazione, o null se nessuno la accetta.</summary>
    ILiveStationKind? For(LiveStationContext ctx);
}

/// <inheritdoc cref="ILiveStationRegistry"/>
public sealed class LiveStationRegistry : ILiveStationRegistry
{
    private readonly IReadOnlyList<ILiveStationKind> _kinds;

    public LiveStationRegistry(IEnumerable<ILiveStationKind> kinds) =>
        _kinds = kinds.OrderBy(k => k.Priority).ToList();

    public ILiveStationKind? For(LiveStationContext ctx) => _kinds.FirstOrDefault(k => k.Matches(ctx));
}
