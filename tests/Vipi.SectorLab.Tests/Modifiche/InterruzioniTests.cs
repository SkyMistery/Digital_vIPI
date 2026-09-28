using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Spezza e unisci (lotto «Subito», slice 5b, «file per file» B6 e R-3): lo stesso gesto nelle cinque scritture dei file —
/// la riga vuota delle SID e delle MVA di scalo, il <c>&lt;br&gt;</c> degli <c>.str</c>, il <c>T;DUMMY</c> dei confini, il
/// <c>T;BREAK</c> delle aerovie. Spezzare e riunire torna al file di prima.
/// </summary>
public sealed class InterruzioniTests : IDisposable
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

    private static int Record(FileAperto file) => ((IFileConRecord)file).RecordDelModello.Count;

    private IReadOnlyList<string> Aggiunte(FileAperto file)
        => [.. _modifiche.DiffDi(file).Pezzi.SelectMany(p => p.Righe).Where(r => r.Segno == SegnoDelDiff.Aggiunta).Select(r => r.Testo)];

    private IReadOnlyList<string> Tolte(FileAperto file)
        => [.. _modifiche.DiffDi(file).Pezzi.SelectMany(p => p.Righe).Where(r => r.Segno == SegnoDelDiff.Tolta).Select(r => r.Testo)];

    // --- SID: la riga vuota -----------------------------------------------------------------------------------

    [Fact]
    public void LaRigaVuotaDiUnaSidEUnInterruzioneDentroIlTracciato()
    {
        var file = Apri("lied.sid");
        int north = Primo<SidProcedure>(file, s => s.Name == "NORTH DEP16");

        var tracciato = ElenchiDiVertici.Uno(file, north, "Track")!;

        Assert.Equal(FormaDellInterruzione.RigaVuota, Interruzioni.Forma(file, north, "Track"));
        Assert.Equal([5], Interruzioni.Dentro(tracciato));
    }

    [Fact]
    public void UnireUnaSidToglieLaRigaVuotaESpezzareLaRimette()
    {
        var file = Apri("lied.sid");
        int north = Primo<SidProcedure>(file, s => s.Name == "NORTH DEP16");
        var prima = Righe(file);

        var unita = _modifiche.SpezzaOUnisci(file, north, "Track", 5, spezza: false, "NORTH DEP16");

        var voce = Assert.IsType<ModificaDelTesto>(unita);
        Assert.Equal("NORTH DEP16: riunita dopo N039.14.46.687 E008.53.50.646", voce.Descrizione);
        Assert.Equal([""], Tolte(file));
        Assert.Empty(Interruzioni.Dentro(ElenchiDiVertici.Uno(file, north, "Track")!));

        _modifiche.SpezzaOUnisci(file, north, "Track", 5, spezza: true);

        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);   // tornato il file dell'apertura: niente da salvare
    }

    [Fact]
    public void SpezzareDoveELaLineaEGiaInterrottaSiRifiuta()
    {
        var file = Apri("lied.sid");
        int north = Primo<SidProcedure>(file, s => s.Name == "NORTH DEP16");

        var esito = _modifiche.SpezzaOUnisci(file, north, "Track", 5, spezza: true);

        Assert.Contains("già interrotta", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
    }

    // --- .str: il <br> --------------------------------------------------------------------------------------

    [Fact]
    public void InUnaProceduraStrSpezzareMetteIlBrNelPuntoDopo()
    {
        var file = Apri("lirf.str");
        int indice = Primo<ProcedureStrRecord>(file, p => p.Waypoints.Count > 3 && p.Waypoints.All(w => !w.IniziaUnTratto && w.SuffixCode is null));
        var waypoints = ((ProcedureStrRecord)((IFileConRecord)file).RecordDelModello[indice]).Waypoints;
        string dopo = waypoints[2].FixName;
        var prima = Righe(file);

        Assert.IsType<ModificaDelTesto>(_modifiche.SpezzaOUnisci(file, indice, "Waypoints", 1, spezza: true));

        // Come lo scrive il file: in lirf.str i <br> sono quasi tutti `…;<br>;`.
        Assert.Equal([$"{dopo};{waypoints[2].DisplayLabel};<br>;"], Aggiunte(file));
        Assert.True(((ProcedureStrRecord)((IFileConRecord)file).RecordDelModello[indice]).Waypoints[2].IniziaUnTratto);

        _modifiche.SpezzaOUnisci(file, indice, "Waypoints", 1, spezza: false);
        Assert.Equal(prima, Righe(file));
    }

    [Fact]
    public void UnPuntoColSuffissoSiRipeteColBrComeNeiFile()
    {
        // Suffisso (4E) e <br> stanno nello stesso campo: i file ripetono il punto, una riga col <br> e una col suffisso.
        var file = Apri("lirf.str");
        int indice = Primo<ProcedureStrRecord>(file, p => p.Waypoints.Skip(1).Any(w => w.SuffixCode is not null && !w.IniziaUnTratto)
                                                        && p.Waypoints.All(w => !w.IniziaUnTratto));
        var waypoints = ((ProcedureStrRecord)((IFileConRecord)file).RecordDelModello[indice]).Waypoints;
        int k = Enumerable.Range(1, waypoints.Count - 1).First(i => waypoints[i].SuffixCode is not null) - 1;
        var col = waypoints[k + 1];
        var prima = Righe(file);

        _modifiche.SpezzaOUnisci(file, indice, "Waypoints", k, spezza: true);

        Assert.Equal([$"{col.FixName};{col.DisplayLabel};<br>;"], Aggiunte(file));
        Assert.Empty(Tolte(file));

        _modifiche.SpezzaOUnisci(file, indice, "Waypoints", k, spezza: false);
        Assert.Equal(prima, Righe(file));
    }

    // --- aerovie: il BREAK ----------------------------------------------------------------------------------

    [Fact]
    public void SpezzareUnAeroviaScriveIlBreakEFaTreRecord()
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);
        int record = Record(file);
        var prima = Righe(file);

        Assert.IsType<ModificaDelTesto>(_modifiche.SpezzaOUnisci(file, l613, "FixLabels", 1, spezza: true));

        Assert.Equal(["T;BREAK;GONOT;GONOT;"], Aggiunte(file));
        Assert.Equal(record + 2, Record(file));
        Assert.Equal(["PAPIZ", "GONOT"], ((Airway)((IFileConRecord)file).RecordDelModello[l613]).FixLabels);
        Assert.Equal("L613", Interruzioni.Dopo(file, l613, "FixLabels")!.Testo[..4]);

        _modifiche.SpezzaOUnisci(file, l613, "FixLabels", 1, spezza: false);
        Assert.Equal(prima, Righe(file));
        Assert.Equal(record, Record(file));
    }

    [Fact]
    public void UnireUnAeroviaToglieIlBreakAncheColCommentoInCoda()
    {
        // itawlow: `T;BREAK;VADIK;VADIK; //discontinuity (creates a break)` fra i due pezzi di L615.
        var file = Apri("AIRWAY/itawlow.lairway");
        int l615 = Primo<Airway>(file, a => a.Name == "L615");
        int ultimo = ((Airway)((IFileConRecord)file).RecordDelModello[l615]).FixLabels.Count - 1;
        int record = Record(file);

        Assert.IsType<ModificaDelTesto>(_modifiche.SpezzaOUnisci(file, l615, "FixLabels", ultimo, spezza: false));

        Assert.Equal(["T;BREAK;VADIK;VADIK; //discontinuity (creates a break)"], Tolte(file));
        Assert.Equal(record - 2, Record(file));
    }

    [Fact]
    public void SeFraIDuePezziCEUnCommentoUnireSiRifiuta()
    {
        // Sul fork (itawlow.lairway:187) il commento sta su una riga sua sopra il BREAK, e per il Lab un commento chiude
        // l'aerovia: tolto il BREAK i pezzi resterebbero due. Il Lab lo vede rileggendo, e non scrive niente.
        _albero.Scrivi("SectorFiles/Include/IT/AIRWAY/prova.lairway", string.Join("\r\n",
            "T;L613;LUMAR;LUMAR;",
            "T;L613;RIVAM;RIVAM;",
            "//discontinuity (creates a break)",
            "T;BREAK;RIVAM;RIVAM;",
            "T;L613;LAPAB;LAPAB;",
            "T;L613;VIC;VIC;",
            ""));
        var file = Apri("AIRWAY/prova.lairway");
        Assert.NotNull(Interruzioni.Dopo(file, 0, "FixLabels"));

        var esito = _modifiche.SpezzaOUnisci(file, 0, "FixLabels", 1, spezza: false);

        Assert.Contains("commento", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
        Assert.False(_modifiche.CEQualcosa);
    }

    // --- confini: il DUMMY ----------------------------------------------------------------------------------

    [Fact]
    public void SpezzareUnConfineScriveIlDummyEFaDuePoligoni()
    {
        var file = Apri("HI_AIRSPACE/lirr.hartcc");
        int conf1 = Primo<StaticBoundaryGroup>(file, g => g.Name == "RR CONF1");
        int poligoni = ((StaticBoundaryGroup)((IFileConRecord)file).RecordDelModello[conf1]).Polygons.Count;
        var prima = Righe(file);

        _modifiche.SpezzaOUnisci(file, conf1, "Polygons[0].Vertices", 2, spezza: true);

        Assert.Equal(["T;DUMMY;TIPNI;TIPNI;"], Aggiunte(file));
        Assert.Equal(poligoni + 1, ((StaticBoundaryGroup)((IFileConRecord)file).RecordDelModello[conf1]).Polygons.Count);
        Assert.Equal("Poligono 2", Interruzioni.Dopo(file, conf1, "Polygons[0].Vertices")!.Testo);

        _modifiche.SpezzaOUnisci(file, conf1, "Polygons[0].Vertices", 2, spezza: false);
        Assert.Equal(prima, Righe(file));
    }

    // --- MVA ------------------------------------------------------------------------------------------------

    [Fact]
    public void UnaZonaMvaDiScaloSiSpezzaConLaRigaVuota()
    {
        var file = Apri("liba.mva");
        int cerchio = Primo<MvaSector>(file, m => m.Nome == "CERCHIO-BA");
        int record = Record(file);
        var prima = Righe(file);

        _modifiche.SpezzaOUnisci(file, cerchio, "Vertices", 3, spezza: true);

        Assert.Equal([""], Aggiunte(file));
        Assert.Equal(record + 1, Record(file));
        Assert.NotNull(Interruzioni.Dopo(file, cerchio, "Vertices"));

        _modifiche.SpezzaOUnisci(file, cerchio, "Vertices", 3, spezza: false);
        Assert.Equal(prima, Righe(file));
    }

    [Fact]
    public void NelleMvaDiAccNonSiSpezzaEIlLabDicePerche()
    {
        var file = Apri("ENRMVA/lirr.mva");
        int zona = Primo<MvaSector>(file, m => m.Vertices.Count > 3);

        var esito = _modifiche.SpezzaOUnisci(file, zona, "Vertices", 1, spezza: true);

        Assert.Null(Interruzioni.Forma(file, zona, "Vertices"));
        Assert.Contains("DUMMY", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
    }

    // --- sul file com'è adesso ------------------------------------------------------------------------------

    [Fact]
    public void SiSpezzaAncheUnRecordGiaToccato()
    {
        // Un punto aggiunto prima sposta le righe: il gesto le cerca nel file com'è adesso, non su quello del disco.
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);
        _modifiche.AggiungiVertice(file, l613, "FixLabels", 0, "ABBOZ");

        Assert.IsType<ModificaDelTesto>(_modifiche.SpezzaOUnisci(file, l613, "FixLabels", 1, spezza: true));

        var righe = Righe(file);
        int abboz = righe.ToList().IndexOf("T;L613;ABBOZ;ABBOZ;");
        Assert.Equal(["T;L613;ABBOZ;ABBOZ;", "T;L613;PAPIZ;PAPIZ;", "T;BREAK;PAPIZ;PAPIZ;", "T;L613;GONOT;GONOT;"], righe.Skip(abboz).Take(4));
    }
}
