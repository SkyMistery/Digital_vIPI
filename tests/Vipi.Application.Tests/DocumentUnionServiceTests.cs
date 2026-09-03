using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le regole delle unioni di documenti (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>): chi può
/// unire, che cosa si può unire, dove finisce chi arriva, e quando un'unione smette di essere tale.
///
/// <para>⚠️ La domanda che questi test tengono ferma è <b>«quali famiglie possono stare in un'unione»</b>. Non
/// è un dettaglio di perimetro: un membro di una famiglia <b>senza <c>IFrozenSectionProvider</c></b> si
/// pubblicherebbe senza congelare niente e <b>senza protestare</b> — <c>FrozenSectionRegistry</c> per un tipo
/// non registrato risponde <c>Empty</c>. È il difetto già pagato con <c>AirportMil</c>, e qui la guardia sta
/// prima, al momento di unire.</para>
/// </summary>
public class DocumentUnionServiceTests
{
    // ---- i casi veri, presi dall'archivio -------------------------------------------------------------
    // LIBV Gioia del Colle ha DUE APP non remotizzati; LIMN Cameri ha civile e militare dello stesso scalo.
    private static ManagedDoc VsopMil(int id, string icao) => Doc(id, ReleaseTargetType.AirportMil, icao, $"vSOP MIL — {icao}");
    private static ManagedDoc Aeroporto(int id, string icao) => Doc(id, ReleaseTargetType.Airport, icao, $"vIPI — {icao}");
    private static ManagedDoc App(int id, string callsign) => Doc(id, ReleaseTargetType.App, callsign, callsign);
    private static ManagedDoc AccVipi(int id, string chiave) => Doc(id, ReleaseTargetType.AccVipi, chiave, "vIPI ACC");

    private static ManagedDoc Doc(int id, ReleaseTargetType tipo, string chiave, string titolo) =>
        new(tipo, titolo, chiave, "LIRR", IsPublished: true, HasDraft: false, IsHidden: false, tipo, chiave, id);

    [Fact]
    public async Task Unire_due_documenti_mette_l_OSPITE_per_primo()
    {
        var s = Servizio(out var repo, VsopMil(24, "LIBV"), App(3, "LIBV_APP"));

        var unionId = await s.UniscoAsync(ospiteDocumentId: 24, invitatoDocumentId: 3);

        var vista = await s.ForDocumentAsync(3);
        Assert.NotNull(vista);
        Assert.Equal(unionId, vista!.Id);
        Assert.Equal(new[] { 24, 3 }, vista.Members.Select(m => m.DocumentId));
        // L'ospite è il primo, e la pagina unita vive al SUO indirizzo: è la sola cosa che il redirect guarda.
        Assert.True(vista.Host.IsHost);
        Assert.Equal(24, vista.Host.DocumentId);
        Assert.True(vista.IsHostDocument(24));
        Assert.False(vista.IsHostDocument(3));
        Assert.Single(repo.Unioni);
    }

    [Fact]
    public async Task Il_TERZO_documento_entra_nell_unione_che_c_e_gia()
    {
        // Il caso di LIBV: due APP non remotizzati e il vSOP. L'unione è un elenco, non una coppia.
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"), App(5, "LIBV_G_APP"));

        var primo = await s.UniscoAsync(24, 3);
        var secondo = await s.UniscoAsync(24, 5);

        Assert.Equal(primo, secondo);   // non ne nasce una seconda
        var vista = await s.ForDocumentAsync(5);
        Assert.Equal(new[] { 24, 3, 5 }, vista!.Members.Select(m => m.DocumentId));
    }

    [Fact]
    public async Task Un_documento_GIA_UNITO_non_si_unisce_altrove_e_lo_dice_col_TITOLO()
    {
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"), Aeroporto(26, "LIBA"));
        await s.UniscoAsync(24, 3);

        var errore = await Assert.ThrowsAsync<Aor.ValidationException>(() => s.UniscoAsync(26, 3));

        // Il messaggio porta il NOME del documento, non l'id: chi ha premuto deve sapere DOVE andare a
        // staccarlo, e «#3» non lo dice.
        Assert.Contains("LIBV_APP", errore.Message);
    }

    [Fact]
    public async Task Un_documento_non_si_unisce_a_SE_STESSO()
    {
        var s = Servizio(out _, VsopMil(24, "LIBV"));
        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.UniscoAsync(24, 24));
    }

    [Fact]
    public async Task La_vIPI_ACC_NON_si_puo_unire_e_il_rifiuto_dice_la_famiglia()
    {
        // È l'unica famiglia a BLOCCHI: non passa da DocumentSectionsView, quindi la pagina unita non
        // saprebbe disegnarla. Il rifiuto è meglio di una pagina che perde metà del contenuto.
        var s = Servizio(out _, VsopMil(24, "LIBV"), AccVipi(9, "LIRR|LIRR_CTR"));

        var errore = await Assert.ThrowsAsync<Aor.ValidationException>(() => s.UniscoAsync(24, 9));
        Assert.Contains("AccVipi", errore.Message);
    }

    [Fact]
    public async Task Chi_non_e_Editor_non_unisce_niente()
    {
        var s = Servizio(out _, new AuthzFinta { Livello = VipiRole.DivisionStaff },
                         VsopMil(24, "LIBV"), App(3, "LIBV_APP"));

        await Assert.ThrowsAsync<EditNotAllowedException>(() => s.UniscoAsync(24, 3));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => s.SciogliAsync(1));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => s.SpostaAsync(1, +1));
    }

    [Fact]
    public async Task LEGGERE_un_unione_non_chiede_permessi()
    {
        // ⚠️ La legge il viewer PUBBLICO, che è il posto da cui si decide se reindirizzare: un cancello qui
        // renderebbe la pagina unita visibile solo allo staff, e chiunque altro vedrebbe metà documento.
        var s = Servizio(out var repo, VsopMil(24, "LIBV"), App(3, "LIBV_APP"));
        await s.UniscoAsync(24, 3);

        repo.Authz.Livello = VipiRole.User;
        var vista = await s.ForDocumentAsync(3);

        Assert.NotNull(vista);
        Assert.Equal(2, vista!.Members.Count);
    }

    [Fact]
    public async Task Togliere_il_penultimo_membro_SCIOGLIE_l_unione()
    {
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"));
        await s.UniscoAsync(24, 3);
        var membro = (await s.ForDocumentAsync(3))!.Of(3)!;

        await s.RimuoviMembroAsync(membro.MemberId);

        // Un'unione con un membro solo è una pagina che unisce sé stessa, e un redirect che non ha dove
        // mandare: la regola sta nel dominio, non nella pagina che ha premuto il tasto.
        Assert.Null(await s.ForDocumentAsync(24));
        Assert.Null(await s.ForDocumentAsync(3));
    }

    [Fact]
    public async Task I_candidati_mettono_PRIMA_quelli_dello_stesso_scalo()
    {
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"), App(5, "LIBV_G_APP"),
                         Aeroporto(26, "LIBA"), App(7, "LIBA_APP"));

        var candidati = await s.CandidatiAsync(24);

        // La testa del callsign è lo scalo: LIBV_G_APP parla di LIBV come LIBV_APP.
        Assert.Equal(new[] { 3, 5 }, candidati.Where(c => c.StessoScalo).Select(c => c.DocumentId).OrderBy(i => i));
        Assert.True(candidati[0].StessoScalo && candidati[1].StessoScalo);
        // Ma gli altri restano nell'elenco: «indipendentemente dal tipo di documento» vuol dire anche senza
        // un recinto che qualcuno dovrà scavalcare.
        Assert.Contains(candidati, c => c.DocumentId == 26);
    }

    [Fact]
    public async Task Un_documento_gia_unito_NON_e_piu_un_candidato()
    {
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"), App(5, "LIBV_G_APP"));
        await s.UniscoAsync(24, 3);

        var candidati = await s.CandidatiAsync(24);

        // Nemmeno se l'unione è la mia: da lì si toglie con il tasto che lo toglie, non riaggiungendolo.
        Assert.DoesNotContain(candidati, c => c.DocumentId == 3);
        Assert.Contains(candidati, c => c.DocumentId == 5);
    }

    [Fact]
    public async Task Dalla_CHIAVE_di_release_si_arriva_all_unione()
    {
        // È la porta che usano i viewer: una pagina conosce il proprio bersaglio, non l'id del suo documento.
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"));
        await s.UniscoAsync(24, 3);

        var vista = await s.ForTargetAsync(ReleaseTargetType.App, "LIBV_APP");

        Assert.NotNull(vista);
        Assert.Equal(new[] { 24, 3 }, vista!.Members.Select(m => m.DocumentId));
        // Un bersaglio senza documento non è un errore: la risposta è «nessuna unione».
        Assert.Null(await s.ForTargetAsync(ReleaseTargetType.App, "LIRP_APP"));
    }

    [Fact]
    public async Task L_OSPITE_si_riconosce_da_famiglia_E_chiave_insieme()
    {
        // ⚠️ La trappola: un aeroporto e il suo vSOP militare hanno la STESSA chiave di release (l'ICAO) e
        // si distinguono per il TIPO — è il fatto su cui poggiano le due edizioni con cicli indipendenti.
        // Confrontare la sola chiave farebbe credere ospite anche l'edizione che ospite non è, e la pagina
        // civile disegnerebbe l'unione del militare.
        var s = Servizio(out _, VsopMil(29, "LIMN"), Aeroporto(28, "LIMN"));
        await s.UniscoAsync(29, 28);

        var vista = await s.ForTargetAsync(ReleaseTargetType.AirportMil, "LIMN");

        Assert.True(vista!.IsHostTarget(ReleaseTargetType.AirportMil, "LIMN"));
        Assert.False(vista.IsHostTarget(ReleaseTargetType.Airport, "LIMN"));
        // E non fa distinzione di maiuscole: le chiavi arrivano dagli indirizzi.
        Assert.True(vista.IsHostTarget(ReleaseTargetType.AirportMil, "limn"));
    }

    [Fact]
    public async Task TUTTE_le_appartenenze_in_una_lettura_sola()
    {
        // È quel che serve a chi mostra un ELENCO di documenti e deve dire quali sono uniti. ⚠️ Una lettura
        // sola e non una per riga: l'elenco unificato ha già pagato due volte il difetto N+1.
        var s = Servizio(out _, VsopMil(24, "LIBV"), App(3, "LIBV_APP"), App(5, "LIBV_G_APP"),
                         Aeroporto(26, "LIBA"));
        await s.UniscoAsync(24, 3);
        await s.UniscoAsync(24, 5);

        var righe = await s.TutteAsync();

        Assert.Equal(3, righe.Count);
        Assert.Single(righe.Select(r => r.UnionId).Distinct());
        Assert.Equal(new[] { 24, 3, 5 }, righe.OrderBy(r => r.Order).Select(r => r.DocumentId));
        // Il documento non unito non compare: «nessuna riga» è la risposta, non una riga vuota.
        Assert.DoesNotContain(righe, r => r.DocumentId == 26);
    }

    [Fact]
    public void La_testa_di_una_chiave_e_lo_scalo()
    {
        Assert.Equal("LIBV", DocumentUnionService.Testa("LIBV"));
        Assert.Equal("LIBV", DocumentUnionService.Testa("LIBV_APP"));
        Assert.Equal("LIBV", DocumentUnionService.Testa("LIBV_G_APP"));
        Assert.Equal("LIBV", DocumentUnionService.Testa("libv_app"));
        // Una chiave di vIPI ACC non ha uno scalo, e la sua testa non deve fingere di essere un ICAO.
        Assert.Equal("LIRR|LIRR_CTR", DocumentUnionService.Testa("LIRR|LIRR_CTR"));
    }

    // ---- doppi di scena -------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>Zero membri descritti → nessuna unione, non un'unione VUOTA.</b> La proiezione salta i
    /// membri che nessun descrittore riconosce; se li salta tutti, la vista resterebbe con zero membri e
    /// <c>Host</c> — che è <c>Members[0]</c> — alzerebbe <c>ArgumentOutOfRangeException</c> al primo che
    /// gliela chiede. Il primo che gliela chiede è un <b>viewer pubblico</b>: circuito giù su una pagina
    /// che apre chiunque.
    /// </summary>
    [Fact]
    public async Task Se_NESSUN_membro_si_descrive_la_risposta_e_null_e_non_una_vista_vuota()
    {
        var s = Servizio(out var repo, VsopMil(24, "LIBV"), App(3, "LIBV_APP"));
        await s.UniscoAsync(24, 3);

        // Le stesse righe in archivio, ma i descrittori non riconoscono piu' nessuno dei due.
        var authz = new AuthzFinta();
        var orfana = new DocumentUnionService(repo, new DocsFinti(Array.Empty<ManagedDoc>()), authz,
                                              new BersagliFinti(Array.Empty<ManagedDoc>()));

        Assert.Null(await orfana.ForDocumentAsync(24));
    }

    /// <summary>
    /// ⚠️ <b>UN membro solo invece resta.</b> Scartando anche quella, un'unione con un membro rotto
    /// diventerebbe insieme invisibile e <b>indissolubile</b>: <c>TidyAsync</c> non la tocca, perché le
    /// RIGHE in archivio sono ancora due. Il pannello dell'editor deve poter mostrare il tasto «sciogli».
    /// </summary>
    [Fact]
    public async Task Se_UN_membro_si_descrive_l_unione_resta_visibile_per_poterla_SCIOGLIERE()
    {
        var s = Servizio(out var repo, VsopMil(24, "LIBV"), App(3, "LIBV_APP"));
        await s.UniscoAsync(24, 3);

        var solo = new[] { VsopMil(24, "LIBV") };
        var authz = new AuthzFinta();
        var mezza = new DocumentUnionService(repo, new DocsFinti(solo), authz, new BersagliFinti(solo));

        var vista = await mezza.ForDocumentAsync(24);
        Assert.NotNull(vista);
        Assert.Equal(new[] { 24 }, vista!.Members.Select(m => m.DocumentId));
        Assert.Equal(24, vista.Host.DocumentId);
    }

    private static DocumentUnionService Servizio(out RepoFinto repo, params ManagedDoc[] docs) =>
        Servizio(out repo, new AuthzFinta(), docs);

    private static DocumentUnionService Servizio(out RepoFinto repo, AuthzFinta authz, params ManagedDoc[] docs)
    {
        repo = new RepoFinto { Authz = authz };
        return new DocumentUnionService(repo, new DocsFinti(docs), authz, new BersagliFinti(docs));
    }

    private sealed class AuthzFinta : IEditAuthorizationService
    {
        public VipiRole Livello { get; set; } = VipiRole.Editor;
        public VipiRole Role => Livello;
        public bool IsAdmin => Livello >= VipiRole.Admin;
        public int? CurrentUserId => 42;
        public string? CurrentName => "chi unisce";
        public void EnsureAdmin() { }
    }

    /// <summary>Le righe in memoria, con la stessa aritmetica dell'originale: coda densa e ricompattamento.</summary>
    private sealed class RepoFinto : IDocumentUnionRepository
    {
        public AuthzFinta Authz { get; set; } = new();
        public List<UnionRow> Righe { get; } = new();
        public IEnumerable<int> Unioni => Righe.Select(r => r.UnionId).Distinct();
        private int _prossimaUnione = 1;
        private int _prossimoMembro = 1;

        public Task<IReadOnlyList<UnionRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UnionRow>>(Righe.ToList());

        public Task<IReadOnlyList<UnionRow>> ByDocumentAsync(int documentId, CancellationToken ct = default)
        {
            var riga = Righe.FirstOrDefault(r => r.DocumentId == documentId);
            return riga is null
                ? Task.FromResult<IReadOnlyList<UnionRow>>(Array.Empty<UnionRow>())
                : ByUnionAsync(riga.UnionId, ct);
        }

        public Task<IReadOnlyList<UnionRow>> ByUnionAsync(int unionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UnionRow>>(
                Righe.Where(r => r.UnionId == unionId).OrderBy(r => r.Order).ToList());

        public Task<int> CreateAsync(int hostDocumentId, int guestDocumentId, int createdByUserId,
                                     CancellationToken ct = default)
        {
            var id = _prossimaUnione++;
            Righe.Add(new UnionRow(id, _prossimoMembro++, hostDocumentId, 0));
            Righe.Add(new UnionRow(id, _prossimoMembro++, guestDocumentId, 1));
            return Task.FromResult(id);
        }

        public Task AddMemberAsync(int unionId, int documentId, CancellationToken ct = default)
        {
            var coda = Righe.Where(r => r.UnionId == unionId).Select(r => r.Order).DefaultIfEmpty(-1).Max();
            Righe.Add(new UnionRow(unionId, _prossimoMembro++, documentId, coda + 1));
            return Task.CompletedTask;
        }

        public Task RemoveMemberAsync(int memberId, CancellationToken ct = default)
        {
            var riga = Righe.FirstOrDefault(r => r.MemberId == memberId);
            if (riga is null) return Task.CompletedTask;
            Righe.Remove(riga);
            Rinumera(riga.UnionId);
            return Task.CompletedTask;
        }

        public Task DissolveAsync(int unionId, CancellationToken ct = default)
        {
            Righe.RemoveAll(r => r.UnionId == unionId);
            return Task.CompletedTask;
        }

        public Task MoveAsync(int memberId, int delta, CancellationToken ct = default)
        {
            var riga = Righe.FirstOrDefault(r => r.MemberId == memberId);
            if (riga is null || delta == 0) return Task.CompletedTask;
            var fratelli = Righe.Where(r => r.UnionId == riga.UnionId).OrderBy(r => r.Order).ToList();
            var i = fratelli.IndexOf(riga);
            var j = i + Math.Sign(delta);
            if (j < 0 || j >= fratelli.Count) return Task.CompletedTask;
            Sostituisci(fratelli[i] with { Order = fratelli[j].Order });
            Sostituisci(fratelli[j] with { Order = fratelli[i].Order });
            return Task.CompletedTask;
        }

        public Task<int> TidyAsync(CancellationToken ct = default)
        {
            var magre = Righe.GroupBy(r => r.UnionId).Where(g => g.Count() < 2).Select(g => g.Key).ToList();
            foreach (var id in magre) Righe.RemoveAll(r => r.UnionId == id);
            return Task.FromResult(magre.Count);
        }

        private void Sostituisci(UnionRow aggiornata)
        {
            Righe.RemoveAll(r => r.MemberId == aggiornata.MemberId);
            Righe.Add(aggiornata);
        }

        private void Rinumera(int unionId)
        {
            var fratelli = Righe.Where(r => r.UnionId == unionId).OrderBy(r => r.Order).ToList();
            for (var i = 0; i < fratelli.Count; i++) Sostituisci(fratelli[i] with { Order = i });
        }
    }

    /// <summary>I descrittori di release, ridotti a quel che serve: chiave → id del documento.</summary>
    private sealed class BersagliFinti : IReleaseTargetRegistry
    {
        private readonly ManagedDoc[] _docs;
        public BersagliFinti(ManagedDoc[] docs) => _docs = docs;

        public IReadOnlyList<IReleaseTarget> ByDescribeOrder =>
            _docs.Select(d => d.ReleaseTarget).Distinct().Select(t => (IReleaseTarget)new Bersaglio(t, _docs)).ToList();

        public IReleaseTarget For(ReleaseTargetType type) => new Bersaglio(type, _docs);

        private sealed class Bersaglio : IReleaseTarget
        {
            private readonly ManagedDoc[] _docs;
            public Bersaglio(ReleaseTargetType type, ManagedDoc[] docs) { Type = type; _docs = docs; }
            public ReleaseTargetType Type { get; }
            public int DescribeOrder => 0;

            public Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
                Task.FromResult(_docs.FirstOrDefault(
                    d => d.ReleaseTarget == Type && string.Equals(d.ReleaseKey, key, StringComparison.OrdinalIgnoreCase))?.DocumentId);

            public Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
                Task.FromResult<string?>("LIRR");

            public bool TryDescribe(Vipi.Domain.Entities.Document doc, bool hasDraft, out ManagedDoc managed)
            {
                managed = default!;
                return false;
            }
        }
    }

    private sealed class DocsFinti : IDocumentAdminRepository
    {
        private readonly ManagedDoc[] _docs;
        public DocsFinti(ManagedDoc[] docs) => _docs = docs;

        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ManagedDoc>>(_docs);

        public Task<IReadOnlyDictionary<int, ManagedDoc>> DescribeAsync(IReadOnlyCollection<int> documentIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, ManagedDoc>>(
                _docs.Where(d => d.DocumentId is not null && documentIds.Contains(d.DocumentId.Value))
                     .ToDictionary(d => d.DocumentId!.Value));

        public Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetLanguageAsync(ManagedDocRef doc, Language language, bool locked, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
