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

    /// <summary>
    /// Il verso opposto: ogni capitolo della Guida deve avere la sua voce nel catalogo.
    ///
    /// <para>⚠️ <b>Dal 28 agosto 2026 non è più solo una questione di ricerca</b>: il <b>titolo</b> del
    /// capitolo viene dal catalogo, quindi un capitolo che non c'è si renderebbe con la propria ancora al
    /// posto del titolo — «editor-minime» stampato in una testata. Prima di allora i titoli stavano in due
    /// posti e gli inglesi erano divergenti in <b>11 casi su 38</b>: chi cercava leggeva un titolo e ne
    /// apriva un altro, e nessuna delle due copie era sbagliata da sola.</para>
    ///
    /// <para>⚠️ E resta vero il motivo di prima: un capitolo fuori dal catalogo <b>non si trova</b>. Era il
    /// caso di «Minime di vettoramento», che è esattamente ciò che qualcuno cerca.</para>
    /// </summary>
    [Fact]
    public void Ogni_capitolo_della_guida_ha_la_sua_voce_nel_catalogo()
    {
        var cut = RenderComponent<GuidaPage>();
        var ancoreDelCatalogo = GuideSearchCatalog.Entries.Select(e => e.Anchor).ToHashSet(StringComparer.Ordinal);

        // Le sezioni della Guida sono i <details> con la classe che il componente mette a ogni capitolo.
        var senzaVoce = cut.FindAll(".guida-sec")
            .Select(e => e.Id)
            .Where(id => !string.IsNullOrEmpty(id) && !ancoreDelCatalogo.Contains(id!))
            .ToList();

        Assert.True(senzaVoce.Count == 0,
            "Capitoli della Guida senza voce nel catalogo di ricerca: il titolo verrebbe reso come l'ancora " +
            "nuda, e il capitolo non si troverebbe cercando.\n  " + string.Join("\n  ", senzaVoce));
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
