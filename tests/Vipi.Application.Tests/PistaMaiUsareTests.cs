using Vipi.Application.Awos;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La soglia «mai usare» (carta <c>docs/feature/2026-09-17-pista-mai-usare.md</c>): il ripiego sul vento non la
/// sceglie mai, le regole che la nominano restano valide.
/// </summary>
public class PistaMaiUsareTests
{
    [Fact]
    public void Il_ripiego_non_sceglie_la_soglia_mai_usare_anche_se_il_vento_la_favorisce()
    {
        // Vento 160/12: senza flag vince il 16.
        Assert.Equal("16", RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12).Best!.Ident);

        var r = RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12, new[] { "16" });

        Assert.Equal("34", r.Best!.Ident);
        Assert.Equal(SuggestionReason.Tailwind, r.Reason);   // la meno peggio, e il motivo lo dice
        Assert.DoesNotContain(r.Ranked, p => p.Ident == "16");
    }

    [Fact]
    public void Il_confronto_ignora_maiuscole_e_spazi()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16l", "16R", "34L", "34R" }, 160, 12, new[] { " 16L " });

        Assert.Equal("16R", r.Best!.Ident);
        Assert.Equal("16R", r.DepIdent);   // sparita la parallela: niente split arrivi/partenze
        Assert.Equal("16R", r.ArrIdent);
    }

    [Fact]
    public void Tutte_escluse_e_un_motivo_diverso_da_nessuna_pista()
    {
        var tutte = RunwaySuggestion.Suggest(new[] { "16", "34" }, 160, 12, new[] { "16", "34" });
        Assert.Null(tutte.Best);
        Assert.Equal(SuggestionReason.AllNeverUse, tutte.Reason);

        Assert.Equal(SuggestionReason.NoRunways, RunwaySuggestion.Suggest(Array.Empty<string>(), 160, 12, new[] { "16" }).Reason);
    }

    /// <summary>⚠️ Decisione del committente: il flag vale SOLO per il ripiego. Una regola che nomina la soglia vince.</summary>
    [Fact]
    public void Una_regola_che_nomina_la_soglia_mai_usare_continua_a_valere()
    {
        var regole = new List<RunwayRuleRow>
        {
            new(0, "16", "16", "Sud", 10, null, RunwaySurface.Any, null),
        };

        var attiva = AwosComposition.PistaAttiva(regole, new[] { "16", "34" }, null, maiUsare: new[] { "16" });

        Assert.Equal(AwosRunwaySource.Regola, attiva.Sorgente);
        Assert.Equal(new[] { "16" }, attiva.Dep);
    }

    [Fact]
    public void Il_vawos_ripiega_senza_la_soglia_mai_usare()
    {
        var metar = Vipi.Application.Weather.MetarParser.ParseMetar("LIBP 171250Z 16012KT 9999 FEW030 20/10 Q1015");

        var attiva = AwosComposition.PistaAttiva(Array.Empty<RunwayRuleRow>(), new[] { "16", "34" }, metar,
            maiUsare: RunwayRow.MaiUsare(new[]
            {
                new RunwayRow(1, "16", 2400, 160, null, null, null, null, null, NeverUse: true),
                new RunwayRow(2, "34", 2400, 340, null, null, null, null, null),
            }));

        Assert.Equal(AwosRunwaySource.Vento, attiva.Sorgente);
        Assert.Equal(new[] { "34" }, attiva.Dep);
    }
}
