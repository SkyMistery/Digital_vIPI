using System.Text;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il registro delle disconnessioni viste dai browser (committente, 30 settembre 2026): la riga che nasce dal beacon di
/// vipi-riconnessione.js, i valori che non si accettano, e il riassunto per la pagina Diagnostica.
/// </summary>
public sealed class RegistroDisconnessioniTests : IDisposable
{
    private static readonly DateTime Ora = new(2026, 9, 30, 18, 42, 7, DateTimeKind.Utc);
    private readonly string _cartella = Directory.CreateTempSubdirectory("vipi-disc-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_cartella, recursive: true); } catch { /* best-effort */ }
    }

    private static string? Riga(string json, int pid = 4242, bool autenticato = true) =>
        RegistroDisconnessioni.Riga(Ora, pid, "1.54.0 · abc1234", Encoding.UTF8.GetBytes(json), autenticato);

    [Fact]
    public void Il_beacon_diventa_una_riga_senza_query_e_col_confronto_dei_processi()
    {
        var riga = Riga("""{"p":"/services/vsop/lirr/airports?icao=LIRF","pid":4242,"e":"riagganciata","sp":612,"d":7,"t":2,"v":1,"n":-1,"r":1,"s":1}""");

        Assert.Equal(
            "18:42:07\t4242\t4242\t1\t1.54.0 · abc1234\t/services/vsop/lirr/airports\triagganciata\t612\t7\t2\t1\t-1\t1\t1\t1",
            riga);
        Assert.Equal(RegistroDisconnessioni.Colonne.Split('\t').Length, riga!.Split('\t').Length);
    }

    [Fact]
    public void Un_processo_diverso_si_vede()
    {
        var riga = Riga("""{"p":"/services/vsop/limm/airports","pid":111,"e":"rifiutata","sp":3000,"d":1,"t":1,"v":0,"n":400,"r":1,"s":1}""", pid: 222);
        var c = riga!.Split('\t');
        Assert.Equal(("222", "111", "0", "rifiutata", "0", "400"), (c[1], c[2], c[3], c[6], c[10], c[11]));
    }

    [Theory]
    [InlineData("""{"p":"/services","e":"esploso"}""")]                 // esito sconosciuto
    [InlineData("""{"p":"https://altro.sito/x","e":"fallita"}""")]      // non un percorso del sito
    [InlineData("""{"e":"fallita"}""")]                                 // senza pagina
    [InlineData("""[1,2,3]""")]                                        // non un oggetto
    public void Quel_che_non_ha_la_forma_attesa_non_si_scrive(string json) => Assert.Null(Riga(json));

    [Fact]
    public void I_numeri_fuori_misura_si_stringono()
    {
        var c = Riga("""{"p":"/services","e":"fallita","sp":-5,"d":999999,"t":"tanti"}""")!.Split('\t');
        Assert.Equal(("-", "0", "86400", "-1"), (c[2], c[7], c[8], c[9]));
    }

    [Fact]
    public void Il_riassunto_conta_per_giorno_e_per_pagina()
    {
        var registro = new RegistroDisconnessioni((_, _) => { }, () => _cartella, "x");
        File.WriteAllLines(Path.Combine(_cartella, "disconnessioni-2026-09-30.tsv"), new[]
        {
            "# testa", RegistroDisconnessioni.Colonne,
            Riga("""{"p":"/a","pid":1,"e":"riagganciata","d":4,"v":1,"r":1}""", pid: 1)!,
            Riga("""{"p":"/a","pid":1,"e":"rifiutata","d":10,"v":0,"r":1}""", pid: 2)!,
            Riga("""{"p":"/b","pid":1,"e":"fallita","d":60,"v":1,"r":0}""", pid: 1)!,
        });

        var r = registro.Leggi();
        var g = Assert.Single(r.Giorni);
        Assert.Equal((3, 1, 1, 1, 0), (g.Totale, g.Riagganciate, g.Rifiutate, g.Fallite, g.Abbandonate));
        Assert.Equal((1, 1, 1, 10.0), (g.ProcessoCambiato, g.SchedaNascosta, g.SenzaRete, g.MedianaBuco));
        Assert.Equal(("/a", 2), r.Pagine[0]);
    }

    /// <summary>Il capo browser: lo script manda il beacon proprio a questa rotta, e le pagine di lettura le riconosce.</summary>
    [Fact]
    public void Lo_script_del_browser_parla_a_questa_rotta()
    {
        var js = File.ReadAllText(Path.Combine(Radice(), "src", "Vipi.Ui", "wwwroot", "vipi-riconnessione.js"));
        Assert.Contains("\"" + RegistroDisconnessioni.Rotta.TrimStart('/') + "\"", js);
        Assert.Contains("sendBeacon", js);
        Assert.Contains("data-riconnessione=\"silenziosa\"", js);
        foreach (var esito in RegistroDisconnessioni.Esiti) Assert.Contains("\"" + esito + "\"", js);
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Vipi.slnx non trovato");
    }
}
