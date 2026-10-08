using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le configurazioni possibili di un gruppo: quali insiemi di aperti esistono. Carta
/// <c>docs/feature/2026-10-08-configurazioni-possibili.md</c> §3.
///
/// <para>Gli elenchi sono quelli veri: Milano come sta in produzione l'8 ottobre 2026, Torino–Genova come l'ha
/// detta il committente lo stesso giorno («WW0 da solo o con WS0, WN0 da solo o con WS0 accanto, WS0 da solo»).</para>
/// </summary>
public class ConfigurazioniPossibiliTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Mil = "LIMM_MIL_CTR";
    private const string Ww0 = "LIMF_WW0_APP", Wn0 = "LIMF_WN0_APP", Ws0 = "LIMJ_WS0_APP";

    private static AccConfiguration Cfg(string nome, params string[] aperti) => new()
    {
        Key = "cfg:" + nome,
        Name = nome,
        Open = aperti.Select(a => new AccConfigOpen { Callsign = a }).ToList(),
    };

    private static readonly AccConfiguration[] Milano =
    {
        Cfg("Conf 1", Ws2), Cfg("Conf 2", Es2, Ws2), Cfg("Conf 2 b", Ws2, Ws5), Cfg("Conf 3", Es2, Es5, Ws2, Ws5),
    };

    private static readonly AccConfiguration[] Torino =
    {
        Cfg("Unico", Ww0), Cfg("Unico + Genova", Ww0, Ws0), Cfg("Torino", Wn0), Cfg("Torino + Genova", Wn0, Ws0),
        Cfg("Genova", Ws0),
    };

    private static ConfigurazioniPossibili Tutte() => new(new[]
    {
        new ElencoDiConfigurazioni(ConfigurationGroupKind.AccArea, "LIMM", Milano),
        new ElencoDiConfigurazioni(ConfigurationGroupKind.AtcUnit, Ww0, Torino),
    });

    private static string[] Ordinati(IEnumerable<string> x) => x.OrderBy(s => s, StringComparer.Ordinal).ToArray();

    // ---- chi non può restare aperto ----

    [Fact]
    public void Chiuso_WS2_cadono_tutti_gli_altri_settori_d_area_di_Milano()
    {
        // È il caso di S99: la sonda chiudeva il solo WS2 e trovava ES2 a coprire il punto.
        Assert.Equal(Ordinati(new[] { Es2, Es5, Ws5 }), Ordinati(Tutte().ChiusiCon(new[] { Ws2 })));
    }

    [Fact]
    public void Chiuso_WS5_cade_ES5_che_non_e_suo_figlio()
    {
        // ⚠️ Il padre di ES5 è ES2: la gerarchia qui direbbe il contrario.
        Assert.Equal(new[] { Es5 }, Ordinati(Tutte().ChiusiCon(new[] { Ws5 })));
    }

    [Fact]
    public void Chiuso_ES5_o_ES2_da_soli_non_cade_chi_ha_una_configurazione_senza_di_loro()
    {
        Assert.Empty(Tutte().ChiusiCon(new[] { Es5 }));
        // Chiuso ES2 cade ES5 (la sola configurazione che lo contiene ha anche ES2); WS5 resta, con «Conf 2 b».
        Assert.Equal(new[] { Es5 }, Ordinati(Tutte().ChiusiCon(new[] { Es2 })));
    }

    [Fact]
    public void Un_esclusione_non_fa_cadere_nessuno()
    {
        // WW0 e WN0 non stanno mai insieme, ma chiuso l'uno l'altro può aprire: «chi non può restare» è vuoto.
        Assert.Empty(Tutte().ChiusiCon(new[] { Ww0 }));
        Assert.Empty(Tutte().ChiusiCon(new[] { Ws0 }));
    }

    [Fact]
    public void Un_settore_che_nessun_elenco_nomina_non_tocca_niente_e_non_cade_mai()
    {
        var tutte = Tutte();
        Assert.Empty(tutte.ChiusiCon(new[] { Mil }));
        Assert.DoesNotContain(Mil, tutte.ChiusiCon(new[] { Ws2 }));
    }

    [Fact]
    public void I_nominativi_si_confrontano_senza_badare_alle_maiuscole()
    {
        Assert.Equal(Ordinati(new[] { Es2, Es5, Ws5 }), Ordinati(Tutte().ChiusiCon(new[] { "limm_ws2_ctr" })));
    }

    [Fact]
    public void Senza_elenchi_non_cade_nessuno()
    {
        Assert.True(ConfigurazioniPossibili.Nessuna.Vuote);
        Assert.Empty(ConfigurazioniPossibili.Nessuna.ChiusiCon(new[] { Ws2 }));
        Assert.Empty(ConfigurazioniPossibili.Nessuna.NonPreviste(new[] { Es2 }));
    }

    [Fact]
    public void Una_configurazione_lasciata_vuota_non_vale_come_tutti_chiusi_ne_come_elenco()
    {
        var soloVuota = new ConfigurazioniPossibili(new[]
        {
            new ElencoDiConfigurazioni(ConfigurationGroupKind.AtcUnit, "LIBG_APP", new[] { Cfg("New configuration") }),
        });
        Assert.True(soloVuota.Vuote);
    }

    // ---- lo scenario è previsto? ----

    [Theory]
    [InlineData("")]
    [InlineData(Ws2)]
    [InlineData(Ws2 + "," + Es2)]
    [InlineData(Ws2 + "," + Ws5 + "," + Es2 + "," + Es5)]
    [InlineData(Ws2 + "," + Mil)]                 // MIL non è nominato: non conta
    [InlineData(Wn0)]
    [InlineData(Ws0)]
    [InlineData(Ww0 + "," + Ws0 + "," + Ws2)]     // due gruppi, ognuno con una configurazione sua
    public void Scenari_previsti(string aperti) =>
        Assert.Empty(Tutte().NonPreviste(aperti.Split(',', StringSplitOptions.RemoveEmptyEntries)));

    [Fact]
    public void ES2_senza_WS2_non_e_previsto_e_si_dice_di_quale_gruppo()
    {
        var fuori = Assert.Single(Tutte().NonPreviste(new[] { Es2, Wn0 }));
        Assert.Equal("LIMM", fuori.Elenco.Codice);
        Assert.Equal(new[] { Es2 }, fuori.Aperti);
    }

    [Fact]
    public void WW0_e_WN0_insieme_non_sono_previsti()
    {
        var fuori = Assert.Single(Tutte().NonPreviste(new[] { Ww0, Wn0, Ws2 }));
        Assert.Equal(Ww0, fuori.Elenco.Codice);
        Assert.Equal(Ordinati(new[] { Wn0, Ww0 }), Ordinati(fuori.Aperti));
    }

    [Fact]
    public void ES5_senza_ES2_non_e_previsto_perche_nessuna_configurazione_lo_scrive()
    {
        // L'elenco è esaustivo: {WS2, WS5, ES5} non c'è, quindi non esiste — anche se a coppie tornerebbe.
        Assert.Single(Tutte().NonPreviste(new[] { Ws2, Ws5, Es5 }));
    }

    // ---- le conseguenze, come le legge chi scrive l'elenco ----

    [Fact]
    public void Milano_le_quattro_configurazioni_dicono_da_sole_le_regole_del_committente()
    {
        var c = ConfigurazioniPossibili.Conseguenze(Milano).ToDictionary(x => x.Settore);

        Assert.Empty(c[Ws2].SempreCon);
        Assert.True(c[Ws2].DaSolo);
        Assert.Equal(new[] { Ws2 }, c[Es2].SempreCon);
        Assert.Equal(new[] { Ws2 }, c[Ws5].SempreCon);
        Assert.Equal(Ordinati(new[] { Es2, Ws2, Ws5 }), Ordinati(c[Es5].SempreCon));
        Assert.False(c[Es2].DaSolo);
        Assert.All(c.Values, x => Assert.Empty(x.MaiCon));
    }

    [Fact]
    public void Torino_l_esclusione_esce_senza_scriverla()
    {
        var c = ConfigurazioniPossibili.Conseguenze(Torino).ToDictionary(x => x.Settore);

        Assert.Equal(new[] { Wn0 }, c[Ww0].MaiCon);
        Assert.Equal(new[] { Ww0 }, c[Wn0].MaiCon);
        Assert.Empty(c[Ws0].MaiCon);
        Assert.All(c.Values, x => { Assert.Empty(x.SempreCon); Assert.True(x.DaSolo); });
    }

    [Fact]
    public void Torino_come_sta_scritta_oggi_nel_documento_mostra_il_buco()
    {
        // Le tre del documento dell'8 ottobre: mancano {WN0} e {WS0}, e si legge.
        var c = ConfigurazioniPossibili.Conseguenze(new[] { Cfg("Conf 1", Ww0), Cfg("Conf 2", Ww0, Ws0), Cfg("Conf 3", Wn0, Ws0) })
            .ToDictionary(x => x.Settore);

        Assert.Equal(new[] { Ws0 }, c[Wn0].SempreCon);   // falso nella realtà: WN0 sta da solo il 95% del tempo
        Assert.False(c[Ws0].DaSolo);                     // falso: WS0 sta da solo l'81% del tempo
        Assert.Empty(c[Ws0].SempreCon);                  // «WW0 o WN0» a coppie non si dice: lo dice «non da solo»
    }

    [Fact]
    public void I_nominati_escono_nell_ordine_in_cui_compaiono_e_una_volta_sola()
    {
        Assert.Equal(new[] { Ws2, Es2, Ws5, Es5 }, ConfigurazioniPossibili.Nominati(Milano));
    }
}
