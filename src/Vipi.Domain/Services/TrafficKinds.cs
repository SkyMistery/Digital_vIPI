namespace Vipi.Domain.Services;

/// <summary>
/// Il traffico visto dall'altro capo di un accordo.
///
/// <para>Serve perché il tipo di traffico sta sull'<b>accordo</b>, non sul verso: un accordo ACC↔APP di tipo
/// <see cref="TransferFlowKind.Arrival"/> ha un verso opposto che di arrivi non parla — da un avvicinamento
/// verso l'area salgono <b>partenze</b>. Finché non esisteva nessun accordo bilaterale la cosa non si vedeva;
/// con i due versi a vista si vede subito.</para>
///
/// <para>È una <b>convenzione dichiarata</b>, non un dato: chi la rende deve dire che il tipo del verso opposto
/// è calcolato, altrimenti passa per una scelta di chi ha scritto l'accordo. Il giorno in cui un verso avesse
/// davvero un tipo proprio, il posto dove metterlo è una colonna sulla clausola (registro delle lacune,
/// <c>docs/feature/2026-08-16-accordi-di-coordinamento.md</c> §5) — non un'eccezione qui.</para>
/// </summary>
public static class TrafficKinds
{
    /// <summary>Il tipo di traffico del verso opposto. Sorvoli, VFR e «altro» sono simmetrici: chi attraversa un
    /// confine in un senso lo attraversa nell'altro allo stesso titolo.</summary>
    public static TransferFlowKind Reciprocal(TransferFlowKind kind) => kind switch
    {
        TransferFlowKind.Arrival => TransferFlowKind.Departure,
        TransferFlowKind.Departure => TransferFlowKind.Arrival,
        _ => kind,
    };

    /// <summary>Vero se il reciproco dice qualcosa di diverso dal tipo dell'accordo: è l'unico caso in cui la
    /// vista deve annunciare che quel tipo è <b>calcolato</b>.</summary>
    public static bool HasDistinctReciprocal(TransferFlowKind kind) => Reciprocal(kind) != kind;
}
