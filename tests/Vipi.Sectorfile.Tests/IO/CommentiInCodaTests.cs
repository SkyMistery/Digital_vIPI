using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 2a: i commenti in coda (carta «file per file» §C). Il validatore li segnala (avviso, uno per
/// file), il Lab li sposta sopra la riga, e gli scrittori del motore non ne scrivono mai.
/// </summary>
public sealed class CommentiInCodaTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "commenti-in-coda-" + Guid.NewGuid().ToString("N"));

    public CommentiInCodaTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    [Theory]
    [InlineData("T;BREAK;RIVAM;RIVAM; //discontinuity (creates a break)", 21)]
    [InlineData("T;LIMM;N045.35.12.000;E008.31.14.000;LIMM;//Coast", 42)]
    [InlineData("L;LIMM;N045.28.00.000;E008.32.00.000;25;8;", null)]
    [InlineData("//Coast", null)]
    [InlineData("   //@\"L81\" locked=si", null)]
    [InlineData("//T;LIRR;N041.00.00.000;E012.00.00.000; //disattivata", null)]
    [InlineData("", null)]
    public void IlCommentoInCodaSiTrovaSoloDopoIDati(string riga, int? dove)
        => Assert.Equal(dove, CommentiInCoda.Dove(riga));

    [Fact]
    public void SpostatoSopraIlCommentoLasciaIDatiColSuoPuntoEVirgola()
    {
        // limc.sid: il commento stava nell'8° campo; spostato, la riga finisce col ';' che c'era.
        var (commento, dati) = CommentiInCoda.Separa("  LIMC;35;OSKOR5A;;;;0;OSKOR; //SUPER-HEAVY-A321")!.Value;

        Assert.Equal(("  //SUPER-HEAVY-A321", "  LIMC;35;OSKOR5A;;;;0;OSKOR;"), (commento, dati));
    }

    [Fact]
    public void SiSpostanoTuttiOSoloQuelliScelti()
    {
        string[] righe = ["//aerovie", "T;L81;TOMGI;TOMGI; //primo", "T;L81;GEMVI;GEMVI;", "T;BREAK;GEMVI;GEMVI; //break"];

        Assert.Equal(["//aerovie", "//primo", "T;L81;TOMGI;TOMGI;", "T;L81;GEMVI;GEMVI;", "//break", "T;BREAK;GEMVI;GEMVI;"],
            CommentiInCoda.SpostaSopra(righe));
        Assert.Equal(["//aerovie", "T;L81;TOMGI;TOMGI; //primo", "T;L81;GEMVI;GEMVI;", "//break", "T;BREAK;GEMVI;GEMVI;"],
            CommentiInCoda.SpostaSopra(righe, new HashSet<int> { 4 }));
        Assert.Equal([2, 4], CommentiInCoda.Righe(righe));
    }

    // Un avviso per file, col numero e le righe: 713 avvisi uno per uno annegherebbero il pannello (slice 0).
    [Fact]
    public void IlValidatoreDaUnAvvisoPerFile()
    {
        string file = Scrivi("itawlow.lairway", "T;L81;TOMGI;TOMGI; //primo", "T;L81;GEMVI;GEMVI;", "T;BREAK;GEMVI;GEMVI; //break");

        var problema = Assert.Single(Validatore.ValidaIlFile(file, "itawlow.lairway"), p => p.Regola == Regola.CommentoInCoda);

        Assert.Equal((Gravita.Avviso, 1), (problema.Gravita, problema.Riga));
        Assert.StartsWith("2 commenti in coda (righe 1, 3)", problema.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void GliAtisSonoTestiENonSiGuardano()
        => Assert.DoesNotContain(Validatore.ValidaIlFile(Scrivi("liml.atis", "[INFO]", "RWY $arr // $dep"), "liml.atis"),
            p => p.Regola == Regola.CommentoInCoda);

    // Il Lab non scrive mai un commento in coda: riscrivendo dal modello ogni record di righe che ne hanno uno, nessuno
    // scrittore lo rimette. 🔴 Fino al 27 settembre il lettore delle MVA di scalo prendeva `//FL110` per il 5° campo di
    // una T;, e lo scrittore lo riscriveva due volte (`T; //FL110;N…;E…; //FL110;`): 74 righe in 8 file del fork.
    [Theory]
    [InlineData("lipe.mva", "L;110;N044.13.15.000;E010.53.34.000;110;7;", "T;110;N044.19.36.000;E010.47.48.000; //FL110")]
    [InlineData("limm.mva", "L;LIMM;N045.28.00.000;E008.32.00.000;25;8;", "T;LIMM;N045.35.12.000;E008.31.14.000;LIMM; //Coast")]
    [InlineData("itawlow.lairway", "T;L81;TOMGI;TOMGI;", "T;BREAK;GEMVI;GEMVI; //discontinuity (creates a break)")]
    [InlineData("limc.sid", "LIMC;35;OSKOR5A;;;;0;OSKOR; //SUPER-HEAVY-A321", "N045.00.00.000;E009.00.00.000;")]
    [InlineData("lirr.hartcc", "T;RR NE;N044.23.17.000;E011.07.44.000; //RR CONF2", "T;RR NE;OTNUN;OTNUN;")]
    [InlineData("limmapp.tfl", "LIPX_ES0_APP;APP;1;APP;1; //Brescia", "N045.33.57.000;E010.21.30.000;")]
    [InlineData("FRA.artcc", "L;ABDAB;N037.53.21.000;E010.37.43.000;8; //confine", "T;FRA BDRY;LUSIL;LUSIL; //Svizzera")]
    public void GliScrittoriNonScrivonoCommentiInCoda(string nome, string prima, string seconda)
    {
        string file = Scrivi(nome, prima, seconda, "N045.01.00.000;E009.01.00.000;");
        string cartella = nome.EndsWith(".mva", StringComparison.Ordinal) && nome != "lipe.mva"
            ? Path.Combine(_cartella, "ENRMVA")
            : _cartella;
        if (cartella != _cartella)
        {
            Directory.CreateDirectory(cartella);
            File.Move(file, file = Path.Combine(cartella, nome));
        }

        Assert.True(Formati.Usa(file, new CollectingWarnings(), new DalModello(file), out var righe));

        Assert.NotEmpty(righe);
        Assert.All(righe, r => Assert.Null(CommentiInCoda.Dove(r)));
    }

    private string Scrivi(string nome, params string[] righe)
    {
        string file = Path.Combine(_cartella, nome);
        File.WriteAllText(file, string.Join("\r\n", righe) + "\r\n");
        return file;
    }

    // Ogni record del file riscritto dal suo modello, come lo scrive il Lab quando lo cambia.
    private sealed class DalModello(string percorso) : IUsoDelFormato<IReadOnlyList<string>>
    {
        public IReadOnlyList<string> Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
            => [.. lettore.Parse(percorso, new Shared.ColorPalette()).Records.SelectMany(scrittore.Serialize)];
    }
}
