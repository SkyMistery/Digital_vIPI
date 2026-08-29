using System.Reflection;
using Microsoft.AspNetCore.Components;
using Vipi.Application.Routing;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Ogni indirizzo che un <see cref="IDocKindRoutes"/> restituisce deve portare a una pagina che <b>esiste</b>.
///
/// <para>
/// ⚠️ <b>Perché serve una rete e non basta l'attenzione.</b> I descrittori di rotta vivono in
/// <c>Vipi.Application</c> e le pagine in <c>Vipi.Ui</c>: nessun compilatore lega le due cose, e un indirizzo
/// sbagliato non si vede rileggendo il file — si vede solo cliccandoci sopra, e solo se qualcuno ci clicca.
/// È già successo: <c>MilDocRoutes</c> dichiarava un <c>EditorUrl</c> verso una pagina che non era mai stata
/// scritta (carta vSOP militari §6), e se ne è accorto chi ha provato a modificare il documento.
/// </para>
/// <para>
/// ⚠️ <c>null</c> <b>passa</b>, ed è voluto: il contratto lo prevede per «questo indirizzo non c'è» — la vLOA
/// senza vicino, l'APP militare finché le sue pagine non esistono. Dire «non c'è» è onesto; dire un indirizzo
/// che porta a una pagina bianca non lo è.
/// </para>
/// </summary>
public class RotteDeiDocumentiEsistonoTests
{
    /// <summary>I template di rotta dichiarati dai componenti (<c>@page</c>), es. <c>/services/vsop/{Acc}/mil</c>.</summary>
    private static readonly string[] Template = typeof(AudienceChip).Assembly.GetTypes()
        .Where(t => typeof(IComponent).IsAssignableFrom(t))
        .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
        .Select(r => r.Template)
        .ToArray();

    private static readonly IDocKindRoutes[] Descrittori =
    {
        new VloaDocRoutes(), new AppDocRoutes(), new AccVipiDocRoutes(),
        new AirportDocRoutes(), new AirportMilDocRoutes(), new AppMilDocRoutes(),
    };

    /// <summary>
    /// Vero se il percorso corrisponde a un template: stesso numero di segmenti, e ogni segmento uguale a
    /// quello del template salvo i parametri <c>{...}</c>, che accettano qualunque cosa.
    /// </summary>
    private static bool Esiste(string url)
    {
        var percorso = url.Split('?')[0].TrimEnd('/');
        var seg = percorso.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return Template.Any(t =>
        {
            var ts = t.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return ts.Length == seg.Length
                && ts.Zip(seg).All(p => p.First.StartsWith('{')
                                        || string.Equals(p.First, p.Second, StringComparison.OrdinalIgnoreCase));
        });
    }

    public static TheoryData<string> TuttiGliIndirizzi()
    {
        var dati = new TheoryData<string>();
        foreach (var d in Descrittori)
        {
            // Argomenti verosimili: un ACC, una chiave e un vicino — senza il vicino la vLOA torna null e
            // metà dei suoi indirizzi non verrebbero provati affatto.
            foreach (var url in new[]
                     {
                         d.ViewerUrl("lirr", "LIRF", "LIMM", 7),
                         d.PublicUrl("lirr", "LIRF", "LIMM"),
                         d.EditorUrl("lirr", "LIRF", "LIMM", 42),
                         d.DraftUrl("lirr", "LIRF", "LIMM"),
                     })
                if (url is not null) dati.Add(url);
        }
        return dati;
    }

    [Theory]
    [MemberData(nameof(TuttiGliIndirizzi))]
    public void Ogni_indirizzo_dichiarato_porta_a_una_pagina_che_esiste(string url) =>
        Assert.True(Esiste(url), $"Nessuna pagina risponde a «{url}»: il descrittore dichiara una rotta che non c'è.");

    [Fact]
    public void L_APP_MILITARE_dichiara_che_le_sue_pagine_NON_ci_sono()
    {
        // ⚠️ Non è un test «di comodo» per far passare quello sopra: mette per iscritto una DECISIONE. Il
        // giorno in cui le pagine si scrivono, questo test diventa rosso ed è il promemoria giusto — insieme
        // ai tre punti elencati su `AppMilDocRoutes`.
        var mil = new AppMilDocRoutes();

        Assert.Null(mil.PublicUrl("lirr", "LIRP_APP", null));
        Assert.Null(mil.EditorUrl("lirr", "LIRP_APP", null, null));
        Assert.Null(mil.DraftUrl("lirr", "LIRP_APP", null));
        Assert.Null(mil.ViewerUrl("lirr", "LIRP_APP", null, 1));
    }

    [Fact]
    public void L_AEROPORTO_militare_invece_le_ha_TUTTE()
    {
        var mil = new AirportMilDocRoutes();

        Assert.All(new[]
        {
            mil.PublicUrl("lirr", "LIPI", null), mil.EditorUrl("lirr", "LIPI", null, null),
            mil.DraftUrl("lirr", "LIPI", null), mil.ViewerUrl("lirr", "LIPI", null, 3),
        }, u => Assert.True(u is not null && Esiste(u)));
    }
}
