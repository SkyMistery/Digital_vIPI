using System.Text.Encodings.Web;
using Vipi.Application;

namespace Vipi.Host;

/// <summary>
/// La pagina che si vede quando una richiesta finisce in un'eccezione non gestita. Prende il posto della
/// <c>Error.razor</c> del modello di progetto — «An error occurred while processing your request.», in
/// inglese, senza marchio e con tre paragrafi che spiegano a chi legge come si accende la modalità di
/// sviluppo: parole per uno sviluppatore, stampate a un controllore.
///
/// <para><b>Perché HTML scritto a mano e non un componente.</b> Per la stessa ragione di
/// <see cref="Auth.IvaoLoginFailurePage"/>: questa pagina deve funzionare <b>proprio quando</b> qualcosa
/// nel resto è rotto. Se a lanciare fosse stato il layout condiviso — ed è successo il 24 agosto 2026 —
/// una pagina d'errore che passa dallo stesso layout lancerebbe una seconda volta, e l'utente resterebbe
/// davanti a una risposta vuota.</para>
///
/// <para><b>Il codice serve a noi.</b> È l'unico filo fra lo screenshot che arriva su WhatsApp e la riga
/// nel file di diagnostica: <see cref="DiagnosticaErrori"/> scrive lo <i>stesso</i> identificativo che
/// questa pagina mostra. Chiedere «che codice c'era?» è quindi una domanda che si può fare, e la risposta
/// porta dritta all'eccezione.</para>
///
/// <para>⚠️ In pagina non entra <b>niente</b> dell'eccezione: né messaggio né tipo. Un messaggio d'errore
/// racconta lo schema del database, i percorsi del server e a volte una connection string.</para>
/// </summary>
internal static class PaginaErrore
{
    /// <summary>Pagina completa, pronta da scrivere nella risposta. <paramref name="codice"/> è l'identificativo
    /// della richiesta, l'unica cosa variabile che entra — ed è comunque codificata.</summary>
    internal static string Build(string? codice)
    {
        var e = HtmlEncoder.Default;

        // ⚠️ Anche questa pagina segue la lingua di chi legge, e fino al 28 agosto 2026 non lo faceva:
        // `lang="it"`, testo italiano e una riga inglese in grigio in fondo. È la pagina che un lettore
        // inglese vede PROPRIO QUANDO qualcosa si è rotto — il momento peggiore per non capire che cosa
        // c'è scritto. Le due lingue viaggiano in linea (Messaggio.Lingua) e non dalle risorse: quelle
        // vivono in Vipi.Ui, e il senso di questa pagina è dipendere dal minor numero di pezzi possibile.
        var riga = string.IsNullOrWhiteSpace(codice)
            ? ""
            : $"""
              <p class="foot">{e.Encode(Messaggio.Lingua(
                  "Se la segnalate, indicate questo codice:",
                  "If you report this, please include this code:"))} <span class="m">{e.Encode(codice)}</span>.</p>
              """;

        return $$"""
        <!doctype html>
        <html lang="{{Messaggio.Codice}}">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex">
        <title>{{Messaggio.Lingua("Errore", "Error")}} · vIPI IVAO Italy</title>
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
          <h1>{{Messaggio.Lingua(
                  "Questa pagina non si è aperta",
                  "This page did not open")}}</h1>
          <p>{{Messaggio.Lingua(
                  "La richiesta si è interrotta a metà. Non è colpa di quello che avete scritto o cliccato, e non si è perso niente di ciò che avevate già salvato.",
                  "The request stopped halfway. It is not down to anything you typed or clicked, and nothing you had already saved has been lost.")}}</p>
          <ul>
            <li>{{Messaggio.Lingua(
                    "Riprova a caricare la pagina: se l'intoppo era di passaggio, la seconda volta va.",
                    "Try loading the page again: if the hiccup was a passing one, the second time works.")}}</li>
            <li>{{Messaggio.Lingua(
                    "Se ricapita sempre sulla stessa pagina, segnalatelo: dall'altra parte c'è scritto perché.",
                    "If it keeps happening on the same page, report it: on our side the reason is written down.")}}</li>
          </ul>
          <div class="row">
            <a class="btn" href="/services">{{Messaggio.Lingua("Torna ai servizi", "Back to services")}}</a>
            <a class="btn alt" href="/services/vsop">{{Messaggio.Lingua("Vai alla documentazione", "Go to the documentation")}}</a>
          </div>
          {{riga}}
        </main>
        </body>
        </html>
        """;
    }
}
