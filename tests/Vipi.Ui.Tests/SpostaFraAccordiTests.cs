using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// «Sposta» nei Trasferimenti: una sezione o delle clausole vanno nell'accordo di un'altra coppia di enti. La
/// logica sta nel repository ed è provata lì (<c>AgreementMoveTests</c>); qui si presidia come la pagina la
/// chiama. Carta <c>docs/feature/2026-10-04-copertura-unica.md</c> §8.
///
/// <para>⚠️ Presidio sul sorgente, come <see cref="FilaDeiTrasferimentiTests"/> e per la stessa ragione: la
/// pagina ha quattordici servizi. Guarda le due cose che romperebbero senza un errore — una scrittura fuori
/// dalla fila, e un annulla che «risposta all'indietro» invece di rimettere i posti di prima.</para>
/// </summary>
public sealed class SpostaFraAccordiTests
{
    private static readonly string Testo = File.ReadAllText(Path.Combine(Radice(), "Pages", "AdminTrasferimentiPage.razor"));

    [Theory]
    [InlineData("private async Task MoveSection(", "Agreements.MoveSectionAsync(")]
    [InlineData("private async Task MoveBulk(", "Agreements.MoveClausesAsync(")]
    public void Lo_spostamento_passa_da_Guarded_cioe_dalla_fila_e_dal_lock(string metodo, string chiamata)
    {
        var corpo = Corpo(metodo);

        Assert.Contains("await Guarded(", corpo);
        Assert.Contains(chiamata, corpo);
    }

    [Fact]
    public void L_annulla_rimette_i_posti_di_prima_e_non_risposta_all_indietro()
    {
        // Rispostando, sezione e clausole finirebbero in coda — e l'ordine è quello in cui il documento le stampa.
        var corpo = Corpo("private void DopoLoSpostamento(");

        Assert.Contains("Agreements.UndoMoveAsync(", corpo);
        Assert.DoesNotContain("MoveSectionAsync", corpo);
        Assert.DoesNotContain("MoveClausesAsync", corpo);
    }

    [Fact]
    public void Niente_annulla_se_non_si_e_spostato_niente()
    {
        // «Stanno già lì» non è un'azione: armare un annulla farebbe credere il contrario.
        var corpo = Corpo("private void DopoLoSpostamento(");
        var niente = corpo.IndexOf("Xfer_MoveNothing", StringComparison.Ordinal);
        var annulla = corpo.IndexOf("_undo =", StringComparison.Ordinal);

        Assert.True(niente >= 0 && annulla > niente, "Il ramo «niente da spostare» deve uscire prima di armare l'annulla.");
        Assert.Matches(new Regex(@"esito\.SectionId is null\)\s*\{[^}]*return;"), corpo);
    }

    [Fact]
    public void I_due_capi_partono_da_chi_cede_e_chi_riceve_adesso()
    {
        // Di solito si cambia un ente solo: partire da due campi vuoti vorrebbe dire riscrivere anche quello giusto.
        var corpo = Corpo("private void OpenMoveSection(");

        Assert.Contains("ag.Sender(sec.Direction).Callsign", corpo);
        Assert.Contains("ag.Receiver(sec.Direction).Callsign", corpo);
    }

    private static string Corpo(string firma)
    {
        var da = Testo.IndexOf(firma, StringComparison.Ordinal);
        Assert.True(da >= 0, $"Nella pagina non c'è più «{firma}».");
        var graffa = Testo.IndexOf('{', da);
        var livello = 0;
        for (var i = graffa; i < Testo.Length; i++)
        {
            if (Testo[i] == '{') livello++;
            else if (Testo[i] == '}' && --livello == 0) return Testo[da..(i + 1)];
        }
        return Testo[da..];
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Vipi.Ui");
    }
}
