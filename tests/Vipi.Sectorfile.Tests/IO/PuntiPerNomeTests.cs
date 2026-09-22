using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F2, slice 4: i punti per nome (<c>AMSOR;AMSOR;</c>) nei <c>.tfl</c>, nei <c>.mva</c> e nei tracciati dei
/// <c>.sid</c>. Nella libreria A erano 417 righe «malformate» sul master del 22 settembre 2026; in un <c>.tfl</c>
/// la prima di loro chiudeva il settore, e i vertici dopo finivano fuori dal record.
/// </summary>
public sealed class PuntiPerNomeTests
{
    private readonly CollectingWarnings _warnings = new();

    // libb_es_ctr.tfl ha 72 vertici per nome in 5 settori: ora ogni riga che non è intestazione, commento o riga
    // vuota è un vertice, e nessuna è opaca.
    [Fact]
    public void UnTflCoiVerticiPerNomeSiLeggeIntero()
    {
        string percorso = RealSectorFiles.Path("DYNAMIC_SEC/libb_es_ctr.tfl")!;
        var letto = new TflParser(_warnings).Parse(percorso, new ColorPalette());

        // Il settore LIBB_ND_CTR è tutto commentato: né intestazione né vertici.
        var attive = File.ReadAllLines(percorso)
            .Where(r => r.Trim().Length > 0 && !r.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .ToList();
        int intestazioni = attive.Count(r => r.Split(';').Length > 5);
        int vertici = attive.Count - intestazioni;

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(intestazioni, letto.Records.Count);
        Assert.Equal(vertici, letto.Records.Sum(r => r.Vertices.Count));
        Assert.Equal(Punto.Nominato("AMSOR"), letto.Records[0].Vertices[1]);
    }

    [Fact]
    public void UnVerticePerNomeNonChiudeIlSettore()
    {
        var letto = new TflParser(_warnings).Parse(ParserTestHelpers.Read(
            "LIBB_ES_CTR;CTR;1;CTR;1;\r\n" +
            "N041.13.55.000;E014.47.19.000;\r\n" +
            "AMSOR;AMSOR;\r\n" +
            "N040.51.52.000;E015.12.51.000;\r\n"), "libb_es_ctr.tfl");

        var settore = Assert.Single(letto.Records);
        Assert.Equal(3, settore.Vertices.Count);
        Assert.True(settore.Vertices[1].PerNome);
        Assert.Empty(_warnings.Snapshot());
    }

    // La riga di intestazione ha cinque campi, e i primi due non sono coordinate: letti come nomi passerebbero per
    // un vertice.
    [Fact]
    public void UnIntestazioneNonEUnVerticePerNome()
    {
        var letto = new TflParser(_warnings).Parse(ParserTestHelpers.Read(
            "A_CTR;CTR;1;CTR;1;\r\nAMSOR;AMSOR;\r\nB_CTR;CTR;1;CTR;1;\r\nLUNAR;LUNAR;\r\n"), "x.tfl");

        Assert.Equal(new[] { "A_CTR", "B_CTR" }, letto.Records.Select(r => r.SectorCode));
    }

    // Una coordinata sbagliata resta opaca: lovv.tfl:48 ha i secondi a 60, ed è un errore vero del sector.
    [Fact]
    public void UnaCoordinataSbagliataNonDiventaUnNome()
    {
        new TflParser(_warnings).Parse(ParserTestHelpers.Read(
            "LOVV_CTR;CTR;1;CTR;1;\r\nN047.42.27.000;E017.04.60.000;\r\n"), "lovv.tfl");

        Assert.Equal("Skipping malformed line", Assert.Single(_warnings.Snapshot()).Message);
    }

    // ENRMVA/lirr.mva: 18 vertici per nome, «Unparseable T; vertex» in A — il poligono perdeva il punto.
    [Fact]
    public void UnMvaDiRottaTieneIVerticiPerNome()
    {
        var letto = new MvaEnrouteParser(_warnings).Parse(RealSectorFiles.Path("ENRMVA/lirr.mva")!, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Contains(letto.Records.SelectMany(r => r.Vertices), v => v.Position == Punto.Nominato("UTENO"));
    }

    [Fact]
    public void UnVerticePerNomeDelMvaSiRiscriveComeEra()
        => Assert.Equal(
            new[] { "T;LIRR;UTENO;UTENO;LIRR;" },
            new MvaSaver(enroute: true).Serialize(new MvaSector
            {
                AltLabel = "100",
                Vertices = { new MvaVertex { Position = Punto.Nominato("UTENO"), ExtraField = "LIRR" } },
            }));

    // lied.sid: le partenze a vista hanno un tracciato sotto l'intestazione — 84 righe malformate in A.
    [Fact]
    public void LaSidConIlTracciatoLoTieneNelRecord()
    {
        var letto = new SidParser(_warnings).Parse(RealSectorFiles.Path("lied.sid")!, new ColorPalette());
        Assert.Empty(_warnings.Snapshot());

        var quirra = letto.Records.Single(s => s.Name == "QUIRRA DEP34");
        Assert.Equal("ZULU", quirra.Field4);
        Assert.Equal(8, quirra.Track.Count);
        Assert.Equal(Punto.Nominato("ZULU"), quirra.Track[2].Punto);
        Assert.Equal(Punto.Nominato("CAPO FERRATO"), quirra.Track[^1].Punto);

        var frasca = letto.Records.Single(s => s.Name == "FRASCA DEP34");
        Assert.Equal("GOLF", frasca.Track.Single(p => p.Etichetta is not null).Etichetta);

        // Le SID di una riga restano di una riga.
        Assert.Empty(letto.Records.Single(s => s.Name == "ALG5D").Track);
    }

    // NORTH DEP16: una riga vuota fra due punti spezza il tracciato, non chiude la SID.
    [Fact]
    public void UnaRigaVuotaNelTracciatoApreUnNuovoTratto()
    {
        var letto = new SidParser(_warnings).Parse(RealSectorFiles.Path("lied.sid")!, new ColorPalette());

        var north = letto.Records.Single(s => s.Name == "NORTH DEP16");
        Assert.Equal(8, north.Track.Count);
        Assert.Equal(new[] { 6 }, north.Track.Select((p, i) => (p, i)).Where(x => x.p.NuovoTratto).Select(x => x.i));
    }

    [Fact]
    public void LaRigaVuotaFraDueSidLeSeparaAncora()
    {
        var letto = new SidParser(_warnings).Parse(ParserTestHelpers.Read(
            "LIED;16;A1; ; ;\r\nN039.00.00.000;E008.00.00.000;\r\n\r\nLIED;16;B1; ; ;\r\n"), "lied.sid", new ColorPalette());

        Assert.Equal(new[] { 1, 0 }, letto.Records.Select(s => s.Track.Count));
    }

    [Fact]
    public void IlTracciatoSiRiscrive()
    {
        var sid = new SidProcedure { IcaoCode = "LIED", Runway = "16", Name = "A1", Field4 = " ", Field5 = " " };
        sid.Track.Add(new PuntoDelTracciato { Punto = new Coordinate(39, 8), Etichetta = "GOLF" });
        sid.Track.Add(new PuntoDelTracciato { Punto = Punto.Nominato("IP FRASCA"), NuovoTratto = true });

        Assert.Equal(
            new[] { "LIED;16;A1; ; ;", "N039.00.00.000;E008.00.00.000;GOLF;", "", "IP FRASCA;IP FRASCA;" },
            new SidSaver().Serialize(sid));
    }

    [Fact]
    public void LiedSidRoundTrip()
    {
        var (originale, riscritto) = ParserTestHelpers.RoundTrip(new SidParser(_warnings), new SidSaver(), RealSectorFiles.Path("lied.sid")!);
        Assert.Equal(originale, riscritto);
    }

    // Cambiare un vertice per nome con un altro nome: cambia quella riga, nei suoi due campi, e nient'altro.
    [Fact]
    public void UnVerticePerNomeSiCambiaDaSolo()
    {
        string percorso = RealSectorFiles.Path("DYNAMIC_SEC/libb_es_ctr.tfl")!;
        string temporaneo = Path.Combine(Path.GetTempPath(), "per-nome-" + Guid.NewGuid().ToString("N") + ".tfl");
        try
        {
            var saver = new TflSaver();
            var letto = new TflParser(_warnings).Parse(percorso, new ColorPalette()).FissaLeBasi(saver);
            var settore = letto.Records[0];
            settore.Vertices[1] = Punto.Nominato("LUNAR");

            new FileSaverOrchestrator().Save(letto, new HashSet<TflSector> { settore }, saver, temporaneo);

            var cambiate = File.ReadAllLines(percorso).Zip(File.ReadAllLines(temporaneo)).Where(c => c.First != c.Second).ToList();
            Assert.Equal(("AMSOR;AMSOR;", "LUNAR;LUNAR;"), Assert.Single(cambiate));
        }
        finally
        {
            File.Delete(temporaneo);
        }
    }

    // Nei .str i nomi c'erano già (ProcedureWaypoint, HoldingFixPoint); una coordinata che non si legge però
    // diventava in silenzio un FIX chiamato come lei. Resta un fix, ma ora lo si dice.
    [Fact]
    public void NelloStrUnaCoordinataSbagliataSiDice()
    {
        new StrParser(_warnings).Parse(ParserTestHelpers.Read(
            "LIRF;MAPS;ZONA; ; ;0;\r\nN041.00.00.000;E012.00.00.000;\r\nN047.44.75.000;E012.00.00.000;\r\nELKAP;ELKAP;\r\n"), "lirf.str");

        var avviso = Assert.Single(_warnings.Snapshot());
        Assert.Equal("Unparseable STR point", avviso.Message);
        Assert.Equal(3, avviso.LineNumber);
    }
}
