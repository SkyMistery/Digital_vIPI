using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il formato con cui un blocco cita la sua immagine è uno solo, condiviso fra documento (<c>BodyJson</c>) e sezioni
/// extra dell'aeroporto (<c>ExtraBlock.ImageJson</c>). Qui si presidia il giro completo e i casi storti: un JSON
/// rotto deve comportarsi come «nessuna immagine», mai far saltare la resa di un documento.
/// </summary>
public class MediaRefTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Serializza_e_rilegge_lo_stesso_riferimento()
    {
        var json = MediaRef.Serialize(new MediaRef(Sha, "Vista della torre", 1600, 900));

        var back = MediaRef.Parse(json);

        Assert.NotNull(back);
        Assert.Equal(Sha, back!.MediaId);
        Assert.Equal("Vista della torre", back.Alt);
        Assert.Equal(1600, back.Width);
        Assert.Equal(900, back.Height);
    }

    [Fact]
    public void La_larghezza_scelta_sopravvive_al_giro_completo()
    {
        var json = MediaRef.Serialize(new MediaRef(Sha, "Torre", 1600, 900, 50));

        var back = MediaRef.Parse(json)!;

        Assert.Equal(50, back.Scale);
        Assert.Equal(1600, back.Width);      // la misura NATIVA non c'entra con quella di resa: restano due cose
    }

    [Theory]
    [InlineData(0, 0)]        // non scelta = piena larghezza
    [InlineData(100, 0)]      // «tutta la colonna» si scrive 0: e' lo stesso stato di chi non ha mai scelto
    [InlineData(140, 0)]      // oltre il pieno non si va: ingrandire un raster lo sgrana
    [InlineData(3, 10)]       // sotto il minimo l'immagine non e' piu' guardabile
    [InlineData(-20, 0)]
    [InlineData(35, 35)]
    public void La_percentuale_si_raddrizza_sempre(int scritta, int attesa)
    {
        Assert.Equal(attesa, MediaRef.ClampScale(scritta));
        Assert.Equal(attesa, MediaRef.Parse(MediaRef.Serialize(new MediaRef(Sha, Scale: scritta)))!.Scale);
    }

    [Fact]
    public void Un_riferimento_scritto_prima_di_questo_campo_resta_a_piena_larghezza()
    {
        // Le release congelate portano nel payload il JSON di allora: senza `scale` devono rendersi come sempre.
        var back = MediaRef.Parse("{\"mediaId\":\"" + Sha + "\",\"width\":800,\"height\":600}")!;

        Assert.Equal(0, back.Scale);
        Assert.Equal(MediaRef.MaxScale, back.ScaleOrFull);   // all'editore si mostra 100, non 0
    }

    [Fact]
    public void L_url_pubblico_e_la_rotta_dello_sha()
    {
        Assert.Equal("/vsop/media/" + Sha, new MediaRef(Sha).Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{")]                       // JSON troncato
    [InlineData("{\"alt\":\"solo testo\"}")] // riferimento senza sha
    [InlineData("{\"mediaId\":\"  \"}")]
    public void Senza_sha_valido_e_come_se_non_ci_fosse_immagine(string? json)
    {
        Assert.Null(MediaRef.Parse(json));
    }

    [Fact]
    public void Il_testo_indicizzabile_e_alt_piu_didascalia_mai_il_json()
    {
        var json = MediaRef.Serialize(new MediaRef(Sha, "Torre di Fiumicino", 800, 600));

        var text = MediaRef.TextOf(json, "Vista da nord");

        Assert.Equal("Torre di Fiumicino Vista da nord", text);
        Assert.DoesNotContain(Sha, text);
    }

    // --- Blocchi delle sezioni extra: stesso formato, stesse regole ---

    [Fact]
    public void Un_blocco_immagine_sopravvive_al_giro_di_serializzazione_degli_extra()
    {
        var blocks = new List<ExtraBlock>
        {
            new() { Format = BlockFormat.Image, ImageJson = MediaRef.Serialize(new MediaRef(Sha, "Mappa", 1024, 768)), Text = "Didascalia" },
        };

        var back = ExtraBlocks.Parse(ExtraBlocks.Serialize(blocks));

        var blk = Assert.Single(back);
        Assert.Equal(BlockFormat.Image, blk.Format);
        Assert.Equal("Didascalia", blk.Text);
        Assert.Equal(Sha, MediaRef.Parse(blk.ImageJson)!.MediaId);
    }

    [Fact]
    public void Un_blocco_immagine_senza_riferimento_non_viene_salvato()
    {
        var blocks = new List<ExtraBlock> { new() { Format = BlockFormat.Image, Text = "didascalia orfana" } };

        Assert.Null(ExtraBlocks.Serialize(blocks));
    }

    [Fact]
    public void Una_foto_senza_didascalia_e_legittima()
    {
        var blocks = new List<ExtraBlock> { new() { Format = BlockFormat.Image, ImageJson = MediaRef.Serialize(new MediaRef(Sha)) } };

        Assert.NotNull(ExtraBlocks.Serialize(blocks));
    }

    [Fact]
    public void L_anteprima_degli_extra_mostra_alt_e_didascalia_non_il_json()
    {
        var body = ExtraBlocks.Serialize(new List<ExtraBlock>
        {
            new() { Format = BlockFormat.Prose, Text = "Introduzione" },
            new() { Format = BlockFormat.Image, ImageJson = MediaRef.Serialize(new MediaRef(Sha, "Piazzale")), Text = "Stand 401" },
        });

        var plain = ExtraBlocks.PlainText(body);

        Assert.Contains("Introduzione", plain);
        Assert.Contains("Piazzale", plain);
        Assert.Contains("Stand 401", plain);
        Assert.DoesNotContain("mediaId", plain);
    }
}
