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

    /// <summary>
    /// ⚠️ Il numero nel buco della ciambella deve STARE nel buco. Il buco è largo 69 unità del viewBox
    /// (r 42 meno mezza traccia da 15 per parte): le ore di una persona ci stanno sempre, quelle della
    /// DIVISIONE no — «12345,6» a corpo fisso 19 misura ~80 unità e finiva sopra l'anello.
    /// </summary>
    [Theory]
    [InlineData("123,4", 19)]        // le ore di una persona: corpo pieno
    [InlineData("1234,5", 19)]       // cinque cifre: l'ultimo caso che ci sta com'è
    [InlineData("12345,6", 18)]      // le ore di una divisione: si stringe
    [InlineData("123456,7", 15)]     // e si stringe ancora
    public void Il_numero_nel_buco_non_esce_dal_buco(string centro, int corpoMassimo)
    {
        var f = RenderComponent<StatsDonut>(p => p
            .Add(c => c.Fette, new[] { new StatsSlice("CTR", 10, "p-ctr", "ctr") })
            .Add(c => c.Centro, centro));

        var corpo = double.Parse(
            f.Find("text.donut-hole").GetAttribute("font-size")!, CultureInfo.InvariantCulture);

        Assert.True(corpo <= corpoMassimo, $"«{centro}» reso a corpo {corpo}");
        // Larghezza stimata come nel componente: una cifra Poppins occupa ~0,6 em.
        Assert.True(centro.Length * 0.6 * corpo <= 69, $"«{centro}» misura più del buco");
    }

    /// <summary>Il corpo è un numero SVG: a cultura italiana una virgola qui spegne il testo.</summary>
    [Fact]
    public void Il_corpo_del_numero_nel_buco_si_scrive_col_punto_decimale()
    {
        InItaliano(() =>
        {
            var f = RenderComponent<StatsDonut>(p => p
                .Add(c => c.Fette, new[] { new StatsSlice("CTR", 10, "p-ctr", "ctr") })
                .Add(c => c.Centro, "1234567,8"));

            Assert.DoesNotContain(",", f.Find("text.donut-hole").GetAttribute("font-size")!);
        });
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
