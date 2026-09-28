using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// <see cref="RecordNuovo.Aggiungi{T}"/> (Sector Lab, lotto «Subito» slice 9b, trovato a schermo): il lettore degli
/// <c>.str</c> lascia nelle righe di una voce la riga vuota e il commento della voce DOPO (<c>//LIRF RNP RWY07</c>). Il
/// nuovo, messo sotto quelle righe, si prendeva il commento dell'altra: ora la coda passa sotto il nuovo.
/// </summary>
public sealed class AggiungiSottoIlVicinoTests
{
    private readonly CollectingWarnings _warnings = new();

    private const string Lirf =
        "//LIRF RNP RWY07\r\nLIRF;07;RNP07(TAQ);;;3;;1;\r\nTAQ;TAQ;\r\nRF782;RF782;\r\n\r\n" +
        "//LIRF RNP RWY07\r\nLIRF;07:MAPS;RNP07(CMP);;;3;;1;\r\nCMP;CMP;\r\n";

    private ParseResult<StrRecord> Leggi(string testo)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(testo), "lirf.str").FissaLeBasi(new StrSaver());

    private static IReadOnlyList<string> Righe(ParseResult<StrRecord> letto)
        => new FileSaverOrchestrator().Righe(letto, new HashSet<StrRecord>(), new StrSaver());

    private static ProcedureStrRecord Nuovo() => new()
    {
        IcaoCode = "LIRF", RunwaySpec = "07", ProcedureId = "NUOVA", RecordType = StrRecordType.Iap, IsRnav = true,
        Waypoints = { new ProcedureWaypoint { FixName = "TAQ", DisplayLabel = "TAQ" } },
    };

    [Fact]
    public void LaCodaDelVicinoPassaSottoIlNuovo()
    {
        var letto = Leggi(Lirf);

        var dopo = RecordNuovo.Aggiungi(letto, new StrSaver(), Nuovo(), 0);

        Assert.Equal(
            [
                "//LIRF RNP RWY07", "LIRF;07;RNP07(TAQ);;;3;;1;", "TAQ;TAQ;", "RF782;RF782;", "",
                "LIRF;07;NUOVA;;;3;;1;", "TAQ;TAQ;", "",
                "//LIRF RNP RWY07", "LIRF;07:MAPS;RNP07(CMP);;;3;;1;", "CMP;CMP;",
            ], Righe(dopo));
        // Riletto, ognuno ha le sue righe: la voce dopo ha ancora il suo commento sopra.
        Assert.Equal(["RNP07(TAQ)", "NUOVA", "RNP07(CMP)"], Leggi(string.Join("\r\n", Righe(dopo)) + "\r\n").Records.Select(r => r.ProcedureId));
    }

    [Fact]
    public void LaStrutturaDiPrimaNonCambia()
    {
        var letto = Leggi(Lirf);
        var prima = Righe(letto);

        RecordNuovo.Aggiungi(letto, new StrSaver(), Nuovo(), 0);

        Assert.Equal(prima, Righe(letto));
    }

    [Fact]
    public void UnPuntoCommentatoAttaccatoAlVicinoRestaSuo()
    {
        // Senza riga vuota prima, un punto commentato in fondo è del vicino (un punto nascosto): non si sposta.
        var letto = Leggi("LIRF;07;RNP07(TAQ);;;3;;1;\r\nTAQ;TAQ;\r\n//RF782;RF782;\r\n");

        var dopo = RecordNuovo.Aggiungi(letto, new StrSaver(), Nuovo(), 0);

        Assert.Equal(["LIRF;07;RNP07(TAQ);;;3;;1;", "TAQ;TAQ;", "//RF782;RF782;", "LIRF;07;NUOVA;;;3;;1;", "TAQ;TAQ;"], Righe(dopo));
    }
}
