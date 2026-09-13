using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vipi.Application.Abstractions;

namespace Vipi.Host.Auth;

/// <summary>
/// Rilegge da IVAO, ogni <see cref="Intervallo"/>, le posizioni staff di chi ha il cookie di sessione, e le
/// sostituisce nel cookie.
///
/// <para>🔴 <b>Perché (T-002, revisione del 13 settembre 2026).</b> Il livello di una persona viene dalle sue
/// posizioni staff IVAO, e quelle stavano nel cookie <c>vipi.auth</c> così come erano al login. Il cookie è
/// scorrevole a 7 giorni: con una visita ogni pochi giorni non scade mai, e le posizioni non si rileggevano
/// mai. Un IT-DIR tolto dallo staff restava Admin <b>per sempre</b>, e nessun admin poteva declassarlo dal
/// prodotto (le promozioni a mano alzano, non abbassano sotto il «pavimento» delle posizioni).</para>
///
/// <para>La lettura passa da <c>/v2/users/{vid}</c> col token dell'APPLICAZIONE, che le posizioni le dà
/// (misurato il 24 agosto 2026, memoria <c>ivao-api-app-token-limits</c>).</para>
///
/// <para>⚠️ <b>Tre regole che rendono la cosa sicura in entrambe le direzioni</b>:
/// <list type="number">
///   <item>IVAO che non risponde <b>non butta fuori nessuno</b>: si tiene il cookie com'è e si riprova fra
///     <see cref="RitentoDopoUnGuasto"/>. Un guasto del portale non deve diventare «nessuno è editor».</item>
///   <item>Una risposta che dice <c>isStaff: true</c> ma nessuna posizione è <b>incoerente</b>, e non si usa:
///     è la forma che avrebbe un cambio del contratto dell'API, e declasserebbe tutta la divisione.</item>
///   <item>Il VID non cambia mai qui: si sostituisce solo il claim delle posizioni.</item>
/// </list></para>
///
/// <para>⚠️ Vale per le richieste NUOVE e per i circuiti che nascono dopo: un circuito Blazor già aperto tiene
/// l'identità con cui è partito (T-018, a parte).</para>
/// </summary>
public static class RiconvalidaPosizioniStaff
{
    public static readonly TimeSpan Intervallo = TimeSpan.FromHours(4);
    public static readonly TimeSpan RitentoDopoUnGuasto = TimeSpan.FromMinutes(15);

    /// <summary>Chiave nelle proprietà del cookie: l'ultima volta che le posizioni sono state rilette.</summary>
    internal const string ChiaveVerifica = "vipi.posizioni.verificate";

    internal const string ClaimPosizioni = "userStaffPositions";

    public static Task OnValidatePrincipal(CookieValidatePrincipalContext ctx) =>
        ValidaAsync(ctx, DateTimeOffset.UtcNow);

    internal static async Task ValidaAsync(CookieValidatePrincipalContext ctx, DateTimeOffset adesso)
    {
        if (ctx.Principal?.Identity is not ClaimsIdentity identita) return;

        var ultima = UltimaVerifica(ctx) ?? ctx.Properties.IssuedUtc ?? adesso;
        if (adesso - ultima < Intervallo) return;

        var vidTesto = identita.FindFirst("id")?.Value ?? identita.FindFirst("sub")?.Value;
        if (!int.TryParse(vidTesto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vid) || vid <= 0) return;

        var elenco = ctx.HttpContext.RequestServices.GetService<IUserDirectory>();
        if (elenco is null) return;

        SourceUserStaff? profilo;
        try { profilo = await elenco.GetUserAsync(vid, ctx.HttpContext.RequestAborted); }
        catch (Exception) when (!ctx.HttpContext.RequestAborted.IsCancellationRequested) { profilo = null; }

        if (profilo is null || (profilo.IsStaff && profilo.StaffPositionCodes.Count == 0))
        {
            // Regole 1 e 2: si tiene quel che c'è, e si riprova presto invece che fra quattro ore.
            Timbra(ctx, adesso - Intervallo + RitentoDopoUnGuasto);
            ctx.ShouldRenew = true;
            return;
        }

        var nuova = new ClaimsIdentity(
            identita.Claims.Where(c => c.Type != ClaimPosizioni),
            identita.AuthenticationType, identita.NameClaimType, identita.RoleClaimType);
        if (profilo.StaffPositionCodes.Count > 0)
            nuova.AddClaim(new Claim(ClaimPosizioni, JsonSerializer.Serialize(profilo.StaffPositionCodes)));

        ctx.ReplacePrincipal(new ClaimsPrincipal(nuova));
        Timbra(ctx, adesso);
        ctx.ShouldRenew = true;
    }

    private static DateTimeOffset? UltimaVerifica(CookieValidatePrincipalContext ctx) =>
        ctx.Properties.Items.TryGetValue(ChiaveVerifica, out var s)
        && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t : null;

    private static void Timbra(CookieValidatePrincipalContext ctx, DateTimeOffset quando) =>
        ctx.Properties.Items[ChiaveVerifica] = quando.ToString("O", CultureInfo.InvariantCulture);
}
