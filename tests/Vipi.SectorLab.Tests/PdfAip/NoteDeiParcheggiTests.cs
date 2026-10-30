using Aip;
using Aip.Pdf;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», §2 D, controllo 3: le note dei parcheggi, le pagine di testo delle carte dei parcheggi di
/// AD 2.24, e lo stand cercato nelle loro righe. Le pagine hanno la forma di quelle del 2611 (LIRF 2-9 in prosa, i punti
/// d'arresto di LIPE stampati sul disegno); i testi sono inventati.
/// </summary>
public sealed class NoteDeiParcheggiTests
{
    private const string File = "LI-AD 2 LIXX - PROVA 2.pdf";

    private static (double, double, string)[] Pagina(int n, params string[] righe) =>
        [.. PdfAipFinto.Testata($"AD 2 LIXX 2 - {n}", "09 JUL 2026"), .. righe.Select((r, i) => (52.0, 60.0 + 14 * i, r))];

    private static readonly string Lunga = string.Join(' ', Enumerable.Repeat("Pilots shall follow the marshaller", 12));

    private static IReadOnlyList<PdfPageText> Pdf() => PdfText.Read(PdfAipFinto.Pagine(
        // 2-1: il disegno della carta, con la legenda (e la parola STAND): fuori.
        Pagina(1, "AIRCRAFT PARKING DOCKING CHART - ICAO", "Bearings are magnetic", "STAND 401"),
        // 2-2: le note in prosa.
        Pagina(2, "AIRCRAFT PARKING DOCKING - REMARKS", "Visual Docking guidance system available at stands: 301, 302,", "401, 402.",
            "(1) Aircraft stands from 101 to 108 are out of ATC operator's sight.", "Max wing-span 36 m", "GND 0600-2200"),
        // 2-3: il seguito, senza titolo ma con tanto testo.
        Pagina(3, Lunga),
        // 2-4: un'altra carta.
        Pagina(4, "AERODROME OBSTACLE CHART", Lunga),
        // 2-5: l'ultima carta dei parcheggi, le sue DESCRIPTION TABLES (fino alla fine della sezione).
        Pagina(5, "DESCRIPTION TABLES", "STAND 12A Max wing-span 24 m")));

    private static readonly IReadOnlyList<IndexedPage> Indice =
        [.. Enumerable.Range(1, 5).Select(i => new IndexedPage(File, i, $"AD 2 LIXX 2-{i}", new DateOnly(2026, 7, 9)))];

    private static Aerodrome Scalo() => new()
    {
        Icao = "LIXX", Name = "PROVA",
        Charts =
        [
            new AerodromeChart("AERODROME CHART ICAO", "AD 2 LIXX 1-9"),
            new AerodromeChart("AIRCRAFT PARKING DOCKING CHART ICAO", "AD 2 LIXX 2-1"),
            new AerodromeChart("AERODROME OBSTACLE CHART ICAO TYPE A", "AD 2 LIXX 2-4"),
            new AerodromeChart("AIRCRAFT PARKING DOCKING CHART: APRON 2 - ICAO", "AD 2 LIXX 2-5"),
        ],
    };

    [Fact]
    public void LePagine_DallaCartaFinoAllaSeguente()
    {
        var pagine = NoteDeiParcheggi.Pagine(Scalo(), Indice);

        Assert.Equal(["AD 2 LIXX 2-1", "AD 2 LIXX 2-2", "AD 2 LIXX 2-3", "AD 2 LIXX 2-5"], pagine.Select(p => p.Pagina.Reference));
        Assert.Equal("AIRCRAFT PARKING DOCKING CHART ICAO", pagine[2].Carta.Title);
    }

    [Fact]
    public void LeNote_TestoESeguito_NonIlDisegno_SiLaTabellaDeiPunti()
    {
        var letti = 0;
        var note = NoteDeiParcheggi.Leggi(Scalo(), Indice, nome =>
        {
            letti++;
            return nome == File ? Pdf() : null;
        });

        Assert.Equal(["AD 2 LIXX 2-2", "AD 2 LIXX 2-3", "AD 2 LIXX 2-5"], note.Select(n => n.Pagina));
        // Il file si legge una volta; la testata e il piè di pagina restano fuori.
        Assert.Equal(1, letti);
        Assert.Equal("AIRCRAFT PARKING DOCKING - REMARKS", note[0].Righe[0]);
        Assert.DoesNotContain(note.SelectMany(n => n.Righe), r => r.Contains("AIP - Italia", StringComparison.Ordinal) || r.StartsWith("AD 2 LIXX", StringComparison.Ordinal));
        Assert.Equal(new DateOnly(2026, 7, 9), note[0].Data);
        Assert.Equal("AIRCRAFT PARKING DOCKING CHART: APRON 2 - ICAO", note[2].Carta);
        // Un PDF che non c'è: niente note, niente eccezioni.
        Assert.Empty(NoteDeiParcheggi.Leggi(Scalo(), Indice, _ => null));
    }

    // LIPE, LIEE, LIPY, LIRP sul 2611: il disegno stampa la tabella dei punti d'arresto degli stand. Con la legenda entra solo
    // con tante coordinate (le soglie di una carta sono poche). Le righe come le dà la lettura: PdfPig coi font standard
    // non scrive il «°».
    [Fact]
    public void UnDisegnoEntraSoloConLaTabellaDeiPunti()
    {
        var carta = new AerodromeChart("AIRCRAFT PARKING DOCKING CHART ICAO", "AD 2 LIXX 2-5");
        var pagina = new IndexedPage(File, 5, "AD 2 LIXX 2-5", null);
        string[] punti = [.. Enumerable.Range(1, 10).Select(i => $"1{i:00}-stop1 44°31'47.16\" 011° 17' 44''")];

        Assert.NotNull(NoteDeiParcheggi.Note(carta, pagina, ["Bearings are magnetic", .. punti], continua: false));
        Assert.Null(NoteDeiParcheggi.Note(carta, pagina, ["Bearings are magnetic", "REMARKS", .. punti[..4]], continua: false));
        // Senza titolo e senza seguito, testo lungo o no, non è una pagina di note.
        Assert.Null(NoteDeiParcheggi.Note(carta, pagina, [Lunga], continua: false));
    }

    [Theory]
    [InlineData("Visual Docking guidance system available at stands: 301, 302,", "302", true)]
    [InlineData("401, 402.", "401", true)]
    [InlineData("(1) Aircraft stands from 101 to 108 are out of ATC operator's sight.", "105", true)]
    [InlineData("(1) Aircraft stands from 101 to 108 are out of ATC operator's sight.", "109", false)]
    [InlineData("Apron 1 - Stands 11-16:", "13", true)]
    [InlineData("Stands da 701 a 711", "705", true)]
    [InlineData("110-stop2 44°31'47.16\" 011°17'44.29\"", "110", true)]
    [InlineData("Max wing-span 36 m", "36", false)]
    [InlineData("GND 0600-2200", "700", false)]
    [InlineData("PCN 85/R/B/W/T", "85", false)]
    [InlineData("Stand 4010 entrance via TWY NZ", "401", false)]
    [InlineData("Stand 12A", "12A", true)]
    [InlineData("qualunque cosa", " ", false)]
    public void LoStandCitato(string riga, string stand, bool cita) => Assert.Equal(cita, NoteDeiParcheggi.CitaLoStand(riga, stand));
}
