using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Il nome dal commento, cambiato dalla scheda (lotto «Subito», slice 6b, «file per file» H3): il commento sopra la parte
/// si riscrive tenendo le sue barre, se non c'è si mette, e vuoto si toglie. Una voce nel testo del file.
/// </summary>
public sealed class NomiDelleVociTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private FileAperto Apri(string relativo)
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/" + relativo];

    private IReadOnlyList<string> Righe(FileAperto file)
        => ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));

    private IReadOnlyList<VoceDellaSelezione> Voci(FileAperto file)
        => VociDellaSelezione.Di(file, Righe(file), ((IFileConRecord)file).PostiDeiRecord(_modifiche.SporchiDi(file.Relativo)))!;

    [Fact]
    public void RinominareRiscriveIlCommentoTenendoLeBarre()
    {
        var file = Apri("ACC/FRA.artcc");
        var veken = Voci(file).Single(v => v.Nome == "NPZ").Parti[0];
        string vecchia = Righe(file)[veken.RigaDelNome!.Value - 1];

        var fatta = _modifiche.CambiaIlNome(file, veken.RigaDelNome, veken.PrimaRiga, "VEKEN");

        Assert.Equal("nome «LINPZ1 VEKEN» → «VEKEN»", Assert.IsType<ModificaDelTesto>(fatta).Descrizione);
        Assert.Equal(vecchia.Replace("LINPZ1 VEKEN", "VEKEN", StringComparison.Ordinal), Righe(file)[veken.RigaDelNome.Value - 1]);
        Assert.Equal("VEKEN", Voci(file).Single(v => v.Nome == "NPZ").Parti[0].Nome);
    }

    [Fact]
    public void UnaParteSenzaNomeLoPrendeDaUnCommentoNuovoSopra()
    {
        var file = Apri("ACC/FRA.artcc");
        int record = ((IFileConRecord)file).RecordDelModello.Count;
        var senza = Voci(file).Single(v => v.Nome == "LIMITROFI").Parti.First(p => p.Nome is null);

        Assert.IsType<ModificaDelTesto>(_modifiche.CambiaIlNome(file, null, senza.PrimaRiga, "LIMM-LSAS"));

        Assert.Equal("//LIMM-LSAS", Righe(file)[senza.PrimaRiga]);
        Assert.Equal(record, ((IFileConRecord)file).RecordDelModello.Count);
        var dopo = Voci(file).Single(v => v.Nome == "LIMITROFI").Parti.Single(p => p.Chiave == senza.Chiave);
        Assert.Equal("LIMM-LSAS", dopo.Nome);
    }

    [Fact]
    public void UnGruppoDelGeoCambiaNomeETornaComEra()
    {
        var file = Apri("GEO/liap.geo");
        var fence = Voci(file)[0];
        var prima = Righe(file);

        _modifiche.CambiaIlNome(file, fence.RigaDelNome, fence.PrimaRiga!.Value, "recinzione");
        Assert.Equal("recinzione", Voci(file)[0].Nome);

        var ora = Voci(file)[0];
        _modifiche.CambiaIlNome(file, ora.RigaDelNome, ora.PrimaRiga!.Value, "fence");
        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnNomeVuotoToglieIlCommento()
    {
        var file = Apri("GEO/liap.geo");
        var fence = Voci(file)[0];
        int righe = Righe(file).Count;

        _modifiche.CambiaIlNome(file, fence.RigaDelNome, fence.PrimaRiga!.Value, "");

        Assert.Equal(righe - 1, Righe(file).Count);
        Assert.DoesNotContain("//fence", Righe(file));
    }

    [Theory]
    [InlineData("A;B", "«;»")]
    [InlineData("@tag", "«@»")]
    public void UnNomeCheRomperebbeIlFileSiRifiuta(string nome, string perche)
    {
        var file = Apri("GEO/liap.geo");
        var fence = Voci(file)[0];

        var esito = _modifiche.CambiaIlNome(file, fence.RigaDelNome, fence.PrimaRiga!.Value, nome);

        Assert.Contains(perche, Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
    }
}
