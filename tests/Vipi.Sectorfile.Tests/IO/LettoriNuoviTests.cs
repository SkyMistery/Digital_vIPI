using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F2, slice 6: i tre formati che la libreria A non leggeva — le attese di rotta (<c>.hold</c>), le rotte VFR
/// (<c>.vrt</c>) e le aree P/R/D (<c>.restrict</c>, <c>.prohibit</c>, <c>.danger</c>). Sui file veri del master
/// <c>7e761aa</c>.
/// </summary>
public sealed class LettoriNuoviTests
{
    private readonly CollectingWarnings _warnings = new();

    [Fact]
    public void LeAtteseDiRottaSonoTutteLette()
    {
        string percorso = RealSectorFiles.Path("HOLDENR.hold")!;
        var letto = new HoldParser(_warnings).Parse(percorso, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(68, letto.Records.Count);
        Assert.Equal(File.ReadLines(percorso).Count(r => r.StartsWith("HLD-", StringComparison.Ordinal)), letto.Records.Count);

        var abboz = letto.Records[0];
        Assert.Equal("HLD-ABBOZ", abboz.Nome);
        Assert.Equal(CoordinateConverter.ParsePair("N046.02.37.000", "E011.07.48.000"), abboz.Posizione.Posizione);
        Assert.Equal("ABBOZ/225R-9000", abboz.Descrizione);
        Assert.Equal(("ABBOZ", 225, 'R', "9000"), (abboz.Fix, abboz.Rotta, abboz.Verso, abboz.Quota));

        // Tutte e 68 (71 righe, 3 vuote) hanno la forma solita; l'ultima non ha il `;` finale ed è letta lo stesso.
        Assert.All(letto.Records, a => Assert.NotNull(a.Quota));
        var oze = letto.Records.Single(a => a.Nome == "HLD-OZE");
        Assert.Equal(("OZE", 196, 'L', "FL135"), (oze.Fix, oze.Rotta, oze.Verso, oze.Quota));
    }

    [Fact]
    public void UnaDescrizioneFuoriFormaResta()
    {
        var letto = new HoldParser(_warnings).Parse(ParserTestHelpers.Read("HLD-X;N046.00.00.000;E011.00.00.000;vedi carta;\r\n"), "t.hold", new ColorPalette());

        var attesa = Assert.Single(letto.Records);
        Assert.Equal("vedi carta", attesa.Descrizione);
        Assert.Null(attesa.Fix);
        Assert.Null(attesa.Rotta);
    }

    // liba.vrt: quattro rotte separate da righe vuote, punti per nome, tre dei quali cominciano con una cifra
    // (`2NM NORTH LUCERA`, VRP di liba.vfi) — prima della regola di Punto erano 7 righe opache nell'albero.
    [Fact]
    public void LeRotteVfrSonoLeRigheConLoStessoNumero()
    {
        var letto = new VrtParser(_warnings).Parse(RealSectorFiles.Path("liba.vrt")!, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(new[] { "1", "2", "3", "4" }, letto.Records.Select(r => r.Numero));
        Assert.Equal(new[] { 4, 4, 3, 3 }, letto.Records.Select(r => r.Punti.Count));
        Assert.All(letto.Records.SelectMany(r => r.Punti), p => Assert.True(p.PerNome));
        Assert.Contains(letto.Records[0].Punti, p => p.Nome == "2NM NORTH LUCERA");
    }

    // libv.vrt: il numero cambia senza righe vuote, i punti sono coordinate, e ogni riga ha due campi che nessuna
    // specifica spiega (`;;1;`): restano nella riga.
    [Fact]
    public void UnaRottaFinisceDoveCambiaIlNumero()
    {
        string percorso = RealSectorFiles.Path("libv.vrt")!;
        var letto = new VrtParser(_warnings).Parse(percorso, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(4, letto.Records.Count);
        Assert.All(letto.Records, r => Assert.Equal(2, r.Punti.Count));
        Assert.All(letto.Records.SelectMany(r => r.Punti), p => Assert.False(p.PerNome));

        var (originale, riscritto) = ParserTestHelpers.RoundTrip(new VrtParser(_warnings), new VrtSaver(), percorso);
        Assert.Equal(originale, riscritto);
    }

    // lirh.vrt: fine riga LF e un commento in coda, dopo l'ultima rotta, che resta fuori dal record.
    [Fact]
    public void IlCommentoInCodaNonEUnaRotta()
    {
        string percorso = RealSectorFiles.Path("lirh.vrt")!;
        var letto = new VrtParser(_warnings).Parse(percorso, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(3, letto.Records.Count);
        Assert.IsType<RawChunk<RottaVfr>>(letto.Chunks[^1]);

        var (originale, riscritto) = ParserTestHelpers.RoundTrip(new VrtParser(_warnings), new VrtSaver(), percorso);
        Assert.Equal(originale, riscritto);
    }

    [Fact]
    public void UnaRigaVfrSenzaNumeroEOpaca()
    {
        var letto = new VrtParser(_warnings).Parse(ParserTestHelpers.Read("1;MNL;MNL;\r\nMNL;MNL;\r\n1;TROIA;TROIA;\r\n"), "t.vrt");

        var avviso = Assert.Single(_warnings.Snapshot());
        Assert.Equal(2, avviso.LineNumber);
        Assert.Equal(2, letto.Records.Count);   // la riga opaca spezza la rotta
    }

    // italy.danger: i segmenti .geo col nome dell'area nel sesto campo.
    [Fact]
    public void LeAreePrdPortanoIlNome()
    {
        string percorso = RealSectorFiles.Path("GEO/italy.danger")!;
        var letto = new GeoParser(_warnings).Parse(percorso, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(664, letto.Records.Count);
        Assert.All(letto.Records, s => Assert.Equal("DANGER", s.Color));
        Assert.Equal(6, letto.Records.Count(s => s.Nome == "D5A"));

        var (originale, riscritto) = ParserTestHelpers.RoundTrip(new GeoParser(_warnings), new GeoSaver(), percorso);
        Assert.Equal(originale, riscritto);
    }

    // Un .geo non ha il sesto campo, e lo scrittore non lo aggiunge.
    [Fact]
    public void UnSegmentoSenzaNomeNonNeScriveUno()
    {
        var segmento = new Line
        {
            Start = CoordinateConverter.ParsePair("N041.00.00.000", "E012.00.00.000"),
            End = CoordinateConverter.ParsePair("N041.00.01.000", "E012.00.01.000"),
            Color = "COAST",
        };

        Assert.Equal("N041.00.00.000;E012.00.00.000;N041.00.01.000;E012.00.01.000;COAST;", Assert.Single(new GeoSaver().Serialize(segmento)));
        segmento.Nome = "R4";
        Assert.EndsWith(";COAST;R4;", Assert.Single(new GeoSaver().Serialize(segmento)));
    }

    // Errore vero del sector (italy.prohibit:3132, P154; così 86 righe di P154, P219 e R107A-D): lo SPAZIO al posto
    // del `;` fra latitudine e longitudine. Resta opaco, e il segmento non si disegna: R107A-D non ne hanno uno buono.
    [Fact]
    public void LoSpazioAlPostoDelPuntoEVirgolaEUnErrore()
    {
        const string Riga = "N038.55.55.424 E016.36.08.523;N038.55.53.716;E016.36.15.757;PROHIBIT;P154;";
        var letto = new GeoParser(_warnings).Parse(ParserTestHelpers.Read(Riga + "\r\n"), "italy.prohibit", new ColorPalette());

        Assert.Empty(letto.Records);
        Assert.Equal(Riga, Assert.Single(_warnings.Snapshot()).RawSnippet);
    }
}
