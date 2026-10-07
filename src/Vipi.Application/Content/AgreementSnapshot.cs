using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una clausola com'era: i suoi dati <b>e la sua posizione nell'outline</b>.
/// <para>Esiste per la stessa ragione del suo predecessore: <see cref="AgreementClauseInput"/> non porta
/// gruppo, profondità e ordine perché quelli li decide il repository quando si <b>scrive</b>. Quando si
/// <b>rimette</b> una clausola che esisteva, la posizione non è una scelta da riprendere — è parte di ciò che
/// si sta restituendo. Un annulla che restituisce righe appiattite non è un annulla: è un secondo danno con un
/// nome rassicurante.</para>
/// <para>Il <b>verso</b> non è più qui: lo dice la sezione che la ospita.</para>
/// <para>⚠️ <b>Una clausola condivisa ricorda dov'era.</b> Toglierla da un accordo non la distrugge — vive negli
/// altri — e l'annulla deve rimettere la <b>presenza</b>, non una copia destinata a divergere. Se invece nel
/// frattempo è sparita ovunque, torna dal contenuto, di casa dov'era e ospite dov'era ospite.</para>
/// </summary>
/// <param name="LinkedClauseId">L'id della clausola, se era <b>condivisa</b>. Se vive ancora, il ripristino ne
/// rimette la presenza.</param>
/// <param name="WasGuest">Nella sezione da cui si fotografa era ospite, non di casa.</param>
/// <param name="HomeSectionId">La sezione di casa, se era ospite.</param>
/// <param name="HostSectionIds">Le sezioni che la ospitavano.</param>
public sealed record AgreementClauseSnapshot(
    AgreementClauseInput Data, int Order, int? VariantGroup, int VariantDepth,
    int? LinkedClauseId = null, bool WasGuest = false, int? HomeSectionId = null,
    IReadOnlyList<int>? HostSectionIds = null)
{
    /// <summary>La stessa fotografia, con le presenze della riga da cui è stata scattata.</summary>
    public AgreementClauseSnapshot ConLePresenzeDi(AgreementClauseRow riga)
    {
        if (!riga.IsShared) return this;
        var casa = riga.HomeSectionId ?? riga.SectionId;
        return this with
        {
            LinkedClauseId = riga.Id,
            WasGuest = riga.IsGuest,
            HomeSectionId = casa,
            HostSectionIds = riga.SharedWith.Select(x => x.SectionId).Append(riga.SectionId)
                .Where(s => s != casa).Distinct().ToList(),
        };
    }
}

/// <summary>Una clausola da rimettere in una sezione che esiste ancora (eliminazione singola o in blocco).</summary>
/// <param name="SectionId">La sezione a cui tornava. Se non esiste più, la clausola non si ripristina —
/// ricrearne l'intestazione per ospitarla sarebbe inventare un accordo che nessuno ha scritto.</param>
public sealed record AgreementClauseRestore(int SectionId, AgreementClauseSnapshot Clause);

/// <summary>
/// La posizione che una clausola <b>rimasta</b> aveva nel gruppo di una clausola eliminata (U-061).
/// <para>Eliminare una variante scioglie il gruppo rimasto di una: la superstite perde gruppo, profondità e «in
/// ogni caso». La foto della sola clausola eliminata non basta a rimettere le cose com'erano — l'eccezione
/// rientrerebbe a profondità 1 accanto a una capofila fuori dal gruppo. Il ripristino rimette questa posizione
/// solo alle sorelle ancora fuori da ogni gruppo: una messa altrove nel frattempo non si tocca.</para>
/// </summary>
public sealed record AgreementOutlineRestore(int ClauseId, int VariantGroup, int VariantDepth, bool IsGroupWide)
{
    /// <summary>Le sorelle che restano quando si eliminano <paramref name="eliminate"/>: le clausole della stessa
    /// sezione e dello stesso gruppo di una eliminata, che non sono eliminate a loro volta.</summary>
    public static IReadOnlyList<AgreementOutlineRestore> SorelleDi(
        IEnumerable<AgreementClauseRow> righe, IReadOnlyCollection<int> eliminate)
    {
        var tutte = righe.ToList();
        var gruppi = tutte.Where(r => eliminate.Contains(r.Id) && r.VariantGroup is not null)
            .Select(r => (r.SectionId, Gruppo: r.VariantGroup!.Value)).ToHashSet();
        return tutte
            .Where(r => r.VariantGroup is int g && gruppi.Contains((r.SectionId, g)) && !eliminate.Contains(r.Id))
            .Select(r => new AgreementOutlineRestore(r.Id, r.VariantGroup!.Value, r.VariantDepth, r.IsGroupWide))
            .ToList();
    }
}

/// <summary>Una sezione com'era: la sua intestazione e tutte le sue clausole con la loro struttura. Le clausole
/// condivise ricordano dov'erano (<see cref="AgreementClauseSnapshot.LinkedClauseId"/>).</summary>
public sealed record AgreementSectionSnapshot(
    AgreementSectionInput Data, int Order, IReadOnlyList<AgreementClauseSnapshot> Clauses);

/// <summary>Un accordo com'era: i due capi e tutte le sue sezioni.</summary>
public sealed record AgreementSnapshot(AgreementInput Data, IReadOnlyList<AgreementSectionSnapshot> Sections);

/// <summary>Una sezione da rimettere in un accordo che esiste ancora.</summary>
/// <param name="AgreementId">L'accordo a cui tornava; se non esiste più, la sezione non si ripristina.</param>
public sealed record AgreementSectionRestore(int AgreementId, AgreementSectionSnapshot Section);
