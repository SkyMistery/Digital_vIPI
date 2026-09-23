using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// «Incolla da testo» dopo le prove a mano del committente (23 settembre): gli archi fitti come quelli del file, e la
/// forma chiusa se lo era. Incollato su <c>LIRN_TWR</c>, un arco di 11 punti ne diventava 95 e l'ultimo vertice (che
/// ripeteva il primo) spariva.
/// </summary>
public sealed class DensitaEChiusuraTests : IDisposable
{
    private const string Torri = "SectorFiles/Include/IT/DYNAMIC_SEC/twrs.tfl";
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl";

    /// <summary>L'esempio del committente della carta F1: un lato, un arco di 17 NM, un punto.</summary>
    private const string TestoAip =
        "44°51'24\" N 008°14'57\" E\n" +
        "then arc of circle in clockwise direction radius 17 NM centred on\n" +
        "44°55'29\" N 007°51'43\" E\n" +
        "till point\n" +
        "44°41'08\" N 008°04'34\" E";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static int Indice(FileAperto file, string nome)
        => Ispettore.Etichette(file, null).Select((e, i) => (e, i)).First(v => v.e.StartsWith(nome, StringComparison.Ordinal)).i;

    /// <summary>Un arco di cerchio disegnato a punti, uno ogni <paramref name="passo"/> gradi, più due lati dritti.</summary>
    private static List<Coordinate> ArcoConPasso(double passo)
    {
        var punti = new List<Coordinate> { new(41.0, 12.0) };
        for (double a = 0; a <= 120 + 1e-9; a += passo)
        {
            double r = a * Math.PI / 180;
            punti.Add(new Coordinate(41.5 + (0.2 * Math.Sin(r)), 12.5 + (0.2 * Math.Cos(r) / Math.Cos(41.5 * Math.PI / 180))));
        }

        punti.Add(new Coordinate(41.0, 12.0));
        return punti;
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(4.0)]
    [InlineData(2.0)]
    public void LaStimaTrovaIlPassoDiUnArco(double passo)
    {
        double? stima = DensitaDegliArchi.Stima([ArcoConPasso(passo)]);

        Assert.NotNull(stima);
        Assert.InRange(1 / stima.Value, passo * 0.95, passo * 1.05);
    }

    [Fact]
    public void UnPoligonoDiLatiDrittiNonHaArchiDaMisurare()
    {
        Coordinate[] quadrato = [new(41, 12), new(41, 13), new(42, 13), new(42, 12), new(41, 12)];

        Assert.Null(DensitaDegliArchi.Stima([quadrato]));
    }

    [Fact]
    public void GliArchiVeriDiLirnTwrDannoUnPassoDiQualcheGrado()
    {
        // Misurato sull'albero vero: un punto ogni 4,3° (i tre archi vanno da ~8° a ~4,6°).
        var file = Apri().File[Torri];
        int lirn = Indice(file, "LIRN_TWR");

        double? stima = DensitaDegliArchi.Stima(ElenchiDiVertici.Di(file, lirn).Select(e => e.Posizioni()));

        Assert.NotNull(stima);
        Assert.InRange(1 / stima.Value, 3.0, 9.0);
    }

    [Fact]
    public void UnaFormaChiusaResta_ChiusaDopoLIncolla()
    {
        var file = Apri().File[Torri];
        int lirn = Indice(file, "LIRN_TWR");
        var vertici = ElenchiDiVertici.Uno(file, lirn, "Vertices")!;
        Assert.Equal(vertici.Scrivi(0), vertici.Scrivi(vertici.Quanti - 1));

        Assert.IsType<ModificaDeiVertici>(_modifiche.IncollaVertici(file, lirn, "Vertices", TestoAip, puntiPerGrado: 0.25));

        Assert.Equal(vertici.Scrivi(0), vertici.Scrivi(vertici.Quanti - 1));
    }

    [Fact]
    public void UnaFormaAperta_NonSiChiudeDaSola()
    {
        // I settori dei campioni sono chiusi: se ne apre uno togliendo l'ultimo vertice (quello che ripete il primo).
        var file = Apri().File[Settore];
        var vertici = ElenchiDiVertici.Uno(file, 0, "Vertices")!;
        Assert.IsType<ModificaDeiVertici>(_modifiche.TogliVertice(file, 0, "Vertices", vertici.Quanti - 1));
        Assert.NotEqual(vertici.Scrivi(0), vertici.Scrivi(vertici.Quanti - 1));

        Assert.IsType<ModificaDeiVertici>(_modifiche.IncollaVertici(file, 0, "Vertices", TestoAip, puntiPerGrado: 0.25));

        Assert.NotEqual(vertici.Scrivi(0), vertici.Scrivi(vertici.Quanti - 1));
    }

    [Fact]
    public void LaDensitaDecideQuantiPuntiHaLArco()
    {
        var file = Apri().File[Settore];

        var fitto = Assert.IsType<ModificaDeiVertici>(_modifiche.IncollaVertici(file, 0, "Vertices", TestoAip, puntiPerGrado: 1.0));
        _modifiche.AnnullaTutto(_ => file);
        var rado = Assert.IsType<ModificaDeiVertici>(_modifiche.IncollaVertici(file, 0, "Vertices", TestoAip, puntiPerGrado: 0.125));

        Assert.True(rado.Dopo < fitto.Dopo / 4, $"un punto ogni 8°: {rado.Dopo} vertici, uno per grado: {fitto.Dopo}");
    }
}
