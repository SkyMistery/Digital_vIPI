using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Chi ha messo la riga in lista.</summary>
public enum WorkOrigin
{
    /// <summary>L'ha aperta il sistema: è un <b>fatto</b> su un documento, e smette da sé quando smette d'essere vero.</summary>
    Sistema,

    /// <summary>L'ha scritta una persona: è un <b>impegno</b>, e lo chiude una persona.</summary>
    Persona,
}

/// <summary>
/// Quanto urge, in scala. È anche l'ordine in cui le righe compaiono: chi apre la lista deve trovare in
/// cima ciò che sta facendo danno <b>adesso</b>, non ciò che è arrivato per ultimo.
/// </summary>
public enum WorkSeverity
{
    /// <summary>Il cambio è <b>già in pubblico</b> senza passare da una ripubblicazione: lo legge la gente ora.</summary>
    GiaInPubblico = 0,

    /// <summary>Qualcosa è rotto: il pubblico non trova le release, il bersaglio non risolve.</summary>
    Rotto = 1,

    /// <summary>Un impegno con una scadenza AIRAC già passata.</summary>
    InRitardo = 2,

    /// <summary>La copia pubblicata è indietro: c'è da ripubblicare.</summary>
    DaRipubblicare = 3,

    /// <summary>Il testo va riletto: qualcosa che il documento nomina non c'è più o è cambiato.</summary>
    DaRileggere = 4,

    /// <summary>Un impegno ordinario, senza urgenza dichiarata.</summary>
    Normale = 5,
}

/// <summary>Che cosa <b>chiude</b> una riga. Non è un'etichetta grafica: è la promessa del tasto.</summary>
public enum WorkAction
{
    /// <summary>Una persona rilegge e spunta. L'unica chiusura possibile per un fatto non calcolato.</summary>
    SegnaFatto,

    /// <summary>
    /// Si ripubblica, e il fatto diventa falso da solo. ⚠️ <b>Non</b> si offre un ✓: la riga la riaprirebbe
    /// il giro notturno, e chi l'ha spuntata penserebbe che il tasto sia rotto.
    /// </summary>
    Ripubblica,

    /// <summary>Non si risolve dall'elenco: è una decisione, e si prende nella pagina che la riguarda.</summary>
    VaiASistemare,

    /// <summary>Un incarico: si muove di stato (Todo → InProgress → Done) dove sta.</summary>
    CambiaStato,
}

/// <summary>
/// Una riga di lavoro, qualunque sia la sua natura: una segnalazione che il sistema ha aperto su un
/// documento, o un incarico che una persona ha scritto.
///
/// <para><b>È un read-model, non un'entità</b>, ed è il punto di tutta la carta
/// (<c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c> §1). Di meccanismi che si somigliano ce n'erano
/// già <b>due</b> — <c>DocumentImpact</c> e <c>EditorTask</c> — e la regola §1 del FEATURE-PROCESS dice di
/// estendere o sostituire, mai affiancare. Una terza tabella «Todo» avrebbe fatto tre racconti dove ne
/// bastava uno. Qui non si salva niente: si <b>legge</b> da tutt'e due e si mostra una lista sola.</para>
///
/// <para>⚠️ <b>La frase resta chiave + argomenti</b> fino a schermo, e non diventa mai testo qui dentro. È la
/// regola già pagata dagli impatti: una riga composta in italiano si ripresenterebbe in italiano a chi legge
/// in inglese, e il circuito Blazor cambia lingua <b>senza ricaricare</b>.</para>
/// </summary>
/// <param name="Chiave">Identità stabile della riga (<c>imp:42</c>, <c>task:7</c>): serve al <c>@key</c> di
/// Blazor e a dire su quale riga si è premuto, senza mescolare gli Id di due tabelle diverse.</param>
/// <param name="Titolo">Il titolo del <b>documento</b> su cui si lavora; per un incarico libero, il suo.</param>
/// <param name="Url">Dove si va a lavorare. <c>null</c> = non raggiungibile (documento sparito, o incarico
/// libero): la riga resta in lista, ma senza collegamento — è un'informazione, non un difetto da nascondere.</param>
public sealed record WorkItem(
    WorkOrigin Origine,
    string Chiave,
    int? DocumentId,
    string Titolo,
    string? AccCode,
    string? Url,
    string FraseKey,
    IReadOnlyList<string> FraseArgs,
    WorkSeverity Severita,
    WorkAction Azione,
    DateTime Da,
    int? AssegnatarioId = null,
    string? AssegnatarioNome = null,
    string? ScadenzaCiclo = null,
    bool InRitardo = false,
    int? ImpactId = null,
    int? TaskId = null,
    EditorTaskStatus? Stato = null)
{
    /// <summary>Il ✓ ha senso su questa riga.</summary>
    public bool SiSpunta => Azione == WorkAction.SegnaFatto;

    /// <summary>La riga porta da qualche parte: il tasto «vai» si accende.</summary>
    public bool SiRaggiunge => !string.IsNullOrWhiteSpace(Url);
}

/// <summary>
/// Da un <see cref="ImpactKind"/> a come la sua riga si comporta in lista: quanto urge, e che cosa la chiude.
///
/// <para><b>Perché qui e non accanto a <see cref="ImpactKinds"/></b>, che è dove stanno i fratelli
/// <c>IsCalcolato</c>/<c>IsRotto</c>/<c>IsDaRipubblicare</c>: quelli sono fatti di dominio e vivono in
/// <c>Vipi.Domain</c>, che non conosce — e non deve conoscere — <see cref="WorkAction"/>. Questa è la
/// <b>traduzione</b> di quei fatti in comportamento di lista, e sta di sopra. Non li duplica: li
/// <b>consulta</b>, così la verità resta una sola e questo file non può divergere.</para>
/// </summary>
public static class WorkMapping
{
    /// <summary>Quanto urge una segnalazione di sistema.</summary>
    /// <param name="giaInPubblico">Il documento ha una sezione <i>Live</i> alimentata da questa famiglia: il
    /// cambio è già sotto gli occhi del pubblico senza che nessuno abbia ripubblicato.</param>
    public static WorkSeverity Severita(this ImpactKind kind, bool giaInPubblico) =>
        giaInPubblico ? WorkSeverity.GiaInPubblico
        : kind.IsRotto() ? WorkSeverity.Rotto
        : kind.IsDaRipubblicare() ? WorkSeverity.DaRipubblicare
        : WorkSeverity.DaRileggere;

    /// <summary>
    /// Che cosa chiude la riga. ⚠️ Le <b>calcolate</b> non si spuntano: il giro che le apre le riaprirebbe
    /// stanotte, e un ✓ che non tiene è peggio di nessun ✓. Si offre invece l'atto che rende il fatto falso.
    /// </summary>
    public static WorkAction AzioneCheChiude(this ImpactKind kind) =>
        kind.IsDaRipubblicare() ? WorkAction.Ripubblica
        : kind.IsCalcolato() ? WorkAction.VaiASistemare
        : WorkAction.SegnaFatto;
}

/// <summary>
/// L'ordine della lista, puro e senza IO — così si può fissare nei test senza database, che è l'unico modo
/// per cui una regola di priorità resta quella che si è deciso.
/// </summary>
public static class WorkOrdering
{
    /// <summary>
    /// Prima la severità, poi la più <b>vecchia</b> in cima. ⚠️ Non la più recente: una segnalazione che
    /// nessuno guarda da tre settimane è esattamente quella che va vista, e ordinando per novità
    /// scenderebbe in fondo ogni volta che ne arriva un'altra.
    /// </summary>
    public static IReadOnlyList<WorkItem> Ordina(IEnumerable<WorkItem> righe) =>
        righe.OrderBy(r => (int)r.Severita)
             .ThenBy(r => r.Da)
             .ThenBy(r => r.Chiave, StringComparer.Ordinal)   // stabile: due righe dello stesso istante
             .ToList();
}
