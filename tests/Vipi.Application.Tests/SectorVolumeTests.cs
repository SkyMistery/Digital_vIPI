using Vipi.Application.Aor;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il volume di competenza di un settore: poligono (orizzontale) E banda di quota (verticale).
/// La regola del committente: «un traffico che sorvola a FL260 uno spazio che finisce a FL195 non è stato
/// gestito da quello spazio». Puro, nessun I/O.
/// </summary>
public class SectorVolumeTests
{
    // Quadrato di 2°×2° attorno a (42N, 12E), nel formato reale del catalogo: [[lon,lat],…].
    private const string Square = "[[11,41],[13,41],[13,43],[11,43]]";

    private static SectorVolume Volume(int? lower, int? upper) =>
        SectorVolume.From("LIRR_TEST_CTR", Square, lower, upper)!;

    [Fact]
    public void Un_punto_dentro_al_poligono_e_dentro_alla_banda_e_gestito()
    {
        var v = Volume(null, 19500);                       // GND → FL195
        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 10_000));
    }

    [Fact]
    public void Sopra_il_tetto_del_settore_NON_e_gestito()
    {
        var v = Volume(null, 19500);                       // tetto FL195
        Assert.False(v.Contains(42.0, 12.0, altitudeFt: 26_000));   // FL260 di passaggio
    }

    [Fact]
    public void Sotto_il_pavimento_del_settore_NON_e_gestito()
    {
        var v = Volume(24500, 32500);                      // FL245 → FL325
        Assert.False(v.Contains(42.0, 12.0, altitudeFt: 12_000));
        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 30_000));
    }

    [Fact]
    public void Fuori_dal_poligono_non_e_gestito_a_nessuna_quota()
    {
        var v = Volume(null, null);                        // GND → UNL
        Assert.False(v.Contains(45.0, 9.0, altitudeFt: 10_000));    // Milano, fuori dal quadrato
        Assert.False(v.Contains(45.0, 9.0, altitudeFt: 0));
    }

    [Fact]
    public void Un_limite_vuoto_vale_0_ft_sotto_e_66000_ft_sopra()
    {
        // ⚠️ Regola del committente (24 agosto): il campo vuoto NON è un dato mancante, è il valore —
        // inferiore vuoto = suolo, superiore vuoto = 66 000 ft, che è poi UNL. 138 settori ACC su 153 stanno
        // così ed è giusto: chi un domani li «completasse» a mano restringerebbe volumi corretti.
        var v = Volume(null, null);

        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 0));          // al suolo
        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 41_000));
        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 66_000));     // il tetto è compreso
        Assert.False(v.Contains(42.0, 12.0, altitudeFt: 67_000));    // sopra no

        // Scriverlo a mano come UNL dà lo stesso volume: sono due modi di dire la stessa cosa.
        var esplicito = Volume(0, 66_000);
        Assert.Equal((v.BottomFl, v.TopFl), (esplicito.BottomFl, esplicito.TopFl));
    }

    [Fact]
    public void Tetto_nullo_significa_senza_limite_non_zero()
    {
        var v = Volume(0, null);                           // il caso reale di LIBB_ES_CTR
        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 41_000));
    }

    [Fact]
    public void I_limiti_seguono_l_euristica_di_AorFlBand_non_una_seconda_regola()
    {
        var feet = Volume(2500, 19500);                    // > 660 → piedi
        var fl = Volume(25, 195);                          // <= 660 → già FL
        Assert.Equal((feet.BottomFl, feet.TopFl), (fl.BottomFl, fl.TopFl));
        Assert.Equal((25, 195), (feet.BottomFl, feet.TopFl));
    }

    [Fact]
    public void Un_poligono_assente_o_degenere_non_produce_volume()
    {
        Assert.Null(SectorVolume.From("LIRF_TWR", null, 0, 19500));
        Assert.Null(SectorVolume.From("LIRF_TWR", "[[11,41],[13,41]]", 0, 19500));   // due soli punti
        Assert.Null(SectorVolume.From("LIRF_TWR", "non json", 0, 19500));
    }

    [Fact]
    public void La_quota_del_pilota_e_in_piedi_e_si_confronta_in_FL()
    {
        var v = Volume(null, 195);                         // tetto FL195 espresso già in FL
        Assert.True(v.Contains(42.0, 12.0, altitudeFt: 19_400));     // FL194
        Assert.False(v.Contains(42.0, 12.0, altitudeFt: 19_600));    // FL196
    }
}

/// <summary>Punto-in-poligono su <see cref="PolygonGeometry"/>: il pezzo che mancava alla geometria esistente.</summary>
public class PolygonContainsTests
{
    private static PolygonGeometry.Ring Ring(string json) => PolygonGeometry.ToRing(json)!;

    [Fact]
    public void Dentro_e_fuori_un_quadrato()
    {
        var r = Ring("[[11,41],[13,41],[13,43],[11,43]]");
        Assert.True(PolygonGeometry.Contains(r, 42, 12));
        Assert.False(PolygonGeometry.Contains(r, 42, 14));
        Assert.False(PolygonGeometry.Contains(r, 40, 12));
    }

    [Fact]
    public void Un_poligono_concavo_esclude_l_insenatura()
    {
        // «C» che si apre a est: il punto (42, 12.5) sta nell'insenatura, fuori dal poligono.
        var c = Ring("[[11,41],[13,41],[13,41.5],[11.5,41.5],[11.5,42.5],[13,42.5],[13,43],[11,43]]");
        Assert.True(PolygonGeometry.Contains(c, 41.2, 12.5));    // braccio inferiore
        Assert.True(PolygonGeometry.Contains(c, 42.8, 12.5));    // braccio superiore
        Assert.False(PolygonGeometry.Contains(c, 42.0, 12.5));   // insenatura
    }

    [Fact]
    public void Il_bounding_box_scarta_subito_i_punti_lontani()
    {
        var r = Ring("[[11,41],[13,41],[13,43],[11,43]]");
        Assert.False(PolygonGeometry.Contains(r, 60, 60));
        Assert.False(PolygonGeometry.Contains(r, -10, -10));
    }

    [Fact]
    public void Un_anello_ripetuto_due_volte_contiene_lo_stesso_di_uno_solo()
    {
        // ⚠️ Difetto vero, trovato da una domanda del committente sui settori annidati: certe shape della
        // sorgente ripetono l'identico contorno. Col test pari/dispari l'anello doppio SI ANNULLA — ogni
        // attraversamento contato due volte, parità sempre pari — e il settore non contiene più niente.
        // Nel vipi.db reale capita a `LIRR_TS_CTR`, che infatti nell'attribuzione non compariva mai.
        var singolo = Ring("[[11,41],[13,41],[13,43],[11,43],[11,41]]");
        var doppio = Ring("[[11,41],[13,41],[13,43],[11,43],[11,41],[11,41],[13,41],[13,43],[11,43],[11,41]]");

        Assert.True(PolygonGeometry.Contains(singolo, 42, 12));
        Assert.True(PolygonGeometry.Contains(doppio, 42, 12));      // senza la correzione: false
        Assert.False(PolygonGeometry.Contains(doppio, 42, 14));
        Assert.Equal(singolo.Points.Count, doppio.Points.Count);
    }

    [Fact]
    public void Un_anello_normale_non_viene_toccato_dalla_correzione()
    {
        // Un contorno che per caso ha lo stesso numero pari di punti non deve perderne la metà.
        var r = Ring("[[11,41],[12,40.5],[13,41],[13,43],[12,43.5],[11,43]]");
        Assert.Equal(6, r.Points.Count);
        Assert.True(PolygonGeometry.Contains(r, 42, 12));
    }

    [Fact]
    public void Un_anello_nullo_non_contiene_niente()
    {
        Assert.False(PolygonGeometry.Contains(null, 42, 12));
    }

    [Fact]
    public void Il_poligono_vale_chiuso_anche_se_il_primo_punto_non_e_ripetuto()
    {
        // Il catalogo IVAO non ripete il punto di chiusura: il lato ultimo→primo esiste lo stesso.
        var aperto = Ring("[[11,41],[13,41],[13,43],[11,43]]");
        var chiuso = Ring("[[11,41],[13,41],[13,43],[11,43],[11,41]]");
        Assert.Equal(PolygonGeometry.Contains(aperto, 42, 11.2), PolygonGeometry.Contains(chiuso, 42, 11.2));
        Assert.True(PolygonGeometry.Contains(aperto, 42, 11.2));
    }
}
