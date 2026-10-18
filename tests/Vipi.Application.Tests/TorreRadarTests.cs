using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La torre radar (18 ottobre 2026, committente): una sezione che dice se la torre è dotata di radar e, solo
/// se lo è, una sotto-sezione con la tecnica operativa dello scalo. Il dato sta nell'anagrafica, perché la torre
/// di un campo civile e militare è sempre una sola.
/// </summary>
public class TorreRadarTests
{
    private static SectionView Sez(string chiave, params SectionView[] figli) => new()
    {
        Id = chiave, Title = chiave, Depth = 0, SectionKey = chiave,
        Blocks = Array.Empty<BlockView>(), Children = figli,
    };

    private static string[] Chiavi(IEnumerable<SectionView> s) =>
        s.SelectMany(x => new[] { x.SectionKey }.Concat(Chiavi(x.Children))).ToArray();

    private static readonly AirportRadarTowerView ConRadar = new(true, "Identificazione radar entro 5 NM.");
    private static readonly AirportRadarTowerView SenzaRadar = new(false, "testo rimasto da un sì di prima");

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
    public void Ha_una_sola_figlia_la_tecnica_e_tutt_e_due_le_disegna_la_pagina(SectionProfile profilo)
    {
        var torre = SectionCatalog.Find(profilo, SectionKeys.RadarTower)!;

        Assert.Equal(new[] { SectionKeys.RadarTowerTechnique }, torre.Children!.Select(d => d.Key));
        Assert.True(SectionCatalog.IsHostRendered(profilo, SectionKeys.RadarTower));
        Assert.True(SectionCatalog.IsHostRendered(profilo, SectionKeys.RadarTowerTechnique));
        Assert.False(SectionCatalog.KeepsOwnBlocks(profilo, SectionKeys.RadarTower));
        Assert.True(SectionCatalog.IsFixed(profilo, SectionKeys.RadarTowerTechnique));
    }

    /// <summary>
    /// Il padre è derivato (si congela alla release), la figlia no: la rende la vista del padre, come le soglie
    /// delle piste. Due interruttori Live/Frozen metterebbero il sì/no e la tecnica su due cicli diversi.
    /// </summary>
    [Fact]
    public void Si_congela_una_volta_sola_sotto_il_padre()
    {
        Assert.Equal(SectionKind.Derived, SectionCatalog.KindOf(SectionKeys.RadarTower));
        Assert.True(SectionCatalog.IsRenderModeToggleable(SectionKeys.RadarTower));
        Assert.False(SectionCatalog.IsRenderModeToggleable(SectionKeys.RadarTowerTechnique));
    }

    /// <summary>La tecnica nasce chiusa (committente): è prosa lunga in mezzo all'indice, e il sì/no no.</summary>
    [Fact]
    public void La_tecnica_nasce_chiusa_e_la_torre_no()
    {
        Assert.True(SectionCatalog.IsInitiallyCollapsed(SectionKeys.RadarTowerTechnique));
        Assert.False(SectionCatalog.IsInitiallyCollapsed(SectionKeys.RadarTower));
    }

    /// <summary>
    /// Il testo sta nell'anagrafica e lo disegna la pagina: contare la tecnica fra le sezioni «da scrivere»
    /// chiederebbe blocchi che nessuno mostrerebbe.
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
        Assert.False(AirportSectionProjection.RadarTower(Scalo(new RadarTowerRow(false, null))).Equipped);
    }

    [Fact]
    public void La_tecnica_fatta_di_soli_spazi_e_una_tecnica_non_scritta()
    {
        var vista = AirportSectionProjection.RadarTower(Scalo(new RadarTowerRow(true, "  \n ")));

        Assert.True(vista.Equipped);
        Assert.Null(vista.Technique);
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

    [Fact]
    public void Un_documento_senza_la_tecnica_torna_com_e()
    {
        var sezioni = new[] { Sez("weather"), Sez("runways") };

        Assert.Same(sezioni, TorreRadar.Filtra(sezioni, SenzaRadar));
    }
}
