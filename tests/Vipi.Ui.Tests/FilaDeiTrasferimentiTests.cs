using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 <b>La pagina dei Trasferimenti legge in fila: un'operazione per volta sul DbContext del circuito.</b>
///
/// <para>Misurato in produzione il 25 settembre 2026 alle 08:28:43 su LICC: aprire una clausola leggeva piste e
/// STAR <b>fuori</b> dalla sentinella <c>_busy</c> di <c>Guarded</c>, mentre la pagina stava ancora leggendo — «A
/// second operation was started» sulle piste e, undici millesimi dopo, sulle STAR. La pagina prende i servizi dal
/// circuito (è nel debito di <see cref="ScopeProprioDellePagineTests"/>): senza lo scope proprio, l'unica porta è
/// la fila, e vale solo se ci passano <b>tutti</b>.</para>
///
/// <para>⚠️ Presidio sul sorgente, non sul comportamento: la pagina ha quattordici servizi e un test che la
/// disegna costerebbe più della correzione. Guarda i due modi in cui la fila si è già rotta — un caricamento del
/// pannello chiamato di fianco, e una scrittura che non ci passa — e con il sorgente di prima fallisce.</para>
/// </summary>
public sealed class FilaDeiTrasferimentiTests
{
    private static readonly string Testo = File.ReadAllText(Path.Combine(Radice(), "Pages", "AdminTrasferimentiPage.razor"));

    [Fact]
    public void Ogni_scrittura_passa_dalla_fila()
    {
        var guarded = Corpo("private async Task<bool> Guarded(");
        Assert.Contains("await InFilaAsync(", guarded);
    }

    /// <summary>Piste e SID/STAR si caricano solo dentro la fila: o da <c>CaricaOpzioniDelPannelloAsync</c>, o
    /// sulla stessa riga di un <c>InFilaAsync</c>.</summary>
    [Fact]
    public void Piste_e_procedure_si_caricano_solo_in_fila()
    {
        // ⚠️ Per POSIZIONE, non per testo: «await LoadRunwaysAsync(sec);» era scritta identica anche nei due
        // gesti che aprivano la clausola, e un confronto sul testo li avrebbe assolti.
        var (da, a) = Estremi("private Task CaricaOpzioniDelPannelloAsync(");
        var fuori = Regex.Matches(Testo, @"^.*await (LoadRunwaysAsync|LoadProceduresAsync)\(.*$", RegexOptions.Multiline)
            .Where(m => m.Index < da || m.Index > a)
            .Where(m => !m.Value.Contains("InFilaAsync("))
            .Select(m => $"  riga {Testo[..m.Index].Count(c => c == '\n') + 1}: {m.Value.Trim()}")
            .ToList();

        Assert.True(fuori.Count == 0,
            "Caricamenti del pannello fuori dalla fila della pagina:\n" + string.Join("\n", fuori) +
            "\n\nSul DbContext del circuito sono «A second operation was started» (LICC, 25 settembre 2026). " +
            "Vanno in `CaricaOpzioniDelPannelloAsync` o dentro `InFilaAsync`.");
    }

    /// <summary>🔴 U-178 (revisione totale 3): «Incolla tabella» scrive le righe in un colpo solo, non una per una.</summary>
    [Fact]
    public void Incolla_tabella_scrive_tutte_le_righe_insieme()
    {
        var corpo = Corpo("private async Task ConfirmPaste(");
        Assert.Contains("AddClausesAsync(", corpo);
        Assert.DoesNotContain("AddClauseAsync(", corpo);
    }

    /// <summary>U-061: le due eliminazioni fotografano anche le sorelle del gruppo, e l'annulla le passa al
    /// ripristino — altrimenti il gruppo sciolto dall'eliminazione non si ricompone.</summary>
    [Theory]
    [InlineData("private async Task DeleteRow(")]
    [InlineData("private async Task DeleteBulk(")]
    public void L_annulla_di_un_eliminazione_rimette_anche_le_sorelle(string firma)
    {
        var corpo = Corpo(firma);
        Assert.Contains("AgreementOutlineRestore.SorelleDi(", corpo);
        Assert.Contains("RestoreClausesAsync(_acc!.Code, snaps, sorelle)", corpo);
    }

    /// <summary>
    /// U-193 (revisione 3): l'annulla di un'eliminazione si armava su <c>_error is null</c>. Ma <c>Guarded</c> esce
    /// SUBITO se la pagina è occupata, prima di toccare <c>_error</c>: un'eliminazione mai fatta armava l'annulla
    /// («0 righe eliminate ↺ Annulla», che reinseriva copie). Ora <c>Guarded</c> dice se ha eseguito e se è andata.
    /// </summary>
    [Theory]
    [InlineData("private async Task DeleteAgreement(")]
    [InlineData("private async Task DeleteSection(")]
    [InlineData("private async Task DeleteRow(")]
    [InlineData("private async Task DeleteBulk(")]
    public void L_annulla_si_arma_solo_se_l_eliminazione_e_avvenuta(string firma)
    {
        Assert.Contains("private async Task<bool> Guarded(", Testo);
        var corpo = Corpo(firma);
        Assert.Contains("if (!await Guarded(", corpo);
        Assert.DoesNotContain("_error is null", corpo);
        Assert.DoesNotContain("_error is not null", corpo);
    }

    /// <summary>U-193: e l'annulla non si consuma prima che il ripristino riesca — se fallisce, resta.</summary>
    [Fact]
    public void Un_ripristino_fallito_lascia_l_annulla()
    {
        var corpo = Corpo("private async Task Undo(");
        Assert.Contains("if (!await Guarded(undo.Restore, undo.Message)) _undo = undo;", corpo);
    }

    /// <summary>
    /// U-176 (revisione 3; scelta del committente del 28-set): «Applica condizione» con i campi vuoti svuotava le
    /// condizioni di tutte le righe scelte, senza conferma né annulla. Ora è spento se non c'è niente da applicare;
    /// svuotare è un gesto suo, «Togli condizione», con la conferma del numero di righe; e tutti e due armano
    /// l'annulla con la condizione di prima di ogni riga.
    /// </summary>
    [Fact]
    public void Applica_condizione_e_spento_a_campi_vuoti_e_togliere_ha_conferma_e_annulla()
    {
        Assert.Contains("disabled=\"@(_busy || !_canEdit || !CondizioneDaApplicare)\"", Testo);
        Assert.Contains("OnConfirm=\"TogliCondizioneBulk\"", Testo);
        foreach (var firma in new[] { "private async Task ApplyBulkCondition(", "private async Task TogliCondizioneBulk(" })
        {
            var corpo = Corpo(firma);
            Assert.Contains("FotografaCondizioni(", corpo);
            Assert.Contains("_undo = ", corpo);
        }
    }

    [Fact]
    public void La_fila_e_rientrante()
    {
        var fila = Corpo("private async Task InFilaAsync(");
        Assert.Contains("if (_inFila.Value)", fila);
    }

    /// <summary>Il corpo di un metodo, dalla firma alla graffa che chiude alla sua stessa rientranza.</summary>
    private static string Corpo(string firma)
    {
        var (da, a) = Estremi(firma);
        return Testo[da..a];
    }

    private static (int Da, int A) Estremi(string firma)
    {
        var inizio = Testo.IndexOf(firma, StringComparison.Ordinal);
        Assert.True(inizio >= 0, $"Metodo non trovato: {firma}");
        var fine = Testo.IndexOf("\n    }", inizio, StringComparison.Ordinal);
        Assert.True(fine > inizio, $"Fine del metodo non trovata: {firma}");
        return (inizio, fine);
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
