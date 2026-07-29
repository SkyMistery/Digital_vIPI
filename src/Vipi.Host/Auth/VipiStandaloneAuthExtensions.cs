using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Vipi.Hosting;

namespace Vipi.Host.Auth;

/// <summary>
/// Modulo di login IVAO (OpenID Connect) per lo scenario "app autonoma" (ADR-0002 D3, scenario C). STACCABILE:
/// tutto il wiring dell'autenticazione vive qui + nella sezione di config <c>VipiAuth</c>. Quando la vIPI
/// verrà embedded in un host con auth propria (scenari A/B), basta <c>VipiAuth:Enabled=false</c> (o rimuovere
/// i file <c>Auth\*.cs</c> + il PackageReference OpenIdConnect): il core (Application/Domain/RCL) non cambia,
/// perché legge sempre l'identità via <see cref="Vipi.Application.Abstractions.ICurrentUserProvider"/> dal
/// <c>ClaimsPrincipal</c> prodotto qui.
///
/// Usa lo stesso IdP e claim del sito ufficiale <c>Ivao.It</c> (Authority <c>https://api.ivao.aero</c>,
/// flusso authorization code + userinfo). I claim IVAO (<c>id</c>, <c>centerId</c>, <c>userStaffPositions</c>)
/// combaciano con i default di <see cref="HostIdentityOptions"/>; si adatta solo il nome visualizzato.
/// </summary>
public static class VipiStandaloneAuthExtensions
{
    /// <summary>Nome dello schema di challenge OIDC IVAO.</summary>
    public const string IvaoScheme = "IVAO";

    /// <summary>
    /// Se <c>VipiAuth:Enabled=true</c>, registra cookie + OpenID Connect IVAO e rimappa i nomi dei claim IVAO
    /// sul modello neutro (<see cref="HostIdentityOptions"/>). Ritorna <c>true</c> se l'auth standalone è attiva.
    /// </summary>
    public static bool AddVipiStandaloneAuth(this WebApplicationBuilder builder)
    {
        var opt = builder.Configuration.GetSection(VipiAuthOptions.SectionName).Get<VipiAuthOptions>()
                  ?? new VipiAuthOptions();
        if (!opt.Enabled) return false;

        if (string.IsNullOrWhiteSpace(opt.ClientId))
            throw new InvalidOperationException(
                "VipiAuth:Enabled=true ma ClientId mancante. Registra l'app sul portale sviluppatori IVAO e imposta " +
                "VipiAuth:ClientId (via user-secrets o variabili d'ambiente). Il ClientSecret è opzionale: " +
                "ometterlo tratta l'app come client pubblico (solo PKCE).");

        builder.Services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = IvaoScheme;
            })
            .AddCookie(o =>
            {
                o.Cookie.Name = "vipi.auth";
                o.ExpireTimeSpan = TimeSpan.FromDays(7);
                o.SlidingExpiration = true;
                o.LoginPath = "/vsop/auth/login";
            })
            .AddOpenIdConnect(IvaoScheme, "IVAO Single Sign-On", oidc =>
            {
                oidc.Authority = opt.Authority;
                oidc.ClientId = opt.ClientId;
                // Secret opzionale: se assente → client pubblico (solo PKCE). Se presente → confidenziale.
                if (!string.IsNullOrWhiteSpace(opt.ClientSecret)) oidc.ClientSecret = opt.ClientSecret;

                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.UsePkce = true;
                oidc.GetClaimsFromUserInfoEndpoint = true;
                oidc.SaveTokens = true;

                // Callback registrati sul portale IVAO (default OIDC): /signin-oidc e /signout-callback-oidc.
                if (!string.IsNullOrWhiteSpace(opt.CallbackPath)) oidc.CallbackPath = opt.CallbackPath;
                if (!string.IsNullOrWhiteSpace(opt.SignedOutCallbackPath)) oidc.SignedOutCallbackPath = opt.SignedOutCallbackPath;

                oidc.Scope.Clear();
                foreach (var s in opt.Scopes.Length > 0 ? opt.Scopes : new[] { "openid", "email", "tracker" })
                    oidc.Scope.Add(s);

                // IVAO non è pienamente OIDC-compliant: nonce/state opzionali (come nel sito ufficiale).
                oidc.ProtocolValidator = new IvaoOidcProtocolValidator(shouldValidateNonce: false) { RequireState = false };

                // Porta a claim TUTTI i campi del profilo IVAO (id, centerId, userStaffPositions, ...).
                oidc.ClaimActions.MapAll();

                oidc.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opt.Authority,
                    ValidAudience = opt.ClientId,
                    NameClaimType = ClaimTypes.NameIdentifier,
                };

                // IVAO via OIDC NON espone nome/cognome reali: l'unico nome è publicNickname/nickname, che vale
                // "User {id}" (placeholder) per chi non ne ha impostato uno. Emetto un claim "name" solo se il
                // nickname è reale; così il roster mostra il nickname vero, e per i placeholder resta il VID.
                oidc.Events.OnUserInformationReceived = context =>
                {
                    if (context.Principal?.Identity is ClaimsIdentity identity && identity.FindFirst("name") is null)
                    {
                        var root = context.User.RootElement;
                        var nick = (root.TryGetProperty("publicNickname", out var p) ? p.GetString() : null)
                                   ?? (root.TryGetProperty("nickname", out var n) ? n.GetString() : null);
                        var vid = identity.FindFirst("id")?.Value ?? identity.FindFirst("sub")?.Value;
                        // "User {id}" è il placeholder IVAO: non è un nome vero.
                        var isPlaceholder = !string.IsNullOrWhiteSpace(vid) &&
                                            string.Equals(nick?.Trim(), $"User {vid}", StringComparison.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(nick) && !isPlaceholder)
                            identity.AddClaim(new Claim("name", nick.Trim()));
                    }
                    return Task.CompletedTask;
                };
            });

        // I nomi-claim IVAO combaciano con i default di HostIdentityOptions (UserIdClaim="id",
        // AccClaim="centerId", StaffPositionsClaim="userStaffPositions"): userStaffPositions è un array JSON
        // di oggetti { "id": "IT-DIR", ... } e ExtractStaffPositions pesca proprio "id" (= codice posizione).
        // Va corretto solo il nome visualizzato: IVAO usa publicNickname/firstName, non name/given_name.
        builder.Services.PostConfigure<HostIdentityOptions>(h =>
        {
            // "name" = nickname reale (emesso in OnUserInformationReceived solo se non placeholder).
            // Niente publicNickname grezzo qui: eviterebbe di scartare il placeholder "User {id}".
            h.NameClaims = new List<string> { "name", ClaimTypes.Name };
        });

        return true;
    }

    /// <summary>Endpoint minimi di login/logout dello scenario standalone. Montati solo se l'auth è attiva.</summary>
    public static WebApplication MapVipiStandaloneAuth(this WebApplication app)
    {
        // Avvia il flusso IVAO; al ritorno il cookie è impostato e si torna a returnUrl (default /vsop).
        app.MapGet("/vsop/auth/login", (string? returnUrl) =>
            Results.Challenge(
                // IsPersistent=true ⇒ cookie sopravvive a chiusura browser; scadenza = ExpireTimeSpan (7gg,
                // sliding). Le props del challenge fanno round-trip via OIDC e finiscono sul sign-in del cookie.
                new AuthenticationProperties { RedirectUri = SafeReturn(returnUrl), IsPersistent = true },
                new[] { IvaoScheme }));

        // Logout: cancella il cookie locale e la sessione IVAO (redirect end-session).
        app.MapGet("/vsop/auth/logout", () =>
            Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/vsop" },
                new[] { CookieAuthenticationDefaults.AuthenticationScheme, IvaoScheme }));

        return app;
    }

    /// <summary>Consente solo redirect locali (anti open-redirect); fallback a /vsop.</summary>
    private static string SafeReturn(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
            ? returnUrl
            : "/vsop";
}

/// <summary>Config dello scenario di login autonomo (sezione "VipiAuth"). Assente/Enabled=false ⇒ modulo spento.</summary>
public sealed class VipiAuthOptions
{
    public const string SectionName = "VipiAuth";

    /// <summary>Attiva il login IVAO standalone. Default false: in embedded l'auth la fornisce l'host.</summary>
    public bool Enabled { get; set; }

    /// <summary>Authority OIDC IVAO. Uguale al sito ufficiale: <c>https://api.ivao.aero</c>.</summary>
    public string Authority { get; set; } = "https://api.ivao.aero";

    /// <summary>Client id dell'app registrata sul portale sviluppatori IVAO. NON committare valori reali.</summary>
    public string? ClientId { get; set; }

    /// <summary>Client secret. Va in user-secrets / variabili d'ambiente, mai in appsettings versionato.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Scope richiesti. Default come il sito: openid, email, tracker.</summary>
    public string[] Scopes { get; set; } = { "openid", "email", "tracker" };

    /// <summary>Path del callback OIDC (deve combaciare col redirect URI registrato su IVAO). Default /signin-oidc.</summary>
    public string? CallbackPath { get; set; }

    /// <summary>Path del callback di logout. Default /signout-callback-oidc.</summary>
    public string? SignedOutCallbackPath { get; set; }
}
