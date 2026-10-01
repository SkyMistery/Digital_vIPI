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

    [Fact]
    public void Testo_vuoto_nessun_risultato()
    {
        Assert.Empty(RicercaLiveFiltro.Aree(Aree, "  - "));
        Assert.Empty(RicercaLiveFiltro.Aeroporti(Scali, ""));
    }
}
