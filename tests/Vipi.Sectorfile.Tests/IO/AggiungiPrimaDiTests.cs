using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// <see cref="RecordNuovo.AggiungiPrimaDi{T}"/> (Sector Lab, prova 6 del committente): un fix nuovo va al suo posto in
/// ordine alfabetico anche PRIMO della sua sezione, cioè sotto l'intestazione <c>//LIBD</c>, non sopra.
/// </summary>
public sealed class AggiungiPrimaDiTests
{
    private readonly CollectingWarnings _warnings = new();

    private ParseResult<Fix> Leggi(string testo)
        => new FixParser(_warnings).Parse(ParserTestHelpers.Read(testo), "APT.fix", new ColorPalette()).FissaLeBasi(new FixSaver());

    private static IReadOnlyList<string> Righe(ParseResult<Fix> letto)
        => new FileSaverOrchestrator().Righe(letto, new HashSet<Fix>(), new FixSaver());

    private const string Due = "//LIBC\r\nBC404;N039.05.11.290;E017.03.27.750;3;\r\n//LIBD\r\nBD423;N040.59.45.270;E016.15.41.860;3;\r\nBD425;N041.16.42.460;E017.17.55.910;3;";

    [Fact]
    public void IlNuovoPrimoDellaSezioneStaSottoLIntestazione()
    {
        var letto = Leggi(Due);
        int bd423 = letto.Records.ToList().FindIndex(f => f.Name == "BD423");
        var nuovo = new Fix { Name = "BD100", Position = new Coordinate(40.9, 16.2), DisplayType = 3 };

        var dopo = RecordNuovo.AggiungiPrimaDi(letto, new FixSaver(), nuovo, bd423);

        var righe = Righe(dopo);
        int intestazione = righe.ToList().IndexOf("//LIBD");
        Assert.StartsWith("BD100;", righe[intestazione + 1], StringComparison.Ordinal);
        Assert.StartsWith("BD423;", righe[intestazione + 2], StringComparison.Ordinal);
        Assert.Equal("BD100", dopo.Records[bd423].Name);
        Assert.Equal(letto.Records.Count + 1, dopo.Records.Count);
    }

    [Fact]
    public void LaStrutturaDiPrimaNonCambia()
    {
        // Annullare rimette la struttura di prima: i suoi chunk non devono essere stati toccati.
        var letto = Leggi(Due);
        var prima = Righe(letto);
        int bd423 = letto.Records.ToList().FindIndex(f => f.Name == "BD423");

        RecordNuovo.AggiungiPrimaDi(letto, new FixSaver(), new Fix { Name = "BD100", Position = new Coordinate(40.9, 16.2), DisplayType = 3 }, bd423);

        Assert.Equal(prima, Righe(letto));
    }

    [Fact]
    public void UnTagDelVicinoRestaAlVicino()
    {
        var letto = Leggi("//LIBD\r\n//@BD423 nota=si\r\nBD423;N040.59.45.270;E016.15.41.860;3;");

        var dopo = RecordNuovo.AggiungiPrimaDi(letto, new FixSaver(), new Fix { Name = "BD100", Position = new Coordinate(40.9, 16.2), DisplayType = 3 }, 0);

        var righe = Righe(dopo).ToList();
        Assert.Equal(["//LIBD", "BD100;N040.54.00.000;E016.12.00.000;3;", "//@BD423 nota=si", "BD423;N040.59.45.270;E016.15.41.860;3;"],
            righe.Select(r => r.StartsWith("BD100;", StringComparison.Ordinal) ? "BD100;N040.54.00.000;E016.12.00.000;3;" : r));
    }
}
