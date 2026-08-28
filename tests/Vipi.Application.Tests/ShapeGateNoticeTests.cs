using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// L'avviso a chi pubblica: c'è un confine nuovo che a questo ciclo non è ancora in vigore.
/// ⚠️ La prova che conta è che l'avviso dica <b>esattamente</b> quello che farà il congelamento: la domanda
/// la fa <see cref="ShapeAiracGate"/>, qui si verifica solo che nessuno la riscriva per conto suo.
/// </summary>
public class ShapeGateNoticeTests
{
    private static readonly IAiracService Airac = new AiracService();
    private const string Vecchia = "[[11.0,44.0],[11.5,44.0],[11.5,44.5]]";
    private const string Nuova = "[[12.0,45.0],[12.5,45.0],[12.5,45.5]]";
    private const string Corrente = "2609";
    private const string Prossimo = "2610";

    private static ShapeGateRow Riga(int id, string cs, ShapeState shape,
        SourceCatalog cat = SourceCatalog.Subcenter) => new(cat, id, cs, cs, shape);

    private static ShapeState Differita(string dalCiclo, bool forzata = false) =>
        new(Nuova, Vecchia, dalCiclo, ShapeSource.Sectorfile, forzata);

    private sealed class Repo : IShapeGateRepository
    {
        public ShapeGateScope Scope = ShapeGateScope.Empty;
        public List<(SourceCatalog, int)> Forzate = new();

        public Task<ShapeGateScope> GetScopeAsync(ReleaseTargetType target, string key, CancellationToken ct = default) =>
            Task.FromResult(Scope);

        public Task<int> SetForcePublishedAsync(
            IReadOnlyList<(SourceCatalog Catalog, int Id)> rows, CancellationToken ct = default)
        {
            Forzate.AddRange(rows.Select(r => (r.Catalog, r.Id)));
            return Task.FromResult(rows.Count);
        }
    }

    private sealed class Authz : IEditAuthorizationService
    {
        public bool Nega;

        // ⚠️ Qui c'erano «AccChieste» e «DocChiesti»: registravano SU CHE COSA veniva chiesto il permesso,
        // perché il permesso dipendeva dall'ACC o dal documento. Dal 28 agosto 2026 non dipende più da
        // niente di tutto ciò — l'Editor edita tutto — e non c'è più un bersaglio da registrare.
        public VipiRole Role => Nega ? VipiRole.DivisionStaff : VipiRole.Admin;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "Chi Pubblica";
        public void EnsureAdmin() { }
    }

    private static ShapeGateNoticeService Servizio(Repo r, Authz a) => new(r, Airac, a);

    [Fact]
    public async Task Avvisa_di_un_confine_che_a_questo_ciclo_non_e_ancora_in_vigore()
    {
        var repo = new Repo { Scope = new ShapeGateScope("LIRR", null, new[] { Riga(1, "LIRR_NE_CTR", Differita(Prossimo)) }) };

        var avvisi = await Servizio(repo, new Authz()).ListDeferredAsync(
            ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", new[] { Corrente });

        var a = Assert.Single(avvisi);
        Assert.Equal("LIRR_NE_CTR", a.Callsign);
        Assert.Equal(Prossimo, a.FromCycle);
    }

    /// <summary>Chi pubblica <b>per il ciclo prossimo</b> quella geometria la porta davvero: niente avviso.</summary>
    [Fact]
    public async Task Nessun_avviso_se_si_pubblica_per_il_ciclo_da_cui_entra_in_vigore()
    {
        var repo = new Repo { Scope = new ShapeGateScope("LIRR", null, new[] { Riga(1, "LIRR_NE_CTR", Differita(Prossimo)) }) };

        var avvisi = await Servizio(repo, new Authz()).ListDeferredAsync(
            ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", new[] { Prossimo });

        Assert.Empty(avvisi);
    }

    /// <summary>
    /// ⚠️ I tasti sono due e usano cicli diversi: basta che UNO dei due porti il confine vecchio perché
    /// l'avviso debba esserci. Sbagliare per difetto qui vuol dire pubblicare in silenzio l'area di prima.
    /// </summary>
    [Fact]
    public async Task Basta_uno_dei_cicli_in_gioco_perche_l_avviso_ci_sia()
    {
        var repo = new Repo { Scope = new ShapeGateScope("LIRR", null, new[] { Riga(1, "LIRR_NE_CTR", Differita(Prossimo)) }) };

        var avvisi = await Servizio(repo, new Authz()).ListDeferredAsync(
            ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", new[] { Corrente, Prossimo });

        Assert.Single(avvisi);
    }

    [Fact]
    public async Task Una_shape_gia_forzata_non_si_riavvisa()
    {
        var repo = new Repo
        {
            Scope = new ShapeGateScope("LIRR", null, new[] { Riga(1, "LIRR_NE_CTR", Differita(Prossimo, forzata: true)) }),
        };

        Assert.Empty(await Servizio(repo, new Authz()).ListDeferredAsync(
            ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", new[] { Corrente }));
    }

    /// <summary>Forza le sole righe differite. (Il permesso non guarda più l'ACC: guarda il livello.)</summary>
    [Fact]
    public async Task Forza_solo_le_righe_differite()
    {
        var repo = new Repo
        {
            Scope = new ShapeGateScope("LIRR", null, new[]
            {
                Riga(1, "LIRR_NE_CTR", Differita(Prossimo)),
                Riga(2, "LIRR_SE_CTR", new ShapeState(Nuova, null, null, ShapeSource.Sectorfile, false)),
                Riga(3, "LIRA_APP", Differita(Prossimo), SourceCatalog.AirportPosition),
            }),
        };
        var authz = new Authz();

        var n = await Servizio(repo, authz).ForcePublishAsync(
            ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", new[] { Corrente });

        Assert.Equal(2, n);
        Assert.Contains((SourceCatalog.Subcenter, 1), repo.Forzate);
        Assert.Contains((SourceCatalog.AirportPosition, 3), repo.Forzate);
        Assert.DoesNotContain((SourceCatalog.Subcenter, 2), repo.Forzate);
    }

    /// <summary>Forzare è un atto editoriale: chi non può pubblicare quel documento non può nemmeno forzarne le aree.</summary>
    [Fact]
    public async Task Chi_non_puo_editare_non_forza_niente()
    {
        var repo = new Repo { Scope = new ShapeGateScope("LIRR", null, new[] { Riga(1, "LIRR_NE_CTR", Differita(Prossimo)) }) };
        var authz = new Authz { Nega = true };

        await Assert.ThrowsAsync<EditNotAllowedException>(() => Servizio(repo, authz).ForcePublishAsync(
            ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", new[] { Corrente }));
        Assert.Empty(repo.Forzate);
    }

    /// <summary>
    /// Anche sul bersaglio vLOA — che non ha una ACC sola a governarlo — si forza.
    /// <para>⚠️ Questo test provava che lì il permesso si chiedesse sul DOCUMENTO invece che sull'ACC: una
    /// distinzione che il 28 agosto 2026 è sparita insieme alle concessioni. Resta a provare che il caso
    /// senza ACC funzioni comunque, che è la metà che poteva rompersi.</para>
    /// </summary>
    [Fact]
    public async Task Anche_la_vloa_che_non_ha_una_acc_si_forza()
    {
        var repo = new Repo { Scope = new ShapeGateScope(null, 42, new[] { Riga(1, "LIRR_NE_CTR", Differita(Prossimo)) }) };

        Assert.Equal(1, await Servizio(repo, new Authz()).ForcePublishAsync(
            ReleaseTargetType.Vloa, "42", new[] { Corrente }));
    }

    /// <summary>Perimetro sconosciuto (chiave illeggibile): non si tocca niente, e non si finge un permesso.</summary>
    [Fact]
    public async Task Perimetro_sconosciuto_non_scrive_niente()
    {
        var repo = new Repo { Scope = ShapeGateScope.Empty };

        Assert.Equal(0, await Servizio(repo, new Authz()).ForcePublishAsync(
            ReleaseTargetType.Vloa, "boh", new[] { Corrente }));
        Assert.Empty(repo.Forzate);
    }
}
