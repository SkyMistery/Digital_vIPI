using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Nessuna promozione a mano: il livello di ognuno è quello che gli danno le sue posizioni staff.
///
/// <para>È il doppio di <see cref="IRoleOverrides"/> per i test che non parlano di promozioni — cioè quasi
/// tutti. Sta in un file suo perché sette classi di prova costruiscono <c>EditAuthorizationService</c> e
/// sette copie della stessa classe vuota sarebbero sette posti in cui, un domani, scriverla diversa.</para>
/// </summary>
internal sealed class SenzaPromozioni : IRoleOverrides
{
    public static readonly SenzaPromozioni Instance = new();

    public bool Loaded => true;
    public VipiRole? For(int userId) => null;
    public IReadOnlyDictionary<int, VipiRole> All { get; } = new Dictionary<int, VipiRole>();
    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
