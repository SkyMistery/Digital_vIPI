using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 11d (carta «file per file» N1): il lettore dei profili <c>.cpr</c>, un INI come i profili di
/// Aurora. Un record per impostazione, con la sua sezione; le intestazioni, i commenti e le righe prima della prima
/// sezione (<c>PAR_VERTICAL_SCAN=30</c> dei profili PAR) restano come sono. Righe vere di <c>LIPI.cpr</c> e <c>TWR.cpr</c>.
/// </summary>
public sealed class ProfiloParserTests
{
    private readonly CollectingWarnings _warnings = new();

    private static readonly string[] Lipi =
    [
        "PAR_VERTICAL_SCAN=30",
        "PAR_HORIZONTAL_SCAN=25",
        "[INSET1]",
        "INS1PAR_CAPTION=LIPI RWY06/2.6°",
        "INS1PAR_Radial=55",
        "INS1Par_Elevation=162",
        "INS1Par_Lat=46.0052477614404",
    ];

    private ParseResult<ImpostazioneDelProfilo> Leggi(params string[] righe)
        => new ProfiloParser(_warnings).Parse(ParserTestHelpers.Read(string.Join("\r\n", righe) + "\r\n"), "LIPI.cpr");

    [Fact]
    public void UnaImpostazionePerRigaConLaSuaSezione()
    {
        var letto = Leggi(Lipi);

        Assert.Equal(6, letto.Records.Count);
        Assert.Equal(("", "PAR_VERTICAL_SCAN", "30"), (letto.Records[0].Sezione, letto.Records[0].Chiave, letto.Records[0].Valore));
        Assert.Equal(("INSET1", "INS1PAR_CAPTION", "LIPI RWY06/2.6°"), (letto.Records[2].Sezione, letto.Records[2].Chiave, letto.Records[2].Valore));
        Assert.Equal(4, letto.Records[2].Source.LineNumber);
        Assert.Empty(_warnings.Snapshot());
    }

    [Fact]
    public void IlFileSiRiscriveUgualeEUnValoreCambiatoTocaSoloLaSuaRiga()
    {
        var letto = Leggi(Lipi);
        var scrittore = new ProfiloSaver();
        Assert.All(letto.Records, r => Assert.Contains(scrittore.Serialize(r)[0], Lipi));

        letto.Records[3].Valore = "56";
        Assert.Equal("INS1PAR_Radial=56", scrittore.Serialize(letto.Records[3])[0]);
    }

    [Fact]
    public void UnProfiloGenerico()
    {
        var letto = Leggi("[PREFS]", "AircraftHorizontal=30", "AircraftHorizontalPR=30");
        Assert.Equal(["PREFS", "PREFS"], letto.Records.Select(r => r.Sezione));
        Assert.Equal("AircraftHorizontal", letto.Records[0].Chiave);
    }
}
