using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un accordo in cui una sezione condivisa compare: l'id per aprirlo, i due capi per nominarlo.</summary>
public sealed record AgreementShareRef(int AgreementId, string SideA, string SideB)
{
    /// <summary>«LIRR_SU_CTR ⇄ LICT_APP»: la coppia, come la scrive il navigatore.</summary>
    public string Label => $"{SideA} ⇄ {SideB}";
}

/// <summary>Una presenza di una sezione: in quale accordo, in che verso, a che posto.</summary>
public sealed record AgreementSharePlacement(int AgreementId, AgreementDirection Direction, int Order);

/// <summary>
/// Come si <b>disfa</b> un gesto sulle presenze di una sezione — condividere, togliere da un accordo, staccare:
/// le presenze <b>com'erano</b>, casa e ospiti.
///
/// <para>⚠️ È uno stato, non un gesto all'indietro. Togliere la sezione dal suo accordo di casa ne sposta la casa
/// al primo ospite: «ricondividi con l'accordo di prima» lo rimetterebbe come ospite e in coda, mentre qui torna
/// di casa dov'era, col suo verso e il suo posto.</para>
/// </summary>
/// <param name="Home">L'accordo di casa, com'era.</param>
/// <param name="Guests">Gli accordi ospiti, com'erano.</param>
/// <param name="CreatedAgreementId">L'accordo nato per ospitarla: annullando se ne va, se è rimasto vuoto.</param>
/// <param name="DetachedCopyId">La copia indipendente nata da «Stacca»: annullando se ne va.</param>
public sealed record AgreementPresenceUndo(
    int SectionId, AgreementSharePlacement Home, IReadOnlyList<AgreementSharePlacement> Guests,
    int? CreatedAgreementId = null, int? DetachedCopyId = null);

/// <summary>L'esito di «Condividi con…».</summary>
/// <param name="AgreementId">L'accordo in cui la sezione compare adesso: quello della coppia indicata.</param>
/// <param name="AgreementCreated">L'accordo non esisteva ed è stato creato.</param>
/// <param name="Added">Falso se la sezione ci compariva già: non è successo niente.</param>
public sealed record AgreementShareResult(int AgreementId, bool AgreementCreated, bool Added, AgreementPresenceUndo Undo);

/// <summary>L'esito di «Stacca»: la sezione indipendente nata in quell'accordo.</summary>
public sealed record AgreementDetachResult(int NewSectionId, AgreementPresenceUndo Undo);
