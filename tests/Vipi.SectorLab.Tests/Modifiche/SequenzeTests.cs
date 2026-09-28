using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Le sequenze di punti su tutti i file (lotto «Subito», slice 5a, «file per file» B7): il tracciato delle aerovie, i
/// vertici delle MVA e dei confini diventano elenchi come gli altri, e ogni elenco si può leggere al contrario.
/// </summary>
public sealed class SequenzeTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private FileAperto Apri(string relativo)
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/" + relativo];

    private static int Primo<T>(FileAperto file, Func<T, bool> quale)
        => ((IFileConRecord)file).RecordDelModello.Select((r, i) => (r, i)).First(v => v.r is T t && quale(t)).i;

    private IReadOnlyList<string> Righe(FileAperto file)
        => ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));

    private IReadOnlyList<string> Aggiunte(FileAperto file)
        => [.. _modifiche.DiffDi(file).Pezzi.SelectMany(p => p.Righe).Where(r => r.Segno == SegnoDelDiff.Aggiunta).Select(r => r.Testo)];

    // --- il tracciato delle aerovie ---------------------------------------------------------------------------

    [Fact]
    public void IlTracciatoDiUnAeroviaEUnElencoDiNomi()
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);

        var tracciato = Assert.Single(ElenchiDiVertici.Di(file, l613), e => e.Chiave == "FixLabels");

        Assert.Equal("Tracciato", tracciato.Nome);
        Assert.True(tracciato.AmmetteNomi);
        Assert.False(tracciato.AmmetteCoordinate);
    }

    [Fact]
    public void UnPuntoDelTracciatoSiScriveComeLeRigheT()
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);

        var fatta = _modifiche.CambiaVertice(file, l613, "FixLabels", 1, "LAPAB");

        Assert.IsType<ModificaDeiVertici>(fatta);
        Assert.Equal(["T;L613;LAPAB;LAPAB;"], Aggiunte(file));
        Assert.Equal(1, _modifiche.DiffDi(file).Tolte);
    }

    [Theory]
    [InlineData("N041.00.00.000 E012.00.00.000", "per nome")]
    [InlineData("LAPAB VIC", "nome solo")]
    public void NelTracciatoDiUnAeroviaCoordinateEDueNomiSiRifiutano(string testo, string motivo)
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);

        var esito = _modifiche.CambiaVertice(file, l613, "FixLabels", 1, testo);

        Assert.Contains(motivo, Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvertireUnAeroviaRiscriveLeSueRigheAlContrario()
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);
        var prima = ((Airway)((IFileConRecord)file).RecordDelModello[l613]).FixLabels.ToList();

        var fatta = _modifiche.InvertiVertici(file, l613, "FixLabels");

        Assert.IsType<ModificaDeiVertici>(fatta);
        var letto = (Airway)((IFileConRecord)file).RecordDelModello[l613];
        Assert.Equal(Enumerable.Reverse(prima), letto.FixLabels);
        Assert.Equal(prima.Select(p => $"T;L613;{p};{p};").Reverse(),
            Righe(file).Where(r => r.StartsWith("T;L613;", StringComparison.Ordinal)).Take(prima.Count));
    }

    [Fact]
    public void InvertireDueVolteNonLasciaModifiche()
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);

        _modifiche.InvertiVertici(file, l613, "FixLabels");
        _modifiche.InvertiVertici(file, l613, "FixLabels");

        Assert.False(_modifiche.CEQualcosa);
    }

    // --- le MVA -----------------------------------------------------------------------------------------------

    [Fact]
    public void UnVerticeNuovoDiUnaMvaDiAccPrendeIlGruppoDeiVicini()
    {
        var file = Apri("ENRMVA/lirr.mva");
        int zona = Primo<MvaSector>(file, m => m.Vertices.Count > 2);

        var fatta = _modifiche.AggiungiVertice(file, zona, "Vertices", 1, "N041.00.00.000 E012.00.00.000");

        Assert.IsType<ModificaDeiVertici>(fatta);
        // «File per file» E3: il 5° campo di ogni T nuova è il gruppo del file.
        Assert.Equal(["T;LIRR;N041.00.00.000;E012.00.00.000;LIRR;"], Aggiunte(file));
        Assert.Equal(0, _modifiche.DiffDi(file).Tolte);
    }

    [Fact]
    public void UnVerticeNuovoDiUnaZonaDiScaloSenzaRigaLTieneIlNomeDellaZona()
    {
        // 🔴 Lo scrittore metteva la QUOTA nel 2° campo; una zona senza riga L (CERCHIO-BA) non ce l'ha, e usciva
        // `T;;N…;E…;`: per Aurora un'altra zona.
        var file = Apri("liba.mva");
        int cerchio = Primo<MvaSector>(file, m => m.Nome == "CERCHIO-BA");

        _modifiche.AggiungiVertice(file, cerchio, "Vertices", 1, "N041.00.00.000 E014.00.00.000");

        Assert.Equal(["T;CERCHIO-BA;N041.00.00.000;E014.00.00.000;"], Aggiunte(file));
    }

    // --- i confini (.hartcc, .lartcc, .artcc) --------------------------------------------------------------------

    [Fact]
    public void UnConfineHaUnElencoPerPoligonoESiScriveAncheCoiNomi()
    {
        var file = Apri("HI_AIRSPACE/lirr.hartcc");
        int conf1 = Primo<StaticBoundaryGroup>(file, g => g.Name == "RR CONF1");
        var poligono = Assert.Single(ElenchiDiVertici.Di(file, conf1), e => e.Chiave == "Polygons[0].Vertices");
        Assert.Equal("Poligono 1", poligono.Nome);
        Assert.Equal("TIPNI", poligono.Scrivi(2));

        var fatta = _modifiche.CambiaVertice(file, conf1, "Polygons[0].Vertices", 2, "OTNUN");

        Assert.IsType<ModificaDeiVertici>(fatta);
        Assert.Equal(["T;RR CONF1;OTNUN;OTNUN;"], Aggiunte(file));
        Assert.Equal(1, _modifiche.DiffDi(file).Tolte);
    }

    // --- «inverti» e le interruzioni --------------------------------------------------------------------------

    [Fact]
    public void InvertendoUnaSidLaRigaVuotaRestaFraGliStessiDuePunti()
    {
        // lied.sid, NORTH DEP16: sei punti, una riga vuota, due punti. Al contrario: due, la riga vuota, sei.
        var file = Apri("lied.sid");
        int north = Primo<SidProcedure>(file, s => s.Name == "NORTH DEP16");

        _modifiche.InvertiVertici(file, north, "Track");

        var righe = Righe(file);
        int testa = righe.ToList().FindIndex(r => r.StartsWith("LIED;16R:16L;NORTH DEP16;", StringComparison.Ordinal));
        Assert.Equal(
        [
            "N039.32.36.880;E008.13.32.751;",
            "N039.23.12.947;E008.38.31.708;",
            "",
            "N039.14.46.687;E008.53.50.646;",
            "N039.14.00.998;E008.55.01.400;",
            "N039.13.49.891;E008.57.18.587;",
            "N039.14.26.211;E008.58.47.922;",
            "N039.15.28.213;E008.59.37.479;",
            "N039.20.30.313;E008.58.34.537;",
        ], righe.Skip(testa + 1).Take(9));
    }

    [Fact]
    public void InvertendoUnaMappaStrIlBrPassaAllAltroPuntoDellaCoppia()
    {
        var file = Apri("lirf.str");
        // Una procedura per nomi con un <br> dentro (non sul primo punto): il tratto nuovo comincia lì.
        int indice = Primo<ProcedureStrRecord>(file, p => p.Waypoints.Skip(1).Any(w => w.IniziaUnTratto));
        var prima = ((ProcedureStrRecord)((IFileConRecord)file).RecordDelModello[indice]).Waypoints
            .Select(w => (w.FixName, w.SuffixCode, w.IniziaUnTratto)).ToList();
        int n = prima.Count;

        _modifiche.InvertiVertici(file, indice, "Waypoints");

        var dopo = ((ProcedureStrRecord)((IFileConRecord)file).RecordDelModello[indice]).Waypoints;
        for (int k = 0; k < n; k++)
        {
            Assert.Equal(prima[n - 1 - k].FixName, dopo[k].FixName);
            Assert.Equal(prima[n - 1 - k].SuffixCode, dopo[k].SuffixCode);   // il suffisso resta col suo punto
            Assert.Equal(k == 0 ? prima[0].IniziaUnTratto : prima[n - k].IniziaUnTratto, dopo[k].IniziaUnTratto);
        }
    }

    [Fact]
    public void DoveISeparatoriNonSonoQuelliDelLabInvertireSiRifiuta()
    {
        // 🔴 Dalla misura sul fork (limc_star.lartcc): il separatore `T;dummy;INLER;INLER;` lo scrittore non lo produce,
        // e girando le righe viaggiava con la riga sotto, dentro un altro poligono. Il Lab rilegge e non lo fa.
        _albero.Scrivi("SectorFiles/Include/IT/LOW_AIRSPACE/limc_star.lartcc", string.Join("\r\n",
            "T;LIMC 35;ASTIG;ASTIG;",
            "T;LIMC 35;MC588;MC588;",
            "T;LIMC 35;INLER;INLER;",
            "T;dummy;INLER;INLER;",
            "T;LIMC 35;DEVOX;DEVOX;<br>;",
            "T;LIMC 35;DEVOX;DEVOX;3E;",
            "T;LIMC 35;MC588;MC588;",
            "T;dummy;INLER;INLER;",
            "T;LIMC 35;EVRIP;EVRIP;<br>;",
            "T;LIMC 35;EVRIP;EVRIP;3E;",
            "T;LIMC 35;MC895;MC895;",
            ""));
        var file = Apri("LOW_AIRSPACE/limc_star.lartcc");
        var prima = ((IFileConRecord)file).RigheDelFile([]);

        var esito = _modifiche.InvertiVertici(file, 0, "Polygons[1].Vertices");

        Assert.Contains("non si rilegge uguale", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnPuntoConGliSpaziSiCopiaSenzaPassareDalTesto()
    {
        // Dalla misura: «+ in fondo» su una rotta VFR (FOCI DEL FORTORE) passava il nome come testo, e il testo con gli
        // spazi si leggeva come coordinate sbagliate. La copia prende il punto com'è.
        var file = Apri("liba.vrt");

        var fatta = _modifiche.AggiungiVertice(file, 0, "Punti", 0, testo: null);

        Assert.IsType<ModificaDeiVertici>(fatta);
        Assert.Equal(["1;FOCI DEL FORTORE;FOCI DEL FORTORE;"], Aggiunte(file));
    }

    [Fact]
    public void UnPuntoSoloNonSiInverte()
    {
        var file = Apri("liba.mva");
        int cerchio = Primo<MvaSector>(file, m => m.Nome == "CERCHIO-BA");
        var elenco = ElenchiDiVertici.Uno(file, cerchio, "Vertices")!.Elenco;
        while (elenco.Count > 1)
            elenco.RemoveAt(elenco.Count - 1);

        Assert.IsType<ModificaRifiutata>(_modifiche.InvertiVertici(file, cerchio, "Vertices"));
    }
}
