using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Presidia una regola che il progetto ha già pagato tre volte: <b>nessuna I/O sul database durante il
/// render</b>.
///
/// <para><see cref="Vipi.Application.Content.IStationResolver"/> è scoped e carica le proprie cache in
/// modo pigro. Leggerlo dal <b>markup</b> di un componente interattivo significa far partire quella query
/// mentre il circuito ne ha un'altra in volo sullo stesso <c>DbContext</c>, e EF risponde
/// «A second operation was started on this context instance» uccidendo il circuito. Il rimedio è una riga:
/// <c>Stations.Prewarm()</c> nel ciclo di vita async, dove il context è libero e sequenziale.</para>
///
/// <para><b>Perché un test sui sorgenti e non su bUnit.</b> Il guasto non è nel componente ma nella
/// combinazione «interattivo + lettura pigra nel render», e riprodurlo richiederebbe di montare pagine
/// admin con otto servizi finti ciascuna. Camminare i <c>.razor</c> costa nulla e copre anche le pagine
/// che nessuno ha ancora scritto.</para>
///
/// <para><b>Perché solo gli interattivi.</b> Il chrome statico (<c>SopLayout</c>) legge lo stesso resolver
/// nel proprio markup ed è corretto così: senza <c>@rendermode</c> è SSR, ha uno scope per richiesta e non
/// condivide alcun context con un circuito. La regola separa i due casi invece di vietare la lettura.</para>
///
/// <para>Storia: <c>AccVipiPage</c>, <c>SopHome</c> e <c>VloaListPage</c> furono sistemate il 29 luglio
/// 2026 su Postgres; <c>AdminTrasferimentiPage</c> e <c>AdminGrantsPage</c> erano rimaste indietro e sono
/// emerse il 9 agosto guidando l'app su MariaDB (voce A6), dove entrambe le pagine non si aprivano affatto.</para>
/// </summary>
public class StationResolverPrewarmTests
{
    private readonly ITestOutputHelper _out;
    public StationResolverPrewarmTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Ogni_componente_interattivo_che_legge_il_resolver_nel_render_lo_scalda_prima()
    {
        var radice = RadiceDelRepo();
        var razor = Directory.GetFiles(Path.Combine(radice, "src", "Vipi.Ui"), "*.razor", SearchOption.AllDirectories);
        Assert.NotEmpty(razor);   // se il percorso cambiasse, il test deve fallire, non passare a vuoto

        var colpevoli = new List<string>();
        foreach (var file in razor)
        {
            var testo = File.ReadAllText(file);

            // Il nome dell'iniezione non è garantito: si legge invece di assumerlo.
            var iniezione = Regex.Match(testo, @"@inject\s+IStationResolver\s+(\w+)");
            if (!iniezione.Success) continue;
            if (!testo.Contains("@rendermode InteractiveServer", StringComparison.Ordinal)) continue;

            var nome = iniezione.Groups[1].Value;
            var markup = MarkupPrimaDelCodice(testo);
            if (!Regex.IsMatch(markup, $@"\b{Regex.Escape(nome)}\.")) continue;

            if (!testo.Contains($"{nome}.Prewarm()", StringComparison.Ordinal))
                colpevoli.Add(Path.GetRelativePath(radice, file));
        }

        foreach (var c in colpevoli) _out.WriteLine(c);

        Assert.True(colpevoli.Count == 0,
            $"{colpevoli.Count} componenti interattivi leggono IStationResolver nel render senza chiamare " +
            "Prewarm() nel ciclo di vita: il lazy-load parte durante il render e il circuito muore con " +
            "«A second operation was started on this context instance».\n  " + string.Join("\n  ", colpevoli));
    }

    /// <summary>
    /// Tutto ciò che precede il primo <c>@code</c>: è la parte che viene valutata a ogni render. Quel che
    /// sta dentro <c>@code</c> gira nel ciclo di vita o negli handler, dove la lettura è legittima.
    /// </summary>
    private static string MarkupPrimaDelCodice(string testo)
    {
        var m = Regex.Match(testo, @"^@code\b", RegexOptions.Multiline);
        return m.Success ? testo[..m.Index] : testo;
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
