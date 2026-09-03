namespace Vipi.Domain.Entities;

/// <summary>
/// Un'<b>unione</b> di documenti: due o più <see cref="Document"/> che si leggono in una pagina sola, si
/// redigono da un editor solo e si pubblicano con un gesto solo (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c>).
///
/// <para>
/// I documenti <b>restano separati</b> in archivio — ognuno con le sue versioni, le sue release e la sua
/// famiglia. L'unione è una <b>relazione</b>, non un sesto tipo di documento: è ciò che la rende
/// indipendente dal tipo, senza toccare i sei descrittori di release, le sei rotte e i cinque provider di
/// congelamento.
/// </para>
///
/// <para>
/// ⚠️ <b>Perché un elenco ordinato e non una coppia.</b> Un aeroporto può avere più di un APP non
/// remotizzato: misurato in archivio, <c>LIBV_APP</c> e <c>LIBV_G_APP</c> su Gioia del Colle, e lo stesso
/// su LIBN, LIPE, LIRM, LIRS. Due colonne su <see cref="Document"/> non reggerebbero un caso che c'è già.
/// </para>
///
/// <para>
/// ⚠️ <b>Il commento in testa a <c>MilDocRoutes</c> dice «non è la stessa pagina con un parametro», e resta
/// vero.</b> L'unione non è un parametro: è un atto editoriale esplicito, registrato e reversibile. I cicli
/// AIRAC dei membri smettono di essere indipendenti <i>perché qualcuno ha deciso che lo smettano</i>, ed è
/// il senso della pubblicazione accoppiata.
/// </para>
/// </summary>
public class DocumentUnion
{
    public int Id { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>VID di chi ha creato l'unione. 0 = sistema, come nelle release di backfill.</summary>
    public int CreatedByUserId { get; set; }

    // ⚠️ Niente RowVersion, e non è una dimenticanza: l'unione si tocca dall'editor, sotto il lock, un
    // redattore alla volta — il last-write-wins è il comportamento voluto, come per DocumentProfile. La
    // decisione del 14 agosto 2026 è che un token che nessuno ruota è peggio della sua assenza, perché fa
    // contare su una difesa che non c'è; l'elenco delle tre entità che ce l'hanno è presidiato da
    // ConcorrenzaOttimisticaTests.Solo_le_entita_decise_dichiarano_un_token_di_concorrenza.

    /// <summary>I membri, in ordine. Il primo (<see cref="DocumentUnionMember.Order"/> minore) è l'OSPITE.</summary>
    public ICollection<DocumentUnionMember> Members { get; set; } = new List<DocumentUnionMember>();
}

/// <summary>
/// Un documento dentro un'<see cref="DocumentUnion"/>, alla sua posizione.
///
/// <para>
/// ⚠️ <b>Il legame è verso <see cref="Document"/>, non verso la chiave di release.</b> <c>DocRelease.TargetKey</c>
/// è un <i>puntatore</i> e viene riscritto — dalla rinomina di un callsign (<c>EfCallsignRenameService</c>) e
/// dal ripuntamento notturno (<c>IReleaseRepository.RepointKeyAsync</c>). Un'unione agganciata alla chiave si
/// romperebbe alla prima rinomina; agganciata all'id del documento, no.
/// </para>
/// </summary>
public class DocumentUnionMember
{
    public int Id { get; set; }

    public int UnionId { get; set; }
    public DocumentUnion? Union { get; set; }

    /// <summary>
    /// Il documento. ⚠️ <b>Indice UNICO</b>: un documento sta in al più <b>una</b> unione. È una guardia,
    /// non una speranza — come l'indice unico su <c>Airports.DocumentId</c>: due unioni sullo stesso
    /// documento si vedrebbero solo mesi dopo, come una pagina che mostra due volte lo stesso contenuto.
    /// </summary>
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>
    /// Posizione nell'unione, 0-based e densa. Il minore è l'ospite: pagina ed editor dell'unione vivono al
    /// <b>suo</b> indirizzo, e gli altri ci reindirizzano.
    /// <para>⚠️ Non ha niente a che vedere con <c>DocumentSection.Order</c>, che ordina i <b>fratelli</b>
    /// dentro un documento. Una sezione non cambia mai documento; qui si ordinano i documenti fra loro.</para>
    /// </summary>
    public int Order { get; set; }
}
