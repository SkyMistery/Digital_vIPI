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

/// <summary>Una sezione com'era: la sua intestazione e tutte le sue clausole con la loro struttura.</summary>
public sealed record AgreementSectionSnapshot(
    AgreementSectionInput Data, int Order, IReadOnlyList<AgreementClauseSnapshot> Clauses);

/// <summary>Un accordo com'era: i due capi e tutte le sue sezioni.</summary>
public sealed record AgreementSnapshot(AgreementInput Data, IReadOnlyList<AgreementSectionSnapshot> Sections);

/// <summary>Una sezione da rimettere in un accordo che esiste ancora.</summary>
/// <param name="AgreementId">L'accordo a cui tornava; se non esiste più, la sezione non si ripristina.</param>
public sealed record AgreementSectionRestore(int AgreementId, AgreementSectionSnapshot Section);
