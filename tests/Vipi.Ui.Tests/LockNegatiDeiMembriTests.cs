using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-052 (revisione totale 3): nell'editor unito un membro col lock di un collega veniva riprovato a ogni
/// render, e ogni rifiuto ridisegnava l'ospite — un giro continuo di prese, ricarichi e ridisegni. Un membro
/// negato non si riprova fino a un gesto, e l'ospite lo sa una volta sola.
/// </summary>
public class LockNegatiDeiMembriTests
{
    [Fact]
    public void Un_membro_negato_non_si_riprova_fino_a_un_gesto()
    {
        var negati = new LockNegatiDeiMembri();
        Assert.True(negati.DaProvare(56));

        Assert.True(negati.Negato(56, "Mario Rossi"));
        Assert.False(negati.DaProvare(56));
        Assert.True(negati.DaProvare(86));   // gli altri membri no

        negati.Dimentica();                   // «Modifica», un ricarico, un membro che entra o esce
        Assert.True(negati.DaProvare(56));
    }

    [Fact]
    public void L_ospite_si_avvisa_solo_se_il_rifiuto_e_nuovo()
    {
        var negati = new LockNegatiDeiMembri();

        Assert.True(negati.Negato(56, "Mario Rossi"));
        Assert.False(negati.Negato(56, "Mario Rossi"));   // lo stesso: niente ridisegno
        Assert.True(negati.Negato(56, "Anna Bianchi"));   // è passato a un altro: si dice
    }

    /// <summary>Presidio sul componente: la presa automatica dopo ogni render passa dall'elenco dei negati, e
    /// avvisa l'ospite solo per un rifiuto nuovo. Col sorgente di prima fallisce.</summary>
    [Fact]
    public void L_editor_unito_non_riprova_i_negati_a_ogni_render()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui"))) dir = dir.Parent;
        var testo = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Vipi.Ui", "Components", "Doc", "UnionMembersEditor.razor"));
        var inizio = testo.IndexOf("private async Task AssicuraLockAsync()", StringComparison.Ordinal);
        var corpo = testo[inizio..testo.IndexOf("\n    }", inizio, StringComparison.Ordinal)];

        Assert.Contains("_negati.DaProvare(", corpo);
        Assert.Contains("if (_negati.Negato(e.DocumentId, chi)) await LockNegato.InvokeAsync(chi);", corpo);
    }

    [Fact]
    public void Preso_il_lock_il_membro_esce_dall_elenco()
    {
        var negati = new LockNegatiDeiMembri();
        negati.Negato(56, "Mario Rossi");

        negati.Preso(56);

        Assert.True(negati.DaProvare(56));
        Assert.True(negati.Negato(56, "Mario Rossi"));    // un rifiuto dopo una presa è di nuovo notizia
    }
}
