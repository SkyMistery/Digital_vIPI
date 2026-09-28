using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 6 («file per file» H3): nei <c>.geo</c> e nei <c>.pol</c> il nome di un gruppo è il commento
/// sopra, e «Percorso/Poligono senza titolo» (Google Earth) è un nome mancante. Un avviso per file, col numero e le righe.
/// </summary>
public sealed class NomeMancanteTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "nome-mancante-" + Guid.NewGuid().ToString("N"));

    public NomeMancanteTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    private string Scrivi(string nome, params string[] righe)
    {
        string file = Path.Combine(_cartella, nome);
        File.WriteAllText(file, string.Join("\r\n", righe) + "\r\n");
        return file;
    }

    private static readonly string[] Geo =
    [
        "//////LIAP//////",
        "//Percorso senza titolo",
        "N042.22.24.448;E013.18.27.278;N042.22.17.909;E013.18.42.313;BUILDING;",
        "",
        "//Percorso senza titolo",
        "//fence",
        "N042.22.17.909;E013.18.42.313;N042.22.18.000;E013.18.43.785;BUILDING;",
        "//Poligono senza titolo",
        "",
        "N042.22.18.000;E013.18.43.785;N042.22.25.901;E013.18.47.673;BUILDING;",
    ];

    [Fact]
    public void ContaIGruppiIlCuiNomeEQuelloDiGoogleEarth()
    {
        // Conta l'ultimo commento prima dei dati: «//fence» dà il nome al secondo gruppo, anche se sopra c'è «senza titolo».
        Assert.Equal([2, 8], Validatore.SenzaTitolo(Geo));
    }

    [Fact]
    public void UnAvvisoPerFileColNumeroELeRighe()
    {
        var problema = Assert.Single(Validatore.ValidaIlFile(Scrivi("liap.geo", Geo), "liap.geo"), p => p.Regola == Regola.NomeMancante);

        Assert.Equal(Gravita.Avviso, problema.Gravita);
        Assert.Equal(2, problema.Riga);
        Assert.StartsWith("2 gruppi col nome di Google Earth", problema.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void NegliAltriFileNonConta()
        => Assert.DoesNotContain(Validatore.ValidaIlFile(Scrivi("prova.fix", "//Percorso senza titolo", "ABBOZ;N041.00.00.000;E012.00.00.000;1;"), "prova.fix"),
            p => p.Regola == Regola.NomeMancante);
}
