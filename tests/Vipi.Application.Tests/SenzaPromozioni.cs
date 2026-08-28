using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Nessuna promozione a mano: il livello di ognuno è quello che gli danno le sue posizioni staff.
/// Gemello di quello in <c>Vipi.Infrastructure.Tests</c>, per i test che non parlano di promozioni.
/// </summary>
internal sealed class SenzaPromozioni : IRoleOverrides
{
    public static readonly SenzaPromozioni Instance = new();

    public bool Loaded => true;
    public VipiRole? For(int userId) => null;
    public IReadOnlyDictionary<int, VipiRole> All { get; } = new Dictionary<int, VipiRole>();
    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
