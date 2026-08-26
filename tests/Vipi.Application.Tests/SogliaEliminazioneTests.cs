using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La regola che autorizza a eliminare ciò che viene da una sorgente: si toglie solo quel che la sorgente
/// non manda da <b>due</b> giri. Questi test fissano la soglia dove sta, perché è l'unica cosa che separa
/// «la fonte l'ha tolto» da «stanotte la fonte ha risposto vuoto».
/// </summary>
public class SogliaEliminazioneTests
{
    private static readonly DateTime Adesso = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Una_riga_confermata_dopo_il_penultimo_giro_non_si_elimina()
    {
        var penultimo = Adesso.AddDays(-1);
        var timbro = Adesso.AddHours(-2);   // vista dopo il penultimo giro: la sorgente la manda ancora
        Assert.False(SogliaEliminazione.Consentita(timbro, penultimo, isManual: false));
    }

    [Fact]
    public void Una_riga_ferma_prima_del_penultimo_giro_si_elimina()
    {
        var penultimo = Adesso.AddDays(-1);
        var timbro = Adesso.AddDays(-3);    // due giri l'hanno taciuta
        Assert.True(SogliaEliminazione.Consentita(timbro, penultimo, isManual: false));
    }

    [Fact]
    public void Senza_penultimo_giro_non_si_elimina_niente()
    {
        // Un giro solo riuscito: «non lo sappiamo» non è «è sparita». È la stessa guardia dell'avvio a freddo.
        Assert.False(SogliaEliminazione.Consentita(Adesso.AddDays(-9), prevSuccessUtc: null, isManual: false));
        Assert.False(SogliaEliminazione.Consentita(null, prevSuccessUtc: null, isManual: false));
    }

    [Fact]
    public void Una_riga_a_mano_si_elimina_sempre()
    {
        // La sorgente non l'ha mai mandata: il suo timbro non dice niente e la regola non la riguarda.
        Assert.True(SogliaEliminazione.Consentita(null, prevSuccessUtc: null, isManual: true));
        Assert.True(SogliaEliminazione.Consentita(Adesso, Adesso.AddDays(-1), isManual: true));
    }

    [Fact]
    public void Senza_timbro_ma_con_due_giri_alle_spalle_si_elimina()
    {
        // Se fosse ancora nella sorgente, uno dei due giri l'avrebbe timbrata.
        Assert.True(SogliaEliminazione.Consentita(null, Adesso.AddDays(-1), isManual: false));
    }

    [Fact]
    public void Il_penultimo_non_scorre_se_i_due_giri_sono_troppo_vicini()
    {
        // ⚠️ La trappola dei due clic: due re-import di fila non devono «consumare» le due conferme.
        var ultimo = Adesso.AddMinutes(-5);
        Assert.False(SogliaEliminazione.IlPenultimoScorre(ultimo, Adesso));
    }

    [Fact]
    public void Il_penultimo_scorre_quando_e_passato_abbastanza()
    {
        Assert.True(SogliaEliminazione.IlPenultimoScorre(Adesso.AddHours(-2), Adesso));
        Assert.True(SogliaEliminazione.IlPenultimoScorre(null, Adesso));   // il primo giro in assoluto
    }

    // ── La scorciatoia: chiedere invece di aspettare ─────────────────────────────────────────────────

    [Fact]
    public void La_prova_puntuale_batte_l_attesa_dei_due_giri()
    {
        // La sorgente l'ha mandata due ore fa, ma interrogata ADESSO ha detto che non ce l'ha più: fra una
        // deduzione dal silenzio e una constatazione, vince la constatazione.
        var penultimo = Adesso.AddDays(-1);
        var timbro = Adesso.AddHours(-2);
        Assert.False(SogliaEliminazione.Consentita(timbro, penultimo, isManual: false));
        Assert.True(SogliaEliminazione.Consentita(timbro, penultimo, isManual: false, provaDiAssenza: true));
    }

    [Fact]
    public void La_prova_puntuale_vale_anche_senza_nessuna_storia()
    {
        // È il caso che rende il meccanismo urgente: DB appena ripulito, ImportState azzerato, nessun
        // penultimo giro. Senza la domanda puntuale non si potrebbe eliminare NIENTE per due giri interi.
        Assert.False(SogliaEliminazione.Consentita(null, prevSuccessUtc: null, isManual: false));
        Assert.True(SogliaEliminazione.Consentita(null, prevSuccessUtc: null, isManual: false, provaDiAssenza: true));
    }

    [Fact]
    public void Senza_prova_la_soglia_resta_quella_di_prima()
    {
        // ⚠️ Il parametro è false di default e non deve cambiare niente: «non si sa» non è «è sparita», e
        // un verdetto NonSiSa arriva qui esattamente come se nessuno avesse chiesto.
        var penultimo = Adesso.AddDays(-1);
        Assert.False(SogliaEliminazione.Consentita(Adesso.AddHours(-2), penultimo, isManual: false, provaDiAssenza: false));
        Assert.NotNull(SogliaEliminazione.MotivoDelRifiuto(Adesso.AddHours(-2), penultimo, isManual: false, provaDiAssenza: false));
    }

    [Fact]
    public void Con_la_prova_non_c_e_piu_niente_da_spiegare()
    {
        Assert.Null(SogliaEliminazione.MotivoDelRifiuto(Adesso, Adesso.AddDays(-1), isManual: false, provaDiAssenza: true));
        Assert.Null(SogliaEliminazione.MotivoDelRifiuto(Adesso.AddDays(-9), null, isManual: false, provaDiAssenza: true));
    }

    [Fact]
    public void Il_rifiuto_si_spiega_in_una_frase()
    {
        Assert.Contains("meno di due volte",
            SogliaEliminazione.MotivoDelRifiuto(Adesso.AddDays(-9), null, isManual: false));
        Assert.Contains("la manda ancora",
            SogliaEliminazione.MotivoDelRifiuto(Adesso, Adesso.AddDays(-1), isManual: false));
        Assert.Null(SogliaEliminazione.MotivoDelRifiuto(Adesso.AddDays(-3), Adesso.AddDays(-1), isManual: false));
    }
}
