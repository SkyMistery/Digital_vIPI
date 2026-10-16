using Vipi.Application.Aor;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il banco di prova della struttura: «con questi aperti, chi tiene cosa». Il banco non ha un motore suo — passa da
/// <see cref="FallbackChain.Holders"/> — quindi qui si prova quel che è suo: quali settori si considerano aperti,
/// come si raggruppa la risposta, e che cosa si dice quando non raccoglie nessuno. Carta
/// <c>docs/feature/2026-10-04-copertura-unica.md</c>.
/// </summary>
public class CoverageBenchTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Padova = "LIPP_CE1_CTR";
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;
    private static readonly string[] Milano = { Ws2, Es2, Ws5, Es5 };

    /// <summary>Milano com'è in produzione, più un centro vicino che dello scenario non fa parte.</summary>
    private static Topology Topo(string? padreDiWs2 = null)
    {
        var padri = new Dictionary<string, string>(OIC) { [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2 };
        if (padreDiWs2 is not null) padri[Ws2] = padreDiWs2;
        return new Topology
        {
            Sectors = new[] { Ws2, Es2, Ws5, Es5, Padova },
            Parent = padri,
            Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(OIC)
            {
                [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: 32500, TopFeet: null) },
            },
            Bands = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC)
            {
                [Ws2] = (0, 32500), [Es2] = (0, 32500), [Ws5] = (32500, null), [Es5] = (32500, null),
            },
        };
    }

    private static string[] Tenuti(IReadOnlyList<BenchGroup> gruppi, string? chi) =>
        gruppi.Single(g => OIC.Equals(g.Holder, chi)).Items.Select(i => i.Sector).ToArray();

    [Fact]
    public void Le_due_configurazioni_di_Milano_dallo_stesso_albero()
    {
        var conWs5 = CoverageBench.Copertura(Topo(), Milano, new[] { Ws2, Es2, Ws5 });
        Assert.Equal(new[] { Es5 }, Tenuti(conWs5, Ws5));
        Assert.Empty(Tenuti(conWs5, Es2));

        var senza = CoverageBench.Copertura(Topo(), Milano, new[] { Ws2, Es2 });
        Assert.Equal(new[] { Es5 }, Tenuti(senza, Es2));
        Assert.Equal(new[] { Ws5 }, Tenuti(senza, Ws2));
    }

    [Fact]
    public void Un_aperto_che_non_assorbe_niente_resta_in_tabella()
    {
        // È un'informazione: «ES2 è aperto e tiene solo il suo». Togliendolo la tabella direbbe che non c'è.
        var gruppi = CoverageBench.Copertura(Topo(), Milano, Milano);

        Assert.Equal(Milano, gruppi.Select(g => g.Holder));
        Assert.All(gruppi, g => Assert.Empty(g.Items));
    }

    [Fact]
    public void I_gruppi_seguono_l_ordine_in_cui_si_sono_aperti_i_settori()
    {
        var gruppi = CoverageBench.Copertura(Topo(), Milano, new[] { Es2, Ws2 });

        Assert.Equal(new[] { Es2, Ws2 }, gruppi.Select(g => g.Holder));
    }

    [Fact]
    public void Tutti_chiusi_e_nessuno_sopra_il_cielo_non_lo_tiene_nessuno()
    {
        var gruppi = CoverageBench.Copertura(Topo(), Milano, Array.Empty<string>());

        var nessuno = Assert.Single(gruppi);
        Assert.Null(nessuno.Holder);
        Assert.Equal(4, nessuno.Items.Count);
    }

    [Fact]
    public void Chi_sta_fuori_dallo_scenario_si_considera_aperto_e_si_dice()
    {
        // WS2 pende da un settore di Padova: chiuso tutto Milano, raccoglie lui — e la tabella dice che è fuori.
        var gruppi = CoverageBench.Copertura(Topo(padreDiWs2: Padova), Milano, Array.Empty<string>());

        var fuori = Assert.Single(gruppi);
        Assert.Equal(Padova, fuori.Holder);
        Assert.True(fuori.Outside);
    }

    [Fact]
    public void Un_callsign_che_non_e_dello_scenario_non_si_puo_aprire_da_qui()
    {
        // Il banco apre e chiude i SOLI settori dell'elenco: un nome in più fra gli aperti non diventa una riga.
        var gruppi = CoverageBench.Copertura(Topo(), Milano, new[] { Ws2, "LIRR_XX_CTR" });

        Assert.DoesNotContain(gruppi, g => g.Holder == "LIRR_XX_CTR");
    }

    [Fact]
    public void L_insieme_online_di_uno_scenario_e_gli_aperti_piu_tutto_il_resto()
    {
        var online = CoverageBench.OnlineDi(Topo().Sectors, Milano, new[] { Ws2 });

        Assert.Equal(new[] { Padova, Ws2 }.OrderBy(x => x), online.OrderBy(x => x));
    }

    [Fact]
    public void Un_settore_diviso_per_quota_porta_la_fascia_accanto_al_nome()
    {
        var t = new Topology
        {
            Sectors = new[] { "XX_CTR", "ALTO_CTR", "PADRE_CTR" },
            Parent = new Dictionary<string, string>(OIC) { ["XX_CTR"] = "PADRE_CTR" },
            Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(OIC)
            {
                ["XX_CTR"] = new[] { new FallbackRow("ALTO_CTR", BaseFeet: 32500, TopFeet: null) },
            },
            Bands = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC) { ["XX_CTR"] = (0, null) },
        };

        var gruppi = CoverageBench.Copertura(t, new[] { "XX_CTR", "ALTO_CTR", "PADRE_CTR" }, new[] { "PADRE_CTR", "ALTO_CTR" });

        Assert.Equal("SFC–FL325", gruppi.Single(g => g.Holder == "PADRE_CTR").Items.Single().Band);
        Assert.Equal("FL325–UNL", gruppi.Single(g => g.Holder == "ALTO_CTR").Items.Single().Band);
    }

    // ---- una configurazione parla dei soli settori del suo gruppo (12 ottobre 2026) ----

    private const string Genova = "LIMJ_WS0_APP", Torino = "LIMF_WW0_APP", Caselle = "LIMF_WN0_APP";
    private static readonly string[] Ovest = { Torino, Caselle, Genova };

    [Fact]
    public void Una_configurazione_dell_area_non_chiude_gli_avvicinamenti()
    {
        // Il difetto: «Conf 2» dell'area apriva WS2 ed ES2 e chiudeva TUTTO il resto dell'ACC, avvicinamenti
        // compresi — e WS2 si trovava a passare a sé stesso il traffico per Genova.
        var aperti = CoverageBench.ConPreset(Milano.Concat(Ovest), delGruppo: Milano, delPreset: new[] { Ws2, Es2 });

        Assert.Equal(new[] { Es2, Caselle, Torino, Genova, Ws2 }.OrderBy(x => x), aperti.OrderBy(x => x));
    }

    [Fact]
    public void Una_configurazione_di_un_ente_non_chiude_l_area()
    {
        var aperti = CoverageBench.ConPreset(new[] { Ws2, Es2, Caselle }, delGruppo: Ovest, delPreset: new[] { Torino, Genova });

        Assert.Equal(new[] { Es2, Torino, Genova, Ws2 }.OrderBy(x => x), aperti.OrderBy(x => x));
    }

    [Fact]
    public void Una_configurazione_non_apre_un_settore_che_non_e_del_suo_gruppo()
    {
        // Un elenco rimasto indietro rispetto alla struttura: ES2 non è più dell'ente, e da lì non si apre.
        var aperti = CoverageBench.ConPreset(new[] { Ws2 }, delGruppo: Ovest, delPreset: new[] { Torino, Es2 });

        Assert.Equal(new[] { Torino, Ws2 }.OrderBy(x => x), aperti.OrderBy(x => x));
    }

    // ---- i trasferimenti: una riga per clausola, tutte, con l'esito ----

    private static readonly string[] Star = { "EGHIN 1J", "TESTO 1J", "UNITA 1J", "IDONA 1J", "KALMO 1J", "GOLAS 1J", "IXITO 1J" };

    private static ResolvedTransferFlow Flusso(string scritto, string? cede, string? scalo, params ResolvedTransferPoint[] punti) => new()
    {
        Flow = new TransferFlowRow
        {
            Id = 1, AccCode = "LIMM", OwningSectorId = 1, OwningSectorCallsign = scritto,
            Kind = Vipi.Domain.TransferFlowKind.Arrival, AirportIcao = scalo, Order = 0,
            Points = Array.Empty<TransferPointRow>(),
        },
        ResolvedOwnerCallsign = cede ?? scritto,
        OwnerOnline = cede is not null,
        Points = punti,
    };

    private static ResolvedTransferPoint Punto(string cop, string scritto, string? riceve, int? clausola = 206) => new()
    {
        Point = new TransferPointRow
        {
            Id = 1, Cop = cop, ClauseId = clausola, LevelUnit = Vipi.Domain.LevelUnit.Fl,
            LevelConstraint = Vipi.Domain.LevelConstraint.Exact, LevelText = "FL120", NextSectorCallsign = scritto, Order = 0,
        },
        ResolvedHandler = riceve ?? TransferOnlineResolver.Unicom,
        IsOnline = riceve is not null,
    };

    private static ResolvedTransferPoint[] Punti(string scritto, string? riceve) =>
        Star.Select(s => Punto(s, scritto, riceve)).ToArray();

    [Fact]
    public void Una_clausola_che_resta_a_chi_e_scritto_compare_ed_e_una_riga_sola()
    {
        // Il caso del committente: WS2 → Genova, sette STAR a FL120. Con tutti e due aperti non cambia niente —
        // e proprio per questo il banco non la mostrava.
        var riga = Assert.Single(CoverageBench.Trasferimenti(new[] { Flusso(Ws2, Ws2, "LIMJ", Punti(Genova, Genova)) }));

        Assert.Equal(BenchTransferState.AsWritten, riga.State);
        Assert.Equal(Star, riga.Points);
        Assert.Equal(new[] { "LIMJ" }, riga.Airports);
        Assert.False(riga.SenderDiffers);
        Assert.False(riga.ReceiverDiffers);
    }

    [Fact]
    public void Chi_tiene_gia_il_cielo_del_ricevente_chiuso_passa_a_se_stesso_e_non_e_un_cambio()
    {
        // Genova e Torino chiuse: la catena di Genova porta a WS2, che è anche chi cede. È il caso che la vista
        // live toglie dall'elenco; qui resta, ma col suo nome.
        var riga = Assert.Single(CoverageBench.Trasferimenti(new[] { Flusso(Ws2, Ws2, "LIMJ", Punti(Genova, Ws2)) }));

        Assert.Equal(BenchTransferState.ToItself, riga.State);
    }

    [Fact]
    public void Se_raccoglie_un_altro_il_trasferimento_cambia_mano()
    {
        var riga = Assert.Single(CoverageBench.Trasferimenti(new[] { Flusso(Ws2, Ws2, "LIMJ", Punti(Genova, Torino)) }));

        Assert.Equal(BenchTransferState.PickedUp, riga.State);
        Assert.Equal(Torino, riga.Receiver);
        Assert.True(riga.ReceiverDiffers);
        Assert.False(riga.SenderDiffers);
    }

    [Fact]
    public void Senza_nessuno_che_ceda_o_che_riceva_la_clausola_non_e_coperta()
    {
        var nessunoRiceve = Assert.Single(CoverageBench.Trasferimenti(new[] { Flusso(Ws2, Ws2, "LIMJ", Punti(Genova, null)) }));
        Assert.Equal(BenchTransferState.Uncovered, nessunoRiceve.State);
        Assert.Null(nessunoRiceve.Receiver);

        var nessunoCede = Assert.Single(CoverageBench.Trasferimenti(new[] { Flusso(Ws2, null, "LIMJ", Punti(Genova, Genova)) }));
        Assert.Equal(BenchTransferState.Uncovered, nessunoCede.State);
        Assert.Null(nessunoCede.Sender);
    }

    [Fact]
    public void Gli_scali_di_una_sezione_stanno_sulla_stessa_riga()
    {
        // Una sezione con tre scali esce come tre flussi che portano le STESSE clausole: è una riga, coi tre scali.
        var righe = CoverageBench.Trasferimenti(new[]
        {
            Flusso(Ws2, Ws2, "LIPO", Punto("ODINA", Es2, Es2)),
            Flusso(Ws2, Ws2, "LIPX", Punto("ODINA", Es2, Es2)),
            Flusso(Ws2, Ws2, "LIPL", Punto("ODINA", Es2, Es2)),
        });

        var riga = Assert.Single(righe);
        Assert.Equal(new[] { "LIPO", "LIPX", "LIPL" }, riga.Airports);
        Assert.Equal(new[] { "ODINA" }, riga.Points);
    }

    [Fact]
    public void Una_clausola_coi_punti_in_due_mani_esce_una_volta_per_mano()
    {
        var righe = CoverageBench.Trasferimenti(new[]
        {
            Flusso(Ws2, Ws2, "LIMJ", Punto("EGHIN 1J", Genova, Genova), Punto("TESTO 1J", Genova, Torino)),
        });

        Assert.Equal(new[] { BenchTransferState.AsWritten, BenchTransferState.PickedUp }, righe.Select(r => r.State));
        Assert.Equal(new[] { "TESTO 1J" }, righe[1].Points);
    }

    [Fact]
    public void Due_clausole_diverse_non_si_riuniscono_anche_se_vanno_alla_stessa_mano()
    {
        var righe = CoverageBench.Trasferimenti(new[]
        {
            Flusso(Ws2, Ws2, "LIMJ", Punto("EGHIN 1J", Genova, Genova, clausola: 1), Punto("TESTO 1J", Genova, Genova, clausola: 2),
                Punto("SENZA", Genova, Genova, clausola: null), Punto("SENZA2", Genova, Genova, clausola: null)),
        });

        Assert.Equal(4, righe.Count);
    }

    // ---- gli altri enti degli accordi: si aprono e si chiudono anche loro (12 ottobre 2026) ----

    private const string Firenze = "LIRQ_NN0_APP", RomaNe = "LIRR_NE_CTR", RomaNc = "LIRR_NC_CTR", RomaEw = "LIRR_EW_CTR";
    private const string RomaEs = "LIRR_ES_CTR", RomaSu = "LIRR_SU_CTR";

    /// <summary>Milano, Padova e un pezzo di Roma: Firenze pende da NE, NE da NC, NC da EW; sopra FL325 NE va a ES.</summary>
    private static Topology ConRoma(string? padreDiWs2 = null)
    {
        var t = Topo(padreDiWs2);
        var padri = new Dictionary<string, string>(t.Parent, OIC) { [Firenze] = RomaNe, [RomaNe] = RomaNc, [RomaNc] = RomaEw };
        var ripieghi = new Dictionary<string, IReadOnlyList<FallbackRow>>(t.Fallbacks, OIC)
        {
            [RomaNe] = new[] { new FallbackRow(RomaEs, BaseFeet: 32500, TopFeet: null) },
        };
        return new Topology
        {
            Sectors = t.Sectors.Concat(new[] { Firenze, RomaNe, RomaNc, RomaEw, RomaEs, RomaSu }).ToArray(),
            Parent = padri, Fallbacks = ripieghi,
            // Firenze ha una banda sua, bassa: senza, la riga «sopra FL325» di NE la dividerebbe in due.
            Bands = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(t.Bands, OIC) { [Firenze] = (0, 10000) },
        };
    }

    private static TransferFlowRow Scritto(string cede, params string[] riceventi) => new()
    {
        Id = 1, AccCode = "LIMM", OwningSectorId = 1, OwningSectorCallsign = cede,
        Kind = Vipi.Domain.TransferFlowKind.Arrival, Order = 0,
        Points = riceventi.Select((r, i) => new TransferPointRow
        {
            Id = i + 1, Cop = "P" + i, LevelUnit = Vipi.Domain.LevelUnit.Fl,
            LevelConstraint = Vipi.Domain.LevelConstraint.Exact, LevelText = "FL120", NextSectorCallsign = r, Order = i,
        }).ToList(),
    };

    [Fact]
    public void Il_ricevente_di_un_altro_centro_entra_nel_banco_con_tutta_la_sua_catena()
    {
        // Milano → Firenze. Per chiedere «e se Firenze è chiusa? e se è chiusa anche Roma NE?» devono essere sul
        // tavolo Firenze e tutti quelli che la sua catena può toccare — anche ES, che la raccoglie solo in quota.
        var altri = CoverageBench.AltriEnti(new[] { Scritto(Ws2, Firenze) }, ConRoma(), Milano);

        Assert.Equal(new[] { Firenze, RomaNe, RomaEs, RomaNc, RomaEw }.OrderBy(x => x), altri.OrderBy(x => x));
        Assert.Equal(Firenze, altri[0]);            // prima il capo scritto, poi chi lo raccoglie
        Assert.DoesNotContain(RomaSu, altri);       // non è su nessuna catena: aperto o chiuso non cambia niente
        Assert.DoesNotContain(Ws2, altri);          // i settori dell'ACC hanno già il loro interruttore
    }

    [Fact]
    public void Anche_chi_cede_da_un_altro_centro_entra_nel_banco()
    {
        var altri = CoverageBench.AltriEnti(new[] { Scritto(Padova, Es2) }, ConRoma(), Milano);

        Assert.Equal(new[] { Padova }, altri);
    }

    [Fact]
    public void Chi_raccoglie_un_settore_dell_ACC_da_fuori_entra_nel_banco()
    {
        // WS2 pende da Padova: l'accordo è tutto di Milano, ma chiuso WS2 il suo traffico lo cede Padova.
        var altri = CoverageBench.AltriEnti(new[] { Scritto(Ws2, Es2) }, ConRoma(padreDiWs2: Padova), Milano);

        Assert.Equal(new[] { Padova }, altri);
    }

    [Fact]
    public void Un_capo_che_la_struttura_non_conosce_non_entra_nel_banco()
    {
        // Un settore cancellato dal catalogo e rimasto in un accordo: non si può aprire, e nello scenario farebbe
        // solo credere di averlo chiuso.
        var altri = CoverageBench.AltriEnti(new[] { Scritto(Ws2, "LIXX_SPARITO_APP", Firenze) }, ConRoma(), Milano);

        Assert.DoesNotContain("LIXX_SPARITO_APP", altri);
        Assert.Contains(Firenze, altri);
    }

    [Fact]
    public void Un_altro_ente_chiuso_lo_raccoglie_la_sua_catena_e_la_tabella_lo_dice()
    {
        var topo = ConRoma();
        var altri = CoverageBench.AltriEnti(new[] { Scritto(Ws2, Firenze) }, topo, Milano);
        var scenario = Milano.Concat(altri).ToArray();

        // Chiuse Firenze e Roma NE: Firenze sale a NC, e NE si divide fra NC e ES.
        var aperti = scenario.Where(s => s != Firenze && s != RomaNe).ToArray();
        var gruppi = CoverageBench.SenzaAltriEntiVuoti(CoverageBench.Copertura(topo, scenario, aperti), altri);

        Assert.Contains(Firenze, Tenuti(gruppi, RomaNc));
        Assert.Contains(RomaNe, Tenuti(gruppi, RomaNc));
        Assert.Equal(new[] { RomaNe }, Tenuti(gruppi, RomaEs));
        // Un altro ente aperto che non raccoglie niente non fa riga; un settore dell'ACC sì, è un'informazione.
        Assert.DoesNotContain(gruppi, g => g.Holder == RomaEw);
        Assert.Contains(gruppi, g => g.Holder == Ws5 && g.Items.Count == 0);
    }
}
