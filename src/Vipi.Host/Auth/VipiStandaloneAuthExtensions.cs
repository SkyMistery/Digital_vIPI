using System.Security.Claims;
using System.Text.Json;
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

                // Scritte invece che ereditate. HttpOnly e SameSite=Lax sono già i default, ma un default
                // è una cosa che cambia con la versione del framework e questo cookie è l'unica credenziale
                // che il sito emette. Lax e non Strict: il ritorno da IVAO è una navigazione cross-site, e
                // con Strict il cookie non verrebbe mandato al primo salto dopo il login.
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                // Always fuori da Development, e non SameAsRequest: dietro il reverse proxy il salto interno
                // è in chiaro, e SameAsRequest ne concluderebbe che il cookie può viaggiare senza TLS.
                // UseForwardedHeaders gira prima e di norma corregge lo schema, ma «di norma» non è la parola
                // giusta per l'unica credenziale che il sito emette.
                // ⚠️ In sviluppo resta SameAsRequest: l'host locale ascolta in http, e con Always il browser
                // scarterebbe il cookie — login che gira a vuoto senza dire perché. È lo scenario in cui si
                // prova il login vero (A6), quindi la deroga serve davvero.
                o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
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

                // SaveTokens = FALSE, e non è una svista: con true l'handler infila id_token, access_token e
                // refresh_token dentro il cookie di autenticazione, che poi viaggia a ogni richiesta e a ogni
                // handshake SignalR. Cercato chi li rilegge: nessuno — `GetTokenAsync` non compare nella
                // soluzione, e le chiamate all'API IVAO passano da tutt'altra strada (IvaoTokenProvider, con
                // le credenziali dell'APPLICAZIONE). Erano quindi credenziali di un utente vero, riemesse a
                // ogni login, spedite a ogni richiesta, per una funzione che non esiste.
                // Se un giorno servisse agire per conto dell'utente, si riaccende — e quel giorno c'è un
                // consumatore che lo giustifica.
                oidc.SaveTokens = false;

                // Callback registrati sul portale IVAO (default OIDC): /signin-oidc e /signout-callback-oidc.
                if (!string.IsNullOrWhiteSpace(opt.CallbackPath)) oidc.CallbackPath = opt.CallbackPath;
                if (!string.IsNullOrWhiteSpace(opt.SignedOutCallbackPath)) oidc.SignedOutCallbackPath = opt.SignedOutCallbackPath;

                // `profile` è lo scope che sblocca firstName/lastName sulla userinfo (/v2/users/me): senza,
                // IVAO risponde comunque, ma senza i due campi — ed è per questo che per mesi il sito ha
                // mostrato «UserId 123456» al posto del nome. Misurato il 22-ago sul flusso reale.
                oidc.Scope.Clear();
                foreach (var s in opt.Scopes.Length > 0 ? opt.Scopes : VipiAuthOptions.DefaultScopes)
                    oidc.Scope.Add(s);

                // NONCE acceso: era spento perché si riteneva che IVAO non lo mandasse. Misurato il
                // 22-ago-2026 sul flusso reale: il `nonce` è dentro l'id_token. È la difesa contro il
                // replay dell'id_token, e ora si valida.
                //
                // RequireState resta FALSO, e non è una resa a IVAO: è che ASP.NET Core non popola mai
                // `OpenIdConnectProtocolValidationContext.State`. Con `true` il validator non trova il
                // campo e lancia IDX21329 «State is null» — con qualunque IdP, sempre. Provato: login
                // rotto al primo ritorno. Lo `state` è comunque controllato, ma dall'handler e per altra
                // via: ci viaggia l'id del cookie di correlazione, che l'handler confronta da sé
                // (`ValidateCorrelationId`). Alzare questo flag non aggiunge una difesa, toglie il login.
                //
                // ⚠️ La via di fuga per il nonce resta in config, non nel codice: se IVAO smettesse di
                // mandarlo, il login di produzione si rimette in piedi con
                // VipiAuth__RelaxProtocolValidation=true, senza ricompilare né ridistribuire.
                oidc.ProtocolValidator = new IvaoOidcProtocolValidator(shouldValidateNonce: !opt.RelaxProtocolValidation)
                {
                    RequireState = false,
                    RequireNonce = !opt.RelaxProtocolValidation,
                };

                // Dalla userinfo si portano a claim SOLO i campi che qualcuno legge davvero. Prima c'era
                // MapAll(), e non era gratis: il profilo IVAO contiene `hours[]`, `rating{}`, `groups`,
                // `userStaffDetails` (email e note interne dello staffista) e un `userStaffPositions` di
                // ~1,5 kB per due incarichi — tutta roba che finiva nel cookie di autenticazione, cioè in
                // ogni richiesta e in ogni handshake SignalR. In più MapAll() AZZERA la collezione, e con
                // essa le DeleteClaim che il framework registra da sé (nonce, aud, iss, iat, exp, at_hash…):
                // toglierlo le rimette in servizio, quindi qui si guadagna due volte.
                //
                // ⚠️ Sbagliare un nome di campo non lancia: toglie l'identità o l'admin in silenzio. Per
                // questo l'elenco qui sotto non è dedotto ma MISURATO sul payload reale di /v2/users/me
                // (22-ago-2026, scope "openid profile email"); i test di ComposeDisplayName e la verifica
                // live del punto 4 (le posizioni staff devono restare) sono la rete di sicurezza.
                oidc.ClaimActions.MapJsonKey("id", "id");                 // VID, la chiave dell'identità
                oidc.ClaimActions.MapJsonKey("sub", "sub");               // ripiego del VID (HostIdentity)
                oidc.ClaimActions.MapJsonKey("centerId", "centerId");     // ACC di appartenenza (es. LIRR)
                oidc.ClaimActions.MapJsonKey("firstName", "firstName");   // ↓ i due campi del nome vero
                oidc.ClaimActions.MapJsonKey("lastName", "lastName");
                oidc.ClaimActions.MapJsonKey("publicNickname", "publicNickname"); // ripiego del nome

                // Le posizioni staff decidono chi può editare: senza, l'utente resta un lettore. Si tiene
                // il nome-claim di HostIdentityOptions ma si scrive solo l'elenco dei CODICI, non gli
                // oggetti interi: ExtractStaffPositions legge già l'array JSON di stringhe.
                oidc.ClaimActions.MapCustomJson("userStaffPositions", StaffPositionCodesJson);

                // Claim dell'id_token che nessuno legge e che pesano (o che sono dati personali di troppo).
                // Anche questi visti nel payload reale, non presunti.
                oidc.ClaimActions.DeleteClaim("ivao.aero/permissions");
                oidc.ClaimActions.DeleteClaim("profile");   // URL della pagina membro
                oidc.ClaimActions.DeleteClaim("jti");
                oidc.ClaimActions.DeleteClaim("type");

                oidc.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opt.Authority,
                    ValidAudience = opt.ClientId,
                    NameClaimType = ClaimTypes.NameIdentifier,
                };

                // Il nome visualizzato si compone qui, una volta sola, e finisce nel claim "name" — l'unico
                // che il resto del sistema legge (HostIdentityOptions.NameClaims). Vedi ComposeDisplayName.
                oidc.Events.OnUserInformationReceived = context =>
                {
                    if (context.Principal?.Identity is ClaimsIdentity identity && identity.FindFirst("name") is null)
                    {
                        var vid = identity.FindFirst("id")?.Value ?? identity.FindFirst("sub")?.Value;
                        if (ComposeDisplayName(context.User.RootElement, vid) is { } display)
                            identity.AddClaim(new Claim("name", display));
                    }
                    return Task.CompletedTask;
                };
            });

        // I nomi-claim IVAO combaciano con i default di HostIdentityOptions (UserIdClaim="id",
        // AccClaim="centerId", StaffPositionsClaim="userStaffPositions"): il claim userStaffPositions lo
        // emette la ClaimAction qui sopra come array JSON di codici, ed ExtractStaffPositions lo legge.
        // Va corretto solo il nome visualizzato: IVAO non usa `name`, usa firstName/lastName.
        builder.Services.PostConfigure<HostIdentityOptions>(h =>
        {
            // "name" = il nome composto in OnUserInformationReceived. Niente firstName/publicNickname
            // grezzi in elenco: qui passerebbe il solo nome di battesimo, o il placeholder scartato apposta.
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

    /// <summary>
    /// Nome da mostrare, dalla userinfo IVAO (<c>/v2/users/me</c>). In ordine:
    /// <list type="number">
    ///   <item><c>firstName</c> + <c>lastName</c> — il nome vero. Arriva <b>solo</b> con lo scope
    ///     <c>profile</c>: senza, i due campi non ci sono e si scende al ripiego.</item>
    ///   <item><c>publicNickname</c> / <c>nickname</c>, se non è il placeholder <c>"User {vid}"</c>
    ///     (che IVAO mette a chi non ne ha scelto uno: non è un nome, è un segnaposto).</item>
    ///   <item><c>null</c> ⇒ nessun claim <c>name</c>, e <c>HostIdentityCurrentUserProvider</c>
    ///     ripiega da sé su <c>"UserId {vid}"</c>.</item>
    /// </list>
    /// <para>Si accettano anche <c>given_name</c>/<c>family_name</c> (nomi OIDC standard): IVAO li manda
    /// entrambe le coppie, ma sono lo stesso dato e la coppia standard è quella che sopravviverebbe a un
    /// cambio di forma dell'API.</para>
    /// <para>Metà nome è meglio di nessun nome: se manca <c>lastName</c> si mostra il solo nome di
    /// battesimo, invece di scendere al VID.</para>
    /// </summary>
    internal static string? ComposeDisplayName(JsonElement userInfo, string? vid)
    {
        var first = Text(userInfo, "firstName") ?? Text(userInfo, "given_name");
        var last = Text(userInfo, "lastName") ?? Text(userInfo, "family_name");

        var real = string.Join(' ', new[] { first, last }.Where(s => s is not null));
        if (real.Length > 0) return real;

        var nick = Text(userInfo, "publicNickname") ?? Text(userInfo, "nickname");
        if (nick is null) return null;

        var isPlaceholder = !string.IsNullOrWhiteSpace(vid) &&
                            string.Equals(nick, $"User {vid}", StringComparison.OrdinalIgnoreCase);
        return isPlaceholder ? null : nick;
    }

    /// <summary>
    /// I soli codici posizione (<c>IT-AOA1</c>, …) dalla userinfo, come array JSON di stringhe —
    /// la forma più compatta che <c>HostIdentityCurrentUserProvider.ExtractStaffPositions</c> sa leggere.
    /// <para>IVAO manda un array di oggetti con dentro l'intero organigramma (<c>staffPosition</c>,
    /// <c>departmentTeam</c>, <c>department</c>): ~1,5 kB per due incarichi, di cui servono due stringhe.
    /// Si legge <c>id</c>, con <c>connectAs</c> di riserva (nel payload reale coincidono).</para>
    /// <para><c>null</c> se non c'è nessun codice: così il claim non viene nemmeno emesso.</para>
    /// </summary>
    internal static string? StaffPositionCodesJson(JsonElement userInfo)
    {
        if (!userInfo.TryGetProperty("userStaffPositions", out var array) || array.ValueKind != JsonValueKind.Array)
            return null;

        var codes = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            var code = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString()?.Trim(),
                JsonValueKind.Object => Text(item, "id") ?? Text(item, "connectAs"),
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(code)) codes.Add(code);
        }

        return codes.Count == 0 ? null : JsonSerializer.Serialize(codes);
    }

    /// <summary>Stringa non vuota di una proprietà JSON, già ripulita dagli spazi; <c>null</c> altrimenti.
    /// Il controllo su <see cref="JsonValueKind"/> evita l'eccezione di <c>GetString()</c> su un campo che
    /// l'API dovesse un giorno mandare come numero o come null.</summary>
    private static string? Text(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.String) return null;
        var s = el.GetString()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    /// <summary>
    /// Consente solo redirect locali (anti open-redirect); ripiego su <c>/vsop</c>.
    ///
    /// <para>⚠️ Il controllo «comincia per <c>/</c> e non per <c>//</c>» NON basta, ed è quello che c'era
    /// prima: i browser normalizzano la barra rovescia in barra <b>prima</b> di risolvere l'URL, quindi
    /// <c>/\evil.com</c> diventa <c>//evil.com</c> e porta fuori. Un salto del genere è un ottimo attrezzo
    /// da phishing proprio perché il primo passo — il login — è autentico.</para>
    ///
    /// <para>Qui si accetta solo ciò che è inequivocabilmente un percorso di questo sito: una barra sola,
    /// e il secondo carattere che non sia né <c>/</c> né <c>\</c>. <c>/</c> nudo compreso.</para>
    /// </summary>
    internal static string SafeReturn(string? returnUrl)
    {
        const string ripiego = "/vsop";
        if (string.IsNullOrEmpty(returnUrl)) return ripiego;

        // Un URL assoluto o uno schema (http:, javascript:, data:) non comincia per '/': cade da sé.
        if (returnUrl[0] != '/') return ripiego;
        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')) return ripiego;

        // Controllo di caratteri: un CR/LF in un Location è response splitting, e non c'è percorso
        // legittimo che li contenga.
        if (returnUrl.Any(c => c is '\r' or '\n' or '\t' || char.IsControl(c))) return ripiego;

        return returnUrl;
    }
}

/// <summary>Config dello scenario di login autonomo (sezione "VipiAuth"). Assente/Enabled=false ⇒ modulo spento.</summary>
public sealed class VipiAuthOptions
{
    public const string SectionName = "VipiAuth";

    /// <summary>
    /// Scope chiesti a IVAO quando la config non ne elenca. <c>profile</c> è quello che porta
    /// <c>firstName</c>/<c>lastName</c> sulla userinfo: senza, il sito conosce il VID e nient'altro.
    /// Unico posto dove l'elenco è scritto: <c>appsettings.json</c> lo ripete per renderlo visibile a
    /// chi configura, ma il default vive qui.
    /// <para>Niente <c>tracker</c>: chiedeva il permesso di leggere la sessione live <b>dell'utente</b>,
    /// e nessuno lo usava. Il pallino «in frequenza» (<c>LiveBadge</c>) lo alimenta il polling del server
    /// col token dell'APPLICAZIONE (sezione <c>Ivao</c>, tutt'altra strada), e il token dell'utente non lo
    /// conserviamo nemmeno (<c>SaveTokens = false</c>). Era un permesso chiesto a ogni staffista nella
    /// schermata di consenso IVAO in cambio di niente: si chiede il minimo, e il minimo è questo.</para>
    /// </summary>
    public static readonly string[] DefaultScopes = { "openid", "profile", "email" };

    /// <summary>Attiva il login IVAO standalone. Default false: in embedded l'auth la fornisce l'host.</summary>
    public bool Enabled { get; set; }

    /// <summary>Authority OIDC IVAO. Uguale al sito ufficiale: <c>https://api.ivao.aero</c>.</summary>
    public string Authority { get; set; } = "https://api.ivao.aero";

    /// <summary>Client id dell'app registrata sul portale sviluppatori IVAO. NON committare valori reali.</summary>
    public string? ClientId { get; set; }

    /// <summary>Client secret. Va in user-secrets / variabili d'ambiente, mai in appsettings versionato.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Scope richiesti. Default: <see cref="DefaultScopes"/>.</summary>
    public string[] Scopes { get; set; } = DefaultScopes;

    /// <summary>Path del callback OIDC (deve combaciare col redirect URI registrato su IVAO). Default /signin-oidc.</summary>
    public string? CallbackPath { get; set; }

    /// <summary>Path del callback di logout. Default /signout-callback-oidc.</summary>
    public string? SignedOutCallbackPath { get; set; }

    /// <summary>
    /// Spegne la validazione del <c>nonce</c> sul giro di login. Default <c>false</c>: il nonce va
    /// validato, ed è stato verificato che IVAO lo mette nell'id_token.
    /// <para>Va acceso SOLO se un cambio lato IVAO rompe il login in produzione: è una toppa da usare
    /// mentre si indaga (<c>VipiAuth__RelaxProtocolValidation=true</c>), non uno stato normale.</para>
    /// <para>Non tocca lo <c>state</c>: quello lo valida l'handler col cookie di correlazione, e il
    /// controllo omonimo del validator è inservibile in ASP.NET Core (vedi il commento sul validator).</para>
    /// </summary>
    public bool RelaxProtocolValidation { get; set; }
}
