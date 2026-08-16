using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una clausola com'era: i suoi dati, il suo verso <b>e la sua posizione nell'outline</b>.
/// <para>Esiste per la stessa ragione del suo predecessore: <see cref="AgreementClauseInput"/> non porta
/// gruppo, profondità e ordine perché quelli li decide il repository quando si <b>scrive</b>. Quando si
/// <b>rimette</b> una clausola che esisteva, la posizione non è una scelta da riprendere — è parte di ciò che
/// si sta restituendo. Un annulla che restituisce righe appiattite non è un annulla: è un secondo danno con un
/// nome rassicurante.</para>
/// </summary>
public sealed record AgreementClauseSnapshot(
    AgreementClauseInput Data, AgreementDirection Direction, int Order, int? VariantGroup, int VariantDepth);

/// <summary>Una clausola da rimettere in un accordo che esiste ancora (eliminazione singola o in blocco).</summary>
/// <param name="AgreementId">L'accordo a cui tornava. Se non esiste più, la clausola non si ripristina —
/// ricrearne l'intestazione per ospitarla sarebbe inventare un accordo che nessuno ha scritto.</param>
public sealed record AgreementClauseRestore(int AgreementId, AgreementClauseSnapshot Clause);

/// <summary>Un accordo com'era: l'intestazione, le parti, gli aeroporti e tutte le clausole con la loro
/// struttura.</summary>
public sealed record AgreementSnapshot(AgreementInput Data, IReadOnlyList<AgreementClauseSnapshot> Clauses);
