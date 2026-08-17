using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Un accordo come lo vede il navigatore: una foglia dell'albero.
/// <para>Porta i due conteggi che fanno alzare la mano — un accordo senza ricevente, e le clausole ancora da
/// rivedere — perché l'avviso deve stare dove si sceglie, non dentro l'accordo che bisogna aprire per vederlo.</para>
/// <para>E porta i conteggi dei <b>due versi separati</b> invece del totale: «3 ⇄ 0» dice che il reciproco non è
/// scritto, «3» da solo non lo diceva. Sul <c>vipi.db</c> vero i reciproci scritti sono <b>zero</b>, e nessuno
/// se n'era accorto proprio perché il totale li nascondeva.</para>
/// </summary>
/// <param name="Airports">Gli aeroporti in una riga sola; vuoto per sorvoli/VFR/altro.</param>
/// <param name="Outbound">Clausole nel verso «noi → loro», orientato sulla ACC aperta.</param>
/// <param name="Inbound">Clausole nel verso «loro → noi».</param>
/// <param name="NoReceiver">L'accordo non ha nessuno sul lato che riceve: il traffico finisce a UNICOM.</param>
public sealed record XferNavAgreement(int Id, TransferFlowKind Kind, string Airports, string? Description,
                                      int Outbound, int Inbound, bool NoReceiver, int ToReview)
{
    public int ClauseCount => Outbound + Inbound;

    /// <summary>Ha clausole nei due versi: bilaterale davvero, non «bilaterale in teoria».</summary>
    public bool IsBilateral => Outbound > 0 && Inbound > 0;

    public bool HasWarning => NoReceiver || ToReview > 0;
}

/// <summary>
/// Una **relazione** fra due enti — noi ⇄ loro — con gli accordi che ci stanno dentro: il secondo livello
/// dell'albero.
///
/// <para>È la <b>coppia</b> e non il solo ente lontano, perché l'identità di un accordo nel modello è
/// <b>(le due parti · il tipo di traffico · il gruppo di aeroporti)</b>: è la chiave con cui
/// <c>AgreementMerge.SplitRelations</c> decide che due accordi dicono la stessa cosa, e la tripla che la
/// proposta di fusione pretende identica prima di offrire il comando. Indicizzare sul solo lato lontano usava
/// <b>mezza</b> di quella chiave.</para>
///
/// <para>⚠️ E sugli accordi <b>interni</b> quella mezza chiave mentiva: con entrambi i capi in casa «l'ente
/// lontano» è un nostro settore, e compariva come se fosse la controparte. Una coppia si legge come una
/// relazione; un callsign da solo, no.</para>
///
/// <para>Non è collassabile, ed è una scelta: sotto Roma stanno cinque relazioni diverse e distinguerle serve,
/// ma farle aprire una per una costerebbe due gesti per arrivare a una foglia che ne chiede uno.</para>
/// </summary>
/// <param name="Near">Il nostro capo, per primo: l'albero è orientato sulla ACC aperta.</param>
/// <param name="Far">La controparte, o l'avviso che non c'è nessuno.</param>
public sealed record XferNavRelation(string Near, string Far, IReadOnlyList<XferNavAgreement> Agreements)
{
    /// <summary>Chiave di raggruppamento e ordinamento: la coppia, nel verso in cui si legge.</summary>
    public string Key => $"{Near}|{Far}";
}

/// <summary>
/// Una **controparte** nel navigatore: la sua ACC, le relazioni con lei, gli accordi di ognuna.
///
/// <para>L'albero ha cambiato asse tre volte. Prima era settore ▸ aeroporto ▸ gruppo, cioè l'ordine in cui il
/// modello vecchio costringeva a <i>scrivere</i>. Poi «la controparte», ma con la chiave sul <b>lato B</b> — e
/// per i 13 accordi di LIBB (10 su 11 di LIRR) in cui la ACC aperta <i>è</i> il lato B il ramo prendeva il nome
/// dei nostri stessi settori. Adesso il primo livello è l'<b>ACC della controparte letta dalla lente</b>
/// (<c>AgreementViewpoint</c>) — «l'accordo con Roma» è il modo in cui un accordo viene in mente — e il secondo
/// è la <b>relazione</b>, cioè la coppia di enti: vedi <see cref="XferNavRelation"/> per il perché.</para>
/// </summary>
/// <param name="Key">Chiave di collasso, univoca nell'albero.</param>
/// <param name="Label">Come si legge la controparte: la sua ACC, o l'avviso che non c'è nessuno.</param>
/// <param name="Note">Qualifica del ramo quando serve: «interni» per gli accordi in casa. Vuoto altrimenti.</param>
/// <param name="SortOrder">0 = controparti vere, 1 = accordi interni, 2 = senza controparte. I due casi
/// particolari stanno in fondo: sono elenchi brevi e non sono un confine con cui si lavora.</param>
public sealed record XferNavCounterpart(string Key, string Label, string? Note,
                                        int SortOrder, IReadOnlyList<XferNavRelation> Relations)
{
    public IEnumerable<XferNavAgreement> Agreements => Relations.SelectMany(e => e.Agreements);

    public int AgreementCount => Relations.Sum(e => e.Agreements.Count);

    public bool HasWarning => Agreements.Any(a => a.HasWarning);
}
