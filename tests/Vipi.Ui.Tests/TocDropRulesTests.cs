using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Che cosa accetta una voce del menu-sezioni e in quale mossa si traduce il gesto (carta 2026-09-04).
///
/// <para>⚠️ <b>Quel che questi test NON provano</b>: che il trascinamento funzioni davvero. Un gesto del
/// browser si prova col browser che lo fa (<c>Input.setInterceptDrags</c>, headful) — eventi fabbricati a
/// mano saltano la trattativa col browser e hanno già tenuto verdi otto test su un trascinamento rotto. Qui
/// si prova la <b>decisione</b>, che è l'altra metà e si sbaglia in silenzio: chi accetta chi, e se il drop
/// diventa un riordino o un cambio di padre.</para>
/// </summary>
public class TocDropRulesTests
{
    private static EditorTocItem Voce(int id, int? padre, bool libera = true, int profondita = 0,
        int altezza = 0, string albero = "root") =>
        new($"s-{id}", $"S{id}", SectionId: id, DragGroup: albero, ParentSectionId: padre,
            Movable: libera, SectionDepth: profondita, SubtreeHeight: altezza);

    /// <summary>
    /// A (radice) ─ A1 ─ A1a
    /// B (radice) ─ B1
    /// </summary>
    private static readonly EditorTocItem A = Voce(1, null);
    private static readonly EditorTocItem A1 = Voce(11, 1, profondita: 1, altezza: 1);
    private static readonly EditorTocItem A1a = Voce(111, 11, profondita: 2);
    private static readonly EditorTocItem B = Voce(2, null, altezza: 1);
    private static readonly EditorTocItem B1 = Voce(21, 2, profondita: 1);

    private static readonly IReadOnlyList<EditorTocItem> Albero = new[] { A, A1, A1a, B, B1 };

    [Fact]
    public void Fra_fratelli_e_un_riordino()
    {
        var m = TocDropRules.Mossa(Albero, B, A);

        Assert.NotNull(m);
        Assert.False(m!.Value.CambiaPadre);
        Assert.Equal(2, m.Value.SectionId);
        Assert.Equal(1, m.Value.BeforeSectionId);   // B sale: si mette prima di A
    }

    /// <summary>Su un'altra famiglia di fratelli: la sezione prende il posto del bersaglio, cioè ne diventa
    /// sorella — suo padre, e prima di lui.</summary>
    [Fact]
    public void Su_un_altro_gruppo_cambia_padre()
    {
        var m = TocDropRules.Mossa(Albero, B1, A1a);

        Assert.NotNull(m);
        Assert.True(m!.Value.CambiaPadre);
        Assert.Equal(21, m.Value.SectionId);
        Assert.Equal(11, m.Value.NuovoPadreId);      // il padre di A1a
        Assert.Equal(111, m.Value.BeforeSectionId);  // prima di A1a
    }

    /// <summary>⚠️ Alberi diversi non si toccano MAI: è quel che tiene separati i membri di un documento
    /// unito e i blocchi della vIPI ACC. Vale anche fra fratelli per posizione.</summary>
    [Fact]
    public void Alberi_diversi_non_si_accettano()
    {
        var altrove = Voce(9, null, albero: "membro-7");

        Assert.False(TocDropRules.Accetta(Albero, B, altrove));
        Assert.False(TocDropRules.Accetta(Albero, altrove, B));
        Assert.Null(TocDropRules.Mossa(Albero, B, altrove));
    }

    /// <summary>Una sezione di CATALOGO si riordina fra i suoi fratelli ma non cambia padre: il posto glielo
    /// assegna il catalogo.</summary>
    [Fact]
    public void Una_sezione_di_catalogo_riordina_ma_non_cambia_gruppo()
    {
        var catalogo = Voce(3, null, libera: false);
        var albero = new[] { A, A1, A1a, B, B1, catalogo };

        Assert.True(TocDropRules.Accetta(albero, catalogo, B));    // fratelli: sì
        Assert.False(TocDropRules.Accetta(albero, catalogo, A1));  // altro gruppo: no
    }

    /// <summary>Guardia del ciclo: A non può diventare sorella di una propria discendente — il suo
    /// sottoalbero sparirebbe dall'albero.</summary>
    [Fact]
    public void Non_si_lascia_dentro_il_proprio_sottoalbero()
    {
        Assert.False(TocDropRules.Accetta(Albero, A, A1a));
        Assert.False(TocDropRules.Accetta(Albero, A, A1));
    }

    /// <summary>Guardia della profondità: si guarda dove finirebbe la sezione (la profondità del bersaglio,
    /// di cui diventa SORELLA) più quel che si porta dietro.
    /// <para>⚠️ I bersagli si costruiscono da <c>DocumentSection.MaxDepth</c> e non si scrivono «A1a,
    /// profondità 2»: fino al 16 settembre 2026 quella voce ERA il bordo, perché il tetto era 3. Alzandolo
    /// a 5 il test è caduto con la guardia intatta — misurava il numero, non la regola.</para></summary>
    [Fact]
    public void Non_si_lascia_dove_il_sottoalbero_non_ci_sta()
    {
        const int Tetto = DocumentSection.MaxDepth;

        // Due bersagli, sul fondo e un gradino sopra. Padri diversi da quello della sezione mossa: fra
        // fratelli il drop è un riordino e la profondità non si guarda nemmeno.
        var fondo = Voce(80, 8, profondita: Tetto);
        var unGradinoSopra = Voce(81, 9, profondita: Tetto - 1);

        // Si porta dietro una figlia: lei finisce dove finisce il bersaglio, la figlia un gradino sotto.
        var conFiglia = Voce(5, null, altezza: 1);
        var albero = new[] { A, A1, A1a, B, B1, conFiglia, fondo, unGradinoSopra };

        Assert.False(TocDropRules.Accetta(albero, conFiglia, fondo));            // Tetto + 1: sfora
        Assert.True(TocDropRules.Accetta(albero, conFiglia, unGradinoSopra));    // Tetto esatto: ci sta

        // Una foglia non porta niente, quindi sul fondo ci sta.
        var foglia = Voce(6, null);
        var conFoglia = new[] { A, A1, A1a, B, B1, foglia, fondo };
        Assert.True(TocDropRules.Accetta(conFoglia, foglia, fondo));
    }

    [Fact]
    public void Una_voce_non_accetta_se_stessa()
    {
        Assert.False(TocDropRules.Accetta(Albero, A1, A1));
        Assert.Null(TocDropRules.Mossa(Albero, A1, A1));
    }

    /// <summary>Le voci che non sono sezioni — il pannello Release, un blocco ACC senza figlie — non entrano
    /// nel gioco né come sorgente né come bersaglio.</summary>
    [Fact]
    public void Una_voce_che_non_e_una_sezione_non_accetta_niente()
    {
        var release = new EditorTocItem("p-release", "Rilascio");

        Assert.False(TocDropRules.Accetta(Albero, A, release));
        Assert.False(TocDropRules.Accetta(Albero, release, A));
    }
}
