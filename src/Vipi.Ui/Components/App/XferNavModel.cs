namespace Vipi.Ui.Components.App;

/// <summary>
/// Un **accordo** come lo vede il navigatore: la coppia di enti, e cosa c'è dentro.
///
/// <para>Dal 18 agosto 2026 è una <b>foglia sola per relazione</b>: l'accordo è la coppia, e le sue sezioni —
/// arrivi, partenze, sorvoli nei due versi — stanno nel riquadro di lavoro. Prima sotto la stessa coppia
/// stavano fino a otto foglie (la relazione LIBB ⇄ LGGG ne aveva otto in archivio), e per vedere «cosa ho
/// concordato con Atene» bisognava aprirle una per una.</para>
///
/// <para>Porta i conteggi che fanno alzare la mano — sezioni, clausole, cose da rivedere — perché l'avviso deve
/// stare dove si sceglie, non dentro l'accordo che bisogna aprire per vederlo.</para>
/// </summary>
/// <param name="Near">Il nostro capo, per primo: l'albero è orientato sulla ACC aperta.</param>
/// <param name="Far">La controparte.</param>
/// <param name="Sections">Quante sezioni ha.</param>
/// <param name="Clauses">Quante clausole in tutto.</param>
/// <param name="MissingReverse">Un traffico scritto in un verso solo dove il reciproco avrebbe senso.</param>
/// <param name="ToReview">Clausole verso un APP che non dicono ancora dove avviene il trasferimento.</param>
public sealed record XferNavAgreement(int Id, string Near, string Far, string? Note,
                                      int Sections, int Clauses, int MissingReverse, int ToReview)
{
    public bool HasWarning => MissingReverse > 0 || ToReview > 0;

    /// <summary>Chiave di ordinamento: la coppia, nel verso in cui si legge.</summary>
    public string Key => $"{Far}|{Near}";
}

/// <summary>
/// Una **controparte** nel navigatore: la sua ACC, e gli accordi con lei.
///
/// <para>L'albero ha cambiato asse quattro volte. Prima settore ▸ aeroporto ▸ gruppo, cioè l'ordine in cui il
/// modello di luglio costringeva a <i>scrivere</i>. Poi «la controparte», ma con la chiave sul <b>lato B</b> — e
/// per i 13 accordi di LIBB (10 su 11 di LIRR) in cui la ACC aperta <i>è</i> il lato B il ramo prendeva il nome
/// dei nostri stessi settori. Poi ACC ▸ relazione ▸ accordo, perché una coppia poteva avere più accordi.
/// <b>Adesso non può</b>: la relazione <i>è</i> l'accordo, e il terzo livello è sparito con la ragione che lo
/// teneva in piedi.</para>
/// </summary>
/// <param name="Key">Chiave di collasso, univoca nell'albero.</param>
/// <param name="Label">Come si legge la controparte: la sua ACC, o l'avviso che è fuori catalogo.</param>
/// <param name="Note">Qualifica del ramo quando serve: «interni» per gli accordi in casa. Vuoto altrimenti.</param>
/// <param name="SortOrder">0 = controparti vere, 1 = accordi interni, 2 = fuori catalogo. I due casi
/// particolari stanno in fondo: sono elenchi brevi e non sono un confine con cui si lavora.</param>
public sealed record XferNavCounterpart(string Key, string Label, string? Note,
                                        int SortOrder, IReadOnlyList<XferNavAgreement> Agreements)
{
    public int AgreementCount => Agreements.Count;

    public bool HasWarning => Agreements.Any(a => a.HasWarning);
}
