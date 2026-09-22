using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// Gli strati della mappa (carta F3, slice 4): in quale finisce un file, e che cosa esce da una sessione intera.
/// </summary>
public sealed class StratiDellaMappaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    [Theory]
    [InlineData("SectorFiles/Include/IT/GEO/itgeo.geo", "sfondo")]
    // Lo stesso lettore, due strati diversi: un disegno d'aeroporto non è la costa d'Italia.
    [InlineData("SectorFiles/Include/IT/GEO/liap.geo", "geo")]
    [InlineData("SectorFiles/Include/IT/GEO/italy.danger", "aree")]
    [InlineData("SectorFiles/Include/IT/AREAS/italy.restrict", "aree")]
    [InlineData("SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl", "settori")]
    [InlineData("SectorFiles/Include/IT/HI_AIRSPACE/lirr.hartcc", "settori")]
    [InlineData("SectorFiles/Include/IT/NAVAIDS/APT.fix", "punti")]
    [InlineData("SectorFiles/Include/IT/NAVAIDS/itvor.vor", "radioassistenze")]
    [InlineData("SectorFiles/Include/IT/lied.sid", "procedure")]
    [InlineData("SectorFiles/Include/IT/HOLDENR.hold", "attese")]
    [InlineData("SectorFiles/Include/IT/OTHER/itrw.rw", "piste")]
    public void OgniFileDisegnabileHaIlSuoStrato(string relativo, string atteso)
        => Assert.Equal(atteso, StratiDellaMappa.DiFile(relativo)?.Id);

    [Theory]
    // Frequenze, ATIS e testo non stanno su una mappa: non è un buco, non hanno coordinate.
    [InlineData("SectorFiles/Include/IT/OTHER/itfreq.frq")]
    [InlineData("SectorFiles/Include/IT/lica.atis")]
    [InlineData("SectorFiles/ITALY.isc")]
    [InlineData("changelog.md")]
    public void UnFileSenzaGeometriaNonStaInNessunoStrato(string relativo)
        => Assert.Null(StratiDellaMappa.DiFile(relativo));

    [Fact]
    public void IlSeparatoreDiWindowsNonCambiaLoStrato()
        => Assert.Equal("sfondo", StratiDellaMappa.DiFile(@"SectorFiles\Include\IT\GEO\itgeo.geo")?.Id);

    [Fact]
    public void LoSfondoELUnicoAccesoDiSuo()
    {
        Assert.Equal(["sfondo"], StratiDellaMappa.Tipi.Where(t => t.Sfondo).Select(t => t.Id));
        Assert.Equal(StratiDellaMappa.Sfondo, StratiDellaMappa.Tipi[0]);
    }

    [Fact]
    public void GliStratiDiUnaSessioneHannoTutteLeFormeDeiLoroFile()
    {
        var sessione = Apri(out var catalogo);
        var strati = StratiDellaMappa.DiSessione(sessione, catalogo);

        // Gli stessi tipi, sempre: una casella che non porta niente resta, e dice zero.
        Assert.Equal(StratiDellaMappa.Tipi.Select(t => t.Id), strati.Select(s => s.Id));

        var punti = strati.Single(s => s.Id == "punti");
        var dalFile = Geometria.DelFile(sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"], catalogo);
        Assert.All(dalFile, f => Assert.Contains(punti.Forme, g => g.File == f.File && g.Record == f.Record));
        Assert.Equal(punti.Forme.Sum(f => f.Punti), punti.Punti);
    }

    [Fact]
    public void NessunaFormaSiPerdePerStrada()
    {
        var sessione = Apri(out var catalogo);
        int daiFile = sessione.File.Values
            .Where(f => StratiDellaMappa.DiFile(f.Relativo) is not null)
            .Sum(f => Geometria.DelFile(f, catalogo).Count);

        Assert.Equal(daiFile, StratiDellaMappa.DiSessione(sessione, catalogo).Sum(s => s.Forme.Count));
        Assert.True(daiFile > 0);
    }

    [Fact]
    public void LOrdineDelleFormeNonDipendeDallOrdineDeiFileInMemoria()
    {
        var sessione = Apri(out var catalogo);

        var prima = StratiDellaMappa.DiSessione(sessione, catalogo);
        var seconda = StratiDellaMappa.DiSessione(sessione, catalogo);

        Assert.Equal(
            prima.SelectMany(s => s.Forme).Select(f => $"{f.File}#{f.Record}"),
            seconda.SelectMany(s => s.Forme).Select(f => $"{f.File}#{f.Record}"));
    }

    [Fact]
    public void SenzaCatalogoIPuntiPerNomeRestanoIrrisolti()
    {
        var sessione = Apri(out var catalogo);

        int conCatalogo = StratiDellaMappa.DiSessione(sessione, catalogo).Sum(s => s.Forme.Sum(f => f.Punti));
        int senza = StratiDellaMappa.DiSessione(sessione, catalogo: null).Sum(s => s.Forme.Sum(f => f.Punti));

        Assert.True(senza < conCatalogo, $"senza catalogo {senza}, col catalogo {conCatalogo}");
    }

    private SessioneAperta Apri(out CatalogoDeiPunti catalogo)
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        catalogo = CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"];
        return sessione;
    }
}
