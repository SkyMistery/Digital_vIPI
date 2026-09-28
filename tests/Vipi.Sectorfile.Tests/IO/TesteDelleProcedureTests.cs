using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 9b: l'RNAV di SID e voci <c>.str</c> scritto dal modello. Ogni procedura toccata cambia UNA
/// riga, la sua testa, anche con un commento in coda (<c>limc.sid</c>) o col tipo vuoto (<c>lirf.str</c>).
/// </summary>
public sealed class TesteDelleProcedureTests
{
    private readonly CollectingWarnings _warnings = new();

    private static string[] Inverti<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, string testo, string nome)
        where T : class
    {
        string cartella = Path.Combine(Path.GetTempPath(), "teste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cartella);
        string percorso = Path.Combine(cartella, nome);
        string temporaneo = Path.Combine(cartella, "uscita.tmp");
        try
        {
            File.WriteAllText(percorso, testo);
            var letto = lettore.Parse(percorso, new ColorPalette()).FissaLeBasi(scrittore);
            foreach (object r in letto.Records)
            {
                switch (r)
                {
                    case SidProcedure sid: sid.IsRnav = sid.IsRnav is null ? true : null; break;
                    case StrRecord str: str.IsRnav = str.IsRnav is null ? true : null; break;
                }
            }

            new FileSaverOrchestrator().Save(letto, new HashSet<T>(letto.Records), scrittore, temporaneo);
            return File.ReadAllLines(temporaneo);
        }
        finally
        {
            Directory.Delete(cartella, recursive: true);
        }
    }

    [Fact]
    public void UnaSidColCommentoInCodaCambiaUnaRigaSola()
    {
        string[] dopo = Inverti(new SidParser(_warnings), new SidSaver(),
            "LIMC;35L;FAR6B-TOP5U; ; ;0;TOP;1; // S-H-A321-EXXX\r\n" +
            "LIMC;35R;DOG7T-AOS5W; ; ;0;AOSTA;1\r\n" +
            "LIMC;35L;IRK8E-PEP2X; ; ;0;PEPAG;\r\n" +
            "LIMC;35R;MMP8G-AOS5X; ; ;0;AOSTA; //SUPER-HEAVY-A321\r\n", "limc.sid");

        // Il commento in coda resta in coda. Nell'ultima cade nell'8° campo: non è un «no», e l'RNAV va al suo posto.
        Assert.Equal(
            [
                "LIMC;35L;FAR6B-TOP5U; ; ;0;TOP; // S-H-A321-EXXX",
                "LIMC;35R;DOG7T-AOS5W; ; ;0;AOSTA",
                "LIMC;35L;IRK8E-PEP2X; ; ;0;PEPAG;1;",
                "LIMC;35R;MMP8G-AOS5X; ; ;0;AOSTA;1; //SUPER-HEAVY-A321",
            ], dopo);
    }

    [Fact]
    public void UnaVoceStrColTipoVuotoCambiaUnaRigaSola()
    {
        string[] dopo = Inverti(new StrParser(_warnings), new StrSaver(),
            "LIRF;16L:16R;ELKA3A;;;;;1;\r\nELKAP;ELKAP;\r\nLIRF;16L;FOO; ; ;\r\nELKAP;ELKAP;\r\n", "lirf.str");

        Assert.Equal(["LIRF;16L:16R;ELKA3A;;;;", "ELKAP;ELKAP;", "LIRF;16L;FOO; ; ;;;1;", "ELKAP;ELKAP;"], dopo);
    }
}
