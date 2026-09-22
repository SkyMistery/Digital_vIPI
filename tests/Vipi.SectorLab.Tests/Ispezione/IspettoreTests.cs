using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// L'ispettore in lettura (carta F3 §2.2 passo 3, slice 5): i campi di un record, e le righe del file col numero
/// VERO — quello che si cita a un AOD.
/// </summary>
public sealed class IspettoreTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private (SessioneAperta Sessione, CatalogoDeiPunti Catalogo) Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]);
    }

    private SchedaDelRecord Scheda(string relativo, Func<string, bool> qualeEtichetta)
    {
        var (sessione, catalogo) = Apri();
        var file = sessione.File["SectorFiles/Include/IT/" + relativo];
        var etichette = Ispettore.Etichette(file, catalogo);
        int indice = etichette.Select((e, i) => (e, i)).First(v => qualeEtichetta(v.e)).i;
        return Ispettore.Scheda(file, indice, catalogo)!;
    }

    [Fact]
    public void UnFixHaINomiDeiSuoiCampiEIlValoreInDms()
    {
        var scheda = Scheda("NAVAIDS/APT.fix", e => e == "BC404");

        Assert.Equal("BC404", scheda.Etichetta);
        Assert.Equal("Fix", scheda.Tipo);
        var posizione = scheda.Campi.Single(c => c.Nome == "Position");
        Assert.Matches(@"^[NS]\d{3}\.\d{2}\.\d{2}\.\d{3} [EW]\d{3}\.\d{2}\.\d{2}\.\d{3}$", posizione.Valore);
    }

    [Fact]
    public void LeRigheDelRecordHannoIlNumeroVeroDelFile()
    {
        var (sessione, catalogo) = Apri();
        var file = sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"];
        var scheda = Ispettore.Scheda(file, 0, catalogo)!;

        var sueRighe = scheda.Righe.Where(r => r.DelRecord).ToList();
        Assert.NotEmpty(sueRighe);

        // Il numero è quello del DISCO, non la posizione nell'elenco dei record: questo file comincia con cinque
        // righe di commento, e il primo record sta alla sesta. Si controlla leggendo il file vero.
        string[] daldisco = File.ReadAllLines(_albero.Percorso("SectorFiles/Include/IT/NAVAIDS/APT.fix"));
        int atteso = Array.FindIndex(daldisco, r => r.Contains(sueRighe[0].Testo, StringComparison.Ordinal)) + 1;
        Assert.Equal(atteso, sueRighe[0].Numero);
        Assert.True(atteso > 1, "il campione comincia con dei commenti: se il record fosse alla riga 1 il test non distinguerebbe");
        Assert.Equal(daldisco[sueRighe[0].Numero - 1], sueRighe[0].Testo);
        // I numeri salgono di uno alla volta, contesto compreso.
        Assert.Equal(
            Enumerable.Range(scheda.Righe[0].Numero, scheda.Righe.Count),
            scheda.Righe.Select(r => r.Numero));
    }

    [Fact]
    public void IntornoAlRecordCiSonoLeRigheDiContesto()
    {
        var (sessione, catalogo) = Apri();
        var file = sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"];

        var scheda = Ispettore.Scheda(file, 5, catalogo)!;

        Assert.Contains(scheda.Righe, r => !r.DelRecord && r.Numero < scheda.Righe.First(x => x.DelRecord).Numero);
        Assert.Contains(scheda.Righe, r => !r.DelRecord && r.Numero > scheda.Righe.Last(x => x.DelRecord).Numero);
        Assert.True(scheda.Righe.Count(r => !r.DelRecord) <= 2 * Ispettore.RigheDiContesto);
    }

    [Fact]
    public void UnSettoreDiceQuantiVerticiHaSenzaElencarliTutti()
    {
        var scheda = Scheda("DYNAMIC_SEC/libb_es_ctr.tfl", e => e.StartsWith("LIBB_ES_CTR", StringComparison.Ordinal));

        var vertici = scheda.Campi.Single(c => c.Nome is "Points" or "Punti" or "Vertices");
        // Un elenco lungo si conta, non si stampa riga per riga: per guardarlo c'è la mappa.
        Assert.Contains("voci:", vertici.Valore);
        Assert.NotNull(scheda.Forma);
        Assert.True(scheda.Forma!.Punti > 4);
    }

    [Fact]
    public void UnPuntoScrittoPerNomeSiLeggeColSuoNome()
    {
        var scheda = Scheda("DYNAMIC_SEC/libb_es_ctr.tfl", e => e.StartsWith("LIBB_ES_CTR", StringComparison.Ordinal));

        var vertici = scheda.Campi.Single(c => c.Nome is "Points" or "Punti" or "Vertices");
        Assert.Matches(@"[A-Z]{3,}", vertici.Valore);
    }

    [Fact]
    public void UnFileNonInterpretatoNonHaSchede()
    {
        _albero.Scrivi("SectorFiles/Include/IT/OTHER/prova.txt", "testo\r\n");
        var (sessione, catalogo) = Apri();

        Assert.Null(Ispettore.Scheda(sessione.File["SectorFiles/Include/IT/OTHER/prova.txt"], 0, catalogo));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000)]
    public void UnIndiceCheNonCEDaNulla(int indice)
    {
        var (sessione, catalogo) = Apri();
        Assert.Null(Ispettore.Scheda(sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"], indice, catalogo));
    }

    [Fact]
    public void LeEtichetteDiUnFileSonoUnaPerRecord()
    {
        var (sessione, catalogo) = Apri();
        var file = sessione.File["SectorFiles/Include/IT/OTHER/itfreq.frq"];

        var etichette = Ispettore.Etichette(file, catalogo);

        // I .frq sulla mappa non ci stanno: l'etichetta esce lo stesso, dal modello.
        Assert.Equal(file.Record, etichette.Count);
        Assert.All(etichette, e => Assert.NotEqual("", e));
    }

    [Fact]
    public void UnRecordSenzaNomeSiChiamaColSuoCodiceOColore()
    {
        // 🔴 L'ha trovato la misura sull'albero vero: gli elenchi dicevano «AtcPosition» 201 volte e «Line» 13 560,
        // cioè il nome del TIPO. Una posizione ATC si chiama col suo Code, un segmento .geo col suo colore.
        var (sessione, catalogo) = Apri();

        var posizioni = Ispettore.Etichette(sessione.File["SectorFiles/Include/IT/OTHER/itfreq.frq"], catalogo);
        Assert.DoesNotContain("AtcPosition", posizioni);
        Assert.Contains(posizioni, e => e.Contains('_', StringComparison.Ordinal));

        var segmenti = Ispettore.Etichette(sessione.File["SectorFiles/Include/IT/GEO/liap.geo"], catalogo);
        Assert.Contains("BUILDING", segmenti);
        // ⚠️ Dove il colore è vuoto — è lecito, F2 slice 5 — non c'è niente che nomini il record: resta il nome del
        // tipo, accanto al suo numero. Meglio del nulla, e non è un buco da riempire inventando.
        Assert.Contains("Line", segmenti);
    }
}
