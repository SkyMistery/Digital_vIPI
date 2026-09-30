using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>Che cosa scrive chi apre una richiesta. Documento e sezione sono opzionali: esiste la richiesta libera.</summary>
/// <param name="Tipo">La famiglia del documento (come nelle release); con <paramref name="Chiave"/> lo identifica.</param>
/// <param name="Chiave">L'ICAO, il codice dell'ente, il codice dell'ACC, l'id della vLOA — quello che la pagina conosce.</param>
public sealed record FieldRequestInput(ReleaseTargetType? Tipo, string? Chiave, string? SectionKey,
    FieldRequestKind Kind, string Body);

/// <summary>Il documento e la sezione di cui si sta per scrivere, per la testata del modulo.</summary>
public sealed record FieldRequestContext(int DocumentId, string DocumentTitle, string? SectionTitle);

/// <summary>Gli argini (carta §4): contro il pestaggio, non contro l'uso.</summary>
public static class FieldRequestRules
{
    /// <summary>Chi ne ha cinque in attesa non ha bisogno della sesta: ha bisogno di una risposta.</summary>
    public const int MaxAperte = 5;
    public const int MaxAlGiorno = 10;
    public const int MaxCorpo = 2000;
    public const int MaxRisposta = 2000;

    /// <summary>
    /// Per quanto si tiene una richiesta CHIUSA, dalla chiusura (committente, d'accordo con IT-HQ, 30 settembre 2026):
    /// poi la toglie il giro notturno di conservazione. ⚠️ Dalla CHIUSURA e non dalla nascita, e le aperte mai: una
    /// richiesta a cui nessuno ha ancora risposto è lavoro, non spazio occupato.
    /// </summary>
    public const int MesiDiConservazione = 3;
}

/// <summary>
/// Le richieste dal campo (S56, committente, 29 settembre 2026): un utente IVAO connesso dice a chi scrive i documenti
/// che qualcosa è sbagliato o si può migliorare, e riceve una risposta — senza passare dalla posta.
/// Carta <c>docs/design/piano-segnalazioni.md</c> §2–§4.
/// </summary>
public interface IFieldRequestService
{
    /// <summary>Documento e sezione di cui si sta per scrivere; null se la coppia famiglia-chiave non risolve.</summary>
    Task<FieldRequestContext?> ContestoAsync(ReleaseTargetType tipo, string chiave, string? sectionKey, CancellationToken ct = default);

    /// <summary>Apre una richiesta. Solo da connessi; tetti e lunghezza controllati qui, non nella pagina.</summary>
    Task<int> ApriAsync(FieldRequestInput input, CancellationToken ct = default);

    /// <summary>Le richieste dell'utente corrente, con lo stato e la risposta.</summary>
    Task<IReadOnlyList<FieldRequestRow>> MieAsync(CancellationToken ct = default);

    /// <summary>La coda dello staff (Editor in su): le aperte, o tutte.</summary>
    Task<IReadOnlyList<FieldRequestRow>> CodaAsync(bool ancheChiuse = false, CancellationToken ct = default);

    /// <summary>Le aperte su un documento: il banner in cima al suo editor.</summary>
    Task<IReadOnlyList<FieldRequestRow>> ApertePerDocumentoAsync(int documentId, CancellationToken ct = default);

    /// <summary>La prende in carico chi preme: nasce un incarico suo in «Da fare», legato alla richiesta.</summary>
    Task<int> PrendiInCaricoAsync(int id, CancellationToken ct = default);

    Task RisolviAsync(int id, string risposta, CancellationToken ct = default);
    Task RespingiAsync(int id, string risposta, CancellationToken ct = default);
    Task DoppioneAsync(int id, int doppioneDi, string? risposta, CancellationToken ct = default);

    /// <summary>
    /// Toglie una richiesta, aperta o chiusa: solo l'Admin (committente, 30 settembre 2026 — le richieste di prova
    /// resterebbero nel sistema per niente). Se aveva un incarico ancora aperto, lo chiude come farebbe una chiusura.
    /// </summary>
    Task EliminaAsync(int id, CancellationToken ct = default);
}

/// <inheritdoc cref="IFieldRequestService"/>
public sealed class FieldRequestService : IFieldRequestService
{
    private readonly IFieldRequestRepository _repo;
    private readonly IEditorTaskRepository _incarichi;
    private readonly IDocumentAdminRepository _documenti;
    private readonly IReleaseTargetRegistry _bersagli;
    private readonly IReleaseRepository _release;
    private readonly IAccDerivationRepository _acc;
    private readonly IEditAuthorizationService _authz;
    private readonly Func<DateTime> _adesso;

    public FieldRequestService(IFieldRequestRepository repo, IEditorTaskRepository incarichi,
        IDocumentAdminRepository documenti, IReleaseTargetRegistry bersagli, IReleaseRepository release,
        IAccDerivationRepository acc, IEditAuthorizationService authz, Func<DateTime>? adesso = null)
    {
        _repo = repo;
        _incarichi = incarichi;
        _documenti = documenti;
        _bersagli = bersagli;
        _release = release;
        _acc = acc;
        _authz = authz;
        _adesso = adesso ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Dalla coppia che la pagina conosce al documento. ⚠️ Per la vIPI ACC la pagina conosce il codice dell'ACC, e
    /// la chiave vera (<c>ACC|CTR radice</c>) la sa l'identità del documento.
    /// </summary>
    private async Task<(int DocumentId, ManagedDoc? Doc)?> RisolviAsync(ReleaseTargetType tipo, string chiave, CancellationToken ct)
    {
        var k = (chiave ?? "").Trim();
        if (k.Length == 0) return null;
        int? id = tipo == ReleaseTargetType.AccVipi && !k.Contains('|')
            ? (await _acc.ResolveAccDocumentIdentityAsync(k.ToUpperInvariant(), ct))?.DocumentId
            : await _bersagli.For(tipo).ResolveDocumentIdAsync(k, ct);
        if (id is not int docId) return null;
        var doc = (await _documenti.ListAsync(ct)).FirstOrDefault(d => d.DocumentId == docId);
        return (docId, doc);
    }

    public async Task<FieldRequestContext?> ContestoAsync(ReleaseTargetType tipo, string chiave, string? sectionKey,
        CancellationToken ct = default)
    {
        if (await RisolviAsync(tipo, chiave, ct) is not { } r) return null;
        var sezione = string.IsNullOrWhiteSpace(sectionKey) ? null : await _repo.SectionTitleAsync(r.DocumentId, sectionKey!, ct);
        return new FieldRequestContext(r.DocumentId, r.Doc?.Title ?? "", sezione);
    }

    public async Task<int> ApriAsync(FieldRequestInput input, CancellationToken ct = default)
    {
        // D1: solo da connessi. Non serve essere staff: serve essere in IVAO.
        var vid = _authz.CurrentUserId
                  ?? throw new Aor.ValidationException(Lingua("Per scrivere una richiesta accedi con IVAO.", "Sign in with IVAO to write a request."));
        var corpo = (input.Body ?? "").Trim();
        if (corpo.Length == 0)
            throw new Aor.ValidationException(Lingua("Scrivi che cosa hai visto.", "Write what you saw."));
        if (corpo.Length > FieldRequestRules.MaxCorpo)
            throw new Aor.ValidationException(Lingua(
                $"Al massimo {FieldRequestRules.MaxCorpo} caratteri.", $"At most {FieldRequestRules.MaxCorpo} characters."));
        if (await _repo.CountOpenAsync(vid, ct) >= FieldRequestRules.MaxAperte)
            throw new Aor.ValidationException(Lingua(
                $"Hai già {FieldRequestRules.MaxAperte} richieste in attesa di risposta: appena lo staff risponde potrai scriverne altre.",
                $"You already have {FieldRequestRules.MaxAperte} requests waiting for a reply: you can write more once the staff answers."));
        if (await _repo.CountSinceAsync(vid, _adesso().AddDays(-1), ct) >= FieldRequestRules.MaxAlGiorno)
            throw new Aor.ValidationException(Lingua(
                $"Hai scritto {FieldRequestRules.MaxAlGiorno} richieste nelle ultime 24 ore: riprova domani.",
                $"You wrote {FieldRequestRules.MaxAlGiorno} requests in the last 24 hours: try again tomorrow."));

        int? docId = null, rilascio = null;
        var sezione = "";
        if (input.Tipo is { } tipo && !string.IsNullOrWhiteSpace(input.Chiave))
        {
            var r = await RisolviAsync(tipo, input.Chiave!, ct)
                    ?? throw new Aor.ValidationException(Lingua("Documento inesistente.", "The document does not exist."));
            docId = r.DocumentId;
            sezione = (input.SectionKey ?? "").Trim();
            if (sezione.Length > 64) sezione = sezione[..64];
            // 🔴 Il rilascio che il lettore aveva davanti è quello in vigore adesso: senza, chi valuta apre la bozza,
            // vede un altro testo e risponde «non c'è nessun errore» mentre in pubblico c'è ancora.
            if (r.Doc is { } d)
                rilascio = (await _release.GetEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, _adesso(), ct))?.VersionNumber;
        }

        return await _repo.AddAsync(new FieldRequest
        {
            ReporterUserId = vid,
            ReporterName = (_authz.CurrentName ?? "").Trim() is { Length: > 0 } n ? (n.Length > 128 ? n[..128] : n) : vid.ToString(),
            CreatedUtc = _adesso(),
            DocumentId = docId,
            SectionKey = sezione,
            ReleaseNumber = rilascio,
            Kind = input.Kind,
            Body = corpo,
        }, ct);
    }

    public async Task<IReadOnlyList<FieldRequestRow>> MieAsync(CancellationToken ct = default) =>
        _authz.CurrentUserId is int vid ? await _repo.ListAsync(reporterUserId: vid, ct: ct) : Array.Empty<FieldRequestRow>();

    public async Task<IReadOnlyList<FieldRequestRow>> CodaAsync(bool ancheChiuse = false, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);   // D3: il triage è di chi possiede il contenuto
        return await _repo.ListAsync(soloAperte: !ancheChiuse, ct: ct);
    }

    public async Task<IReadOnlyList<FieldRequestRow>> ApertePerDocumentoAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        return await _repo.ListAsync(documentId: documentId, soloAperte: true, ct: ct);
    }

    private async Task<FieldRequestRow> ApertaAsync(int id, CancellationToken ct)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var r = await _repo.GetAsync(id, ct)
                ?? throw new Aor.ValidationException(Lingua($"Richiesta {id} inesistente.", $"Request {id} does not exist."));
        if (!r.Aperta)
            throw new Aor.ValidationException(Lingua($"La richiesta {id} è già chiusa.", $"Request {id} is already closed."));
        return r;
    }

    public async Task<int> PrendiInCaricoAsync(int id, CancellationToken ct = default)
    {
        var r = await ApertaAsync(id, ct);
        if (r.TaskId is int gia) return gia;   // presa in carico due volte: lo stesso incarico, non un secondo
        var io = _authz.CurrentUserId ?? 0;
        var doc = r.DocumentId is int d ? (await _documenti.ListAsync(ct)).FirstOrDefault(x => x.DocumentId == d) : null;
        var taskId = await _incarichi.AddAsync(new EditorTaskInput(
            Title: doc?.Title ?? r.DocumentTitle ?? Lingua($"Richiesta #{r.Id}", $"Request #{r.Id}"),
            Description: r.Body,
            AssigneeUserId: io,
            AssigneeName: _authz.CurrentName,
            Priority: r.Kind == FieldRequestKind.Errore ? EditorTaskPriority.High : EditorTaskPriority.Normal,
            DueAiracCycle: null,
            TargetType: doc?.ReleaseTarget,
            TargetKey: doc?.ReleaseKey,
            TargetLabel: doc?.Title ?? r.DocumentTitle,
            FromRequestId: r.Id), io, ct);
        await _repo.SetStatusAsync(id, FieldRequestStatus.PresaInCarico, io, _authz.CurrentName ?? "", "", null, ct);
        return taskId;
    }

    public Task RisolviAsync(int id, string risposta, CancellationToken ct = default) =>
        ChiudiAsync(id, FieldRequestStatus.Risolta, risposta, null, ct);

    public Task RespingiAsync(int id, string risposta, CancellationToken ct = default) =>
        ChiudiAsync(id, FieldRequestStatus.Respinta, risposta, null, ct);

    public async Task DoppioneAsync(int id, int doppioneDi, string? risposta, CancellationToken ct = default)
    {
        if (doppioneDi == id || await _repo.GetAsync(doppioneDi, ct) is null)
            throw new Aor.ValidationException(Lingua($"La richiesta {doppioneDi} non esiste.", $"Request {doppioneDi} does not exist."));
        // Il rimando è già una frase: «doppione di #N». Se chi chiude vuole aggiungere qualcosa, lo aggiunge.
        await ChiudiAsync(id, FieldRequestStatus.Doppione,
            string.IsNullOrWhiteSpace(risposta) ? Lingua($"Doppione della richiesta #{doppioneDi}.", $"Duplicate of request #{doppioneDi}.") : risposta,
            doppioneDi, ct);
    }

    public async Task EliminaAsync(int id, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var r = await _repo.GetAsync(id, ct)
                ?? throw new Aor.ValidationException(Lingua($"Richiesta {id} inesistente.", $"Request {id} does not exist."));
        // Prima l'incarico, poi la richiesta: tolta la richiesta, l'incarico resterebbe in «Da fare» su una domanda che
        // non c'è più.
        if (r.TaskId is int incarico && await _incarichi.GetAsync(incarico, ct) is { Status: not EditorTaskStatus.Done })
            await _incarichi.UpdateStatusAsync(incarico, EditorTaskStatus.Done, _authz.CurrentUserId ?? 0, ct);
        await _repo.DeleteAsync(id, ct);
    }

    /// <summary>D4: ogni chiusura ha una frase. Una richiesta chiusa in silenzio insegna a non chiederne più.</summary>
    private async Task ChiudiAsync(int id, FieldRequestStatus stato, string? risposta, int? doppioneDi, CancellationToken ct)
    {
        await ApertaAsync(id, ct);
        var frase = (risposta ?? "").Trim();
        if (frase.Length == 0)
            throw new Aor.ValidationException(Lingua("Scrivi la risposta: è quello che legge chi l'ha chiesta.",
                "Write the reply: it is what the requester reads."));
        if (frase.Length > FieldRequestRules.MaxRisposta)
            throw new Aor.ValidationException(Lingua(
                $"Al massimo {FieldRequestRules.MaxRisposta} caratteri.", $"At most {FieldRequestRules.MaxRisposta} characters."));
        var r = await _repo.GetAsync(id, ct);
        await _repo.SetStatusAsync(id, stato, _authz.CurrentUserId ?? 0, _authz.CurrentName ?? "", frase, doppioneDi, ct);
        // Chiusa la richiesta si chiude anche il suo incarico (decisione del committente, 29 settembre 2026):
        // altrimenti restava in «Da fare» un impegno su una domanda che ha già la sua risposta.
        if (r?.TaskId is int incarico && await _incarichi.GetAsync(incarico, ct) is { Status: not EditorTaskStatus.Done })
            await _incarichi.UpdateStatusAsync(incarico, EditorTaskStatus.Done, _authz.CurrentUserId ?? 0, ct);
    }
}
