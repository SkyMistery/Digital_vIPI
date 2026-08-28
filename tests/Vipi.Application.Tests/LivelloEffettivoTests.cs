using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Il livello effettivo dell'utente corrente, come lo calcola il servizio che poi tutti interrogano.
/// Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c>.
///
/// <para>Sostituisce <c>AdminCodeTests</c>, che rispondeva alla domanda vecchia — «questo codice è
/// admin?» — quando le risposte possibili erano due. Ora sono cinque, ordinate, e la domanda è «a che
/// livello arriva questa persona?».</para>
///
/// <para>⚠️ <b>Il cambio di sostanza sta nella prima tabella</b>: fino al 28 agosto 2026 valeva admin
/// qualunque <c>IT-*</c>, jolly. Da oggi lo sono gli otto codici di direzione, e i quattro codici veri che
/// nel 2026 avevano fatto scegliere il jolly — <c>IT-SOC</c> a parte, che è nell'elenco — scendono a
/// <see cref="VipiRole.DivisionStaff"/>.</para>
/// </summary>
public class LivelloEffettivoTests
{
    private const int VidFondatore = 704798;

    private static IEditAuthorizationService Authz(
        CurrentUser? utente, VipiRole? promozione = null, AuthOptions? auth = null, DivisionOptions? division = null) =>
        new EditAuthorizationService(
            new UtenteFinto(utente),
            new RoleResolver(auth ?? new AuthOptions(), division ?? new DivisionOptions()),
            new PromozioniFinte(utente?.UserId ?? 0, promozione));

    private static CurrentUser Utente(params string[] posizioni) => new(123, "Tester", "LIRR", posizioni);

    private static VipiRole Livello(params string[] posizioni) => Authz(Utente(posizioni)).Role;

    // ------------------------------------------------------------------ dal codice staff al livello

    [Theory]
    [InlineData("IT-DIR")]
    [InlineData("IT-ADIR")]
    [InlineData("IT-WM")]
    [InlineData("IT-AWM")]
    [InlineData("IT-AOC")]
    [InlineData("IT-AOAC")]
    [InlineData("IT-SOC")]
    [InlineData("IT-SOAC")]
    [InlineData("it-dir")]                 // i claim non garantiscono le maiuscole
    public void Gli_otto_codici_di_direzione_sono_admin(string codice)
    {
        var authz = Authz(Utente(codice));
        Assert.Equal(VipiRole.Admin, authz.Role);
        Assert.True(authz.IsAdmin);
        Assert.True(authz.IsEditor);         // cumulativo: l'admin ha tutto ciò che ha l'editor
        Assert.True(authz.IsDivisionStaff);
    }

    /// <summary>
    /// ⚠️ Il cambio di sostanza: questi codici sono <b>veri</b>, visti ai login, e fino a ieri erano admin.
    /// Da oggi vedono le statistiche e non toccano i documenti. La via per rimetterli in gioco è la
    /// promozione a mano, non l'elenco dei codici.
    /// </summary>
    [Theory]
    [InlineData("IT-T01")]
    [InlineData("IT-T03")]
    [InlineData("IT-FOC")]
    [InlineData("IT-FOAC")]
    [InlineData("IT-AOA1")]
    public void Il_resto_dello_staff_italiano_vede_le_statistiche_e_non_edita(string codice)
    {
        var authz = Authz(Utente(codice));
        Assert.Equal(VipiRole.DivisionStaff, authz.Role);
        Assert.False(authz.IsAdmin);
        Assert.False(authz.IsEditor);
        Assert.True(authz.IsDivisionStaff);
    }

    [Fact]
    public void Un_chief_dacc_e_editor_e_quindi_anche_staff_di_divisione()
    {
        var authz = Authz(Utente("LIRR-CH"));
        Assert.Equal(VipiRole.Editor, authz.Role);
        Assert.False(authz.IsAdmin);
        Assert.True(authz.IsEditor);
        Assert.True(authz.IsDivisionStaff);   // il chief È membro della divisione: cade da sé dall'ordine
    }

    [Fact]
    public void Lo_staff_di_unaltra_divisione_non_apre_niente()
    {
        var authz = Authz(Utente("DE-DIR"));
        Assert.Equal(VipiRole.IvaoStaff, authz.Role);
        Assert.False(authz.IsDivisionStaff);
    }

    [Fact]
    public void Lanonimo_e_utente_qualunque()
    {
        var authz = Authz(null);
        Assert.Equal(VipiRole.User, authz.Role);
        Assert.False(authz.IsAdmin);
        Assert.Null(authz.CurrentUserId);
    }

    // ------------------------------------------------------------------ il pavimento

    /// <summary>
    /// Il pavimento non è un controllo: è il <c>max</c>. Un «declassamento» sotto ciò che la posizione
    /// staff garantisce non è vietato — è <b>inerte</b>.
    /// </summary>
    [Theory]
    [InlineData("IT-T01", VipiRole.Admin, VipiRole.Admin)]
    [InlineData("IT-T01", VipiRole.Editor, VipiRole.Editor)]
    [InlineData("IT-T01", VipiRole.User, VipiRole.DivisionStaff)]
    [InlineData("IT-DIR", VipiRole.IvaoStaff, VipiRole.Admin)]
    [InlineData("LIRR-CH", VipiRole.DivisionStaff, VipiRole.Editor)]
    public void Il_livello_effettivo_e_il_massimo_fra_staff_e_promozione(
        string posizione, VipiRole promozione, VipiRole atteso) =>
        Assert.Equal(atteso, Authz(Utente(posizione), promozione).Role);

    /// <summary>Una persona senza nessuna posizione staff può essere promossa: è il caso del socio che aiuta.</summary>
    [Fact]
    public void Chi_non_e_staff_puo_essere_promosso_a_mano()
    {
        var authz = Authz(new CurrentUser(999, "Socio", null, Array.Empty<string>()), VipiRole.Editor);
        Assert.Equal(VipiRole.Editor, authz.Role);
        Assert.True(authz.IsEditor);
    }

    // ------------------------------------------------------------------ il cancello

    [Fact]
    public void Il_cancello_lascia_passare_dal_livello_in_su()
    {
        var authz = Authz(Utente("LIRR-CH"));   // Editor

        authz.EnsureAtLeast(VipiRole.User);
        authz.EnsureAtLeast(VipiRole.DivisionStaff);
        authz.EnsureAtLeast(VipiRole.Editor);

        Assert.Throws<EditNotAllowedException>(() => authz.EnsureAtLeast(VipiRole.Admin));
        Assert.Throws<EditNotAllowedException>(authz.EnsureAdmin);
    }

    [Fact]
    public void Per_lanonimo_ogni_cancello_e_chiuso()
    {
        var authz = Authz(null);
        Assert.Throws<EditNotAllowedException>(() => authz.EnsureAtLeast(VipiRole.IvaoStaff));
    }

    // ------------------------------------------------------------------ il fondatore e la config

    [Fact]
    public void Il_fondatore_e_admin_senza_nessuna_posizione_staff()
    {
        var auth = new AuthOptions { FounderVids = { VidFondatore } };
        var authz = Authz(new CurrentUser(VidFondatore, "Chi ha costruito", null, Array.Empty<string>()), auth: auth);

        Assert.True(authz.IsAdmin);
    }

    /// <summary><c>Auth:AdminStaffCodes</c> sostituisce i default: è la via per restringere.</summary>
    [Fact]
    public void I_pattern_espliciti_sostituiscono_i_default()
    {
        var auth = new AuthOptions { AdminStaffCodes = { @"^IT-TA\d+$" } };

        Assert.True(Authz(Utente("IT-TA1"), auth: auth).IsAdmin);
        Assert.False(Authz(Utente("IT-DIR"), auth: auth).IsAdmin);
    }

    [Fact]
    public void Cambiare_divisione_sposta_i_codici()
    {
        var de = new DivisionOptions { Code = "DE" };

        Assert.True(Authz(Utente("DE-DIR"), division: de).IsAdmin);
        Assert.False(Authz(Utente("IT-DIR"), division: de).IsAdmin);
    }

    // ------------------------------------------------------------------ una volta per scope

    /// <summary>
    /// L'identità si risolve <b>una volta per scope</b>. Non è ottimizzazione fine a sé stessa: ogni
    /// <c>Get()</c> rilegge i claim e rifà il parse dell'array JSON <c>userStaffPositions</c>, e le pagine
    /// leggono <c>IsAdmin</c> dentro il markup — <c>StrutturaPage</c> sette volte per render, una delle
    /// quali dentro il <c>foreach</c> sui nodi della gerarchia. Su ~300 callsign erano ~300 parse a ogni
    /// ridisegno, per rispondere sempre la stessa cosa.
    /// </summary>
    [Fact]
    public void L_identita_si_chiede_una_volta_sola_per_scope()
    {
        var provider = new UtenteContato(Utente("IT-AOC"));
        var authz = new EditAuthorizationService(
            provider,
            new RoleResolver(new AuthOptions(), new DivisionOptions()),
            new PromozioniFinte(123, null));

        for (var i = 0; i < 50; i++)
        {
            _ = authz.Role;
            _ = authz.IsAdmin;
            _ = authz.IsEditor;
            _ = authz.CurrentUserId;
        }

        Assert.Equal(1, provider.Letture);
        Assert.True(authz.IsAdmin);
    }

    /// <summary>
    /// L'anonimo è il caso che si sbaglia per primo memoizzando: senza distinguere «non ancora chiesto» da
    /// «chiesto, e non c'è nessuno», un <c>null</c> in cache sembra un valore mancante e il giro si rifà
    /// ogni volta — cioè proprio il caso peggiore, perché le pagine pubbliche chiedono il livello per
    /// decidere se mostrare i comandi di editing.
    /// </summary>
    [Fact]
    public void Anche_lanonimo_si_chiede_una_volta_sola()
    {
        var provider = new UtenteContato(null);
        var authz = new EditAuthorizationService(
            provider,
            new RoleResolver(new AuthOptions(), new DivisionOptions()),
            new PromozioniFinte(0, null));

        for (var i = 0; i < 50; i++) _ = authz.Role;

        Assert.Equal(1, provider.Letture);
        Assert.Equal(VipiRole.User, authz.Role);
    }

    // ------------------------------------------------------------------ doppi

    private sealed class UtenteFinto : ICurrentUserProvider
    {
        private readonly CurrentUser? _u;
        public UtenteFinto(CurrentUser? u) => _u = u;
        public CurrentUser? Get() => _u;
    }

    private sealed class UtenteContato : ICurrentUserProvider
    {
        private readonly CurrentUser? _u;
        public UtenteContato(CurrentUser? u) => _u = u;
        public int Letture { get; private set; }
        public CurrentUser? Get() { Letture++; return _u; }
    }

    private sealed class PromozioniFinte : IRoleOverrides
    {
        private readonly int _vid;
        private readonly VipiRole? _livello;
        public PromozioniFinte(int vid, VipiRole? livello) { _vid = vid; _livello = livello; }

        public bool Loaded => true;
        public VipiRole? For(int userId) => userId == _vid ? _livello : null;
        public IReadOnlyDictionary<int, VipiRole> All => _livello is { } l
            ? new Dictionary<int, VipiRole> { [_vid] = l }
            : new Dictionary<int, VipiRole>();
        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    // Il livello non tocca i grant: stub inerte.
}
