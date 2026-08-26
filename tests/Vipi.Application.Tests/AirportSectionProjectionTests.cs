using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La proiezione dal profilo strutturato alle viste delle sezioni derivate (carta 2026-08-26 §2). È pura, e per
/// questo si verifica senza database: prima queste formattazioni vivevano dentro la cottura del documento, quindi
/// esistevano solo al momento del rebuild e nessun test le guardava da vicino.
/// </summary>
public class AirportSectionProjectionTests
{
    private static AirportData Profilo(
        int? ta = null,
        IReadOnlyList<TlRow>? tl = null,
        IReadOnlyList<RunwayRow>? piste = null,
        IReadOnlyList<RunwayRuleRow>? regole = null,
        IReadOnlyList<FrequencyLinkRow>? link = null) => new()
    {
        AirportId = 1, Icao = "LIRF", Name = "Roma Fiumicino", AccCode = "LIRR",
        TransitionAltitudeFt = ta,
        TransitionLevels = tl ?? Array.Empty<TlRow>(),
        Runways = piste ?? Array.Empty<RunwayRow>(),
        Rules = regole ?? Array.Empty<RunwayRuleRow>(),
        Sids = Array.Empty<SidRow>(),
        Links = link ?? Array.Empty<FrequencyLinkRow>(),
    };

    private static RunwayRuleRow Regola(
        int maxTail = 5, int? maxCross = null, RunwaySurface superficie = RunwaySurface.Any, string? nome = null,
        int? daMin = null, int? aMin = null, int? giorni = null, DateParity parita = DateParity.Any,
        int? dalMmdd = null, int? alMmdd = null) =>
        new(1, "16R", "16L", nome, maxTail, maxCross, superficie, null, daMin, aMin, giorni, parita, dalMmdd, alMmdd);

    // ---- un profilo assente non è un errore: è una sezione vuota ----

    [Fact]
    public void Senza_profilo_le_sezioni_sono_vuote()
    {
        // Capita davvero: un ICAO non ancora assegnato a una ACC. Prima la cottura non partiva nemmeno.
        Assert.Empty(AirportSectionProjection.Rules(null).Rows);
        Assert.Empty(AirportSectionProjection.Runways(null).Rows);
        Assert.Empty(AirportSectionProjection.Frequencies(null, null).Rows);
        Assert.Null(AirportSectionProjection.Transition(null).TransitionAltitudeFt);
    }

    // ---- quote di transizione ----

    [Fact]
    public void Le_fasce_QNH_si_leggono_coi_simboli_giusti()
    {
        var v = AirportSectionProjection.Transition(Profilo(6000, new[]
        {
            new TlRow(1, null, 976, "FL85"),
            new TlRow(2, 977, 994, "FL80"),
            new TlRow(3, 1013, null, "FL70"),
        }));

        Assert.Equal(6000, v.TransitionAltitudeFt);
        Assert.Equal(new[] { "≤ 976", "977 – 994", "≥ 1013" }, v.Rows.Select(r => r.QnhRange));
        Assert.Equal(new[] { "FL85", "FL80", "FL70" }, v.Rows.Select(r => r.Level));
    }

    [Fact]
    public void Una_TA_non_definita_resta_nulla_e_non_diventa_zero()
    {
        // Zero sarebbe un'altitudine, e il lettore la crederebbe vera: la pagina deve poter dire «N/A».
        Assert.Null(AirportSectionProjection.Transition(Profilo(null)).TransitionAltitudeFt);
    }

    // ---- piste ----

    [Fact]
    public void TORA_e_LDA_non_compilate_ricadono_sulla_lunghezza_di_anagrafica()
    {
        var v = AirportSectionProjection.Runways(Profilo(piste: new[]
        {
            new RunwayRow(1, "16L", 3902, 160, null, null, "ILS CAT III", null, null),
            new RunwayRow(2, "16R", 3900, 160, "3600", "3300", null, null, null),
        }));

        Assert.Equal("3902 m", v.Rows[0].Tora);
        Assert.Equal("3902 m", v.Rows[0].Lda);
        Assert.Equal("ILS CAT III", v.Rows[0].AppProcedures);
        Assert.Equal("—", v.Rows[0].Patterns);          // colonna editoriale vuota: trattino, mai cella bianca
        Assert.Equal("3600", v.Rows[1].Tora);           // il valore editoriale vince sulla lunghezza
        Assert.Equal("3300", v.Rows[1].Lda);
    }

    [Fact]
    public void Una_pista_senza_lunghezza_ne_valore_editoriale_dice_trattino()
    {
        var v = AirportSectionProjection.Runways(Profilo(piste: new[]
        {
            new RunwayRow(1, "07", null, null, null, null, null, null, null),
        }));
        Assert.Equal("—", Assert.Single(v.Rows).Tora);
    }

    // ---- frequenze ----

    [Fact]
    public void Le_frequenze_seguono_l_ordine_delle_posizioni_e_la_principale_e_marcata()
    {
        var catalogo = new[]
        {
            Settore(1, "LIRF_TWR", "TWR", "118.700", principale: true),
            Settore(2, "LIRF_ATIS", "ATIS", "135.975"),
            Settore(3, "LIRF_GND", "GND", "121.700"),
        };

        var v = AirportSectionProjection.Frequencies(catalogo, null);

        Assert.Equal(new[] { "LIRF_ATIS", "LIRF_GND", "LIRF_TWR" }, v.Rows.Select(r => r.Callsign));
        Assert.Single(v.Rows, r => r.IsPrimary);
        Assert.Equal("LIRF_TWR", v.Rows.Single(r => r.IsPrimary).Callsign);
    }

    [Fact]
    public void Le_righe_nascoste_e_quelle_senza_frequenza_restano_fuori_dal_documento()
    {
        // ⚠️ Il catalogo settori serve anche all'amministrazione della struttura: contiene righe che nel
        // documento non ci vanno. Una posizione nascosta e' nascosta anche qui.
        var catalogo = new[]
        {
            Settore(1, "LIRF_TWR", "TWR", "118.700"),
            Settore(2, "LIRF_DEL", "DEL", "121.900", nascosto: true),
            Settore(3, "LIRF_APP", "APP", null),
        };

        var v = AirportSectionProjection.Frequencies(catalogo, null);
        Assert.Equal("LIRF_TWR", Assert.Single(v.Rows).Callsign);
    }

    [Fact]
    public void I_link_ad_altri_enti_vanno_in_coda_e_non_sono_mai_principali()
    {
        var v = AirportSectionProjection.Frequencies(
            new[] { Settore(1, "LIRF_TWR", "TWR", "118.700", principale: true) },
            new[] { new FrequencyLinkRow(1, 9, "Roma Approach", "LIRF_APP", "119.200") });

        Assert.Equal(2, v.Rows.Count);
        Assert.Equal("Roma Approach", v.Rows[1].Name);
        Assert.Equal("119.200", v.Rows[1].Frequency);
        Assert.False(v.Rows[1].IsPrimary);
    }

    private static AirportSectorRow Settore(int id, string callsign, string posizione, string? freq,
        bool principale = false, bool nascosto = false) =>
        new(id, callsign, "LIRF", "LIRR", posizione, null, freq, null, null, nascosto, false, principale, false);

    // ---- regole piste ----

    [Fact]
    public void La_condizione_di_una_regola_e_testo_leggibile_non_soglie_da_comporre()
    {
        var v = AirportSectionProjection.Rules(Profilo(regole: new[]
        {
            new RunwayRuleRow(1, "16R", "16L", "Sud", 5, 25, RunwaySurface.Wet, "in caso di pioggia"),
        }));

        var r = Assert.Single(v.Rows);
        Assert.Equal(1, r.Position);
        Assert.Equal("Sud: vento in coda ≤ 5 kt, traverso ≤ 25 kt, pista bagnata", r.Condition);
        Assert.Equal("16R", r.Dep);
        Assert.Equal("16L", r.Arr);
        Assert.Equal("in caso di pioggia", r.Note);
    }

    [Fact]
    public void Le_regole_sono_numerate_nell_ordine_in_cui_si_applicano()
    {
        // Il numero non è decorazione: si applica la PRIMA regola le cui condizioni sono soddisfatte.
        var v = AirportSectionProjection.Rules(Profilo(regole: new[] { Regola(3), Regola(8), Regola(12) }));
        Assert.Equal(new[] { 1, 2, 3 }, v.Rows.Select(r => r.Position));
    }

    [Fact]
    public void Una_finestra_oraria_o_stagionale_entra_nella_condizione()
    {
        Assert.Contains("22:00–06:00 LT",
            AirportSectionProjection.RuleCondition(Regola(daMin: 22 * 60, aMin: 6 * 60)));
        Assert.Contains("dalle 22:00 LT", AirportSectionProjection.RuleCondition(Regola(daMin: 22 * 60)));
        Assert.Contains("fino alle 06:00 LT", AirportSectionProjection.RuleCondition(Regola(aMin: 6 * 60)));
        // MMDD = mese × 100 + giorno: il 1º giugno è 601, il 30 settembre 930.
        Assert.Contains("dal 1 giu al 30 set",
            AirportSectionProjection.RuleCondition(Regola(dalMmdd: 601, alMmdd: 930)));
    }

    [Fact]
    public void I_giorni_si_mostrano_solo_quando_sono_un_vincolo()
    {
        // Tutti e sette (o nessuno) non è un vincolo: scriverlo direbbe al lettore che c'è una restrizione.
        Assert.DoesNotContain("lun", AirportSectionProjection.RuleCondition(Regola(giorni: 0b111_1111)));
        Assert.DoesNotContain("lun", AirportSectionProjection.RuleCondition(Regola(giorni: 0)));
        Assert.DoesNotContain("lun", AirportSectionProjection.RuleCondition(Regola(giorni: null)));
        Assert.Contains("sab/dom", AirportSectionProjection.RuleCondition(Regola(giorni: 0b110_0000)));
    }

    [Fact]
    public void La_parita_dei_giorni_entra_nella_condizione()
    {
        Assert.Contains("giorni pari", AirportSectionProjection.RuleCondition(Regola(parita: DateParity.Even)));
        Assert.Contains("giorni dispari", AirportSectionProjection.RuleCondition(Regola(parita: DateParity.Odd)));
        Assert.DoesNotContain("giorni", AirportSectionProjection.RuleCondition(Regola(parita: DateParity.Any)));
    }
}
