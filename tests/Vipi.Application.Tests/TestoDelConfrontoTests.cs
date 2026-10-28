using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>S133, carta 2026-10-28-stato-dei-documenti §2.1: il testo di una sezione per il confronto, e le righe che
/// cambiano fra due testi.</summary>
public class TestoDelConfrontoTests
{
    private static RawBlock Blocco(int ordine, string? body = null, string? json = null) => new()
    {
        Id = ordine, Order = ordine, Format = json is null ? BlockFormat.Prose : BlockFormat.Table,
        Visibility = BlockVisibility.Always, Tier = BlockTier.Reduced, Body = body, BodyJson = json,
    };

    [Fact]
    public void La_prosa_da_una_riga_per_paragrafo_e_i_blocchi_vanno_in_ordine()
    {
        var righe = TestoDelConfronto.Righe(new[] { Blocco(2, "Terzo."), Blocco(1, "Primo.\n\nSecondo.") });

        Assert.Equal(new[] { "Primo.", "Secondo.", "Terzo." }, righe);
    }

    /// <summary>Una cella sola fuori dalla sua riga non dice niente: la tabella dà l'intestazione e una riga per fila.</summary>
    [Fact]
    public void Una_tabella_da_una_riga_per_fila_con_le_celle_in_fila()
    {
        const string json = """{"columns":["Pista","Vento"],"rows":[{"cells":["05","calmo"]},{"cells":["23",""]}]}""";

        var righe = TestoDelConfronto.Righe(new[] { Blocco(1, json: json) });

        Assert.Equal(new[] { "Pista · Vento", "05 · calmo", "23" }, righe);
    }

    [Fact]
    public void Un_json_che_non_e_una_tabella_da_una_riga_per_testo()
    {
        var righe = TestoDelConfronto.Righe(new[] { Blocco(1, json: """{"mediaId":7,"alt":"Carta","title":"Parcheggi"}""") });

        Assert.Equal(new[] { "Carta", "Parcheggi" }, righe.OrderBy(r => r));
    }

    [Fact]
    public void Un_json_illeggibile_non_da_righe_e_non_cade() =>
        Assert.Empty(TestoDelConfronto.Righe(new[] { Blocco(1, json: "{non json") }));

    [Fact]
    public void Testi_uguali_nessuna_differenza() =>
        Assert.Empty(TestoDelConfronto.Differenze(new[] { "a", "b" }, new[] { "a", "b" }));

    /// <summary>Le righe rimaste uguali non compaiono; tolte e aggiunte nell'ordine in cui si leggono.</summary>
    [Fact]
    public void Solo_le_righe_cambiate_nell_ordine_di_lettura()
    {
        var diff = TestoDelConfronto.Differenze(new[] { "a", "b", "c" }, new[] { "a", "B", "c", "d" });

        Assert.Equal(new[]
        {
            new TextDiffLine(TextDiffKind.Removed, "b"),
            new TextDiffLine(TextDiffKind.Added, "B"),
            new TextDiffLine(TextDiffKind.Added, "d"),
        }, diff);
    }

    [Fact]
    public void Da_vuoto_tutto_aggiunto_e_a_vuoto_tutto_tolto()
    {
        Assert.All(TestoDelConfronto.Differenze(Array.Empty<string>(), new[] { "x", "y" }),
            d => Assert.Equal(TextDiffKind.Added, d.Kind));
        Assert.All(TestoDelConfronto.Differenze(new[] { "x", "y" }, Array.Empty<string>()),
            d => Assert.Equal(TextDiffKind.Removed, d.Kind));
    }
}
