using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 17a (carta «file per file» §20, T2): i controlli dei simboli — il nome senza <c>//</c>, il
/// simbolo senza nome, il simbolo che non è 13×13.
/// </summary>
public sealed class ControlloDeiSimboliTests : IDisposable
{
    private const string Pixel = "0000000100000;0000001100000;0000010100000;0000100100000;0001000100000;0010000100000;0100000100000;0010000100000;0001000100000;0000100100000;0000010100000;0000001100000;0000000100000;";

    private readonly string _radice = Path.Combine(Path.GetTempPath(), "sym-" + Guid.NewGuid().ToString("N"));

    public ControlloDeiSimboliTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "", "[SYMBOLS]", "F;symbols.sym"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(params Regola[] regole)
        => Validatore.ValidaLAlbero(_radice).Where(p => regole.Contains(p.Regola)).ToList();

    [Fact]
    public void SimboliColNomeNonDannoAvvisi()
    {
        Scrivi(@"Include\IT\symbols.sym", Righe("//APT", Pixel, "", "////ALTRI", "", "//CROCE X", Pixel));

        Assert.Empty(Di(Regola.NomeDelSimboloSenzaBarre, Regola.SimboloSenzaNome, Regola.RigaIllegibile));
    }

    [Fact]
    public void IlNomeSenzaBarreHaLaRigaCorretta()
    {
        Scrivi(@"Include\IT\symbols.sym", Righe("//APT", Pixel, "", "AC_comb SEL", Pixel));

        var problema = Assert.Single(Di(Regola.NomeDelSimboloSenzaBarre));
        Assert.Equal(4, problema.Riga);
        Assert.Equal("AC_comb SEL", problema.Testo);
        Assert.Equal("//AC_comb SEL", problema.Proposta);
        Assert.Equal(Gravita.Avviso, problema.Gravita);
        Assert.Empty(Di(Regola.SimboloSenzaNome));
    }

    [Fact]
    public void IlSimboloSenzaNomeDiceIlSuoNumero()
    {
        Scrivi(@"Include\IT\symbols.sym", Righe("//APT", Pixel, "", Pixel));

        var problema = Assert.Single(Di(Regola.SimboloSenzaNome));
        Assert.Equal(4, problema.Riga);
        Assert.Contains("2° simbolo", problema.Dettaglio, StringComparison.Ordinal);
        Assert.Null(problema.Proposta);
    }

    [Fact]
    public void UnSimboloCheNonE13Per13ERigaIllegibile()
    {
        Scrivi(@"Include\IT\symbols.sym", Righe("//CORTO", "0000;1111;"));

        var problema = Assert.Single(Di(Regola.RigaIllegibile));
        Assert.Equal(2, problema.Riga);
        Assert.Contains("13×13", problema.Dettaglio, StringComparison.Ordinal);
    }
}
