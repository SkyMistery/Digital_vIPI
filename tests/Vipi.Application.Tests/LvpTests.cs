using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La valutazione dei minimi LVP: che cosa <b>suggerisce</b> il quadro col bollettino di adesso.
/// Carta 2026-09-12, §5.6. Puro e deterministico: niente banco.
/// </summary>
public class LvpTests
{
    private static LvpRow Minimi(int? prepRvr = 800, int? prepCeil = 300, int? lvpRvr = 550, int? lvpCeil = 200,
                                 bool declared = true) =>
        new(1, declared, prepRvr, prepCeil, lvpRvr, lvpCeil, 800, 300, null);

    [Fact] // sopra tutte le soglie: NIL
    public void Sopra_Le_Soglie_Nil()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: null, visibilitaM: 10000, ceilingFt: 3000);
        Assert.Equal(LvpStato.Nil, e.Stato);
    }

    [Fact] // l'RVR sotto la soglia preparatoria accende la preparazione
    public void Rvr_Sotto_Preparazione()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: 700, visibilitaM: 700, ceilingFt: 3000);
        Assert.Equal(LvpStato.Preparazione, e.Stato);
        Assert.Equal(LvpMisura.Rvr, e.Misura);
    }

    [Fact] // l'RVR sotto la soglia di LVP in vigore vince sulla preparazione
    public void Rvr_Sotto_In_Vigore()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: 350, visibilitaM: 400, ceilingFt: 3000);
        Assert.Equal(LvpStato.InVigore, e.Stato);
        Assert.Equal(350, e.RvrM);
    }

    [Fact] // basta il SOFFITTO, anche con la visibilità perfetta: le procedure dicono «or», non «and»
    public void Basta_Il_Soffitto()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: null, visibilitaM: 10000, ceilingFt: 150);
        Assert.Equal(LvpStato.InVigore, e.Stato);
    }

    [Fact] // 🔴 senza RVR si usa la visibilità, e lo si DICHIARA: non sono la stessa misura
    public void Senza_Rvr_La_Visibilita_Si_Dichiara()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: null, visibilitaM: 500, ceilingFt: null);
        Assert.Equal(LvpStato.InVigore, e.Stato);
        Assert.Equal(LvpMisura.Visibilita, e.Misura);
    }

    [Fact] // una soglia che lo scalo non ha scritto non si valuta: non è «zero»
    public void Soglia_Mancante_Non_Si_Valuta()
    {
        // Solo l'RVR dichiarato: un soffitto bassissimo non deve far scattare niente.
        var soloRvr = Minimi(prepCeil: null, lvpCeil: null);
        var e = LvpValutatore.Valuta(soloRvr, rvrM: 2000, visibilitaM: 10000, ceilingFt: 50);
        Assert.Equal(LvpStato.Nil, e.Stato);
    }

    [Fact] // lo scalo dichiara di NON operare in LVP: è un'informazione, non un silenzio
    public void Non_Applicabile()
    {
        var e = LvpValutatore.Valuta(Minimi(declared: false), rvrM: 100, visibilitaM: 100, ceilingFt: 50);
        Assert.Equal(LvpStato.NonApplicabile, e.Stato);
    }

    [Fact] // 🔴 minimi non dichiarati: si usa lo STANDARD e lo si dice
    public void Senza_Minimi_Si_Usa_Lo_Standard_E_Si_Dice()
    {
        var e = LvpValutatore.Valuta(null, rvrM: 400, visibilitaM: null, ceilingFt: null);
        Assert.Equal(LvpStato.InVigore, e.Stato);
        Assert.False(e.DaiMinimiDelloScalo);

        var suo = LvpValutatore.Valuta(Minimi(), rvrM: 400, visibilitaM: null, ceilingFt: null);
        Assert.True(suo.DaiMinimiDelloScalo);
    }

    [Fact] // una riga senza nessuna soglia non dice niente, e non si finge che dica NIL
    public void Riga_Vuota_Non_Valutabile()
    {
        var vuota = new LvpRow(1, true, null, null, null, null, null, null, null);
        Assert.Equal(LvpStato.NonValutabile, LvpValutatore.Valuta(vuota, 100, 100, 50).Stato);
    }

    [Fact] // senza dato meteo non si valuta niente
    public void Senza_Dato_Meteo_Non_Valutabile()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: null, visibilitaM: null, ceilingFt: null);
        Assert.Equal(LvpStato.NonValutabile, e.Stato);
    }

    [Fact] // la soglia preparatoria è «≤», quella in vigore è «<»: sul valore esatto si è in preparazione
    public void Sul_Valore_Esatto_Si_Prepara()
    {
        Assert.Equal(LvpStato.Preparazione, LvpValutatore.Valuta(Minimi(), 800, null, null).Stato);
        Assert.Equal(LvpStato.Preparazione, LvpValutatore.Valuta(Minimi(), 550, null, null).Stato);
        Assert.Equal(LvpStato.InVigore, LvpValutatore.Valuta(Minimi(), 549, null, null).Stato);
    }

    [Fact] // i valori standard sono quelli scritti nella carta, in un posto solo
    public void Lo_Standard_E_Quello_Della_Carta()
    {
        var r = LvpStandard.Riga;
        Assert.True(r.Declared);
        Assert.Equal(800, r.PrepRvrM);
        Assert.Equal(300, r.PrepCeilingFt);
        Assert.Equal(550, r.LvpRvrM);
        Assert.Equal(200, r.LvpCeilingFt);
        Assert.Equal(800, r.CancelRvrM);
        Assert.Equal(300, r.CancelCeilingFt);
    }
}
