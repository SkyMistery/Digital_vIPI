using System.Collections.Generic;

namespace Vipi.Application.Content;

/// <summary>Un accordo in cui una clausola condivisa compare: l'id per aprirlo, i due capi per nominarlo, e la
/// sezione di quell'accordo che la porta.</summary>
public sealed record AgreementShareRef(int AgreementId, string SideA, string SideB, int SectionId = 0)
{
    /// <summary>«LIRR_SU_CTR ⇄ LICT_APP»: la coppia, come la scrive il navigatore.</summary>
    public string Label => $"{SideA} ⇄ {SideB}";
}

/// <summary>Una presenza di una clausola: in quale sezione compare.</summary>
public sealed record AgreementClausePresence(int ClauseId, int SectionId);

/// <summary>
/// Come si <b>disfa</b> un «Condividi con…»: le presenze che ha aggiunto, e quel che è nato per ospitarle.
/// </summary>
/// <param name="Added">Le presenze aggiunte: annullando se ne vanno, e solo quelle.</param>
/// <param name="CreatedSectionIds">Le sezioni nate per ospitare le clausole: se restano vuote, annullando se ne vanno.</param>
/// <param name="CreatedAgreementId">L'accordo nato per la coppia: se resta vuoto, annullando se ne va.</param>
public sealed record AgreementShareUndo(
    IReadOnlyList<AgreementClausePresence> Added, IReadOnlyList<int> CreatedSectionIds, int? CreatedAgreementId);

/// <summary>L'esito di «Condividi con…».</summary>
/// <param name="AgreementId">L'accordo della coppia indicata.</param>
/// <param name="SectionId">La sezione di quell'accordo in cui le clausole compaiono adesso; null se non è successo niente.</param>
/// <param name="Clauses">Quante clausole compaiono lì da adesso. Zero = c'erano già tutte.</param>
/// <param name="AgreementCreated">L'accordo non esisteva ed è stato creato.</param>
public sealed record AgreementShareResult(
    int AgreementId, int? SectionId, int Clauses, bool AgreementCreated, AgreementShareUndo Undo)
{
    /// <summary>Falso se le clausole ci comparivano già tutte: non è successo niente.</summary>
    public bool Added => Clauses > 0;

    /// <summary>Vero se la sezione di arrivo non c'era ed è nata col gesto; falso se le clausole sono entrate in
    /// una sezione che diceva già la stessa cosa.</summary>
    public bool SectionCreated => Undo.CreatedSectionIds.Count > 0;
}

/// <summary>Una presenza tolta da «Stacca»: quel che serve a rimetterla com'era.</summary>
/// <param name="WasGuest">Vero se lì la clausola era ospite; falso se ci stava di casa (e staccandola la casa è
/// passata a un'altra sezione).</param>
/// <param name="Order">Il posto che aveva di casa, se ci stava di casa.</param>
public sealed record AgreementDetachedPresence(int ClauseId, int SectionId, bool WasGuest, int Order);

/// <summary>Come si disfa uno «Stacca»: le copie indipendenti se ne vanno, le presenze tornano.</summary>
public sealed record AgreementDetachUndo(
    IReadOnlyList<int> CopyIds, IReadOnlyList<AgreementDetachedPresence> Presences);

/// <summary>L'esito di «Stacca»: quante clausole sono diventate copie indipendenti, e in quale sezione.</summary>
public sealed record AgreementDetachResult(int Clauses, int? SectionId, AgreementDetachUndo Undo);
