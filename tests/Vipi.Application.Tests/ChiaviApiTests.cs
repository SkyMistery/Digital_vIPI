using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// Le chiavi delle API: chi le emette, che cosa si conserva, che cosa apre una chiave. Carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c> (T-017).
/// </summary>
public class ChiaviApiTests
{
    private const int VidFondatore = 704798;

    // ------------------------------------------------------------------ chi emette

    private static IEmittentiChiaviApi Emittenti(CurrentUser? utente, VipiRole? promozione = null, AuthOptions? auth = null)
    {
        auth ??= new AuthOptions { FounderVids = { VidFondatore } };
        var division = new DivisionOptions();
        var resolver = new RoleResolver(auth, division);
        var provider = new UtenteFinto(utente);
        var authz = new EditAuthorizationService(provider, resolver, new PromozioniFinte(utente?.UserId ?? 0, promozione));
        return new EmittentiChiaviApi(provider, authz, resolver, Options.Create(auth), Options.Create(division));
    }

    private static CurrentUser Utente(params string[] posizioni) => new(123, "Tester", null, posizioni);

    [Theory]
    [InlineData("IT-DIR")]
    [InlineData("IT-ADIR")]
    [InlineData("IT-WM")]
    [InlineData("IT-AWM")]
    [InlineData("it-awm")]
    public void HQ_e_WD_emettono(string codice) =>
        Assert.True(Emittenti(Utente(codice)).PuoEmettere);

    /// <summary>⚠️ Il cuore del cancello: sono Admin anche loro, e le chiavi non le vedono.</summary>
    [Theory]
    [InlineData("IT-AOC")]
    [InlineData("IT-AOAC")]
    [InlineData("IT-SOC")]
    [InlineData("IT-SOAC")]
    [InlineData("IT-FOC")]
    [InlineData("LIRR-CH")]
    [InlineData("DE-DIR")]
    public void Gli_altri_non_emettono_neanche_se_Admin(string codice) =>
        Assert.False(Emittenti(Utente(codice)).PuoEmettere);

    [Fact]
    public void Il_fondatore_emette_senza_posizioni() =>
        Assert.True(Emittenti(new CurrentUser(VidFondatore, "F", null, Array.Empty<string>())).PuoEmettere);

    [Fact]
    public void Una_promozione_ad_Admin_non_basta() =>
        Assert.False(Emittenti(Utente(), VipiRole.Admin).PuoEmettere);

    [Fact]
    public void L_anonimo_non_emette() => Assert.False(Emittenti(null).PuoEmettere);

    /// <summary>Se <c>AdminStaffCodes</c> restringe l'admin, un IT-WM che non è più Admin non emette più.</summary>
    [Fact]
    public void Chi_non_e_Admin_non_emette_anche_col_codice()
    {
        var auth = new AuthOptions { AdminStaffCodes = { "^IT-DIR$" } };
        Assert.False(Emittenti(Utente("IT-WM"), auth: auth).PuoEmettere);
        Assert.True(Emittenti(Utente("IT-DIR"), auth: auth).PuoEmettere);
    }

    // ------------------------------------------------------------------ la chiave

    [Fact]
    public void La_chiave_ha_la_sua_forma_e_ogni_volta_e_diversa()
    {
        var a = ChiaveApi.Genera();
        var b = ChiaveApi.Genera();
        Assert.NotEqual(a, b);
        Assert.StartsWith("vipi_", a);
        Assert.True(ChiaveApi.BenFormata(a));
        Assert.Equal(64, ChiaveApi.Impronta(a).Length);
        Assert.Equal(a[..12], ChiaveApi.Prefisso(a));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vipi_corta")]
    public void Una_stringa_qualunque_non_e_una_chiave(string? testo) =>
        Assert.False(ChiaveApi.BenFormata(testo));

    /// <summary>Lunghezza giusta, e un solo carattere fuori posto: l'inizio o l'alfabeto.</summary>
    [Fact]
    public void Basta_un_carattere_sbagliato()
    {
        var buona = ChiaveApi.Genera();
        Assert.True(ChiaveApi.BenFormata(buona));
        Assert.False(ChiaveApi.BenFormata("xipi_" + buona[5..]));
        Assert.False(ChiaveApi.BenFormata(buona[..^1] + "+"));
        Assert.False(ChiaveApi.BenFormata(buona[..^1] + "="));
    }

    // ------------------------------------------------------------------ il servizio

    private static (ApiClientService Servizio, StoreFinto Store) Servizio(string codice = "IT-DIR")
    {
        var store = new StoreFinto();
        var utente = Utente(codice);
        var auth = new AuthOptions();
        var resolver = new RoleResolver(auth, new DivisionOptions());
        var authz = new EditAuthorizationService(new UtenteFinto(utente), resolver, new PromozioniFinte(0, null));
        return (new ApiClientService(store, Emittenti(utente), authz), store);
    }

    [Fact]
    public async Task Si_conserva_l_impronta_mai_la_chiave()
    {
        var (servizio, store) = Servizio();

        var emessa = await servizio.CreaAsync("  Validatore tour IT ", new[] { "archivio" });

        var riga = Assert.Single(store.Righe);
        Assert.Equal("Validatore tour IT", riga.Nome);
        Assert.Equal(ChiaveApi.Impronta(emessa.Chiave), riga.Impronta);
        Assert.NotEqual(emessa.Chiave, riga.Impronta);
        Assert.Equal(ChiaveApi.Prefisso(emessa.Chiave), riga.Prefisso);
        Assert.DoesNotContain(emessa.Chiave, riga.Impronta + riga.Prefisso + riga.Nome);
        Assert.Equal(new[] { "archivio" }, riga.Endpoint);
        Assert.Equal(123, riga.Attore);
    }

    [Fact]
    public async Task Chi_non_emette_non_legge_non_crea_non_revoca()
    {
        var (servizio, store) = Servizio("IT-AOC");

        await Assert.ThrowsAsync<EditNotAllowedException>(() => servizio.ListAsync());
        await Assert.ThrowsAsync<EditNotAllowedException>(() => servizio.CreaAsync("x", new[] { "archivio" }));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => servizio.RevocaAsync(1));
        Assert.Empty(store.Righe);
    }

    [Theory]
    [InlineData("", "archivio")]
    [InlineData("nome", "")]
    [InlineData("nome", "statistiche")]
    public async Task Nome_e_API_si_validano(string nome, string endpoint)
    {
        var (servizio, store) = Servizio();
        await Assert.ThrowsAsync<Aor.ValidationException>(() =>
            servizio.CreaAsync(nome, endpoint.Length == 0 ? Array.Empty<string>() : new[] { endpoint }));
        Assert.Empty(store.Righe);
    }

    [Fact]
    public async Task Nome_troppo_lungo_si_rifiuta_prima_del_database()
    {
        var (servizio, store) = Servizio();
        await Assert.ThrowsAsync<Aor.ValidationException>(() =>
            servizio.CreaAsync(new string('x', ApiClientLimits.Nome + 1), new[] { "archivio" }));
        Assert.Empty(store.Righe);
    }

    // ------------------------------------------------------------------ la verifica

    [Fact]
    public async Task La_verifica_distingue_le_tre_risposte()
    {
        var store = new StoreFinto();
        var buona = ChiaveApi.Genera();
        var cliente = await store.AddAsync("t", ChiaveApi.Prefisso(buona), ChiaveApi.Impronta(buona), new[] { "archivio" }, 1);
        var verifica = new VerificaChiaveApi(store);

        Assert.Equal(EsitoChiaveApi.Valida, (await verifica.VerificaAsync(buona, "archivio")).Esito);
        Assert.Equal(EsitoChiaveApi.NonAbilitata, (await verifica.VerificaAsync(buona, "bridge")).Esito);
        Assert.Equal(EsitoChiaveApi.Sconosciuta, (await verifica.VerificaAsync(ChiaveApi.Genera(), "archivio")).Esito);
        Assert.Equal(EsitoChiaveApi.Sconosciuta, (await verifica.VerificaAsync("vipi_rotta", "archivio")).Esito);

        await store.RevocaAsync(cliente.Id, 1);
        Assert.Equal(EsitoChiaveApi.Sconosciuta, (await verifica.VerificaAsync(buona, "archivio")).Esito);
    }

    [Fact]
    public async Task Una_stringa_malformata_non_si_cerca_nel_database()
    {
        var store = new StoreFinto();
        await new VerificaChiaveApi(store).VerificaAsync("Bearer qualcosa", "archivio");
        Assert.Equal(0, store.Ricerche);
    }

    [Fact]
    public async Task L_ultimo_uso_si_scrive_una_volta_ogni_tanto_non_a_ogni_chiamata()
    {
        var store = new StoreFinto();
        var chiave = ChiaveApi.Genera();
        await store.AddAsync("t", ChiaveApi.Prefisso(chiave), ChiaveApi.Impronta(chiave), new[] { "archivio" }, 1);
        var verifica = new VerificaChiaveApi(store);

        for (var i = 0; i < 5; i++) await verifica.VerificaAsync(chiave, "archivio");
        Assert.Equal(1, store.UsiSegnati);

        store.Righe[0].UltimoUso = DateTime.UtcNow - VerificaChiaveApi.PassoUltimoUso - TimeSpan.FromSeconds(1);
        await verifica.VerificaAsync(chiave, "archivio");
        Assert.Equal(2, store.UsiSegnati);
    }

    // ------------------------------------------------------------------ finti

    private sealed class StoreFinto : IApiClientStore
    {
        public sealed class Riga
        {
            public int Id; public string Nome = ""; public string Prefisso = ""; public string Impronta = "";
            public IReadOnlyList<string> Endpoint = Array.Empty<string>(); public int Attore;
            public DateTime? Revocata; public DateTime? UltimoUso;
        }

        public List<Riga> Righe { get; } = new();
        public int Ricerche { get; private set; }
        public int UsiSegnati { get; private set; }

        private static ApiClientRow Vista(Riga r) =>
            new(r.Id, r.Nome, r.Prefisso, r.Endpoint, r.Attore, DateTime.UtcNow, r.Revocata, null, r.UltimoUso);

        public Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApiClientRow>>(Righe.Select(Vista).ToList());

        public Task<ApiClientRow> AddAsync(string nome, string prefisso, string impronta, IReadOnlyList<string> endpoint,
            int actorUserId, CancellationToken ct = default)
        {
            var r = new Riga { Id = Righe.Count + 1, Nome = nome, Prefisso = prefisso, Impronta = impronta, Endpoint = endpoint, Attore = actorUserId };
            Righe.Add(r);
            return Task.FromResult(Vista(r));
        }

        public Task<bool> RevocaAsync(int id, int actorUserId, CancellationToken ct = default)
        {
            var r = Righe.FirstOrDefault(x => x.Id == id);
            if (r is null || r.Revocata is not null) return Task.FromResult(false);
            r.Revocata = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        public Task<ApiClientRow?> TrovaPerImprontaAsync(string impronta, CancellationToken ct = default)
        {
            Ricerche++;
            var r = Righe.FirstOrDefault(x => x.Impronta == impronta);
            return Task.FromResult(r is null ? null : Vista(r));
        }

        public Task SegnaUsoAsync(int id, DateTime quandoUtc, CancellationToken ct = default)
        {
            UsiSegnati++;
            Righe.First(x => x.Id == id).UltimoUso = quandoUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class UtenteFinto : ICurrentUserProvider
    {
        private readonly CurrentUser? _u;
        public UtenteFinto(CurrentUser? u) => _u = u;
        public CurrentUser? Get() => _u;
    }

    private sealed class PromozioniFinte : IRoleOverrides
    {
        private readonly int _vid;
        private readonly VipiRole? _livello;
        public PromozioniFinte(int vid, VipiRole? livello) { _vid = vid; _livello = livello; }
        public bool Loaded => true;
        public VipiRole? For(int userId) => userId == _vid ? _livello : null;
        public IReadOnlyDictionary<int, VipiRole> All => new Dictionary<int, VipiRole>();
        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
