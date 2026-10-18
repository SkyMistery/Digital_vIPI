using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le sezioni da scrivere di un documento: quali contano e quando una ha qualcosa. Carta
/// <c>docs/feature/2026-10-18-sezioni-da-scrivere.md</c>.
/// </summary>
public class SezioniDaScrivereTests
{
    private static SezioneDaContare S(int id, string chiave, string titolo, int? madre = null, bool nascosta = false,
        params BloccoDaContare[] blocchi) => new(id, madre, chiave, titolo, nascosta, blocchi);

    private static BloccoDaContare Testo(string t) => new(t, null);
    private static BloccoDaContare Json(string j) => new(null, j);

    /// <summary>Le sezioni da scrivere di ogni profilo, per chiave. ⚠️ È qui che si vede se il catalogo cambia.</summary>
    [Fact]
    public void Le_Sezioni_Da_Scrivere_Di_Ogni_Profilo()
    {
        string[] DelProfilo(SectionProfile p) => Appiattisci(SectionCatalog.For(p))
            .Where(k => SezioniDaScrivere.DaScrivere(p, k)).ToArray();

        Assert.Equal(new[] { "separations", "trafficmanagement:ifr", "vfr", "operatingtechnique", "regulated", "operationaltechnique" }
            .OrderBy(x => x), DelProfilo(SectionProfile.App).OrderBy(x => x));
        Assert.Equal(DelProfilo(SectionProfile.App).OrderBy(x => x), DelProfilo(SectionProfile.AccAppBlock).OrderBy(x => x));
        Assert.Equal(new[] { "operationaltechnique", "separations" }, DelProfilo(SectionProfile.AccAerovia).OrderBy(x => x));
        Assert.Equal(new[] { "operationaltechnique", "purpose", "regulated" }, DelProfilo(SectionProfile.Vloa).OrderBy(x => x));
        Assert.Equal(new[] { "charts:aerodrome", "charts:iac", "charts:sid", "charts:star", "charts:vfr", "operationaltechnique" },
            DelProfilo(SectionProfile.Airport).OrderBy(x => x));

        // Il militare: nessun contenitore, niente soglie, niente derivate.
        var mil = DelProfilo(SectionProfile.AirportMil);
        Assert.DoesNotContain("generaldata", mil);
        Assert.DoesNotContain("flightprocedures", mil);
        Assert.DoesNotContain(SectionKeys.RunwayThresholds, mil);
        Assert.DoesNotContain("navaids", mil);
        Assert.Contains("parkings", mil);
        Assert.Contains("vfrjet", mil);
        Assert.Contains(SectionKeys.DepartureProceduresVfr, mil);
    }

    /// <summary>
    /// 🔴 I due versi dei coordinamenti della vLOA stanno nel catalogo (sotto-sezioni fisse) senza una natura scritta,
    /// e per difetto passavano per editoriali: sulla copia di produzione del 12 ottobre 2026 ogni vLOA aveva «due
    /// sezioni vuote» che nessuno può scrivere.
    /// </summary>
    [Fact]
    public void I_Versi_Dei_Coordinamenti_Della_vLOA_Non_Contano()
    {
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.Vloa, SectionKeys.CoordinationOut));
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.Vloa, SectionKeys.CoordinationIn));
    }

    [Fact]
    public void Le_Sezioni_Libere_E_Le_Derivate_Non_Contano()
    {
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.App, SectionKeys.NewCustom()));
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.App, "aor"));
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.App, "validity"));
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.App, "configurations"));
    }

    [Fact]
    public void Conta_Scritte_E_Vuote_Col_Percorso()
    {
        var conto = SezioniDaScrivere.Conta(SectionProfile.App, new[]
        {
            S(1, "separations", "Separazioni", blocchi: Json("[{\"Type\":\"Radar\",\"Min\":\"3 NM\"}]")),
            S(2, SectionKeys.TrafficManagement, "Gestione del traffico"),
            S(3, SectionKeys.TrafficManagementIfr, "IFR", 2, blocchi: Testo("Le partenze da LIRF…")),
            S(4, "vfr", "VFR", 2),
            S(5, "aor", "AOR", blocchi: Json("{\"x\":1}")),
            S(6, "operationaltechnique", "Procedure generali", blocchi: Testo("   ")),
        });

        Assert.Equal(2, conto.Scritte);
        Assert.Equal(new[] { "Gestione del traffico › VFR", "Procedure generali" }, conto.Vuote.Select(v => v.Percorso));
        Assert.Equal(4, conto.Totale);
    }

    [Fact]
    public void Le_Nascoste_E_Le_Figlie_Di_Una_Nascosta_Non_Contano()
    {
        var conto = SezioniDaScrivere.Conta(SectionProfile.App, new[]
        {
            S(2, SectionKeys.TrafficManagement, "Gestione del traffico", nascosta: true),
            S(3, SectionKeys.TrafficManagementIfr, "IFR", 2),
            S(4, "vfr", "VFR", 2),
            S(6, "operationaltechnique", "Procedure generali", nascosta: true),
            S(7, "regulated", "Aree regolamentate"),
        });

        Assert.Equal(0, conto.Scritte);
        Assert.Equal("Aree regolamentate", Assert.Single(conto.Vuote).Percorso);
    }

    /// <summary>Le aree del blocco Aerovia nascono in automatico: non sono da scrivere. Nel gruppo APP sì.</summary>
    [Fact]
    public void Le_Aree_Regolamentate_Del_Blocco_Aerovia_Non_Contano()
    {
        Assert.False(SezioniDaScrivere.DaScrivere(SectionProfile.AccAerovia, "regulated"));
        Assert.True(SezioniDaScrivere.DaScrivere(SectionProfile.AccAppBlock, "regulated"));
    }

    [Theory]
    // Vuoti: niente, segnaposto, intestazioni senza righe, righe bianche, variant da sola, impostazioni in cima.
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("[]", false)]
    [InlineData("{}", false)]
    [InlineData("{\"columns\":[\"Settore\",\"Note\"],\"rows\":[]}", false)]
    [InlineData("{\"columns\":[\"Settore\",\"Note\"],\"rows\":[[\"\",\" \"]]}", false)]
    [InlineData("{\"variant\":\"callsigns\",\"rows\":[]}", false)]
    [InlineData("{\"OwnAuto\":false,\"OwnIds\":[],\"ExtraIds\":[],\"activities\":{},\"notes\":{}}", false)]
    [InlineData("{\"Order\":3}", false)]
    // Pieni: una riga, un'area scelta (forma storica e nuova), un'immagine, un allegato, il titolo di una callout.
    [InlineData("{\"columns\":[\"Settore\"],\"rows\":[[\"LIMM_WS2_CTR\"]]}", true)]
    [InlineData("[\"1029\"]", true)]
    [InlineData("[1029]", true)]
    [InlineData("{\"OwnAuto\":false,\"OwnIds\":[\"1029\"],\"ExtraIds\":[]}", true)]
    [InlineData("{\"variant\":\"callsigns\",\"rows\":[{\"Callsign\":\"\",\"Note\":\"Lupo\"}]}", true)]
    [InlineData("{\"mediaId\":\"a1b2\"}", true)]
    [InlineData("{\"ref\":\"att:42\"}", true)]
    [InlineData("{\"title\":\"Reduced coordination\"}", true)]
    // Illeggibile: qualcuno ci ha scritto, e dirla vuota la farebbe riscrivere.
    [InlineData("{rotto", true)]
    public void Il_Json_Di_Un_Blocco(string? json, bool atteso) =>
        Assert.Equal(atteso, SezioniDaScrivere.JsonHaContenuto(json));

    [Fact]
    public void Un_Blocco_Riusato_Ha_Contenuto()
    {
        Assert.True(SezioniDaScrivere.HaContenuto(new BloccoDaContare(null, null, Riusato: true)));
        Assert.False(SezioniDaScrivere.HaContenuto(new BloccoDaContare("\n  \t", null)));
    }

    private static IEnumerable<string> Appiattisci(IEnumerable<SectionDescriptor> descrittori)
    {
        foreach (var d in descrittori)
        {
            yield return d.Key;
            if (d.Children is { } figli)
                foreach (var k in Appiattisci(figli)) yield return k;
        }
    }
}
