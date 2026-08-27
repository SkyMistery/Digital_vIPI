using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Gli incarichi editoriali (<see cref="EditorTaskService"/>), fino al 22 agosto 2026 senza <b>un solo</b>
/// test: in tutta la suite <c>EditorTask</c> compariva una volta, e per la lunghezza delle colonne indicizzate.
///
/// <para>Le tre regole d'autorizzazione stavano scritte solo nei commenti dell'interfaccia — un non-admin
/// assegna solo a sé, aggiorna solo i propri, elimina solo quelli che ha creato — e il ritardo dipende dal
/// confronto fra cicli AIRAC, che è la parte che si rompe in silenzio quando cambia l'anno.
/// Caratterizzazione prima di toccare il service (FEATURE-PROCESS), carta
/// <c>docs/feature/2026-08-22-incarichi-cosa-sono.md</c>.</para>
/// </summary>
public class EditorTaskServiceTests
{
    private const int Io = 704798, Altro = 555001;

    // ---- creazione -------------------------------------------------------------------------------------

    [Fact]
    public async Task Il_titolo_e_obbligatorio()
    {
        var (servizio, _) = Servizio(admin: true);
        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.CreateAsync(Incarico(titolo: "  ")));
    }

    [Fact]
    public async Task Un_admin_assegna_a_chi_vuole()
    {
        var (servizio, repo) = Servizio(admin: true);

        var id = await servizio.CreateAsync(Incarico(assegnatario: Altro));

        Assert.Equal(Altro, repo.Tasks.Single(t => t.Id == id).AssigneeUserId);
        Assert.Equal(Io, repo.Tasks.Single(t => t.Id == id).CreatedByUserId);
    }

    [Fact]
    public async Task Un_non_admin_assegna_solo_a_se_stesso()
    {
        var (servizio, _) = Servizio(admin: false);

        await servizio.CreateAsync(Incarico(assegnatario: Io));   // a sé: passa
        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.CreateAsync(Incarico(assegnatario: Altro)));
    }

    /// <summary>Un incarico legato a un documento è un impegno su quel documento: chi non è admin deve poterlo
    /// editare. Il gate passa dall'ACC del bersaglio, la stessa domanda che autorizza le release.</summary>
    [Fact]
    public async Task Un_non_admin_non_si_assegna_un_documento_che_non_puo_editare()
    {
        var (servizio, _) = Servizio(admin: false, puoEditare: false, accDelBersaglio: "LIRR");

        await Assert.ThrowsAsync<EditNotAllowedException>(() =>
            servizio.CreateAsync(Incarico(assegnatario: Io, tipo: ReleaseTargetType.AccVipi, chiave: "LIRR|")));
    }

    [Fact]
    public async Task Un_admin_non_passa_dal_gate_del_documento()
    {
        var (servizio, repo) = Servizio(admin: true, puoEditare: false, accDelBersaglio: "LIRR");

        var id = await servizio.CreateAsync(Incarico(assegnatario: Altro, tipo: ReleaseTargetType.AccVipi, chiave: "LIRR|"));

        Assert.Equal(ReleaseTargetType.AccVipi, repo.Tasks.Single(t => t.Id == id).TargetType);
    }

    /// <summary>Un bersaglio che non risolve un ACC non è autorizzabile: si rifiuta invece di lasciar passare.</summary>
    [Fact]
    public async Task Un_documento_collegato_inesistente_e_un_rifiuto()
    {
        var (servizio, _) = Servizio(admin: false, accDelBersaglio: null);

        await Assert.ThrowsAsync<Aor.ValidationException>(() =>
            servizio.CreateAsync(Incarico(assegnatario: Io, tipo: ReleaseTargetType.Airport, chiave: "LIRF")));
    }

    [Fact]
    public async Task Senza_identita_non_si_crea_niente()
    {
        var (servizio, _) = Servizio(admin: false, utente: null);
        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.CreateAsync(Incarico(assegnatario: Io)));
    }

    /// <summary>
    /// ⚠️ Il difetto N1 della carta: l'opzione «Seleziona» della tendina vale <c>0</c>, e fino al 22 agosto
    /// 2026 premere «Crea» senza scegliere nessuno faceva nascere un incarico con <c>AssigneeUserId = 0</c>.
    /// Quell'incarico non è di nessuno — non compare negli incarichi di nessun utente, si vede solo
    /// nell'elenco admin, e nemmeno si riassegna, perché la riassegnazione non era in UI.
    /// </summary>
    [Fact]
    public async Task Un_incarico_senza_assegnatario_non_si_crea()
    {
        var (servizio, repo) = Servizio(admin: true);

        var ex = await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.CreateAsync(Incarico(assegnatario: 0)));

        Assert.Equal("Task_Err_AssigneeRequired", ex.Key);
        Assert.Empty(repo.Tasks);
    }

    /// <summary>La guardia vale anche sui negativi: un VID non è mai zero né sotto.</summary>
    [Fact]
    public async Task Un_assegnatario_negativo_non_e_un_VID()
    {
        var (servizio, _) = Servizio(admin: true);
        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.CreateAsync(Incarico(assegnatario: -3)));
    }

    /// <summary>Il messaggio grezzo resta per i log; la chiave serve a chi lo mostra in pagina (regola 159).</summary>
    [Fact]
    public async Task Ogni_rifiuto_porta_la_sua_chiave_di_traduzione()
    {
        var (admin, _) = Servizio(admin: true);
        var senzaTitolo = await Assert.ThrowsAsync<Aor.ValidationException>(() => admin.CreateAsync(Incarico(titolo: " ")));
        Assert.Equal("Task_Err_TitleRequired", senzaTitolo.Key);
        Assert.False(string.IsNullOrWhiteSpace(senzaTitolo.Message));

        var (mio, repo) = Servizio(admin: false);
        var altrui = repo.Semina(assegnatario: Altro, creatore: Altro);
        var rifiuto = await Assert.ThrowsAsync<Aor.ValidationException>(() => mio.UpdateStatusAsync(altrui, EditorTaskStatus.Done));
        Assert.Equal("Task_Err_UpdateOnlyMine", rifiuto.Key);
    }

    // ---- stato -----------------------------------------------------------------------------------------

    [Fact]
    public async Task L_assegnatario_aggiorna_il_proprio_incarico()
    {
        var (servizio, repo) = Servizio(admin: false);
        var id = repo.Semina(assegnatario: Io, creatore: Altro);

        await servizio.UpdateStatusAsync(id, EditorTaskStatus.InProgress);

        Assert.Equal(EditorTaskStatus.InProgress, repo.Tasks.Single(t => t.Id == id).Status);
    }

    [Fact]
    public async Task Un_terzo_non_aggiorna_l_incarico_di_un_altro()
    {
        var (servizio, repo) = Servizio(admin: false);
        var id = repo.Semina(assegnatario: Altro, creatore: Altro);

        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.UpdateStatusAsync(id, EditorTaskStatus.Done));
    }

    [Fact]
    public async Task Un_admin_aggiorna_l_incarico_di_chiunque()
    {
        var (servizio, repo) = Servizio(admin: true);
        var id = repo.Semina(assegnatario: Altro, creatore: Altro);

        await servizio.UpdateStatusAsync(id, EditorTaskStatus.Done);

        Assert.Equal(EditorTaskStatus.Done, repo.Tasks.Single(t => t.Id == id).Status);
    }

    [Fact]
    public async Task Un_incarico_inesistente_non_si_aggiorna()
    {
        var (servizio, _) = Servizio(admin: true);
        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.UpdateStatusAsync(999, EditorTaskStatus.Done));
    }

    // ---- riassegnazione ed eliminazione ----------------------------------------------------------------

    /// <summary>⚠️ Fino al 22 agosto 2026 <c>AssignAsync</c> non aveva un solo chiamante: né UI né test.
    /// La riassegnazione è di soli admin.</summary>
    [Fact]
    public async Task Solo_un_admin_riassegna()
    {
        var (mio, repoMio) = Servizio(admin: false);
        var id = repoMio.Semina(assegnatario: Io, creatore: Io);
        await Assert.ThrowsAsync<EditNotAllowedException>(() => mio.AssignAsync(id, Altro, "Giulia Bianchi"));

        var (admin, repoAdmin) = Servizio(admin: true);
        var id2 = repoAdmin.Semina(assegnatario: Io, creatore: Io);
        await admin.AssignAsync(id2, Altro, "Giulia Bianchi");

        Assert.Equal(Altro, repoAdmin.Tasks.Single(t => t.Id == id2).AssigneeUserId);
        Assert.Equal("Giulia Bianchi", repoAdmin.Tasks.Single(t => t.Id == id2).AssigneeName);
    }

    [Fact]
    public async Task Si_elimina_solo_cio_che_si_e_creato()
    {
        var (servizio, repo) = Servizio(admin: false);
        var mio = repo.Semina(assegnatario: Altro, creatore: Io);
        var altrui = repo.Semina(assegnatario: Io, creatore: Altro);

        await servizio.DeleteAsync(mio);
        await Assert.ThrowsAsync<Aor.ValidationException>(() => servizio.DeleteAsync(altrui));

        Assert.DoesNotContain(repo.Tasks, t => t.Id == mio);
        Assert.Contains(repo.Tasks, t => t.Id == altrui);
    }

    [Fact]
    public async Task Un_admin_elimina_l_incarico_di_chiunque()
    {
        var (servizio, repo) = Servizio(admin: true);
        var id = repo.Semina(assegnatario: Altro, creatore: Altro);

        await servizio.DeleteAsync(id);

        Assert.Empty(repo.Tasks);
    }

    // ---- elenchi ---------------------------------------------------------------------------------------

    [Fact]
    public async Task L_elenco_completo_e_di_soli_admin()
    {
        var (mio, _) = Servizio(admin: false);
        await Assert.ThrowsAsync<EditNotAllowedException>(() => mio.ListAllAsync());
    }

    [Fact]
    public async Task I_miei_incarichi_sono_quelli_assegnati_a_me()
    {
        var (servizio, repo) = Servizio(admin: false);
        repo.Semina(assegnatario: Io, creatore: Altro);
        repo.Semina(assegnatario: Altro, creatore: Altro);

        var miei = await servizio.ListMineAsync();

        Assert.All(miei, t => Assert.Equal(Io, t.AssigneeUserId));
        Assert.Single(miei);
    }

    /// <summary>⚠️ Senza identità l'elenco è vuoto, non «quello del VID 0»: gli incarichi orfani nati dal
    /// difetto N1 esistono ancora nei database veri, e non sono di chi capita.</summary>
    [Fact]
    public async Task Senza_identita_i_miei_incarichi_sono_vuoti()
    {
        var (servizio, repo) = Servizio(admin: false, utente: null);
        repo.Semina(assegnatario: 0, creatore: Altro);

        Assert.Empty(await servizio.ListMineAsync());
    }

    // ---- ritardo e cicli -------------------------------------------------------------------------------

    /// <summary>Il ritardo si misura sui cicli AIRAC, non sulle date: un incarico è in ritardo se la scadenza
    /// è un ciclo già cominciato e l'incarico non è concluso.</summary>
    [Fact]
    public void In_ritardo_solo_se_il_ciclo_e_passato_e_l_incarico_non_e_concluso()
    {
        var (servizio, _) = Servizio(admin: true);
        var airac = new AiracService();
        var corrente = airac.GetCycle(DateTime.UtcNow);
        var passato = airac.GetCycle(DateTime.UtcNow.AddDays(-28));
        var futuro = airac.GetCycle(DateTime.UtcNow.AddDays(28));

        Assert.True(servizio.IsOverdue(Con(passato, EditorTaskStatus.Todo)));
        Assert.False(servizio.IsOverdue(Con(passato, EditorTaskStatus.Done)));
        Assert.False(servizio.IsOverdue(Con(corrente, EditorTaskStatus.Todo)));
        Assert.False(servizio.IsOverdue(Con(futuro, EditorTaskStatus.Todo)));
        Assert.False(servizio.IsOverdue(Con(null, EditorTaskStatus.Todo)));
    }

    /// <summary>⚠️ Un ciclo malformato (una riga vecchia, un import a mano) non deve far esplodere l'elenco.</summary>
    [Fact]
    public void Un_ciclo_scritto_male_non_e_un_ritardo_ed_e_muto()
    {
        var (servizio, _) = Servizio(admin: true);
        Assert.False(servizio.IsOverdue(Con("pippo", EditorTaskStatus.Todo)));
    }

    /// <summary>Le scadenze proposte partono dal ciclo SUCCESSIVO: il corrente è già cominciato e non è una
    /// scadenza futura utile.</summary>
    [Fact]
    public void Le_scadenze_proposte_non_contengono_il_ciclo_corrente()
    {
        var (servizio, _) = Servizio(admin: true);

        var prossimi = servizio.UpcomingCycles(6);

        Assert.Equal(6, prossimi.Count);
        Assert.DoesNotContain(servizio.CurrentCycle(), prossimi.Select(c => c.Cycle));
        Assert.Equal(prossimi.OrderBy(c => c.EffectiveUtc).Select(c => c.Cycle), prossimi.Select(c => c.Cycle));
    }

    // ---- impalcatura -----------------------------------------------------------------------------------

    private static EditorTask Con(string? ciclo, EditorTaskStatus stato) =>
        new() { Id = 1, Title = "x", DueAiracCycle = ciclo, Status = stato };

    private static EditorTaskInput Incarico(string titolo = "Rivedere le frequenze", int assegnatario = Io,
        ReleaseTargetType? tipo = null, string? chiave = null) =>
        new(titolo, null, assegnatario, null, EditorTaskPriority.Normal, null, tipo, chiave, null);

    private static (EditorTaskService Servizio, RepoFinto Repo) Servizio(
        bool admin, bool puoEditare = true, string? accDelBersaglio = "LIRR", int? utente = Io)
    {
        var repo = new RepoFinto();
        var servizio = new EditorTaskService(repo, new AuthzFinta(utente, admin, puoEditare),
            new ReleasesFinte(accDelBersaglio), new AiracService());
        return (servizio, repo);
    }

    /// <summary>Incarichi in memoria: la persistenza vera è provata dai test di Infrastructure.</summary>
    private sealed class RepoFinto : IEditorTaskRepository
    {
        private int _prossimoId = 1;
        public List<EditorTask> Tasks { get; } = new();

        public int Semina(int assegnatario, int creatore, EditorTaskStatus stato = EditorTaskStatus.Todo)
        {
            var t = new EditorTask
            {
                Id = _prossimoId++, Title = $"Incarico {_prossimoId}", AssigneeUserId = assegnatario,
                CreatedByUserId = creatore, Status = stato, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow,
            };
            Tasks.Add(t);
            return t.Id;
        }

        public Task<IReadOnlyList<EditorTask>> ListByAssigneeAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EditorTask>>(Tasks.Where(t => t.AssigneeUserId == userId).ToList());

        public Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EditorTask>>(Tasks.ToList());

        public Task<EditorTask?> GetAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(Tasks.FirstOrDefault(t => t.Id == id));

        public Task<int> AddAsync(EditorTaskInput input, int createdByUserId, CancellationToken ct = default)
        {
            var t = new EditorTask
            {
                Id = _prossimoId++, Title = input.Title.Trim(), Description = input.Description,
                AssigneeUserId = input.AssigneeUserId, AssigneeName = input.AssigneeName,
                CreatedByUserId = createdByUserId, Priority = input.Priority, DueAiracCycle = input.DueAiracCycle,
                TargetType = input.TargetType, TargetKey = input.TargetKey, TargetLabel = input.TargetLabel,
                CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow,
            };
            Tasks.Add(t);
            return Task.FromResult(t.Id);
        }

        public Task UpdateStatusAsync(int id, EditorTaskStatus status, int actorUserId, CancellationToken ct = default)
        {
            var t = Tasks.Single(x => x.Id == id);
            t.Status = status;
            t.UpdatedUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task AssignAsync(int id, int assigneeUserId, string? assigneeName, int actorUserId, CancellationToken ct = default)
        {
            var t = Tasks.Single(x => x.Id == id);
            t.AssigneeUserId = assigneeUserId;
            t.AssigneeName = assigneeName;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default)
        {
            Tasks.RemoveAll(t => t.Id == id);
            return Task.CompletedTask;
        }
    }

    /// <summary>Autorizzazione finta: il gate ACC vero è provato altrove, qui conta solo chi sono e se posso.</summary>
    private sealed class AuthzFinta : IEditAuthorizationService
    {
        private readonly bool _puo;
        public AuthzFinta(int? userId, bool admin, bool puo) { CurrentUserId = userId; IsAdmin = admin; _puo = puo; }

        public bool IsAdmin { get; }
        public int? CurrentUserId { get; }
        public string? CurrentName => CurrentUserId is null ? null : $"VID {CurrentUserId}";
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) =>
            _puo ? Task.CompletedTask : throw new EditNotAllowedException();
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) =>
            _puo ? Task.CompletedTask : throw new EditNotAllowedException();
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(_puo);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(_puo);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(_puo);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Del registro release serve una domanda sola: qual è l'ACC da autorizzare per questo bersaglio.
    /// Il resto non lo chiama nessuno da qui, e se lo chiamasse il test deve dirlo forte.</summary>
    private sealed class ReleasesFinte : IReleaseRepository
    {
        public Task<IReadOnlyList<string>> ListKeysWithReleasesAsync(ReleaseTargetType type, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<int> RepointKeyAsync(ReleaseTargetType type, string oldKey, string newKey, CancellationToken ct = default) =>
            Task.FromResult(0);

        private readonly string? _acc;
        public ReleasesFinte(string? acc) => _acc = acc;

        public Task<string?> GetAuthAccCodeAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult(_acc);

        public Task<string?> SnapshotWorkingAsync(ReleaseTargetType type, string key, string airacCycle, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SaveReleaseAsync(ReleaseTargetType type, string key, string releaseCycle, DateTime effectiveUtc, string payloadJson, int createdByUserId, string? note, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PublishWorkingVersionAsync(ReleaseTargetType type, string key, int actorUserId, string airacCycle, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocRelease?> GetEffectiveAsync(ReleaseTargetType type, string key, DateTime atUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocRelease?> GetByIdAsync(int releaseId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(ReleaseTargetType Type, string Key)?> CancelAsync(int releaseId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PruneReleasesAsync(ReleaseTargetType type, string key, DateTime keepFromUtc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
