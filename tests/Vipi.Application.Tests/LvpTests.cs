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

    // ─── L'isteresi: le soglie di cancellazione (revisione del 12 settembre 2026) ───────────────────

    [Fact] // 🔴 in vigore + dato risalito SOPRA la cancellazione ⇒ si propone di uscirne
    public void Risalito_Sopra_La_Cancellazione_Si_Puo_Cancellare()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: 1200, visibilitaM: null, ceilingFt: 900, giaInVigore: true);
        Assert.Equal(LvpStato.Cancellabile, e.Stato);
    }

    [Fact] // 🔴 in vigore + dato risalito solo SOPRA L'INGRESSO ⇒ restano in vigore, non si sfarfalla
    public void Fra_Ingresso_E_Cancellazione_Restano_In_Vigore()
    {
        // 700 m: sopra i 550 dell'ingresso, sotto gli 800 della cancellazione.
        var e = LvpValutatore.Valuta(Minimi(), rvrM: 700, visibilitaM: null, ceilingFt: 900, giaInVigore: true);
        Assert.Equal(LvpStato.InVigore, e.Stato);

        // Senza la memoria, lo stesso dato è solo «preparazione»: è tutta la differenza che fa l'isteresi.
        Assert.Equal(LvpStato.Preparazione,
            LvpValutatore.Valuta(Minimi(), rvrM: 700, visibilitaM: null, ceilingFt: 900).Stato);
    }

    [Fact] // 🔴 per USCIRE devono essere risalite TUTT'E DUE: si entra con «o», si esce con «e»
    public void Per_Uscire_Servono_Tutte_E_Due_Le_Misure()
    {
        // RVR ottimo ma soffitto ancora a 100 ft: non si propone di cancellare niente.
        var e = LvpValutatore.Valuta(Minimi(), rvrM: 2000, visibilitaM: null, ceilingFt: 100, giaInVigore: true);
        Assert.Equal(LvpStato.InVigore, e.Stato);
    }

    [Fact] // nessuna soglia di cancellazione dichiarata: non si propone niente, e a togliere e' una persona
    public void Senza_Soglie_Di_Cancellazione_Non_Si_Propone()
    {
        var senza = new LvpRow(1, true, 800, 300, 550, 200, null, null, null);
        var e = LvpValutatore.Valuta(senza, rvrM: 5000, visibilitaM: null, ceilingFt: 5000, giaInVigore: true);
        Assert.Equal(LvpStato.InVigore, e.Stato);
    }

    [Fact] // una misura che MANCA non fa risalire niente: non si dichiara migliorato quel che non si misura
    public void Una_Misura_Mancante_Non_Fa_Cancellare()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: null, visibilitaM: null, ceilingFt: 900, giaInVigore: true);
        Assert.Equal(LvpStato.InVigore, e.Stato);
    }

    [Fact] // sotto la soglia d'ingresso resta «in vigore» anche con la memoria: l'isteresi non la scavalca
    public void Sotto_L_Ingresso_Resta_In_Vigore()
    {
        Assert.Equal(LvpStato.InVigore,
            LvpValutatore.Valuta(Minimi(), rvrM: 300, visibilitaM: null, ceilingFt: 100, giaInVigore: true).Stato);
    }

    [Fact] // un documento non ha memoria: passa false e la cancellazione non si presenta MAI
    public void Senza_Memoria_La_Cancellazione_Non_Esiste()
    {
        var e = LvpValutatore.Valuta(Minimi(), rvrM: 1200, visibilitaM: null, ceilingFt: 900);
        Assert.Equal(LvpStato.Nil, e.Stato);
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
