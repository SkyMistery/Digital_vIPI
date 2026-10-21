using System.Text.Json;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Le API dei settori (S119, carta <c>docs/feature/2026-10-21-api-settori.md</c>): come la gerarchia e i coordinamenti
/// diventano JSON. La porta (401/403/200/404) sta negli E2E, in <c>ChiaviApiTests</c>; la lettura dal database in
/// <c>SettoriPerApiTests</c>.
/// </summary>
public class ApiSettoriTests
{
    private static readonly ApiAccRow Roma = new("LIRR", "Roma", false);

    private static JsonElement Json(object o) =>
        JsonSerializer.SerializeToElement(o, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static TransferPointRow Punto(string cop, string? ricevente, int? fl = 240) => new()
    {
        Id = 1, Cop = cop, LevelValue = fl, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
        LevelText = fl is int v ? $"FL{v} o inferiore" : "", NextSectorCallsign = ricevente, Order = 1,
    };

    private static TransferFlowRow Flusso(string cedente, params TransferPointRow[] punti) => new()
    {
        Id = 7, AccCode = "LIRR", OwningSectorId = 1, OwningSectorCallsign = cedente, Kind = TransferFlowKind.Arrival,
        AirportIcao = "LIRF", Order = 1, Points = punti,
    };

    // ─── I centri e i settori ───────────────────────────────────────────────────

    [Fact]
    public void I_centri_col_loro_numero()
    {
        var j = Json(ApiSettori.Proiezione.Centri(new[] { Roma, new ApiAccRow("LIMM", "Milano", false) }));

        Assert.Equal(2, j.GetProperty("count").GetInt32());
        Assert.Equal("LIRR", j.GetProperty("accs")[0].GetProperty("code").GetString());
        Assert.False(j.GetProperty("accs")[0].GetProperty("military").GetBoolean());
    }

    /// <summary>
    /// Un settore porta il padre (anche di un altro centro), la banda dichiarata e le righe di ripiego nell'ordine;
    /// il tipo come lo scrive il nominativo. Senza banda né ripiego: null e lista vuota, non campi spariti.
    /// </summary>
    [Fact]
    public void Un_settore_porta_padre_banda_e_ripiego()
    {
        var righe = new[]
        {
            new ApiSectorRow("LIRR_NE_CTR", "Roma Nord-Est", SectorType.Ctr, SectorKind.Acc, null, "125.500", null, 1),
            new ApiSectorRow("LIRF_APP", "Roma Avvicinamento", SectorType.App, SectorKind.Airport, "LIRF", "119.200", "LIRR_NE_CTR", 2),
        };
        var topo = new Topology
        {
            Sectors = new[] { "LIRR_NE_CTR", "LIRF_APP" },
            Parent = new Dictionary<string, string> { ["LIRF_APP"] = "LIRR_NE_CTR" },
            Bands = new Dictionary<string, (int?, int?)>(StringComparer.OrdinalIgnoreCase) { ["LIRR_NE_CTR"] = (null, 66000) },
            Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase)
            {
                ["LIRR_NE_CTR"] = new[] { new FallbackRow("LIRR_SU_CTR", 24500, null), new FallbackRow("LIRR_CTR", null, null) },
            },
        };

        var j = Json(ApiSettori.Proiezione.Settori(Roma, righe, topo));

        Assert.Equal("LIRR", j.GetProperty("acc").GetString());
        var ctr = j.GetProperty("sectors")[0];
        Assert.Equal("CTR", ctr.GetProperty("type").GetString());
        Assert.Equal("acc", ctr.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, ctr.GetProperty("parent").ValueKind);
        Assert.Equal(66000, ctr.GetProperty("band").GetProperty("topFt").GetInt32());
        Assert.Equal(JsonValueKind.Null, ctr.GetProperty("band").GetProperty("baseFt").ValueKind);
        var ripiego = ctr.GetProperty("fallbacks");
        Assert.Equal(new[] { "LIRR_SU_CTR", "LIRR_CTR" }, ripiego.EnumerateArray().Select(x => x.GetProperty("target").GetString()));
        Assert.Equal(24500, ripiego[0].GetProperty("baseFt").GetInt32());
        Assert.Equal("callsign", ripiego[0].GetProperty("kind").GetString());

        var app = j.GetProperty("sectors")[1];
        Assert.Equal("APP", app.GetProperty("type").GetString());
        Assert.Equal("LIRF", app.GetProperty("airport").GetString());
        Assert.Equal("LIRR_NE_CTR", app.GetProperty("parent").GetString());
        Assert.Equal(JsonValueKind.Null, app.GetProperty("band").ValueKind);
        Assert.Equal(0, app.GetProperty("fallbacks").GetArrayLength());
    }

    // ─── I coordinamenti ─────────────────────────────────────────────────────────

    [Fact]
    public void Scritti_le_righe_come_negli_accordi_con_le_parole_al_posto_dei_numeri()
    {
        var j = Json(ApiSettori.Proiezione.Scritti(Roma, new[] { Flusso("LIRR_NE_CTR", Punto("TIBER", "LIRF_APP")) }));

        Assert.False(j.GetProperty("resolved").GetBoolean());
        var f = j.GetProperty("flows")[0];
        Assert.Equal("LIRR_NE_CTR", f.GetProperty("owner").GetString());
        Assert.Equal("arrival", f.GetProperty("kind").GetString());
        Assert.Equal(new[] { "LIRF" }, f.GetProperty("airports").EnumerateArray().Select(x => x.GetString()));
        var p = f.GetProperty("points")[0];
        Assert.Equal("TIBER", p.GetProperty("cop").GetString());
        Assert.Equal("LIRF_APP", p.GetProperty("receiver").GetString());
        Assert.Equal(240, p.GetProperty("level").GetProperty("value").GetInt32());
        Assert.Equal("fl", p.GetProperty("level").GetProperty("unit").GetString());
        Assert.Equal("atOrBelow", p.GetProperty("level").GetProperty("constraint").GetString());
        // Senza risoluzione chi prende davvero non si sa: null, non il ricevente scritto.
        Assert.Equal(JsonValueKind.Null, p.GetProperty("handler").ValueKind);
        Assert.Equal(JsonValueKind.Null, p.GetProperty("handoff").ValueKind);
        Assert.Equal(JsonValueKind.Null, p.GetProperty("condition").ValueKind);
    }

    /// <summary>Un accordo per più scali li porta tutti, nell'ordine scritto (la frase del documento fa lo stesso).</summary>
    [Fact]
    public void Un_flusso_di_piu_scali_li_porta_tutti()
    {
        var f = new TransferFlowRow
        {
            Id = 1, AccCode = "LICC", OwningSectorId = 1, OwningSectorCallsign = "LICC_CTR", Kind = TransferFlowKind.Arrival,
            AirportIcao = "LICC", AirportIcaos = new[] { "LICC", "LICZ" }, Order = 1, Points = Array.Empty<TransferPointRow>(),
        };

        Assert.Equal(new[] { "LICC", "LICZ" }, ApiSettori.Proiezione.Scali(f));
    }

    /// <summary>
    /// Risolte: chi cede davvero e chi riceve davvero con gli online dati, accanto a quel che è scritto. L'elenco degli
    /// online torna indietro, in ordine, così chi integra sa su che cosa è stata fatta la risposta.
    /// </summary>
    [Fact]
    public void Risolti_dicono_chi_cede_e_chi_riceve_adesso()
    {
        var punto = Punto("TIBER", "LIRF_APP");
        var risolto = new ResolvedTransferFlow
        {
            Flow = Flusso("LIRR_NE_CTR", punto), ResolvedOwnerCallsign = "LIRR_CTR", OwnerOnline = true,
            Points = new[] { new ResolvedTransferPoint { Point = punto, ResolvedHandler = "UNICOM", IsOnline = false } },
        };

        var j = Json(ApiSettori.Proiezione.Risolti(Roma, ApiSettori.Online("lirr_ctr"), new[] { risolto }));

        Assert.True(j.GetProperty("resolved").GetBoolean());
        Assert.Equal(new[] { "LIRR_CTR" }, j.GetProperty("online").EnumerateArray().Select(x => x.GetString()));
        var f = j.GetProperty("flows")[0];
        Assert.Equal("LIRR_NE_CTR", f.GetProperty("owner").GetString());
        Assert.Equal("LIRR_CTR", f.GetProperty("resolvedOwner").GetString());
        Assert.True(f.GetProperty("ownerOnline").GetBoolean());
        var p = f.GetProperty("points")[0];
        Assert.Equal("LIRF_APP", p.GetProperty("receiver").GetString());
        Assert.Equal("UNICOM", p.GetProperty("handler").GetString());
        Assert.False(p.GetProperty("handlerOnline").GetBoolean());
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData(" lirr_ctr , LIRF_APP,,lirr_CTR ", new[] { "LIRF_APP", "LIRR_CTR" })]
    public void L_elenco_online_ignora_spazi_maiuscole_e_doppioni(string? elenco, string[] attesi)
    {
        Assert.Equal(attesi, ApiSettori.Online(elenco).OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>Il permesso ha le sue rotte, sotto la radice dei centri: la pagina delle chiavi le elenca da qui.</summary>
    [Fact]
    public void Il_permesso_settori_apre_le_tre_rotte()
    {
        Assert.Contains(ApiEndpoints.Settori, ApiEndpoints.Tutti);
        Assert.Equal(new[] { "accs", "sectors", "coordinations" },
                     ApiRotte.Tutte.Where(r => r.Endpoint == ApiEndpoints.Settori).Select(r => r.Id));
        Assert.All(ApiRotte.Tutte.Where(r => r.Endpoint == ApiEndpoints.Settori),
                   r => Assert.StartsWith(ApiSettori.Radice, r.Percorso));
    }
}
