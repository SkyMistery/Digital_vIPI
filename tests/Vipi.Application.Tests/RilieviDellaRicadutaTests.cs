using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Diagnostics;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le tre reti della carta <c>docs/feature/2026-09-10-rinvio-geometrico.md</c> Parte 8. Tutt'e tre esistono
/// perche' il difetto che pescano <b>non si manifesta come un errore</b>: la ricaduta riesce, verso l'ente
/// sbagliato; i due alberi rispondono ognuno per conto suo; il rinvio tace su un punto che non sa collocare.
/// </summary>
public class RilieviDellaRicadutaTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Mil = "LIMM_MIL_CTR";
    private const int Split = 32500;

    /// <summary>L'albero e le bande di PRODUZIONE, lette il 9 settembre 2026.</summary>
    private static ConsistencyDataset Milano(
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>>? ripieghi = null,
        IReadOnlyDictionary<string, string?>? proiettati = null)
    {
        var padri = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2, [Mil] = Ws2,
        };

        return new ConsistencyDataset
        {
            EffectiveParents = padri,
            ProjectedParents = proiettati ?? padri,
            SectorBands = new[]
            {
                new SectorBandRow(Ws2, 0, Split),
                new SectorBandRow(Es2, 0, Split),
                new SectorBandRow(Ws5, Split, null),
                new SectorBandRow(Es5, Split, null),
                new SectorBandRow(Mil, 0, null),      // SFC–UNL: piu' alto del suo stesso padre
            },
            Fallbacks = ripieghi ?? new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Un dataset con le sole clausole: il resto non entra nel rilievo sui CoP.</summary>
    private static ConsistencyDataset ConAccordi(params TransferConditionRow[] righe) =>
        new() { TransferConditions = righe };

    private static IReadOnlyList<ConsistencyFinding> Rilievi(ConsistencyDataset d, string categoria,
        CopPositions? punti = null) =>
        ConsistencyReportService.Analyze(d, punti).Where(f => f.Category == categoria).ToList();

    // =====================================================================================================
    //  Ricaduta che non copre la quota
    // =====================================================================================================

    /// <summary>
    /// 🔴 Il caso vero: senza la riga dichiarata, ES5 (FL325–UNL) ricade su ES2, che a FL325 non c'e'. La
    /// ricaduta <b>riesce</b>, e il traffico va a chi non ha niente. Nessun errore, nessun log.
    /// </summary>
    [Fact]
    public void Senza_la_riga_ES5_viene_segnalato()
    {
        var f = Assert.Single(Rilievi(Milano(), "Ricaduta che non copre la quota"), x => x.Entity == Es5);

        Assert.Equal(ConsistencySeverity.Error, f.Severity);
        Assert.Contains("FL325", f.Detail);
    }

    /// <summary>Con la riga che sta in produzione, ES5 tace: e' la prova che il rilievo guarda la CATENA e
    /// non il solo padre.</summary>
    [Fact]
    public void Con_la_riga_dichiarata_ES5_non_si_segnala()
    {
        var righe = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase)
        {
            [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: Split, TopFeet: null) },
        };

        Assert.DoesNotContain(Rilievi(Milano(righe), "Ricaduta che non copre la quota"), x => x.Entity == Es5);
    }

    /// <summary>
    /// ⚠️ WS5 invece NON si segnala nemmeno senza righe, e non e' una svista: pende da WS2, che parte da
    /// terra e a FL325 <i>c'e'</i> — il rilievo guarda il PIEDE, e il piede di WS5 sta dentro la banda di
    /// WS2. E' il ripiego giusto quando l'altro alto e' chiuso.
    /// </summary>
    [Fact]
    public void Un_settore_alto_col_padre_che_arriva_fin_li_non_si_segnala()
    {
        // WS2 e' 0–FL325 e il tetto e' ESCLUSO: a FL325 non c'e'. Lo si allarga per il caso di prova.
        var padri = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [Ws2] = null, [Ws5] = Ws2,
        };
        var d = new ConsistencyDataset
        {
            EffectiveParents = padri,
            ProjectedParents = padri,
            SectorBands = new[] { new SectorBandRow(Ws2, 0, null), new SectorBandRow(Ws5, Split, null) },
        };

        Assert.Empty(Rilievi(d, "Ricaduta che non copre la quota"));
    }

    /// <summary>
    /// ⚠️ Un <b>rinvio</b> conta come «copre»: il suo bersaglio dipende dal punto, e questo report i punti
    /// non li ha. Segnalarlo direbbe il falso proprio sulla riga scritta per riparare il difetto.
    /// </summary>
    [Fact]
    public void Un_rinvio_non_si_segnala_perche_qui_non_si_puo_sapere()
    {
        var righe = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase)
        {
            [Es5] = new[] { new FallbackRow("", null, null, FallbackTargetKind.Coverage) },
        };

        Assert.DoesNotContain(Rilievi(Milano(righe), "Ricaduta che non copre la quota"), x => x.Entity == Es5);
    }

    // =====================================================================================================
    //  Albero proiettato divergente
    // =====================================================================================================

    /// <summary>
    /// 🔴 I cataloghi dicono una cosa e la proiezione un'altra: la ricaduta legge i primi, la geometria la
    /// seconda, e alla stessa domanda si ottengono due risposte a seconda di chi la fa.
    /// </summary>
    [Fact]
    public void Un_padre_diverso_fra_cataloghi_e_proiezione_si_segnala()
    {
        var proiettati = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Ws5, [Mil] = Ws2,   // ES5 sotto WS5: il DB di sviluppo
        };

        var f = Assert.Single(Rilievi(Milano(proiettati: proiettati), "Albero proiettato divergente"));

        Assert.Equal(Es5, f.Entity);
        Assert.Contains(Es2, f.Detail);
        Assert.Contains(Ws5, f.Detail);
    }

    [Fact]
    public void Due_alberi_uguali_non_dicono_niente() =>
        Assert.Empty(Rilievi(Milano(), "Albero proiettato divergente"));

    // =====================================================================================================
    //  CoP senza posizione
    // =====================================================================================================

    /// <summary>
    /// ⚠️ Si segnala solo quel che <b>ha la forma di un punto</b>: `Y01-Y12` e `ALL` non sono dati mancanti,
    /// sono domande che non si possono porre — e un avviso che grida su dati corretti si impara a ignorare.
    /// </summary>
    [Fact]
    public void Solo_i_nomi_di_punto_che_nessun_catalogo_colloca()
    {
        var d = ConAccordi(
            new TransferConditionRow(1, "LIMM", "NELAB, PIPPO", null, null, null),
            new TransferConditionRow(2, "LIMM", "Y01-Y12, ALL", null, null, null),
            new TransferConditionRow(3, "LIPP", "TOPNO 3A", null, null, null));
        var punti = new CopPositions(new[] { ("NELAB", 45.0, 10.5) });

        var f = Assert.Single(Rilievi(d, "CoP senza posizione", punti));

        Assert.Equal("LIMM", f.Entity);
        Assert.Contains("PIPPO", f.Detail);
        Assert.DoesNotContain("Y01", f.Detail);
        Assert.DoesNotContain("NELAB", f.Detail);
    }

    /// <summary>
    /// ⚠️ A catalogo <b>assente</b> non si segnala niente: e' la stessa regola di <c>NavaidCheck</c> — una
    /// sorgente muta trasformerebbe un disservizio in una pagina piena di avvisi falsi.
    /// </summary>
    [Fact]
    public void A_catalogo_vuoto_nessuno_e_sconosciuto()
    {
        var d = ConAccordi(new TransferConditionRow(1, "LIMM", "PIPPO", null, null, null));

        Assert.Empty(Rilievi(d, "CoP senza posizione"));
        Assert.Empty(Rilievi(d, "CoP senza posizione", CopPositions.Empty));
    }
}
