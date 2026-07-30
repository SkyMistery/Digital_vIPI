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
}
