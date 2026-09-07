using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// <b>La patch che si consegna deve dire quel che il modulo chiede oggi.</b>
///
/// <para><c>docs/guide/ivao-it-wiring.patch</c> non è un documento fra gli altri: è <b>l'artefatto</b> che il
/// sito ospitante applica con <c>git am</c>. Fino al 7 settembre 2026 era ferma al 1° agosto e le mancava
/// <c>app.RunVipiStartupMaintenance()</c> — cinque passi d'avvio che non giravano: promozioni a mano
/// ignorate, settori non proiettati, release non riempite. E il guasto non si vedeva, perché la rete che
/// manda <c>/vsop/health</c> in Degraded scatta solo se un passo <i>gira e fallisce</i>: se non viene
/// chiamato, non fallisce (revisione del 6 settembre 2026, R-007).</para>
///
/// <para>⚠️ Il difetto vero non era la patch ferma — era che l'elenco di ciò che in lei era scaduto fosse
/// <b>incompleto</b>, perché è quell'elenco che qualcuno usa per correggerla. Da qui in avanti l'elenco è
/// questo test: se il modulo cambia un passo d'avvio o un prefisso, la patch diventa rossa.</para>
/// </summary>
public class PatchDiConsegnaTests
{
    private static string Patch() => File.ReadAllText(
        Path.Combine(Radice(), "docs", "guide", "ivao-it-wiring.patch"));

    /// <summary>
    /// Le cinque chiamate d'aggancio, nell'ordine in cui il nostro host le fa (<c>VipiStartup</c>) e in cui
    /// <c>integration.md</c> le documenta.
    /// </summary>
    [Theory]
    [InlineData("builder.Services.AddVipiModule(")]
    [InlineData("app.MigrateVipiDatabase();")]
    [InlineData("app.RunVipiStartupMaintenance();")]
    [InlineData("app.UseVipiModule();")]
    [InlineData("app.MapVipiModule();")]
    public void La_patch_aggancia_tutte_le_chiamate_del_modulo(string chiamata) =>
        Assert.Contains(chiamata, Patch());

    /// <summary>
    /// ⚠️ <b>Il DB si migra PRIMA delle manutenzioni</b>, e le manutenzioni prima che il sito serva pagine:
    /// invertirli vorrebbe dire proiettare settori su uno schema che non c'è ancora.
    /// </summary>
    [Fact]
    public void Le_manutenzioni_stanno_dopo_la_migrazione_e_prima_degli_endpoint()
    {
        var testo = Patch();

        var migrazione = testo.IndexOf("app.MigrateVipiDatabase();", StringComparison.Ordinal);
        var manutenzioni = testo.IndexOf("app.RunVipiStartupMaintenance();", StringComparison.Ordinal);
        var endpoint = testo.IndexOf("app.MapVipiModule();", StringComparison.Ordinal);

        Assert.True(migrazione < manutenzioni, "le manutenzioni d'avvio vanno DOPO la migrazione del DB");
        Assert.True(manutenzioni < endpoint, "le manutenzioni d'avvio vanno PRIMA di mappare gli endpoint");
    }

    /// <summary>
    /// Il prefisso delle pagine è <c>/services/vsop</c> dal 22 agosto 2026. ⚠️ Gli endpoint MACCHINA no:
    /// <c>/vsop/health</c>, <c>/vsop/live/atc</c> e i loro fratelli non si sono spostati e non si spostano —
    /// li conoscono i monitor e i binari del bridge Aurora già distribuiti.
    /// </summary>
    [Fact]
    public void La_patch_non_manda_le_pagine_al_prefisso_di_prima()
    {
        var macchina = new[] { "/vsop/health", "/vsop/live/atc", "/vsop/media", "/vsop/api", "/vsop/files" };

        var pagine = Patch()
            .Split('\n')
            .Select((riga, n) => (Riga: riga.Trim(), Numero: n + 1))
            .Where(x => x.Riga.Contains("/vsop", StringComparison.Ordinal)
                        && !x.Riga.Contains("/services/vsop", StringComparison.Ordinal)
                        && !macchina.Any(m => x.Riga.Contains(m, StringComparison.Ordinal)))
            .Select(x => $"riga {x.Numero}: {x.Riga}")
            .ToList();

        Assert.True(pagine.Count == 0,
            "La patch di consegna nomina ancora il prefisso delle pagine di prima del 22 agosto 2026:\n  " +
            string.Join("\n  ", pagine) +
            "\n\nLe pagine stanno sotto `/services/vsop`; solo gli endpoint macchina restano sotto `/vsop`.");
    }

    /// <summary>
    /// Leaflet e three.js sono vendorizzati dall'11 e dal 22 agosto 2026: la patch non deve più mandare
    /// l'host su un CDN — l'SRI copriva la manomissione, non l'irraggiungibilità.
    /// </summary>
    [Fact]
    public void La_patch_non_manda_piu_su_un_CDN_esterno() =>
        Assert.DoesNotContain("unpkg.com", Patch(), StringComparison.OrdinalIgnoreCase);

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "guide", "ivao-it-wiring.patch")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"docs/guide/ivao-it-wiring.patch non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
