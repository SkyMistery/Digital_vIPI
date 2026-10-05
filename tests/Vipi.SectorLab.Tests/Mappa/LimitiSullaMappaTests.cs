using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// Limiti verticali e classe sulla mappa (lotto «Subito» slice 13g; D9, J7, Q8): al passaggio del mouse, come i vincoli
/// dei punti (committente, 5 ottobre). Viaggiano con la forma, nella stessa riga di testo.
/// </summary>
public sealed class LimitiSullaMappaTests : IDisposable
{
    private const string Settori = "SectorFiles/Include/IT/DYNAMIC_SEC/prova13.tfl";
    private const string Confini = "SectorFiles/Include/IT/HI_AIRSPACE/prova13.hartcc";
    private const string Mappe = "SectorFiles/Include/IT/lzzz.str";

    private const string Quadrato = "N041.00.00.000;E012.00.00.000;\r\nN041.10.00.000;E012.00.00.000;\r\nN041.10.00.000;E012.10.00.000;\r\nN041.00.00.000;E012.10.00.000;\r\n";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private (SessioneAperta Sessione, CatalogoDeiPunti Catalogo) Apri()
    {
        _albero.Scrivi(Settori, "LZZZ_NE_CTR;CTR;1;CTR;1;\r\n" + Quadrato + "\r\nLZZZ_APP;APP;1;APP;1;\r\n" + Quadrato);
        _albero.Scrivi(Confini, string.Join("\r\n", Quadrato.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(v => "T;ZZ NE;" + v)) + "\r\n");
        _albero.Scrivi(Mappe, "LZZZ;MAPS;LZZZ ATZ;;;5;\r\n" + Quadrato);
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]);
    }

    private string? Suggerimento(SessioneAperta sessione, CatalogoDeiPunti catalogo, string file, int record)
        => Geometria.DelFile(sessione.File[file], catalogo).Single(f => f.Record == record).Vincoli;

    [Fact]
    public void LimitiEClasseViaggianoConLaFormaDelSettoreDelConfineEDellAtz()
    {
        var (sessione, catalogo) = Apri();
        _modifiche.CambiaIlMetadato(sessione.File[Settori], 0, "lower", "FL195");
        _modifiche.CambiaIlMetadato(sessione.File[Settori], 0, "upper", "UNL");
        _modifiche.CambiaIlMetadato(sessione.File[Settori], 0, "class", "C");
        _modifiche.CambiaIlMetadato(sessione.File[Confini], 0, "upper", "FL195");
        _modifiche.CambiaIlMetadato(sessione.File[Mappe], 0, "lower", "SFC");
        _modifiche.CambiaIlMetadato(sessione.File[Mappe], 0, "upper", "2000ft");
        _modifiche.CambiaIlMetadato(sessione.File[Mappe], 0, "class", "D");

        Assert.Equal("FL195 – UNL · classe C", Suggerimento(sessione, catalogo, Settori, 0));
        Assert.Null(Suggerimento(sessione, catalogo, Settori, 1));
        // Un limite solo: l'altro si vede che manca.
        Assert.Equal("? – FL195", Suggerimento(sessione, catalogo, Confini, 0));
        Assert.Equal("SFC – 2000ft · classe D", Suggerimento(sessione, catalogo, Mappe, 0));
    }

    [Fact]
    public void LaSolaClasseSiVedeDaSola()
    {
        var (sessione, catalogo) = Apri();
        _modifiche.CambiaIlMetadato(sessione.File[Settori], 1, "class", "D");

        Assert.Equal("classe D", Suggerimento(sessione, catalogo, Settori, 1));
    }
}
