using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Application.Media;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Lo scanner decide che cosa si può cancellare, quindi i suoi due errori non pesano uguale: riconoscere di più
/// lascia in vita un'immagine inutile (spazio), riconoscere di meno ne cancella una ancora in uso (documento
/// pubblicato rotto, in silenzio). Questi test fissano il lato prudente.
/// </summary>
public class MediaReferenceScannerTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Altro = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    [Fact]
    public void Trova_lo_sha_nel_json_di_un_blocco()
    {
        var json = MediaRef.Serialize(new MediaRef(Sha, "Torre", 800, 600));

        Assert.Equal(new[] { Sha }, MediaReferenceScanner.Scan(json));
    }

    [Fact]
    public void Trova_lo_sha_anche_dentro_il_json_escapato_di_una_release()
    {
        // È la forma reale del payload: il BodyJson del blocco è una STRINGA dentro un altro JSON.
        var payload = $"{{\"Doc\":{{\"Roots\":[{{\"Blocks\":[{{\"BodyJson\":\"{{\\\"mediaId\\\":\\\"{Sha}\\\"}}\"}}]}}]}}}}";

        Assert.Contains(Sha, MediaReferenceScanner.Scan(payload));
    }

    [Fact]
    public void Trova_piu_sha_e_non_li_ripete()
    {
        var testo = $"prima {Sha} poi {Altro} e ancora {Sha}";

        Assert.Equal(new[] { Sha, Altro }, MediaReferenceScanner.Scan(testo));
    }

    [Fact]
    public void Il_maiuscolo_e_lo_stesso_sha()
    {
        Assert.Equal(new[] { Sha }, MediaReferenceScanner.Scan(Sha.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nessun riferimento qui")]
    [InlineData("0123456789abcdef")]                      // troppo corto per essere uno sha
    public void Dove_non_c_e_niente_non_trova_niente(string? testo)
    {
        Assert.Empty(MediaReferenceScanner.Scan(testo));
    }

    [Fact]
    public void Una_sequenza_piu_lunga_di_64_non_e_uno_sha()
    {
        // Il confine evita di ritagliare 64 caratteri dal mezzo di un'altra stringa esadecimale.
        Assert.Empty(MediaReferenceScanner.Scan(Sha + "abcdef"));
    }

    [Fact]
    public void Lo_sha_dentro_una_stringa_json_annidata_si_legge_intero()
    {
        // Come lo scrive davvero System.Text.Json: le virgolette della stringa annidata diventano una sequenza di
        // escape che finisce per 22, cioe' due cifre esadecimali attaccate allo sha da entrambe le parti. Senza
        // neutralizzare gli escape prima di cercare, il riferimento sfuggiva e la foto in uso finiva fra le orfane.
        var annidato = JsonSerializer.Serialize(new { ImageJson = MediaRef.Serialize(new MediaRef(Sha)) });

        Assert.DoesNotContain(Sha + "\"", annidato);          // lo sha NON e' delimitato da virgolette vere
        Assert.Contains(Sha, MediaReferenceScanner.Scan(annidato));
    }

    [Fact]
    public void Riconosce_il_riferimento_nel_corpo_serializzato_di_una_sezione_extra()
    {
        // La forma vera: blocchi degli extra d'aeroporto serializzati con System.Text.Json, ImageJson annidato.
        var body = ExtraBlocks.Serialize(new List<ExtraBlock>
        {
            new() { Format = Vipi.Domain.BlockFormat.Image, ImageJson = MediaRef.Serialize(new MediaRef(Sha)), Text = "didascalia" },
        });

        Assert.Contains(Sha, MediaReferenceScanner.Scan(body));
    }

    [Fact]
    public void ScanAll_unisce_le_sorgenti()
    {
        var trovati = MediaReferenceScanner.ScanAll(new[] { $"blocco {Sha}", null, $"release {Altro}" });

        Assert.Equal(2, trovati.Count);
        Assert.Contains(Sha, trovati);
        Assert.Contains(Altro, trovati);
    }
}
