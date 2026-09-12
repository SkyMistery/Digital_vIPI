using Microsoft.AspNetCore.Http;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Di quali risposte si può tenere una copia, e di quali no.
///
/// <para>La decisione ha OTTO clausole e ognuna è un modo diverso di sbagliare — e sbagliare qui non
/// produce un errore, produce <b>la pagina di un altro</b>. Per questo sono provate una per una invece che
/// attraverso «apro una pagina e guardo l'intestazione».</para>
/// </summary>
public sealed class CacheDelleLettureAnonimeTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public CacheDelleLettureAnonimeTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    /// <summary>I documenti pubblici: copie congelate, si tengono.</summary>
    [Theory]
    [InlineData("/services/vsop")]
    [InlineData("/services/vsop/guide")]
    [InlineData("/services/vsop/libb/vipi")]
    [InlineData("/services/vsop/libb/airports")]
    [InlineData("/services/vsop/libb/vloa")]
    [InlineData("/services/vsop/libb/apps")]
    public void Una_lettura_anonima_di_un_documento_pubblico_si_puo_tenere(string percorso)
        => Assert.True(CacheDelleLettureAnonime.Riutilizzabile(Richiesta(percorso)));

    /// <summary>
    /// Le pagine il cui contenuto dipende da chi guarda, no. Ognuna per una ragione sua: le schermate di
    /// amministrazione e gli editor non sono pubblici; il live è vivo per definizione; ricerca e
    /// «cambiati» dipendono dai permessi; login e logout non si tengono mai.
    /// </summary>
    [Theory]
    [InlineData("/services/vsop/admin/airports")]
    [InlineData("/services/vsop/admin/sector-structure")]
    [InlineData("/services/vsop/libb/editor")]
    [InlineData("/services/vsop/editor/new-document")]
    [InlineData("/services/vsop/admin/pending")]
    [InlineData("/services/vsop/versions")]
    [InlineData("/services/vsop/tasks")]
    [InlineData("/services/vsop/live")]
    [InlineData("/services/vsop/live/LIRR_CTR")]
    [InlineData("/services/vsop/search")]
    [InlineData("/services/vsop/changed")]
    [InlineData("/services/vsop/auth/login")]
    [InlineData("/services/stats/world")]
    public void Le_pagine_che_dipendono_da_chi_guarda_non_si_tengono(string percorso)
        => Assert.False(CacheDelleLettureAnonime.Riutilizzabile(Richiesta(percorso)));

    /// <summary>
    /// ⚠️ L'anteprima di una bozza o di una release non ancora effettiva è materiale di LAVORAZIONE: la
    /// vede solo chi può modificare. Una copia tenuta da parte la mostrerebbe al primo che passa con lo
    /// stesso indirizzo — cioè pubblicherebbe un documento che nessuno ha pubblicato.
    /// </summary>
    [Theory]
    [InlineData("/services/vsop/libb/vipi", "as", "draft")]
    [InlineData("/services/vsop/libb/vipi", "as", "rel:12")]
    public void Lanteprima_di_una_bozza_non_si_tiene(string percorso, string chiave, string valore)
    {
        var ctx = Richiesta(percorso);
        ctx.Request.QueryString = QueryString.Create(chiave, valore);

        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(ctx));
    }

    /// <summary>Chi è entrato vede una pagina sua: quella copia non si presta a nessuno.</summary>
    [Fact]
    public void La_pagina_di_chi_e_entrato_non_si_tiene()
    {
        var ctx = Richiesta("/services/vsop/libb/vipi");
        ctx.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "vipi"));

        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(ctx));
    }

    /// <summary>
    /// E nemmeno quella di chi porta un cookie qualunque. È una rete di sicurezza in più rispetto al
    /// controllo sull'identità: in sviluppo l'identità è finta e non passa dal <c>ClaimsPrincipal</c>,
    /// quindi «non autenticato» da solo direbbe di sì anche per l'admin di sviluppo.
    ///
    /// <para>⚠️ <c>vipi.auth</c> è il cookie di sessione vero: è il caso che non deve mai passare, ed è
    /// anche quello su cui la Cache Rule di Cloudflare mette la propria condizione.</para>
    /// </summary>
    [Theory]
    [InlineData(".AspNetCore.Cookies=qualcosa")]
    [InlineData("vipi.auth=qualcosa")]
    [InlineData("_ga=GA1.2.3")]
    [InlineData("vipi.auth=x; .AspNetCore.Culture=c%3Dit%7Cuic%3Dit")]
    public void Chi_porta_un_cookie_non_riceve_una_copia_da_tenere(string cookie)
    {
        var ctx = Richiesta("/services/vsop/libb/vipi");
        ctx.Request.Headers.Cookie = cookie;

        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(ctx));
    }

    /// <summary>
    /// <b>I due cookie che il sito emette da sé NON spengono la cache</b> (12 settembre 2026).
    ///
    /// <para>Fino a quel giorno la clausola era «zero cookie», e il sito si sabotava da solo: la home, la
    /// ricerca, i «cambiati» e il live — tutte pagine <i>escluse</i>, dove l'antiforgery non viene tolto —
    /// lasciavano quel cookie al primo passaggio, e <c>?culture=</c> lasciava <c>.AspNetCore.Culture</c> per
    /// <b>un anno</b>. Da lì in poi ogni pagina di quel lettore tornava <c>no-store</c>: la cache anonima era
    /// morta per chiunque girasse il sito.</para>
    ///
    /// <para>⚠️ Il nome dell'antiforgery porta un <b>hash in coda</b> e cambia da installazione a
    /// installazione: si riconosce per prefisso, e il test lo prova con un hash finto.</para>
    /// </summary>
    [Theory]
    [InlineData(".AspNetCore.Antiforgery.kn7jjSKzJss=CfDJ8KEfvUOp")]
    [InlineData(".AspNetCore.Culture=c%3Den%7Cuic%3Den")]
    [InlineData(".AspNetCore.Antiforgery.abc=x; .AspNetCore.Culture=c%3Dit%7Cuic%3Dit")]
    public void I_due_cookie_del_sito_non_spengono_la_cache(string cookie)
    {
        var ctx = Richiesta("/services/vsop/libb/vipi");
        ctx.Request.Headers.Cookie = cookie;

        Assert.True(CacheDelleLettureAnonime.Riutilizzabile(ctx));
    }

    /// <summary>
    /// ⚠️ <b>La richiesta esplicita di una lingua non si tiene</b>, ed è l'unica risposta del sito che porta
    /// un <c>Set-Cookie</c> (lo scrive <c>CultureCookieMiddleware</c> sulla stessa condizione). Una risposta
    /// che dice insieme «tenetemi» e «prendi questo cookie» farebbe consegnare a una cache condivisa il
    /// cookie di un altro. È una richiesta sola per lettore: la pagina successiva porta il cookie, non la
    /// query, e si tiene regolarmente — che è il test qui sopra.
    /// </summary>
    [Theory]
    [InlineData("culture")]
    [InlineData("ui-culture")]
    public void La_richiesta_esplicita_di_una_lingua_non_si_tiene(string chiave)
    {
        var ctx = Richiesta("/services/vsop/libb/vipi");
        ctx.Request.QueryString = QueryString.Create(chiave, "en");

        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(ctx));
    }

    /// <summary>Solo le letture: una POST non è una pagina di cui tenere una copia.</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void Solo_le_letture(string metodo)
    {
        var ctx = Richiesta("/services/vsop/libb/vipi");
        ctx.Request.Method = metodo;

        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(ctx));
    }

    /// <summary>
    /// <b>L'API del quadro vAWOS sta sotto <c>/services</c>, quindi passa di qui</b> — e fino al 12 settembre
    /// 2026 nessuno lo sapeva: l'endpoint scriveva <c>Cache-Control: no-store</c> e la produzione rispondeva
    /// <c>public, max-age=60</c>, perché questo middleware scrive in <c>OnStarting</c>, cioè dopo.
    ///
    /// <para>Il comportamento è quello giusto — sotto c'è un METAR con dieci minuti di TTL, e una copia
    /// tenuta un minuto toglie dall'origine il giro al minuto di ogni scheda aperta — ma va <b>pinnato</b>,
    /// perché è il risultato di due pezzi che si incontrano e non di una riga che lo dice.</para>
    ///
    /// <para>⚠️ E vale solo per l'anonimo: il payload di un editor è diverso (<c>BuildAsync</c> riceve
    /// <c>IsEditor</c>), e il METAR di prova è dello staff. Il test qui sotto prova tutte e due le facce.</para>
    /// </summary>
    [Fact]
    public void Lapi_del_vawos_si_tiene_per_gli_anonimi_e_non_per_gli_altri()
    {
        Assert.True(CacheDelleLettureAnonime.Riutilizzabile(Richiesta("/services/vawos/api/LIBA")));

        var entrato = Richiesta("/services/vawos/api/LIBA");
        entrato.Request.Headers.Cookie = "vipi.auth=qualcosa";
        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(entrato));
    }

    /// <summary>Quel che sta fuori da /services non passa di qui: gli endpoint macchina e i callback del login.</summary>
    [Theory]
    [InlineData("/vsop/health")]
    [InlineData("/vsop/live/atc")]
    [InlineData("/vsop/api/v1/transfers/resolve")]
    [InlineData("/signin-oidc")]
    [InlineData("/Error")]
    public void Fuori_da_services_non_si_decide_niente(string percorso)
        => Assert.False(CacheDelleLettureAnonime.Riutilizzabile(Richiesta(percorso)));

    /// <summary>
    /// <b>La premessa di tutto questo, tenuta ferma.</b>
    ///
    /// <para>Togliere il cookie antiforgery a un anonimo è sicuro <b>perché in questa interfaccia non c'è
    /// niente che un anonimo possa inviare</b>: l'unico form è la ricerca in barra, che è
    /// <c>method="get"</c>, e login e logout sono richieste GET. Non è una proprietà eterna: è vera oggi,
    /// nel codice, e basta un <c>&lt;EditForm&gt;</c> perché smetta di esserlo.</para>
    ///
    /// <para>⚠️ Se questo test diventa rosso <b>non si aggiusta il test</b>: si torna a leggere
    /// <c>CacheDelleLettureAnonime</c> e si decide se quel form sta su una pagina pubblica. È l'unico
    /// modo in cui un form nuovo può accorgersi di aver perso il proprio token — perché altrimenti non se
    /// ne accorgerebbe: fallirebbe soltanto, in produzione, a chi lo usa.</para>
    /// </summary>
    [Fact]
    public void Nellinterfaccia_non_esiste_un_form_da_inviare()
    {
        var colpevoli = Directory
            .EnumerateFiles(CartellaUi(), "*.razor", SearchOption.AllDirectories)
            .Where(f =>
            {
                var s = File.ReadAllText(f);
                return s.Contains("<EditForm", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("method=\"post\"", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("@onsubmit", StringComparison.OrdinalIgnoreCase);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(colpevoli.Count == 0,
            $"Compare un form che si invia: {string.Join(", ", colpevoli)}.\n" +
            "Le letture anonime dei documenti pubblici viaggiano SENZA cookie antiforgery, e la ragione è " +
            "che non c'era niente da inviare. Rileggere CacheDelleLettureAnonime prima di toccare questo test.");
    }

    private static DefaultHttpContext Richiesta(string percorso)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = percorso;
        return ctx;
    }

    private static string CartellaUi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
