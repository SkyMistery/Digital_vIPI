using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// La lista <b>«Da fare»</b>: tutto ciò che aspetta una persona, da qualunque delle due parti arrivi — le
/// segnalazioni che il sistema ha aperto su un documento e gli incarichi che qualcuno ha scritto.
///
/// <para><b>Non è un terzo meccanismo</b>, ed è tutto il punto della carta
/// (<c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c>). <c>DocumentImpact</c> ed <c>EditorTask</c>
/// restano quello che sono e continuano a comportarsi come si comportavano: uno è un <b>fatto</b> che smette
/// da sé quando smette d'essere vero, l'altro è un <b>impegno</b> che chiude una persona. Qui si leggono
/// tutt'e due e si mostra una lista sola.</para>
///
/// <para>⚠️ <b>Una query per pagina, non una per riga</b> (regola 136). L'elenco dei documenti gestiti arriva
/// in un colpo e si indicizza per Id; le ACC su cui l'utente ha una concessione si chiedono <b>una</b> volta.
/// La strada ovvia — <c>CanEditDocumentAsync</c> riga per riga — costerebbe due interrogazioni per voce.</para>
/// </summary>
public interface IWorkListService
{
    /// <summary>
    /// Tutto ciò che l'utente corrente può prendere in mano, ordinato per urgenza: gli admin vedono tutto,
    /// gli altri i documenti delle ACC su cui hanno una concessione, più i propri incarichi.
    /// </summary>
    Task<IReadOnlyList<WorkItem>> MieAsync(CancellationToken ct = default);

    /// <summary>Che cosa resta da fare su <b>un</b> documento: è il banner in cima all'editor.</summary>
    Task<IReadOnlyList<WorkItem>> PerDocumentoAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// Trasforma una segnalazione del sistema in un <b>incarico</b> assegnato a qualcuno. La segnalazione
    /// resta dov'è e continua a essere la verità su <i>se il fatto è ancora vero</i>: l'incarico ne porta
    /// l'Id (<see cref="EditorTask.FromImpactId"/>) e per questo la lista non mostra due volte lo stesso
    /// lavoro. Ritorna l'Id dell'incarico creato.
    /// </summary>
    Task<int> PrendiInCaricoAsync(int impactId, int assegnatarioId, string? assegnatarioNome,
        string? scadenzaCiclo, CancellationToken ct = default);
}

/// <inheritdoc cref="IWorkListService"/>
public sealed class WorkListService : IWorkListService
{
    private readonly IDocumentImpactRepository _impatti;
    private readonly IEditorTaskRepository _incarichi;
    private readonly IDocumentAdminService _documenti;
    private readonly IDocRoutesRegistry _rotte;
    private readonly IEditAuthorizationService _authz;
    private readonly IEditGrantRepository _grants;

    // ⚠️ Iniettato per UN metodo puro, `IsOverdue`, e non per le sue liste — quelle chiamano EnsureAdmin.
    // Ricalcolare qui «in ritardo» sarebbe una seconda lettura dello stesso metro: due posti che decidono
    // la stessa cosa sono due racconti che iniziano a divergere, ed e' gia' costato in questo progetto.
    private readonly IEditorTaskService _regoleIncarichi;

    public WorkListService(IDocumentImpactRepository impatti, IEditorTaskRepository incarichi,
        IDocumentAdminService documenti, IDocRoutesRegistry rotte, IEditAuthorizationService authz,
        IEditGrantRepository grants, IEditorTaskService regoleIncarichi)
    {
        _impatti = impatti;
        _incarichi = incarichi;
        _documenti = documenti;
        _rotte = rotte;
        _authz = authz;
        _grants = grants;
        _regoleIncarichi = regoleIncarichi;
    }

    public async Task<IReadOnlyList<WorkItem>> MieAsync(CancellationToken ct = default)
    {
        var io = _authz.CurrentUserId;
        if (io is null) return Array.Empty<WorkItem>();

        var gestiti = await _documenti.ListAsync(ct);
        var perDoc = IndicizzaPerDocumento(gestiti);

        // Le ACC che posso toccare: UNA domanda, non una per riga. Per un admin la domanda non si fa
        // nemmeno — vede tutto, e chiederlo sarebbe una query per rispondere «sì» comunque.
        var mieAcc = _authz.IsAdmin
            ? null
            : (await _grants.ListAccCodesForUserAsync(io.Value, ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool PossoToccare(ManagedDoc? d) =>
            mieAcc is null || (d?.AccCode is { } acc && mieAcc.Contains(acc));

        var righe = new List<WorkItem>();

        // ── Le segnalazioni del sistema ───────────────────────────────────────────────────────────────
        var incarichiTutti = await _incarichi.ListAllAsync(ct);

        // ⚠️ Gli impatti già presi in carico NON si ripetono: comparirebbero due volte, una come fatto e una
        // come impegno, e chi legge penserebbe di avere il doppio del lavoro.
        var presiInCarico = incarichiTutti
            .Where(t => t.FromImpactId is not null && t.Status != EditorTaskStatus.Done)
            .Select(t => t.FromImpactId!.Value)
            .ToHashSet();

        var aperti = await _impatti.ListAllOpenAsync(ct);
        var perId = aperti.ToDictionary(i => i.Id);

        foreach (var i in aperti)
        {
            if (presiInCarico.Contains(i.Id)) continue;
            perDoc.TryGetValue(i.DocumentId, out var doc);
            if (!PossoToccare(doc)) continue;
            righe.Add(DaImpatto(i, doc));
        }

        // ── Gli incarichi ─────────────────────────────────────────────────────────────────────────────
        // I miei sempre; quelli di altri solo se sono admin — a un editor la lista deve dire che cosa
        // aspetta LUI, non fargli da cruscotto sul lavoro dei colleghi.
        foreach (var t in incarichiTutti)
        {
            if (t.Status == EditorTaskStatus.Done) continue;
            var mio = t.AssigneeUserId == io.Value;
            if (!mio && !_authz.IsAdmin) continue;

            var doc = Documento(gestiti, t);

            // Un incarico LIBERO non ha documento e quindi nessuna ACC: è un promemoria personale, e lo
            // vede chi ce l'ha. Filtrarlo per ACC lo farebbe sparire proprio a chi deve farlo.
            if (doc is not null && !PossoToccare(doc)) continue;

            righe.Add(DaIncarico(t, doc, Origine(perId, t)));
        }

        return WorkOrdering.Ordina(righe);
    }

    public async Task<IReadOnlyList<WorkItem>> PerDocumentoAsync(int documentId, CancellationToken ct = default)
    {
        var gestiti = await _documenti.ListAsync(ct);
        var doc = gestiti.FirstOrDefault(d => d.DocumentId == documentId);

        var righe = new List<WorkItem>();

        var incarichi = await _incarichi.ListAllAsync(ct);
        var presiInCarico = incarichi
            .Where(t => t.FromImpactId is not null && t.Status != EditorTaskStatus.Done)
            .Select(t => t.FromImpactId!.Value)
            .ToHashSet();

        var aperti = await _impatti.ListOpenAsync(documentId, ct);
        var perId = aperti.ToDictionary(i => i.Id);

        foreach (var i in aperti)
        {
            if (presiInCarico.Contains(i.Id)) continue;
            righe.Add(DaImpatto(i, doc));
        }

        if (doc is not null)
        {
            foreach (var t in incarichi)
            {
                if (t.Status == EditorTaskStatus.Done) continue;
                if (t.TargetType != doc.ReleaseTarget) continue;
                if (!string.Equals(t.TargetKey, doc.ReleaseKey, StringComparison.OrdinalIgnoreCase)) continue;
                righe.Add(DaIncarico(t, doc, Origine(perId, t)));
            }
        }

        return WorkOrdering.Ordina(righe);
    }

    public async Task<int> PrendiInCaricoAsync(int impactId, int assegnatarioId, string? assegnatarioNome,
        string? scadenzaCiclo, CancellationToken ct = default)
    {
        // Assegnare lavoro a qualcuno è un atto di regia: lo fa chi può già assegnare incarichi.
        _authz.EnsureAdmin();

        var i = await _impatti.GetOpenAsync(impactId, ct)
            ?? throw new ValidationException(Lingua("Questa segnalazione non è più aperta.", "This notice is no longer open."));

        var doc = (await _documenti.ListAsync(ct)).FirstOrDefault(d => d.DocumentId == i.DocumentId);

        // ⚠️ Il titolo dell'incarico NON è la frase dell'impatto: quella è una chiave di localizzazione con
        // i suoi argomenti, e scritta qui diventerebbe testo fisso in una lingua sola. L'incarico porta il
        // titolo del documento e il rimando all'impatto, che la frase se la ricompone da sé a ogni lettura.
        var input = new EditorTaskInput(
            Title: i.DocumentTitle,
            Description: null,
            AssigneeUserId: assegnatarioId,
            AssigneeName: assegnatarioNome,
            // Ciò che è già in pubblico, o rotto, non nasce «normale»: la priorità la sa già l'impatto, e
            // farla riscegliere a chi assegna sarebbe chiedere due volte una cosa già decisa.
            Priority: i.Kind.IsRotto() || i.IsPublicNow ? EditorTaskPriority.High : EditorTaskPriority.Normal,
            DueAiracCycle: scadenzaCiclo,
            TargetType: doc?.ReleaseTarget,
            TargetKey: doc?.ReleaseKey,
            TargetLabel: doc?.Title ?? i.DocumentTitle,
            FromImpactId: impactId);

        return await _incarichi.AddAsync(input, _authz.CurrentUserId ?? 0, ct);
    }

    // ── Traduzione ───────────────────────────────────────────────────────────────────────────────────

    private WorkItem DaImpatto(DocumentImpactRow i, ManagedDoc? doc) => new(
        WorkOrigin.Sistema,
        $"imp:{i.Id}",
        i.DocumentId,
        i.DocumentTitle,
        doc?.AccCode,
        Url(doc),
        i.ReasonKey,
        i.ReasonArgs,
        i.Kind.Severita(i.IsPublicNow),
        i.Kind.AzioneCheChiude(),
        i.RaisedUtc,
        ImpactId: i.Id);

    /// <summary>La segnalazione da cui l'incarico è nato, se è ancora aperta. <c>null</c> = incarico scritto
    /// da una persona, o segnalazione nel frattempo chiusa.</summary>
    private static DocumentImpactRow? Origine(IReadOnlyDictionary<int, DocumentImpactRow> perId, EditorTask t) =>
        t.FromImpactId is int id && perId.TryGetValue(id, out var i) ? i : null;

    /// <param name="origine">
    /// ⚠️ Quando l'incarico nasce da una segnalazione, <b>frase e urgenza restano quelle della
    /// segnalazione</b>, e l'incarico aggiunge solo <i>chi</i> e <i>entro quando</i>. Senza, prendere in
    /// carico un lavoro lo <b>peggiorava</b>: la riga perdeva il motivo — a schermo si leggeva due volte il
    /// titolo del documento, «vLOA LIBB ↔ LGGG · vLOA LIBB ↔ LGGG» — e scivolava in fondo alla lista, perché
    /// un incarico a priorità normale urge meno di una copia da ripubblicare. Assegnare un lavoro non lo
    /// rende meno urgente né meno comprensibile.
    /// </param>
    private WorkItem DaIncarico(EditorTask t, ManagedDoc? doc, DocumentImpactRow? origine = null)
    {
        var inRitardo = _regoleIncarichi.IsOverdue(t);

        var suaUrgenza = inRitardo ? WorkSeverity.InRitardo
            : t.Priority == EditorTaskPriority.High ? WorkSeverity.Rotto
            : WorkSeverity.Normale;

        // Fra le due vince la più urgente, che nella scala è la MINORE: una scadenza scaduta batte la
        // deriva, e la deriva batte un incarico ordinario.
        var urgenza = origine is { } o
            ? (WorkSeverity)Math.Min((int)suaUrgenza, (int)o.Kind.Severita(o.IsPublicNow))
            : suaUrgenza;

        return new WorkItem(
            WorkOrigin.Persona,
            $"task:{t.Id}",
            doc?.DocumentId,
            doc?.Title ?? t.TargetLabel ?? t.Title,
            doc?.AccCode,
            Url(doc),
            // Dalla segnalazione la frase arriva come CHIAVE + argomenti e resta traducibile. Solo il titolo
            // scritto a mano da una persona passa per `Work_Raw`, che dice alla UI di stamparlo com'è.
            origine?.ReasonKey ?? WorkPhrases.Raw,
            origine?.ReasonArgs ?? new[] { t.Title },
            urgenza,
            WorkAction.CambiaStato,
            t.CreatedUtc,
            t.AssigneeUserId,
            t.AssigneeName,
            t.DueAiracCycle,
            inRitardo,
            ImpactId: t.FromImpactId,
            TaskId: t.Id,
            Stato: t.Status);
    }

    /// <summary>Dove si va a lavorare. <c>null</c> quando il documento non è raggiungibile — e la riga resta
    /// in lista lo stesso: sparire sarebbe peggio che comparire senza collegamento.</summary>
    private string? Url(ManagedDoc? d) =>
        d is null ? null
        : _rotte.For(d.ReleaseTarget).EditorUrl(
            (d.AccCode ?? "").ToLowerInvariant(), d.ReleaseKey, d.NeighbourCode, d.DocumentId);

    /// <summary>Il documento collegato a un incarico, per la coppia <c>(tipo, chiave)</c> — le stesse chiavi
    /// delle release. <c>null</c> = incarico libero, o documento che non c'è più.</summary>
    private static ManagedDoc? Documento(IReadOnlyList<ManagedDoc> gestiti, EditorTask t) =>
        t.TargetType is { } tipo && !string.IsNullOrWhiteSpace(t.TargetKey)
            ? gestiti.FirstOrDefault(d => d.ReleaseTarget == tipo
                && string.Equals(d.ReleaseKey, t.TargetKey, StringComparison.OrdinalIgnoreCase))
            : null;

    private static Dictionary<int, ManagedDoc> IndicizzaPerDocumento(IReadOnlyList<ManagedDoc> gestiti)
    {
        var m = new Dictionary<int, ManagedDoc>();
        foreach (var d in gestiti)
            if (d.DocumentId is int id)
                m[id] = d;
        return m;
    }
}

/// <summary>Chiavi di frase che non vengono dal <c>.resx</c>.</summary>
public static class WorkPhrases
{
    /// <summary>
    /// «Il primo argomento è già testo, stampalo com'è». Serve agli <b>incarichi</b>, il cui titolo l'ha
    /// scritto una persona: cercarlo fra le chiavi di localizzazione lo restituirebbe identico ma dopo un
    /// giro a vuoto, e un titolo che per caso somigliasse a una chiave verrebbe tradotto.
    /// </summary>
    public const string Raw = "__raw__";
}
