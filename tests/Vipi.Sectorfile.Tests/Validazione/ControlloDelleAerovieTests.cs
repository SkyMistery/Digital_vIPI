using Vipi.Sectorfile.Validazione;
using Xunit;

namespace Vipi.Sectorfile.Validazione.Tests;

/// <summary>
/// Lotto «Subito», slice 14b (carta «file per file» B12): i controlli delle aerovie — l'aerovia senza un'etichetta,
/// l'etichetta che nomina un'aerovia che non c'è (le vecchie «U»), l'etichetta rimasta lontana dalla sua aerovia.
/// </summary>
public sealed class ControlloDelleAerovieTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "aerovie-" + Guid.NewGuid().ToString("N"));

    public ControlloDelleAerovieTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "NAVAIDS"));
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "AIRWAY"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "",
            "[FIXES]", @"F;NAVAIDS\itfix.fix", "[LOW AIRWAY]", @"F;AIRWAY\itawlow.lairway"));
        // Quattro punti in fila sul parallelo 45°, a mezzo grado l'uno dall'altro, e uno a nord.
        Scrivi(@"Include\IT\NAVAIDS\itfix.fix", Righe(
            "AAAAA;N045.00.00.000;E010.00.00.000;0;0;",
            "BBBBB;N045.00.00.000;E010.30.00.000;0;0;",
            "CCCCC;N045.00.00.000;E011.00.00.000;0;0;",
            "DDDDD;N045.00.00.000;E011.30.00.000;0;0;",
            "NNNNN;N046.00.00.000;E010.00.00.000;0;0;"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    private void ScriviLeAerovie(params string[] etichette)
        => Scrivi(@"Include\IT\AIRWAY\itawlow.lairway", Righe([
            "//Airway tracks",
            "T;L1;AAAAA;AAAAA;",
            "T;L1;BBBBB;BBBBB;",
            // Un'interruzione: il pezzo dopo è ancora della L1.
            "T;BREAK;BBBBB;BBBBB;",
            "T;L1;CCCCC;CCCCC;",
            "T;L1;DDDDD;DDDDD;",
            "T;M2;AAAAA;AAAAA;",
            "T;M2;NNNNN;NNNNN;",
            "",
            "//Airway labels",
            .. etichette]));

    [Fact]
    public void UnAeroviaCheNessunaEtichettaNominaEUnAvviso()
    {
        ScriviLeAerovie("L;L1;N045.00.00.000;E010.15.00.000;");

        var p = Assert.Single(Di(Regola.AeroviaSenzaEtichetta));

        Assert.EndsWith("itawlow.lairway", p.File, StringComparison.Ordinal);
        Assert.Equal(7, p.Riga);
        Assert.Equal(Gravita.Avviso, p.Gravita);
        Assert.Contains("M2", p.Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnEtichettaCondivisaNominaTutteLeSueAerovie()
    {
        // L'etichetta unita vale per tutte e due; BREAK non è un'aerovia e non chiede un'etichetta.
        ScriviLeAerovie("L;L1-M2;N045.00.00.000;E010.00.30.000;");

        Assert.Empty(Di(Regola.AeroviaSenzaEtichetta));
        Assert.Empty(Di(Regola.EtichettaDiUnAeroviaAssente));
    }

    [Fact]
    public void UnEtichettaColNomeDiUnAeroviaCheNonCESiSegnalaConLaRigaGiusta()
    {
        ScriviLeAerovie(
            "L;UL1-L1;N045.00.00.000;E010.15.00.000;",
            "L;M2;N045.30.00.000;E010.00.00.000;",
            "L;UZ9;N045.00.00.000;E011.15.00.000;");

        var problemi = Di(Regola.EtichettaDiUnAeroviaAssente);

        // Le etichette cominciano alla riga 11 (8 righe di tracciati, una vuota, il commento).
        Assert.Equal([11, 13], problemi.Select(p => p.Riga));
        Assert.Contains("UL1", problemi[0].Dettaglio, StringComparison.Ordinal);
        // Tolto il nome che non c'è, resta l'aerovia vera; un'etichetta di soli nomi che non ci sono non ha una riga giusta.
        Assert.Equal("L;L1;N045.00.00.000;E010.15.00.000;", problemi[0].Proposta);
        Assert.Null(problemi[1].Proposta);
    }

    [Fact]
    public void UnEtichettaLontanaDallaSuaAeroviaEUnAvviso()
    {
        ScriviLeAerovie(
            // Sulla L1, a metà del primo pezzo e — non a metà — del secondo: vanno bene.
            "L;L1;N045.00.00.000;E010.15.00.000;",
            "L;L1;N045.00.00.000;E011.05.00.000;",
            // Tre miglia a nord della L1: l'aerovia è stata spostata, l'etichetta no.
            "L;L1;N045.03.00.000;E011.15.00.000;",
            // Sul buco fra i due pezzi (il BREAK): lì la L1 non passa.
            "L;L1;N045.00.00.000;E010.45.00.000;",
            "L;M2;N045.30.00.000;E010.00.00.000;");

        var problemi = Di(Regola.EtichettaLontanaDallAerovia);

        Assert.Equal([13, 14], problemi.Select(p => p.Riga));
        Assert.Contains("3", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("L1", problemi[0].Dettaglio, StringComparison.Ordinal);
    }
}
