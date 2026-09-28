using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete sulla pagina dello swapper. Il motore ha i suoi test — e sono quelli veri, sui 26 profili reali —
/// quindi qui si guarda solo ciò che il motore non può sapere: che la pagina si apra senza un profilo
/// caricato (è lo stato in cui la trova chiunque ci arrivi), che il filo di Arianna punti all'hub e non
/// alla documentazione, e che la frase sulla privacy sia quella giusta.
///
/// <para>Quest'ultima non è pignoleria: fuori di qui lo stesso strumento è WebAssembly e promette che i
/// file non lasciano il browser. Copiare quella frase dentro un'applicazione Blazor Server significherebbe
/// scrivere in pagina una cosa falsa, ed è l'unico difetto di questo trasloco che nessun compilatore
/// potrebbe vedere.</para>
/// </summary>
public class ProfileSwapperPageTests : TestContext
{
    /// <summary>Localizer che rende la chiave: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private IRenderedComponent<ProfileSwapperPage> Render()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        // La briciola di pane legge le stringhe in INGLESE FISSO (regole-lingua R3): senza questo
        // servizio la pagina non si costruisce nemmeno.
        Services.AddSingleton(new EnglishStrings());
        return RenderComponent<ProfileSwapperPage>();
    }

    [Fact]
    public void Si_apre_con_le_due_aree_di_caricamento_e_nientaltro()
    {
        var cut = Render();

        // Le due aree di trascinamento ci sono entrambe: sorgente e destinazioni.
        Assert.Equal(2, cut.FindAll("label.swap-drop").Count);

        // Senza profili non si mostra né l'elenco delle sezioni né il tasto che copia: non ci sarebbe
        // nulla da copiare, e un tasto spento su una pagina vuota è solo una domanda senza risposta.
        Assert.Empty(cut.FindAll(".swap-grid"));
        Assert.Empty(cut.FindAll("button.btn.primary"));
    }

    [Fact]
    public void Il_filo_di_arianna_porta_ai_servizi_non_alla_documentazione()
    {
        var cut = Render();
        var link = cut.Find(".breadcrumb a");

        // /services, non /services/vsop: lo swapper è un servizio pari grado della documentazione,
        // non una sua sottopagina. È l'intera ragione della forma delle URL scelta in questo giro.
        Assert.Equal("/services", link.GetAttribute("href"));
    }

    [Fact]
    public void Dichiara_che_i_profili_passano_dal_server()
    {
        var cut = Render();
        Assert.Contains("Swap_Privacy", cut.Markup);
    }

    /// <summary>
    /// Il taglio dell'anteprima esiste perché su un circuito Server ogni riga di diff è markup che passa
    /// dalla rete. Qui si verifica il diff stesso, che è la parte che produce quelle righe: due sezioni
    /// diverse devono dare aggiunte e rimozioni, due identiche nessuna.
    /// </summary>
    [Fact]
    public void Il_diff_distingue_aggiunte_rimozioni_e_uguali()
    {
        var prima = new[] { "[X]\r\n", "A=1\r\n", "B=2\r\n" };
        var dopo = new[] { "[X]\r\n", "A=9\r\n", "B=2\r\n" };

        var righe = LineDiff.Diff(prima, dopo);

        Assert.Contains(righe, r => r.Kind == DiffKind.Removed && r.Text == "A=1");
        Assert.Contains(righe, r => r.Kind == DiffKind.Added && r.Text == "A=9");
        Assert.Contains(righe, r => r.Kind == DiffKind.Equal && r.Text == "B=2");

        // Identiche: nessuna riga marcata. È il caso che in pagina diventa «identica — nessuna modifica».
        Assert.All(LineDiff.Diff(prima, prima), r => Assert.Equal(DiffKind.Equal, r.Kind));
    }

    // ---- U-001 (revisione totale 3): la pagina è anonima, quindi niente in lei cresce senza un tetto ----

    private static string[] Righe(string prefisso, int quante) =>
        Enumerable.Range(0, quante).Select(i => $"{prefisso}{i}\r\n").ToArray();

    /// <summary>
    /// 🔴 U-001: il diff era una tabella LCS piena, n·m interi. Due sezioni da 5.000 righe diverse allocavano
    /// 100 MB a ogni render, per ogni destinazione: con dieci file costruiti apposta un anonimo metteva in
    /// ginocchio l'unico processo del sito. La prova è quella proposta dal registro.
    /// </summary>
    [Fact]
    public void LineDiff_non_cresce_col_quadrato_delle_righe()
    {
        var a = Righe("x", 5_000);
        var b = Righe("y", 5_000);
        LineDiff.Diff(Righe("w", 10), Righe("z", 10));   // JIT fuori dalla misura

        var prima = GC.GetAllocatedBytesForCurrentThread();
        var righe = LineDiff.Diff(a, b);
        var allocati = GC.GetAllocatedBytesForCurrentThread() - prima;

        Assert.True(allocati < 10 * 1024 * 1024, $"LineDiff ha allocato {allocati / (1024 * 1024)} MB per 5.000×5.000 righe");
        Assert.Equal(10_000, righe.Count);
    }

    /// <summary>
    /// Qualunque strada prenda (tabella piccola o blocco sostituito), il diff resta un diff: le righe uguali e
    /// tolte ridanno la sezione di partenza, le uguali e aggiunte quella d'arrivo, nell'ordine.
    /// </summary>
    [Theory]
    [InlineData(3, 3)]
    [InlineData(40, 60)]
    [InlineData(2_000, 2_000)]
    public void Il_diff_ricostruisce_le_due_sezioni(int n, int m)
    {
        var comuneInTesta = Righe("t", 5);
        var comuneInCoda = Righe("c", 5);
        var da = comuneInTesta.Concat(Righe("a", n)).Concat(Righe("m", 3)).Concat(comuneInCoda).ToArray();
        var a = comuneInTesta.Concat(Righe("b", m)).Concat(Righe("m", 3)).Concat(comuneInCoda).ToArray();

        var righe = LineDiff.Diff(da, a);

        Assert.Equal(da.Select(r => r.TrimEnd('\r', '\n')),
            righe.Where(r => r.Kind != DiffKind.Added).Select(r => r.Text));
        Assert.Equal(a.Select(r => r.TrimEnd('\r', '\n')),
            righe.Where(r => r.Kind != DiffKind.Removed).Select(r => r.Text));
        // Testa e coda comuni restano uguali qualunque sia la taglia del mezzo.
        Assert.All(righe.Take(5), r => Assert.Equal(DiffKind.Equal, r.Kind));
        Assert.All(righe.TakeLast(5), r => Assert.Equal(DiffKind.Equal, r.Kind));
    }

    private static InputFileContent Profilo(string nome, int righe = 3) =>
        InputFileContent.CreateFromText("[A]\r\n" + string.Concat(Righe("r", righe)), nome);

    [Fact]
    public void Le_destinazioni_hanno_un_tetto_e_la_pagina_dice_perche()
    {
        var cut = Render();
        var dest = cut.FindComponents<InputFile>().Last();

        dest.UploadFiles(Enumerable.Range(1, 8).Select(i => Profilo($"d{i}.cpr")).ToArray());
        cut.FindComponents<InputFile>().Last()
            .UploadFiles(Enumerable.Range(9, 4).Select(i => Profilo($"d{i}.cpr")).ToArray());

        Assert.Equal(10, cut.FindAll("ul .swap-file").Count);
        Assert.Contains("Swap_ErrTooManyDests", cut.Markup);
    }

    [Fact]
    public void Un_file_oltre_il_tetto_di_byte_non_entra()
    {
        var cut = Render();
        var grosso = InputFileContent.CreateFromText("[A]\r\n" + new string('x', 600 * 1024) + "\r\n", "grosso.cpr");

        cut.FindComponents<InputFile>().Last().UploadFiles(grosso);

        Assert.Empty(cut.FindAll("ul .swap-file"));
        Assert.Contains("Swap_ErrTooBig", cut.Markup);
    }

    /// <summary>
    /// Il tetto sulle righe è separato da quello sui byte: un file di soli «\r» sta in pochi KB e diventa una
    /// riga per carattere (<c>SplitKeepEnds</c> tratta ogni «\r» come fine riga).
    /// </summary>
    [Fact]
    public void Un_file_di_troppe_righe_non_entra_anche_se_e_piccolo()
    {
        var cut = Render();
        var righe = InputFileContent.CreateFromText("[A]\r" + new string('\r', 30_000), "righe.cpr");

        cut.FindComponents<InputFile>().First().UploadFiles(righe);

        Assert.Empty(cut.FindAll(".swap-file-name"));
        Assert.Contains("Swap_ErrTooManyLines", cut.Markup);
    }
}
