using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le targhette di un volo, rese davvero.
///
/// <para>La regola è già provata su <c>TrafficStory</c>; qui si presidia il pezzo che quei test non vedono:
/// che il componente <b>chieda</b> a quella regola invece di rifarsela nel markup, che la consegna porti il
/// callsign di chi ha preso il volo, e che il colore segua il significato — verde quando si è concluso da
/// noi, non a caso.</para>
/// </summary>
public class TrafficTagsTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        // Gli argomenti si accodano: così un test può vedere che il callsign è arrivato alla chiave.
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, arguments.Length == 0 ? name : name + ":" + string.Join(",", arguments), false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    public TrafficTagsTests() => Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private static StatsTrafficRow Volo(
        string? dep = "LIRF", string? arr = "LIMC", bool mosso = true, bool inVolo = true,
        FlightPhase? prima = FlightPhase.Parked, FlightPhase? ultima = FlightPhase.Airborne,
        string? consegnatoA = null, bool buco = false, TrafficOrigin origine = TrafficOrigin.Aor) =>
        new("AZA123", 1, dep, arr, "A320",
            DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow, 20,
            mosso, buco, origine, prima, ultima, inVolo, 0, 24000, 24000, consegnatoA, null);

    private IRenderedFragment Rendi(StatsTrafficRow riga, string? icao = "LIRF") =>
        RenderComponent<TrafficTags>(p => p
            .Add(c => c.Riga, riga)
            .Add(c => c.StationIcao, icao)
            .Add(c => c.Prefissi, new[] { "LI" }));

    private static IEnumerable<string> Targhette(IRenderedFragment f) =>
        f.FindAll(".pill").Select(e => e.TextContent.Trim());

    [Fact]
    public void Una_partenza_vista_staccare_dice_partenza_e_decollato()
    {
        var t = Targhette(Rendi(Volo())).ToList();

        Assert.Contains("Stats_Tag_Departure", t);
        Assert.Contains("Stats_Tag_TookOff", t);
    }

    [Fact]
    public void La_consegna_porta_il_callsign_di_chi_lo_ha_preso()
    {
        var f = Rendi(Volo(consegnatoA: "LIRR_NE1_CTR"));

        Assert.Contains("Stats_Tag_HandedOffTo:LIRR_NE1_CTR", Targhette(f));
        Assert.DoesNotContain("Stats_Tag_LeftAirborne", Targhette(f));
    }

    [Fact]
    public void Un_arrivo_visto_toccare_terra_e_verde()
    {
        var f = Rendi(Volo(dep: "LIMC", arr: "LIRF", prima: FlightPhase.Airborne, ultima: FlightPhase.Ground));

        var atterrato = f.FindAll(".pill").Single(e => e.TextContent.Trim() == "Stats_Tag_Landed");
        Assert.Contains("landed", atterrato.ClassList);
    }

    [Fact]
    public void Chi_esce_ancora_in_volo_non_dice_atterrato()
    {
        var t = Targhette(Rendi(Volo(dep: "LIMC", arr: "LIRF",
            prima: FlightPhase.Airborne, ultima: FlightPhase.Airborne))).ToList();

        Assert.Contains("Stats_Tag_Arrival", t);
        Assert.DoesNotContain("Stats_Tag_Landed", t);
        Assert.Contains("Stats_Tag_LeftAirborne", t);
    }

    [Fact]
    public void Una_riga_ricostruita_non_racconta_fasi()
    {
        var t = Targhette(Rendi(Volo(prima: null, ultima: null, inVolo: false, mosso: false,
            origine: TrafficOrigin.AirportApi))).ToList();

        Assert.Contains("Stats_Tag_Rebuilt", t);
        Assert.DoesNotContain("Stats_Tag_Parked", t);
    }

    [Fact]
    public void Ogni_targhetta_porta_la_sua_spiegazione()
    {
        // Il «?» in questo prodotto si apre a clic; qui la spiegazione è il titolo nativo, e deve esserci
        // su tutte: una targhetta senza spiegazione è gergo.
        var f = Rendi(Volo(consegnatoA: "LIRR_NE1_CTR", buco: true));

        Assert.All(f.FindAll(".pill"), e =>
            Assert.StartsWith("Stats_TagHint_", e.GetAttribute("title") ?? ""));
    }
}
