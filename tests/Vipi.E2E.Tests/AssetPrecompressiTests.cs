using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// La scelta della variante precompressa, provata direttamente.
///
/// <para>È la parte che può sbagliare <b>in silenzio</b>: consegnare un file Brotli a un client che non
/// l'ha chiesto non produce un errore, produce una pagina illeggibile — e siccome ogni browser degli
/// ultimi dieci anni accetta <c>br</c>, un difetto qui si vedrebbe solo su un client vecchio, cioè da
/// nessuna parte finché non conta.</para>
/// </summary>
public sealed class AssetPrecompressiTests
{
    [Fact]
    public void Con_la_variante_a_fianco_e_il_browser_che_la_accetta_si_serve_quella()
    {
        var scelto = AssetPrecompressi.Applicabile(
            Richiesta("/a/vipi-theme.css", "br, gzip, deflate"),
            new FinteCartelle("/a/vipi-theme.css.br", "/a/vipi-theme.css.gz"),
            out var percorso, out var codifica);

        Assert.True(scelto);
        Assert.Equal("/a/vipi-theme.css.br", percorso);
        Assert.Equal("br", codifica);
    }

    /// <summary>Brotli batte gzip di parecchio: quando ci sono entrambe si prende quella, non la prima trovata.</summary>
    [Fact]
    public void Fra_le_due_varianti_vince_Brotli()
    {
        AssetPrecompressi.Applicabile(
            Richiesta("/a.js", "gzip, br"),
            new FinteCartelle("/a.js.br", "/a.js.gz"),
            out _, out var codifica);

        Assert.Equal("br", codifica);
    }

    /// <summary>Chi accetta solo gzip riceve gzip, anche se la variante Brotli esiste.</summary>
    [Fact]
    public void Chi_accetta_solo_gzip_riceve_gzip()
    {
        var scelto = AssetPrecompressi.Applicabile(
            Richiesta("/a.js", "gzip"),
            new FinteCartelle("/a.js.br", "/a.js.gz"),
            out var percorso, out var codifica);

        Assert.True(scelto);
        Assert.Equal("/a.js.gz", percorso);
        Assert.Equal("gzip", codifica);
    }

    /// <summary>
    /// ⚠️ Il caso per cui il confronto è per segmento e non un <c>Contains</c>. «brotli» CONTIENE «br», e
    /// un client che dichiarasse solo quello — o qualunque codifica futura con «br» dentro — si vedrebbe
    /// arrivare un file Brotli senza averlo chiesto.
    /// </summary>
    [Theory]
    [InlineData("brotli")]
    [InlineData("gzip-br")]
    [InlineData("identity")]
    [InlineData("deflate")]
    public void Una_codifica_che_contiene_br_senza_essere_br_non_basta(string accettate)
    {
        var scelto = AssetPrecompressi.Applicabile(
            Richiesta("/a.js", accettate),
            new FinteCartelle("/a.js.br"),
            out _, out _);

        Assert.False(scelto, $"«{accettate}» non è «br»: la variante Brotli non va servita.");
    }

    /// <summary>I pesi «q=» non scelgono, ma non devono nemmeno impedire il riconoscimento.</summary>
    [Fact]
    public void Il_peso_q_non_impedisce_di_riconoscere_la_codifica()
    {
        Assert.True(AssetPrecompressi.Applicabile(
            Richiesta("/a.js", "deflate;q=1.0, br;q=0.8"), new FinteCartelle("/a.js.br"), out _, out var c));
        Assert.Equal("br", c);
    }

    [Fact]
    public void Senza_variante_a_fianco_non_si_riscrive_niente()
    {
        Assert.False(AssetPrecompressi.Applicabile(
            Richiesta("/a.js", "br, gzip"), new FinteCartelle(), out _, out _));
    }

    [Fact]
    public void Senza_intestazione_Accept_Encoding_non_si_riscrive_niente()
    {
        Assert.False(AssetPrecompressi.Applicabile(
            Richiesta("/a.js", accettate: null), new FinteCartelle("/a.js.br"), out _, out _));
    }

    /// <summary>Una richiesta che chiede già la variante non si riscrive una seconda volta (niente «.br.br»).</summary>
    [Theory]
    [InlineData("/a.js.br")]
    [InlineData("/a.js.gz")]
    public void Una_variante_chiesta_per_nome_non_si_riscrive(string percorso)
    {
        Assert.False(AssetPrecompressi.Applicabile(
            Richiesta(percorso, "br, gzip"), new FinteCartelle(percorso + ".br"), out _, out _));
    }

    /// <summary>Solo GET e HEAD: su una POST non c'è un file statico da servire.</summary>
    [Theory]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    public void Solo_le_richieste_di_lettura(string metodo, bool atteso)
    {
        var ctx = Richiesta("/a.js", "br");
        ctx.Request.Method = metodo;

        Assert.Equal(atteso, AssetPrecompressi.Applicabile(ctx, new FinteCartelle("/a.js.br"), out _, out _));
    }

    /// <summary>
    /// Il tipo di contenuto si legge sul nome ORIGINALE. Senza, <c>UseStaticFiles</c> non saprebbe che
    /// cos'è un «.css.br» e risponderebbe 404: le varianti sarebbero nel pacchetto senza raggiungere nessuno.
    /// </summary>
    [Theory]
    [InlineData("_content/Vipi.Ui/vipi-theme.css.br", "text/css")]
    [InlineData("_content/Vipi.Ui/vipi-theme.css.gz", "text/css")]
    [InlineData("_content/Vipi.Ui/vipi-theme.css", "text/css")]
    [InlineData("_content/Vipi.Ui/vipi-ui.js.br", "text/javascript")]
    [InlineData("IT_symbol.svg.br", "image/svg+xml")]
    public void Il_tipo_di_contenuto_e_quello_del_file_vero(string percorso, string atteso)
    {
        var trovato = new AssetPrecompressi.TipiConVariantiCompresse().TryGetContentType(percorso, out var tipo);

        Assert.True(trovato, $"nessun tipo per «{percorso}»: UseStaticFiles risponderebbe 404.");
        Assert.Equal(atteso, tipo);
    }

    /// <summary>Un font non ha varianti e non deve perdere il proprio tipo passando di qui.</summary>
    [Fact]
    public void I_file_senza_variante_conservano_il_proprio_tipo()
    {
        Assert.True(new AssetPrecompressi.TipiConVariantiCompresse().TryGetContentType("fonts/x.woff2", out var tipo));
        Assert.Equal("font/woff2", tipo);
    }

    /// <summary>
    /// ⚠️ Il caso che rende sicuro l'aggiornamento via FTP. Chi carica un foglio di stile nuovo e lascia
    /// accanto la variante vecchia non riceve nessun errore da nessuna parte: il sito servirebbe il
    /// contenuto VECCHIO, per sempre, a tutti, con la pagina perfetta e solo sbagliata. Qui la variante
    /// stantia viene ignorata e si torna alla compressione a richiesta — qualche byte in più, e basta.
    /// </summary>
    [Fact]
    public void Una_variante_piu_vecchia_del_file_non_si_serve()
    {
        var cartelle = new FinteCartelle()
            .Con("/a.css", quando: new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc))
            .Con("/a.css.br", quando: new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));

        Assert.False(AssetPrecompressi.Applicabile(Richiesta("/a.css", "br"), cartelle, out _, out _),
            "servita una variante piu' vecchia del file: il browser riceverebbe il contenuto vecchio.");
    }

    /// <summary>
    /// Ma a parità di istante si serve, e non è un dettaglio: un publish scrive i due file a un secondo di
    /// distanza, e un trasferimento FTP arrotonda le date. Un controllo che rifiutasse anche l'uguaglianza
    /// spegnerebbe la precompressione quasi sempre — cioè la renderebbe inutile per prudenza.
    /// </summary>
    [Fact]
    public void A_parita_di_istante_la_variante_si_serve()
    {
        var istante = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var cartelle = new FinteCartelle().Con("/a.css", istante).Con("/a.css.br", istante);

        Assert.True(AssetPrecompressi.Applicabile(Richiesta("/a.css", "br"), cartelle, out _, out _));
    }

    private static DefaultHttpContext Richiesta(string percorso, string? accettate)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = percorso;
        if (accettate is not null) ctx.Request.Headers.AcceptEncoding = new StringValues(accettate);
        return ctx;
    }

    /// <summary>Un provider che conosce esattamente i percorsi che gli si dichiarano, e la loro data.</summary>
    private sealed class FinteCartelle : IFileProvider
    {
        private readonly Dictionary<string, DateTimeOffset> _presenti = new(StringComparer.Ordinal);

        /// <summary>Tutti allo stesso istante: la data conta solo per i test che la nominano.</summary>
        public FinteCartelle(params string[] presenti)
        {
            foreach (var p in presenti) _presenti[p] = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        }

        public FinteCartelle Con(string percorso, DateTime quando)
        {
            _presenti[percorso] = new DateTimeOffset(quando);
            return this;
        }

        public IFileInfo GetFileInfo(string subpath) =>
            _presenti.TryGetValue(subpath, out var quando) ? new Voce(true, quando) : new Voce(false, default);

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

        private sealed class Voce : IFileInfo
        {
            public Voce(bool esiste, DateTimeOffset quando) { Exists = esiste; LastModified = quando; }
            public bool Exists { get; }
            public long Length => 0;
            public string? PhysicalPath => null;
            public string Name => "";
            public DateTimeOffset LastModified { get; }
            public bool IsDirectory => false;
            public Stream CreateReadStream() => Stream.Null;
        }
    }
}
