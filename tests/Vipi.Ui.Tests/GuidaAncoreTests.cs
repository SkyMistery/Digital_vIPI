using Bunit;
using Vipi.Application.Content;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le ancore della ricerca globale esistono davvero nella Guida.
///
/// <para>⚠️ Non è un test di forma. Il 25 agosto 2026 la voce «Statistiche ATC» era <b>già</b> nel catalogo
/// della ricerca — e portava a un'ancora che nella Guida <b>non c'era</b>: chi cercava «statistiche»
/// trovava un risultato, lo apriva, e finiva su una pagina senza quel capitolo. Un collegamento morto è
/// peggio di nessun collegamento, perché nessuno lo denuncia.</para>
/// </summary>
public class GuidaAncoreTests : TestContext
{
    [Fact]
    public void Ogni_voce_di_ricerca_ha_il_suo_capitolo_nella_guida()
    {
        var cut = RenderComponent<GuidaPage>();

        var mancanti = GuideSearchCatalog.Entries
            .Select(e => e.Anchor)
            // «admin» è l'unica voce-ombrello: rimanda alle aree admin, non a un capitolo suo.
            .Where(a => a != "admin")
            .Where(a => cut.FindAll($"#{a}").Count == 0)
            .ToList();

        Assert.Empty(mancanti);
    }

    [Fact]
    public void Il_capitolo_delle_statistiche_dice_le_due_pagine()
    {
        var cut = RenderComponent<GuidaPage>();

        var capitolo = cut.Find("#statistiche").TextContent;
        Assert.Contains("/services/stats", capitolo);
        Assert.Contains("/services/stats/division", capitolo);
    }
}
