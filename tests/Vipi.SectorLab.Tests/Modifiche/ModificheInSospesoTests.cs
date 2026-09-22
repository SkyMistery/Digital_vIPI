using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Le modifiche in sospeso (carta F3 §2.3 e §8, slice 6): cambiare un campo, vedere il diff vero, annullare.
/// Il criterio del §0 della carta è qui: <b>sposto un fix → una riga tolta e una aggiunta</b>, e nient'altro.
/// </summary>
public sealed class ModificheInSospesoTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri()
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static (FileAperto File, int Indice) Fix(SessioneAperta sessione, string nome)
    {
        var file = sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"];
        var etichette = Ispettore.Etichette(file, null);
        return (file, etichette.Select((e, i) => (e, i)).First(v => v.e == nome).i);
    }

    [Fact]
    public void SpostareUnFixCambiaUnaRigaSola()
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");

        var fatta = _modifiche.Cambia(file, indice, "Position", "N041.00.00.000 E012.00.00.000", "BC404");

        Assert.IsType<ModificaDiCampo>(fatta);
        var diff = _modifiche.DiffDi(file);
        Assert.False(diff.InBlocco);
        // Il criterio della carta: una riga tolta, una aggiunta, e basta.
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
        var pezzo = Assert.Single(diff.Pezzi);
        Assert.Contains(pezzo.Righe, r => r.Segno == SegnoDelDiff.Aggiunta && r.Testo.Contains("N041.00.00.000", StringComparison.Ordinal));
        Assert.Contains(pezzo.Righe, r => r.Segno == SegnoDelDiff.Uguale);
    }

    [Fact]
    public void SenzaModificheIlDiffEVuoto()
    {
        var sessione = Apri();
        var (file, _) = Fix(sessione, "BC404");

        var diff = _modifiche.DiffDi(file);

        Assert.Empty(diff.Pezzi);
        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.FileToccati);
    }

    [Fact]
    public void IlRestoDelFileNonSiMuove()
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");
        var prima = ((IFileConRecord)file).RigheDelFile([]).ToList();

        _modifiche.Cambia(file, indice, "Position", "N041.00.00.000 E012.00.00.000");

        var dopo = ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo)).ToList();
        Assert.Equal(prima.Count, dopo.Count);
        Assert.Equal(1, prima.Zip(dopo).Count(c => c.First != c.Second));
    }

    [Fact]
    public void AnnullareRiportaIlFileComeEra()
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");
        var comeEra = ((IFileConRecord)file).RigheDelFile([]).ToList();
        var fatta = (ModificaDiCampo)_modifiche.Cambia(file, indice, "Position", "N041.00.00.000 E012.00.00.000");

        Assert.True(_modifiche.Annulla(file, fatta));

        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.FileToccati);
        Assert.Empty(_modifiche.DiffDi(file).Pezzi);
        Assert.Equal(comeEra, ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo)));
    }

    [Fact]
    public void DueModificheDiFilaEPoiAnnullaTornanoAlValoreDELLAPERTURA()
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");
        var comeEra = ((IFileConRecord)file).RigheDelFile([]).ToList();

        _modifiche.Cambia(file, indice, "Position", "N041.00.00.000 E012.00.00.000");
        var seconda = (ModificaDiCampo)_modifiche.Cambia(file, indice, "Position", "N042.00.00.000 E013.00.00.000");
        _modifiche.Annulla(file, seconda);

        // 🔴 Il «prima» è quello dell'apertura, non il valore intermedio: quello sul disco non è mai esistito.
        Assert.Equal(comeEra, ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo)));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void RimettereAManoIlValoreDiPartenzaNonEUnaModifica()
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");
        string comeEra = Ispettore.Scheda(file, indice, null)!.Campi.Single(c => c.Nome == "Position").Valore;

        _modifiche.Cambia(file, indice, "Position", "N041.00.00.000 E012.00.00.000");
        _modifiche.Cambia(file, indice, "Position", comeEra);

        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.DiffDi(file).Pezzi);
    }

    [Fact]
    public void ModificheInDueFileSiContanoPerFile()
    {
        var sessione = Apri();
        var (fix, indiceFix) = Fix(sessione, "BC404");
        var vor = sessione.File["SectorFiles/Include/IT/NAVAIDS/itvor.vor"];

        _modifiche.Cambia(fix, indiceFix, "Position", "N041.00.00.000 E012.00.00.000");
        _modifiche.Cambia(vor, 0, "Ident", "PROVA");

        Assert.Equal(2, _modifiche.Quante);
        Assert.Equal(2, _modifiche.FileToccati.Count);
        Assert.Equal(1, _modifiche.DiffDi(fix).Aggiunte);
        Assert.Equal(1, _modifiche.DiffDi(vor).Aggiunte);
    }

    [Theory]
    [InlineData("Position", "non è una coordinata", "una coordinata")]
    [InlineData("CampoCheNonEsiste", "qualunque cosa", "non si scrive")]
    public void UnaModificaRifiutataNonToccaNiente(string campo, string valore, string pezzoDelMotivo)
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");
        var comeEra = ((IFileConRecord)file).RigheDelFile([]).ToList();

        var esito = _modifiche.Cambia(file, indice, campo, valore);

        var rifiuto = Assert.IsType<ModificaRifiutata>(esito);
        Assert.Contains(pezzoDelMotivo, rifiuto.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(comeEra, ((IFileConRecord)file).RigheDelFile([]));
    }

    [Fact]
    public void UnaCoordinataSiScriveInQualunqueForma()
    {
        var sessione = Apri();
        var (file, indice) = Fix(sessione, "BC404");

        // Compatta (N041 00 00 000 senza punti): il file la riscriverà nella SUA forma (F2 slice 2), non in questa.
        var fatta = _modifiche.Cambia(file, indice, "Position", "N0410000000 E0120000000");

        Assert.IsType<ModificaDiCampo>(fatta);
        var aggiunta = _modifiche.DiffDi(file).Pezzi.Single().Righe.Single(r => r.Segno == SegnoDelDiff.Aggiunta);
        Assert.Contains("N041.00.00.000", aggiunta.Testo, StringComparison.Ordinal);
    }

    [Fact]
    public void AnnullaTuttoSvuotaIlPannello()
    {
        var sessione = Apri();
        var (fix, indiceFix) = Fix(sessione, "BC404");
        var vor = sessione.File["SectorFiles/Include/IT/NAVAIDS/itvor.vor"];
        _modifiche.Cambia(fix, indiceFix, "Position", "N041.00.00.000 E012.00.00.000");
        _modifiche.Cambia(vor, 0, "Ident", "PROVA");

        _modifiche.AnnullaTutto(f => sessione.File.GetValueOrDefault(f));

        Assert.False(_modifiche.CEQualcosa);
        Assert.Empty(_modifiche.FileToccati);
        Assert.Empty(_modifiche.DiffDi(fix).Pezzi);
        Assert.Empty(_modifiche.DiffDi(vor).Pezzi);
    }
}
