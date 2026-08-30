using Microsoft.AspNetCore.Http;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Gli indirizzi di ieri devono arrivare a quelli di oggi in <b>un salto solo</b>. Non è un modo di dire: il
/// 22 agosto 2026 sono cambiati insieme il prefisso (<c>/vsop</c> → <c>/services/vsop</c>) e dieci segmenti
/// rimasti in italiano, e la strada facile — riscrivere il prefisso e lasciare che il resto lo sistemi un
/// secondo redirect — costa un viaggio di rete in più a ogni apertura di un segnalibro.
///
/// <para>Questi casi sono la tabella di <see cref="LegacyRoutes"/> letta al contrario: per ognuno si pretende
/// l'indirizzo <b>finale</b>, cioè uno che nessuna regola successiva riscriverebbe ancora. Il test in fondo
/// lo verifica in modo generale, senza doversi fidare dell'elenco.</para>
/// </summary>
public sealed class LegacyRoutesTests
{
    private static string? Resolve(string url)
    {
        var i = url.IndexOf('?');
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = i < 0 ? url : url.Substring(0, i);
        ctx.Request.QueryString = i < 0 ? QueryString.Empty : new QueryString(url.Substring(i));
        return LegacyRoutes.Resolve(ctx.Request);
    }

    [Theory]
    // Il prefisso, nudo e con la coda.
    [InlineData("/vsop", "/services/vsop")]
    [InlineData("/vsop/", "/services/vsop")]
    [InlineData("/vsop/changed", "/services/vsop/changed")]
    [InlineData("/vsop/lirr", "/services/vsop/lirr")]
    // Il prefisso di due rinomine fa (Round 12): anche lui arriva a destinazione senza passare da /vsop.
    [InlineData("/sop", "/services/vsop")]
    [InlineData("/sop/changed", "/services/vsop/changed")]
    [InlineData("/sop/guida", "/services/vsop/guide")]
    // I dieci segmenti tradotti il 22 agosto.
    [InlineData("/vsop/guida", "/services/vsop/guide")]
    [InlineData("/vsop/versioni", "/services/vsop/versions")]
    [InlineData("/vsop/lirr/versioni", "/services/vsop/lirr/versions")]
    [InlineData("/vsop/admin/permessi", "/services/vsop/admin/permissions")]
    [InlineData("/vsop/admin/trasferimenti", "/services/vsop/admin/transfers")]
    [InlineData("/vsop/admin/confinanti", "/services/vsop/admin/neighbours")]
    [InlineData("/vsop/admin/diagnostica", "/services/vsop/admin/diagnostics")]
    [InlineData("/vsop/admin/sorgenti", "/services/vsop/admin/sources")]
    [InlineData("/vsop/admin/sectorstructure", "/services/vsop/admin/sector-structure")]
    [InlineData("/vsop/editor/newdoc", "/services/vsop/editor/new-document")]
    // I due alias cancellati: chi li aveva nei preferiti finisce sulla rotta canonica.
    [InlineData("/vsop/admin/aeroporti", "/services/vsop/admin/airports")]
    [InlineData("/vsop/lirr/aeroporto/editor", "/services/vsop/lirr/airports/editor")]
    // La struttura, rinominata a Round 12: un salto solo anche per lei, e verso il nome di OGGI.
    [InlineData("/vsop/admin/struttura", "/services/vsop/admin/sector-structure")]
    // La query si ricopia: chi apriva un aeroporto deve riaprire quell'aeroporto.
    [InlineData("/vsop/lirr/airports?icao=LIRF", "/services/vsop/lirr/airports?icao=LIRF")]
    [InlineData("/vsop?q=lirf", "/services/vsop?q=lirf")]
    [InlineData("/vsop/search?q=separazione&tipo=vloa", "/services/vsop/search?q=separazione&tipo=vloa")]
    // Le viste operative per-ACC: il callsign era in query, oggi è un segmento (doc refactor 12).
    [InlineData("/vsop/lirr/operativa?p=LIRR_CTR", "/services/vsop/live/lirr_ctr")]
    [InlineData("/vsop/lirr/live?p=LIRR_CTR", "/services/vsop/live/lirr_ctr")]
    [InlineData("/vsop/lirr/operativa", "/services/vsop/live")]
    [InlineData("/vsop/lirr/live", "/services/vsop/live")]
    [InlineData("/vsop/limm/live-app?app=LIMC_APP", "/services/vsop/live/limc_app")]
    [InlineData("/vsop/limm/operativa-app?app=LIMC_APP", "/services/vsop/live/limc_app")]
    public void Ogni_indirizzo_storico_arriva_a_quello_di_oggi(string storico, string atteso) =>
        Assert.Equal(atteso, Resolve(storico));

    /// <summary>
    /// Gli endpoint macchina non si spostano: li conoscono <c>render.yaml</c> e la dashboard Render, lo smoke
    /// della CI e i binari del bridge Aurora già distribuiti. Qui si pretende che la tabella li <b>rifiuti</b>,
    /// così se un giorno qualcuno li aggiungesse per simmetria, questo test lo direbbe.
    /// </summary>
    [Theory]
    [InlineData("/vsop/health")]
    [InlineData("/vsop/health/ready")]
    [InlineData("/vsop/api/v1/transfers/resolve")]
    [InlineData("/vsop/live/atc")]
    [InlineData("/vsop/media/abc123")]
    [InlineData("/vsop/files/loa-lirr-lfmm")]
    public void Gli_endpoint_macchina_non_si_redirigono(string macchina) =>
        Assert.Null(Resolve(macchina));

    /// <summary>Ciò che non è un percorso storico non riguarda questa tabella.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/services/vsop")]
    [InlineData("/services/vsop/guide")]
    [InlineData("/signin-oidc")]
    [InlineData("/vsopravvissuto")]     // /vsop non è un prefisso di stringa: è un segmento
    public void Cio_che_non_e_storico_resta_dove_sta(string estraneo) =>
        Assert.Null(Resolve(estraneo));

    /// <summary>
    /// La proprietà che i casi qui sopra illustrano uno per uno: l'indirizzo d'arrivo non è a sua volta un
    /// indirizzo storico. Se lo fosse, il browser farebbe un secondo salto — ed è esattamente ciò che questa
    /// tabella esiste per evitare.
    /// </summary>
    [Theory]
    [InlineData("/vsop/guida")]
    [InlineData("/sop/guida")]
    [InlineData("/sop/admin/struttura")]
    [InlineData("/vsop/admin/aeroporti")]
    [InlineData("/vsop/lirr/operativa?p=LIRR_CTR")]
    public void Nessun_secondo_salto(string storico)
    {
        var arrivo = Resolve(storico);
        Assert.NotNull(arrivo);
        Assert.Null(Resolve(arrivo!));
    }
}
