using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'hub è fatto di collegamenti, quindi è dei collegamenti che ci si deve fidare: un'etichetta sbagliata si
/// vede, un indirizzo sbagliato no — porta a una pagina bianca, e solo per chi ci clicca sopra. Stessa rete
/// che <c>AdminNavTests</c> tiene sulla barra admin, per la stessa ragione.
/// </summary>
public class ServicesHomeTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Autorizzazione finta: dal 29 agosto 2026 l'hub chiede il LIVELLO, perché una scheda è chiusa.</summary>
    private sealed class FakeAuthz(VipiRole livello) : IEditAuthorizationService
    {
        public VipiRole Role { get; } = livello;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    /// <summary>
    /// ⚠️ Il livello di default è <b>Editor</b> e non staff di divisione dal 1 settembre 2026: la scheda
    /// della coerenza col sectorfile chiede un gradino in più delle altre due della sezione staff, e con
    /// <c>DivisionStaff</c> i test sull'elenco completo proverebbero un elenco a cui manca una scheda.
    /// Le prove <i>per livello</i> restano i <c>Theory</c> qui sotto, che il livello lo dichiarano.
    /// </summary>
    private IRenderedComponent<ServicesHome> Render(VipiRole livello = VipiRole.Editor)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        return RenderComponent<ServicesHome>();
    }

    [Fact]
    public void Elenca_i_servizi_con_gli_indirizzi_giusti()
    {
        var cut = Render();
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Contains("/services/vsop", indirizzi);
        Assert.Contains("/services/vsop/mil", indirizzi);
        Assert.Contains("/services/stats", indirizzi);
        Assert.Contains("/services/profile-swapper", indirizzi);
        Assert.Contains("/services/coordinates", indirizzi);
        Assert.Contains("/services/vsop/sectorfile", indirizzi);
    }

    /// <summary>
    /// L'ORDINE conta, e non è alfabetico: va dal documento allo strumento — prima si legge, poi si guardano
    /// i propri numeri, poi si usa un attrezzo. È la disposizione chiesta il 29 agosto 2026, ed è l'unica
    /// cosa di questa pagina che un lettore percepisce senza cliccare niente.
    /// </summary>
    [Fact]
    public void Le_schede_stanno_nell_ordine_deciso()
    {
        var cut = Render();
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        // ⚠️ Gli spazi aerei sono usciti dalla griglia pubblica il 1 settembre 2026 (staff di divisione) e
        // stanno ora nella sezione dello staff, PRIMA del convertitore: è una mappa da leggere, e il
        // convertitore è un attrezzo — lo stesso ordine «prima si guarda, poi si usa» del resto dell'hub.
        // ⚠️ La coerenza col sectorfile entra ULTIMA (1 settembre 2026): non è un documento da leggere né un
        // attrezzo che si usa mentre si lavora — è una verifica che si guarda ogni tanto. L'ordine «prima si
        // legge, poi si guarda, poi si usa» la mette in coda, dov'è.
        Assert.Equal(
            new[] { "/services/vsop", "/services/vsop/mil", "/services/stats",
                    "/services/profile-swapper", "/services/vsop/airspace", "/services/coordinates",
                    "/services/vsop/sectorfile" },
            indirizzi);
    }

    /// <summary>
    /// Il convertitore è per lo staff di divisione, e chi non lo è non deve nemmeno vedere la porta: un elenco
    /// di porte chiuse è la stessa cosa che la barra admin evita da sempre (regola 120).
    /// </summary>
    [Theory]
    [InlineData(VipiRole.User, false)]
    [InlineData(VipiRole.IvaoStaff, false)]
    [InlineData(VipiRole.DivisionStaff, true)]
    [InlineData(VipiRole.Admin, true)]
    public void Il_convertitore_si_vede_solo_dallo_staff_di_divisione(VipiRole livello, bool atteso)
    {
        var cut = Render(livello);
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Equal(atteso, indirizzi.Contains("/services/coordinates"));
    }

    /// <summary>
    /// I <b>servizi</b> sono figli diretti di <c>/services</c>, tutti allo stesso livello: è la regola che
    /// rende la forma delle URL leggibile senza spiegarla. Se un giorno qualcuno annidasse uno strumento
    /// sotto un altro — o sotto la documentazione — questo test lo direbbe.
    ///
    /// <para>⚠️ Le <b>scorciatoie</b> (<c>a.choice.shortcut</c>) sono esentate, e l'esenzione è il punto: dal
    /// 29 agosto 2026 l'hub porta anche i vSOP militari, che <b>non</b> sono un servizio ma una parte della
    /// vSOP — e infatti stanno a <c>/services/vsop/mil</c>, due segmenti sotto. Marcarle invece di allargare
    /// la regola tiene in piedi la regola: senza il segno, questo test sarebbe stato semplicemente
    /// cancellato per far entrare un'eccezione, e da lì in poi nessuno avrebbe più notato un servizio
    /// annidato per sbaglio.</para>
    ///
    /// <para>⚠️ Una scorciatoia resta comunque <b>dentro</b> <c>/services/</c>: l'hub non è un elenco di
    /// segnalibri per il resto del sito.</para>
    /// </summary>
    [Fact]
    public void Ogni_servizio_e_figlio_diretto_di_services()
    {
        var cut = Render();

        foreach (var a in cut.FindAll("a.choice"))
        {
            var href = a.GetAttribute("href")!;
            Assert.StartsWith("/services/", href);

            if (a.ClassList.Contains("shortcut")) continue;
            Assert.Equal(2, href.Trim('/').Split('/').Length);
        }
    }

    /// <summary>
    /// ⚠️ Una scorciatoia è un'<b>eccezione</b>, e le eccezioni si contano: se un giorno metà dell'hub fosse
    /// marcata <c>shortcut</c>, la regola sopra non proverebbe più niente pur restando verde. Il numero non è
    /// sacro — si alza scrivendo perché — ma va alzato <i>di proposito</i>.
    ///
    /// <para>⚠️ <b>Due dal 1 settembre 2026</b>, e il perché è lo stesso della prima: la mappa degli spazi
    /// aerei è passata sotto <c>/services/vsop/airspace</c> per decisione del committente, quindi ha smesso
    /// di essere un servizio a sé ed è diventata una parte della documentazione — esattamente come i vSOP
    /// militari. Marcarla è dire questo; non marcarla sarebbe stato allargare la regola.</para>
    ///
    /// <para>⚠️ <b>Tre</b>, sempre dal 1 settembre: la coerenza col sectorfile
    /// (<c>/services/vsop/sectorfile</c>) è una lente sui dati che i documenti già usano, non uno strumento a
    /// sé — il committente l'ha voluta <i>visibile</i>, e questa è la forma in cui esserlo senza sfondare la
    /// regola. Vedi <c>docs/design/regole-perimetro-servizi.md</c> §P5.</para>
    /// </summary>
    [Fact]
    public void Le_scorciatoie_restano_un_eccezione_contata()
    {
        var cut = Render();
        Assert.Equal(3, cut.FindAll("a.choice.shortcut").Count);
    }

    /// <summary>
    /// La coerenza col sectorfile chiede <b>Editor</b>, un gradino più su delle altre due schede di quella
    /// sezione (decisione del committente, 1 settembre 2026): i suoi rilievi parlano del contenuto dei
    /// documenti — frequenze, TA, designatori di pista — e chi li legge deve poterci fare qualcosa.
    /// ⚠️ Il cancello sta in DUE sedi, qui e nella pagina. ⚠️ E <c>DivisionStaff</c> è il caso che conta:
    /// vede le altre due schede della sezione e <b>non</b> questa.
    /// </summary>
    [Theory]
    [InlineData(VipiRole.User, false)]
    [InlineData(VipiRole.IvaoStaff, false)]
    [InlineData(VipiRole.DivisionStaff, false)]
    [InlineData(VipiRole.Editor, true)]
    [InlineData(VipiRole.Admin, true)]
    public void La_coerenza_sectorfile_si_vede_solo_dall_editor_in_su(VipiRole livello, bool atteso)
    {
        var cut = Render(livello);
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Equal(atteso, indirizzi.Contains("/services/vsop/sectorfile"));
    }

    /// <summary>
    /// La mappa degli spazi aerei è per lo staff di divisione, come il convertitore: chi non lo è non deve
    /// nemmeno vedere la porta. ⚠️ Il cancello sta in DUE sedi — qui e nella pagina — perché un indirizzo si
    /// scrive anche a mano.
    /// </summary>
    [Theory]
    [InlineData(VipiRole.User, false)]
    [InlineData(VipiRole.IvaoStaff, false)]
    [InlineData(VipiRole.DivisionStaff, true)]
    [InlineData(VipiRole.Admin, true)]
    public void Gli_spazi_aerei_si_vedono_solo_dallo_staff_di_divisione(VipiRole livello, bool atteso)
    {
        var cut = Render(livello);
        var indirizzi = cut.FindAll("a.choice").Select(a => a.GetAttribute("href")).ToList();

        Assert.Equal(atteso, indirizzi.Contains("/services/vsop/airspace"));
        // E il vecchio indirizzo pubblico non compare più in nessun caso.
        Assert.DoesNotContain("/services/airspace", indirizzi);
    }
}
