using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La torre radar (18 ottobre 2026, committente): una sezione che dice se la torre è dotata di radar e, solo
/// se lo è, una sotto-sezione con la tecnica operativa dello scalo. Il sì/no sta nell'anagrafica, perché la torre
/// di un campo civile e militare è sempre una sola; la tecnica è fatta di blocchi dal 21 ottobre 2026.
/// </summary>
public class TorreRadarTests
{
    private static SectionView Sez(string chiave, params SectionView[] figli) => Sez(chiave, false, figli);

    private static SectionView Sez(string chiave, bool nascosta, params SectionView[] figli) => new()
    {
        Id = chiave, Title = chiave, Depth = 0, SectionKey = chiave, IsHidden = nascosta,
        Blocks = Array.Empty<BlockView>(), Children = figli,
    };

    private static string[] Chiavi(IEnumerable<SectionView> s) =>
        s.SelectMany(x => new[] { x.SectionKey }.Concat(Chiavi(x.Children))).ToArray();

    private static readonly AirportRadarTowerView ConRadar = new(true);
    private static readonly AirportRadarTowerView SenzaRadar = new(false);

    /// <summary>Una release scattata prima del 21 ottobre 2026: la tecnica era un testo congelato nella vista.</summary>
    private static readonly AirportRadarTowerView ConRadarDiPrima = new(true, "Identificazione radar entro 5 NM.\nSeconda riga.");

    private static BlockView Blocco(string? body, string? json = null) => new()
    {
        Id = 7, Order = 1, Format = json is null ? BlockFormat.Prose : BlockFormat.Table, State = RenderState.Expanded,
        Body = body, BodyJson = json,
    };

    private static SectionView Tecnica(params BlockView[] blocchi) => new()
    {
        Id = "s-9", Title = "Tecnica operativa", Depth = 1, SectionKey = SectionKeys.RadarTowerTechnique,
        Blocks = blocchi, Children = Array.Empty<SectionView>(),
    };

    // ---- Il catalogo -------------------------------------------------------------------------------

    /// <summary>Fra Piste e SID nella vIPI: la tecnica si sceglie con la pista in uso, la SID dopo.</summary>
    [Fact]
    public void Nella_vIPI_sta_fra_Piste_e_SID()
    {
        var radici = SectionCatalog.For(SectionProfile.Airport).OrderBy(d => d.Order).Select(d => d.Key).ToList();
        var torre = radici.IndexOf(SectionKeys.RadarTower);

        Assert.Equal("runways", radici[torre - 1]);
        Assert.Equal("sids", radici[torre + 1]);
    }

    /// <summary>Nel vSOP nello stesso punto logico: dentro i Dati generali, dopo piste e regole, prima delle SID.</summary>
    [Fact]
    public void Nel_vSOP_sta_nei_dati_generali_dopo_le_regole_e_prima_delle_SID()
    {
        var generali = SectionCatalog.For(SectionProfile.AirportMil).Single(d => d.Key == "generaldata")
            .Children!.OrderBy(d => d.Order).Select(d => d.Key).ToList();
        var torre = generali.IndexOf(SectionKeys.RadarTower);

        Assert.Equal("runwayrules", generali[torre - 1]);
        Assert.Equal("sids", generali[torre + 1]);
    }

    /// <summary>
    /// ⚠️ La STESSA chiave e la stessa forma nei due profili: è ciò che la fa trovare come sezione unica in un'unione
    /// vIPI + vSOP, e ciò che la fa congelare dallo stesso provider registrato due volte.
    /// </summary>
    [Theory]
    [InlineData(SectionProfile.Airport)]
    [InlineData(SectionProfile.AirportMil)]
    public void Ha_una_sola_figlia_la_tecnica_e_la_tecnica_e_fatta_di_blocchi(SectionProfile profilo)
    {
        var torre = SectionCatalog.Find(profilo, SectionKeys.RadarTower)!;

        Assert.Equal(new[] { SectionKeys.RadarTowerTechnique }, torre.Children!.Select(d => d.Key));
        Assert.True(SectionCatalog.IsHostRendered(profilo, SectionKeys.RadarTower));
        // Committente, 21 ottobre 2026: «non ci deve essere un paragrafo fisso», solo paragrafi, callout… come nel resto.
        Assert.False(SectionCatalog.IsHostRendered(profilo, SectionKeys.RadarTowerTechnique));
        Assert.Equal(SectionBodySource.Blocks, SectionCatalog.Find(profilo, SectionKeys.RadarTowerTechnique)!.BodySource);
        Assert.False(SectionCatalog.KeepsOwnBlocks(profilo, SectionKeys.RadarTower));
        Assert.True(SectionCatalog.IsFixed(profilo, SectionKeys.RadarTowerTechnique));
    }

    /// <summary>
    /// Il padre è derivato (si congela alla release), la figlia è editoriale: i suoi blocchi li fotografa la
    /// release col documento, e un interruttore Live/Frozen non ha niente da scegliere.
    /// </summary>
    [Fact]
    public void Si_congela_una_volta_sola_sotto_il_padre()
    {
        Assert.Equal(SectionKind.Derived, SectionCatalog.KindOf(SectionKeys.RadarTower));
        Assert.True(SectionCatalog.IsRenderModeToggleable(SectionKeys.RadarTower));
        Assert.False(SectionCatalog.IsRenderModeToggleable(SectionKeys.RadarTowerTechnique));
    }

    /// <summary>
    /// Nasce NASCOSTA (committente: «poi le mostriamo man mano che le sistemiamo»), come le STAR. Basta il padre: la
    /// tecnica sparisce con lui, e nascosta anche lei andrebbe rimostrata due volte.
    /// </summary>
    [Theory]
    [InlineData(SectionProfile.Airport)]
    [InlineData(SectionProfile.AirportMil)]
    public void La_torre_nasce_nascosta_e_la_tecnica_la_segue(SectionProfile profilo)
    {
        Assert.True(SectionCatalog.Find(profilo, SectionKeys.RadarTower)!.BornHidden);
        Assert.False(SectionCatalog.Find(profilo, SectionKeys.RadarTowerTechnique)!.BornHidden);
    }

    /// <summary>La tecnica nasce chiusa (committente): è prosa lunga in mezzo all'indice, e il sì/no no.</summary>
    [Fact]
    public void La_tecnica_nasce_chiusa_e_la_torre_no()
    {
        Assert.True(SectionCatalog.IsInitiallyCollapsed(SectionKeys.RadarTowerTechnique));
        Assert.False(SectionCatalog.IsInitiallyCollapsed(SectionKeys.RadarTower));
    }

    /// <summary>
    /// La tecnica c'è solo con la torre radar, e il conto delle sezioni «da scrivere» non sa il sì/no: contarla
    /// chiederebbe, sugli scali senza radar, blocchi che il documento non mostra.
    /// </summary>
    [Theory]
    [InlineData(SectionProfile.Airport)]
    [InlineData(SectionProfile.AirportMil)]
    public void La_tecnica_non_e_una_sezione_da_scrivere(SectionProfile profilo)
    {
        Assert.False(SezioniDaScrivere.DaScrivere(profilo, SectionKeys.RadarTowerTechnique));
        Assert.False(SezioniDaScrivere.DaScrivere(profilo, SectionKeys.RadarTower));
    }

    // ---- La proiezione dall'anagrafica ---------------------------------------------------------------

    private static AirportData Scalo(RadarTowerRow? torre) => new()
    {
        AirportId = 1, Icao = "LIPZ", Name = "Venezia Tessera", AccCode = "LIPP",
        TransitionLevels = Array.Empty<TlRow>(), Runways = Array.Empty<RunwayRow>(),
        Rules = Array.Empty<RunwayRuleRow>(), Sids = Array.Empty<SidRow>(),
        Links = Array.Empty<FrequencyLinkRow>(), RadarTower = torre,
    };

    /// <summary>«Non indicato» non è «senza radar»: senza anagrafica, o senza un sì/no, la vista lo dice.</summary>
    [Fact]
    public void Senza_un_si_o_un_no_la_torre_e_NON_INDICATA()
    {
        Assert.Null(AirportSectionProjection.RadarTower(null).Equipped);
        Assert.Null(AirportSectionProjection.RadarTower(Scalo(null)).Equipped);
        Assert.False(AirportSectionProjection.RadarTower(Scalo(new RadarTowerRow(false))).Equipped);
    }

    /// <summary>Dal 21 ottobre 2026 la vista che la release congela porta il solo sì/no: la tecnica sono blocchi.</summary>
    [Fact]
    public void La_vista_nuova_non_porta_la_tecnica()
    {
        var vista = AirportSectionProjection.RadarTower(Scalo(new RadarTowerRow(true)));

        Assert.True(vista.Equipped);
        Assert.Null(vista.Technique);
    }

    // ---- Le release di prima del 21 ottobre 2026: la tecnica congelata diventa un paragrafo -----------------

    /// <summary>
    /// In quelle release la sezione ha solo il segnaposto delle sezioni rese dalla pagina (né testo né JSON), e il
    /// testo sta nella vista: senza questo passo la pagina pubblica lo perderebbe fino alla pubblicazione seguente.
    /// </summary>
    [Fact]
    public void In_una_release_di_prima_la_tecnica_congelata_diventa_un_paragrafo()
    {
        var sezioni = new[] { Sez(SectionKeys.RadarTower), Sez("runways") };
        sezioni[0] = new SectionView
        {
            Id = sezioni[0].Id, Title = sezioni[0].Title, Depth = 0, SectionKey = SectionKeys.RadarTower,
            Blocks = Array.Empty<BlockView>(), Children = new[] { Tecnica(Blocco(null, "")) },
        };

        var tecnica = TorreRadar.Filtra(sezioni, ConRadarDiPrima)[0].Children.Single();

        var paragrafo = Assert.Single(tecnica.Blocks);
        Assert.Equal(BlockFormat.Prose, paragrafo.Format);
        Assert.Equal("Identificazione radar entro 5 NM.\nSeconda riga.", paragrafo.Body);
        Assert.Equal("s-9", tecnica.Id);
    }

    /// <summary>Una sezione coi suoi blocchi scritti è del documento: il testo vecchio non ci si aggiunge.</summary>
    [Fact]
    public void Una_tecnica_coi_blocchi_scritti_non_riceve_il_testo_di_prima()
    {
        var scritta = Tecnica(Blocco("Il testo nuovo."));
        var sezioni = new[] { Sez(SectionKeys.RadarTower, scritta) };

        Assert.Same(scritta, TorreRadar.Filtra(sezioni, ConRadarDiPrima)[0].Children.Single());
    }

    /// <summary>Senza radar il testo di prima non fa tornare la sezione: il filtro la toglie come sempre.</summary>
    [Fact]
    public void Senza_radar_il_testo_di_prima_non_riporta_la_tecnica()
    {
        var sezioni = new[] { Sez(SectionKeys.RadarTower, Tecnica()) };

        Assert.Equal(new[] { SectionKeys.RadarTower },
                     Chiavi(TorreRadar.Filtra(sezioni, new AirportRadarTowerView(false, "testo di prima"))));
    }

    // ---- Il filtro: la tecnica c'è solo con la torre radar --------------------------------------------

    [Fact]
    public void Con_la_torre_radar_la_tecnica_resta_e_la_lista_e_la_stessa()
    {
        var sezioni = new[] { Sez("runways"), Sez(SectionKeys.RadarTower, Sez(SectionKeys.RadarTowerTechnique)), Sez("sids") };

        Assert.Same(sezioni, TorreRadar.Filtra(sezioni, ConRadar));
    }

    /// <summary>
    /// Senza radar la tecnica sparisce dalla LISTA — e quindi anche dall'indice, che si rende dalla stessa lista.
    /// Il resto resta, sotto-sezioni libere della torre comprese.
    /// </summary>
    [Fact]
    public void Senza_radar_la_tecnica_sparisce_e_il_resto_resta()
    {
        var sezioni = new[]
        {
            Sez("runways"),
            Sez(SectionKeys.RadarTower, Sez(SectionKeys.RadarTowerTechnique), Sez("custom:1a2b3c4d")),
            Sez("sids"),
        };

        var mostrate = TorreRadar.Filtra(sezioni, SenzaRadar);

        Assert.Equal(new[] { "runways", SectionKeys.RadarTower, "custom:1a2b3c4d", "sids" }, Chiavi(mostrate));
    }

    /// <summary>La tecnica di una torre di cui non si sa se ha il radar risponderebbe a una domanda non fatta.</summary>
    [Fact]
    public void Con_la_torre_non_indicata_la_tecnica_non_c_e()
    {
        var sezioni = new[] { Sez(SectionKeys.RadarTower, Sez(SectionKeys.RadarTowerTechnique)) };

        Assert.Equal(new[] { SectionKeys.RadarTower }, Chiavi(TorreRadar.Filtra(sezioni, AirportRadarTowerView.Empty)));
    }

    /// <summary>Nel vSOP la torre è figlia dei Dati generali: il filtro deve scendere fino a lì.</summary>
    [Fact]
    public void Nel_vSOP_il_filtro_scende_dentro_i_dati_generali()
    {
        var sezioni = new[]
        {
            Sez("generaldata", Sez("runways"), Sez(SectionKeys.RadarTower, Sez(SectionKeys.RadarTowerTechnique)), Sez("sids")),
            Sez("lvp"),
        };

        Assert.Equal(new[] { "generaldata", "runways", SectionKeys.RadarTower, "sids", "lvp" },
                     Chiavi(TorreRadar.Filtra(sezioni, SenzaRadar)));
    }

    // ---- La marca nella vista live (committente, 18 ottobre 2026) ---------------------------------------------

    /// <summary>«Radar di torre», non «Torre radar» (committente): il soggetto è il radar.</summary>
    [Theory]
    [InlineData(SectionProfile.Airport)]
    [InlineData(SectionProfile.AirportMil)]
    public void Si_chiama_radar_di_torre(SectionProfile profilo)
    {
        var d = SectionCatalog.Find(profilo, SectionKeys.RadarTower)!;
        Assert.Equal("Radar di torre", d.Title);
        Assert.Equal("Tower radar", d.TitleIn("en"));
    }

    [Fact]
    public void La_marca_porta_alla_sezione_se_la_torre_ha_il_radar_e_la_sezione_si_vede()
    {
        var sezioni = new[] { Sez("runways"), Sez(SectionKeys.RadarTower, Sez(SectionKeys.RadarTowerTechnique)) };

        Assert.Equal(SectionKeys.RadarTower, TorreRadar.Ancora(sezioni, ConRadar));
        Assert.Null(TorreRadar.Ancora(sezioni, SenzaRadar));
        Assert.Null(TorreRadar.Ancora(sezioni, AirportRadarTowerView.Empty));
    }

    /// <summary>
    /// La sezione nasce nascosta e si mostra scalo per scalo: finché è nascosta la marca non c'è, o porterebbe in
    /// cima a una pagina che non la contiene. Nel vSOP vale anche se è nascosta la madre (i Dati generali).
    /// </summary>
    [Fact]
    public void Con_la_sezione_nascosta_o_sotto_una_madre_nascosta_la_marca_non_c_e()
    {
        Assert.Null(TorreRadar.Ancora(new[] { Sez(SectionKeys.RadarTower, nascosta: true) }, ConRadar));
        Assert.Null(TorreRadar.Ancora(new[] { Sez("generaldata", nascosta: true, Sez(SectionKeys.RadarTower)) }, ConRadar));
        Assert.Equal(SectionKeys.RadarTower,
                     TorreRadar.Ancora(new[] { Sez("generaldata", Sez("runways"), Sez(SectionKeys.RadarTower)) }, ConRadar));
    }

    [Fact]
    public void Un_documento_senza_la_tecnica_torna_com_e()
    {
        var sezioni = new[] { Sez("weather"), Sez("runways") };

        Assert.Same(sezioni, TorreRadar.Filtra(sezioni, SenzaRadar));
    }
}
