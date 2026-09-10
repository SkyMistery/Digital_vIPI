using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La catena di ripiego con la quota. Lo scenario e' quello della carta
/// (<c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2), e ⚠️ <b>e' MISURATO sul
/// <c>vipi.db</c> reale</b>, non assunto: la prima stesura della carta dava per buono un albero diverso
/// (ES5 figlio di ES2) e uno split a FL305, e il dato l'ha smentita su tutti e due i punti.
///
/// <list type="table">
/// <item><term>WS2</term><description>ovest, SFC – FL325, radice</description></item>
/// <item><term>ES2</term><description>est, SFC – FL325, figlio di WS2</description></item>
/// <item><term>WS5</term><description>ovest, FL325 – UNL, figlio di WS2</description></item>
/// <item><term>ES5</term><description>est, FL325 – UNL, figlio di <b>ES2</b></description></item>
/// </list>
///
/// <para>⚠️ <b>Il padre di ES5 e' stato corretto il 10 settembre 2026.</b> Questo commento diceva «figlio di
/// WS5», misurato sul <c>vipi.db</c> di sviluppo — e il <c>vipi.db</c> di sviluppo era <b>sbagliato</b>: in
/// produzione (letto su <c>atc.it.ivao.aero</c> il 9 settembre) <c>LIMM_ES5_CTR</c> pende da
/// <c>LIMM_ES2_CTR</c>. Il DB locale e' stato riallineato; la lezione e' che «misurato sul dato vero» vale
/// quanto vale il dato che si e' guardato.</para>
///
/// <para>E' questa forma dell'albero a decidere quale caso e' rotto, e con l'albero vero i casi rotti sono
/// <b>due</b>, non uno. Con <b>WS5</b> chiuso la catena salta a WS2, che sopra FL325 non ha niente, mentre
/// quel cielo lo tiene ES5. E con <b>ES5</b> chiuso la catena salta a ES2, che sopra FL325 non ha niente
/// nemmeno lui, mentre quel cielo lo tiene WS5. L'albero <b>non puo'</b> dirlo in nessuno dei due versi: i
/// due settori alti stanno su rami diversi, e nessuno dei due e' antenato dell'altro.</para>
/// </summary>
public class FallbackChainTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";

    /// <summary>FL325: il piede MISURATO dello strato alto di Milano sul vipi.db reale.</summary>
    private const int Split = 32500;

    private static readonly Dictionary<string, string?> Padri = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2,
    };

    private static string? Padre(string cs) => Padri.GetValueOrDefault(cs);

    /// <summary>Le due righe che l'admin conferma su proposta di B: sopra FL325 il sostituto è l'altro alto.</summary>
    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> Dichiarate = new(StringComparer.OrdinalIgnoreCase)
    {
        [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: Split, TopFeet: null) },
        [Ws5] = new[] { new FallbackRow(Es5, BaseFeet: Split, TopFeet: null) },
    };

    private static IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> Nessuna =>
        new Dictionary<string, IReadOnlyList<FallbackRow>>();

    /// <summary>Un albero minimo dove riga dichiarata e padre puntano allo STESSO settore: e' il solo modo di
    /// tenere in piedi la proprieta' «due motivi, un candidato» senza appenderla alla forma di Milano.</summary>
    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> RigaVersoIlPadre =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FIGLIO"] = new[] { new FallbackRow("PADRE", BaseFeet: Split, TopFeet: null) },
        };

    private static string? PadreDiFiglio(string cs) =>
        cs.Equals("FIGLIO", StringComparison.OrdinalIgnoreCase) ? "PADRE" : null;

    private static (string Handler, bool Online) Risolvi(string ricevente, int? quotaFt, params string[] online) =>
        TransferOnlineResolver.Resolve(
            FallbackChain.Candidates(ricevente, quotaFt, Dichiarate, Padre),
            new HashSet<string>(online, StringComparer.OrdinalIgnoreCase));

    // =====================================================================================================
    //  I quattro casi della carta
    // =====================================================================================================

    /// <summary>
    /// Caso 1 — <b>il difetto vero</b>. WS5 chiuso ed ES5 aperto: senza la riga il traffico dell'alto ovest
    /// va a WS2, che sopra FL325 non ha niente. Con la riga va a ES5, che quel cielo lo sta tenendo.
    /// </summary>
    [Fact]
    public void A_FL350_con_ES5_aperto_il_traffico_di_WS5_va_a_ES5()
    {
        var (handler, online) = Risolvi(Ws5, quotaFt: 35000, Ws2, Es2, Es5);

        Assert.Equal(Es5, handler);
        Assert.True(online);
    }

    /// <summary>Senza la riga dichiarata lo stesso caso finisce su WS2: e' la fotografia del difetto.</summary>
    [Fact]
    public void Senza_la_riga_lo_stesso_caso_finiva_su_WS2()
    {
        var (handler, _) = TransferOnlineResolver.Resolve(
            FallbackChain.Candidates(Ws5, 35000, Nessuna, Padre),
            new HashSet<string>(new[] { Ws2, Es2, Es5 }, StringComparer.OrdinalIgnoreCase));

        Assert.Equal(Ws2, handler);
    }

    /// <summary>Caso 2 — il punto della carta: la stessa tabella da' una risposta diversa a quota diversa.</summary>
    [Fact]
    public void A_FL250_lo_stesso_punto_va_a_WS2()
    {
        var (handler, _) = Risolvi(Ws5, quotaFt: 25000, Ws2, Es2, Es5);

        Assert.Equal(Ws2, handler);
    }

    /// <summary>Caso 3 — con solo il capo online il risultato e' quello di sempre: nessuna regressione.</summary>
    [Fact]
    public void Con_solo_WS2_online_si_ricade_su_WS2_come_prima()
    {
        var (handler, _) = Risolvi(Ws5, quotaFt: 35000, Ws2);

        Assert.Equal(Ws2, handler);
    }

    /// <summary>Caso 4 — un punto senza quota non si risolve in verticale, e va bene cosi'.</summary>
    [Fact]
    public void Senza_quota_le_righe_con_fascia_si_saltano()
    {
        var (handler, _) = Risolvi(Ws5, quotaFt: null, Ws2, Es2, Es5);

        Assert.Equal(Ws2, handler);
    }

    /// <summary>
    /// L'altro verso: ES5 chiuso con WS5 aperto. ⚠️ Sull'albero <b>vero</b> non funzionava «per caso» — ES5
    /// pende da ES2, che sopra FL325 non ha niente: senza la riga il traffico dell'alto est finiva li'. Con
    /// la riga va a WS5, che quel cielo lo sta tenendo.
    /// </summary>
    [Fact]
    public void A_FL350_con_WS5_aperto_il_traffico_di_ES5_va_a_WS5()
    {
        var (handler, _) = Risolvi(Es5, quotaFt: 35000, Ws2, Es2, Ws5);

        Assert.Equal(Ws5, handler);
    }

    /// <summary>
    /// La fotografia del difetto nell'altro verso, sull'albero vero: senza la riga, ES5 chiuso a FL350 va a
    /// <b>ES2</b>. ⚠️ E' la prova che quella riga in produzione e' <b>portante</b>: cancellarla non da'
    /// nessun errore, il traffico continua a ricadere — sul settore che a quella quota non c'e'.
    /// </summary>
    [Fact]
    public void Senza_la_riga_ES5_chiuso_a_FL350_finisce_su_ES2()
    {
        var (handler, _) = TransferOnlineResolver.Resolve(
            FallbackChain.Candidates(Es5, 35000, Nessuna, Padre),
            new HashSet<string>(new[] { Ws2, Es2, Ws5 }, StringComparer.OrdinalIgnoreCase));

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
    /// ⚠️ In ampiezza, non in profondita' — e sull'albero <b>vero</b> la differenza si vede a occhio nudo.
    /// Da ES5, con la riga «sopra FL325 -> WS5» e il padre ES2, in profondita' si esaurirebbe tutto il ramo
    /// di WS5 — il suo padre WS2 compreso — <i>prima</i> di guardare il proprio padre. Con WS2 ed ES2
    /// tutt'e due online il traffico dell'est finirebbe a ovest, saltando il padre.
    /// </summary>
    [Fact]
    public void I_candidati_escono_per_distanza_non_per_ramo()
    {
        var c = FallbackChain.Candidates(Es5, 35000, Dichiarate, Padre);

        Assert.Equal(new[] { Es5, Ws5, Es2, Ws2 }, c);   // in profondita' sarebbe ES5, WS5, WS2, ES2
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
    //  Sequence — la catena come si MOSTRA
    // =====================================================================================================

    /// <summary>
    /// Il pannello deve poter dire «a questo passo, sopra FL325 c'e' ES5, altrimenti il padre WS2»: quindi
    /// la sequenza raggruppa per PASSO e porta la fascia di ogni voce, senza filtrare su una quota.
    /// </summary>
    [Fact]
    public void La_sequenza_mette_sullo_stesso_passo_chi_si_divide_il_traffico()
    {
        var passi = FallbackChain.Sequence(Ws5, Dichiarate, Padre);

        var primo = passi[0];
        Assert.Equal(2, primo.Count);

        Assert.Equal(Es5, primo[0].TargetCallsign);
        Assert.Equal(Split, primo[0].BaseFeet);
        Assert.Null(primo[0].TopFeet);
        Assert.False(primo[0].FromParent);

        Assert.Equal(Ws2, primo[1].TargetCallsign);
        Assert.Null(primo[1].BaseFeet);
        Assert.True(primo[1].FromParent);      // il padre: nessuno lo scrive e non si toglie
    }

    /// <summary>
    /// ⚠️ Allo stesso passo un settore puo' comparire DUE volte, e non e' un doppione: sono due motivi per
    /// arrivarci. Un settore che dichiara «sopra FL325 -> il padre» E ha quel padre; mostrarne uno solo
    /// direbbe che sotto FL325 il padre non c'e', mentre c'e'. Visto dal vivo il 1 settembre 2026.
    ///
    /// <para>⚠️ Vuole un albero <b>suo</b>: su quello vero di Milano la riga di ES5 e il padre di ES5 puntano
    /// a due settori <i>diversi</i> (WS5 e ES2), quindi il caso non si presenta. Tenerlo appeso all'albero
    /// reale vorrebbe dire perdere la proprieta' il giorno che l'albero cambia — ed e' appena successo.</para>
    /// </summary>
    [Fact]
    public void Allo_stesso_passo_si_vedono_tutti_i_motivi_per_arrivarci()
    {
        var passi = FallbackChain.Sequence("FIGLIO", RigaVersoIlPadre, PadreDiFiglio);

        var primo = passi[0];
        Assert.Equal(2, primo.Count);
        Assert.All(primo, e => Assert.Equal("PADRE", e.TargetCallsign));

        Assert.Equal(Split, primo[0].BaseFeet);      // la riga dichiarata: sopra FL325
        Assert.False(primo[0].FromParent);
        Assert.Null(primo[1].BaseFeet);              // e il padre: a ogni quota
        Assert.True(primo[1].FromParent);
    }

    /// <summary>Ma chi RISOLVE lo conta una volta sola: due motivi, un candidato.</summary>
    [Fact]
    public void I_due_motivi_restano_un_candidato_solo() =>
        Assert.Equal(new[] { "FIGLIO", "PADRE" },
            FallbackChain.Candidates("FIGLIO", 35000, RigaVersoIlPadre, PadreDiFiglio));

    /// <summary>Il settore di partenza non e' un ripiego di se' stesso: e' l'intestazione, non una voce.</summary>
    [Fact]
    public void La_sequenza_non_contiene_il_settore_di_partenza() =>
        Assert.DoesNotContain(FallbackChain.Sequence(Ws5, Dichiarate, Padre).SelectMany(p => p),
            e => e.TargetCallsign == Ws5);

    /// <summary>Piu' passi: da ES5 si arriva a WS2 al secondo giro, passando per il padre ES2.</summary>
    [Fact]
    public void La_sequenza_conta_i_passi()
    {
        var passi = FallbackChain.Sequence(Es5, Nessuna, Padre);

        Assert.Equal(2, passi.Count);
        Assert.Equal(Es2, Assert.Single(passi[0]).TargetCallsign);
        Assert.Equal(Ws2, Assert.Single(passi[1]).TargetCallsign);
    }

    /// <summary>
    /// Un settore preso a un passo non torna ai successivi: e' quel che chiude i cicli. ⚠️ Da ES5,
    /// sull'albero vero, a WS2 si arriva per DUE strade (il padre di WS5 e il padre di ES2) e deve comparire
    /// <b>una volta sola</b>.
    /// </summary>
    [Fact]
    public void Un_settore_gia_preso_non_torna_ai_passi_successivi()
    {
        var tutte = FallbackChain.Sequence(Es5, Dichiarate, Padre)
            .SelectMany(p => p).Select(e => e.TargetCallsign).ToList();

        Assert.Equal(new[] { Ws5, Es2, Ws2 }, tutte);
        Assert.Equal(tutte.Distinct(StringComparer.OrdinalIgnoreCase).Count(), tutte.Count);
    }

    /// <summary>Senza righe e senza padre non c'e' nessun passo: la catena finisce sul settore stesso.</summary>
    [Fact]
    public void Una_radice_senza_righe_non_ha_passi() =>
        Assert.Empty(FallbackChain.Sequence(Ws2, Nessuna, Padre));

    /// <summary>
    /// ⚠️ Il vincolo che tiene insieme le due facce: quel che il pannello DISEGNA e quel che la ricaduta
    /// RISOLVE vengono dalla stessa camminata. Se divergessero, il pannello racconterebbe una catena e il
    /// traffico ne seguirebbe un'altra — cioe' il difetto che questa carta esiste per chiudere.
    /// </summary>
    [Fact]
    public void La_sequenza_e_i_candidati_dicono_la_stessa_cosa()
    {
        // A una quota dentro la fascia, i candidati sono il settore piu' le voci della sequenza, DISTINTE:
        // allo stesso passo lo stesso settore puo' comparire per due motivi, ma resta un candidato solo.
        var attesi = new[] { Ws5 }.Concat(
            FallbackChain.Sequence(Ws5, Dichiarate, Padre).SelectMany(p => p).Select(e => e.TargetCallsign))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(attesi, FallbackChain.Candidates(Ws5, 35000, Dichiarate, Padre));
    }

    // =====================================================================================================
    //  La fascia
    // =====================================================================================================

    /// <summary>Piede incluso, tetto escluso: FL305 va all'alta, non alla bassa.</summary>
    [Theory]
    [InlineData(32400, false)]
    [InlineData(32500, true)]
    [InlineData(35000, true)]
    public void Il_piede_della_fascia_e_incluso(int quotaFt, bool atteso) =>
        Assert.Equal(atteso, new FallbackRow("X", 32500, null).AppliesAt(quotaFt));

    [Theory]
    [InlineData(0, true)]
    [InlineData(32400, true)]
    [InlineData(32500, false)]
    public void Il_tetto_della_fascia_e_escluso(int quotaFt, bool atteso) =>
        Assert.Equal(atteso, new FallbackRow("X", null, 32500).AppliesAt(quotaFt));

    [Fact]
    public void Una_riga_senza_fascia_vale_sempre_anche_senza_quota()
    {
        Assert.True(new FallbackRow("X", null, null).AppliesAt(35000));
        Assert.True(new FallbackRow("X", null, null).AppliesAt(null));
    }

    [Fact]
    public void Una_riga_con_fascia_non_si_valuta_senza_quota()
    {
        Assert.False(new FallbackRow("X", 32500, null).AppliesAt(null));
        Assert.False(new FallbackRow("X", null, 32500).AppliesAt(null));
    }

    // =====================================================================================================

    [Theory]
    [InlineData(350, LevelUnit.Fl, 35000)]
    [InlineData(5000, LevelUnit.Feet, 5000)]
    [InlineData(null, LevelUnit.Fl, null)]
    public void FeetOf_converte_il_livello(int? valore, LevelUnit unita, int? atteso) =>
        Assert.Equal(atteso, FallbackChain.FeetOf(valore, unita));
}
