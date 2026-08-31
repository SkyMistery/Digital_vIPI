using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La catena di ripiego con la quota. Lo scenario è quello della carta
/// (<c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2): Milano divisa in due su due strati.
///
/// <list type="table">
/// <item><term>WS2</term><description>ovest, SFC – FL305</description></item>
/// <item><term>ES2</term><description>est, SFC – FL305</description></item>
/// <item><term>WS5</term><description>ovest, FL305 – UNL</description></item>
/// <item><term>ES5</term><description>est, FL305 – UNL</description></item>
/// </list>
///
/// Albero: WS2 radice, ES2 e WS5 figli di WS2, ES5 figlio di ES2.
/// </summary>
public class FallbackChainTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";

    private static readonly Dictionary<string, string?> Padri = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2,
    };

    private static string? Padre(string cs) => Padri.GetValueOrDefault(cs);

    /// <summary>Le due righe che l'admin conferma su proposta di B: sopra FL305 il sostituto è l'altro alto.</summary>
    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> Dichiarate = new(StringComparer.OrdinalIgnoreCase)
    {
        [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: 30500, TopFeet: null) },
        [Ws5] = new[] { new FallbackRow(Es5, BaseFeet: 30500, TopFeet: null) },
    };

    private static IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> Nessuna =>
        new Dictionary<string, IReadOnlyList<FallbackRow>>();

    private static (string Handler, bool Online) Risolvi(string ricevente, int? quotaFt, params string[] online) =>
        TransferOnlineResolver.Resolve(
            FallbackChain.Candidates(ricevente, quotaFt, Dichiarate, Padre),
            new HashSet<string>(online, StringComparer.OrdinalIgnoreCase));

    // =====================================================================================================
    //  I quattro casi della carta
    // =====================================================================================================

    /// <summary>Caso 1 — il difetto. A FL350 il traffico dell'alto est va all'alto ovest, non al basso est.</summary>
    [Fact]
    public void A_FL350_con_WS5_aperto_il_traffico_di_ES5_va_a_WS5()
    {
        var (handler, online) = Risolvi(Es5, quotaFt: 35000, Ws2, Es2, Ws5);

        Assert.Equal(Ws5, handler);
        Assert.True(online);
    }

    /// <summary>Caso 2 — il punto della carta: la stessa tabella dà una risposta diversa a quota diversa.</summary>
    [Fact]
    public void A_FL250_lo_stesso_punto_va_a_ES2()
    {
        var (handler, _) = Risolvi(Es5, quotaFt: 25000, Ws2, Es2, Ws5);

        Assert.Equal(Es2, handler);
    }

    /// <summary>Caso 3 — con solo il capo online il risultato è quello di sempre: nessuna regressione.</summary>
    [Fact]
    public void Con_solo_WS2_online_si_ricade_su_WS2_come_prima()
    {
        var (handler, _) = Risolvi(Es5, quotaFt: 35000, Ws2);

        Assert.Equal(Ws2, handler);
    }

    /// <summary>Caso 4 — un punto senza quota non si risolve in verticale, e va bene così.</summary>
    [Fact]
    public void Senza_quota_le_righe_con_fascia_si_saltano()
    {
        var (handler, _) = Risolvi(Es5, quotaFt: null, Ws2, Es2, Ws5);

        Assert.Equal(Es2, handler);
    }

    /// <summary>Nessuno online: il traffico va su UNICOM, come prima.</summary>
    [Fact]
    public void Nessuno_online_resta_UNICOM()
    {
        var (handler, online) = Risolvi(Es5, quotaFt: 35000);

        Assert.Equal(TransferOnlineResolver.Unicom, handler);
        Assert.False(online);
    }

    // =====================================================================================================
    //  L'ordine dei candidati
    // =====================================================================================================

    /// <summary>
    /// ⚠️ In ampiezza, non in profondità. In profondità l'ordine sarebbe <c>ES5, WS5, WS2, ES2</c> e con WS2
    /// ed ES2 entrambi online il traffico dell'est finirebbe a ovest, scavalcando il proprio padre.
    /// </summary>
    [Fact]
    public void I_candidati_escono_per_distanza_non_per_ramo()
    {
        var c = FallbackChain.Candidates(Es5, 35000, Dichiarate, Padre);

        Assert.Equal(new[] { Es5, Ws5, Es2, Ws2 }, c);
    }

    [Fact]
    public void Il_ricevente_e_sempre_il_primo_candidato()
    {
        Assert.Equal(Es5, FallbackChain.Candidates(Es5, 35000, Dichiarate, Padre)[0]);
    }

    /// <summary>Due settori che si citano a vicenda non fanno girare a vuoto la risoluzione.</summary>
    [Fact]
    public void Un_anello_fra_righe_dichiarate_non_ripete_i_candidati()
    {
        var c = FallbackChain.Candidates(Es5, 35000, Dichiarate, Padre);

        Assert.Equal(c.Count, c.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>Un anello nei PADRI (dato sporco in archivio) non deve piantare la risoluzione.</summary>
    [Fact]
    public void Un_anello_fra_i_padri_non_pianta_la_risoluzione()
    {
        var padriMalati = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B", ["B"] = "A",
        };

        Assert.Equal(new[] { "A", "B" }, FallbackChain.Candidates("A", 35000, Nessuna, cs => padriMalati.GetValueOrDefault(cs)));
    }

    /// <summary>Senza righe dichiarate la catena È la catena dei padri: il comportamento di prima, intatto.</summary>
    [Fact]
    public void A_tabella_vuota_la_catena_e_quella_dei_padri()
    {
        Assert.Equal(new[] { Es5, Es2, Ws2 }, FallbackChain.Candidates(Es5, 35000, Nessuna, Padre));
    }

    [Fact]
    public void Un_callsign_vuoto_non_produce_candidati()
    {
        Assert.Empty(FallbackChain.Candidates("  ", 35000, Dichiarate, Padre));
    }

    // =====================================================================================================
    //  La fascia
    // =====================================================================================================

    /// <summary>Piede incluso, tetto escluso: FL305 va all'alta, non alla bassa.</summary>
    [Theory]
    [InlineData(30400, false)]
    [InlineData(30500, true)]
    [InlineData(35000, true)]
    public void Il_piede_della_fascia_e_incluso(int quotaFt, bool atteso) =>
        Assert.Equal(atteso, new FallbackRow("X", 30500, null).AppliesAt(quotaFt));

    [Theory]
    [InlineData(0, true)]
    [InlineData(30400, true)]
    [InlineData(30500, false)]
    public void Il_tetto_della_fascia_e_escluso(int quotaFt, bool atteso) =>
        Assert.Equal(atteso, new FallbackRow("X", null, 30500).AppliesAt(quotaFt));

    [Fact]
    public void Una_riga_senza_fascia_vale_sempre_anche_senza_quota()
    {
        Assert.True(new FallbackRow("X", null, null).AppliesAt(35000));
        Assert.True(new FallbackRow("X", null, null).AppliesAt(null));
    }

    [Fact]
    public void Una_riga_con_fascia_non_si_valuta_senza_quota()
    {
        Assert.False(new FallbackRow("X", 30500, null).AppliesAt(null));
        Assert.False(new FallbackRow("X", null, 30500).AppliesAt(null));
    }

    // =====================================================================================================

    [Theory]
    [InlineData(350, LevelUnit.Fl, 35000)]
    [InlineData(5000, LevelUnit.Feet, 5000)]
    [InlineData(null, LevelUnit.Fl, null)]
    public void FeetOf_converte_il_livello(int? valore, LevelUnit unita, int? atteso) =>
        Assert.Equal(atteso, FallbackChain.FeetOf(valore, unita));
}
