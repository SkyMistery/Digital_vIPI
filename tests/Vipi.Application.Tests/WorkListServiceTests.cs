using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il giro che unisce le due sorgenti. Qui si prova ciò che nessuna delle due metà prova da sola: <b>chi
/// vede che cosa</b>, e che uno stesso lavoro non compaia <b>due volte</b> — una come fatto del sistema e
/// una come impegno di una persona.
///
/// <para>Carta: <c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c> §2/D1-D2-D5.</para>
/// </summary>
public class WorkListServiceTests
{
    private static readonly DateTime Adesso = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    // ── Chi vede che cosa ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L_admin_vede_le_segnalazioni_di_tutte_le_ACC()
    {
        var s = Costruisci(VipiRole.Admin,
            impatti: new[] { Impatto(1, 10), Impatto(2, 20) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR"), Doc(20, "vIPI Milano", "LIMM") });

        var righe = await s.MieAsync();

        Assert.Equal(2, righe.Count);
        Assert.All(righe, r => Assert.Equal(WorkOrigin.Sistema, r.Origine));
    }

    /// <summary>
    /// ⚠️ Fino al 28 agosto 2026 questo test si chiamava «Un_editor_vede_solo_le_ACC_su_cui_ha_la
    /// _concessione» e provava la decisione D2: un editor di LIRR non doveva vedere il lavoro di Milano.
    /// Con la morte delle concessioni per ACC quel «solo» non esiste più — l'Editor edita tutto, quindi il
    /// lavoro di Milano <b>è</b> anche suo. Resta da provare la metà che conta ancora: chi non è Editor non
    /// vede niente.
    /// </summary>
    [Fact]
    public async Task Un_editor_vede_il_lavoro_di_tutte_le_ACC()
    {
        var s = Costruisci(VipiRole.Editor,
            impatti: new[] { Impatto(1, 10), Impatto(2, 20) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR"), Doc(20, "vIPI Milano", "LIMM") });

        var righe = await s.MieAsync();

        // Due righe: quella di Roma e quella di Milano. Prima ne sarebbe arrivata una sola.
        Assert.Equal(2, righe.Count);
    }

    [Fact]
    public async Task Chi_non_e_editor_non_vede_lavoro_da_fare()
    {
        var s = Costruisci(VipiRole.DivisionStaff,
            impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") });

        Assert.Empty(await s.MieAsync());
    }

    [Fact]
    public async Task Un_editor_vede_i_propri_incarichi_e_non_quelli_degli_altri()
    {
        var s = Costruisci(VipiRole.Editor, io: 555,
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[]
            {
                Incarico(1, "il mio", assegnatario: 555),
                Incarico(2, "di un altro", assegnatario: 999),
            });

        var righe = await s.MieAsync();

        Assert.Equal("il mio", Assert.Single(righe).FraseArgs[0]);
    }

    [Fact]
    public async Task Un_incarico_LIBERO_lo_vede_chi_ce_l_ha()
    {
        // Non ha documento, quindi non ha ACC: quando si filtrava per ACC, spariva proprio a chi doveva farlo.
        var s = Costruisci(VipiRole.Editor, io: 555,
            incarichi: new[] { Incarico(1, "comprare il caffè", assegnatario: 555, libero: true) });

        Assert.Single(await s.MieAsync());
    }

    [Fact]
    public async Task Gli_incarichi_conclusi_non_sono_lavoro()
    {
        var s = Costruisci(VipiRole.Admin, io: 555,
            incarichi: new[] { Incarico(1, "fatto ieri", 555, stato: EditorTaskStatus.Done) });

        Assert.Empty(await s.MieAsync());
    }

    [Fact]
    public async Task Senza_utente_la_lista_e_vuota()
    {
        var s = Costruisci(VipiRole.Editor, io: null, impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") });

        Assert.Empty(await s.MieAsync());
    }

    // ── Il ponte: niente doppioni ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Una_segnalazione_presa_in_carico_non_compare_due_volte()
    {
        // ⚠️ Il cuore di D5: senza il rimando `FromImpactId` lo stesso lavoro comparirebbe come fatto E come
        // impegno, e chi legge penserebbe di averne il doppio.
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[] { Incarico(7, "vIPI Roma", 555, daImpatto: 1) });

        var riga = Assert.Single(await s.MieAsync());
        Assert.Equal(WorkOrigin.Persona, riga.Origine);
        Assert.Equal(7, riga.TaskId);
    }

    [Fact]
    public async Task Se_l_incarico_e_concluso_la_segnalazione_torna_a_farsi_vedere()
    {
        // Il fatto è ancora vero: chiudere l'impegno non lo rende falso, e nasconderlo sarebbe perdere il
        // lavoro che resta.
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[] { Incarico(7, "vIPI Roma", 555, daImpatto: 1, stato: EditorTaskStatus.Done) });

        var riga = Assert.Single(await s.MieAsync());
        Assert.Equal(WorkOrigin.Sistema, riga.Origine);
    }

    [Fact]
    public async Task Prendere_in_carico_non_fa_perdere_alla_riga_il_suo_PERCHE()
    {
        // ⚠️ Trovato a schermo, non dai test: la riga presa in carico mostrava due volte il titolo del
        // documento («vLOA LIBB ↔ LGGG · vLOA LIBB ↔ LGGG») e nessun motivo. La segnalazione resta la
        // verità su COSA e QUANTO urge; l'incarico aggiunge solo chi e entro quando.
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10, ImpactKind.SectorGone) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[] { Incarico(7, "vIPI Roma", 555, daImpatto: 1) });

        var riga = Assert.Single(await s.MieAsync());

        Assert.Equal("Impact_SectorGone", riga.FraseKey);
        Assert.Equal("LIRR_TS_CTR", Assert.Single(riga.FraseArgs));
    }

    [Fact]
    public async Task Prendere_in_carico_non_fa_scivolare_la_riga_in_fondo()
    {
        // Una copia da ripubblicare presa in carico resta «da ripubblicare»: assegnare un lavoro non lo
        // rende meno urgente, e degradarlo a incarico normale lo spingeva sotto tutto il resto.
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10, ImpactKind.ReleaseDrift) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[] { Incarico(7, "vIPI Roma", 555, daImpatto: 1) });

        Assert.Equal(WorkSeverity.DaRipubblicare, Assert.Single(await s.MieAsync()).Severita);
    }

    [Fact]
    public async Task Un_incarico_scritto_a_mano_mostra_il_suo_titolo()
    {
        // Nessuna segnalazione dietro: la frase è il titolo, e `Work_Raw` dice alla UI di stamparlo com'è.
        var s = Costruisci(VipiRole.Admin, io: 555,
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[] { Incarico(7, "Rivedere le frequenze", 555) });

        var riga = Assert.Single(await s.MieAsync());
        Assert.Equal(WorkPhrases.Raw, riga.FraseKey);
        Assert.Equal("Rivedere le frequenze", Assert.Single(riga.FraseArgs));
    }

    [Fact]
    public async Task Prendere_in_carico_crea_un_incarico_che_ricorda_da_dove_viene()
    {
        var incarichi = new IncarichiFinti();
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            repoIncarichi: incarichi);

        await s.PrendiInCaricoAsync(1, assegnatarioId: 777, assegnatarioNome: "Giulia", scadenzaCiclo: "2609");

        var creato = Assert.Single(incarichi.Creati);
        Assert.Equal(1, creato.FromImpactId);
        Assert.Equal(777, creato.AssigneeUserId);
        Assert.Equal("2609", creato.DueAiracCycle);
        Assert.Equal(ReleaseTargetType.AccVipi, creato.TargetType);
    }

    [Fact]
    public async Task Cio_che_e_gia_in_pubblico_nasce_incarico_urgente()
    {
        // La priorità la sa già l'impatto: farla riscegliere a chi assegna sarebbe chiedere due volte una
        // cosa già decisa.
        var incarichi = new IncarichiFinti();
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10) with { IsPublicNow = true } },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            repoIncarichi: incarichi);

        await s.PrendiInCaricoAsync(1, 777, "Giulia", null);

        Assert.Equal(EditorTaskPriority.High, Assert.Single(incarichi.Creati).Priority);
    }

    [Fact]
    public async Task Non_si_prende_in_carico_una_segnalazione_gia_chiusa()
    {
        var s = Costruisci(VipiRole.Admin, io: 555, documenti: new[] { Doc(10, "vIPI Roma", "LIRR") });

        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.PrendiInCaricoAsync(99, 777, "Giulia", null));
    }

    // ── Il banner di un documento ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Il_banner_di_un_documento_porta_tutt_e_due_le_nature()
    {
        // È la richiesta «lo stesso task deve apparire anche in cima all'editor»: fino al 26 agosto il
        // banner mostrava solo le segnalazioni, e chi apriva l'editor vedeva metà del lavoro suo.
        var s = Costruisci(VipiRole.Admin, io: 555,
            impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") },
            incarichi: new[] { Incarico(7, "rivedi le frequenze", 555, chiave: "LIRR|LIRR_CTR") });

        var righe = await s.PerDocumentoAsync(10);

        Assert.Equal(2, righe.Count);
        Assert.Contains(righe, r => r.Origine == WorkOrigin.Sistema);
        Assert.Contains(righe, r => r.Origine == WorkOrigin.Persona);
    }

    [Fact]
    public async Task Il_banner_non_mostra_gli_incarichi_di_un_ALTRO_documento()
    {
        var s = Costruisci(VipiRole.Admin, io: 555,
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR"), Doc(20, "vIPI Milano", "LIMM") },
            incarichi: new[] { Incarico(7, "roba di Milano", 555, chiave: "LIMM|LIMM_CTR") });

        Assert.Empty(await s.PerDocumentoAsync(10));
    }

    // ── La riga che ne esce ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task La_riga_porta_il_link_all_editor_del_tipo_giusto()
    {
        var s = Costruisci(VipiRole.Admin, impatti: new[] { Impatto(1, 10) },
            documenti: new[] { Doc(10, "vIPI Roma", "LIRR") });

        var riga = Assert.Single(await s.MieAsync());
        Assert.Equal("/services/vsop/lirr/editor", riga.Url);
        Assert.True(riga.SiRaggiunge);
    }

    [Fact]
    public async Task Una_segnalazione_su_un_documento_fuori_elenco_resta_in_lista_senza_link()
    {
        // Un documento che nessun descrittore riconosce non ha una pagina: sparire dalla lista lo
        // renderebbe invisibile due volte.
        var s = Costruisci(VipiRole.Admin, impatti: new[] { Impatto(1, 99) }, documenti: Array.Empty<ManagedDoc>());

        var riga = Assert.Single(await s.MieAsync());
        Assert.Null(riga.Url);
        Assert.False(riga.SiRaggiunge);
    }

    // ── Impalcatura ──────────────────────────────────────────────────────────────────────────────────

    private static DocumentImpactRow Impatto(int id, int docId, ImpactKind kind = ImpactKind.SectorGone) =>
        new(id, docId, "vIPI Roma", kind, "LIRR_TS_CTR", "Impact_SectorGone",
            new[] { "LIRR_TS_CTR" }, false, Adesso.AddDays(-1));

    private static ManagedDoc Doc(int id, string titolo, string acc) =>
        new(ReleaseTargetType.AccVipi, titolo, acc, acc, true, false, false,
            ReleaseTargetType.AccVipi, $"{acc}|{acc}_CTR", id);

    private static EditorTask Incarico(int id, string titolo, int assegnatario,
        EditorTaskStatus stato = EditorTaskStatus.Todo, int? daImpatto = null, bool libero = false,
        string? chiave = null) =>
        new()
        {
            Id = id,
            Title = titolo,
            AssigneeUserId = assegnatario,
            Status = stato,
            FromImpactId = daImpatto,
            TargetType = libero ? null : ReleaseTargetType.AccVipi,
            TargetKey = libero ? null : (chiave ?? "LIRR|LIRR_CTR"),
            CreatedUtc = Adesso,
        };

    private static WorkListService Costruisci(
        VipiRole livello, int? io = 555,
        IReadOnlyList<DocumentImpactRow>? impatti = null,
        IReadOnlyList<ManagedDoc>? documenti = null,
        IReadOnlyList<EditorTask>? incarichi = null,
        IncarichiFinti? repoIncarichi = null)
    {
        var repo = repoIncarichi ?? new IncarichiFinti();
        repo.Esistenti = incarichi ?? Array.Empty<EditorTask>();

        return new WorkListService(
            new ImpattiFinti(impatti ?? Array.Empty<DocumentImpactRow>()),
            repo,
            new DocumentiFinti(documenti ?? Array.Empty<ManagedDoc>()),
            new DocRoutesRegistry(new IDocKindRoutes[] { new RotteFinte() }),
            new AuthzFinta(livello, io),
            new RegoleIncarichiFinte());
    }

    private sealed class RotteFinte : IDocKindRoutes
    {
        public ReleaseTargetType Kind => ReleaseTargetType.AccVipi;
        public ReleaseTargetType Target => ReleaseTargetType.AccVipi;
        public string? ViewerUrl(string acc, string key, string? n, int releaseId) => $"/services/vsop/{acc}";
        public string? PublicUrl(string acc, string key, string? n) => $"/services/vsop/{acc}";
        public string? EditorUrl(string acc, string key, string? n, int? documentId) => $"/services/vsop/{acc}/editor";
        public string? DraftUrl(string acc, string key, string? n) => $"/services/vsop/{acc}/vipi?as=draft";
    }

    private sealed class ImpattiFinti : IDocumentImpactRepository
    {
        private readonly IReadOnlyList<DocumentImpactRow> _righe;
        public ImpattiFinti(IReadOnlyList<DocumentImpactRow> righe) => _righe = righe;

        public Task<IReadOnlyList<DocumentImpactRow>> ListAllOpenAsync(CancellationToken ct = default) =>
            Task.FromResult(_righe);
        public Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentImpactRow>>(_righe.Where(r => r.DocumentId == documentId).ToList());
        public Task<DocumentImpactRow?> GetOpenAsync(int impactId, CancellationToken ct = default) =>
            Task.FromResult(_righe.FirstOrDefault(r => r.Id == impactId));

        public Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSectorAsync(string c, string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSpecialAreaAsync(string i, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RaiseAsync(RaiseImpactInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ClearAsync(int impactId, int byUserId, DateTime whenUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> k, string s, int u, DateTime w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentImpactRow>> ListOpenByKindAsync(ImpactKind kind, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlySet<int>> WithLiveSectionAsync(IReadOnlyCollection<int> d, ImpactFamily f, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetDocAccCodeAsync(int documentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetDocTitleAsync(int documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class IncarichiFinti : IEditorTaskRepository
    {
        public IReadOnlyList<EditorTask> Esistenti { get; set; } = Array.Empty<EditorTask>();
        public List<EditorTaskInput> Creati { get; } = new();

        public Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) => Task.FromResult(Esistenti);
        public Task<int> AddAsync(EditorTaskInput input, int createdByUserId, CancellationToken ct = default)
        {
            Creati.Add(input);
            return Task.FromResult(Creati.Count);
        }

        public Task<IReadOnlyList<EditorTask>> ListByAssigneeAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EditorTask?> GetAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(int id, EditorTaskStatus s, int a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AssignAsync(int id, int u, string? n, int a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, int a, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class DocumentiFinti : IDocumentAdminService
    {
        private readonly IReadOnlyList<ManagedDoc> _docs;
        public DocumentiFinti(IReadOnlyList<ManagedDoc> docs) => _docs = docs;
        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) => Task.FromResult(_docs);
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AuthzFinta : IEditAuthorizationService
    {
        private readonly int? _io;
        public AuthzFinta(VipiRole livello, int? io) { Role = livello; _io = io; }

        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => _io;
        public string? CurrentName => "Chi Lavora";
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
    }


    /// <summary>Serve solo per <c>IsOverdue</c>: il resto non lo tocca il servizio.</summary>
    private sealed class RegoleIncarichiFinte : IEditorTaskService
    {
        public bool IsOverdue(EditorTask t) => false;
        public string CurrentCycle() => "2609";
        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => Array.Empty<AiracCycleInfo>();
        public Task<IReadOnlyList<EditorTask>> ListMineAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CreateAsync(EditorTaskInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(int id, EditorTaskStatus s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AssignAsync(int id, int u, string? n, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
