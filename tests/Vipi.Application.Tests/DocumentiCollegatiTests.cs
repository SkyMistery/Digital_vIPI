using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le regole dei documenti collegati (§A109, carta <c>docs/feature/2026-09-21-documenti-collegati.md</c>). La
/// struttura qui sotto ricalca quella vera del 21 settembre 2026: LIBN sotto LIBN_G_APP sotto LIBN_APP (nessuno dei
/// due col documento), LIRN sotto l'APP remotizzato LIRN_US0_APP, LIBV con vIPI, vSOP e APP.
/// </summary>
public class DocumentiCollegatiTests
{
    private static ManagedDoc Doc(ReleaseTargetType t, string key, string acc, int id, string? vicino = null) =>
        new(t, $"doc {id}", key, acc, true, false, false, t, key, id, vicino, EffectiveCycle: "2610");

    private static readonly Dictionary<string, string?> Padri = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = null, ["LIRR_TS_CTR"] = null,
        ["LIBN_TWR"] = "LIBN_G_APP", ["LIBN_G_APP"] = "LIBN_APP", ["LIBN_APP"] = "LIBB_ES_CTR",
        ["LIBV_TWR"] = "LIBV_G_APP", ["LIBV_G_APP"] = "LIBV_APP", ["LIBV_APP"] = "LIBB_ES_CTR",
        ["LIRN_GND"] = "LIRN_TWR", ["LIRN_TWR"] = "LIRN_US0_APP", ["LIRN_US0_APP"] = "LIRR_TS_CTR",
    };

    private static DocLinkGraph Grafo(Dictionary<string, DocLinkApp>? app = null, params ManagedDoc[] extra) => new(
        Padri,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["LIBB_ES_CTR"] = "LIBB", ["LIRR_TS_CTR"] = "LIRR" },
        new[]
        {
            new DocLinkAirport("LIBN", "LIBB", false, "LIBN_G_APP", new[] { "LIBN_TWR", "LIBN_G_APP", "LIBN_APP" }),
            new DocLinkAirport("LIBV", "LIBB", false, "LIBV_G_APP", new[] { "LIBV_TWR", "LIBV_G_APP", "LIBV_APP" }),
            new DocLinkAirport("LIRN", "LIRR", false, null, new[] { "LIRN_GND", "LIRN_TWR", "LIRN_US0_APP" }),
            new DocLinkAirport("LIBZ", "LIBB", true, "LIBB_ES_CTR", Array.Empty<string>()),
        },
        app ?? new Dictionary<string, DocLinkApp>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIBN_APP"] = new("LIBN_APP", false, null, "LIBB"),
            ["LIBN_G_APP"] = new("LIBN_G_APP", false, null, "LIBB"),
            ["LIBV_APP"] = new("LIBV_APP", false, 35, "LIBB"),
            ["LIBV_G_APP"] = new("LIBV_G_APP", false, null, "LIBB"),
            ["LIRN_US0_APP"] = new("LIRN_US0_APP", true, null, "LIRR"),
        },
        new[]
        {
            Doc(ReleaseTargetType.AccVipi, "LIBB|LIBB_CTR", "LIBB", 1),
            Doc(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", "LIRR", 16),
            Doc(ReleaseTargetType.AirportMil, "LIBN", "LIBB", 27),
            Doc(ReleaseTargetType.Airport, "LIBV", "LIBB", 34),
            Doc(ReleaseTargetType.AirportMil, "LIBV", "LIBB", 33),
            Doc(ReleaseTargetType.App, "LIBV_APP", "LIBB", 35),
            Doc(ReleaseTargetType.Airport, "LIRN", "LIRR", 7),
            Doc(ReleaseTargetType.Airport, "LIBZ", "LIBB", 50),
            Doc(ReleaseTargetType.Vloa, "8", "LIBB", 8, "LGGG"),
        }.Concat(extra).ToList());

    private static List<string> Etichette(DocLinkGraph g, ReleaseTargetType t, string key, Func<DocLinkTarget, bool>? vis = null) =>
        DocumentiCollegati.Resolve(DocumentiCollegati.Capture(g, t, key), vis ?? (_ => true))
            .Select(x => $"{x.Group}:{x.Target.Label}").ToList();

    [Fact]
    public void Scalo_sotto_APP_senza_documento_sale_fino_all_ACC()
    {
        Assert.Equal(new[] { "Acc:LIBB vIPI" }, Etichette(Grafo(), ReleaseTargetType.AirportMil, "LIBN"));
    }

    [Fact]
    public void Se_il_padre_non_ha_documento_si_sale_all_APP_di_sopra()
    {
        // LIBN_G_APP (padre di LIBN) non ha documento: l'APP di LIBN è LIBN_APP — l'esempio del committente.
        var app = new Dictionary<string, DocLinkApp>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIBN_APP"] = new("LIBN_APP", false, 60, "LIBB"),
            ["LIBN_G_APP"] = new("LIBN_G_APP", false, null, "LIBB"),
        };
        var g = Grafo(app, Doc(ReleaseTargetType.App, "LIBN_APP", "LIBB", 60));

        Assert.Equal(new[] { "Acc:LIBB vIPI", "App:LIBN_APP" }, Etichette(g, ReleaseTargetType.AirportMil, "LIBN"));
    }

    [Fact]
    public void APP_piu_vicino_non_pubblico_cede_il_posto_al_disegno()
    {
        var app = new Dictionary<string, DocLinkApp>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIBN_APP"] = new("LIBN_APP", false, 60, "LIBB"),
            ["LIBN_G_APP"] = new("LIBN_G_APP", false, 61, "LIBB"),
        };
        var g = Grafo(app, Doc(ReleaseTargetType.App, "LIBN_APP", "LIBB", 60), Doc(ReleaseTargetType.App, "LIBN_G_APP", "LIBB", 61));

        Assert.Contains("App:LIBN_G_APP", Etichette(g, ReleaseTargetType.AirportMil, "LIBN"));
        // Il documento di LIBN_G_APP è in bozza: la stessa fotografia, disegnata oggi, porta a LIBN_APP.
        Assert.Equal(new[] { "Acc:LIBB vIPI", "App:LIBN_APP" },
            Etichette(g, ReleaseTargetType.AirportMil, "LIBN", t => t.DocumentId != 61));
    }

    [Fact]
    public void Scalo_sotto_APP_remotizzato_ha_la_testa_e_la_sezione_dell_APP()
    {
        var snap = DocumentiCollegati.Capture(Grafo(), ReleaseTargetType.Airport, "LIRN");
        var link = DocumentiCollegati.Resolve(snap, _ => true);

        Assert.Equal(new[] { "LIRR vIPI", "LIRR vIPI · LIRN_US0_APP" }, link.Select(l => l.Target.Label));
        Assert.Null(link[0].Target.Anchor);
        Assert.Equal("app-LIRN_US0_APP", link[1].Target.Anchor);
        Assert.Equal(16, link[1].Target.DocumentId);
    }

    [Fact]
    public void Vipi_di_scalo_ACC_poi_APP_poi_altra_edizione()
    {
        Assert.Equal(new[] { "Acc:LIBB vIPI", "App:LIBV_APP", "Airport:LIBV vSOP" },
            Etichette(Grafo(), ReleaseTargetType.Airport, "LIBV"));
        Assert.Equal(new[] { "Acc:LIBB vIPI", "App:LIBV_APP", "Airport:LIBV vIPI" },
            Etichette(Grafo(), ReleaseTargetType.AirportMil, "LIBV"));
    }

    [Fact]
    public void APP_porta_all_ACC_e_a_vIPI_e_vSOP_dei_suoi_scali()
    {
        Assert.Equal(new[] { "Acc:LIBB vIPI", "Airport:LIBV vIPI", "Airport:LIBV vSOP" },
            Etichette(Grafo(), ReleaseTargetType.App, "LIBV_APP"));
    }

    [Fact]
    public void ACC_elenca_APP_e_tutti_gli_scali_con_vIPI_prima_del_vSOP()
    {
        Assert.Equal(new[] { "App:LIBV_APP", "Airport:LIBN vSOP", "Airport:LIBV vIPI" },
            Etichette(Grafo(), ReleaseTargetType.AccVipi, "LIBB|LIBB_CTR"));
        // vIPI di LIBV non pubblica adesso: al suo posto il vSOP. LIBZ è nascosto e non compare mai.
        Assert.Equal(new[] { "App:LIBV_APP", "Airport:LIBN vSOP", "Airport:LIBV vSOP" },
            Etichette(Grafo(), ReleaseTargetType.AccVipi, "LIBB|LIBB_CTR", t => t.DocumentId != 34));
        Assert.Equal(new[] { "Airport:LIRN vIPI" }, Etichette(Grafo(), ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR"));
    }

    [Fact]
    public void Nessun_documento_pubblico_nessun_link()
    {
        Assert.Empty(Etichette(Grafo(), ReleaseTargetType.Airport, "LIBV", _ => false));
    }

    [Fact]
    public void vLOA_porta_solo_alla_vIPI_degli_ACC_che_ne_hanno_una()
    {
        Assert.Equal(new[] { "Acc:LIBB vIPI" }, Etichette(Grafo(), ReleaseTargetType.Vloa, "8"));
    }

    [Fact]
    public void Un_anello_nell_albero_non_blocca()
    {
        var g = Grafo() with
        {
            ParentOf = new Dictionary<string, string?>(Padri, StringComparer.OrdinalIgnoreCase)
            {
                ["LIBV_APP"] = "LIBV_TWR",
            },
        };
        // La catena si tronca: niente ACC, e l'ACC d'anagrafica fa da ripiego.
        Assert.Equal(new[] { "Acc:LIBB vIPI", "App:LIBV_APP", "Airport:LIBV vSOP" },
            Etichette(g, ReleaseTargetType.Airport, "LIBV"));
    }
}
