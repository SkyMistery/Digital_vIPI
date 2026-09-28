using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Vipi.Host.Auth;

/// <summary>
/// Il <c>nonce</c> del giro IVAO viaggia anche dentro lo <c>state</c>, e se il suo cookie non torna si recupera da lì.
///
/// <para>🔴 <b>Perché (28 settembre 2026).</b> In produzione, al primo login dopo un logout: IDX21323 «Nonce was
/// null», pagina «The sign-in expired along the way»; il secondo tentativo passava. Una sola <c>/auth/login</c>
/// prima del ritorno (non il doppio avvio del 18 settembre), e il cookie di CORRELAZIONE era tornato — quello del
/// NONCE no. Le due cose escono nella stessa risposta con gli stessi attributi: perché il browser rimandi l'uno e
/// non l'altro, da qui non si vede.</para>
///
/// <para><b>Perché il recupero non abbassa la difesa.</b> Il cookie del nonce serve a legare l'id_token al browser
/// che ha avviato il giro. Lo <c>state</c> è cifrato e firmato con le chiavi del sito, e l'handler lo accetta solo se
/// il browser presenta il cookie di correlazione che ci è scritto dentro: il nonce che porta è legato allo stesso
/// browser, per un'altra strada. Si recupera SOLO se l'id_token porta esattamente il nonce mandato in questo giro;
/// un id_token con un nonce diverso resta fuori, col cookie o senza.</para>
///
/// <para>Ogni recupero lascia un avviso (<c>Vipi.Auth.Ivao</c>): è la misura che mancava. E se il login fallisce
/// lo stesso, il registro dice quale dei due casi era — cookie perso, o IVAO che rimanda un nonce che non gli si
/// è dato.</para>
/// </summary>
internal static class NonceNelloStato
{
    /// <summary>Chiave fra le proprietà del giro. Esce prima che le proprietà finiscano nel cookie di sessione.</summary>
    internal const string Chiave = "vipi.nonce";

    /// <summary>Dove il giro lascia la diagnosi per <c>OnRemoteFailure</c>, che gira dopo sulla stessa richiesta.</summary>
    internal const string ChiaveDiagnosi = "vipi.nonce.diagnosi";

    private const string PrefissoCookie = ".AspNetCore.OpenIdConnect.Nonce.";

    /// <summary>All'andata: il nonce appena generato dall'handler entra nelle proprietà, cioè nello state.</summary>
    public static Task RicordaAsync(RedirectContext context)
    {
        if (!string.IsNullOrEmpty(context.ProtocolMessage.Nonce))
            context.Properties.Items[Chiave] = context.ProtocolMessage.Nonce;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Al ritorno, dopo che l'handler ha cercato il cookie e prima che il validator guardi il nonce. Se il cookie
    /// non c'era e l'id_token porta il nonce mandato, il nonce da validare è quello dello stato.
    /// </summary>
    public static Task RecuperaAsync(TokenValidatedContext context)
    {
        string? mandato = null;
        if (context.Properties is { } proprieta && proprieta.Items.Remove(Chiave, out var valore)) mandato = valore;

        var ricevuto = context.SecurityToken?.Payload?.Nonce;
        var esito = Valuta(
            cookieTrovato: context.Nonce is not null,
            cookieInRichiesta: context.Request.Cookies.Keys.Count(k => k.StartsWith(PrefissoCookie, StringComparison.Ordinal)),
            mandato: mandato,
            ricevuto: ricevuto);

        context.HttpContext.Items[ChiaveDiagnosi] = esito.Diagnosi;

        if (esito.Recupera)
        {
            context.Nonce = mandato;
            context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                .CreateLogger(VipiStandaloneAuthExtensions.AuthLogCategory)
                .LogWarning("Login IVAO: cookie del nonce non tornato, nonce recuperato dallo stato del giro. {Diagnosi}",
                    esito.Diagnosi);
        }

        return Task.CompletedTask;
    }

    /// <summary>La decisione, separata dall'evento perché è la parte da provare. Mai un nonce nel testo.</summary>
    internal static (bool Recupera, string Diagnosi) Valuta(bool cookieTrovato, int cookieInRichiesta, string? mandato, string? ricevuto)
    {
        var combacia = mandato is not null && !string.IsNullOrEmpty(ricevuto) && Uguali(mandato, ricevuto);
        var confronto =
            mandato is null ? "stato senza nonce"
            : string.IsNullOrEmpty(ricevuto) ? "token senza nonce"
            : combacia ? "token col nonce mandato"
            : "token con un nonce DIVERSO da quello mandato";
        var diagnosi = $"Cookie del nonce: {(cookieTrovato ? "trovato" : "non trovato")} ({cookieInRichiesta} in richiesta); {confronto}.";

        return (!cookieTrovato && combacia, diagnosi);
    }

    private static bool Uguali(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
