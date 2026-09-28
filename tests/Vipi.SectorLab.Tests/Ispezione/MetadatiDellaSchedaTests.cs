using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// I metadati di §M nella scheda (lotto «Subito», slice 3d): le chiavi del catalogo del tipo di file appaiono da sole,
/// si scrivono nel tag sopra il record, e togliendole il file torna com'era.
/// </summary>
public sealed class MetadatiDellaSchedaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private (FileAperto File, int Indice) Record(string relativo, string etichetta)
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var file = sessione.File["SectorFiles/Include/IT/" + relativo];
        return (file, Ispettore.Etichette(file, null).Select((e, i) => (e, i)).First(v => v.e == etichetta).i);
    }

    private IReadOnlyList<string> Righe(FileAperto file) => ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));

    [Fact]
    public void UnaSidMostraLeChiaviDelSuoCatalogo()
    {
        var (file, sid) = Record("lirf.sid", "OST1E");

        var metadati = MetadatiDellaScheda.Di(file, sid);

        Assert.Equal(["locked", "gen", "note", "fix", "trans", "initialclimb", "wtc", "cat", "nav"], metadati.Select(m => m.Chiave));
        Assert.All(metadati, m => Assert.Null(m.Valore));
        Assert.Equal("Salita iniziale", metadati.Single(m => m.Chiave == "initialclimb").Nome);
        Assert.True(metadati.Single(m => m.Chiave == "locked").SiNo);
        // «gen» lo scrive il generatore, non la scheda.
        Assert.False(metadati.Single(m => m.Chiave == "gen").SiScrive);
        Assert.True(metadati.Single(m => m.Chiave == "fix").SiScrive);
    }

    [Fact]
    public void UnaChiaveScrittaFaIlTagSopraIlRecordETogliendolaIlFileTornaUguale()
    {
        var (file, sid) = Record("lirf.sid", "OST1E");
        var prima = Righe(file).ToList();

        var fatta = Assert.IsType<ModificaDelMetadato>(_modifiche.CambiaIlMetadato(file, sid, "initialclimb", "6000ft", "OST1E"));

        Assert.Equal(("—", "6000ft", "initialclimb: — → 6000ft"), (fatta.Prima, fatta.Dopo, fatta.Descrizione));
        var dopo = Righe(file);
        int riga = dopo.ToList().IndexOf("LIRF;07;OST1E;;;;;1;");
        Assert.Contains(dopo.Take(riga), r => r == "//@\"OST1E\" initialclimb=6000ft");
        Assert.Equal("6000ft", MetadatiDellaScheda.Di(file, sid).Single(m => m.Chiave == "initialclimb").Valore);

        // Il valore vuoto toglie la chiave: senza chiavi il tag sparisce, e il file torna quello di prima.
        Assert.IsType<ModificaDelMetadato>(_modifiche.CambiaIlMetadato(file, sid, "initialclimb", "", "OST1E"));
        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnValoreConGliSpaziVaFraVirgoletteESiLeggeSenza()
    {
        var (file, sid) = Record("lirf.sid", "OST1E");

        _modifiche.CambiaIlMetadato(file, sid, "initialclimb", "COO APP");

        Assert.Contains("//@\"OST1E\" initialclimb=\"COO APP\"", Righe(file));
        Assert.Equal("COO APP", MetadatiDellaScheda.Di(file, sid).Single(m => m.Chiave == "initialclimb").Valore);
    }

    [Fact]
    public void DueChiaviStannoNellaStessaDichiarazione()
    {
        var (file, sid) = Record("lirf.sid", "OST1E");

        _modifiche.CambiaIlMetadato(file, sid, "fix", "OSTIA");
        _modifiche.CambiaIlMetadato(file, sid, "wtc", "LMHS");

        Assert.Single(Righe(file), r => r.StartsWith("//@\"OST1E\"", StringComparison.Ordinal));
        Assert.Contains(Righe(file), r => r.StartsWith("//@\"OST1E\"", StringComparison.Ordinal)
                                          && r.Contains("fix=OSTIA", StringComparison.Ordinal) && r.Contains("wtc=LMHS", StringComparison.Ordinal));
        Assert.Equal(2, _modifiche.Tutte.Count(m => m is ModificaDelMetadato));
    }

    [Fact]
    public void AnnullareUnaChiaveLaRimetteComEra()
    {
        var (file, sid) = Record("lirf.sid", "OST1E");
        var prima = Righe(file).ToList();
        var fatta = (Modifica)_modifiche.CambiaIlMetadato(file, sid, "note", "da rivedere");

        Assert.True(_modifiche.Annulla(file, fatta));

        Assert.Equal(prima, Righe(file));
        Assert.Empty(_modifiche.Tutte);
    }

    [Fact]
    public void LaPistaHaLeChiaviPerVersoColSuoNumero()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var file = sessione.File["SectorFiles/Include/IT/OTHER/itrw.rw"];
        int pista = ((IFileConRecord)file).RecordDelModello.Select((r, i) => (r, i))
            .First(v => v.r is Vipi.Sectorfile.Models.Runway { IcaoCode: "LIRF", Designator1: "16L" }).i;

        var metadati = MetadatiDellaScheda.Di(file, pista);

        Assert.Contains(metadati, m => m.Chiave == "width");
        Assert.Contains(metadati, m => m.Chiave == "16L.tora" && m.Nome == "TORA 16L");
        Assert.Contains(metadati, m => m.Chiave == "34R.circuit");
        Assert.IsType<ModificaDelMetadato>(_modifiche.CambiaIlMetadato(file, pista, "16L.tora", "3300"));
        Assert.Contains(Righe(file), r => r == "//@\"LIRF 16L/34R\" 16L.tora=3300");
    }

    [Fact]
    public void UnRecordSenzaNomeFuoriDaUnGruppoColNomeMostraIMetadatiMaNonLiScrive()
    {
        // Dalla slice 6c un segmento .geo scrive i metadati nel blocco del suo gruppo, col nome del commento sopra
        // (BloccoDalCommentoTests). Senza nessun commento il gruppo non ha un nome da dare al blocco: si leggono e basta.
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo", string.Join("\r\n",
            "N042.22.24.448;E013.18.27.278;N042.22.17.909;E013.18.42.313;BUILDING;",
            "N042.22.17.909;E013.18.42.313;N042.22.18.000;E013.18.43.785;BUILDING;",
            ""));
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var file = sessione.File["SectorFiles/Include/IT/GEO/prova.geo"];

        var metadati = MetadatiDellaScheda.Di(file, 0);

        Assert.Contains(metadati, m => m.Chiave == "form");
        Assert.All(metadati, m => Assert.False(m.SiScrive));
        Assert.Contains("blocco", Assert.IsType<ModificaRifiutata>(_modifiche.CambiaIlMetadato(file, 0, "note", "prova")).Motivo,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UnaChiaveFuoriCatalogoSiRifiuta()
    {
        var (file, sid) = Record("lirf.sid", "OST1E");

        var rifiutata = Assert.IsType<ModificaRifiutata>(_modifiche.CambiaIlMetadato(file, sid, "zone", "Torino"));

        Assert.Contains(".sid", rifiutata.Motivo, StringComparison.Ordinal);
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnFileSenzaTagNonHaMetadati()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

        Assert.Empty(MetadatiDellaScheda.Di(sessione.File["SectorFiles/Include/IT/lica.atis"], 0));
    }
}
