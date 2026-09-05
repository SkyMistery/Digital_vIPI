using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Live;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// La vista live è la <b>propria</b> postazione. Da DivisionStaff in su si può guardare quella di chiunque
/// — per assistenza e supervisione — ma per tutti gli altri un callsign altrui nell'indirizzo non compone
/// niente. Carta <c>docs/feature/2026-09-05-vista-live-selettore-e-cancello.md</c>.
///
/// <para>⚠️ Il cancello sta in <b>due</b> sedi (§U): qui si prova quella che conta, il servizio. Che la
/// pagina rimandi è un fatto della pagina; che il modello non si componga è un fatto del prodotto.</para>
///
/// <para>Le dipendenze che il ramo negato non deve toccare sono passate a <c>null!</c> <b>apposta</b>: se un
/// domani il cancello scivolasse dopo la risoluzione del callsign, questi test non tornerebbero verdi per
/// caso — cadrebbero con un <c>NullReferenceException</c>, che è esattamente il rumore giusto.</para>
/// </summary>
public class LiveViewCancelloTests
{
    private const string Mia = "LIRR_N_CTR";
    private const string Altrui = "LIMM_W_CTR";

    private static LiveViewService Servizio(VipiRole livello, string? miaPostazione, IStationResolver? stazioni = null)
    {
        var online = new FintoOnline(miaPostazione);
        return new LiveViewService(
            stations: stazioni!,
            structure: null!,
            topology: null!,
            online: online,
            users: new FintoUtente(),
            registry: null!,
            authz: new FintoLivello(livello),
            sectors: null!);
    }

    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    public async Task La_postazione_di_un_altro_non_si_compone(VipiRole livello)
    {
        var esito = await Servizio(livello, Mia).BuildAsync(Altrui);

        Assert.True(esito.Denied);
        Assert.Null(esito.View);
    }

    /// <summary>Chi non è connesso non ha una postazione «sua»: nessun callsign gli si apre.</summary>
    [Fact]
    public async Task Da_disconnesso_non_si_apre_niente()
    {
        var esito = await Servizio(VipiRole.User, miaPostazione: null).BuildAsync(Mia);

        Assert.True(esito.Denied);
    }

    /// <summary>
    /// La propria si apre eccome — è il caso normale della pagina. Qui il resolver non conosce il callsign,
    /// quindi l'esito è «non trovata»: la cosa provata è che <b>non è «negata»</b>, cioè che il cancello ha
    /// lasciato passare.
    /// </summary>
    [Fact]
    public async Task La_propria_postazione_passa_il_cancello()
    {
        var esito = await Servizio(VipiRole.User, Mia, new ResolverCieco()).BuildAsync(Mia);

        Assert.False(esito.Denied);
        Assert.False(esito.Found);
    }

    /// <summary>Il caso per cui esiste questa feature: lo staff di divisione guarda una postazione altrui.</summary>
    [Theory]
    [InlineData(VipiRole.DivisionStaff)]
    [InlineData(VipiRole.Editor)]
    [InlineData(VipiRole.Admin)]
    public async Task Lo_staff_di_divisione_apre_la_postazione_di_chiunque(VipiRole livello)
    {
        var esito = await Servizio(livello, Mia, new ResolverCieco()).BuildAsync(Altrui);

        Assert.False(esito.Denied);
    }

    /// <summary>⚠️ Anche l'elenco del selettore è chiuso: la pagina lo nasconde, il servizio lo rifiuta.</summary>
    [Fact]
    public async Task Lelenco_delle_postazioni_e_chiuso_sotto_il_livello()
    {
        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => Servizio(VipiRole.User, Mia).ListStationsAsync());
    }

    /// <summary>Snapshot ATC in cui l'utente 1 è connesso con la postazione indicata (o non è connesso).</summary>
    private sealed class FintoOnline : IOnlineAtcProvider
    {
        private readonly OnlineAtcSnapshot _snap;

        public FintoOnline(string? miaPostazione) =>
            _snap = miaPostazione is null
                ? OnlineAtcSnapshot.Empty
                : new OnlineAtcSnapshot
                {
                    Callsigns = new HashSet<string>(new[] { miaPostazione }, StringComparer.OrdinalIgnoreCase),
                    Details = new[] { new OnlineAtc(miaPostazione, UserId: 1, Name: "Tizio", Rating: 5) },
                    AsOf = DateTimeOffset.UtcNow,
                };

        public OnlineAtcSnapshot GetCurrent() => _snap;
    }

    private sealed class FintoUtente : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(UserId: 1, Name: "Tizio", Acc: "LIRR", StaffPositions: Array.Empty<string>());
    }

    private sealed class FintoLivello : IEditAuthorizationService
    {
        public FintoLivello(VipiRole livello) => Role = livello;
        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "Tizio";
    }

    /// <summary>Non conosce nessun callsign: fa fermare <c>BuildAsync</c> al primo passo, dopo il cancello.</summary>
    private sealed class ResolverCieco : IStationResolver
    {
        public IReadOnlyList<AccInfo> Accs => Array.Empty<AccInfo>();
        public AccInfo? Resolve(string accCode) => null;
        public AccInfo? ResolveByCallsign(string callsign) => null;
        public AirportStation? Airport(string? icao) => null;
        public AirportStation? AirportOfCallsign(string? callsign) => null;
        public void Prewarm() { }
    }
}
