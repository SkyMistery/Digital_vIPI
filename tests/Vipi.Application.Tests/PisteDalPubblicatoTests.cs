using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La porta unica su cui vAWOS, vista rapida ed elenco aeroporti decidono la pista in uso: regole e soglie escluse del
/// documento PUBBLICATO, i vivi solo quando non c'è niente di pubblicato o la sezione non è congelata.
/// </summary>
public class PisteDalPubblicatoTests
{
    private sealed class Lettore : IFrozenSectionReader
    {
        public Dictionary<string, string> Chiavi { get; } = new();
        public int Letture { get; private set; }
        public ReleaseTargetType? Chiesto { get; private set; }

        public Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Letture++;
            Chiesto = type;
            return Task.FromResult(FrozenSections.FromKeys(Chiavi));
        }
    }

    private static ManagedDoc Doc(ReleaseTargetType tipo, string icao, bool pubblicato = true) =>
        new(tipo, "doc", icao, "LIBB", IsPublished: true, HasDraft: false, IsHidden: false,
            ReleaseTarget: tipo, ReleaseKey: icao, DocumentId: 1, EffectiveCycle: pubblicato ? "2609" : null);

    private static readonly RunwayRuleRow[] RegoleVive = { new(1, "34", "34", "Viva", 10, null, RunwaySurface.Any, null) };
    private static readonly RunwayRow[] PisteVive =
    {
        new(1, "16", 2400, 160, null, null, null, null, null, NeverDeparture: true),
        new(2, "34", 2400, 340, null, null, null, null, null),
    };

    private static (PisteDalPubblicato Porta, Lettore Lettore) Porta()
    {
        var l = new Lettore();
        return (new PisteDalPubblicato(null!, l), l);   // l'elenco dei documenti lo passa il test
    }

    [Fact]
    public async Task Senza_documento_pubblicato_valgono_i_vivi_e_non_si_legge_nessuna_release()
    {
        var (porta, lettore) = Porta();

        var d = await porta.PerScaloAsync("LIBD", RegoleVive, null, PisteVive, new[] { Doc(ReleaseTargetType.Airport, "LIBD", pubblicato: false) });

        Assert.Same(RegoleVive, d.Regole);
        Assert.Equal(new[] { "16" }, d.Escluse.Departures);
        Assert.Equal(0, lettore.Letture);
    }

    [Fact]
    public async Task Con_sezioni_congelate_decide_il_pubblicato_non_la_casella_viva()
    {
        var (porta, lettore) = Porta();
        var regolePubblicate = new List<RunwayRuleRow> { new(9, "16", "16", "Pubblicata", 10, null, RunwaySurface.Any, null) };
        lettore.Chiavi["runwayrules"] = JsonSerializer.Serialize(new AirportRulesView(Array.Empty<AirportRuleRowView>(), regolePubblicate));
        lettore.Chiavi["runways"] = JsonSerializer.Serialize(new AirportRunwaysView(new[]
        {
            new AirportRunwayRowView("16", 2400, "", "", "", "", ""),                        // pubblicata SENZA flag
            new AirportRunwayRowView("34", 2400, "", "", "", "", "", NeverArrival: true),
        }));

        var d = await porta.PerScaloAsync("LIBD", RegoleVive, null, PisteVive, new[] { Doc(ReleaseTargetType.Airport, "LIBD") });

        Assert.Equal("Pubblicata", Assert.Single(d.Regole).Name);
        Assert.Empty(d.Escluse.Departures);                 // la 16 «mai in partenza» è solo nell'editor: non conta
        Assert.Equal(new[] { "34" }, d.Escluse.Arrivals);
        Assert.Equal(ReleaseTargetType.Airport, lettore.Chiesto);
    }

    /// <summary>Sezione in Live (lo snapshot non la porta): si ricade sui vivi, come nel documento.</summary>
    [Fact]
    public async Task Sezione_non_congelata_ricade_sui_vivi_e_un_vsop_militare_da_solo_decide_lui()
    {
        var (porta, lettore) = Porta();

        var d = await porta.PerScaloAsync("LIBA", RegoleVive, null, PisteVive, new[] { Doc(ReleaseTargetType.AirportMil, "LIBA") });

        Assert.Same(RegoleVive, d.Regole);
        Assert.Equal(new[] { "16" }, d.Escluse.Departures);
        Assert.Equal(ReleaseTargetType.AirportMil, lettore.Chiesto);
    }
}
