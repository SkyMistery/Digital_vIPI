using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Ui.Components;

/// <summary>
/// Le regole del trascinamento nel menu-sezioni: che cosa <b>accetta</b> una voce, e in quale mossa si
/// traduce lasciandoci sopra un'altra voce. Funzione <b>pura</b>, come <see cref="SectionOrdering"/> — e per
/// la stessa ragione: è la parte che si può sbagliare in silenzio, e provarla dentro il pannello vorrebbe
/// dire fabbricare eventi di trascinamento, che è proprio ciò che una volta ha nascosto un gesto rotto per
/// un giorno intero.
///
/// <para>La regola a schermo resta una sola — <b>la sezione lasciata prende il posto di quella su cui la si
/// lascia</b> — ma il posto vuol dire due cose: fra <b>fratelli</b> è un ordine, su un <b>altro gruppo</b> è
/// un padre nuovo (carta 2026-09-04).</para>
/// </summary>
public static class TocDropRules
{
    /// <summary>
    /// Vero se <paramref name="bersaglio"/> accetta <paramref name="mossa"/>.
    ///
    /// <para>Sempre dentro il medesimo <b>albero</b> (<see cref="EditorTocItem.DragGroup"/>): ⚠️ è quel che
    /// impedisce a una sezione di cambiare documento in un editor unito e di saltare da un blocco all'altro
    /// nella vIPI ACC. Dentro l'albero, un bersaglio di un altro gruppo chiede quel che chiederebbe il
    /// motore — sezione <b>libera</b>, niente cicli, profondità sufficiente per il sottoalbero che si porta
    /// dietro — perché un bersaglio che si illumina e poi non fa niente è peggio di uno che non si illumina.</para>
    /// </summary>
    public static bool Accetta(IReadOnlyList<EditorTocItem> voci, EditorTocItem mossa, EditorTocItem bersaglio)
    {
        if (mossa.SectionId is not int d || bersaglio.SectionId is not int t || t == d) return false;
        if (!string.Equals(bersaglio.DragGroup, mossa.DragGroup, StringComparison.Ordinal)) return false;

        if (bersaglio.ParentSectionId == mossa.ParentSectionId) return true;   // fratelli: riordino

        return mossa.Movable
               && !Discende(voci, bersaglio, d)
               && bersaglio.SectionDepth + mossa.SubtreeHeight <= DocumentSection.MaxDepth;
    }

    /// <summary>La mossa che nasce dal gesto, o <c>null</c> se il bersaglio non accetta.</summary>
    public static TocReorder? Mossa(IReadOnlyList<EditorTocItem> voci, EditorTocItem mossa, EditorTocItem bersaglio)
    {
        if (!Accetta(voci, mossa, bersaglio)) return null;
        var m = mossa.SectionId!.Value;
        var t = bersaglio.SectionId!.Value;

        // Gruppi diversi: prendere il posto del bersaglio vuol dire diventarne SORELLA — suo padre, e prima di lui.
        if (bersaglio.ParentSectionId != mossa.ParentSectionId)
            return new TocReorder(m, t, CambiaPadre: true, NuovoPadreId: bersaglio.ParentSectionId);

        // Fratelli nell'ordine attuale = le voci dello stesso albero E dello stesso padre, nell'ordine in cui
        // sono rese: il menu è la proiezione del documento, non un secondo elenco.
        var fratelli = voci
            .Where(x => x.SectionId is not null
                        && string.Equals(x.DragGroup, bersaglio.DragGroup, StringComparison.Ordinal)
                        && x.ParentSectionId == bersaglio.ParentSectionId)
            .Select(x => x.SectionId!.Value)
            .ToList();

        return SectionOrdering.TryDropOnto(fratelli, m, t, out var prima) ? new TocReorder(m, prima) : null;
    }

    /// <summary>Vero se <paramref name="voce"/> discende dalla sezione <paramref name="avoId"/>: si risale di
    /// padre in padre fra le voci, che l'albero ce l'hanno già tutto.</summary>
    private static bool Discende(IReadOnlyList<EditorTocItem> voci, EditorTocItem voce, int avoId)
    {
        var padre = voce.ParentSectionId;
        for (var giri = 0; padre is int p && giri <= voci.Count; giri++)
        {
            if (p == avoId) return true;
            padre = voci.FirstOrDefault(x => x.SectionId == p).ParentSectionId;
        }
        return false;
    }
}
