using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I disegni delle statistiche: sparkline, ciambella, striscia del turno.
///
/// <para>⚠️ Il test che conta davvero è quello sulla <b>cultura</b>: questi componenti scrivono numeri dentro
/// attributi SVG (<c>points</c>, <c>stroke-dasharray</c>), e a cultura italiana una virgola decimale li
/// spezza <b>in silenzio</b> — nessuna eccezione, nessun avviso, solo un disegno che non compare.</para>
/// </summary>
public class StatsGraficiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join(",", arguments), false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    public StatsGraficiTests() => Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    /// <summary>Esegue il corpo con la cultura italiana in vigore, e la rimette com'era.</summary>
    private static void InItaliano(Action corpo)
    {
        var prima = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("it-IT");
        try { corpo(); }
        finally { CultureInfo.CurrentCulture = prima; }
    }

    [Fact]
    public void La_sparkline_scrive_i_punti_col_punto_decimale_anche_in_italiano()
    {
        InItaliano(() =>
        {
            var f = RenderComponent<StatsSpark>(p => p.Add(c => c.Valori, new[] { 1.0, 7.0, 3.0 }));
            var punti = f.Find(".spark-line").GetAttribute("points")!;

            Assert.DoesNotContain(",,", punti);                       // «1,5,20,3» = coordinate perse
            Assert.All(punti.Split(' '), c => Assert.Equal(2, c.Split(',').Length));
        });
    }

    [Fact]
    public void Una_serie_piatta_non_produce_NaN()
    {
        var f = RenderComponent<StatsSpark>(p => p.Add(c => c.Valori, new[] { 4.0, 4.0, 4.0 }));
        Assert.DoesNotContain("NaN", f.Find(".spark-line").GetAttribute("points"));
    }

    [Fact]
    public void Sotto_i_due_punti_la_sparkline_non_disegna_niente()
    {
        var f = RenderComponent<StatsSpark>(p => p.Add(c => c.Valori, new[] { 9.0 }));
        Assert.Empty(f.FindAll("svg"));
    }

    [Fact]
    public void La_ciambella_scrive_archi_leggibili_anche_in_italiano()
    {
        InItaliano(() =>
        {
            var f = RenderComponent<StatsDonut>(p => p.Add(c => c.Fette, new[]
            {
                new StatsSlice("CTR", 3600, "p-ctr", "ctr"),
                new StatsSlice("TWR", 1800, "p-twr", "twr"),
            }));

            var archi = f.FindAll(".donut circle").Where(e => e.HasAttribute("stroke-dasharray")).ToList();
            Assert.Equal(2, archi.Count);
            Assert.All(archi, a =>
            {
                var d = a.GetAttribute("stroke-dasharray")!;
                Assert.DoesNotContain(",", d);          // due numeri separati da spazio, non da virgola
                Assert.Equal(2, d.Split(' ').Length);
            });
        });
    }

    [Fact]
    public void La_legenda_della_ciambella_dice_le_percentuali_in_cifre()
    {
        var f = RenderComponent<StatsDonut>(p => p.Add(c => c.Fette, new[]
        {
            new StatsSlice("CTR", 75, "p-ctr", "ctr"),
            new StatsSlice("TWR", 25, "p-twr", "twr"),
        }));

        var quote = f.FindAll(".donut-legend .q").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "75%", "25%" }, quote);
    }

    private static StatsTrafficRow Volo(int daMin, int aMin, string callsign,
        TrafficOrigin origine = TrafficOrigin.Aor, FlightPhase ultima = FlightPhase.Airborne)
    {
        var t0 = new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
        return new StatsTrafficRow(callsign, 1, "LIRF", "LIMC", "A320",
            t0.AddMinutes(daMin), t0.AddMinutes(aMin), aMin - daMin, true, false, origine,
            FlightPhase.Ground, ultima, true, 0, 20000, 20000, null, null);
    }

    [Fact]
    public void La_striscia_mette_una_barra_per_volo_e_le_dispone_in_corsie()
    {
        var t0 = new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
        var f = RenderComponent<SessionTimeline>(p => p
            .Add(c => c.Voli, new[] { Volo(0, 30, "AZA1"), Volo(5, 20, "AZA2"), Volo(40, 50, "AZA3") })
            .Add(c => c.Inizio, t0)
            .Add(c => c.Fine, t0.AddMinutes(60)));

        Assert.Equal(3, f.FindAll(".tl-bar").Count);
        Assert.Equal(2, f.FindAll(".tl-row").Count);          // la terza riprende la prima corsia
    }

    [Fact]
    public void Le_righe_ricostruite_restano_fuori_dalla_striscia()
    {
        // Non hanno una finestra vera (primo = ultimo avvistamento): sarebbero puntini a caso.
        var t0 = new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
        var f = RenderComponent<SessionTimeline>(p => p
            .Add(c => c.Voli, new[]
            {
                Volo(0, 30, "AZA1"),
                Volo(10, 10, "SAS9", TrafficOrigin.AirportApi),
            })
            .Add(c => c.Inizio, t0)
            .Add(c => c.Fine, t0.AddMinutes(60)));

        var barre = f.FindAll(".tl-bar").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "AZA1" }, barre);
    }

    [Fact]
    public void La_barra_sta_dentro_la_striscia_anche_in_italiano()
    {
        InItaliano(() =>
        {
            var t0 = new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
            var f = RenderComponent<SessionTimeline>(p => p
                .Add(c => c.Voli, new[] { Volo(15, 45, "AZA1") })
                .Add(c => c.Inizio, t0)
                .Add(c => c.Fine, t0.AddMinutes(60)));

            var stile = f.Find(".tl-bar").GetAttribute("style")!;
            Assert.Contains("left:25%", stile);
            Assert.Contains("width:50%", stile);
        });
    }

    [Fact]
    public void La_punta_di_traffico_si_dichiara_stimata()
    {
        var t0 = new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
        var f = RenderComponent<SessionTimeline>(p => p
            .Add(c => c.Voli, new[] { Volo(0, 30, "AZA1"), Volo(5, 20, "AZA2") })
            .Add(c => c.Inizio, t0)
            .Add(c => c.Fine, t0.AddMinutes(60)));

        Assert.Contains("Stats_PeakTraffic:2,18:05", f.Markup);
        Assert.Contains("Stats_PeakEstimated", f.Markup);
    }
}
