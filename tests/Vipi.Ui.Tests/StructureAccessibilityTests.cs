using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Comandi raggiungibili anche senza mouse. Cammina i <c>.razor</c> invece di renderizzare: le pagine che
/// portano questi comandi hanno un grafo di dipendenze grosso (gerarchia, lock, import), e montarle in bUnit
/// costerebbe più di quel che prova. Qui interessa <b>la forma del markup</b>, che è dove sta il difetto.
///
/// <para><b>Perché non è rifinitura.</b> Un <c>&lt;div&gt;</c> o uno <c>&lt;span&gt;</c> con solo
/// <c>@onclick</c> è un comando che esiste unicamente per il mouse: non entra nel giro del tabulatore, non
/// risponde a Invio o Spazio, e uno screen reader lo legge come testo qualsiasi. Nel caso della pagina
/// Struttura si tratta di espandere e richiudere l'albero dei settori: senza, quella pagina non si usa da
/// tastiera.</para>
/// </summary>
public sealed class StructureAccessibilityTests
{
    private readonly ITestOutputHelper _out;

    public StructureAccessibilityTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// Elementi non interattivi (<c>div</c>, <c>span</c>, <c>td</c>, <c>tr</c>, <c>li</c>) con un
    /// <c>@onclick</c> e senza <c>role="button"</c>: ogni occorrenza è un comando che il mouse vede e la
    /// tastiera no.
    ///
    /// <para><b>Eccezioni dichiarate, non silenziose.</b> Le celle <c>sel-cell</c> di AeroportiPage
    /// duplicano per comodità una checkbox che nella stessa riga esiste, è raggiungibile da tastiera e ha un
    /// nome accessibile: dare il fuoco anche alla cella creerebbe tre fermate di tabulazione per un solo
    /// comando, cioè peggio. Stanno in whitelist con questa ragione scritta accanto.</para>
    /// </summary>
    [Fact]
    public void Nessun_comando_raggiungibile_col_solo_mouse()
    {
        // file → frammento di riga tollerato, con il perché.
        var tollerati = new (string File, string Frammento, string Perche)[]
        {
            ("AeroportiPage.razor", "ClickAssigned",
             "duplica la checkbox della riga, che è già raggiungibile e ha aria-label"),
            ("AeroportiPage.razor", "ClickIvao",
             "duplica la checkbox della riga, che è già raggiungibile e ha aria-label"),
            ("DeleteDialog.razor", "del-backdrop",
             "il velo chiude la finestra come il tasto Annulla che le sta dentro, raggiungibile da tastiera: "
             + "dargli il fuoco aggiungerebbe una fermata di tabulazione muta prima del contenuto del dialogo"),
        };

        var radice = RadiceDelRepo();
        var colpevoli = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(radice, "src", "Vipi.Ui"), "*.razor", SearchOption.AllDirectories))
        {
            var nome = Path.GetFileName(file);
            // Chip.razor È la soluzione, non il problema: il suo <span role="button"> è già a posto, e la
            // spiegazione che lo accompagna cita «<span @onclick>» a parole — questo test guarda le righe,
            // non distingue il markup dal commento, e su quel file prenderebbe la prosa per codice.
            if (nome == "Chip.razor") continue;

            foreach (var (riga, numero) in File.ReadLines(file).Select((r, i) => (r, i + 1)))
            {
                if (!Regex.IsMatch(riga, @"<(div|span|td|tr|li)\b[^>]*@onclick")) continue;
                if (riga.Contains("role=\"button\"", StringComparison.Ordinal)) continue;
                if (riga.Contains("@onclick:preventDefault", StringComparison.Ordinal)) continue;   // non è un comando: ferma la propagazione
                if (tollerati.Any(t => nome == t.File && riga.Contains(t.Frammento, StringComparison.Ordinal))) continue;

                colpevoli.Add($"{nome}:{numero}  {riga.Trim()[..Math.Min(110, riga.Trim().Length)]}");
            }
        }

        foreach (var c in colpevoli) _out.WriteLine(c);

        Assert.True(colpevoli.Count == 0,
            $"{colpevoli.Count} comandi raggiungibili col solo mouse: un elemento non interattivo con " +
            "@onclick non entra nel giro del tabulatore, non risponde a Invio o Spazio e uno screen reader " +
            "lo legge come testo. Rimedi: un <button>, oppure role=\"button\" + tabindex + @onkeydown " +
            "(vedi il componente Chip), oppure — se duplica un comando già accessibile — la whitelist qui " +
            "sopra, con la ragione scritta.\n  " + string.Join("\n  ", colpevoli));
    }

    /// <summary>
    /// I due toggle della pagina Struttura dicono anche <b>in che stato sono</b>. Un pulsante che apre e
    /// chiude senza <c>aria-expanded</c> si annuncia come un comando qualsiasi: chi non vede il triangolino
    /// non sa se il ramo è già aperto.
    /// </summary>
    [Fact]
    public void I_toggle_della_struttura_dichiarano_se_sono_aperti()
    {
        var testo = File.ReadAllText(Path.Combine(RadiceDelRepo(), "src", "Vipi.Ui", "Pages", "StrutturaPage.razor"));

        Assert.Contains("class=\"htree-toggle\"", testo);
        Assert.Matches(@"<button[^>]*class=""htree-toggle""[^>]*aria-expanded=", testo.Replace("\r\n", "\n").Replace("\n", " "));
        Assert.Matches(@"class=""acc-grp-h""[^>]*role=""button""", testo.Replace("\r\n", "\n").Replace("\n", " "));
        Assert.Contains("aria-expanded=\"@cardOpen", testo);
    }

    /// <summary>
    /// Le checkbox delle tabelle aeroporti hanno un nome. Senza, uno screen reader annuncia «casella di
    /// controllo, non selezionata» venti volte di fila senza dire di quale aeroporto si tratti — e quelle
    /// caselle comandano una cancellazione in blocco.
    /// </summary>
    [Fact]
    public void Le_checkbox_degli_aeroporti_hanno_un_nome()
    {
        var testo = File.ReadAllText(Path.Combine(RadiceDelRepo(), "src", "Vipi.Ui", "Pages", "AeroportiPage.razor"));

        var senzaNome = Regex.Matches(testo, @"<input[^>]*type=""checkbox""[^>]*>")
            .Select(m => m.Value)
            .Where(m => !m.Contains("aria-label", StringComparison.Ordinal))
            .ToList();

        foreach (var s in senzaNome) _out.WriteLine(s);

        Assert.True(senzaNome.Count == 0,
            $"{senzaNome.Count} checkbox senza nome accessibile in AeroportiPage.\n  " + string.Join("\n  ", senzaNome));
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
