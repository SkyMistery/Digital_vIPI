using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Un accordo come lo vede il navigatore: una foglia dell'albero.
/// <para>Porta i due conteggi che fanno alzare la mano — un accordo senza ricevente, e le clausole ancora da
/// rivedere — perché l'avviso deve stare dove si sceglie, non dentro l'accordo che bisogna aprire per vederlo.</para>
/// </summary>
/// <param name="Airports">Gli aeroporti in una riga sola; vuoto per sorvoli/VFR/altro.</param>
/// <param name="Directions">Quanti versi ha l'accordo: 2 = bilaterale, e si vede senza aprirlo.</param>
/// <param name="NoReceiver">L'accordo non ha nessuno sul lato che riceve: il traffico finisce a UNICOM.</param>
public sealed record XferNavAgreement(int Id, TransferFlowKind Kind, string Airports, string? Description,
                                      int ClauseCount, int Directions, bool NoReceiver, int ToReview)
{
    public bool HasWarning => NoReceiver || ToReview > 0;
}

/// <summary>
/// Una **controparte** nel navigatore, con gli accordi che la riguardano.
/// <para>L'albero è cambiato asse: prima era settore ▸ aeroporto ▸ gruppo, cioè l'ordine in cui il modello
/// vecchio costringeva a scrivere. Adesso è la controparte, che è il modo in cui un accordo viene in mente —
/// «l'accordo con Roma», non «il flusso del settore ES verso l'aeroporto di Bari».</para>
/// </summary>
/// <param name="Key">Chiave di collasso, univoca nell'albero.</param>
/// <param name="Label">Come si legge la controparte: gli enti dell'altro capo, o «— senza ricevente».</param>
public sealed record XferNavCounterpart(string Key, string Label, IReadOnlyList<XferNavAgreement> Agreements)
{
    public int AgreementCount => Agreements.Count;

    public bool HasWarning => Agreements.Any(a => a.HasWarning);
}
