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

    // =====================================================================================================
    //  Gerarchia ciclica — la rete per gli anelli che entrano da porte diverse dall'interfaccia
    // =====================================================================================================

    /// <summary>
    /// Il caso di produzione del 31 agosto 2026: <c>LIMF_WW0_APP</c> antenato di sé stesso. ⚠️ L'anello vive
    /// nell'albero EFFETTIVO — nei padri SCRITTI (<c>ParentRefs</c>) non c'è, ed è per questo che nessuna
    /// guardia lo vedeva.
    /// </summary>
    [Fact]
    public void Gerarchia_ciclica_viene_segnalata_col_percorso()
    {
        var d = new ConsistencyDataset
        {
            EffectiveParents = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LIMF_WW0_APP"] = "LIMF_WN0_APP",
                ["LIMF_WN0_APP"] = "LIMF_WW0_APP",
            },
        };

        var f = Assert.Single(ConsistencyReportService.Analyze(d), x => x.Category == "Gerarchia ciclica");

        Assert.Equal(ConsistencySeverity.Error, f.Severity);
        Assert.Equal(ConsistencyArea.Dati, f.Area);
        Assert.Contains("LIMF_WW0_APP", f.Detail);
        Assert.Contains("LIMF_WN0_APP", f.Detail);
    }

    /// <summary>
    /// ⚠️ <b>Lo stesso callsign nei due cataloghi si racconta.</b> Nessun indice lo può impedire — le tabelle
    /// sono due — e prima del 7 settembre 2026 la seconda riga sovrascriveva la prima in silenzio: la
    /// gerarchia risultava diversa da quella scritta, e si vedeva solo a valle, sulla ricaduta (R-014).
    /// </summary>
    [Fact]
    public void Un_callsign_nei_due_cataloghi_diventa_un_rilievo()
    {
        var d = new ConsistencyDataset
        {
            HierarchyDuplicates = new[]
            {
                new Vipi.Domain.Services.HierarchyDuplicate("LIPE_W_APP", "LIMM_WS2_CTR", "LIRR_CTR"),
            },
        };

        var f = Assert.Single(ConsistencyReportService.Analyze(d), x => x.Category == "Callsign in due cataloghi");

        Assert.Equal(ConsistencySeverity.Error, f.Severity);
        Assert.Equal("LIPE_W_APP", f.Entity);
        Assert.Contains("LIMM_WS2_CTR", f.Detail);
        Assert.Contains("LIRR_CTR", f.Detail);
    }

    /// <summary>Un anello si segnala UNA volta, non una per ogni nodo che ci finisce dentro.</summary>
    [Fact]
    public void Un_anello_produce_un_rilievo_solo()
    {
        var d = new ConsistencyDataset
        {
            EffectiveParents = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["A"] = "B", ["B"] = "C", ["C"] = "A",
                ["X"] = "A",     // ci arriva ma non ne fa parte
            },
        };

        Assert.Single(ConsistencyReportService.Analyze(d), x => x.Category == "Gerarchia ciclica");
    }

    /// <summary>Un albero sano non produce il rilievo: la rete non deve fare rumore tutti i giorni.</summary>
    [Fact]
    public void Un_albero_sano_non_produce_il_rilievo_di_ciclo()
    {
        var d = new ConsistencyDataset
        {
            EffectiveParents = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LIRF_TWR"] = "LIRR_APP", ["LIRR_APP"] = null, ["LIRF_GND"] = "LIRF_TWR",
            },
        };

        Assert.DoesNotContain(ConsistencyReportService.Analyze(d), x => x.Category == "Gerarchia ciclica");
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

    /// <summary>
    /// 🔴 Dal 10 settembre 2026 l'etichetta è un ELENCO, e questa è la guardia che tiene il controllo
    /// onesto: confrontando l'etichetta intera, una riga con due aree non combacerebbe MAI con un nome di
    /// catalogo e l'avviso scatterebbe sul caso NORMALE — «un avviso che scatta sul caso normale non è un
    /// avviso», che questa pagina ha già imparato due volte.
    /// <para>⚠️ E il messaggio deve dire QUALE nome manca, non l'elenco: chi legge deve sapere che cosa
    /// correggere.</para>
    /// </summary>
    [Fact]
    public void Phantom_area_is_found_INSIDE_a_list_and_names_the_missing_one()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = new[]
            {
                new TransferConditionRow(1, "LIRR", "VALMA", null, null, "LI R14A;Area Sparita;LI R21A/B - Sara"),
            },
            RunwayIdents = new Dictionary<int, string>(),
            AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LI R14A", "LI R21A/B - Sara" },
            ParentRefs = Array.Empty<ParentRefRow>(),
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };

        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Area fantasma", f.Category);
        Assert.Contains("Area Sparita", f.Detail);
        // ⚠️ E le due che ci sono NON compaiono: l'avviso nomina il colpevole, non la riga.
        Assert.DoesNotContain("LI R14A", f.Detail);
    }

    /// <summary>
    /// L'altra metà: con tutti i nomi in catalogo non si lamenta. ⚠️ Compresa un'area che ha lo <c>/</c> nel
    /// nome, che col separatore delle piste sarebbe stata spezzata in sei fantasmi.
    /// </summary>
    [Fact]
    public void A_list_of_areas_that_all_exist_says_nothing()
    {
        var d = new ConsistencyDataset
        {
            TransferConditions = new[]
            {
                new TransferConditionRow(1, "LIRR", "VALMA", null, null, "LI R14A;LI R49A/B/C/D/E/F - Zita"),
            },
            RunwayIdents = new Dictionary<int, string>(),
            AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LI R14A", "LI R49A/B/C/D/E/F - Zita" },
            ParentRefs = Array.Empty<ParentRefRow>(),
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };

        Assert.Empty(ConsistencyReportService.Analyze(d));
    }

    // ---- Campo solo militare con una vIPI civile (carta 2026-09-10-solo-militare-con-vipi-civile.md) ----

    private static ConsistencyDataset ConCampo(params CampoSoloMilitareRow[] campi) => new()
    {
        CampiSoloMilitari = campi,
        RunwayIdents = new Dictionary<int, string>(),
        AreaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ParentRefs = Array.Empty<ParentRefRow>(),
        ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// 🔴 <b>La metà che conta.</b> Un campo la cui vIPI civile è già nascosta e già staccata ha percorso la
    /// via d'uscita per intero: qui il report deve TACERE. Un avviso che scatta sul caso normale non è un
    /// avviso — è il modo in cui si smette di leggerli, e questa pagina l'ha già imparato due volte.
    /// </summary>
    [Fact]
    public void Un_campo_gia_a_posto_non_dice_NIENTE()
    {
        var d = ConCampo(new CampoSoloMilitareRow("LIBG", "LIBB", CivileVisibile: false, UnitaAlVsop: false));

        Assert.Empty(ConsistencyReportService.Analyze(d));
    }

    /// <summary>La vIPI civile è ancora online: un gesto, nasconderla.</summary>
    [Fact]
    public void La_vIPI_civile_ancora_VISIBILE_si_dice()
    {
        var d = ConCampo(new CampoSoloMilitareRow("LIBG", "LIBB", CivileVisibile: true, UnitaAlVsop: false));

        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("vIPI civile su campo solo militare", f.Category);
        Assert.Equal(ConsistencySeverity.Warning, f.Severity);
        Assert.Contains("LIBG", f.Detail);
        Assert.Equal("Diag_Msg_SoloMilConCivile", f.DetailKey);
        // ⚠️ Il rilievo manda dove stanno TUTTI E DUE i gesti (la pastiglia e il «nascondi»), non dove è
        // nato lo stato.
        Assert.Equal("/services/vsop/admin/airports", f.Where);
    }

    /// <summary>
    /// 🔴 Visibile <b>e</b> unita: i gesti sono DUE, e il messaggio deve dirli tutti e due. Chi ne fa uno
    /// solo crede di aver finito — ed è esattamente il caso in cui la pubblicazione accoppiata continua a
    /// girare su un documento che nessuno vede.
    /// </summary>
    [Fact]
    public void Visibile_E_unita_chiede_DUE_gesti()
    {
        var d = ConCampo(new CampoSoloMilitareRow("LIBG", "LIBB", CivileVisibile: true, UnitaAlVsop: true));

        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Diag_Msg_SoloMilConCivileUnita", f.DetailKey);
        Assert.Contains("unione", f.Detail);
    }

    /// <summary>
    /// Non visibile ma ancora unita: dal web non si vede, ma ogni pubblicazione del vSOP le fa una release.
    /// ⚠️ UN solo rilievo, e quello giusto: resta il solo scioglimento.
    /// <para>🔴 E il messaggio dice «non visibile», non «nascosta»: quel secchio contiene DUE situazioni —
    /// un documento nascosto e uno mai pubblicato — e sui dati veri di sviluppo (LIBV) è il secondo. Trovato
    /// guardando l'archivio prima di provare, non dopo.</para>
    /// </summary>
    [Fact]
    public void Non_visibile_ma_ancora_UNITA_dice_di_sciogliere()
    {
        var d = ConCampo(new CampoSoloMilitareRow("LIBG", "LIBB", CivileVisibile: false, UnitaAlVsop: true));

        var f = Assert.Single(ConsistencyReportService.Analyze(d));
        Assert.Equal("Diag_Msg_SoloMilNascostaMaUnita", f.DetailKey);
        Assert.Equal(ConsistencySeverity.Warning, f.Severity);
        // ⚠️ Non deve affermare che è NASCOSTA: potrebbe essere solo mai pubblicata.
        Assert.DoesNotContain("nascosta", f.Detail);
    }

    /// <summary>⚠️ E ogni campo parla per sé: due campi in stati diversi danno due rilievi diversi, non uno
    /// riassuntivo — chi ripara lavora su un ICAO alla volta.</summary>
    [Fact]
    public void Due_campi_danno_due_rilievi_distinti()
    {
        var d = ConCampo(
            new CampoSoloMilitareRow("LIBG", "LIBB", CivileVisibile: true, UnitaAlVsop: false),
            new CampoSoloMilitareRow("LIPA", "LIPP", CivileVisibile: false, UnitaAlVsop: true),
            new CampoSoloMilitareRow("LIBN", "LIBB", CivileVisibile: false, UnitaAlVsop: false));

        var f = ConsistencyReportService.Analyze(d).ToList();
        Assert.Equal(2, f.Count);
        Assert.Contains(f, x => x.Detail.Contains("LIBG"));
        Assert.Contains(f, x => x.Detail.Contains("LIPA"));
        Assert.DoesNotContain(f, x => x.Detail.Contains("LIBN"));
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
