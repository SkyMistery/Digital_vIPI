using Vipi.Application.Live;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «Chi controlla questo aeroporto adesso» (voce E3). La risposta non è una sola: al gate serve il ground,
/// in avvicinamento la torre, e ciò che in loco non è presidiato lo copre qualcuno più in alto. Questi test
/// fissano proprio quella forma — elenco locale + copertura — perché è la scelta che distingue questa
/// risposta da un «sì/no» come quello che c'era prima.
/// </summary>
public class AirportPresidencyResolverTests
{
    private static readonly (string, SectorType)[] Posizioni =
    {
        ("LIRF_DEL", SectorType.Del),
        ("LIRF_GND", SectorType.Gnd),
        ("LIRF_TWR", SectorType.Twr),
        ("LIRF_APP", SectorType.App),
    };

    private static readonly string[] Antenati = { "LIRR_NE_CTR", "LIRR_CTR" };

    private static HashSet<string> Online(params string[] cs) => new(cs, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Le_posizioni_locali_online_escono_dalla_piu_locale_alla_piu_estesa()
    {
        var p = AirportPresidencyResolver.Resolve(Posizioni, Antenati, Online("LIRF_TWR", "LIRF_GND", "LIRF_APP"));

        Assert.Equal(new[] { "LIRF_GND", "LIRF_TWR", "LIRF_APP" }, p.Local.Select(x => x.Callsign));
        Assert.All(p.Local, s => Assert.True(s.IsAirportOwn));
        Assert.Null(p.Covering);   // c'è già chi copre in loco fino all'avvicinamento
        Assert.False(p.Unicom);
    }

    [Fact]
    public void Se_in_loco_non_c_e_nessuno_risponde_il_primo_antenato_online()
    {
        // È il caso che la vecchia logica sbagliava: diceva «non delegato» e si fermava lì.
        var p = AirportPresidencyResolver.Resolve(Posizioni, Antenati, Online("LIRR_CTR"));

        Assert.Empty(p.Local);
        Assert.NotNull(p.Covering);
        Assert.Equal("LIRR_CTR", p.Covering!.Callsign);
        Assert.False(p.Covering.IsAirportOwn);
        Assert.False(p.Unicom);
    }

    [Fact]
    public void Fra_gli_antenati_vince_il_piu_vicino_non_il_primo_online_qualsiasi()
    {
        var p = AirportPresidencyResolver.Resolve(Posizioni, Antenati, Online("LIRR_CTR", "LIRR_NE_CTR"));

        Assert.Equal("LIRR_NE_CTR", p.Covering!.Callsign);   // la catena è ordinata: si prende chi viene prima
    }

    [Fact]
    public void Con_solo_il_ground_online_il_resto_lo_copre_chi_sta_sopra()
    {
        // Il caso che ha motivato la forma scelta: a terra rispondi al ground, ma chi arriva chiama l'ACC.
        var p = AirportPresidencyResolver.Resolve(Posizioni, Antenati, Online("LIRF_GND", "LIRR_NE_CTR"));

        Assert.Equal(new[] { "LIRF_GND" }, p.Local.Select(x => x.Callsign));
        Assert.Equal("LIRR_NE_CTR", p.Covering!.Callsign);
    }

    [Fact]
    public void Nessuno_online_ne_in_loco_ne_sopra_significa_unicom()
    {
        var p = AirportPresidencyResolver.Resolve(Posizioni, Antenati, Online("LIMM_WS2_CTR"));

        Assert.Empty(p.Local);
        Assert.Null(p.Covering);
        Assert.True(p.Unicom);
        Assert.False(p.AnyOnline);
    }

    /// <summary>
    /// Il padre di uno scalo è spesso il suo stesso avvicinamento: senza una guardia, quello comparirebbe
    /// due volte — fra le posizioni locali e come copertura — e la pagina direbbe due volte la stessa cosa.
    /// </summary>
    [Fact]
    public void Un_avvicinamento_dell_aeroporto_non_compare_anche_come_copertura()
    {
        var p = AirportPresidencyResolver.Resolve(
            Posizioni,
            new[] { "LIRF_APP", "LIRR_NE_CTR" },     // il padre dell'aeroporto È il suo APP
            Online("LIRF_APP"));

        Assert.Equal(new[] { "LIRF_APP" }, p.Local.Select(x => x.Callsign));
        Assert.Null(p.Covering);
    }

    /// <summary>
    /// La regola di confronto è quella condivisa coi trasferimenti, non una nuova: un callsign online copre
    /// anche il candidato che ne sia un segmento. Qui lo si verifica dal di fuori, perché è la proprietà che
    /// tiene allineate le due schermate — se un domani cambia, deve cambiare per entrambe.
    /// </summary>
    [Fact]
    public void Usa_la_stessa_regola_di_confronto_dei_trasferimenti()
    {
        var p = AirportPresidencyResolver.Resolve(
            new[] { ("LIRF", SectorType.Twr) }, Array.Empty<string>(), Online("LIRF_TWR"));

        Assert.Single(p.Local);
        Assert.Equal("LIRF", p.Local[0].Callsign);
    }
}
