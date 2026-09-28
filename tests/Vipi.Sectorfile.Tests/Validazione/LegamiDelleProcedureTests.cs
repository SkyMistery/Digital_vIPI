using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 9e (carta «file per file» Q2c): i legami fra le procedure della stessa pista — STAR → attesa
/// di scalo → IAP → GA — e l'avviso per la STAR che finisce dove nessuna IAP della pista passa.
/// </summary>
public sealed class LegamiDelleProcedureTests : IDisposable
{
    private readonly CollectingWarnings _warnings = new();
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "legami-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private IReadOnlyList<StrRecord> Str(string testo)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(testo), "lizz.str").Records;

    // Due STAR della 16L: ELKA3A finisce a ELVAD (c'è l'attesa e c'è l'IAP che parte da lì), GILI3A a GILIO (nessuna IAP
    // ci passa). La GA parte dove finisce l'IAP. La STAR della 34R non ha IAP sulla sua pista: niente avviso.
    private static readonly string[] Lizz =
    [
        "LIZZ;16L:16R;ELKA3A;;;;;1;", "ELKAP;ELKAP;", "ELVAD;ELVAD;",
        "",
        "LIZZ;16L;GILI3A;;;;;1;", "GILIS;GILIS;", "GILIO;GILIO;",
        "",
        "LIZZ;16L;HLD-ELVAD;;;2;", "ELVAD;ELVAD;", "N041.50.00.000;E012.10.00.000;",
        "",
        "LIZZ;16L;ILS16L;;;3;", "ELVAD;ELVAD;", "RF400;RF400;", "MAPT16;MAPT16;",
        "",
        "LIZZ;16L;GA16L;;;5;", "MAPT16;MAPT16;", "OST;OST;",
        "",
        "LIZZ;34R;TOP3B;;;;;1;", "TOP;TOP;",
        "",
        "LIZZ;MAPS;STAR 16L(ALL);;;0;", "ELKAP;ELKAP;", "ELVAD;ELVAD;",
    ];

    [Fact]
    public void LaStarPortaAllAttesaEAllIapELIapAllaGa()
    {
        var voci = Str(Righe(Lizz));

        var legami = LegamiDelleProcedure.Di(voci);

        string Nome(int i) => voci[i].ProcedureId;
        Assert.Equal(
            ["ELKA3A → HLD-ELVAD", "ELKA3A → ILS16L", "HLD-ELVAD → ILS16L", "ILS16L → GA16L"],
            legami.Select(l => $"{Nome(l.Da)} → {Nome(l.A)}"));
        Assert.All(legami, l => Assert.Equal("16L", l.Pista));
    }

    [Fact]
    public void UnaStarCheFinisceDoveNessunaIapPassaEUnAvviso()
    {
        string cartella = Path.Combine(_radice, "Include", "IT");
        Directory.CreateDirectory(cartella);
        File.WriteAllText(Path.Combine(_radice, "ITALY.isc"), Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", ""));
        File.WriteAllText(Path.Combine(cartella, "lizz.str"), Righe(Lizz));

        var avvisi = Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == Regola.StarSenzaAvvicinamento).ToList();

        var gili = Assert.Single(avvisi);
        Assert.Equal((5, Gravita.Avviso), (gili.Riga, gili.Gravita));
        Assert.Contains("GILIO", gili.Dettaglio, StringComparison.Ordinal);
        Assert.Contains("ILS16L", gili.Dettaglio, StringComparison.Ordinal);
    }
}
