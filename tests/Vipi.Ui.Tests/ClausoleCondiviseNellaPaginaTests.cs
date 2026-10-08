using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le clausole condivise nella pagina dei Trasferimenti. La logica è del repository ed è provata lì
/// (<c>AgreementShareTests</c>); qui si presidia come la pagina la chiama. Carta
/// <c>docs/feature/2026-10-06-sezioni-condivise.md</c> §10.
///
/// <para>⚠️ Presidio sul sorgente, come <see cref="FilaDeiTrasferimentiTests"/> e per la stessa ragione. Da quando
/// una clausola può stare in due accordi, lo stesso id sta <b>sotto due accordi</b>, ed eliminare vuol dire due
/// cose diverse secondo da dove la si guarda. I modi in cui la pagina può sbagliare senza un errore: eliminare
/// senza dire da quale accordo, cercare un id fra tutti gli accordi senza partire da quello aperto, usare l'id
/// della clausola come chiave di riga, fotografare una clausola condivisa come se non lo fosse.</para>
/// </summary>
public sealed class ClausoleCondiviseNellaPaginaTests
{
    private static readonly string Pagina = Leggi("Pages", "AdminTrasferimentiPage.razor");
    private static readonly string Tabella = Leggi("Components", "App", "XferRowsTable.razor");

    [Fact]
    public void Ogni_eliminazione_di_clausole_dice_da_quale_accordo_le_si_guarda()
    {
        // Senza, una clausola condivisa tolta da un accordo sparirebbe anche dagli altri.
        var chiamate = Regex.Matches(Pagina, @"Agreements\.DeleteClausesAsync\((?<args>[^()]*)\)");

        Assert.Equal(2, chiamate.Count);
        Assert.All(chiamate, c => Assert.Equal("_acc!.Code, ids, qui", c.Groups["args"].Value));
        Assert.DoesNotContain("Agreements.DeleteClauseAsync(", Pagina);
    }

    [Fact]
    public void L_accordo_da_cui_si_guarda_e_quello_aperto_e_in_elenco_non_ce_n_e_uno()
    {
        // In elenco la stessa clausola condivisa è due righe con una spunta sola: lì un gesto vale ovunque, e
        // «Stacca» — che vale in UN accordo — è spento.
        Assert.Contains("private int? AccordoGuardato() => _view == XferView.Tree ? _selectedId : null;", Pagina);
        Assert.Contains("Disabled=\"@(_busy || !_canEdit || AccordoGuardato() is null)\"", Pagina);
    }

    [Fact]
    public void La_conferma_dice_dove_resta_o_che_sparisce_da_tutti()
    {
        var corpo = Corpo(Pagina, "private string PromptEliminaRiga(int clauseId)");

        Assert.Contains("Xfer_DeleteRowSharedPrompt", corpo);
        Assert.Contains("Xfer_DeleteRowSharedEverywherePrompt", corpo);
        // La regola è quella del repository: una variante sola di un gruppo condiviso sparisce ovunque.
        Assert.Contains("SiToglieSoloDaQui(", corpo);
        Assert.Contains("Prompt=\"@PromptEliminaScelte()\"", Pagina);
    }

    [Theory]
    [InlineData("private AgreementRow? AgreementOf(int clauseId)")]
    [InlineData("private AgreementSectionRow? SectionOf(int clauseId)")]
    [InlineData("private AgreementSectionRow? SectionById(int sectionId)")]
    [InlineData("private AgreementRow? CurrentAgreement()")]
    public void Chi_cerca_un_id_fra_gli_accordi_parte_da_quello_aperto(string firma)
    {
        // La stessa clausola sta sotto più accordi: il primo dell'elenco è un altro, con un'altra coppia.
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
    public void La_fotografia_di_una_clausola_ricorda_dov_era_condivisa()
    {
        // Senza, annullare «togli da questo accordo» rimetterebbe una COPIA, destinata a divergere dall'originale.
        Assert.Contains(".ConLePresenzeDi(c)", Corpo(Pagina, "private static AgreementClauseSnapshot SnapshotOf(AgreementClauseRow c)", finoA: ';'));
    }

    [Fact]
    public void L_eliminazione_in_blocco_fotografa_ogni_clausola_una_volta_sola()
    {
        // Una clausola condivisa è una riga in ogni accordo che la porta: fotografata due volte, l'annulla la
        // rimetterebbe due volte.
        var corpo = Corpo(Pagina, "private async Task DeleteBulk()");

        Assert.Contains(".GroupBy(x => x.Clausola.Id)", corpo);
    }

    [Fact]
    public void Una_clausola_condivisa_lo_dice_sulla_riga_sulla_testata_e_nel_pannello()
    {
        // Chi corregge una clausola deve sapere che la sta correggendo anche in un altro accordo.
        Assert.Contains("@if (c.IsShared)", Tabella);
        Assert.Single(Regex.Matches(Tabella, @"class=""pill blue xt-shared"""));
        Assert.Equal(2, Regex.Matches(Pagina, @"class=""pill blue xt-shared""").Count);
    }

    [Fact]
    public void Condividere_si_puo_per_sezione_e_per_clausole_scelte()
    {
        Assert.Contains("Agreements.ShareSectionAsync(_acc!.Code, sec.Id, SectorIdOf(da), SectorIdOf(a))", Pagina);
        Assert.Contains("Agreements.ShareClausesAsync(_acc!.Code, ids, SectorIdOf(da), SectorIdOf(a))", Pagina);
    }

    [Fact]
    public void Condividere_si_puo_anche_dalla_riga_e_per_l_accordo_intero()
    {
        // Il gesto sulla riga: il committente nella barra delle scelte non l'aveva trovato (7 ottobre 2026).
        Assert.Contains("OnShare.InvokeAsync(r)", Tabella);
        Assert.Contains("OnShare=\"OpenShareRow\"", Pagina);
        Assert.Contains("Agreements.ShareClausesAsync(_acc!.Code, new[] { c.Id }, SectorIdOf(da), SectorIdOf(a))", Pagina);
        Assert.Contains("Agreements.ShareAgreementAsync(_acc!.Code, ag.Id,", Pagina);
        // Dell'accordo si cambia UN ente: con due, o nessuno, non è «lo stesso accordo con un altro».
        Assert.Contains("if (cambiaA == cambiaB)", Corpo(Pagina, "private async Task ShareAgreement(AgreementRow ag)"));
    }

    [Fact]
    public void Chi_riscrive_una_sezione_ne_riporta_l_ordine_delle_clausole()
    {
        // UpdateSectionAsync riscrive la sezione intera: omesso, l'ordine tornerebbe «a mano» girando il verso,
        // togliendo un aeroporto o salvando il form — senza che nessuno l'abbia chiesto.
        var chiamate = Regex.Matches(Pagina, @"Agreements\.UpdateSectionAsync\(_acc!\.Code, sec\.Id, new AgreementSectionInput\s*\{(?<corpo>[^}]*)\}");

        Assert.Equal(3, chiamate.Count);
        Assert.All(chiamate, c => Assert.Contains("ClauseOrder = ", c.Groups["corpo"].Value));
        Assert.Contains("ClauseOrder = f.ClauseOrder,", Corpo(Pagina, "private static AgreementSectionInput ToSectionInput(SectionForm f)", finoA: ';'));
        Assert.Contains("ClauseOrder = s.ClauseOrder;", Corpo(Pagina, "public void LoadFrom(AgreementSectionRow s)"));
    }

    [Fact]
    public void Con_un_ordine_dichiarato_non_si_riordina_a_mano_e_la_fotografia_porta_il_posto_salvato()
    {
        Assert.Contains("canDrag: _sort == RowSort.Manual && sec.ClauseOrder == AgreementClauseOrder.Manual", Pagina);
        Assert.Equal(2, Regex.Matches(Pagina, @"sec\.ClauseOrder != AgreementClauseOrder\.Manual\)"" title=""@L\[""Xfer_Move(Up|Down)""\]").Count);
        Assert.Contains("c.StoredOrder ?? c.Order", Corpo(Pagina, "private static AgreementClauseSnapshot SnapshotOf(AgreementClauseRow c)", finoA: ';'));
    }

    [Fact]
    public void Niente_annulla_se_le_clausole_comparivano_gia_la()
    {
        var corpo = Corpo(Pagina, "private void DopoLaCondivisione(");
        var gia = corpo.IndexOf("Xfer_ShareAlready", StringComparison.Ordinal);
        var annulla = corpo.IndexOf("_undo =", StringComparison.Ordinal);

        Assert.True(gia >= 0 && annulla > gia, "Il ramo «compaiono già» deve uscire prima di armare l'annulla.");
        Assert.Matches(new Regex(@"if \(!esito\.Added\)\s*\{[^}]*return;"), corpo);
    }

    private static string Corpo(string testo, string firma, char? finoA = null)
    {
        var da = testo.IndexOf(firma, StringComparison.Ordinal);
        Assert.True(da >= 0, $"Nella pagina non c'è più «{firma}».");
        if (finoA is char fine) return testo[da..(testo.IndexOf(fine, da) + 1)];
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
