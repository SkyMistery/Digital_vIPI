using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Spec di un blocco per la creazione annidata della vIPI ACC (<see cref="IEditingRepository.EnsureVipiDocumentTreeAsync"/>):
/// una sezione radice (depth 0) con chiave <paramref name="Key"/> e titolo <paramref name="Title"/>, e sotto di lei le
/// sezioni del <paramref name="Profile"/> indicato. Doc refactor 08e-acc.
/// <para>
/// ⚠️ Le sezioni NON si elencano più qui (doc 14 §3f): le dice il catalogo, che è la fonte unica. Prima ogni
/// chiamante ne passava una lista, e accanto un secondo elenco — le «chiavi live» — scritto a mano e divergente:
/// l'ACC ne aveva cinque, l'APP otto, per una domanda a cui <c>SectionCatalog.IsHostRendered</c> risponde già.
/// </para>
/// </summary>
public sealed record VipiBlockSpec(string Key, string Title, SectionProfile Profile);
