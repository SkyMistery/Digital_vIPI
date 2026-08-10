using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Report di consistenza soft-ref (Fase 2): la logica pura <see cref="ConsistencyReportService.Analyze"/> becca
/// pista orfana, label divergente, area fantasma e gerarchia dangling; un dataset coerente non produce nulla.
/// </summary>
public class ConsistencyReportTests
{
    private static ConsistencyDataset Clean() => new()
    {
        TransferConditions = new[]
        {
            new TransferConditionRow(1, "LIRR", "VALMA", ConditionRefId: 10, ConditionLabel: "16R / 16L", ConditionAreaLabel: "LI R14A"),
        },
        RunwayIdents = new Dictionary<int, string> { [10] = "16R" },
        AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LI R14A" },
        ParentRefs = new[] { new ParentRefRow("Settore APT", "LIRF_TWR", "LIRR_APP") },
        ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRR_APP", "LIRF_TWR" },
        RegulatedRefs = new[] { new RegulatedRefRow("vIPI", "Roma ACC", """{"OwnAuto":false,"OwnIds":["8963"],"ExtraIds":[]}""") },
        SpecialAreaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "8963" },
    };

    [Fact]
    public void Clean_dataset_has_no_findings()
    {
        Assert.Empty(ConsistencyReportService.Analyze(Clean()));
    }

    /// <summary>
    /// La risoluzione live del ricevente accetta anche il candidato che sia un <b>segmento</b> del callsign
    /// online (serve alla risalita della copertura). Se due callsign del catalogo si confondono così, un
    /// settore online ne fa apparire online un altro: al controllore comparirebbe un consegnatario che non c'è.
    ///
    /// <para>Sui 313 callsign reali (9 agosto 2026) le collisioni sono <b>zero</b> — nessuno è privo di
    /// underscore, nessuno è contenuto in un altro — ed è la misura che ha fatto scartare la tabella di
    /// mapping esplicita della voce E1. Questa è la sentinella che rende revocabile quella scelta.</para>
    /// </summary>
    [Fact]
    public void Callsign_che_si_confondono_nella_risoluzione_live_sono_segnalati()
    {
        // Solo i callsign: così il caso è isolato e nessun'altra regola può sporcare l'esito.
        var d = new ConsistencyDataset
        {
            // «LIRR» nudo è un segmento di «LIRR_APP»: con l'APP online, l'ACC risulterebbe online.
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRR_APP", "LIRF_TWR", "LIRR" },
        };

        var f = Assert.Single(ConsistencyReportService.Analyze(d), x => x.Category.StartsWith("Callsign ambiguo"));
        Assert.Equal("LIRR", f.Entity);
        Assert.Contains("LIRR_APP", f.Detail);
    }

    /// <summary>
    /// Il caso che <b>non</b> deve allarmare, ed è la forma normale del catalogo: callsign che condividono
    /// l'ICAO e la posizione ma differiscono per infisso non si confondono affatto — né segmento né sottostringa.
    /// Senza questo test la regola precedente potrebbe passare rendendo il report inutilizzabile di rumore.
    /// </summary>
    [Fact]
    public void Callsign_con_infisso_diverso_non_sono_ambigui()
    {
        var d = new ConsistencyDataset
        {
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "LIRF_APP", "LIRF_E_APP", "LIBB_ES_CTR", "LIMM_WS2_CTR",
            },
        };

        Assert.DoesNotContain(ConsistencyReportService.Analyze(d), x => x.Category.StartsWith("Callsign ambiguo"));
    }

    [Fact]
    public void Orphan_runway_ref_is_flagged()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = new[] { new TransferConditionRow(1, "LIRR", "VALMA", 999, "16R", null) },
            RunwayIdents = new Dictionary<int, string> { [10] = "16R" },   // 999 non esiste
            AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ParentRefs = Array.Empty<ParentRefRow>(),
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Pista orfana", f.Category);
        Assert.Equal(ConsistencySeverity.Error, f.Severity);
    }

    [Fact]
    public void Divergent_runway_label_is_flagged()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = new[] { new TransferConditionRow(1, "LIRR", "VALMA", 10, "34L", null) },
            RunwayIdents = new Dictionary<int, string> { [10] = "16R" },   // ident cambiato, label vecchia
            AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ParentRefs = Array.Empty<ParentRefRow>(),
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Label pista divergente", f.Category);
    }

    [Fact]
    public void Phantom_area_is_flagged()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = new[] { new TransferConditionRow(1, "LIRR", "VALMA", null, null, "Area Sparita") },
            RunwayIdents = new Dictionary<int, string>(),
            AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LI R14A" },
            ParentRefs = Array.Empty<ParentRefRow>(),
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Area fantasma", f.Category);
    }

    [Fact]
    public void Dangling_regulated_area_id_is_flagged()
    {
        var d = new ConsistencyDataset
        {
            RegulatedRefs = new[]
            {
                new RegulatedRefRow("vIPI", "Roma ACC", """{"OwnAuto":false,"OwnIds":["8963"],"ExtraIds":["9999"]}"""),
            },
            SpecialAreaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "8963" },   // 9999 potata dall'import
        };
        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Area regolamentata dangling", f.Category);
        Assert.Equal(ConsistencySeverity.Warning, f.Severity);
        Assert.Contains("9999", f.Detail);
        Assert.DoesNotContain("8963", f.Detail);
    }

    [Fact]
    public void Regulated_selection_in_auto_mode_has_nothing_to_dangle()
    {
        var d = new ConsistencyDataset
        {
            // Automatico = lista viva delle aree dell'ACC, nessun id salvato; anche senza aree in catalogo è coerente.
            RegulatedRefs = new[] { new RegulatedRefRow("vIPI", "Roma ACC", """{"OwnAuto":true,"OwnIds":[],"ExtraIds":[]}""") },
            SpecialAreaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        Assert.Empty(ConsistencyReportService.Analyze(d));
    }

    [Fact]
    public void Legacy_array_selection_is_read_as_manual_ids()
    {
        var d = new ConsistencyDataset
        {
            RegulatedRefs = new[] { new RegulatedRefRow("vIPI", "Napoli APP", """["8963","9999"]""") },
            SpecialAreaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "8963" },
        };
        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Area regolamentata dangling", f.Category);
        Assert.Contains("9999", f.Detail);
    }

    [Fact]
    public void Dangling_parent_callsign_is_flagged()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = Array.Empty<TransferConditionRow>(),
            RunwayIdents = new Dictionary<int, string>(),
            AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ParentRefs = new[] { new ParentRefRow("Aeroporto", "LIRF", "LIRR_SPARITO") },
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRR_APP" },
        };
        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Gerarchia dangling", f.Category);
        Assert.Equal(ConsistencySeverity.Error, f.Severity);
    }
}
