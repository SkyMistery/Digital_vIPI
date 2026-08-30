using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// L'intro di pagina (carta <c>2026-08-30-intro-di-pagina.md</c>): quel che si salva è la forma che lo staff
/// scrive, quel che si rende è una <b>proiezione</b>. Qui si prova che il giro completo non perde niente e
/// che quel che è vuoto o rotto non arriva in cima a una pagina pubblica.
/// </summary>
public class PageIntroTests
{
    private static PageIntroSection Sezione(string titolo, params ExtraBlock[] blocchi) =>
        new() { Title = titolo, Blocks = blocchi.ToList() };

    private static ExtraBlock Prosa(string testo) =>
        new() { Format = BlockFormat.Prose, Text = testo };

    private static ExtraBlock Allegato(string slug, string? nota = null) => new()
    {
        Format = BlockFormat.Attachment,
        Text = nota,
        AttachmentJson = AttachmentRef.Serialize(new AttachmentRef(slug, null)),
    };

    [Fact]
    public void Il_giro_completo_non_perde_niente()
    {
        var originali = new List<PageIntroSection>
        {
            Sezione("Documenti generali", Prosa("Leggere prima di controllare."), Allegato("manuale-mil")),
        };

        var lette = PageIntro.Parse(PageIntro.Serialize(originali));

        var sola = Assert.Single(lette);
        Assert.Equal("Documenti generali", sola.Title);
        Assert.Equal(2, sola.Blocks.Count);
        Assert.Equal("manuale-mil", AttachmentRef.Parse(sola.Blocks[1].AttachmentJson)!.Slug);
    }

    /// <summary>Niente da salvare è <c>null</c>, non <c>{"sections":[]}</c>: la colonna dice «non c'è», e la
    /// pagina non deve distinguere fra due modi di essere vuota.</summary>
    [Fact]
    public void Senza_sezioni_non_si_salva_niente()
    {
        Assert.Null(PageIntro.Serialize(new List<PageIntroSection>()));
        Assert.Null(PageIntro.Serialize(new List<PageIntroSection> { Sezione("   ") }));
    }

    /// <summary>⚠️ Un corpo illeggibile non diventa una sezione di prosa col JSON dentro: sarebbe del JSON
    /// stampato in cima a una pagina pubblica.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non sono json")]
    [InlineData("{\"sections\":\"e nemmeno questo\"}")]
    public void Un_corpo_che_non_capiamo_non_produce_sezioni(string? json)
    {
        Assert.Empty(PageIntro.Parse(json));
    }

    /// <summary>La radice è un ARRAY e non un oggetto: <c>JsonSerializer</c> non alza <c>JsonException</c>
    /// su tutto, e questo caso ha già morso una volta.</summary>
    [Fact]
    public void Una_radice_array_non_fa_esplodere_la_lettura()
    {
        Assert.Empty(PageIntro.Parse("[1,2,3]"));
    }

    [Fact]
    public void La_vista_porta_una_sezione_per_sezione_con_ancora_stabile()
    {
        var view = PageIntro.ToView(new List<PageIntroSection>
        {
            Sezione("Prima", Prosa("a")),
            Sezione("Seconda", Prosa("b")),
        });

        Assert.Equal(2, view.Sections.Count);
        Assert.Equal("pi-1", view.Sections[0].Id);
        Assert.Equal("pi-2", view.Sections[1].Id);
        Assert.Equal("Seconda", view.Sections[1].Title);
        Assert.All(view.Sections, s => Assert.Equal(0, s.Depth));
        Assert.All(view.Sections, s => Assert.Empty(s.Children));
    }

    /// <summary>La lingua sorgente sta sulla vista: senza, il traduttore non saprebbe da dove traduce e il
    /// lettore inglese resterebbe con l'italiano senza che nulla protesti.</summary>
    [Fact]
    public void La_vista_dichiara_la_lingua_in_cui_l_intro_e_scritta()
    {
        var view = PageIntro.ToView(new List<PageIntroSection> { Sezione("Titolo", Prosa("testo")) });

        Assert.Equal(Language.It, view.Language);
    }

    /// <summary>Stessa regola della cottura degli extra: prosa senza testo e allegato senza riferimento non
    /// entrano in un documento, e non entrano nemmeno qui.</summary>
    [Fact]
    public void I_blocchi_senza_contenuto_non_arrivano_alla_vista()
    {
        var view = PageIntro.ToView(new List<PageIntroSection>
        {
            Sezione("Titolo",
                Prosa("   "),
                new ExtraBlock { Format = BlockFormat.Attachment, AttachmentJson = null },
                new ExtraBlock { Format = BlockFormat.Image, ImageJson = null },
                Prosa("questo si vede")),
        });

        var blocco = Assert.Single(view.Sections[0].Blocks);
        Assert.Equal("questo si vede", blocco.Body);
    }

    /// <summary>⚠️ Gli id sono unici DENTRO la vista: due blocchi con lo stesso id in sezioni diverse farebbero
    /// riusare a Blazor il nodo sbagliato.</summary>
    [Fact]
    public void Gli_id_dei_blocchi_non_si_ripetono_fra_sezioni()
    {
        var view = PageIntro.ToView(new List<PageIntroSection>
        {
            Sezione("Prima", Prosa("a"), Prosa("b")),
            Sezione("Seconda", Prosa("c"), Prosa("d")),
        });

        var id = view.Sections.SelectMany(s => s.Blocks).Select(b => b.Id).ToList();
        Assert.Equal(4, id.Count);
        Assert.Equal(id.Count, id.Distinct().Count());
    }

    /// <summary>Il blocco allegato arriva alla vista col riferimento nel <c>BodyJson</c>, che è dove
    /// <c>AttachmentBlock</c> lo cerca: la nota resta il corpo.</summary>
    [Fact]
    public void Il_blocco_allegato_arriva_come_lo_vuole_il_viewer()
    {
        var view = PageIntro.ToView(new List<PageIntroSection>
        {
            Sezione("Documenti", Allegato("circolare-01", "in vigore dal 1 settembre")),
        });

        var blocco = Assert.Single(view.Sections[0].Blocks);
        Assert.Equal(BlockFormat.Attachment, blocco.Format);
        Assert.Equal("in vigore dal 1 settembre", blocco.Body);
        Assert.Equal("circolare-01", AttachmentRef.Parse(blocco.BodyJson)!.Slug);
    }

    /// <summary>Il prefisso della chiave è ciò che permette alla seconda pagina di registrarne una invece di
    /// inventarsi un secondo meccanismo.</summary>
    [Fact]
    public void La_chiave_e_prefissata_e_normalizzata()
    {
        Assert.Equal("page-intro:mil", PageIntro.Chiave(" MIL "));
    }
}
