using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Dove può andare una sezione: <see cref="SectionMoveTargets"/>, carta 2026-09-04.
///
/// <para>⚠️ L'elenco non è la garanzia — quella sta nel repository — ma è quel che si legge a schermo, e una
/// voce che offre una destinazione impossibile è un comando che non fa niente. Qui si prova che l'elenco non
/// contenga mai sé stessa, il proprio sottoalbero, il padre attuale né una destinazione troppo profonda.</para>
/// </summary>
public class SectionMoveTargetsTests
{
    private static EditableSection Sez(int id, string titolo, int depth, string? chiave = null,
        params EditableSection[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        SectionKey = chiave ?? $"custom:{id:x8}",
        Depth = depth,
        Order = id,
        Blocks = Array.Empty<EditableBlock>(),
        Children = figlie,
    };

    /// <summary>
    /// A ─ A1 ─ A1a
    /// B ─ B1
    /// </summary>
    private static IReadOnlyList<EditableSection> Albero() => new[]
    {
        Sez(1, "A", 0, null, Sez(11, "A1", 1, null, Sez(111, "A1a", 2))),
        Sez(2, "B", 0, null, Sez(21, "B1", 1)),
    };

    private static EditableSection Trova(IReadOnlyList<EditableSection> albero, int id)
    {
        foreach (var s in albero)
        {
            if (s.Id == id) return s;
            var dentro = Cerca(s, id);
            if (dentro is not null) return dentro;
        }
        throw new InvalidOperationException($"sezione {id} non nell'albero di prova");
    }

    private static EditableSection? Cerca(EditableSection s, int id)
    {
        foreach (var c in s.Children)
        {
            if (c.Id == id) return c;
            var dentro = Cerca(c, id);
            if (dentro is not null) return dentro;
        }
        return null;
    }

    private static IReadOnlyList<SectionMoveTarget> Per(int id, IReadOnlyList<EditableSection>? albero = null,
        int? radiceId = null)
    {
        var a = albero ?? Albero();
        return SectionMoveTargets.Per(a, Trova(a, id), radiceId, "Primo livello");
    }

    [Fact]
    public void Non_offre_se_stessa_ne_il_proprio_sottoalbero()
    {
        var ids = Per(11).Select(t => t.ParentId).ToList();

        Assert.DoesNotContain(11, ids);    // sé stessa
        Assert.DoesNotContain(111, ids);   // la propria figlia
    }

    [Fact]
    public void Non_offre_il_padre_attuale()
    {
        // A1 sta già dentro A: dentro il proprio gruppo ci si muove con le frecce, non con questo comando.
        Assert.DoesNotContain(1, Per(11).Select(t => t.ParentId));
        // ...e una radice non si offre «al primo livello», che è dove sta.
        Assert.DoesNotContain(Per(1), t => t.ParentId is null);
    }

    [Fact]
    public void Offre_le_altre_sezioni_e_il_primo_livello()
    {
        var t = Per(11);

        Assert.Contains(t, x => x.ParentId is null);   // primo livello
        Assert.Contains(t, x => x.ParentId == 2);      // B
        Assert.Contains(t, x => x.ParentId == 21);     // B1 — ci sta: A1 porta con sé una figlia sola
    }

    /// <summary>⚠️ La profondità si misura sul SOTTOALBERO: A ne porta due, quindi sotto B1 (profondità 1)
    /// non ci sta, mentre A1, che ne porta uno, sì.</summary>
    [Fact]
    public void Esclude_le_destinazioni_troppo_profonde_per_il_sottoalbero()
    {
        Assert.DoesNotContain(21, Per(1).Select(t => t.ParentId));   // A sotto B1: sarebbe un livello 4
        Assert.Contains(2, Per(1).Select(t => t.ParentId));          // A sotto B: 1 + 2 = 3, il massimo
        Assert.Contains(21, Per(11).Select(t => t.ParentId));        // A1 sotto B1: 2 + 1 = 3
    }

    /// <summary>La vIPI ACC: l'albero mostrato sono le figlie del BLOCCO, e «primo livello» è il blocco —
    /// non la radice del documento, dove una sezione diventerebbe un blocco.</summary>
    [Fact]
    public void Nella_vIPI_ACC_il_primo_livello_e_il_blocco()
    {
        var dentroIlBlocco = new[]
        {
            Sez(1, "A", 1, null, Sez(11, "A1", 2)),
            Sez(2, "B", 1),
        };

        var t = SectionMoveTargets.Per(dentroIlBlocco, Trova(dentroIlBlocco, 11), radiceId: 99, "Blocco");

        Assert.Contains(t, x => x.ParentId == 99);                 // il blocco, non null
        Assert.DoesNotContain(t, x => x.ParentId is null);
        // ⚠️ A1 è già a profondità 2: sotto B (profondità 1) ci sta, e non c'è altro spazio sotto di lei.
        Assert.Contains(t, x => x.ParentId == 2);
    }

    [Fact]
    public void Le_voci_rientrano_come_l_albero()
    {
        var t = Per(21);   // B1 si sposta: le destinazioni sono A, A1, A1a e il primo livello

        Assert.Equal(0, t.First(x => x.ParentId is null).Indent);
        Assert.Equal(1, t.First(x => x.ParentId == 1).Indent);
        Assert.Equal(2, t.First(x => x.ParentId == 11).Indent);
    }

    /// <summary>Solo le sezioni LIBERE si spostano di gruppo: è la stessa domanda che fa il motore.</summary>
    [Fact]
    public void Spostabile_solo_una_sezione_libera()
    {
        Assert.True(SectionMoveTargets.Spostabile(Sez(1, "Libera", 0)));
        Assert.True(SectionMoveTargets.Spostabile(Sez(1, "Storica", 0, SectionKeys.LegacyCustom)));
        Assert.False(SectionMoveTargets.Spostabile(Sez(1, "Frequenze", 0, "frequencies")));
        Assert.False(SectionMoveTargets.Spostabile(Sez(1, "Carte", 0, SectionKeys.ChartsSid)));
    }

    /// <summary>Una sezione che non è in questo albero non ha destinazioni: l'editor unito monta un componente
    /// per membro, e le sezioni di un membro non sono destinazioni per quelle di un altro.</summary>
    [Fact]
    public void Una_sezione_di_un_altro_albero_non_ha_destinazioni()
    {
        var albero = Albero();
        var estranea = Sez(999, "Altrove", 0);

        Assert.Empty(SectionMoveTargets.Per(albero, estranea, null, "Primo livello"));
    }
}
