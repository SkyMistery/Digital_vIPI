using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I limiti che DICHIARANO l'unita' non passano dall'euristica. Segnalato dal committente il 10 settembre
/// 2026: nell'AoR delle aree BOAT le quote uscivano in FL invece che in piedi.
/// </summary>
public class AorFlBandPiediTests
{
    /// <summary>
    /// 🔴 Il caso vero: <c>AT Basilicata</c>, 150 ft – 1000 ft. Con l'euristica il piede restava 150 («FL150»,
    /// perche' 150 &lt;= 660) e il tetto diventava FL10 — cioe' il tetto finiva SOTTO il piede, la banda
    /// degenerava, e il 3D disegnava un prisma a 15 000 piedi alto cento.
    /// </summary>
    [Fact]
    public void Un_area_bassa_con_l_euristica_finiva_a_quindicimila_piedi()
    {
        var (bottomVecchio, topVecchio) = AorFlBand.Normalize(150, 1000);
        Assert.Equal(150, bottomVecchio);      // letto come FL150
        Assert.Equal(151, topVecchio);         // e il tetto degenerato

        var (bottom, top) = AorFlBand.FromFeet(150, 1000);
        Assert.Equal(2, bottom);               // 150 ft → FL1,5 arrotondato
        Assert.Equal(10, top);                 // 1000 ft → FL10
    }

    /// <summary>Sulle aree ALTE le due strade coincidono: l'euristica sopra 660 divideva gia' per cento.</summary>
    [Theory]
    [InlineData(19500, 66000, 195, 660)]
    [InlineData(0, 32500, 0, 325)]
    public void Sulle_quote_alte_niente_cambia(int loFt, int hiFt, int loFl, int hiFl)
    {
        Assert.Equal((loFl, hiFl), AorFlBand.FromFeet(loFt, hiFt));
        Assert.Equal((loFl, hiFl), AorFlBand.Normalize(loFt, hiFt));
    }

    [Fact]
    public void Il_nulla_resta_suolo_e_illimitato() =>
        Assert.Equal((AorFlBand.Ground, AorFlBand.Unlimited), AorFlBand.FromFeet(null, null));

    /// <summary>Banda degenere: il tetto sta sempre sopra il piede, o il 3D non disegna niente.</summary>
    [Fact]
    public void Una_banda_degenere_resta_alta_un_livello() =>
        Assert.Equal((5, 6), AorFlBand.FromFeet(500, 500));

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0, "GND")]
    [InlineData(150, "150 ft")]
    public void Il_testo_di_una_quota_in_piedi(int? ft, string atteso) =>
        Assert.Equal(atteso, AorFlBand.FeetLabel(ft));

    /// <summary>⚠️ La banda che la MAPPA scrive e' la stessa che scrive la TABELLA: era il difetto.</summary>
    [Fact]
    public void La_banda_dell_area_di_Basilicata_si_legge_in_piedi() =>
        Assert.Equal("150 ft – 1000 ft", AorFlBand.FeetBand(150, 1000));

    [Fact]
    public void Piede_e_tetto_uguali_danno_un_valore_solo() =>
        Assert.Equal("1000 ft", AorFlBand.FeetBand(1000, 1000));
}
