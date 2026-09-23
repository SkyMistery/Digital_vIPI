using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Carta F3-bis, slice 4, nel Lab: <b>una STAR spostata → la mappa composta segue</b>, nelle stesse modifiche in
/// sospeso e col suo diff; e torna com'era quando la STAR torna com'era. La forma è quella di <c>lime.str</c>.
/// </summary>
public sealed class MappeComposteNelLabTests : IDisposable
{
    private const string Lime = "SectorFiles/Include/IT/lime.str";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();
    private readonly SessioneAperta _sessione;

    public MappeComposteNelLabTests()
    {
        _albero.Scrivi(Lime, string.Join("\r\n",
            "//@\"STAR RNAV(ALL)\" composta=ODIN4E,EKLI4E",
            "//@START",
            "LIME;MAPS;STAR RNAV(ALL);;;;;1;",
            "ODINA;ODINA;<br>", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
            "EKLIB;EKLIB;<br>", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "",
            "N045.29.09.347;E010.01.56.131;<br>", "N045.28.41.379;E010.01.41.829;",
            "//@END \"STAR RNAV(ALL)\"",
            "",
            "LIME;28:10;ODIN4E;;;;;1;", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
            "LIME;28:10;EKLI4E;;;;;1;", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
            "LIME;28;NOBM3K;;;;;1;", "NOBMI;NOBMI;3K;", "ME565;ME565;", "OBFUL;OBFUL;") + "\r\n");
        _sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
    }

    public void Dispose() => _albero.Dispose();

    private FileAperto File => _sessione.File[Lime];

    private IReadOnlyList<StrRecord> Record => ((FileLetto<StrRecord>)File).Letto.Records;

    private int Indice(string nome) => Record.Select((r, i) => (r, i)).First(v => v.r.ProcedureId == nome).i;

    private IReadOnlyList<string> PuntiDellaMappa()
        => [.. MappeComposte.PuntiDi(Record[Indice("STAR RNAV(ALL)")]).Select(p => p.Chiave)];

    private object Sposta(string procedura, int posizione, string nuovo)
        => _modifiche.CambiaVertice(File, Indice(procedura), "Waypoints", posizione, nuovo, procedura);

    [Fact]
    public void UnaStarSpostataSiPortaDietroLaMappa()
    {
        Assert.IsType<ModificaDeiVertici>(Sposta("ODIN4E", 1, "ME999"));

        Assert.Contains("ME999", PuntiDellaMappa());
        Assert.DoesNotContain("ME872", PuntiDellaMappa());
        var mappa = Assert.Single(_modifiche.Voci.OfType<ModificaDellaComposta>());
        Assert.Equal(("STAR RNAV(ALL)", 11, 11), (mappa.Etichetta, mappa.Prima, mappa.Dopo));

        // Il diff vero: la riga della STAR e quella della mappa, nient'altro.
        var diff = _modifiche.DiffDi(File);
        Assert.Equal((2, 2), (diff.Tolte, diff.Aggiunte));
    }

    [Fact]
    public void AnnullareLaStarRimetteAncheLaMappa()
    {
        var spostata = Assert.IsType<ModificaDeiVertici>(Sposta("ODIN4E", 1, "ME999"));

        Assert.True(_modifiche.Annulla(File, spostata));

        Assert.Contains("ME872", PuntiDellaMappa());
        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.DiffDi(File).Pezzi);
    }

    // D8: se EKLI4E non passa più da OBFUL, non si innesta più sul tratto di ODIN4E e va disegnata fino in fondo.
    [Fact]
    public void UnaStarCheNonSInnestaPiuSiDisegnaTutta()
    {
        Sposta("EKLI4E", 2, "MX001");

        Assert.Equal(["ODINA", "ODINA", "ME872", "OBFUL", "TIXUM", "EKLIB", "EKLIB", "ME768", "MX001", "TIXUM"],
            PuntiDellaMappa().Take(10));
    }

    [Fact]
    public void AnnullareLaMappaLaLasciaComEraELaStarResta()
    {
        Sposta("ODIN4E", 1, "ME999");
        var mappa = Assert.Single(_modifiche.Voci.OfType<ModificaDellaComposta>());

        Assert.True(_modifiche.Annulla(File, mappa));

        Assert.Contains("ME872", PuntiDellaMappa());
        Assert.Single(_modifiche.Voci);   // resta lo spostamento della STAR
    }

    [Fact]
    public void UnaStarCheLaMappaNonElencaNonLaTocca()
    {
        Sposta("NOBM3K", 1, "ME566");

        Assert.Empty(_modifiche.Voci.OfType<ModificaDellaComposta>());
        Assert.Contains("ME872", PuntiDellaMappa());
    }

    // I punti di una STAR del .str sono nomi, e molti cominciano con N, S, E o W (NELAB, SOKVO, EKLIB).
    [Fact]
    public void UnNomeCheCominciaConNSEWEUnNomeUnaCoordinataNo()
    {
        Assert.IsType<ModificaDeiVertici>(Sposta("ODIN4E", 1, "NELAB"));
        Assert.Contains("NELAB", PuntiDellaMappa());

        var rifiutata = Assert.IsType<ModificaRifiutata>(Sposta("ODIN4E", 1, "N045.00.00.000 E010.00.00.000"));
        Assert.Contains("per nome", rifiutata.Motivo, StringComparison.Ordinal);
    }
}
