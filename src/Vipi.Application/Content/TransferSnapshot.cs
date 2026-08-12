namespace Vipi.Application.Content;

/// <summary>
/// Una riga com'era: i suoi dati <b>e la sua posizione nell'outline</b>.
/// <para>Esiste perché <see cref="TransferPointInput"/> di proposito <b>non</b> porta gruppo, profondità e
/// ordine — li decide il repository, così che nessun editor possa scrivere a mano una riga orfana o un salto di
/// profondità. Quella regola vale quando si <b>scrive</b> una riga. Quando si <b>rimette</b> una riga che
/// esisteva, la posizione non è una scelta da riprendere: è parte di ciò che si sta restituendo.</para>
/// <para>Senza questo tipo, un annulla ricostruito con <c>AddPointAsync</c> restituirebbe le righe
/// <b>appiattite</b>, senza il loro outline — la clonazione lo fa già, ma lo fa apposta e lo dichiara. Un
/// annulla che restituisce righe diverse da quelle tolte non è un annulla: è un secondo danno con un nome
/// rassicurante.</para>
/// </summary>
/// <param name="Data">I campi editoriali della riga.</param>
/// <param name="Order">La posizione nel flusso: nell'outline l'ordine <b>è</b> la struttura.</param>
/// <param name="VariantGroup">Il gruppo di varianti, o <c>null</c> se la riga non stava in un gruppo.</param>
/// <param name="VariantDepth">Il rientro: 0 = alternativa, &gt; 0 = eccezione.</param>
public sealed record TransferPointSnapshot(TransferPointInput Data, int Order, int? VariantGroup, int VariantDepth);

/// <summary>Una riga da rimettere in un flusso che esiste ancora (eliminazione di una riga, o in blocco).</summary>
/// <param name="FlowId">Il flusso a cui la riga tornava. Se non esiste più, la riga non si ripristina.</param>
public sealed record TransferPointRestore(int FlowId, TransferPointSnapshot Point);

/// <summary>Un gruppo com'era: l'intestazione e tutte le sue righe con la loro struttura.</summary>
public sealed record TransferFlowSnapshot(TransferFlowInput Data, IReadOnlyList<TransferPointSnapshot> Points);
