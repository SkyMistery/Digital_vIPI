using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'indice di un documento, condiviso fra i viewer (carta <c>2026-08-27-vsop-militari.md</c> §12, S1).
///
/// <para>
/// ⚠️ <b>Il difetto che presidia è reale.</b> I quattro indici elencavano le sole sezioni <b>radice</b>. Su
/// un documento a un livello solo è la stessa cosa; sul vSOP militare, dove <b>venti sezioni su ventisei
/// sono figlie</b>, l'indice ne mostrava sei — e «Radioassistenze», che è dove si va a leggere una
/// frequenza in fretta, si trovava solo scorrendo il documento.
/// </para>
/// </summary>
public class DocumentTocTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public DocumentTocTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static SectionView Sez(string id, string titolo, bool nascosta = false,
                                   params SectionView[] figlie) => new()
    {
        Id = id,
        Title = titolo,
        Depth = 0,
        SectionKey = titolo.ToLowerInvariant(),
        IsHidden = nascosta,
        Blocks = Array.Empty<BlockView>(),
        Children = figlie,
    };

    private IRenderedComponent<DocumentToc> Indice(IReadOnlyList<SectionView> sezioni, bool bozza = false,
                                                   Func<SectionView, SectionView>? figlieDi = null) =>
        RenderComponent<DocumentToc>(p =>
            p.Add(x => x.Gruppi, TocGruppo.Uno(sezioni, bozza, figlieDi: figlieDi)));

    /// <summary>Una radice senza figlie resta quel che era: un link, senza involucro.</summary>
    [Fact]
    public void Una_sezione_senza_figlie_e_un_link_e_basta()
    {
        var cut = Indice(new[] { Sez("s-1", "METAR & TAF") });

        Assert.Empty(cut.FindAll("details.toc-sub").ToList());
        var link = cut.FindAll("a").ToList();
        Assert.Single(link);
        Assert.Equal("#s-1", link[0].GetAttribute("href"));
    }

    /// <summary>La richiesta del committente: «Dati generali» si espande e sotto escono le figlie.</summary>
    [Fact]
    public void Una_sezione_con_figlie_le_elenca_sotto_di_se()
    {
        var cut = Indice(new[]
        {
            Sez("s-1", "Dati generali", false, Sez("s-2", "Radioassistenze"), Sez("s-3", "Frequenze ATC/CRC")),
        });

        var sub = cut.FindAll("details.toc-sub").ToList();
        Assert.Single(sub);
        // 🔴 CHIUSO di default, dal 6 settembre 2026 (richiesta del committente). Prima nasceva aperto,
        // con una ragione scritta accanto — «un indice che nasce chiuso costringe a due clic» — che vale
        // finché le figlie sono poche: sul vSOP militare sono venticinque, e un sommario lungo quanto il
        // documento non aiuta più a cercare.
        Assert.False(sub[0].HasAttribute("open"));

        // Il padre resta un LINK: il chevron apre, il titolo porta alla sezione.
        Assert.Equal("#s-1", cut.Find("details.toc-sub > summary > a").GetAttribute("href"));

        var figlie = cut.FindAll("a.lvl3").ToList();
        Assert.Equal(2, figlie.Count);
        Assert.Equal("#s-2", figlie[0].GetAttribute("href"));
        Assert.Equal("Radioassistenze", figlie[0].TextContent.Trim());
        Assert.Equal("#s-3", figlie[1].GetAttribute("href"));
    }

    /// <summary>
    /// «Nascosta» vale anche per le figlie, e nell'indice come nel documento: in pubblica non ci sono, in
    /// anteprima bozza sì. ⚠️ È la stessa regola che nel 2026 era finita solo sulle radici — chi la scrive a
    /// metà pubblica una sezione che qualcuno aveva deciso di nascondere.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Una_figlia_nascosta_si_vede_solo_in_bozza(bool bozza, int atteseFiglie)
    {
        var cut = Indice(new[]
        {
            Sez("s-1", "Procedure di volo", false,
                Sez("s-2", "Restrizioni al decollo"),
                Sez("s-3", "QRA / Scramble", nascosta: true)),
        }, bozza);

        Assert.Equal(atteseFiglie, cut.FindAll("a.lvl3").ToList().Count);
    }

    /// <summary>Una radice nascosta sparisce con tutte le sue figlie, non solo con la propria voce.</summary>
    [Fact]
    public void Una_radice_nascosta_non_lascia_dietro_le_sue_figlie()
    {
        var cut = Indice(new[]
        {
            Sez("s-1", "Bassa quota", nascosta: true, Sez("s-2", "Aree BOAT")),
            Sez("s-9", "Validità e revisione"),
        });

        var link = cut.FindAll("a").ToList();
        Assert.Single(link);
        Assert.Equal("#s-9", link[0].GetAttribute("href"));
    }

    /// <summary>
    /// La testata è <b>sempre</b> la stessa parola, per tutte e cinque le famiglie. ⚠️ Non era così fino al
    /// 6 settembre 2026: quattro dicevano «Sommario»/«Contents» e la vIPI ACC «Navigazione»/«Navigation»,
    /// perché aveva un indice tutto suo e puntava a un'altra chiave — quella che usano gli EDITOR.
    /// </summary>
    [Fact]
    public void La_testata_e_sempre_la_stessa_chiave()
    {
        var cut = Indice(new[] { Sez("s-1", "METAR & TAF") });

        // ⚠️ Qui si legge la PAROLA vera e non la chiave: `StringheDelSito` non passa dal localizzatore
        // finto di questi test, legge le risorse. Che è quel che serve — la richiesta era sulla parola.
        Assert.Equal("Summary", cut.Find("aside.toc > p.toc-h").TextContent.Trim());
        Assert.Single(cut.FindAll("p.toc-h"));   // una sola testata, anche coi gruppi
    }

    /// <summary>
    /// I GRUPPI stanno dentro lo <b>stesso</b> riquadro, con la loro intestazione. È la forma con cui ci
    /// stanno sia i blocchi della vIPI ACC sia i membri di una pagina unita.
    ///
    /// <para>🔴 Un riquadro solo non è un vezzo: <c>position:sticky</c> si appende al <b>genitore</b>, e
    /// impilare due riquadri obbligava ad avvolgerli in un <c>&lt;div&gt;</c> alto quanto loro — che è la
    /// ragione, misurata, per cui su tre documenti su cinque il sommario se ne andava in cima scorrendo.</para>
    /// </summary>
    [Fact]
    public void I_gruppi_stanno_nello_stesso_riquadro_con_la_loro_intestazione()
    {
        var cut = RenderComponent<DocumentToc>(p => p.Add(x => x.Gruppi, new[]
        {
            new TocGruppo("Settori di aerovia", TocVoce.Da(new[] { Sez("s-1", "Frequenze") }, bozza: false)),
            new TocGruppo("Gruppo APP", TocVoce.Da(new[] { Sez("s-2", "Separazioni") }, bozza: false)),
        }));

        Assert.Single(cut.FindAll("aside.toc"));
        Assert.Equal(new[] { "Settori di aerovia", "Gruppo APP" },
                     cut.FindAll("p.toc-grp").Select(e => e.TextContent.Trim()).ToArray());
        Assert.Equal(2, cut.FindAll("a").Count);
    }

    /// <summary>
    /// Il terzo livello ha un rientro suo. ⚠️ Il modello ne ammette tre
    /// (<c>DocumentSection.MaxDepth</c>), e senza una classe propria una nipote si leggerebbe alla stessa
    /// altezza di sua madre — cioè l'indice direbbe una gerarchia che il documento non ha.
    /// </summary>
    [Fact]
    public void Il_terzo_livello_rientra_piu_del_secondo()
    {
        var cut = Indice(new[]
        {
            Sez("s-1", "Aree di lavoro", false,
                Sez("s-2", "Procedure generali", false,
                    Sez("s-3", "Bassa quota"))),
        });

        Assert.Equal(2, cut.FindAll("details.toc-sub").Count);   // madre e figlia
        Assert.Equal("#s-2", cut.Find("a.lvl3").GetAttribute("href"));
        Assert.Equal("#s-3", cut.Find("a.lvl4").GetAttribute("href"));
    }

    /// <summary>
    /// ⚠️ Chi disegna certe figlie da sé le toglie <b>anche</b> dall'indice, con la stessa funzione che passa
    /// a <c>DocumentSectionsView</c>: la vLOA rende le due direzioni dei coordinamenti come intestazioni sue,
    /// e quelle <b>non hanno un id</b>. Elencarle darebbe due voci che non portano da nessuna parte — e un
    /// link che non fa niente non dà errori, quindi resterebbe lì.
    /// </summary>
    [Fact]
    public void Le_figlie_che_la_pagina_disegna_da_se_restano_fuori()
    {
        var coordinamenti = Sez("s-1", "Coordination", false,
            Sez("s-2", "LIBB → LDZO"), Sez("s-3", "LDZO → LIBB"));

        var senzaDirezioni = (SectionView s) => s.Id == "s-1"
            ? new SectionView
            {
                Id = s.Id, Title = s.Title, Depth = s.Depth, SectionKey = s.SectionKey,
                Blocks = s.Blocks, Children = Array.Empty<SectionView>(),
            }
            : s;

        var cut = Indice(new[] { coordinamenti }, figlieDi: senzaDirezioni);

        Assert.Empty(cut.FindAll("a.lvl3").ToList());
        Assert.Empty(cut.FindAll("details.toc-sub").ToList());
        Assert.Equal("#s-1", cut.Find("a").GetAttribute("href"));
    }
}
