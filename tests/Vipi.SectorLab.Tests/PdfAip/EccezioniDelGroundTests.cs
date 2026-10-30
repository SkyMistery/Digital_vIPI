using System.Globalization;
using Aip;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», S5: le eccezioni dello scalo (controllo 4) in <c>eccezioni\&lt;ICAO&gt;.json</c>, le carte di
/// AD 2.24 che disegnano il ground, e lo scarto della posa in metri. I titoli delle carte sono quelli del 2611.
/// </summary>
public sealed class EccezioniDelGroundTests : IDisposable
{
    private readonly string _espanso = Path.Combine(Path.GetTempPath(), "eccezioni-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_espanso))
            Directory.Delete(_espanso, recursive: true);
    }

    [Fact]
    public void Un_eccezione_si_scrive_si_sostituisce_e_si_toglie()
    {
        Assert.Empty(EccezioniDelGround.Leggi(_espanso, "lixx"));

        EccezioniDelGround.Scrivi(_espanso, "lixx", new EccezioneDelGround(ControlloDelGround.Taxiway, " cm ", " Chiusa da anni, il sector non la disegna. ", new DateOnly(2026, 10, 30)));
        EccezioniDelGround.Scrivi(_espanso, "LIXX", new EccezioneDelGround(ControlloDelGround.Soglia, "2", "Soglia spostata per i lavori: il sector segue il NOTAM."));
        EccezioniDelGround.Scrivi(_espanso, "LIXX", new EccezioneDelGround(ControlloDelGround.Stand, "07", "Lo stand 7 del sector è al posto vecchio."));

        // Una per voce: «02» è la stessa testata di «2», e il perché nuovo sostituisce il vecchio.
        EccezioniDelGround.Scrivi(_espanso, "LIXX", new EccezioneDelGround(ControlloDelGround.Soglia, "02", "Spostata col NOTAM."));
        var file = EccezioniDelGround.File(_espanso, "lixx");
        Assert.EndsWith(Path.Combine("eccezioni", "LIXX.json"), file, StringComparison.Ordinal);
        var lette = EccezioniDelGround.Leggi(_espanso, "LIXX");
        Assert.Equal([ControlloDelGround.Soglia, ControlloDelGround.Taxiway, ControlloDelGround.Stand], lette.Select(e => e.Controllo));
        Assert.Equal(("02", "Spostata col NOTAM."), (lette[0].Voce, lette[0].Perche));
        Assert.Equal(("cm", "Chiusa da anni, il sector non la disegna.", new DateOnly(2026, 10, 30)), (lette[1].Voce, lette[1].Perche, lette[1].Data));
        // Il file si legge: i nomi dei controlli in parole, le lettere accentate come sono.
        var testo = File.ReadAllText(file);
        Assert.Contains("\"controllo\": \"Taxiway\"", testo, StringComparison.Ordinal);
        Assert.Contains("è al posto vecchio", testo, StringComparison.Ordinal);

        // Si trova come la scrive il controllo: «CM», «2», «7».
        Assert.Same(lette[1], EccezioniDelGround.Di(lette, ControlloDelGround.Taxiway, "CM"));
        Assert.Same(lette[0], EccezioniDelGround.Di(lette, ControlloDelGround.Soglia, "2"));
        Assert.Same(lette[2], EccezioniDelGround.Di(lette, ControlloDelGround.Stand, "7"));
        Assert.Null(EccezioniDelGround.Di(lette, ControlloDelGround.Taxiway, "7"));

        Assert.True(EccezioniDelGround.Togli(_espanso, "LIXX", ControlloDelGround.Stand, "7"));
        Assert.False(EccezioniDelGround.Togli(_espanso, "LIXX", ControlloDelGround.Stand, "7"));
        Assert.True(EccezioniDelGround.Togli(_espanso, "LIXX", ControlloDelGround.Soglia, "02"));
        Assert.True(EccezioniDelGround.Togli(_espanso, "LIXX", ControlloDelGround.Taxiway, "CM"));
        // Senza eccezioni il file se ne va.
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Senza_il_perche_non_si_scrive_e_un_file_rotto_lo_dice()
    {
        Assert.Throws<ArgumentException>(() => EccezioniDelGround.Scrivi(_espanso, "LIXX", new EccezioneDelGround(ControlloDelGround.Taxiway, "CM", " ")));
        Assert.Throws<ArgumentException>(() => EccezioniDelGround.Scrivi(_espanso, "LIXX", new EccezioneDelGround(ControlloDelGround.Taxiway, "", "perché")));
        Directory.CreateDirectory(Path.Combine(_espanso, EccezioniDelGround.Cartella));
        File.WriteAllText(EccezioniDelGround.File(_espanso, "LIXX"), "{ non è json");
        Assert.Throws<InvalidDataException>(() => EccezioniDelGround.Leggi(_espanso, "LIXX"));
    }

    [Theory]
    [InlineData("AERODROME CHART - ICAO", true)]
    [InlineData("AERODROME CHART ICAO – HELICOPTER LANDING AREA", true)]
    [InlineData("AIRCRAFT PARKING DOCKING CHART – ICAO", true)]
    [InlineData("AERODROME PARKING DOCKING CHART ICAO", true)]
    [InlineData("AERODROME GROUND MOVEMENT CHART: ACFT CODE F – ICAO", true)]
    [InlineData("HOTSPOT MAP (NOT FOR NAVIGATION)", true)]
    [InlineData("HOT SPOT MAP (NOT FOR NAVIGATION)", true)]
    [InlineData("LOW VISIBILITY PROCEDURES CHART", true)]
    [InlineData("AIRCRAFT PARKING DOCKING CHART – ICAO DESCRIPTION TABLES", false)]
    [InlineData("AERODROME OBSTACLE CHART – TYPE B ICAO", false)]
    [InlineData("AERODROME LANDING CHART", false)]
    [InlineData("ATC SURVEILLANCE MINIMUM ALTITUDE CHART ICAO", false)]
    [InlineData("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 16L", false)]
    public void Le_carte_del_ground(string titolo, bool delGround) =>
        Assert.Equal(delGround, GroundDelloScalo.EDelGround(new AerodromeChart(titolo, "AD 2 LIXX 2-1", null)));

    [Theory]
    [InlineData(0.02, "37 m")]
    [InlineData(0.4999, "926 m")]
    [InlineData(0.5, "0,5 NM")]
    [InlineData(2.734, "2,73 NM")]
    [InlineData(12.4, "12 NM")]
    public void Lo_scarto_sotto_il_mezzo_miglio_in_metri(double miglia, string atteso) =>
        Assert.Equal(atteso, PosaSullaMappa.InParole(miglia, CultureInfo.GetCultureInfo("it-IT")));
}
