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
/// Un **ente della controparte** con gli accordi che lo riguardano: il secondo livello dell'albero.
/// <para>Non è collassabile, ed è una scelta: sotto Roma stanno <c>LIRR_US_CTR</c>, <c>LIRR_TS_CTR</c>,
/// <c>LIRN_US0_APP</c> e <c>LICA_ES0_APP</c>, e distinguerli serve; farli aprire uno per uno costerebbe due
/// gesti per arrivare a una foglia che ne richiede uno.</para>
/// </summary>
public sealed record XferNavEntity(string Label, IReadOnlyList<XferNavAgreement> Agreements);

/// <summary>
/// Una **controparte** nel navigatore: la sua ACC, i suoi enti, i suoi accordi.
///
/// <para>L'albero ha cambiato asse due volte. Prima era settore ▸ aeroporto ▸ gruppo, cioè l'ordine in cui il
/// modello vecchio costringeva a <i>scrivere</i>. Poi è diventato «la controparte», ma la chiave era il
/// <b>lato B</b> dell'accordo — e per i 13 accordi di LIBB (10 su 11 di LIRR) in cui la ACC aperta <i>è</i> il
/// lato B, il ramo prendeva il nome dei nostri stessi settori. Adesso la chiave è l'<b>ACC della controparte
/// letta dalla lente</b> (<c>AgreementViewpoint</c>): «l'accordo con Roma» è il modo in cui un accordo viene in
/// mente, e ci si arriva anche quando è stato scritto dall'altro capo.</para>
/// </summary>
/// <param name="Key">Chiave di collasso, univoca nell'albero.</param>
/// <param name="Label">Come si legge la controparte: la sua ACC, o l'avviso che non c'è nessuno.</param>
/// <param name="Note">Qualifica del ramo quando serve: «interni» per gli accordi in casa. Vuoto altrimenti.</param>
/// <param name="SortOrder">0 = controparti vere, 1 = accordi interni, 2 = senza controparte. I due casi
/// particolari stanno in fondo: sono elenchi brevi e non sono un confine con cui si lavora.</param>
public sealed record XferNavCounterpart(string Key, string Label, string? Note,
                                        int SortOrder, IReadOnlyList<XferNavEntity> Entities)
{
    public IEnumerable<XferNavAgreement> Agreements => Entities.SelectMany(e => e.Agreements);

    public int AgreementCount => Entities.Sum(e => e.Agreements.Count);

    public bool HasWarning => Agreements.Any(a => a.HasWarning);
}
