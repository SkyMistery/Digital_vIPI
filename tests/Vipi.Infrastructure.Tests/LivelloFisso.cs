using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Un livello deciso dal test. <see cref="Editor"/> per i test che provano <b>che cosa</b> si scrive, non
/// <b>chi</b> può scrivere — come <see cref="LockConcesso"/> per il lock. Il cancello di ruolo delle anagrafiche
/// si prova in <c>PorteDelleAnagraficheTests</c> (T-060).
/// </summary>
internal sealed class LivelloFisso : IEditAuthorizationService
{
    public static readonly LivelloFisso Anonimo = new(VipiRole.User);
    public static readonly LivelloFisso Editor = new(VipiRole.Editor);
    public static readonly LivelloFisso Admin = new(VipiRole.Admin);

    public LivelloFisso(VipiRole livello) => Role = livello;

    public VipiRole Role { get; }
    public bool IsAdmin => Role >= VipiRole.Admin;
    public int? CurrentUserId => Role > VipiRole.User ? 1 : null;
    public string? CurrentName => Role > VipiRole.User ? "test" : null;
}
