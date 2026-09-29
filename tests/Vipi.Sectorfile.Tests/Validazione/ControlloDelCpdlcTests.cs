using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 11c (carta «file per file» M5): il lettore dei <c>.cpdlc</c> e dei <c>.cpdlcnames</c> (righe vere
/// di <c>ita.cpdlc</c>) e i controlli: un gruppo con un nome e senza messaggi, un messaggio senza risposta (non nel gruppo
/// 20 del DCL, dove tre pezzi non l'hanno sul fork), i valori dichiarati diversi da quelli del testo, i valori fuori elenco.
/// </summary>
public sealed class ControlloDelCpdlcTests : IDisposable
{
    private readonly CollectingWarnings _warnings = new();
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "cpdlc-" + Guid.NewGuid().ToString("N"));

    private static readonly string[] Messaggi =
    [
        "//REVISED PHRASEOLOGY",
        "CLIMB TO [0];WU;0;[0];;;;1;0;0;0;1;0;0;0;1;",
        "CLIMB TO REACH [0] BY [1];WU;0;[0];[1];;;1;1;0;0;1;3;0;0;2;",
        "SQK [0];;20;[0];;;;0;0;0;0;4;0;0;0;1;",
        "REPORT PASSING [0];;9;[0];;;;0;0;0;0;3;0;0;0;3;",
        "CONTACT [0];XX;19;[0];;;;0;0;0;0;6;0;0;0;1;",
    ];

    public ControlloDelCpdlcTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "OTHER"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[CPDLC]", @"F;OTHER\ita.cpdlc", "[CPDLCNAMES]", @"F;OTHER\ita.cpdlcnames"));
        Scrivi(@"Include\IT\OTHER\ita.cpdlc", Righe(Messaggi));
        Scrivi(@"Include\IT\OTHER\ita.cpdlcnames", Righe("//TEST", "GROUP.0;SALITA;", "GROUP.15;TWR;"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    [Fact]
    public void IMessaggiSiLeggonoESiRiscrivonoUguali()
    {
        var letto = new CpdlcParser(_warnings).Parse(ParserTestHelpers.Read(Righe(Messaggi)), "ita.cpdlc", new ColorPalette());

        Assert.Equal(5, letto.Records.Count);
        var reach = letto.Records[1];
        Assert.Equal(("CLIMB TO REACH [0] BY [1]", "WU", "0", 2), (reach.Comando, reach.Risposta, reach.Gruppo, reach.TotaleDeiValori));
        Assert.All(letto.Records.Zip(Messaggi.Skip(1)), c => Assert.Equal(c.Second, new CpdlcSaver().Serialize(c.First)[0]));

        var nomi = new CpdlcNamesParser(_warnings).Parse(ParserTestHelpers.Read("//TEST\r\nGROUP.15;TWR;"), "ita.cpdlcnames", new ColorPalette());
        var twr = Assert.Single(nomi.Records);
        Assert.Equal(("15", "TWR"), (twr.Gruppo, twr.Nome));
        Assert.Equal("GROUP.15;TWR;", new CpdlcNamesSaver().Serialize(twr)[0]);
        Assert.Empty(_warnings.Snapshot());
    }

    [Fact]
    public void UnGruppoColNomeESenzaMessaggiEUnAvviso()
    {
        var p = Assert.Single(Di(Regola.GruppoSenzaMessaggi));
        Assert.Equal(3, p.Riga);
        Assert.Contains("15", p.Dettaglio);
    }

    [Fact]
    public void UnMessaggioSenzaRispostaEUnAvviso_NelDclNo()
    {
        var p = Assert.Single(Di(Regola.MessaggioSenzaRisposta));
        Assert.Equal(5, p.Riga);
    }

    [Fact]
    public void IValoriDichiaratiDiversiDaQuelliDelTestoSonoUnAvviso()
    {
        var p = Assert.Single(Di(Regola.ValoriDelMessaggio));
        Assert.Equal(5, p.Riga);
        Assert.Contains("3", p.Dettaglio);
    }

    [Fact]
    public void UnaRispostaOUnGruppoFuoriElencoSonoUnAvviso()
    {
        var fuori = Di(Regola.ValoreFuoriElenco);
        Assert.Equal([6, 6], fuori.Select(p => p.Riga));
        Assert.Contains(fuori, p => p.Dettaglio.Contains("«XX»", StringComparison.Ordinal));
        Assert.Contains(fuori, p => p.Dettaglio.Contains("«19»", StringComparison.Ordinal));
    }
}
