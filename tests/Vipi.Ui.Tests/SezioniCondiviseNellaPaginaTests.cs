using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le sezioni condivise nella pagina dei Trasferimenti. La logica è del repository ed è provata lì
/// (<c>AgreementShareTests</c>); qui si presidia come la pagina la chiama. Carta
/// <c>docs/feature/2026-10-06-sezioni-condivise.md</c>.
///
/// <para>⚠️ Presidio sul sorgente, come <see cref="FilaDeiTrasferimentiTests"/> e per la stessa ragione. Da quando
/// una sezione può stare in due accordi, la stessa riga ha <b>due versi</b> e lo stesso id sta <b>sotto due
/// accordi</b>: i quattro modi in cui la pagina può sbagliare senza un errore sono scrivere il verso senza dire
/// in quale accordo, eliminare invece di togliere, cercare un id fra tutti gli accordi senza partire da quello
/// aperto, e usare l'id della clausola come chiave di riga.</para>
/// </summary>
public sealed class SezioniCondiviseNellaPaginaTests
{
    private static readonly string Pagina = Leggi("Pages", "AdminTrasferimentiPage.razor");
    private static readonly string Tabella = Leggi("Components", "App", "XferRowsTable.razor");

    [Fact]
    public void Ogni_scrittura_della_sezione_dice_da_quale_accordo()
    {
        // Il verso mandato è quello della presenza che si sta guardando: senza l'accordo finirebbe sulla sezione di
        // casa, e girerebbe il verso in un ALTRO accordo.
        var chiamate = Regex.Matches(Pagina, @"Agreements\.UpdateSectionAsync\((?<args>(?:[^()]|\((?:[^()]|\([^()]*\))*\))*)\)", RegexOptions.Singleline);

        Assert.Equal(3, chiamate.Count);
        Assert.All(chiamate, c => Assert.Contains("AccordoDi(sec)", c.Groups["args"].Value));
        Assert.Contains("Agreements.CopySectionToReverseAsync(_acc!.Code, sec.Id, AccordoDi(sec))", Pagina);
    }

    [Fact]
    public void Togliere_una_sezione_la_toglie_dall_accordo_che_si_guarda_e_non_la_elimina_alla_cieca()
    {
        var corpo = Corpo(Pagina, "private async Task DeleteSection(");

        Assert.Contains("Agreements.RemoveSectionAsync(_acc!.Code, sec.Id, a.Id)", corpo);
        Assert.DoesNotContain("DeleteSectionAsync", Pagina);
        // Due esiti, due annulla: la presenza se vive ancora altrove, il contenuto se era l'ultima.
        Assert.Contains("Agreements.UndoPresenceAsync(", corpo);
        Assert.Contains("Agreements.RestoreSectionAsync(", corpo);
    }

    [Theory]
    [InlineData("private AgreementRow? AgreementOf(int clauseId)")]
    [InlineData("private AgreementSectionRow? SectionOf(int clauseId)")]
    [InlineData("private AgreementSectionRow? SectionById(int sectionId)")]
    [InlineData("private AgreementRow? CurrentAgreement()")]
    public void Chi_cerca_un_id_fra_gli_accordi_parte_da_quello_aperto(string firma)
    {
        // La stessa clausola sta sotto più accordi: il primo dell'elenco è un altro, con un altro verso.
        var da = Pagina.IndexOf(firma, StringComparison.Ordinal);
        Assert.True(da >= 0, $"Nella pagina non c'è più «{firma}».");
        var espressione = Pagina[da..Pagina.IndexOf(';', da)];

        Assert.Contains("AccordiDaQuelloAperto()", espressione);
        Assert.DoesNotContain("_agreements.", espressione);
    }

    [Fact]
    public void La_sezione_a_fuoco_si_cerca_partendo_dall_accordo_aperto()
    {
        var corpo = Corpo(Pagina, "private (AgreementRow Agreement, AgreementSectionRow Section)? FocusSection()");

        Assert.Contains("foreach (var a in AccordiDaQuelloAperto())", corpo);
    }

    [Fact]
    public void La_riga_di_tabella_ha_per_chiave_accordo_e_clausola()
    {
        // Nella vista a elenco la stessa clausola compare una volta per accordo: con la sola clausola come chiave
        // Blazor rifiuta il render.
        Assert.Contains("@key=\"(r.Agreement.Id, c.Id)\"", Tabella);
        Assert.DoesNotContain("@key=\"c.Id\"", Tabella);
    }

    [Fact]
    public void La_fotografia_di_una_sezione_ricorda_che_era_condivisa()
    {
        // Senza, annullare l'eliminazione di un accordo rimetterebbe una COPIA della sezione condivisa.
        Assert.Contains("SharedSectionId: s.IsShared ? s.Id : null", Pagina);
    }

    [Fact]
    public void Una_sezione_condivisa_lo_dice_sulla_testata_e_nel_pannello_della_clausola()
    {
        // Chi modifica una clausola deve sapere che la sta modificando anche in un altro accordo.
        Assert.Equal(2, Regex.Matches(Pagina, @"class=""pill blue xt-shared""").Count);
    }

    [Fact]
    public void Niente_annulla_se_la_sezione_compariva_gia_la()
    {
        var corpo = Corpo(Pagina, "private async Task ShareSection(");
        var gia = corpo.IndexOf("Xfer_ShareAlready", StringComparison.Ordinal);
        var annulla = corpo.IndexOf("_undo =", StringComparison.Ordinal);

        Assert.True(gia >= 0 && annulla > gia, "Il ramo «compare già» deve uscire prima di armare l'annulla.");
        Assert.Matches(new Regex(@"if \(!esito\.Added\)\s*\{[^}]*return;"), corpo);
    }

    private static string Corpo(string testo, string firma)
    {
        var da = testo.IndexOf(firma, StringComparison.Ordinal);
        Assert.True(da >= 0, $"Nella pagina non c'è più «{firma}».");
        var graffa = testo.IndexOf('{', da);
        var livello = 0;
        for (var i = graffa; i < testo.Length; i++)
        {
            if (testo[i] == '{') livello++;
            else if (testo[i] == '}' && --livello == 0) return testo[da..(i + 1)];
        }
        return testo[da..];
    }

    private static string Leggi(params string[] percorso)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "src", "Vipi.Ui" }.Concat(percorso).ToArray()));
    }
}
