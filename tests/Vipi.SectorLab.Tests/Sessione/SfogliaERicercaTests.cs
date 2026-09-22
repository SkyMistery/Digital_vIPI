using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Sessione;

/// <summary>
/// L'albero da sfogliare e la ricerca per nome (carta F3 §2.2 passo 2, slice 5).
/// </summary>
public sealed class SfogliaERicercaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri(out CatalogoDeiPunti catalogo)
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        catalogo = CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"];
        return sessione;
    }

    [Fact]
    public void LAlberoHaLeCartelleDeiDatiENonQuelloCheStaSopra()
    {
        var radice = AlberoDaSfogliare.Di(Apri(out _));

        Assert.Equal("IT", radice.Nome);
        Assert.Contains(radice.Cartelle, c => c.Nome == "NAVAIDS");
        Assert.Contains(radice.Cartelle.Single(c => c.Nome == "NAVAIDS").File, f => f.Nome == "APT.fix");
        // Gli .isc, update.ini e changelog.md non sono dati: stanno fuori.
        Assert.DoesNotContain(radice.File, f => f.Nome.EndsWith(".isc", StringComparison.OrdinalIgnoreCase));
        Assert.All(Tutti(radice), f => Assert.Contains("/Include/IT/", f.Relativo, StringComparison.Ordinal));
    }

    [Fact]
    public void IFileFuoriDaiDatiSonoGliIscEIDueDellaRadice()
    {
        var fuori = AlberoDaSfogliare.FuoriDaiDati(Apri(out _)).Select(f => f.Nome).ToList();

        Assert.Contains("ITALY.isc", fuori);
        Assert.Contains("update.ini", fuori);
        Assert.Contains("changelog.md", fuori);
        Assert.DoesNotContain("APT.fix", fuori);
    }

    [Fact]
    public void OgniFileApertoStaInUNSoloPostoDellAlbero()
    {
        var sessione = Apri(out _);
        var radice = AlberoDaSfogliare.Di(sessione);

        var nellAlbero = Tutti(radice).Select(f => f.Relativo).ToList();
        var fuori = AlberoDaSfogliare.FuoriDaiDati(sessione).Select(f => f.Relativo);

        Assert.Equal(nellAlbero.Count, nellAlbero.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(sessione.File.Count, nellAlbero.Count + fuori.Count());
    }

    [Fact]
    public void IlConteggioDiUnaCartellaTieneDentroLeSueFiglie()
    {
        var radice = AlberoDaSfogliare.Di(Apri(out _));

        Assert.Equal(Tutti(radice).Count, radice.FileTotali);
        Assert.Equal(Tutti(radice).Sum(f => f.Record), radice.RecordTotali);
    }

    [Fact]
    public void UnFileCheIlMotoreNonInterpretaSiVedeComeTale()
    {
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/prova.txt", "due righe\r\ndi testo\r\n");
        var radice = AlberoDaSfogliare.Di(Apri(out _));

        var testo = Tutti(radice).Single(f => f.Nome == "prova.txt");
        Assert.False(testo.Interpretato);
        Assert.Equal(0, testo.Record);
    }

    [Fact]
    public void LaRicercaTrovaUnFixPerNome()
    {
        var strati = Strati();

        var trovati = Ricerca.Cerca(strati, "BC404");

        var primo = Assert.Single(trovati, t => t.Etichetta == "BC404");
        Assert.EndsWith("NAVAIDS/APT.fix", primo.File);
        Assert.Equal("punti", primo.Strato);
    }

    [Fact]
    public void ChiSiChiamaEsattamenteCosiVienePrima()
    {
        // 🔴 I nomi sono scelti perché l'ordine giusto NON è quello alfabetico: alfabetico darebbe AMMM, MMM, MMMX,
        // e un test coi nomi sbagliati passerebbe anche senza la regola (è successo: prova che non distingue).
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/prova.fix", """
            AMMM;N041.00.00.000;E012.00.00.000;
            MMMX;N041.10.00.000;E012.10.00.000;
            MMM;N041.20.00.000;E012.20.00.000;

            """);

        var trovati = Ricerca.Cerca(Strati(), "MMM").Select(t => t.Etichetta).ToList();

        // Uguale, poi comincia così, poi lo contiene: chi cerca MMM vuole MMM.
        Assert.Equal(["MMM", "MMMX", "AMMM"], trovati);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void UnaLetteraSolaNonECercare(string testo)
        => Assert.Empty(Ricerca.Cerca(Strati(), testo));

    [Fact]
    public void LaRicercaNonGuardaLeMaiuscoleEHaUnTetto()
    {
        var strati = Strati();

        Assert.NotEmpty(Ricerca.Cerca(strati, "bc404"));
        Assert.True(Ricerca.Cerca(strati, "0", tetto: 5).Count <= 5);
    }

    private IReadOnlyList<StratoDellaMappa> Strati()
    {
        var sessione = Apri(out var catalogo);
        return StratiDellaMappa.DiSessione(sessione, catalogo);
    }

    private static List<FileDaSfogliare> Tutti(CartellaDaSfogliare cartella)
        => [.. cartella.File, .. cartella.Cartelle.SelectMany(Tutti)];
}
