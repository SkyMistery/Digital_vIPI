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
/// </summary>
public sealed record AgreementClauseSnapshot(
    AgreementClauseInput Data, int Order, int? VariantGroup, int VariantDepth);

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

/// <summary>Una sezione com'era: la sua intestazione e tutte le sue clausole con la loro struttura.</summary>
/// <param name="SharedSectionId">
/// L'id della sezione, se era <b>condivisa</b> con altri accordi. Il ripristino allora la rimette come presenza di
/// quella che vive ancora altrove; rimetterne il contenuto ne farebbe una copia destinata a divergere. Se nel
/// frattempo è sparita anche là, torna dal contenuto.
/// </param>
public sealed record AgreementSectionSnapshot(
    AgreementSectionInput Data, int Order, IReadOnlyList<AgreementClauseSnapshot> Clauses, int? SharedSectionId = null);

/// <summary>Un accordo com'era: i due capi e tutte le sue sezioni.</summary>
public sealed record AgreementSnapshot(AgreementInput Data, IReadOnlyList<AgreementSectionSnapshot> Sections);

/// <summary>Una sezione da rimettere in un accordo che esiste ancora.</summary>
/// <param name="AgreementId">L'accordo a cui tornava; se non esiste più, la sezione non si ripristina.</param>
public sealed record AgreementSectionRestore(int AgreementId, AgreementSectionSnapshot Section);
