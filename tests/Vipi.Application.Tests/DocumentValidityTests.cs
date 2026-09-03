using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il timbro di «Validità e revisione» (richiesta del committente, 26 agosto 2026): ciclo AIRAC, data di entrata
/// in vigore e chi ha premuto Pubblica — nome, posizioni staff e VID.
/// <para>⚠️ La parte che sbaglia più facilmente è <b>quale release</b> si legge, e quella è pura
/// (<see cref="ValidityRelease.Pick"/>): si verifica senza database.</para>
/// </summary>
public class DocumentValidityTests
{
    private static ReleaseInfo Rel(int id, bool effettiva, string ciclo = "2608", int vid = 704798) =>
        new(id, VersionNumber: 1, ReleaseAiracCycle: ciclo,
            ReleaseEffectiveUtc: new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            Status: effettiva ? ReleaseStatus.Effective : ReleaseStatus.Superseded,
            CreatedByUserId: vid, CreatedUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Note: null, IsEffectiveNow: effettiva);

    // ---- quale release ----

    [Fact]
    public void Senza_id_si_legge_la_release_EFFETTIVA()
    {
        var scelta = ValidityRelease.Pick(new[] { Rel(1, false), Rel(2, true), Rel(3, false) }, null);
        Assert.Equal(2, scelta!.Id);
    }

    [Fact]
    public void Con_un_id_si_legge_QUELLA_release()
    {
        // Anteprima di una release passata: il timbro deve dire il SUO ciclo e il SUO firmatario.
        var scelta = ValidityRelease.Pick(new[] { Rel(1, false, "2607"), Rel(2, true, "2608") }, 1);
        Assert.Equal("2607", scelta!.ReleaseAiracCycle);
    }

    [Fact]
    public void Un_id_che_non_esiste_NON_ricade_sull_effettiva()
    {
        // ⚠️ Ricadere direbbe al lettore, sotto l'intestazione «stai guardando la release #9», il ciclo e il
        // firmatario di un'ALTRA release. Meglio «non pubblicato» che un timbro sbagliato con l'aria giusta.
        Assert.Null(ValidityRelease.Pick(new[] { Rel(1, false), Rel(2, true) }, 9));
    }

    [Fact]
    public void Nessuna_release_significa_non_pubblicato()
    {
        Assert.Null(ValidityRelease.Pick(Array.Empty<ReleaseInfo>(), null));
        Assert.Null(ValidityRelease.Pick(null, null));
        Assert.Null(ValidityRelease.Pick(new[] { Rel(1, false) }, null));   // solo superate: nessuna in vigore
    }

    // ---- il timbro ----

    [Fact]
    public async Task Il_timbro_porta_ciclo_data_e_chi_ha_pubblicato()
    {
        var svc = Servizio(new[] { Rel(7, true) },
            new StaffRosterEntry(704798, "Mario Rossi (704798)", "C3", new[] { "IT-AOA1", "IT-T03" }, DateTime.UtcNow));

        var t = await svc.ResolveAsync(ReleaseTargetType.Airport, "LIBD");

        Assert.True(t.Published);
        Assert.Equal("2608", t.AiracCycle);
        Assert.Equal(new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc), t.EffectiveUtc);
        Assert.Equal(704798, t.ReviewerVid);
        Assert.Equal("Mario Rossi", t.ReviewerName);
        Assert.Equal(new[] { "IT-AOA1", "IT-T03" }, t.ReviewerPositions);
    }

    [Fact]
    public async Task Un_documento_mai_pubblicato_lo_dice()
    {
        var t = await Servizio(Array.Empty<ReleaseInfo>(), null).ResolveAsync(ReleaseTargetType.App, "LIBA_APP");

        Assert.False(t.Published);
        Assert.Null(t.AiracCycle);
        Assert.Null(t.EffectiveUtc);
        Assert.Null(t.ReviewerVid);
    }

    [Fact]
    public async Task Il_VID_zero_non_e_una_persona()
    {
        // ⚠️ In archivio ci sono release scritte senza un utente (tre vLOA). Zero non è «l'utente numero zero»:
        // è «non registrato», e stampare uno zero darebbe al lettore un numero da cercare che non esiste.
        var t = await Servizio(new[] { Rel(1, true, vid: 0) }, null).ResolveAsync(ReleaseTargetType.Vloa, "8");

        Assert.True(t.Published);
        Assert.Null(t.ReviewerVid);
        Assert.Null(t.ReviewerName);
        Assert.Empty(t.ReviewerPositions);
    }

    [Fact]
    public async Task Un_VID_che_il_roster_non_conosce_resta_un_VID()
    {
        // Chi ha pubblicato prima che il roster esistesse, o non si è mai loggato: il numero c'è, il nome no.
        // Meglio il solo VID che nessun revisore: è comunque una persona rintracciabile.
        var t = await Servizio(new[] { Rel(1, true) }, null).ResolveAsync(ReleaseTargetType.Airport, "LIBD");

        Assert.Equal(704798, t.ReviewerVid);
        Assert.Null(t.ReviewerName);
        Assert.Empty(t.ReviewerPositions);
    }

    [Fact]
    public async Task Senza_chiave_non_si_interroga_nessuno()
    {
        // La vLOA monta il componente prima di conoscere il proprio id: una chiave vuota non deve diventare
        // una lettura con chiave "" che risponde qualcosa.
        var releases = new ReleasePerFinta(new[] { Rel(1, true) });
        var svc = new DocumentValidityService(releases, new RosterPerFinta(null));

        var t = await svc.ResolveAsync(ReleaseTargetType.Vloa, "");

        Assert.False(t.Published);
        Assert.Equal(0, releases.Chiamate);
    }

    [Theory]
    // ⚠️ Nel roster i nomi arrivano anche col VID dentro: chi li scrive è il login, e nell'elenco dei permessi
    // il numero serve a distinguere due omonimi. Qui il VID lo aggiunge già il link, e senza questa pulizia si
    // leggeva «Carmine (704798) (VID 704798)» — misurato a schermo.
    [InlineData("Carmine (704798)", 704798, "Carmine")]
    [InlineData("Carmine  (704798)", 704798, "Carmine")]
    [InlineData("Carmine (704798)", 111111, "Carmine (704798)")]   // un ALTRO numero non è il suo VID: resta
    [InlineData("Mario Rossi", 704798, "Mario Rossi")]
    [InlineData("(704798)", 704798, null)]                          // solo il numero: non è un nome
    [InlineData("   ", 704798, null)]
    [InlineData(null, 704798, null)]
    public void Il_nome_non_ripete_il_VID(string? nome, int vid, string? atteso) =>
        Assert.Equal(atteso, DocumentValidityService.CleanName(nome, vid));

    private static DocumentValidityService Servizio(IReadOnlyList<ReleaseInfo> releases, StaffRosterEntry? staff) =>
        new(new ReleasePerFinta(releases), new RosterPerFinta(staff));

    private sealed class ReleasePerFinta : IReleaseService
    {
        private readonly IReadOnlyList<ReleaseInfo> _rel;
        public int Chiamate { get; private set; }
        public ReleasePerFinta(IReadOnlyList<ReleaseInfo> rel) => _rel = rel;

        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult(_rel);
        }

        // Il resto del contratto: questo servizio legge e basta.
        // Le porte dell'unione: questi doppi non pubblicano niente, e senza unione sono le stesse di sotto.
        public Task<IReadOnlyList<BersaglioUnito>> BersagliUnitiAsync(
            ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BersaglioUnito>>(Array.Empty<BersaglioUnito>());
        public Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CancelReleaseAsync(int releaseId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType, string expectedKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) => throw new NotSupportedException();
        public string CurrentCycle() => throw new NotSupportedException();
        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, string? alCiclo = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Vipi.Domain.Services.AiracCycleInfo NextCycle() => throw new NotSupportedException();
        public Task<int> PruneAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RosterPerFinta : IStaffRosterRepository
    {
        private readonly StaffRosterEntry? _staff;
        public RosterPerFinta(StaffRosterEntry? staff) => _staff = staff;

        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult(_staff is not null && _staff.UserId == userId ? _staff : null);

        public Task UpsertLoginAsync(int userId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateVerifiedAsync(int userId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeactivateAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
