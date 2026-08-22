using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Ui;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Guardie sugli orari a schermo. Nascono da un guasto reale: diciannove punti dell'interfaccia scrivevano
/// <c>ToLocalTime()</c>, che in Blazor Server converte nel fuso del <b>server</b> — non in quello di chi guarda.
/// L'host di produzione sta a UTC, quindi «lock fino alle 14:32» era già UTC, solo senza dirlo; e su un server
/// con un fuso diverso sarebbe stata l'ora di quella macchina, cioè di nessuno.
/// </summary>
public sealed class VipiTimeTests
{
    private readonly ITestOutputHelper _out;

    public VipiTimeTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// Il caso che rendeva incoerenti i due percorsi del lock: <c>AcquireAsync</c> costruisce la scadenza in
    /// memoria (Kind = Utc), <c>InspectAsync</c> la rilegge dal database, dove Pomelo la restituisce
    /// <c>Unspecified</c>. Con <c>ToLocalTime()</c> il primo sottraeva lo scarto e il secondo lo sommava.
    /// </summary>
    [Fact]
    public void Unspecified_e_Utc_danno_lo_stesso_orario()
    {
        var dalDb = new DateTime(2026, 8, 22, 14, 32, 5, DateTimeKind.Unspecified);
        var inMemoria = new DateTime(2026, 8, 22, 14, 32, 5, DateTimeKind.Utc);

        Assert.Equal("14:32Z", VipiTime.Z(dalDb));
        Assert.Equal(VipiTime.Z(inMemoria), VipiTime.Z(dalDb));
        Assert.Equal(VipiTime.Iso(inMemoria), VipiTime.Iso(dalDb));
    }

    /// <summary>Un <c>Local</c> non dovrebbe arrivarci, ma se arriva va convertito, non ri-etichettato.</summary>
    [Fact]
    public void Local_viene_convertito_in_Utc()
    {
        var locale = new DateTime(2026, 8, 22, 14, 32, 0, DateTimeKind.Local);
        Assert.Equal(locale.ToUniversalTime().ToString("HH:mm", CultureInfo.InvariantCulture) + "Z",
            VipiTime.Z(locale));
    }

    /// <summary>
    /// L'orario non dipende dal fuso della macchina che lo formatta: è la proprietà che <c>ToLocalTime()</c>
    /// non aveva, ed è l'unica ragione per cui i test giravano verdi mentre la pagina mentiva.
    /// </summary>
    [Fact]
    public void Il_fuso_della_macchina_non_entra_nel_risultato()
    {
        var istante = new DateTime(2026, 1, 15, 23, 45, 0, DateTimeKind.Utc);
        Assert.Equal("23:45Z", VipiTime.Z(istante));
        Assert.Equal("23:45:00Z", VipiTime.Zs(istante));
        Assert.Equal("2026-01-15T23:45:00Z", VipiTime.Iso(istante));
        Assert.DoesNotContain(TimeZoneInfo.Local.Id, VipiTime.Iso(istante));
    }

    /// <summary>La data non porta la <c>Z</c>: non è un'ora, e una <c>Z</c> su una data sola confonde e basta.</summary>
    [Fact]
    public void La_data_sola_non_porta_la_Z()
    {
        var d = VipiTime.Day(new DateTime(2026, 8, 22, 23, 30, 0, DateTimeKind.Utc));
        Assert.DoesNotContain("Z", d, StringComparison.Ordinal);
        Assert.Contains("2026", d, StringComparison.Ordinal);
        Assert.StartsWith("22 ", d, StringComparison.Ordinal);   // il giorno è quello UTC, non quello locale
    }

    [Fact]
    public void Istante_assente_niente_attributo()
    {
        Assert.Null(VipiTime.Iso((DateTime?)null));
        Assert.Equal("2026-08-22T14:32:00Z", VipiTime.Iso((DateTime?)new DateTime(2026, 8, 22, 14, 32, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// La regola, non il singolo punto: in interfaccia <c>ToLocalTime()</c> non si usa. Vale per i sorgenti,
    /// non per i commenti — <c>VipiTime</c> spiega proprio perché non si usa, e deve poterlo nominare.
    /// </summary>
    [Fact]
    public void Nessun_ToLocalTime_nei_sorgenti_di_interfaccia()
    {
        var radice = RadiceDelRepo();
        var colpevoli = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(radice, "src", "Vipi.Ui"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var righe = File.ReadAllLines(file);
            for (var i = 0; i < righe.Length; i++)
            {
                if (!righe[i].Contains("ToLocalTime", StringComparison.Ordinal)) continue;
                // Commenti: `///`, `//`, `/* */` C# e `@* *@` Razor. Qui basta riconoscere l'inizio riga.
                var t = righe[i].TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("///", StringComparison.Ordinal) ||
                    t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal) ||
                    t.StartsWith("@*", StringComparison.Ordinal)) continue;
                colpevoli.Add($"{Path.GetRelativePath(radice, file)}:{i + 1}");
            }
        }

        foreach (var c in colpevoli) _out.WriteLine(c);

        Assert.True(colpevoli.Count == 0,
            "ToLocalTime() in interfaccia converte nel fuso del SERVER, che non è l'ora di chi guarda: usa " +
            "VipiTime (UTC col suffisso Z) e lascia a vipi-time.js l'ora locale.\n  " + string.Join("\n  ", colpevoli));
    }

    /// <summary>
    /// Chi marca un elemento con <c>data-utc</c>/<c>data-utc-title</c> deve anche scrivere l'ora UTC:
    /// vipi-time.js <b>aggiunge</b> l'ora locale accanto a quella, non la sostituisce, e una marcatura senza
    /// orario è un attributo che non annota niente.
    ///
    /// <para>Il controllo è per FILE e non per riga: in <c>VersioniPage</c> la pill porta gli attributi e
    /// l'orario lo compone <c>LockBadge</c>/<c>LockTitle</c> venti righe più in basso — chiedere le due cose
    /// sulla stessa riga vieterebbe di estrarre un metodo, che è il contrario di quel che si vuole.</para>
    /// </summary>
    [Fact]
    public void Chi_marca_data_utc_formatta_anche_con_VipiTime()
    {
        var radice = RadiceDelRepo();
        var senzaOrario = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(radice, "src", "Vipi.Ui"), "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var testo = File.ReadAllText(file);
            if (!Regex.IsMatch(testo, @"data-utc(-title)?=""")) continue;
            if (Regex.IsMatch(testo, @"VipiTime\.(Z|Zs|DayZ)\b")) continue;
            senzaOrario.Add(Path.GetRelativePath(radice, file));
        }

        foreach (var s in senzaOrario) _out.WriteLine(s);

        Assert.True(senzaOrario.Count == 0,
            "data-utc senza un orario UTC scritto nello stesso file: vipi-time.js accoda l'ora locale a un " +
            "orario che non c'è.\n  " + string.Join("\n  ", senzaOrario));
    }

    /// <summary>Risale dalla cartella dell'assembly fino alla soluzione: fallisce forte se non la trova.</summary>
    private static string RadiceDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.True(dir is not null, "Vipi.slnx non trovata risalendo da " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}
