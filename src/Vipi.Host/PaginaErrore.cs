using System.Text.Encodings.Web;

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
        var riga = string.IsNullOrWhiteSpace(codice)
            ? ""
            : $"""
              <p class="foot">Se la segnalate, indicate questo codice: <span class="m">{e.Encode(codice)}</span>.<br>
              <span class="en">If you report this, please include the code above.</span></p>
              """;

        return $$"""
        <!doctype html>
        <html lang="it">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex">
        <title>Errore · vIPI IVAO Italy</title>
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
          .en { color:var(--mut); font-size:.88rem; }
        </style>
        </head>
        <body>
        <main class="card">
          <h1>Questa pagina non si è aperta</h1>
          <p>La richiesta si è interrotta a metà. Non è colpa di quello che avete scritto o cliccato, e non
             si è perso niente di ciò che avevate già salvato.</p>
          <ul>
            <li>Riprova a caricare la pagina: se l'intoppo era di passaggio, la seconda volta va.</li>
            <li>Se ricapita sempre sulla stessa pagina, segnalatelo: dall'altra parte c'è scritto perché.</li>
          </ul>
          <div class="row">
            <a class="btn" href="/services">Torna ai servizi</a>
            <a class="btn alt" href="/services/vsop">Vai alla documentazione</a>
          </div>
          {{riga}}
        </main>
        </body>
        </html>
        """;
    }
}
