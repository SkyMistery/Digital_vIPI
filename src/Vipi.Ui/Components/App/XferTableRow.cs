using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Come si legge una riga dentro il suo gruppo di varianti. Le righe del gruppo sono lo <b>stesso accordo</b> in
/// casi diversi — il salvataggio propaga CoP e ricevente a tutte — e a schermo devono leggersi come un blocco.
/// <para>Stava dentro <c>AdminTrasferimentiPage</c> come tipo privato; è uscita perché la tabella delle righe è
/// diventata un componente, e la vista a gruppo e la vista a elenco devono renderla allo stesso modo. Due copie
/// vorrebbero dire correggere due volte ogni difetto di lettura.</para>
/// </summary>
/// <param name="InGroup">La riga appartiene a un gruppo di varianti.</param>
/// <param name="First">È la prima del blocco <b>come si vede</b> (la guida verticale deve aprirsi qui).</param>
/// <param name="Last">È l'ultima del blocco come si vede.</param>
/// <param name="Odd">Gruppo di indice dispari: due tinte alternate, altrimenti due blocchi adiacenti si fondono.</param>
/// <param name="Count">Quante righe ha il gruppo nel flusso (non nella vista): è il numero che si annuncia.</param>
/// <param name="Depth">Profondità nell'outline: 0 = alternativa pari-grado, &gt; 0 = eccezione.</param>
/// <param name="Parent">La riga di cui questa è l'eccezione, letta sull'ordine SALVATO.</param>
public sealed record XferGroupView(bool InGroup, bool First, bool Last, bool Odd, int Count, int Depth,
                                   TransferPointRow? Parent)
{
    public static readonly XferGroupView None = new(false, false, false, false, 0, 0, null);
}

/// <summary>
/// Una riga così come la tabella la deve rendere: il dato, il flusso da cui viene, il posto che occupa nel suo
/// blocco di varianti, e le due frasi già composte.
/// <para>Il calcolo sta nella <b>pagina</b> e non nel componente di proposito: la vista a gruppo e la vista a
/// elenco costruiscono l'elenco in modo diverso (una scorre un flusso, l'altra li attraversa tutti), ma
/// producono la stessa cosa — e da lì in giù il codice che disegna è uno solo.</para>
/// </summary>
/// <param name="Flow">Il gruppo a cui la riga appartiene: serve alla vista a elenco per le colonne di contesto.</param>
/// <param name="Point">La riga.</param>
/// <param name="Group">Dove sta nel proprio blocco di varianti.</param>
/// <param name="Preview">La frase che il documento renderà, o <c>null</c> se le anteprime sono spente.</param>
/// <param name="FacetSummary">Il riassunto della faccetta trasferimento, vuoto se la riga non la usa.</param>
public sealed record XferTableRow(
    TransferFlowRow Flow,
    TransferPointRow Point,
    XferGroupView Group,
    string? Preview,
    string FacetSummary)
{
    public bool HasPreview => !string.IsNullOrWhiteSpace(Preview);
}

/// <summary>
/// Le colonne che si scrivono <b>dentro la tabella</b>. Sono tre e non sette di proposito: condizione e faccetta
/// sono composte da più campi, e comprimerle in una casella è l'errore che questa pagina ha già fatto una volta
/// — la riga che diventava «una fila di sei controlli senza etichetta». Quelle restano nel pannello, dove i
/// campi hanno un nome e lo spazio per starci.
/// </summary>
public enum XferCell
{
    /// <summary>Il punto di trasferimento. Identità dell'accordo: si propaga a tutto il gruppo di varianti.</summary>
    Cop,

    /// <summary>Il livello autorizzato, scritto come si legge («FL130-»): lo rilegge <c>LevelFormatting.Parse</c>.</summary>
    Level,

    /// <summary>Il settore ricevente, per callsign. Vuoto = UNICOM. Anch'esso si propaga al gruppo.</summary>
    Receiver,
}

/// <summary>Quale casella di quale riga: la coordinata di una cella nella tabella.</summary>
public readonly record struct XferCellRef(int PointId, XferCell Cell);

/// <summary>Etichette localizzate condivise fra la pagina e i suoi componenti: un solo <c>switch</c> per tipo di
/// flusso, invece di uno nella pagina e uno nella tabella che possono divergere.</summary>
public static class XferLabels
{
    public static string Kind(IStringLocalizer localizer, TransferFlowKind kind) => kind switch
    {
        TransferFlowKind.Arrival => localizer["Xfer_KindArrival"],
        TransferFlowKind.Departure => localizer["Xfer_KindDeparture"],
        TransferFlowKind.Overflight => localizer["Xfer_KindOverflight"],
        TransferFlowKind.Vfr => localizer["Xfer_KindVfr"],
        _ => localizer["Xfer_KindOther"],
    };
}
