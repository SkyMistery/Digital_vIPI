using System.Text.Encodings.Web;
using Vipi.Application;

namespace Vipi.Host.Auth;

/// <summary>
/// La pagina che si vede quando il giro di login IVAO non si chiude. Prima al suo posto c'era
/// <c>UseExceptionHandler("/Error")</c>: «An error occurred while processing your request.», che non dice
/// niente a chi legge e non lascia niente a noi.
///
/// <para>È HTML scritto a mano, e non un componente Blazor, per una ragione sola: il modulo di login è
/// <b>staccabile</b> (vedi <see cref="VipiStandaloneAuthExtensions"/>) e vive tutto dentro
/// <c>Auth\*.cs</c>. Una pagina in <c>Vipi.Ui</c> legherebbe il core al modulo. In più questa pagina deve
/// funzionare proprio quando l'autenticazione è rotta: meno pezzi coinvolge, meglio è.</para>
///
/// <para>⚠️ Nella pagina non entra <b>nessun</b> testo di provenienza esterna. Il motivo è un valore preso
/// da un insieme chiuso; l'unica stringa variabile è il <c>returnUrl</c>, che passa da
/// <see cref="VipiStandaloneAuthExtensions.SafeReturn"/> ed è comunque codificato per attributo.</para>
/// </summary>
internal static class IvaoLoginFailurePage
{
    /// <summary>Le frasi, una per motivo riconosciuto. La chiave arriva da
    /// <see cref="VipiStandaloneAuthExtensions.ClassifyRemoteFailure"/>: qualunque altra cosa cade sul ripiego.
    ///
    /// <para>⚠️ <b>Le due lingue viaggiano in linea</b> (<see cref="Messaggio.Lingua"/>) e non dalle
    /// risorse: quelle vivono in <c>Vipi.Ui</c>, e il senso di questa pagina è dipendere dal minor numero
    /// di pezzi possibile — deve reggere <b>proprio quando</b> l'autenticazione è rotta. È lo schema
    /// dichiarato per il testo che nasce nel backend (<c>docs/design/regole-lingua.md</c>).</para>
    /// </summary>
    private static (string Titolo, string Spiegazione, bool RiprovaAiuta) Testo(string reason) => reason switch
    {
        "portale" => (
            Messaggio.Lingua(
                "IVAO non ha completato l'accesso",
                "IVAO did not complete the sign-in"),
            Messaggio.Lingua(
                "Il portale IVAO ha rifiutato la richiesta. Di solito significa che il consenso non è stato dato, "
                + "oppure che la sessione IVAO è scaduta mentre l'accesso era in corso.",
                "The IVAO portal refused the request. Usually that means consent was not granted, or that the "
                + "IVAO session expired while the sign-in was under way."),
            false),
        "correlazione" => (
            Messaggio.Lingua(
                "L'accesso è scaduto durante il percorso",
                "The sign-in expired along the way"),
            Messaggio.Lingua(
                "Il collegamento fra questa scheda e il giro su IVAO si è perso: succede se la pagina è rimasta "
                + "aperta a lungo prima di accedere, o se i cookie del sito sono stati cancellati a metà strada.",
                "The link between this tab and the trip to IVAO was lost: it happens if the page sat open for a "
                + "long time before signing in, or if the site cookies were cleared halfway through."),
            true),
        "nonce" => (
            Messaggio.Lingua(
                "L'accesso è scaduto durante il percorso",
                "The sign-in expired along the way"),
            Messaggio.Lingua(
                "La prova che lega la risposta di IVAO a questa scheda non è arrivata. Succede se la pagina è "
                + "rimasta aperta a lungo prima di accedere, o se il browser blocca i cookie di questo sito.",
                "The proof that ties IVAO's answer to this tab did not arrive. It happens if the page sat open "
                + "for a long time before signing in, or if the browser blocks this site's cookies."),
            true),
        _ => (
            Messaggio.Lingua(
                "Accesso non riuscito",
                "Sign-in did not succeed"),
            Messaggio.Lingua(
                "Il ritorno da IVAO non si è concluso. Il motivo preciso è stato registrato sul server.",
                "The return from IVAO did not complete. The precise reason has been recorded on the server."),
            true),
    };

    /// <summary>Pagina completa, pronta da scrivere nella risposta.</summary>
    internal static string Build(string reason, string returnUrl)
    {
        var (titolo, spiegazione, riprovaAiuta) = Testo(reason);
        var e = HtmlEncoder.Default;
        var loginHref = "/services/vsop/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl);

        // Il suggerimento che risolve il caso più frequente: una sessione vecchia che si porta dietro un
        // cookie non più leggibile dalla build di oggi. Uscire e rientrare lo consuma in un colpo.
        var rimedio = riprovaAiuta
            ? Messaggio.Lingua(
                "<li>Riprova: quasi sempre al secondo tentativo entra.</li>"
                + "<li>Se ricapita, esci dal sito e rientra, oppure cancella i cookie di questo sito dal browser: "
                + "una sessione vecchia può portarsi dietro dati che la versione attuale non rilegge.</li>",
                "<li>Try again: the second attempt almost always gets through.</li>"
                + "<li>If it happens again, sign out of the site and back in, or clear this site's cookies in the "
                + "browser: an old session can carry data that the current version no longer reads.</li>")
            : Messaggio.Lingua(
                "<li>Verifica di aver effettuato l'accesso su <span class=\"m\">ivao.aero</span> e di aver dato il consenso all'applicazione, poi riprova.</li>",
                "<li>Check that you are signed in on <span class=\"m\">ivao.aero</span> and that you granted the application consent, then try again.</li>");

        return $$"""
        <!doctype html>
        <html lang="{{Messaggio.Codice}}">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex">
        <title>{{e.Encode(titolo)}} · vIPI IVAO Italy</title>
        <style>
          :root { color-scheme: light dark; --bg:#f6f7f9; --fg:#16181d; --mut:#5b6472; --card:#fff; --bd:#dfe3e8; --acc:#0b5fff; }
          @media (prefers-color-scheme: dark) {
            :root { --bg:#101318; --fg:#e8eaee; --mut:#9aa4b2; --card:#171b22; --bd:#2a313b; --acc:#6ea0ff; }
          }
          * { box-sizing:border-box; }
          body { margin:0; min-height:100vh; display:flex; align-items:center; justify-content:center; padding:24px;
                 background:var(--bg); color:var(--fg);
                 font:16px/1.55 system-ui,-apple-system,"Segoe UI",Roboto,Helvetica,Arial,sans-serif; }
          .card { background:var(--card); border:1px solid var(--bd); border-radius:12px; padding:28px 30px; max-width:38rem; width:100%; }
          h1 { font-size:1.3rem; margin:0 0 10px; }
          p { margin:0 0 14px; }
          ul { margin:0 0 20px; padding-left:20px; }
          li { margin-bottom:7px; }
          .m { font-family:ui-monospace,SFMono-Regular,Consolas,monospace; font-size:.92em; }
          .row { display:flex; flex-wrap:wrap; gap:10px; align-items:center; }
          a.btn { display:inline-block; padding:9px 16px; border-radius:8px; text-decoration:none;
                  background:var(--acc); color:#fff; font-weight:600; }
          a.alt { background:transparent; color:var(--acc); border:1px solid var(--bd); font-weight:500; }
          .foot { margin-top:22px; padding-top:14px; border-top:1px solid var(--bd); color:var(--mut); font-size:.85rem; }
        </style>
        </head>
        <body>
        <main class="card">
          <h1>{{e.Encode(titolo)}}</h1>
          <p>{{e.Encode(spiegazione)}}</p>
          <ul>{{rimedio}}</ul>
          <div class="row">
            <a class="btn" href="{{e.Encode(loginHref)}}">{{Messaggio.Lingua("Riprova ad accedere", "Try signing in again")}}</a>
            <a class="btn alt" href="{{e.Encode(returnUrl)}}">{{Messaggio.Lingua("Continua senza accedere", "Carry on without signing in")}}</a>
          </div>
          <p class="foot">
            {{Messaggio.Lingua(
                "Se succede sempre, segnalatelo indicando questo codice:",
                "If it keeps happening, report it and include this code:")}} <span class="m">{{e.Encode(reason)}}</span>.
            {{Messaggio.Lingua(
                "Il dettaglio è nei log del server.",
                "The detail is in the server logs.")}}
          </p>
        </main>
        </body>
        </html>
        """;
    }
}
