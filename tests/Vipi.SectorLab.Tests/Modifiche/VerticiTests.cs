using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// I vertici di una forma (carta F3 §2.3, slice 7): cambia, aggiungi, togli, e «incolla da testo» col convertitore
/// di F1. La prova della carta: un'area incollata da testo AIP <b>con un arco</b>, e il diff che tocca solo i
/// vertici cambiati.
/// </summary>
public sealed class VerticiTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private (FileAperto File, int Indice) Forma()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var file = sessione.File[Settore];
        var etichette = Ispettore.Etichette(file, null);
        return (file, etichette.Select((e, i) => (e, i)).First(v => v.e.StartsWith("LIBB_ES_CTR", StringComparison.Ordinal)).i);
    }

    private static int Quanti(FileAperto file, int indice) => ModificheInSospeso.Vertici(file, indice, "Vertices")!.Count;

    [Fact]
    public void SpostareUnVerticeCambiaUnaRigaSola()
    {
        var (file, indice) = Forma();

        var fatta = _modifiche.CambiaVertice(file, indice, "Vertices", 0, "N041.00.00.000 E012.00.00.000");

        Assert.IsType<ModificaDeiVertici>(fatta);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
    }

    [Fact]
    public void AggiungereUnVerticeAggiungeUnaRigaESENZAToglierne()
    {
        var (file, indice) = Forma();
        int prima = Quanti(file, indice);

        _modifiche.AggiungiVertice(file, indice, "Vertices", 2, "N041.00.00.000 E012.00.00.000");

        Assert.Equal(prima + 1, Quanti(file, indice));
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(0, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
    }

    [Fact]
    public void TogliereUnVerticeToglieUnaRigaSola()
    {
        var (file, indice) = Forma();
        int prima = Quanti(file, indice);

        _modifiche.TogliVertice(file, indice, "Vertices", 1);

        Assert.Equal(prima - 1, Quanti(file, indice));
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(0, diff.Aggiunte);
    }

    [Fact]
    public void UnVerticeSiPuoScrivereANCHEPerNomeDoveIlFileLoAmmette()
    {
        var (file, indice) = Forma();

        // Nei .tfl un vertice per nome è normale (AMSOR;AMSOR;): il file lo sa riscrivere.
        var fatta = _modifiche.CambiaVertice(file, indice, "Vertices", 0, "AMSOR");

        Assert.IsType<ModificaDeiVertici>(fatta);
        var aggiunta = _modifiche.DiffDi(file).Pezzi.Single().Righe.Single(r => r.Segno == SegnoDelDiff.Aggiunta);
        Assert.Contains("AMSOR", aggiunta.Testo, StringComparison.Ordinal);
    }

    [Fact]
    public void DoveIlFileNonSaScrivereINomiIlNomeSiRifiuta()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        // Un .pol tiene i vertici come coordinate e basta: un nome lì non si potrebbe riscrivere.
        var pol = sessione.File["SectorFiles/Include/IT/GND_LAYOUT/br_ad_gnd.pol"];

        var esito = _modifiche.CambiaVertice(pol, 0, "Vertices", 0, "AMSOR");

        var rifiuto = Assert.IsType<ModificaRifiutata>(esito);
        Assert.Contains("coordinate", rifiuto.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AncheDoveIVerticiSonoSOLOCoordinateSiSpostano()
    {
        // 🔴 Trovato dalla misura sull'albero: `Punto` ha una conversione implicita da `Coordinate`, e il ternario
        // che sceglieva il tipo faceva uscire un Punto ANCHE per gli elenchi di Coordinate — eccezione a tempo
        // d'esecuzione su tutti i .pol e gli .lairway. Qui si spostano davvero.
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var pol = sessione.File["SectorFiles/Include/IT/GND_LAYOUT/br_ad_gnd.pol"];

        var fatta = _modifiche.CambiaVertice(pol, 0, "Vertices", 0, "N041.00.00.000 E012.00.00.000");

        Assert.IsType<ModificaDeiVertici>(fatta);
        var diff = _modifiche.DiffDi(pol);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
    }

    [Fact]
    public void LUltimoVerticeNonSiToglie()
    {
        var (file, indice) = Forma();
        var elenco = ModificheInSospeso.Vertici(file, indice, "Vertices")!;
        while (elenco.Count > 1)
            elenco.RemoveAt(elenco.Count - 1);

        var esito = _modifiche.TogliVertice(file, indice, "Vertices", 0);

        Assert.IsType<ModificaRifiutata>(esito);
        Assert.Single(elenco);
    }

    [Fact]
    public void UnCampoCheNonEUnElencoDiVerticiSiRifiuta()
    {
        var (file, indice) = Forma();

        var esito = _modifiche.CambiaVertice(file, indice, "SectorCode", 0, "N041.00.00.000 E012.00.00.000");

        Assert.Contains("elenco di vertici", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnAreaDiTestoAipConUnArcoDiventaLElencoDeiVertici()
    {
        var (file, indice) = Forma();

        // Il testo dell'AIP con un ARCO, nella forma vera: è l'esempio del committente della carta F1 (§0).
        var fatta = _modifiche.IncollaVertici(file, indice, "Vertices",
            "44°51'24\" N 008°14'57\" E\n" +
            "then arc of circle in clockwise direction radius 17 NM centred on\n" +
            "44°55'29\" N 007°51'43\" E\n" +
            "till point\n" +
            "44°41'08\" N 008°04'34\" E",
            puntiPerGrado: 2.0);

        var modifica = Assert.IsType<ModificaDeiVertici>(fatta);
        Assert.Equal("vertici incollati", modifica.Cosa);
        // L'arco porta più punti dei tre scritti: è lui che li genera.
        Assert.True(modifica.Dopo > 4, $"vertici dopo: {modifica.Dopo}");
        Assert.Equal(modifica.Dopo, Quanti(file, indice));
        Assert.True(_modifiche.DiffDi(file).Aggiunte > 0);
    }

    [Fact]
    public void UnTestoConDueAreeSiRifiutaEDiceQuante()
    {
        var (file, indice) = Forma();
        int prima = Quanti(file, indice);

        var esito = _modifiche.IncollaVertici(file, indice, "Vertices", """
            N041.00.00.000 E012.00.00.000
            N041.10.00.000 E012.10.00.000
            N041.20.00.000 E012.00.00.000

            N042.00.00.000 E013.00.00.000
            N042.10.00.000 E013.10.00.000
            N042.20.00.000 E013.00.00.000
            """);

        Assert.Contains("2 aree", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(prima, Quanti(file, indice));
    }

    [Fact]
    public void UnTestoSenzaCoordinateSiRifiutaENonTocca()
    {
        var (file, indice) = Forma();
        int prima = Quanti(file, indice);

        var esito = _modifiche.IncollaVertici(file, indice, "Vertices", "questo non è un elenco di punti");

        Assert.IsType<ModificaRifiutata>(esito);
        Assert.Equal(prima, Quanti(file, indice));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void AnnullareRimetteLElencoComEraAllApertura()
    {
        var (file, indice) = Forma();
        var comEra = ModificheInSospeso.Vertici(file, indice, "Vertices")!.Cast<object>().ToList();
        var righeComErano = ((IFileConRecord)file).RigheDelFile([]).ToList();

        _modifiche.TogliVertice(file, indice, "Vertices", 1);
        _modifiche.CambiaVertice(file, indice, "Vertices", 0, "N041.00.00.000 E012.00.00.000");
        var ultima = (ModificaDeiVertici)_modifiche.AggiungiVertice(file, indice, "Vertices", 0, "N042.00.00.000 E013.00.00.000");

        Assert.True(_modifiche.Annulla(file, ultima));

        // 🔴 Un annulla solo rimette l'elenco DELL'APERTURA, non il gesto di prima: i vertici si annullano insieme.
        Assert.Equal(comEra, ModificheInSospeso.Vertici(file, indice, "Vertices")!.Cast<object>());
        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(righeComErano, ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo)));
    }

    [Fact]
    public void TornareDaSoliAllElencoDiPartenzaNonEUnaModifica()
    {
        var (file, indice) = Forma();
        string primoVertice = Ispettore.Scheda(file, indice, null)!.Campi.Single(c => c.Nome == "Vertices").Valore.Split(',')[0].Trim();

        _modifiche.AggiungiVertice(file, indice, "Vertices", 0, "N041.00.00.000 E012.00.00.000");
        _modifiche.TogliVertice(file, indice, "Vertices", 0);

        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.DiffDi(file).Pezzi);
        Assert.StartsWith(primoVertice.Split(' ')[0], Ispettore.Scheda(file, indice, null)!.Campi.Single(c => c.Nome == "Vertices").Valore, StringComparison.Ordinal);
    }
}
