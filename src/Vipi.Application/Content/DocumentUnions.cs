using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>Un documento dentro un'unione, con la sua identità già risolta.</summary>
/// <param name="MemberId">La riga di appartenenza: è ciò che si sposta e si toglie.</param>
/// <param name="Order">La posizione, 0-based.</param>
/// <param name="IsHost">È il primo: pagina ed editor dell'unione vivono al suo indirizzo.</param>
/// <param name="Doc">Titolo, famiglia, chiave di release, ACC e stato del lock — dai descrittori, non a mano.</param>
public sealed record UnionMemberView(int MemberId, int Order, bool IsHost, ManagedDoc Doc)
{
    public int DocumentId => Doc.DocumentId!.Value;
}

/// <summary>
/// Un'unione vista da chi la mostra: i membri in ordine, il primo è l'ospite.
/// </summary>
public sealed record UnionView(int Id, IReadOnlyList<UnionMemberView> Members)
{
    /// <summary>Il membro al cui indirizzo vive la pagina unita.</summary>
    public UnionMemberView Host => Members[0];

    /// <summary>Il membro che porta questo documento, se c'è.</summary>
    public UnionMemberView? Of(int documentId) => Members.FirstOrDefault(m => m.DocumentId == documentId);

    /// <summary>Vero se questo documento è l'ospite: chi non lo è, in pubblico, reindirizza qui.</summary>
    public bool IsHostDocument(int documentId) => Host.DocumentId == documentId;

    /// <summary>
    /// Vero se l'ospite è il documento di questa famiglia e questa chiave — la domanda che si fa un
    /// <b>viewer</b>, che conosce il proprio bersaglio e non l'id del proprio documento.
    /// <para>⚠️ Servono TUTTE E DUE: la chiave da sola non basta. Un aeroporto e il suo vSOP militare hanno
    /// la <b>stessa</b> chiave di release (l'ICAO) e si distinguono per il tipo — è il fatto su cui poggiano
    /// le due edizioni con cicli AIRAC indipendenti. Confrontare la sola chiave farebbe credere ospite anche
    /// l'edizione che ospite non è.</para>
    /// </summary>
    public bool IsHostTarget(ReleaseTargetType type, string key) =>
        Host.Doc.ReleaseTarget == type
        && string.Equals(Host.Doc.ReleaseKey, key, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Un documento che si può unire a quello che si sta redigendo.</summary>
/// <param name="StessoScalo">Ha la stessa testa di codice (ICAO o testa del callsign): sono i candidati
/// veri, e vanno in cima all'elenco.</param>
public sealed record UnionCandidate(ManagedDoc Doc, bool StessoScalo)
{
    public int DocumentId => Doc.DocumentId!.Value;
}

/// <summary>
/// Le unioni di documenti (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>): due o più documenti che
/// si leggono in una pagina, si redigono da un editor e si pubblicano con un gesto.
///
/// <para>
/// ⚠️ <b>L'unione non è un tipo di documento</b>, è una relazione fra documenti che restano quello che sono.
/// È ciò che la rende indipendente dalla famiglia: nessun <c>ReleaseTargetType</c> nuovo, nessun profilo di
/// sezioni nuovo, nessun descrittore in più da registrare.
/// </para>
/// </summary>
public interface IDocumentUnionService
{
    /// <summary>
    /// L'unione a cui appartiene questo documento, o null. ⚠️ <b>Non autorizza</b>: la legge anche il
    /// viewer pubblico, che è il posto da cui si decide se reindirizzare.
    /// </summary>
    Task<UnionView?> ForDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// L'unione a cui appartiene il documento di questa famiglia e questa chiave di release, o null.
    ///
    /// <para>È la porta che usano i <b>viewer</b>: una pagina conosce il proprio bersaglio (ICAO, callsign),
    /// non l'id del suo documento. ⚠️ La chiave → documento la risolve <c>IReleaseTarget</c>, che è la
    /// risoluzione che esiste già: scriverne una qui sarebbe la sesta, e le prime cinque hanno insegnato
    /// come vanno a finire.</para>
    /// </summary>
    Task<UnionView?> ForTargetAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>Unisce <paramref name="invitatoDocumentId"/> a <paramref name="ospiteDocumentId"/>, in coda.
    /// Ritorna l'id dell'unione — nuova, o quella che l'ospite aveva già.</summary>
    Task<int> UniscoAsync(int ospiteDocumentId, int invitatoDocumentId, CancellationToken ct = default);

    /// <summary>Toglie un membro. Se ne resta uno solo, l'unione si scioglie: unire un documento a sé stesso
    /// non è uno stato che qualcuno abbia chiesto.</summary>
    Task RimuoviMembroAsync(int memberId, CancellationToken ct = default);

    /// <summary>Scioglie l'unione. I documenti tornano alle loro pagine, senza perdere niente.</summary>
    Task SciogliAsync(int unionId, CancellationToken ct = default);

    /// <summary>Sposta un membro di una posizione: <paramref name="delta"/> −1 su, +1 giù. Ai bordi non fa niente.</summary>
    Task SpostaAsync(int memberId, int delta, CancellationToken ct = default);

    /// <summary>I documenti che si possono unire a questo: famiglia ammessa, non già uniti altrove, non sé
    /// stesso. Quelli dello <b>stesso scalo</b> per primi.</summary>
    Task<IReadOnlyList<UnionCandidate>> CandidatiAsync(int documentId, CancellationToken ct = default);

    /// <summary><inheritdoc cref="CandidatiAsync" path="/summary"/> Per famiglia e chiave, che è quel che
    /// una pagina ha in mano.</summary>
    Task<IReadOnlyList<UnionCandidate>> CandidatiPerTargetAsync(ReleaseTargetType type, string key,
                                                                CancellationToken ct = default);

    /// <summary>
    /// L'id del documento di questa famiglia e questa chiave, o null.
    /// <para>⚠️ È la sola porta che una pagina deve usare per la domanda «qual è il mio documento»: dietro
    /// c'è <c>IReleaseTarget.ResolveDocumentIdAsync</c>, cioè la risoluzione che esiste già. Le prime cinque
    /// scritte a mano hanno insegnato come vanno a finire.</para>
    /// </summary>
    Task<int?> DocumentIdAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>
    /// Tutte le appartenenze, in una lettura sola: serve a chi mostra un ELENCO di documenti e deve dire
    /// quali sono uniti.
    /// <para>⚠️ Una lettura sola e non una per riga: l'elenco unificato ha già pagato due volte il difetto
    /// N+1, e qui le righe sono poche — la divisione ha una manciata di unioni, non una per documento.</para>
    /// </summary>
    Task<IReadOnlyList<UnionRow>> TutteAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentUnionService"/>
public sealed class DocumentUnionService : IDocumentUnionService
{
    private readonly IDocumentUnionRepository _repo;
    private readonly IDocumentAdminRepository _docs;
    private readonly IEditAuthorizationService _authz;
    private readonly IReleaseTargetRegistry _targets;

    public DocumentUnionService(IDocumentUnionRepository repo, IDocumentAdminRepository docs,
                                IEditAuthorizationService authz, IReleaseTargetRegistry targets)
    {
        _repo = repo;
        _docs = docs;
        _authz = authz;
        _targets = targets;
    }

    /// <summary>
    /// Le famiglie che possono stare in un'unione, <b>e il perché di ogni assenza</b> — un elenco senza le
    /// ragioni è un elenco che il prossimo allarga per simmetria.
    ///
    /// <list type="bullet">
    /// <item><c>AccVipi</c> è fuori: è l'unica famiglia <b>a blocchi</b> e non passa né da
    /// <c>DocumentSectionsView</c> né da <c>DocumentToc</c>. Portarla dentro vuol dire cambiare il modello che
    /// la sua pagina ha in mano, ed è un lavoro suo.</item>
    /// <item><c>Vloa</c> è fuori per ora: il suo viewer disegna da sé le due direzioni dei coordinamenti
    /// (<c>SlotsOf</c>), quindi il suo corpo non è ancora un componente montabile altrove. Tecnicamente
    /// entrerebbe; nessuna delle combinazioni chieste la usa.</item>
    /// <item><c>AppMil</c> è fuori perché <b>non esiste</b>: non ha pagine (<c>AppMilDocRoutes</c> torna
    /// tutto null) e soprattutto <b>non ha un <c>IFrozenSectionProvider</c></b>. ⚠️ Un membro senza provider
    /// si pubblicherebbe <b>senza congelare niente</b>, in silenzio — <c>FrozenSectionRegistry</c> per un tipo
    /// non registrato risponde <c>Empty</c> e non protesta. È il difetto già pagato con <c>AirportMil</c>.</item>
    /// </list>
    /// </summary>
    public static readonly IReadOnlySet<ReleaseTargetType> FamiglieAmmesse = new HashSet<ReleaseTargetType>
    {
        ReleaseTargetType.Airport,
        ReleaseTargetType.AirportMil,
        ReleaseTargetType.App,
    };

    public async Task<UnionView?> ForDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var righe = await _repo.ByDocumentAsync(documentId, ct).ConfigureAwait(false);
        return righe.Count == 0 ? null : await ProiettaAsync(righe, ct).ConfigureAwait(false);
    }

    public async Task<UnionView?> ForTargetAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        // ⚠️ Un bersaglio senza documento non è un errore: un aeroporto senza vIPI, un APP mai scritto. La
        // risposta è «nessuna unione», che è quel che il chiamante deve sapere.
        var id = await DocumentIdAsync(type, key, ct).ConfigureAwait(false);
        return id is null ? null : await ForDocumentAsync(id.Value, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<UnionRow>> TutteAsync(CancellationToken ct = default) => _repo.ListAsync(ct);

    public async Task<int?> DocumentIdAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var id = await _targets.For(type).ResolveDocumentIdAsync(key, ct).ConfigureAwait(false);
        // ⚠️ Lo zero non è un id: è quel che tornano le proiezioni EF su `int?` quando la riga non c'è e
        // il campo è un `int` non nullabile a valle. Trattarlo come un documento porterebbe a cercare
        // un'unione del documento #0.
        return id is null or 0 ? null : id;
    }

    public async Task<IReadOnlyList<UnionCandidate>> CandidatiPerTargetAsync(
        ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var id = await DocumentIdAsync(type, key, ct).ConfigureAwait(false);
        return id is null ? Array.Empty<UnionCandidate>() : await CandidatiAsync(id.Value, ct).ConfigureAwait(false);
    }

    public async Task<int> UniscoAsync(int ospiteDocumentId, int invitatoDocumentId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        if (ospiteDocumentId == invitatoDocumentId)
            throw new Aor.ValidationException(Lingua(
                "Un documento non si unisce a sé stesso.",
                "A document cannot be joined to itself."));

        var descritti = await _docs.DescribeAsync(new[] { ospiteDocumentId, invitatoDocumentId }, ct)
                                   .ConfigureAwait(false);
        var ospite = Esigi(descritti, ospiteDocumentId);
        var invitato = Esigi(descritti, invitatoDocumentId);
        EsigiFamigliaAmmessa(ospite);
        EsigiFamigliaAmmessa(invitato);

        // ⚠️ Il controllo è QUI e non solo sull'indice unico: la violazione dell'indice arriva come una
        // DbUpdateException che la pagina non sa raccontare, e chi ha premuto vedrebbe un errore tecnico al
        // posto del nome del documento con cui il suo è già unito.
        var giaUnito = await _repo.ByDocumentAsync(invitatoDocumentId, ct).ConfigureAwait(false);
        if (giaUnito.Count > 0)
            throw new Aor.ValidationException(Lingua(
                $"«{invitato.Title}» è già unito ad altri documenti: va prima staccato da lì.",
                $"“{invitato.Title}” is already joined to other documents: detach it there first."));

        var unioneOspite = await _repo.ByDocumentAsync(ospiteDocumentId, ct).ConfigureAwait(false);
        if (unioneOspite.Count > 0)
        {
            var id = unioneOspite[0].UnionId;
            await _repo.AddMemberAsync(id, invitatoDocumentId, ct).ConfigureAwait(false);
            return id;
        }

        return await _repo.CreateAsync(ospiteDocumentId, invitatoDocumentId, _authz.CurrentUserId ?? 0, ct)
                          .ConfigureAwait(false);
    }

    public async Task RimuoviMembroAsync(int memberId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        await _repo.RemoveMemberAsync(memberId, ct).ConfigureAwait(false);
        // ⚠️ «Un'unione con un membro solo non è un'unione»: se restasse, la pagina unita mostrerebbe un
        // documento sotto l'intestazione di un gruppo, e il redirect continuerebbe a esistere senza avere
        // dove mandare. La regola sta qui, nel dominio, non nella pagina che ha premuto il tasto.
        await _repo.TidyAsync(ct).ConfigureAwait(false);
    }

    public async Task SciogliAsync(int unionId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        await _repo.DissolveAsync(unionId, ct).ConfigureAwait(false);
    }

    public async Task SpostaAsync(int memberId, int delta, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        await _repo.MoveAsync(memberId, delta, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UnionCandidate>> CandidatiAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        var tutti = await _docs.ListAsync(ct).ConfigureAwait(false);
        var mio = tutti.FirstOrDefault(d => d.DocumentId == documentId);
        if (mio is null) return Array.Empty<UnionCandidate>();

        // Chi è già unito da qualche parte non è un candidato — nemmeno se l'unione è la mia: da lì si toglie
        // con il tasto che lo toglie, non riaggiungendolo.
        var occupati = (await _repo.ListAsync(ct).ConfigureAwait(false)).Select(r => r.DocumentId).ToHashSet();

        var testaMia = Testa(mio.ReleaseKey);
        return tutti
            .Where(d => d.DocumentId is int id && id != documentId && !occupati.Contains(id))
            .Where(d => FamiglieAmmesse.Contains(d.ReleaseTarget))
            .Select(d => new UnionCandidate(d, StessoScalo: Testa(d.ReleaseKey) == testaMia))
            // Prima quelli dello stesso scalo — sono la risposta nel 99% dei casi — poi per titolo. L'elenco
            // NON si taglia per ACC: «indipendentemente dal tipo di documento» vuol dire anche «senza un
            // recinto che qualcuno dovrà scavalcare».
            .OrderByDescending(c => c.StessoScalo)
            .ThenBy(c => c.Doc.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// La testa di una chiave di release: l'ICAO per un aeroporto (<c>LIBV</c>), la testa del callsign per un
    /// APP (<c>LIBV_G_APP</c> → <c>LIBV</c>). È la stessa regola di <c>IStationResolver.AirportOfCallsign</c>.
    /// <para>⚠️ Qui è una funzione <b>pura</b> e non una chiamata a quel servizio, di proposito: quella cache
    /// è <c>scoped</c>, cioè vive quanto il circuito, e un elenco di candidati non ha bisogno di sapere se lo
    /// scalo esiste — solo se due chiavi parlano dello stesso posto.</para>
    /// </summary>
    public static string Testa(string releaseKey)
    {
        var k = (releaseKey ?? "").Trim();
        // ⚠️ Una chiave con la barra è quella di una vIPI ACC (`{acc}|{root}`), e NON parla di uno scalo:
        // tagliarla al primo `_` darebbe «LIRR|LIRR», una testa che non è né un ICAO né la chiave intera —
        // cioè un codice che potrebbe combaciare con un altro per caso. Torna sé stessa: combacia solo con sé.
        if (k.Contains('|')) return k.ToUpperInvariant();
        var i = k.IndexOf('_');
        return (i > 0 ? k[..i] : k).ToUpperInvariant();
    }

    private async Task<UnionView> ProiettaAsync(IReadOnlyList<UnionRow> righe, CancellationToken ct)
    {
        var descritti = await _docs.DescribeAsync(righe.Select(r => r.DocumentId).ToList(), ct)
                                   .ConfigureAwait(false);
        var membri = righe
            .OrderBy(r => r.Order)
            // ⚠️ Un membro che nessun descrittore riconosce si SALTA invece di far cadere la pagina: è la
            // stessa risposta che l'elenco unificato dà da sempre. Se ne resta uno solo, `TidyAsync` chiuderà
            // l'unione al primo giro; nel frattempo la pagina mostra ciò che sa mostrare.
            .Where(r => descritti.ContainsKey(r.DocumentId))
            .Select((r, i) => new UnionMemberView(r.MemberId, r.Order, IsHost: i == 0, descritti[r.DocumentId]))
            .ToList();
        return new UnionView(righe[0].UnionId, membri);
    }

    private static ManagedDoc Esigi(IReadOnlyDictionary<int, ManagedDoc> descritti, int documentId) =>
        descritti.TryGetValue(documentId, out var d)
            ? d
            : throw new Aor.ValidationException(Lingua(
                $"Il documento #{documentId} non esiste, o non è di una famiglia riconosciuta.",
                $"Document #{documentId} does not exist, or is not of a recognised family."));

    private static void EsigiFamigliaAmmessa(ManagedDoc d)
    {
        if (FamiglieAmmesse.Contains(d.ReleaseTarget)) return;
        throw new Aor.ValidationException(Lingua(
            $"«{d.Title}» è di una famiglia che non si può ancora unire ({d.ReleaseTarget}).",
            $"“{d.Title}” belongs to a family that cannot be joined yet ({d.ReleaseTarget})."));
    }
}
