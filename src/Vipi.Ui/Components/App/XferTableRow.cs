using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Come si legge una clausola dentro il suo gruppo di varianti. Le clausole del gruppo sono lo <b>stesso
/// accordo</b> in casi diversi — il salvataggio propaga i punti a tutte — e a schermo devono leggersi come un
/// blocco.
/// <para>Il ricevente non è più fra le cose che si propagano, e non è una svista: è il lato opposto
/// dell'accordo nel verso della sezione, quindi non può divergere fra sorelle nemmeno volendo.</para>
/// </summary>
/// <param name="InGroup">La clausola appartiene a un gruppo di varianti.</param>
/// <param name="First">È la prima del blocco <b>come si vede</b> (la guida verticale deve aprirsi qui).</param>
/// <param name="Last">È l'ultima del blocco come si vede.</param>
/// <param name="Odd">Gruppo di indice dispari: due tinte alternate, altrimenti due blocchi adiacenti si fondono.</param>
/// <param name="Count">Quante clausole ha il gruppo nella sezione (non nella vista): è il numero che si annuncia.</param>
/// <param name="Depth">Profondità nell'outline: 0 = alternativa pari-grado, &gt; 0 = eccezione.</param>
/// <param name="Parent">La clausola di cui questa è l'eccezione, letta sull'ordine SALVATO.</param>
public sealed record XferGroupView(bool InGroup, bool First, bool Last, bool Odd, int Count, int Depth,
                                   AgreementClauseRow? Parent)
{
    public static readonly XferGroupView None = new(false, false, false, false, 0, 0, null);
}

/// <summary>
/// Una clausola così come la tabella la deve rendere: il dato, la sezione e l'accordo da cui viene, il posto
/// che occupa nel suo blocco di varianti, e le due frasi già composte.
/// <para>Il calcolo sta nella <b>pagina</b> e non nel componente di proposito: la vista ad accordo e la vista a
/// elenco costruiscono l'elenco in modo diverso (una scorre una sezione, l'altra attraversa tutti gli accordi),
/// ma producono la stessa cosa — e da lì in giù il codice che disegna è uno solo.</para>
/// </summary>
/// <param name="Agreement">L'accordo: porta i due capi, che è ciò che le colonne di contesto mostrano.</param>
/// <param name="Section">La sezione: porta il traffico, il verso e gli aeroporti della riga.</param>
/// <param name="Clause">La clausola.</param>
/// <param name="Group">Dove sta nel proprio blocco di varianti.</param>
/// <param name="Preview">La frase che il documento renderà, o <c>null</c> se le anteprime sono spente.</param>
/// <param name="FacetSummary">Il riassunto della faccetta trasferimento, vuoto se la clausola non la usa.</param>
public sealed record XferTableRow(
    AgreementRow Agreement,
    AgreementSectionRow Section,
    AgreementClauseRow Clause,
    XferGroupView Group,
    string? Preview,
    string FacetSummary)
{
    public bool HasPreview => !string.IsNullOrWhiteSpace(Preview);
}

/// <summary>
/// Le colonne che si scrivono <b>dentro la tabella</b>. Sono due, non tre: il ricevente se n'è andato con il
/// modello vecchio — è dell'accordo, e si scrive nella sua testata insieme al mittente, dove i due capi si
/// leggono uno accanto all'altro invece che uno per riga.
/// <para>Condizione e faccetta restano nel pannello, dove i campi hanno un nome e lo spazio per starci:
/// comprimerle in una casella è l'errore che questa pagina ha già fatto una volta.</para>
/// </summary>
public enum XferCell
{
    /// <summary>I punti d'ingresso, in elenco («BIRSU, TOPNO»). Identità dell'accordo dentro il gruppo di
    /// varianti: si propagano a tutte le sorelle.</summary>
    Points,

    /// <summary>Il livello autorizzato, scritto come si legge («FL130-»): lo rilegge <c>LevelFormatting.Parse</c>.</summary>
    Level,
}

/// <summary>Quale casella di quale clausola: la coordinata di una cella nella tabella.</summary>
public readonly record struct XferCellRef(int ClauseId, XferCell Cell);

/// <summary>Etichette localizzate condivise fra la pagina e i suoi componenti: un solo <c>switch</c> per tipo di
/// traffico, invece di uno nella pagina e uno nella tabella che possono divergere.</summary>
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

    /// <summary>Un capo dell'accordo.</summary>
    public static string Side(AgreementRow a, AgreementSide side) => a.Side(side).Callsign;

    /// <summary>Chi cede, nel verso della sezione.</summary>
    public static string Sender(AgreementRow a, AgreementSectionRow s) => a.Sender(s.Direction).Callsign;

    /// <summary>Chi riceve, nel verso della sezione.</summary>
    public static string Receiver(AgreementRow a, AgreementSectionRow s) => a.Receiver(s.Direction).Callsign;

    /// <summary>Gli aeroporti della sezione in una riga sola («LIRF · LIRA · LIRU»); vuoto se non ne ha.</summary>
    public static string Airports(AgreementSectionRow s) => s.AirportsLabel;

    /// <summary>Come si legge una sezione: il traffico e i suoi scali.</summary>
    public static string Section(IStringLocalizer localizer, AgreementSectionRow s)
    {
        var kind = Kind(localizer, s.Kind);
        var apts = s.AirportsLabel;
        return apts.Length > 0 ? $"{kind} · {apts}" : kind;
    }
}
