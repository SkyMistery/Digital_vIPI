using Vipi.Application.Content;
using Vipi.Application.Live;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La ricerca rapida della vista live (1 ottobre 2026): scali per ICAO o nome, aree per nome scritto in qualunque modo
/// o per tipo. Il confronto ignora maiuscole, spazi, trattini e punti.
/// </summary>
public class RicercaLiveFiltroTests
{
    private static readonly LiveAeroportoTrovato[] Scali =
    {
        new("LIPE", "Bologna Borgo Panigale", "LIPP", true, false),
        new("LIPB", "Bolzano", "LIPP", true, false),
        new("LIRF", "Roma Fiumicino", "LIRR", true, false),
        new("LIPA", "Aviano", "LIPP", false, true),
    };

    private static SpecialAreaPick Area(string id, string nome, string? tipo) => new(id, nome, tipo, 0, 5000, Array.Empty<string>());

    private static readonly SpecialAreaPick[] Aree =
    {
        Area("1", "LI R14A - S.Severa", "R"),
        Area("2", "LI R14B - S.Severa", "R"),
        Area("3", "LI D120 - Capo Frasca", "D"),
        Area("4", "TRA 21 Ghedi", "TRA"),
    };

    [Fact]
    public void Lo_scalo_si_trova_col_suo_ICAO_anche_fuori_dal_proprio_ACC()
    {
        var r = RicercaLiveFiltro.Aeroporti(Scali, "lipe");
        Assert.Equal("LIPE", Assert.Single(r).Icao);
        Assert.Equal("LIPP", r[0].AccCode);
    }

    [Fact]
    public void Prima_l_ICAO_esatto_poi_il_prefisso_poi_il_nome()
    {
        var r = RicercaLiveFiltro.Aeroporti(Scali, "LIP");
        Assert.Equal(new[] { "LIPA", "LIPB", "LIPE" }, r.Select(a => a.Icao));

        var perNome = RicercaLiveFiltro.Aeroporti(Scali, "bol");
        Assert.Equal(new[] { "LIPB", "LIPE" }, perNome.Select(a => a.Icao));
    }

    [Fact]
    public void Un_carattere_solo_non_cerca_niente() =>
        Assert.Empty(RicercaLiveFiltro.Aeroporti(Scali, "L"));

    [Theory]
    [InlineData("R14")]
    [InlineData("LI-R14")]
    [InlineData("li r 14")]
    [InlineData("severa")]
    public void L_area_si_trova_comunque_la_si_scriva(string q) =>
        Assert.Equal(new[] { "1", "2" }, RicercaLiveFiltro.Aree(Aree, q).Select(a => a.IvaoId));

    [Fact]
    public void Il_tipo_da_solo_trova_tutte_le_aree_di_quel_tipo()
    {
        Assert.Equal(new[] { "3" }, RicercaLiveFiltro.Aree(Aree, "D").Select(a => a.IvaoId));
        Assert.Equal(new[] { "4" }, RicercaLiveFiltro.Aree(Aree, "tra").Select(a => a.IvaoId));
    }

    private static readonly LivePostazioneTrovata[] Postazioni =
    {
        new("LIRR_NE_CTR", "Roma Radar", "128.705", null),
        new("LIRR_NE1_CTR", "Roma Radar", "125.500", null),
        new("LIRF_TWR", "Fiumicino Torre", "118.705", "LIRF"),
        new("LIRF_GND", "Fiumicino Ground", null, "LIRF"),
    };

    [Theory]
    [InlineData("128.705")]
    [InlineData("128.7")]
    [InlineData("1287")]
    public void La_postazione_si_trova_per_frequenza_dall_inizio(string q) =>
        Assert.Equal("LIRR_NE_CTR", Assert.Single(RicercaLiveFiltro.Postazioni(Postazioni, q)).Callsign);

    [Fact]
    public void La_postazione_si_trova_per_callsign_prima_poi_per_nominativo()
    {
        Assert.Equal(new[] { "LIRR_NE1_CTR", "LIRR_NE_CTR" }, RicercaLiveFiltro.Postazioni(Postazioni, "lirr ne").Select(p => p.Callsign));
        Assert.Equal(new[] { "LIRF_GND", "LIRF_TWR" }, RicercaLiveFiltro.Postazioni(Postazioni, "fiumicino").Select(p => p.Callsign));
    }

    [Fact]
    public void Una_frequenza_col_punto_non_trova_scali_ne_aree()
    {
        Assert.True(RicercaLiveFiltro.SoloFrequenza("118.1"));
        Assert.False(RicercaLiveFiltro.SoloFrequenza("120"));
        Assert.False(RicercaLiveFiltro.SembraFrequenza("R14"));
        Assert.Empty(RicercaLiveFiltro.Aree(Aree, "120.5"));
        Assert.Empty(RicercaLiveFiltro.Aeroporti(Scali, "118.1"));
    }

    /// <summary>Review del 1 ottobre 2026: le sole cifre sono anche un nome — «120» trova l'area D120.</summary>
    [Fact]
    public void Le_sole_cifre_trovano_anche_i_nomi() =>
        Assert.Equal(new[] { "3" }, RicercaLiveFiltro.Aree(Aree, "120").Select(a => a.IvaoId));

    /// <summary>
    /// 🔴 La memoria condivisa: dieci richieste insieme caricano UNA volta (review del 1 ottobre 2026, «dieci utenti che
    /// cercano insieme»), e chi arriva dopo, entro la durata, non carica affatto.
    /// </summary>
    [Fact]
    public async Task Dieci_richieste_insieme_caricano_una_volta()
    {
        var chiave = "prova-" + Guid.NewGuid();
        var caricamenti = 0;
        var via = new TaskCompletionSource();
        async Task<string[]> Carica()
        {
            Interlocked.Increment(ref caricamenti);
            await via.Task;
            return new[] { "ok" };
        }
        var richieste = Enumerable.Range(0, 10)
            .Select(_ => MemoriaRicercaLive.PrendiAsync(chiave, TimeSpan.FromMinutes(5), Carica, CancellationToken.None))
            .ToList();
        via.SetResult();
        var risposte = await Task.WhenAll(richieste);
        Assert.Equal(1, caricamenti);
        Assert.All(risposte, r => Assert.Same(risposte[0], r));
        await MemoriaRicercaLive.PrendiAsync(chiave, TimeSpan.FromMinutes(5), Carica, CancellationToken.None);
        Assert.Equal(1, caricamenti);
    }

    private static readonly LiveProceduraTrovata[] Procedure =
    {
        new("LIRN", "LIRR", true, "AGNI7G", "AGNIS", "—", "24"),
        new("LIRN", "LIRR", false, "ALAX1A", "ALAXI", "AGNIS", "24"),
        new("LIRF", "LIRR", true, "BOL5A", "BOLSE", "—", "16L"),
    };

    [Fact]
    public void Il_punto_si_trova_nel_fix_e_nella_transition_da_tre_lettere()
    {
        Assert.Equal(new[] { "AGNI7G", "ALAX1A" }, RicercaLiveFiltro.Punti(Procedure, "agn").Select(p => p.Nome));
        Assert.Empty(RicercaLiveFiltro.Punti(Procedure, "AG"));
    }

    [Fact]
    public void Il_punto_di_trasferimento_si_trova_dall_inizio_del_nome()
    {
        var cop = new[]
        {
            new LiveTrasferimentoTrovato("BOL", "LIRR_NE_CTR", "LIMM_E_CTR", "FL250", null, "LIRR", Array.Empty<string>()),
            new LiveTrasferimentoTrovato("ELB", "LIRR_NW_CTR", "LIMM_W_CTR", "FL190", null, "LIRR", Array.Empty<string>()),
        };
        Assert.Equal("LIMM_E_CTR", Assert.Single(RicercaLiveFiltro.Trasferimenti(cop, "bol")).A);
        Assert.Empty(RicercaLiveFiltro.Trasferimenti(cop, "BO"));
    }

    [Fact]
    public void La_radioassistenza_si_trova_per_codice_o_per_frequenza()
    {
        var nav = new[]
        {
            new LiveNavaidTrovato("PES", "VHF", "VOR/DME", "115.800", null, 42.4, 14.2),
            new LiveNavaidTrovato("PAL", "VHF", "VOR", "112.300", null, 38.1, 13.4),
        };
        Assert.Equal("PES", Assert.Single(RicercaLiveFiltro.Navaid(nav, "pe")).Codice);
        Assert.Equal("PES", Assert.Single(RicercaLiveFiltro.Navaid(nav, "115.8")).Codice);
    }

    [Fact]
    public void Testo_vuoto_nessun_risultato()
    {
        Assert.Empty(RicercaLiveFiltro.Aree(Aree, "  - "));
        Assert.Empty(RicercaLiveFiltro.Aeroporti(Scali, ""));
    }
}
