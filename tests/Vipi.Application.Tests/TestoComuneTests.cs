using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il testo comune delle torri radar (S127, 22 ottobre 2026): due callout uguali per tutti gli scali con torre radar,
/// scritti una volta, in testa alla «Tecnica operativa» di ognuno. Congelato con la release (committente).
/// </summary>
public class TestoComuneTests
{
    private static ExtraBlock Callout(string testo, CalloutKind tipo = CalloutKind.Warning, string? allegati = null) =>
        new() { Format = BlockFormat.Callout, Text = testo, CalloutKind = tipo, CalloutJson = allegati };

    private static ExtraBlock Paragrafo(string? testo) => new() { Format = BlockFormat.Prose, Text = testo };

    private static readonly ExtraBlock[] DueCallout =
    {
        Callout("È fatto obbligo la visione del documento linkato qui sotto.", CalloutKind.Warning,
            """{"allegati":[{"ref":"allegato:linee-guida-radar-di-torre","titolo":"Linee Guida radar di torre"}]}"""),
        Callout("LA TORRE NON FORNISCE VETTORAMENTO", CalloutKind.Success),
    };

    private static SectionView Sez(string chiave, bool nascosta = false, IReadOnlyList<BlockView>? blocchi = null,
        params SectionView[] figli) => new()
    {
        Id = "s-" + chiave, Title = chiave, Depth = 0, SectionKey = chiave, IsHidden = nascosta,
        Blocks = blocchi ?? Array.Empty<BlockView>(), Children = figli,
    };

    private static BlockView DelloScalo(string testo) => new()
    {
        Id = 41, Order = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = testo,
    };

    private static AirportRadarTowerView ConRadar(IReadOnlyList<ExtraBlock> comune) =>
        new(true, null, TestoComune.Vista(comune));

    // ---- I blocchi -----------------------------------------------------------------------------------

    [Fact]
    public void I_blocchi_vuoti_non_entrano_e_il_callout_porta_i_suoi_allegati()
    {
        var vista = TestoComune.Vista(new[] { DueCallout[0], Paragrafo("  "), DueCallout[1] });

        Assert.Equal(2, vista.Count);
        Assert.Equal(BlockFormat.Callout, vista[0].Format);
        Assert.Contains("allegato:linee-guida-radar-di-torre", vista[0].BodyJson);
        Assert.Equal(CalloutKind.Success, vista[1].CalloutKind);
    }

    /// <summary>Id negativi: nessun blocco vero li ha, e i componenti li usano come chiave di resa.</summary>
    [Fact]
    public void Gli_id_sono_negativi_e_diversi()
    {
        var vista = TestoComune.Vista(DueCallout);

        Assert.All(vista, b => Assert.True(b.Id < 0));
        Assert.Equal(vista.Count, vista.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public void Si_salva_e_si_rilegge_uguale_e_vuoto_vuol_dire_che_non_c_e()
    {
        var json = TestoComune.Serialize(DueCallout);

        Assert.NotNull(json);
        Assert.Equal(TestoComune.Firma(TestoComune.Vista(DueCallout)), TestoComune.Firma(TestoComune.Vista(TestoComune.Parse(json))));
        Assert.Null(TestoComune.Serialize(new[] { Paragrafo("") }));
        Assert.Empty(TestoComune.Parse(null));
        Assert.Empty(TestoComune.Parse("testo che non è il nostro JSON"));
    }

    [Fact]
    public void La_chiave_e_il_lock_hanno_spazi_di_nomi_distinti()
    {
        Assert.Equal("section-common:radartower:technique", TestoComune.Chiave(SectionKeys.RadarTowerTechnique));
        Assert.Equal("editor:section-common:radartower:technique", TestoComune.ChiaveLock(" RadarTower:Technique "));
    }

    // ---- In testa alla sezione -----------------------------------------------------------------------

    [Fact]
    public void Il_testo_comune_va_in_testa_alla_tecnica_prima_dei_blocchi_dello_scalo()
    {
        var sezioni = new[]
        {
            Sez(SectionKeys.RadarTower, figli: Sez(SectionKeys.RadarTowerTechnique, blocchi: new[] { DelloScalo("Avvicinamento RWY 12.") })),
        };

        var tecnica = TorreRadar.ConTestoComune(sezioni, ConRadar(DueCallout))[0].Children[0];

        Assert.Equal(3, tecnica.Blocks.Count);
        Assert.Equal(BlockFormat.Callout, tecnica.Blocks[0].Format);
        Assert.Equal("Avvicinamento RWY 12.", tecnica.Blocks[2].Body);
    }

    /// <summary>
    /// La pagina ordina blocchi e sotto-sezioni per <c>Order</c> (<see cref="CorpoDiSezione"/>): il testo comune deve
    /// stare in testa anche davanti a una sotto-sezione messa «prima di ogni blocco» (soglia 0) dello scalo.
    /// </summary>
    [Fact]
    public void Nella_fila_del_corpo_sta_davanti_ai_blocchi_e_alle_figlie_dello_scalo()
    {
        var blocchi = TestoComune.Vista(DueCallout).Concat(new[] { DelloScalo("Dello scalo") }).ToList();

        var fila = CorpoDiSezione.Componi(blocchi, b => b.Order, new[] { "figlia" }, _ => 0, conScheda: false);

        Assert.Equal(TipoVoce.Blocco, fila[0].Tipo);
        Assert.Equal(TipoVoce.Blocco, fila[1].Tipo);
        Assert.Equal(TipoVoce.Figlia, fila[2].Tipo);
        Assert.Equal("Dello scalo", fila[3].Blocco!.Body);
    }

    [Fact]
    public void Nel_vSOP_scende_dentro_i_dati_generali()
    {
        var sezioni = new[]
        {
            Sez("generaldata", figli: Sez(SectionKeys.RadarTower, figli: Sez(SectionKeys.RadarTowerTechnique))),
        };

        var tecnica = TorreRadar.ConTestoComune(sezioni, ConRadar(DueCallout))[0].Children[0].Children[0];

        Assert.Equal(2, tecnica.Blocks.Count);
    }

    /// <summary>Una release scattata prima del testo comune non l'ha congelato: la pagina resta com'era.</summary>
    [Fact]
    public void Senza_testo_comune_la_lista_e_la_stessa()
    {
        var sezioni = new[] { Sez(SectionKeys.RadarTower, figli: Sez(SectionKeys.RadarTowerTechnique)) };

        Assert.Same(sezioni, TorreRadar.ConTestoComune(sezioni, new AirportRadarTowerView(true)));
        Assert.Same(sezioni, TorreRadar.ConTestoComune(sezioni, new AirportRadarTowerView(true, null, Array.Empty<BlockView>())));
    }

    /// <summary>Senza radar la tecnica sparisce, testo comune compreso: chi decide se si vede resta il filtro.</summary>
    [Fact]
    public void Senza_radar_la_tecnica_sparisce_anche_col_testo_comune()
    {
        var vista = new AirportRadarTowerView(false, null, TestoComune.Vista(DueCallout));
        var sezioni = new[] { Sez(SectionKeys.RadarTower, figli: Sez(SectionKeys.RadarTowerTechnique)) };

        var mostrate = TorreRadar.Filtra(TorreRadar.ConTestoComune(sezioni, vista), vista);

        Assert.Empty(mostrate[0].Children);
    }

    // ---- La firma e la release -------------------------------------------------------------------------

    [Fact]
    public void La_firma_guarda_il_testo_non_gli_id_e_niente_vale_vuoto()
    {
        var a = TestoComune.Vista(DueCallout);
        var spostati = a.Select(b => new BlockView
        {
            Id = b.Id - 100, Order = b.Order + 7, Format = b.Format, State = b.State, Body = b.Body, BodyJson = b.BodyJson,
            CalloutKind = b.CalloutKind,
        }).ToList();
        var cambiato = TestoComune.Vista(new[] { DueCallout[0], Callout("LA TORRE NON FORNISCE VETTORAMENTO RADAR", CalloutKind.Success) });

        Assert.Equal(TestoComune.Firma(a), TestoComune.Firma(spostati));
        Assert.NotEqual(TestoComune.Firma(a), TestoComune.Firma(cambiato));
        Assert.Equal(TestoComune.Firma(null), TestoComune.Firma(Array.Empty<BlockView>()));
    }

    /// <summary>Il testo comune viaggia nella vista congelata: deve uscire dal JSON della release uguale a com'è entrato.</summary>
    [Fact]
    public void Congelato_nella_vista_esce_dalla_release_uguale()
    {
        var vista = ConRadar(DueCallout);
        var tornata = JsonSerializer.Deserialize<AirportRadarTowerView>(JsonSerializer.Serialize(vista))!;

        Assert.True(tornata.Equipped);
        Assert.Equal(TestoComune.Firma(vista.Comune), TestoComune.Firma(tornata.Comune));
    }

    private static RawSection Raw(int id, string chiave, bool nascosta = false, params RawSection[] figli) => new()
    {
        Id = id, Title = chiave, Depth = 0, SectionKey = chiave, Order = id, IsHidden = nascosta, Children = figli.ToList(),
    };

    private static string Payload(AirportRadarTowerView? vista, bool radarNascosta = false, bool tecnicaNascosta = false)
    {
        var p = new DocReleasePayload
        {
            Doc = new RawDocument
            {
                Title = "vIPI LIRQ", AiracCycle = "2611",
                Roots = new[]
                {
                    Raw(1, "runways"),
                    Raw(5, SectionKeys.RadarTower, radarNascosta, Raw(6, SectionKeys.RadarTowerTechnique, tecnicaNascosta)),
                },
            },
        };
        if (vista is not null) p.FrozenSections[5] = JsonSerializer.Serialize(vista);
        return JsonSerializer.Serialize(p);
    }

    [Fact]
    public void Dalla_release_si_legge_il_testo_comune_che_il_lettore_vede()
    {
        var (mostrato, comune) = TorreRadar.TestoComuneDellaRelease(Payload(ConRadar(DueCallout)));

        Assert.True(mostrato);
        Assert.Equal(TestoComune.Firma(TestoComune.Vista(DueCallout)), TestoComune.Firma(comune));
    }

    /// <summary>Una release di prima: il lettore vedrebbe la sezione, ma il testo comune non c'è.</summary>
    [Fact]
    public void Una_release_di_prima_mostra_la_sezione_senza_testo_comune()
    {
        var (mostrato, comune) = TorreRadar.TestoComuneDellaRelease(Payload(new AirportRadarTowerView(true)));

        Assert.True(mostrato);
        Assert.Null(comune);
    }

    /// <summary>Dove il lettore non lo vede, un testo comune cambiato non è un documento da ripubblicare.</summary>
    [Fact]
    public void Senza_radar_o_con_la_sezione_nascosta_non_si_vede()
    {
        Assert.False(TorreRadar.TestoComuneDellaRelease(Payload(new AirportRadarTowerView(false, null, TestoComune.Vista(DueCallout)))).Mostrato);
        Assert.False(TorreRadar.TestoComuneDellaRelease(Payload(ConRadar(DueCallout), radarNascosta: true)).Mostrato);
        Assert.False(TorreRadar.TestoComuneDellaRelease(Payload(ConRadar(DueCallout), tecnicaNascosta: true)).Mostrato);
        Assert.False(TorreRadar.TestoComuneDellaRelease(Payload(null)).Mostrato);
        Assert.False(TorreRadar.TestoComuneDellaRelease("{}").Mostrato);
        Assert.False(TorreRadar.TestoComuneDellaRelease("non è JSON").Mostrato);
    }
}
