namespace Vipi.Ui.Components;

/// <summary>
/// Quali sotto-sezioni rendere rispetto al corpo della sezione padre (doc 11 §3g). Il corpo è una posizione in una
/// sequenza di tre slot: <c>Before</c> → corpo → <c>After</c>. Gli host che producono il corpo da sé (le sezioni
/// derivate di vIPI ACC / APP / vLOA) invocano <c>SectionBody</c> due volte, una per slot.
/// </summary>
public enum SectionSlot
{
    /// <summary>Tutte, nell'ordine corretto attorno al corpo (usato quando è <c>SectionBody</c> a rendere i blocchi).</summary>
    All,
    /// <summary>Solo le sotto-sezioni con <c>BeforeParentBody</c>.</summary>
    Before,
    /// <summary>Solo le sotto-sezioni che seguono il corpo (default storico).</summary>
    After,
    /// <summary>
    /// Solo i blocchi: nessuna sotto-sezione. Serve a chi rende il corpo in tre chiamate — sotto-sezioni
    /// «prima», blocchi, sotto-sezioni «dopo» — e lo fa perché in mezzo ci mette la scheda che disegna da sé.
    /// <para>⚠️ Senza questo valore quella chiamata di mezzo usava <see cref="All"/>, e su una sezione che ha
    /// <b>sia</b> la scheda <b>sia</b> delle sotto-sezioni (la prima è «Aree di lavoro» del vSOP militare) le
    /// sotto-sezioni uscivano <b>due volte</b>.</para>
    /// </summary>
    Blocks,
}
