using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Carta F3-bis, slice 1: l'indice dei gemelli sui file aperti. I campioni hanno <c>itap</c>/<c>lirr</c> per gli
/// <c>.ap</c>, i <c>.rw</c> e i <c>.frq</c>, con le loro copie vere (55 scali, 63 piste: le 43 righe <c>MAPS</c> della slice 0 stanno sotto <c>//MENU MAPPE</c>,
/// e il motore le tiene come righe grezze, non come record).
/// </summary>
public sealed class GemelliDellaSessioneTests : IDisposable
{
    private const string Ap = "SectorFiles/Include/IT/OTHER/itap.ap";
    private const string ApFir = "SectorFiles/Include/IT/OTHER/lirr.ap";

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private (SessioneAperta Sessione, GemelliDellaSessione Gemelli) Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, GemelliDellaSessione.Di(sessione));
    }

    private static int Indice(SessioneAperta sessione, string file, string icao)
        => ((IFileConRecord)sessione.File[file]).RecordDelModello.Select((r, i) => (r, i))
            .First(v => v.r is AirportInfo a && a.IcaoCode == icao && !a.IsDisabled).i;

    [Fact]
    public void UnoScaloDelFileNazionaleHaLaSuaCopiaInQuelloDellaFir()
    {
        var (sessione, gemelli) = Apri();

        var altra = Assert.Single(gemelli.AltreCopie(Ap, Indice(sessione, Ap, "LIRF")));

        Assert.Equal(ApFir, altra.File);
        Assert.Equal(41, altra.Riga);
        Assert.Same(((IFileConRecord)sessione.File[ApFir]).RecordDelModello[altra.Indice], altra.Record);
    }

    [Fact]
    public void UnoScaloCheStaInUnFileSoloNonHaCopie()
    {
        var (sessione, gemelli) = Apri();

        Assert.Empty(gemelli.AltreCopie(Ap, Indice(sessione, Ap, "LIML")));
        Assert.Null(gemelli.GruppoDi(Ap, Indice(sessione, Ap, "LIML")));
    }

    [Fact]
    public void LeFamiglieSonoQuelleDeiCampioni()
    {
        var (_, gemelli) = Apri();

        Assert.Equal(55, gemelli.Gruppi.Count(g => g.Famiglia.EndsWith("*.ap", StringComparison.Ordinal)));
        Assert.Equal(63, gemelli.Gruppi.Count(g => g.Famiglia.EndsWith("*.rw", StringComparison.Ordinal)));
        Assert.All(gemelli.Gruppi, g => Assert.Equal(2, g.Copie.Count));
    }

    // I percorsi della sessione non badano alle maiuscole (è Windows): nemmeno l'indice.
    [Fact]
    public void IlPercorsoSiCercaSenzaBadareAlleMaiuscole()
    {
        var (sessione, gemelli) = Apri();

        Assert.Single(gemelli.AltreCopie(Ap.ToUpperInvariant(), Indice(sessione, Ap, "LIRF")));
    }
}
