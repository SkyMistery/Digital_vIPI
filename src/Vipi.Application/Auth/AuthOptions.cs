namespace Vipi.Application.Auth;

/// <summary>
/// Config dell'autorizzazione editing (sezione "Auth"). I codici staff che valgono come admin completo
/// (editano tutto + gestiscono i grant) sono pattern regex, modificabili da config/env var senza ricompilare.
/// Se la lista è vuota si usano i <see cref="EditAuthorizationService"/> default.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Pattern regex (case-insensitive) dei codici staff con privilegi admin completi.</summary>
    public List<string> AdminStaffCodes { get; set; } = new();
}
