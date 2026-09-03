namespace Vipi.Application.Content;

/// <summary>
/// Una destinazione possibile per lo spostamento di una sezione: il <b>padre</b> in cui finirebbe
/// (<c>null</c> = la radice dell'albero mostrato) e come si chiama a schermo.
/// </summary>
/// <param name="ParentId">Nuovo padre da passare a <c>IEditingService.MoveSectionToParentAsync</c>.</param>
/// <param name="Label">Titolo della sezione di destinazione (o l'etichetta della radice).</param>
/// <param name="Indent">Quanto rientra la voce nel menu: 0 = radice dell'albero mostrato.</param>
public readonly record struct SectionMoveTarget(int? ParentId, string Label, int Indent);

/// <summary>
/// Dove può andare una sezione. Funzione <b>pura</b>, come <see cref="SectionOrdering"/>: è la parte che si
/// può sbagliare in silenzio — un elenco che offre una destinazione impossibile dà un comando che non fa
/// niente — e provarla montando l'editor intero costerebbe una fixture con servizio di editing e JS.
///
/// <para>⚠️ Questo elenco <b>non è la garanzia</b>: le stesse domande le rifà
/// <c>IEditingRepository.MoveSectionToParentAsync</c>, che è la porta da cui passa la mutazione. Qui si
/// decide che cosa <b>mostrare</b>; là che cosa si può <b>fare</b>. Un albero vecchio in mano alla pagina
/// deve dare una mossa rifiutata, non un documento storto.</para>
/// </summary>
public static class SectionMoveTargets
{
    /// <summary>
    /// Vero se questa sezione si può spostare in un altro gruppo: solo le <b>libere</b>.
    ///
    /// <para>⚠️ Una sezione di catalogo ha una posizione standard — è quella che conta
    /// <see cref="SectionOrdering.OffsetsFromStandard"/> — e portarla in un altro gruppo la renderebbe muta.
    /// La domanda si fa sulla CHIAVE e non sul profilo, così la risposta è la stessa in UI e nel motore.</para>
    /// </summary>
    public static bool Spostabile(EditableSection s) => SectionKeys.IsCustom(s.SectionKey);

    /// <summary>
    /// Le destinazioni per <paramref name="mossa"/> dentro l'albero mostrato dall'editor.
    ///
    /// <para>Si escludono: sé stessa e il proprio <b>sottoalbero</b> (sarebbe un ciclo), il <b>padre attuale</b>
    /// (è dove sta già: per muoversi lì dentro ci sono le frecce e il trascinamento) e ogni sezione che non
    /// avrebbe abbastanza profondità residua per ospitare il sottoalbero che la sezione si porta dietro.</para>
    /// </summary>
    /// <param name="radici">Le sezioni di primo livello dell'albero mostrato. ⚠️ Per la vIPI ACC sono le figlie
    /// del <b>blocco</b> che si sta modificando, non le radici del documento: il blocco è il gruppo.</param>
    /// <param name="mossa">La sezione da spostare.</param>
    /// <param name="radiceId">Il padre delle <paramref name="radici"/>: <c>null</c> per un documento piatto,
    /// l'Id della sezione-blocco per la vIPI ACC. È la destinazione «primo livello».</param>
    /// <param name="etichettaRadice">Come si chiama a schermo quella destinazione.</param>
    /// <param name="titolo">Come si chiama una sezione a schermo (titoli di catalogo risolti). Null = quel che
    /// porta il documento.</param>
    /// <param name="profonditaMassima">Livelli consentiti dal modello (<c>DocumentSection.MaxDepth</c>).</param>
    public static IReadOnlyList<SectionMoveTarget> Per(
        IReadOnlyList<EditableSection> radici, EditableSection mossa, int? radiceId, string etichettaRadice,
        Func<EditableSection, string>? titolo = null, int profonditaMassima = 3)
    {
        var esiti = new List<SectionMoveTarget>();
        if (radici.Count == 0) return esiti;

        var padreAttuale = PadreDi(radici, mossa.Id, radiceId, out var trovata);
        if (!trovata) return esiti;   // la sezione non è in questo albero: nessuna destinazione da offrire

        var altezza = Altezza(mossa);
        var profonditaRadice = radici[0].Depth - 1;

        // «Primo livello»: le radici dell'albero mostrato. ⚠️ Per la vIPI ACC non è la radice del DOCUMENTO —
        // là le radici sono i blocchi, e farne una vorrebbe dire creare un blocco.
        if (padreAttuale != radiceId && profonditaRadice + 1 + altezza <= profonditaMassima)
            esiti.Add(new SectionMoveTarget(radiceId, etichettaRadice, 0));

        void Scendi(IReadOnlyList<EditableSection> gruppo, int indent)
        {
            foreach (var s in gruppo)
            {
                if (s.Id == mossa.Id) continue;   // sé stessa e, con lei, tutto il suo sottoalbero
                if (s.Id != padreAttuale && s.Depth + 1 + altezza <= profonditaMassima)
                    esiti.Add(new SectionMoveTarget(s.Id, titolo?.Invoke(s) ?? s.Title, indent + 1));
                Scendi(s.Children, indent + 1);
            }
        }

        Scendi(radici, 0);
        return esiti;
    }

    /// <summary>Quanti livelli scende il sottoalbero: 0 per una sezione senza figlie. ⚠️ È l'altezza che la
    /// sezione si porta dietro, ed è quella a decidere se una destinazione ci sta.</summary>
    public static int Altezza(EditableSection s)
    {
        var max = 0;
        foreach (var c in s.Children) max = Math.Max(max, Altezza(c) + 1);
        return max;
    }

    /// <summary>Il padre di una sezione dentro l'albero mostrato (<paramref name="radiceId"/> se è una radice).</summary>
    private static int? PadreDi(IReadOnlyList<EditableSection> gruppo, int id, int? padre, out bool trovata)
    {
        foreach (var s in gruppo)
        {
            if (s.Id == id) { trovata = true; return padre; }
            var dentro = PadreDi(s.Children, id, s.Id, out trovata);
            if (trovata) return dentro;
        }
        trovata = false;
        return null;
    }
}
