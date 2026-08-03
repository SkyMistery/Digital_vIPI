using Vipi.AuroraBridge.Contracts;
using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Logica di presentazione della finestra. Sta in Core proprio per essere verificabile così: la regola che
/// conta è che un pulsante spento dica SEMPRE perché, e che le tre ragioni («niente traffico», «livello
/// assente nella vIPI», «traffico non assunto») restino distinte, perché si risolvono in modi diversi.
/// </summary>
public class BridgeViewModelTests
{
    private static TransferCandidate Candidate(bool writable = true, string? value = "210", string cop = "ASPIR") => new()
    {
        Cop = cop,
        AuroraValue = value,
        Writable = writable,
        ResolvedHandler = "LIRR_NC_CTR",
        HandlerOnline = true,
        Level = new CandidateLevel { Value = 210, Unit = "Fl", Constraint = "AtOrBelow", Text = "FL210- (dispari)" },
        Reasons = { "arrivo a LIRF", "CoP ASPIR in rotta" },
    };

    private static BridgeState State(TransferCandidate? candidate = null, bool assumed = true, string? traffic = "AZA123") =>
        new()
        {
            AuroraConnected = true,
            OwnerCallsign = "LIBB_ES_CTR",
            ConnectedCallsign = "LIBB_ES_CTR",
            SelectedTraffic = traffic,
            TrafficAssumed = assumed,
            FlightPlan = new FlightPlanRecord("LIBD", "LIRF", null, "0820", "A320", "M", "I", "S", null,
                "F350", "N0450", null, null, "PISIP DCT ASPIR", null),
            Position = new TrafficPositionRecord(200, 200, 32000, 450, 41.0, 13.0, "2000", null, null, null, null,
                assumed ? "LIBB_ES_CTR" : null, null, false, true, true, null, "1", null, -400, null),
            Proposal = candidate is null ? null : new TransferResolveResponse { Candidates = { candidate } },
        };

    [Fact]
    public void Un_candidato_scrivibile_su_traffico_assunto_accende_il_pulsante()
    {
        var (canWrite, hint) = BridgeViewModel.WriteAbility(Candidate(), assumed: true, traffic: "AZA123");

        Assert.True(canWrite);
        Assert.Equal("Scrivi «210»", hint);
    }

    [Fact]
    public void Senza_livello_nella_vIPI_il_pulsante_spiega_che_manca_il_dato()
    {
        // È il caso dei sorvoli LIBB: vincolo presente, valore assente → non si scrive nulla.
        var (canWrite, hint) = BridgeViewModel.WriteAbility(
            Candidate(writable: false, value: null), assumed: true, traffic: "AZA123");

        Assert.False(canWrite);
        Assert.Contains("manca il valore nella vIPI", hint);
    }

    [Fact]
    public void Su_traffico_non_assunto_il_pulsante_spiega_il_vincolo_di_Aurora()
    {
        var (canWrite, hint) = BridgeViewModel.WriteAbility(Candidate(), assumed: false, traffic: "AZA123");

        Assert.False(canWrite);
        Assert.Contains("non assunto", hint);
    }

    [Fact]
    public void Senza_selezione_il_motivo_e_quello_e_non_un_altro()
    {
        var (canWrite, hint) = BridgeViewModel.WriteAbility(Candidate(), assumed: false, traffic: null);

        Assert.False(canWrite);
        Assert.Contains("Nessun traffico", hint);
    }

    [Fact]
    public void La_riga_del_volo_riassume_rotta_crociera_quota_e_assunzione()
    {
        var line = BridgeViewModel.FormatFlight(State());

        Assert.Contains("AZA123", line);
        Assert.Contains("LIBD → LIRF", line);
        Assert.Contains("FL350", line);
        Assert.Contains("ASSUNTO", line);
    }

    [Fact]
    public void Senza_traffico_la_riga_lo_dice_invece_di_restare_vuota()
    {
        Assert.Equal("Nessun traffico selezionato", BridgeViewModel.FormatFlight(State(traffic: null)));
    }

    [Theory]
    [InlineData("matched", "✓")]
    [InlineData("unmatched", "✗")]
    [InlineData("unknown", "?")]
    public void La_condizione_ha_un_simbolo_per_ogni_esito(string match, string symbol)
    {
        var badge = BridgeViewModel.ConditionBadge(new CandidateCondition { Display = "RWY 16L", Match = match });

        Assert.StartsWith(symbol, badge);
    }

    [Fact]
    public void Senza_condizioni_non_si_mostra_nessun_distintivo()
    {
        Assert.Equal("", BridgeViewModel.ConditionBadge(new CandidateCondition { Match = "none" }));
    }

    [Fact]
    public void Gli_avvisi_del_sito_e_della_cache_si_uniscono_in_una_riga_sola()
    {
        var state = State(Candidate());
        var withWarnings = new BridgeState
        {
            AuroraConnected = true,
            SelectedTraffic = state.SelectedTraffic,
            Notice = "Sito irraggiungibile: sto mostrando l'ultima risposta valida.",
            ProposalFromCache = true,
            Proposal = new TransferResolveResponse { Warnings = { "Condizioni non verificabili." } },
        };

        var warning = BridgeViewModel.ComposeWarning(withWarnings);

        Assert.Contains("Sito irraggiungibile", warning);
        Assert.Contains("cache locale", warning);
        Assert.Contains("Condizioni non verificabili", warning);
    }

    [Fact]
    public void Senza_nulla_da_dire_non_c_e_nessun_avviso()
    {
        Assert.Null(BridgeViewModel.ComposeWarning(State(Candidate())));
    }

    [Fact]
    public void Le_righe_dei_candidati_arrivano_gia_formattate()
    {
        var rows = BridgeViewModel.BuildRows(State(Candidate())).ToList();

        var row = Assert.Single(rows);
        Assert.Equal("ASPIR", row.Cop);
        Assert.Equal("FL210- (dispari)", row.Level);
        Assert.Equal("LIRR_NC_CTR", row.Handler);
        Assert.Equal("arrivo a LIRF · CoP ASPIR in rotta", row.Reasons);
        Assert.True(row.CanWrite);
        Assert.Equal("", row.HandlerNote);
    }

    [Fact]
    public void Un_ente_offline_viene_segnalato_nella_riga()
    {
        var candidate = Candidate();
        candidate.HandlerOnline = false;
        candidate.ResolvedHandler = "UNICOM";

        var row = BridgeViewModel.BuildRows(State(candidate)).Single();

        Assert.Equal("UNICOM", row.Handler);
        Assert.Equal(" (offline)", row.HandlerNote);
    }

    [Fact]
    public void Le_impostazioni_sopravvivono_al_salvataggio_e_i_file_rotti_non_bloccano_il_tool()
    {
        var file = Path.Combine(Path.GetTempPath(), $"vipi-bridge-{Guid.NewGuid():N}.json");
        try
        {
            new BridgeSettings { SiteUrl = "http://127.0.0.1:5034", OwnerOverride = "LIBB_ES_CTR", AlwaysOnTop = false }
                .Save(file);

            var loaded = BridgeSettings.Load(file);
            Assert.Equal("http://127.0.0.1:5034", loaded.SiteUrl);
            Assert.Equal("LIBB_ES_CTR", loaded.OwnerOverride);
            Assert.False(loaded.AlwaysOnTop);

            File.WriteAllText(file, "{ non è json");
            Assert.Equal("https://it.ivao.aero", BridgeSettings.Load(file).SiteUrl);   // default, non eccezione
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Un_intervallo_di_polling_assurdo_viene_riportato_a_un_minimo_sensato()
    {
        Assert.Equal(250, new BridgeSettings { SelectionPollMs = 5 }.ToPollingOptions().SelectionMs);
        Assert.Equal(2000, new BridgeSettings { SelectionPollMs = 2000 }.ToPollingOptions().SelectionMs);
    }
}
