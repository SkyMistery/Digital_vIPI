using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// La scheda delle procedure (lotto «Subito», slice 9b; «file per file» P1, Q1): l'RNAV delle SID, la transizione e
/// l'RNAV delle voci degli <c>.str</c> si scrivono, e nel <c>MAPS</c> il tipo è il tasto che accende la mappa.
/// </summary>
public sealed class SchedaDelleProcedureTests : IDisposable
{
    private const string Sid = "SectorFiles/Include/IT/lirf.sid";
    private const string Str = "SectorFiles/Include/IT/lirf.str";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static int Indice(FileAperto file, string nome)
        => ((IFileConRecord)file).RecordDelModello.Select((r, i) => (r, i))
               .First(v => v.r is SidProcedure { Name: var s } && s == nome || v.r is StrRecord { ProcedureId: var p } && p == nome).i;

    private List<string> Dopo(FileAperto file) => [.. ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo))];

    [Theory]
    [InlineData("OST1E", "", "LIRF;07;OST1E;;;")]
    [InlineData("OST1D", "sì", "LIRF;07;OST1D; ; ;;;1;")]
    [InlineData("EKLO8R", "no", "LIRF;25;EKLO8R;;;;;0;")]
    public void LRnavDiUnaSidSiScriveNellOttavoCampo(string sid, string scritto, string atteso)
    {
        var file = Apri().File[Sid];
        int prima = ((IFileConRecord)file).RigheDelFile([]).Count;

        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, Indice(file, sid), "IsRnav", scritto));

        var dopo = Dopo(file);
        Assert.Contains(atteso, dopo);
        Assert.Equal(prima, dopo.Count);
    }

    [Fact]
    public void TransizioneERnavDiUnaVoceStrSiScrivonoETengonoIlTipoVuoto()
    {
        // `LIRF;16L:16R;ELKA3A;;;;;1;`: il 6° campo (tipo) è vuoto, come in 519 voci RNAV del fork.
        var file = Apri().File[Str];
        int elka = Indice(file, "ELKA3A");
        int prima = ((IFileConRecord)file).RigheDelFile([]).Count;

        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, elka, "Transition", "ELKAP"));
        var dopo = Dopo(file);
        Assert.Contains("LIRF;16L:16R;ELKA3A;;;;ELKAP;1;", dopo);
        Assert.Equal(prima, dopo.Count);
        Assert.Single(dopo, r => r.StartsWith("LIRF;16L:16R;ELKA3A;", StringComparison.Ordinal));

        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, elka, "IsRnav", ""));
        Assert.Contains("LIRF;16L:16R;ELKA3A;;;;ELKAP;", Dopo(file));
    }

    [Fact]
    public void NelMapsIlTipoEIlTastoCheAccendeLaMappa()
    {
        var file = Apri().File[Str];
        var record = ((IFileConRecord)file).RecordDelModello;

        var ctr = DescrizioniDeiCampi.Di(record[Indice(file, "LIRF CTR")], Str)!;
        var tipo = ctr.Campi.Single(c => c.Proprieta == "RecordType");
        Assert.Equal("Si accende col tasto", tipo.Nome);
        Assert.Equal(["0 · STAR", "1 · TRANS", "2 · HOLD", "3 · IAP", "4 · FAP", "5 · GA"], tipo.Valori.Select(v => v.Voce));

        var elka = DescrizioniDeiCampi.Di(record[Indice(file, "ELKA3A")], Str)!;
        Assert.Equal("Tipo", elka.Campi.Single(c => c.Proprieta == "RecordType").Nome);
        Assert.Equal(Editor.Navaid, elka.Campi.Single(c => c.Proprieta == "Transition").Editor);
        Assert.Equal(Editor.SiNo, elka.Campi.Single(c => c.Proprieta == "IsRnav").Editor);
    }

    [Fact]
    public void UnaStarConPuntiECoordinateSiPresentaComeStarEMostraIPunti()
    {
        // DELI1H di LICA: punti per nome e coordinate nello stesso corpo, quindi una HoldingStrRecord per il parser.
        // La scheda diceva «Attesa di scalo» e mostrava Vipi.Sectorfile.Models.HoldingFixPoint al posto dei punti.
        const string lica = "SectorFiles/Include/IT/lica.str";
        _albero.Scrivi(lica, string.Join("\r\n",
            "LICA;10;DELI1H; ; ; ;", "DELIK;DELIK;1H", "LMT;LMT;", "N038.54.16.000;E016.15.56.000;",
            "LICA;28;HLD-ORTEV; ; ;2;", "ORTEV;ORTEV;", "N038.52.23.494;E016.30.43.501;") + "\r\n");
        var file = Apri().File[lica];

        var star = Ispettore.Scheda(file, Indice(file, "DELI1H"), null)!;
        Assert.IsType<HoldingStrRecord>(((IFileConRecord)file).RecordDelModello[Indice(file, "DELI1H")]);
        Assert.Equal("STAR (punti e coordinate)", star.NomeDelTipo);
        Assert.Equal("DELIK, LMT, N038.54.16.000 E016.15.56.000", star.Campi.Single(c => c.Nome == "Points").Valore);

        var attesa = Ispettore.Scheda(file, Indice(file, "HLD-ORTEV"), null)!;
        Assert.Equal("Attesa di scalo", attesa.NomeDelTipo);
        Assert.Equal("ORTEV, N038.52.23.494 E016.30.43.501", attesa.Campi.Single(c => c.Nome == "Points").Valore);
    }
}
