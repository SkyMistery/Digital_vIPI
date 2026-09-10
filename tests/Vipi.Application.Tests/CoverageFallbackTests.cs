using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Content;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il banco di prova della carta <c>docs/feature/2026-09-10-rinvio-geometrico.md</c> §9: i casi che il
/// committente ha elencato il 9 settembre 2026, uno per uno.
///
/// <para>⚠️ <b>I due che contano</b> sono LIMN verso est e LIMN verso ovest: <b>stesso cedente</b>
/// (<c>LIMC_ANE_APP</c>), <b>stessa quota</b>, due riceventi diversi. Nessun meccanismo basato su chi cede
/// puo' passarli — ed e' il motivo per cui l'idea «riparti dal cedente» e' stata scartata.</para>
///
/// <para>La geometria e' finta ma la <b>forma</b> e' quella vera: ovest e est divisi da lon 10, Padova oltre
/// lon 12, lo strato alto a FL325, MIL sovrapposto SFC–UNL su tutta la FIR. L'albero e' quello di
/// PRODUZIONE, letto su <c>atc.it.ivao.aero</c> il 9 settembre.</para>
/// </summary>
public class CoverageFallbackTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Mil = "LIMM_MIL_CTR", Ane = "LIMC_ANE_APP", Es0 = "LIPX_ES0_APP", Ade = "LIME_ADE_APP";
    private const string PpCe1 = "LIPP_CE1_CTR", PpMil = "LIPP_MIL_CTR";

    private const int Split = 32500;

    // lon 8–10 ovest · 10–12 est · 11.9–14 Padova (la sovrapposizione sul confine e' voluta: serve a un test)
    private const string Ovest = "[[8,44],[10,44],[10,46],[8,46]]";
    private const string Est = "[[10,44],[12,44],[12,46],[10,46]]";
    private const string Fir = "[[8,44],[12.2,44],[12.2,46],[8,46]]";   // MIL sborda un poco oltre ES2: la striscia serve a un test
    private const string Padova = "[[11.9,44],[14,44],[14,46],[11.9,46]]";
    private const string TmaMalpensa = "[[9,44.5],[11,44.5],[11,45.5],[9,45.5]]";   // a cavallo del confine ovest/est
    private const string TmaGhedi = "[[10.2,44.5],[11,44.5],[11,45.5],[10.2,45.5]]";
    private const string TmaBergamo = "[[10.2,44.5],[11,44.5],[11,45.5],[10.2,45.5]]";

    private static SectorVolumeRow Riga(string cs, string? padre, SectorType tipo, string poligono, int? basso, int? alto) =>
        new(cs, padre, tipo, null,
            new[] { new ShapePart(poligono, basso, alto, AirspaceDatum.Amsl, AirspaceDatum.Amsl, "", "") },
            ShapeSource.Source);

    private static readonly List<SectorVolumeRow> Settori = new()
    {
        Riga(Ws2, null, SectorType.Ctr, Ovest, 0, Split),
        Riga(Es2, Ws2, SectorType.Ctr, Est, 0, Split),
        Riga(Ws5, Ws2, SectorType.Ctr, Ovest, Split, null),
        Riga(Es5, Es2, SectorType.Ctr, Est, Split, null),
        // ⚠️ MIL: tutta la FIR, SFC–UNL. Piu' alto del suo stesso padre — ed e' il fatto da cui nasce tutto.
        Riga(Mil, Ws2, SectorType.Ctr, Fir, 0, null),
        Riga(Ane, Ws2, SectorType.App, TmaMalpensa, 0, 19500),
        Riga(Ade, Ane, SectorType.App, TmaBergamo, 0, 19500),
        Riga(Es0, Es2, SectorType.App, TmaGhedi, 0, 19500),
        Riga(PpCe1, null, SectorType.Ctr, Padova, 0, null),
        Riga(PpMil, PpCe1, SectorType.Ctr, Padova, 0, null),
    };

    private static readonly Dictionary<string, string?> Padri = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2, [Mil] = Ws2,
        [Ane] = Ws2, [Ade] = Ane, [Es0] = Es2, [PpCe1] = null, [PpMil] = PpCe1,
    };

    private static readonly Dictionary<string, string> AccDi = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ws2] = "LIMM", [Es2] = "LIMM", [Ws5] = "LIMM", [Es5] = "LIMM", [Mil] = "LIMM",
        [Ane] = "LIMM", [Ade] = "LIMM", [Es0] = "LIMM", [PpCe1] = "LIPP", [PpMil] = "LIPP",
    };

    /// <summary>La riga vera che sta in produzione su ES5, per non perdere la famiglia «dipende dalla quota».</summary>
    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> Dichiarate =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: Split, TopFeet: null) },
        };

    private static readonly CopPositions Punti = new(new[]
    {
        ("NELAB", 45.0, 10.5),     // est, e dentro la TMA di Ghedi
        ("ITCAP", 45.0, 10.8),     // est, e dentro la TMA di Malpensa
        ("MMP", 45.0, 9.0),        // ovest, e dentro la TMA di Malpensa
        ("KUKEV", 45.0, 12.5),     // oltre il confine: Padova
        ("VEROB", 45.0, 12.1),     // NELLA sovrapposizione dei due MIL, e FUORI da ES2
        ("ZZZZZ", 45.0, 30.0),     // fuori da ogni poligono
    });

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    /// <summary>Le pretese come le costruisce chi risolve un rinvio: collassate con la CATENA, non coi padri.</summary>
    private static IReadOnlyList<SectorClaim> Claims(IReadOnlySet<string> online, int quotaFt) =>
        SectorVolumeMap.BuildClaims(Settori, online,
            cs => TransferOnlineResolver.FirstOnline(
                FallbackChain.Candidates(cs, quotaFt, Dichiarate, c => Padri.GetValueOrDefault(c)), online));

    /// <summary>Il cedente e tutti i suoi discendenti: chi consegna non puo' essere chi raccoglie.</summary>
    private static IReadOnlySet<string> Dominio(string cedente)
    {
        var dentro = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cedente };
        bool cresciuto = true;
        while (cresciuto)
        {
            cresciuto = false;
            foreach (var (figlio, padre) in Padri)
                if (padre is not null && dentro.Contains(padre) && dentro.Add(figlio)) cresciuto = true;
        }
        return dentro;
    }

    private static CoverageFallbackResult Risolvi(string cop, int? quotaFt, string cedente,
        string ricevente, IReadOnlySet<string> online) =>
        CoverageFallback.Resolve(cop, quotaFt, Punti, Claims(online, quotaFt ?? 0),
            tipoRicevente: Settori.First(s => s.Callsign == ricevente).Type,
            accRicevente: AccDi.GetValueOrDefault(ricevente),
            fuoriGioco: Dominio(cedente),
            accDi: cs => AccDi.GetValueOrDefault(cs));

    // =====================================================================================================
    //  I casi del committente
    // =====================================================================================================

    /// <summary>Ghedi, MIL chiuso: il CoP sta a est ⇒ ES2. Oggi la catena dava WS2 (il padre di MIL).</summary>
    [Fact]
    public void Ghedi_con_MIL_chiuso_va_a_ES2()
    {
        var r = Risolvi("NELAB", 14000, cedente: Es0, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(CoverageFallbackOutcome.Resolved, r.Outcome);
        Assert.Equal(Es2, r.TargetCallsign);
    }

    /// <summary>Ghedi con MIL <b>e</b> ES2 chiusi: il volume di ES2 lo tiene WS2, e il traffico ci va.</summary>
    [Fact]
    public void Ghedi_con_MIL_e_ES2_chiusi_va_a_WS2()
    {
        var r = Risolvi("NELAB", 14000, cedente: Es0, ricevente: Mil, Online(Ws2));

        Assert.Equal(Ws2, r.TargetCallsign);
    }

    /// <summary>
    /// 🔴 <b>Il caso che scarta l'idea «riparti dal cedente»</b>, prima meta': dallo stesso ANE, verso est.
    /// </summary>
    [Fact]
    public void LIMN_verso_EST_con_MIL_chiuso_va_a_ES2()
    {
        var r = Risolvi("ITCAP", 14000, cedente: Ane, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(Es2, r.TargetCallsign);
    }

    /// <summary>
    /// 🔴 Seconda meta': <b>stesso cedente, stessa quota</b>, e la risposta cambia perche' cambia il PUNTO.
    /// Il padre di ANE e' WS2, quindi qualunque meccanismo basato sul cedente direbbe WS2 tutt'e due le volte.
    /// </summary>
    [Fact]
    public void LIMN_verso_OVEST_con_MIL_chiuso_va_a_WS2()
    {
        var r = Risolvi("MMP", 14000, cedente: Ane, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(Ws2, r.TargetCallsign);
    }

    /// <summary>Verso Padova: LIPP_MIL e' figlio di CE1, quindi piu' profondo, quindi vince lui.</summary>
    [Fact]
    public void Verso_Padova_con_il_MIL_di_Padova_aperto_va_a_LIPP_MIL()
    {
        var r = Risolvi("KUKEV", 14000, cedente: Es0, ricevente: Mil, Online(PpMil, PpCe1, Es2, Ws2));

        Assert.Equal(PpMil, r.TargetCallsign);
    }

    /// <summary>E col MIL di Padova chiuso, il suo volume lo tiene CE1: nessuna riga da scrivere.</summary>
    [Fact]
    public void Verso_Padova_col_MIL_di_Padova_chiuso_va_a_LIPP_CE1()
    {
        var r = Risolvi("KUKEV", 14000, cedente: Es0, ricevente: Mil, Online(PpCe1, Es2, Ws2));

        Assert.Equal(PpCe1, r.TargetCallsign);
    }

    // =====================================================================================================
    //  Le tre esclusioni
    // =====================================================================================================

    /// <summary>
    /// ⚠️ <b>Filtro di rango.</b> Un APP di terzi (Bergamo) e' online e contiene il punto, ed e' piu' profondo
    /// di ES2: senza il filtro raccoglierebbe lui. Il poligono di un APP e' <b>spazio aereo</b>, non
    /// titolarita' del flusso — un APP e' delegato per i suoi arrivi e le sue partenze, non per chi transita.
    /// </summary>
    [Fact]
    public void Un_APP_di_terzi_che_contiene_il_punto_non_raccoglie_mai()
    {
        var r = Risolvi("NELAB", 14000, cedente: Es0, ricevente: Mil, Online(Ade, Es2, Ws2));

        Assert.Equal(Es2, r.TargetCallsign);

        // ⚠️ La mutazione: ABBASSATA la soglia a quella di un APP, lo stesso punto va a Bergamo — cioe' senza
        // il filtro il difetto ci sarebbe, e questo test lo vedrebbe.
        var sogliaBassa = CoverageFallback.Resolve("NELAB", 14000, Punti, Claims(Online(Ade, Es2, Ws2), 14000),
            SectorType.App, "LIMM", Dominio(Es0), cs => AccDi.GetValueOrDefault(cs));
        Assert.Equal(Ade, sogliaBassa.TargetCallsign);
    }

    /// <summary>
    /// ⚠️ <b>Il cedente esce.</b> Con un cedente d'area il rango non lo escluderebbe: qui cede ES2, il punto
    /// sta dentro ES2, e senza l'esclusione la risposta sarebbe «ES2 consegna a ES2».
    /// </summary>
    [Fact]
    public void Il_cedente_non_puo_essere_chi_raccoglie()
    {
        var r = Risolvi("NELAB", 14000, cedente: Es2, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(Ws2, r.TargetCallsign);
    }

    /// <summary>E con lui i suoi discendenti: quel che e' delegato a chi cede resta suo.</summary>
    [Fact]
    public void Anche_i_discendenti_del_cedente_escono()
    {
        var r = Risolvi("ITCAP", 14000, cedente: Ane, ricevente: Mil, Online(Ade, Es2, Ws2));

        Assert.Equal(Es2, r.TargetCallsign);   // ADE e' figlio di ANE
    }

    /// <summary>
    /// Lo spareggio «stesso centro». Nella striscia dove i due MIL si sovrappongono le due pretese hanno la
    /// STESSA profondita' (1) e la STESSA banda (SFC–UNL): a decidere resterebbe l'area del bounding box,
    /// cioe' il caso — e vincerebbe Padova, che ce l'ha piu' piccolo. Col ricevente di LIMM si resta in LIMM.
    /// </summary>
    [Fact]
    public void A_pari_specificita_si_preferisce_il_centro_del_ricevente()
    {
        var online = Online(PpMil, PpCe1, Ws2, Es2);

        var perLimm = Risolvi("VEROB", 14000, cedente: Es0, ricevente: Mil, online);
        Assert.Equal("LIMM", AccDi[perLimm.TargetCallsign!]);

        // ⚠️ La mutazione, scritta nel test invece che provata a mano: TOLTA la preferenza, lo stesso punto
        // va a Padova. Senza questa riga il test sopra proverebbe solo che il codice non esplode.
        var senzaPreferenza = CoverageFallback.Resolve("VEROB", 14000, Punti, Claims(online, 14000),
            SectorType.Ctr, accRicevente: null, Dominio(Es0), cs => AccDi.GetValueOrDefault(cs));
        Assert.Equal("LIPP", AccDi[senzaPreferenza.TargetCallsign!]);
    }

    // =====================================================================================================
    //  Quando il rinvio NON risponde — e lo dice
    // =====================================================================================================

    /// <summary>
    /// ⚠️ <c>Y01-Y12</c> non e' un punto sconosciuto: e' un <b>tratto di aerovie</b>, e attraversa ES2 e WS2.
    /// Non e' un dato che manca, e' una domanda che non si puo' porre — la risposta va scritta.
    /// </summary>
    [Theory]
    [InlineData("Y01-Y12")]
    [InlineData("ALL")]
    [InlineData("TOPNO 3A")]
    [InlineData("")]
    public void Un_CoP_che_non_e_un_punto_non_si_risolve(string cop)
    {
        var r = Risolvi(cop, 14000, cedente: Es0, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(CoverageFallbackOutcome.NotAPoint, r.Outcome);
        Assert.Null(r.TargetCallsign);
    }

    /// <summary>Ha la forma di un punto ma nessun catalogo lo colloca: si apre una coordinata a mano.</summary>
    [Fact]
    public void Un_punto_che_nessun_catalogo_colloca_lo_dice()
    {
        var r = Risolvi("PIPPO", 14000, cedente: Es0, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(CoverageFallbackOutcome.PointUnknown, r.Outcome);
    }

    /// <summary>Senza quota al trasferimento un volume non si interroga.</summary>
    [Fact]
    public void Senza_quota_non_si_risolve()
    {
        var r = CoverageFallback.Resolve("NELAB", null, Punti, Claims(Online(Es2, Ws2), 14000),
            SectorType.Ctr, "LIMM", Dominio(Es0), cs => AccDi.GetValueOrDefault(cs));

        Assert.Equal(CoverageFallbackOutcome.NoLevel, r.Outcome);
    }

    /// <summary>Un punto fuori da ogni poligono: la catena prosegue sul padre, come ha sempre fatto.</summary>
    [Fact]
    public void Un_punto_che_nessuno_copre_lascia_proseguire_la_catena()
    {
        var r = Risolvi("ZZZZZ", 14000, cedente: Es0, ricevente: Mil, Online(Es2, Ws2));

        Assert.Equal(CoverageFallbackOutcome.NobodyCovers, r.Outcome);
        Assert.Empty(r.AsCandidates());
    }

    // =====================================================================================================
    //  L'innesto nella catena
    // =====================================================================================================

    /// <summary>
    /// La riga di rinvio dentro <see cref="FallbackChain.Candidates"/>: il rinvio sta DAVANTI al padre, e il
    /// padre resta la coda. Con MIL chiuso da Ghedi la catena diventa <c>MIL, ES2, WS2</c> — mentre senza il
    /// rinvio sarebbe <c>MIL, WS2</c>, che e' il difetto visto in produzione.
    /// </summary>
    [Fact]
    public void Nella_catena_il_rinvio_sta_davanti_al_padre()
    {
        var online = Online(Es2, Ws2);
        var righe = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase)
        {
            [Mil] = new[] { new FallbackRow("", null, null, FallbackTargetKind.Coverage) },
        };

        var conRinvio = FallbackChain.Candidates(Mil, 14000, righe, cs => Padri.GetValueOrDefault(cs),
            () => Risolvi("NELAB", 14000, cedente: Es0, ricevente: Mil, online).AsCandidates());

        Assert.Equal(new[] { Mil, Es2, Ws2 }, conRinvio);
        Assert.Equal(new[] { Mil, Ws2 }, FallbackChain.Candidates(Mil, 14000, righe, cs => Padri.GetValueOrDefault(cs)));
    }
}
