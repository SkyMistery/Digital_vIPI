using System.Text.Json;
using Vipi.Application.Awos;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le soglie «mai in partenza» / «mai in arrivo» (carta <c>docs/feature/2026-09-17-pista-mai-usare.md</c>): il ripiego
/// sul vento non le sceglie nel loro verso, le regole che le nominano restano valide.
/// </summary>
public class PistaMaiUsareTests
{
    private static RunwayExclusions Escluse(string[]? dep = null, string[]? arr = null) =>
        new(dep ?? Array.Empty<string>(), arr ?? Array.Empty<string>());

    [Fact]
    public void Senza_esclusioni_il_ripiego_e_quello_di_sempre()
    {
        var prima = RunwaySuggestion.Suggest(new[] { "35L", "35R", "17L", "17R" }, 350, 12);
        var dopo = RunwaySuggestion.Suggest(new[] { "35L", "35R", "17L", "17R" }, 350, 12, RunwayExclusions.None);

        Assert.Equal(("35R", "35L"), (prima.DepIdent, prima.ArrIdent));   // parallele: destra partenze, sinistra arrivi
        Assert.Equal((prima.DepIdent, prima.ArrIdent, prima.Reason), (dopo.DepIdent, dopo.ArrIdent, dopo.Reason));
    }

    [Fact]
    public void Esclusa_in_tutti_e_due_i_versi_la_soglia_non_esce_mai()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12, Escluse(dep: new[] { "16" }, arr: new[] { "16" }));

        Assert.Equal(("34", "34"), (r.DepIdent, r.ArrIdent));
        Assert.Equal(SuggestionReason.Tailwind, r.Reason);   // la meno peggio, e il motivo lo dice
        Assert.DoesNotContain(r.Ranked, p => p.Ident == "16");
    }

    /// <summary>Il caso che ha fatto nascere i due flag: una soglia usata solo per gli arrivi.</summary>
    [Fact]
    public void Mai_in_partenza_lascia_la_soglia_agli_arrivi()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12, Escluse(dep: new[] { "16" }));

        Assert.Equal("34", r.DepIdent);   // in partenza si va sull'altra, anche col vento in coda
        Assert.Equal("16", r.ArrIdent);   // in arrivo resta la migliore
    }

    [Fact]
    public void Mai_in_arrivo_e_parallele_col_confronto_senza_maiuscole()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16l", "16R", "34L", "34R" }, 160, 12, Escluse(arr: new[] { " 16L " }));

        Assert.Equal("16R", r.DepIdent);   // partenze: le parallele restano due, la destra
        Assert.Equal("16R", r.ArrIdent);   // arrivi: la sinistra è esclusa, resta la destra
    }

    [Fact]
    public void Un_verso_tutto_escluso_resta_senza_pista_e_non_ripiega_su_best()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12, Escluse(arr: new[] { "16", "34" }));

        Assert.NotNull(r.Best);
        Assert.Equal("16", r.DepIdent);
        Assert.Null(r.ArrIdent);
    }

    [Fact]
    public void Tutte_escluse_in_tutti_e_due_i_versi_e_un_motivo_diverso_da_nessuna_pista()
    {
        var tutte = RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12,
            Escluse(dep: new[] { "16", "34" }, arr: new[] { "16", "34" }));
        Assert.Null(tutte.Best);
        Assert.Equal(SuggestionReason.AllNeverUse, tutte.Reason);

        Assert.Equal(SuggestionReason.NoRunways,
            RunwaySuggestion.Suggest(Array.Empty<string>(), 160, 12, Escluse(dep: new[] { "16" })).Reason);
    }

    /// <summary>⚠️ Decisione del committente: il flag vale SOLO per il ripiego. Una regola che nomina la soglia vince.</summary>
    [Fact]
    public void Una_regola_che_nomina_la_soglia_esclusa_continua_a_valere()
    {
        var regole = new List<RunwayRuleRow> { new(0, "16", "16", "Sud", 10, null, RunwaySurface.Any, null) };

        var attiva = AwosComposition.PistaAttiva(regole, new[] { "16", "34" }, null,
            escluse: Escluse(dep: new[] { "16" }, arr: new[] { "16" }));

        Assert.Equal(AwosRunwaySource.Regola, attiva.Sorgente);
        Assert.Equal(new[] { "16" }, attiva.Dep);
    }

    [Fact]
    public void Il_vawos_ripiega_per_verso_e_lascia_vuoto_il_verso_senza_piste()
    {
        var metar = MetarParser.ParseMetar("LIBP 171250Z 16012KT 9999 FEW030 20/10 Q1015");
        var righe = new[]
        {
            new RunwayRow(1, "16", 2400, 160, null, null, null, null, null, NeverDeparture: true),
            new RunwayRow(2, "34", 2400, 340, null, null, null, null, null, NeverArrival: true),
        };

        var attiva = AwosComposition.PistaAttiva(Array.Empty<RunwayRuleRow>(), new[] { "16", "34" }, metar,
            escluse: RunwayRow.Esclusioni(righe));

        Assert.Equal(AwosRunwaySource.Vento, attiva.Sorgente);
        Assert.Equal(new[] { "34" }, attiva.Dep);
        Assert.Equal(new[] { "16" }, attiva.Arr);
    }

    /// <summary>
    /// ⚠️ Il documento e il vAWOS leggono i flag dalla sezione Piste CONGELATA: devono sopravvivere al giro dello
    /// snapshot, e uno snapshot di prima (senza i campi) vale «nessuna esclusione».
    /// </summary>
    [Fact]
    public void I_flag_sopravvivono_allo_snapshot_e_uno_snapshot_vecchio_non_esclude_niente()
    {
        var vista = new AirportRunwaysView(new[]
        {
            new AirportRunwayRowView("16", 2400, "2400", "2400", "—", "—", "—", NeverDeparture: true),
            new AirportRunwayRowView("34", 2400, "2400", "2400", "—", "—", "—", NeverArrival: true),
        });
        var riletta = JsonSerializer.Deserialize<AirportRunwaysView>(JsonSerializer.Serialize(vista))!;
        var e = AirportRunwayRowView.Esclusioni(riletta.Rows);
        Assert.Equal(new[] { "16" }, e.Departures);
        Assert.Equal(new[] { "34" }, e.Arrivals);

        var vecchia = JsonSerializer.Deserialize<AirportRunwaysView>(
            "{\"Rows\":[{\"Ident\":\"16\",\"LengthM\":2400,\"Tora\":\"\",\"Lda\":\"\",\"AppProcedures\":\"\",\"Patterns\":\"\",\"Circling\":\"\"}]}")!;
        var nessuna = AirportRunwayRowView.Esclusioni(vecchia.Rows);
        Assert.Empty(nessuna.Departures);
        Assert.Empty(nessuna.Arrivals);
    }
}
