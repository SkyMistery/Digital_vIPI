using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Etichette dei tipi di flusso di trasferimento — unico punto di verità condiviso da derivazioni
/// e viewer (Coordinamenti Estesa, TransfersLive, vIPI ACC/APP). <see cref="LabelEn"/> = variante inglese per le vLOA.</summary>
public static class TransferFlowKindLabels
{
    public static string Label(TransferFlowKind k) => k switch
    {
        TransferFlowKind.Arrival => "Arrivi",
        TransferFlowKind.Departure => "Partenze",
        TransferFlowKind.Overflight => "Sorvoli",
        TransferFlowKind.Vfr => "VFR",
        _ => "Altro",
    };

    /// <summary>Etichetta inglese (vLOA, documento bilaterale in EN).</summary>
    public static string LabelEn(TransferFlowKind k) => k switch
    {
        TransferFlowKind.Arrival => "Arrivals",
        TransferFlowKind.Departure => "Departures",
        TransferFlowKind.Overflight => "Overflights",
        TransferFlowKind.Vfr => "VFR",
        _ => "Other",
    };
}
