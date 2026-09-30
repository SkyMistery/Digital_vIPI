using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Le API degli aeroporti (carta <c>docs/feature/2026-09-30-api-aeroporti.md</c>): quale documento si legge, e come le
/// celle del documento diventano JSON. La porta (401/403/200/404) sta negli E2E, in <c>ChiaviApiTests</c>.
/// </summary>
public class ApiAeroportiTests
{
    private static AirportSidRowView Sid(string runway, string fix, string name, string climb = "—") =>
        new(runway, fix, name, "—", climb, "RNAV", "—", "—", "—");

    private static AirportDerived Derivate(IReadOnlyList<AirportSidRowView> sids, IReadOnlyList<AirportSidRowView>? stars = null) =>
        AirportDerived.Empty with
        {
            Transition = new AirportTransitionView(6000, Array.Empty<AirportTlRowView>()),
            Sids = new AirportSidView(sids),
            Stars = new AirportSidView(stars ?? Array.Empty<AirportSidRowView>()),
        };

    private static JsonElement Json(object o) =>
        JsonSerializer.SerializeToElement(o, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    // ─── Le celle ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("—", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    [InlineData(" 16L ", "16L")]
    public void Il_trattino_del_documento_diventa_null(string? cella, string? atteso)
    {
        Assert.Equal(atteso, ApiAeroporti.Proiezione.Valore(cella));
    }

    [Theory]
    [InlineData("5000", 5000, false)]
    [InlineData("5,000", 5000, false)]
    [InlineData("9000 (to coord with APP)", 9000, true)]
    [InlineData("to coord with APP", null, true)]
    [InlineData("—", null, false)]
    public void La_quota_di_salita_si_legge_in_piedi_con_la_nota_dell_APP(string cella, int? piedi, bool conApp)
    {
        Assert.Equal((piedi, conApp), ApiAeroporti.Proiezione.Salita(cella));
    }

    [Fact]
    public void L_initial_climb_si_scrive_come_nel_documento_in_FL_sopra_la_TA()
    {
        var riga = ApiAeroporti.Proiezione.Riga(Sid("16L", "ELKAP", "ELKAP5A", "9000 (to coord with APP)"),
                                                ProcedureKind.Sid, transitionAltitudeFt: 6000);

        Assert.Equal("FL90 (to coord with APP)", riga.InitialClimb);
        Assert.Equal(9000, riga.InitialClimbFt);
        Assert.True(riga.InitialClimbByApp);
        Assert.Null(riga.Transition);
        Assert.Null(riga.Cat);
    }

    // ─── La scheda ─────────────────────────────────────────────────────────────────

    [Fact]
    public void La_scheda_porta_TA_fasce_piste_e_frequenze_del_documento()
    {
        var scalo = new AirportData
        {
            AirportId = 1, Icao = "LIRF", Name = "Roma Fiumicino", AccCode = "LIRR",
            TransitionLevels = Array.Empty<TlRow>(), Runways = Array.Empty<RunwayRow>(),
            Rules = Array.Empty<RunwayRuleRow>(), Sids = Array.Empty<SidRow>(), Links = Array.Empty<FrequencyLinkRow>(),
        };
        var d = AirportDerived.Empty with
        {
            Transition = new AirportTransitionView(7000, new[] { new AirportTlRowView("1013-1030", "FL80") }),
            Runways = new AirportRunwaysView(new[]
            {
                new AirportRunwayRowView("16L", 3900, "3900", "—", "ILS,RNP", "—", "—", Threshold: ""),
            }),
            Frequencies = new AirportFreqView(new[] { new AirportFreqRowView("Tower", "LIRF_TWR", "118.700", true) }),
        };

        var json = Json(ApiAeroporti.Proiezione.Scheda(scalo, ReleaseTargetType.Airport, d));

        Assert.Equal("vipi", json.GetProperty("document").GetString());
        Assert.Equal(7000, json.GetProperty("transitionAltitudeFt").GetInt32());
        Assert.Equal("FL80", json.GetProperty("transitionLevels")[0].GetProperty("level").GetString());
        var pista = json.GetProperty("runways")[0];
        Assert.Equal(new[] { "ILS", "RNP" }, pista.GetProperty("approaches").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(JsonValueKind.Null, pista.GetProperty("lda").ValueKind);
        Assert.Equal(JsonValueKind.Null, pista.GetProperty("threshold").ValueKind);
        Assert.True(json.GetProperty("frequencies")[0].GetProperty("primary").GetBoolean());
    }

    // ─── Le procedure ──────────────────────────────────────────────────────────────

    [Fact]
    public void Il_filtro_per_pista_tiene_quelle_della_pista_e_quelle_senza_pista()
    {
        var d = Derivate(new[] { Sid("16L", "ELKAP", "ELKAP5A"), Sid("34R", "ELKAP", "ELKAP5B"), Sid("—", "TORVU", "TORVU1X") });

        var json = Json(ApiAeroporti.Proiezione.Procedure("LIRF", ProcedureKind.Sid, d, runway: "16l"));

        Assert.Equal(2, json.GetProperty("count").GetInt32());
        var nomi = json.GetProperty("sids").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "ELKAP5A", "TORVU1X" }, nomi);
    }

    [Fact]
    public void Senza_filtro_escono_tutte_nell_ordine_del_documento()
    {
        var d = Derivate(new[] { Sid("16L", "ELKAP", "ELKAP5A"), Sid("34R", "ELKAP", "ELKAP5B") });

        var json = Json(ApiAeroporti.Proiezione.Procedure("LIRF", ProcedureKind.Sid, d, runway: null));

        Assert.Equal(6000, json.GetProperty("transitionAltitudeFt").GetInt32());
        Assert.Equal(new[] { "ELKAP5A", "ELKAP5B" },
            json.GetProperty("sids").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
    }

    [Fact]
    public void Le_STAR_escono_dalla_loro_meta_della_tabella_e_senza_initial_climb()
    {
        var d = Derivate(new[] { Sid("16L", "ELKAP", "ELKAP5A") }, stars: new[] { Sid("16L", "TAQ", "TAQ1A", "5000") });

        var json = Json(ApiAeroporti.Proiezione.Procedure("LIRF", ProcedureKind.Star, d, runway: null));

        Assert.False(json.TryGetProperty("sids", out _));
        var star = Assert.Single(json.GetProperty("stars").EnumerateArray());
        Assert.Equal("TAQ1A", star.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, star.GetProperty("initialClimb").ValueKind);
    }
}
