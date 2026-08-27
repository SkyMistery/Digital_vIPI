using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Vipi.Host;

/// <summary>
/// Serve la variante <c>.br</c> o <c>.gz</c> di un file statico quando c'è, invece di comprimerlo da capo
/// a ogni richiesta.
///
/// <para><b>Perché.</b> Su net8 non esistono né <c>MapStaticAssets</c> né le varianti precompilate che
/// porta con sé (sono .NET 9+, vedi ADR-0007 §D4-ter): <c>UseStaticFiles</c> serve i file così come sono e
/// a comprimerli è il middleware, <b>a ogni richiesta</b>. Il livello che ci si può permettere lì è la
/// qualità 4 di Brotli, perché la qualità 11 su un foglio di stile da 300 KB costa centinaia di
/// millisecondi di CPU. Le varianti che <c>tools/Vipi.Assets</c> prepara al publish sono alla qualità 11:
/// qui si tratta solo di consegnarle.</para>
///
/// <para><b>Cosa fa, in una riga.</b> Se il client accetta <c>br</c> e accanto al file richiesto esiste
/// <c>&lt;file&gt;.br</c>, riscrive il percorso della richiesta su quel file e dichiara la codifica. Del
/// resto — ETag, 304, richieste parziali — continua a occuparsene <c>UseStaticFiles</c>, che è il motivo
/// per cui questo non serve il file da sé.</para>
///
/// <para>⚠️ In sviluppo le varianti non ci sono (le fa il publish) e questo middleware non fa nulla: la
/// compressione torna a essere quella a richiesta. È voluto — in sviluppo i file devono restare quelli
/// leggibili.</para>
///
/// <para>⚠️ <c>Vary: Accept-Encoding</c> non è decorativo: davanti al sito c'è Cloudflare, e senza quella
/// riga una cache intermedia potrebbe servire la variante Brotli a un client che non l'accetta.</para>
/// </summary>
internal static class AssetPrecompressi
{
    /// <summary>
    /// Le codifiche, <b>nel nostro ordine di preferenza</b>: Brotli alla qualità 11 batte gzip di parecchio,
    /// quindi si prova prima quella. Non si guardano i pesi «q=» dell'intestazione: nella pratica i browser
    /// non li usano per scegliere fra br e gzip, e leggerli qui vorrebbe dire riscrivere la negoziazione
    /// del contenuto per un caso che non si presenta.
    /// </summary>
    private static readonly (string Codifica, string Estensione)[] Varianti =
    {
        ("br", ".br"),
        ("gzip", ".gz"),
    };

    /// <summary>Va montato PRIMA di <c>UseStaticFiles</c>: è quello che poi consegna il file riscritto.</summary>
    public static IApplicationBuilder UseVipiAssetPrecompressi(this WebApplication app)
    {
        var file = app.Environment.WebRootFileProvider;

        return app.Use(async (context, next) =>
        {
            if (Applicabile(context, file, out var percorsoVariante, out var codifica))
            {
                // L'ordine conta: la codifica va dichiarata PRIMA di passare oltre, perché è quel che dice
                // a UseResponseCompression di tenere le mani a posto (non ricomprime una risposta che ha
                // già un Content-Encoding). Senza, il file compresso verrebbe compresso una seconda volta.
                context.Response.Headers.ContentEncoding = codifica;
                context.Response.Headers.Vary = "Accept-Encoding";
                context.Request.Path = percorsoVariante;
            }

            await next();
        });
    }

    /// <summary>⚠️ internal e non private: la negoziazione è la parte che può sbagliare in silenzio
    /// (servire Brotli a chi non l'ha chiesto è una pagina illeggibile, non un byte di troppo), quindi
    /// è provata direttamente invece che attraverso una richiesta.</summary>
    internal static bool Applicabile(HttpContext context, IFileProvider file,
        out string percorsoVariante, out string codifica)
    {
        percorsoVariante = "";
        codifica = "";

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) return false;

        var percorso = context.Request.Path.Value;
        if (string.IsNullOrEmpty(percorso)) return false;

        // Una richiesta che chiede già la variante non si riscrive una seconda volta.
        if (percorso.EndsWith(".br", StringComparison.OrdinalIgnoreCase)
            || percorso.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) return false;

        var accettate = context.Request.Headers.AcceptEncoding;
        if (accettate.Count == 0) return false;

        var originale = file.GetFileInfo(percorso);

        foreach (var (nome, estensione) in Varianti)
        {
            if (!Accetta(accettate, nome)) continue;

            var candidato = percorso + estensione;
            var variante = file.GetFileInfo(candidato);
            if (!variante.Exists) continue;
            if (Stantia(originale, variante)) continue;

            percorsoVariante = candidato;
            codifica = nome;
            return true;
        }

        return false;
    }

    /// <summary>
    /// La variante è più vecchia del file che dovrebbe rappresentare?
    ///
    /// <para><b>Perché questo controllo esiste.</b> Questo sito si aggiorna <b>via FTP</b>, file per file, da
    /// una persona che guarda un elenco (vedi <c>deploy/atc-ivao/LEGGIMI-FTP.md</c>). Caricare un
    /// <c>vipi-theme.css</c> nuovo e lasciare accanto il <c>vipi-theme.css.br</c> vecchio è un errore che
    /// capiterà: sono due file con lo stesso nome, uno dei due non si vede nell'elenco ordinato per nome, e
    /// non c'è nessun momento in cui qualcosa dia errore. Il sito servirebbe <b>il foglio di stile
    /// vecchio</b>, per sempre, a tutti — e la pagina sarebbe perfetta, solo sbagliata.</para>
    ///
    /// <para>Con questa riga quel caso diventa: la variante viene ignorata e si torna alla compressione a
    /// richiesta. Si perde qualche byte fino al prossimo publish fatto bene, e nient'altro. ⚠️ Sono i
    /// «qualche byte» che rendono questo controllo un affare: un guasto silenzioso vale molto di più.</para>
    ///
    /// <para>⚠️ Il confronto è a parità di secondo (<c>&lt;</c> e non <c>&lt;=</c>): un publish scrive i due
    /// file a un istante di distanza, e su un filesystem con granularità al secondo — o dopo un trasferimento
    /// FTP che arrotonda le date — l'uguaglianza è il caso normale, non il sospetto.</para>
    /// </summary>
    private static bool Stantia(IFileInfo originale, IFileInfo variante)
        => originale.Exists && variante.LastModified < originale.LastModified;

    /// <summary>
    /// «br» compare fra le codifiche accettate?
    /// ⚠️ Confronto per SEGMENTO e non <c>Contains</c>: la stringa «br» è dentro «brotli» ma anche dentro
    /// nomi che non c'entrano, e un <c>Contains</c> qui vorrebbe dire servire un file Brotli a chi non l'ha
    /// chiesto — che non è un byte di troppo, è una pagina illeggibile.
    /// </summary>
    private static bool Accetta(StringValues accettate, string codifica)
    {
        foreach (var riga in accettate)
        {
            if (riga is null) continue;
            foreach (var pezzo in riga.Split(','))
            {
                var voce = pezzo.AsSpan().Trim();
                var puntoEVirgola = voce.IndexOf(';');
                if (puntoEVirgola >= 0) voce = voce[..puntoEVirgola].Trim();
                if (voce.Equals(codifica, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Il tipo di contenuto di <c>vipi-theme.css.br</c> è <c>text/css</c>, non «sconosciuto».
    ///
    /// <para>Senza questo, <c>UseStaticFiles</c> non saprebbe che tipo dare al file riscritto e — con
    /// <c>ServeUnknownFileTypes</c> a false, che è il default e va benissimo così — risponderebbe 404: le
    /// varianti sarebbero nel pacchetto e non le riceverebbe nessuno.</para>
    /// </summary>
    public sealed class TipiConVariantiCompresse : IContentTypeProvider
    {
        private readonly IContentTypeProvider _interno;

        public TipiConVariantiCompresse(IContentTypeProvider? interno = null)
            => _interno = interno ?? new FileExtensionContentTypeProvider();

        public bool TryGetContentType(string subpath, out string contentType)
        {
            if (subpath.EndsWith(".br", StringComparison.OrdinalIgnoreCase)
                || subpath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                subpath = subpath[..subpath.LastIndexOf('.')];

            return _interno.TryGetContentType(subpath, out contentType!);
        }
    }
}
