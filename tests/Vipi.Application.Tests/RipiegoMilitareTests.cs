using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il MIL_CTR raccoglie solo il traffico militare, e assorbe gli APP militari suoi fratelli. Carta
/// <c>docs/feature/2026-09-24-mil-solo-traffico-militare.md</c>. L'albero è quello di Brindisi nella copia locale:
/// <c>LIBB_MIL_CTR</c> e gli APP degli scali solo militari (Grottaglie, Gioia) pendono tutti da <c>LIBB_ES_CTR</c>.
/// </summary>
public class RipiegoMilitareTests
{
    private const string Es = "LIBB_ES_CTR", Mil = "LIBB_MIL_CTR", Grottaglie = "LIBG_APP", Gioia = "LIBV_APP";
    private const string Bari = "LIBD_APP", Lontano = "LIBN_G_APP";

    private static readonly (string, string?)[] Albero =
    {
        (Es, null), (Mil, Es), (Grottaglie, Es), (Gioia, Es), (Bari, Es), (Lontano, Bari),
    };

    private static string? Padre(string cs) => Albero.FirstOrDefault(a => a.Item1 == cs).Item2;

    [Theory]
    [InlineData("LIMM_MIL_CTR", true, true)]
    [InlineData("LIEE_MIL_APP", true, false)]
    [InlineData("LIMM_WS2_CTR", false, false)]
    [InlineData("MILANO_CTR", false, false)]   // MIL nel primo pezzo è l'ICAO, non la famiglia
    public void Militare_si_legge_dal_nome(string cs, bool militare, bool milCtr)
    {
        Assert.Equal(militare, RipiegoMilitare.Militare(cs));
        Assert.Equal(milCtr, RipiegoMilitare.MilCtr(cs));
    }

    [Fact]
    public void Solo_gli_APP_militari_fratelli_del_MIL_hanno_il_ripiego_automatico()
    {
        // Lontano è un APP militare ma pende da Bari, non dal padre del MIL: segue l'albero civile (decisione D2).
        var fratelli = RipiegoMilitare.Fratelli(
            new (string, string?)[] { (Grottaglie, Es), (Gioia, Es), (Lontano, Bari) }, Albero);

        Assert.Equal(Mil, fratelli[Grottaglie]);
        Assert.Equal(Mil, fratelli[Gioia]);
        Assert.False(fratelli.ContainsKey(Lontano));
        Assert.False(fratelli.ContainsKey(Bari));   // non è uno scalo solo militare: non era fra gli APP militari
    }

    [Fact]
    public void Chiuso_l_APP_militare_va_al_MIL_e_poi_al_padre_civile()
    {
        var dichiarate = RipiegoMilitare.ConAutomatiche(
            new Dictionary<string, IReadOnlyList<FallbackRow>>(),
            new Dictionary<string, string> { [Grottaglie] = Mil });

        // D3: prima il MIL, poi il padre civile — e il padre c'è ancora, non è stato sostituito.
        var catena = FallbackChain.Candidates(Grottaglie, 5000, dichiarate, Padre);
        Assert.Equal(new[] { Grottaglie, Mil, Es }, catena);

        Assert.Equal(Mil, TransferOnlineResolver.FirstOnline(catena.Skip(1).ToList(), new HashSet<string> { Mil, Es }));
        Assert.Equal(Es, TransferOnlineResolver.FirstOnline(catena.Skip(1).ToList(), new HashSet<string> { Es }));
    }

    [Fact]
    public void Le_righe_scritte_a_mano_passano_prima_e_la_automatica_non_si_raddoppia()
    {
        var scritte = new Dictionary<string, IReadOnlyList<FallbackRow>>
        {
            [Grottaglie] = new[] { new FallbackRow(Bari, null, null) },
            [Gioia] = new[] { new FallbackRow(Mil, null, 20000) },
        };

        var tutte = RipiegoMilitare.ConAutomatiche(scritte,
            new Dictionary<string, string> { [Grottaglie] = Mil, [Gioia] = Mil });

        Assert.Equal(new[] { Bari, Mil }, tutte[Grottaglie].Select(r => r.TargetCallsign));
        Assert.True(tutte[Grottaglie][1].Automatica);
        Assert.Single(tutte[Gioia]);   // Gioia nomina già il MIL: chi l'ha scritta ha deciso la sua fascia
        Assert.False(tutte[Gioia][0].Automatica);
    }

    [Fact]
    public void La_catena_disegnata_dice_che_la_riga_e_automatica()
    {
        var dichiarate = RipiegoMilitare.ConAutomatiche(
            new Dictionary<string, IReadOnlyList<FallbackRow>>(),
            new Dictionary<string, string> { [Grottaglie] = Mil });

        var primoPasso = FallbackChain.Sequence(Grottaglie, dichiarate, Padre)[0];

        Assert.True(primoPasso.Single(p => p.TargetCallsign == Mil).Automatica);
        Assert.True(primoPasso.Single(p => p.TargetCallsign == Es).FromParent);
    }
}
