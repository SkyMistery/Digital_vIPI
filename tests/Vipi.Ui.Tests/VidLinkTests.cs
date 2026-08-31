using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete su <c>VidLink</c>, il componente che fa del VID una porta sul profilo IVAO.
///
/// <para><b>Perché serve.</b> L'indirizzo del profilo è scritto in un posto solo apposta — undici punti
/// dell'app lo usano senza conoscerlo — e queste sono le prove che quel posto dice la cosa giusta: il
/// dominio pubblico e non l'IdP, il VID nella query, la finestra nuova (chi sta editando ha un lock aperto
/// e una bozza non salvata) e le tre forme in cui il VID compare a schermo.</para>
/// </summary>
public class VidLinkTests : TestContext
{
    /// <summary>Localizer che si comporta come le risorse vere per le due chiavi che il componente usa:
    /// col solito «la chiave per valore» il testo del link sarebbe stato <c>Audit_VidN</c>, cioè proprio
    /// ciò che queste prove devono guardare.</summary>
    private sealed class FormatLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] => new(
            name,
            name switch
            {
                "Audit_VidN" => $"VID {arguments[0]}",
                "Vid_ProfileTitle" => $"Apri il profilo IVAO — VID {arguments[0]}",
                _ => name,
            },
            resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public VidLinkTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new FormatLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private IRenderedComponent<VidLink> Render(int vid, string? nome = null, bool soloNumero = false) =>
        RenderComponent<VidLink>(p =>
        {
            p.Add(x => x.Vid, vid);
            if (nome is not null) p.Add(x => x.Nome, nome);
            if (soloNumero) p.Add(x => x.SoloNumero, true);
        });

    [Fact]
    public void Il_vid_porta_al_profilo_pubblico_ivao()
    {
        var a = Render(704798).Find("a");

        // ⚠️ ivao.aero, non api.ivao.aero: quello è l'IdP del login, questo è il sito che si apre a una persona.
        Assert.Equal("https://ivao.aero/Member.aspx?Id=704798", a.GetAttribute("href"));
        Assert.Equal("VID 704798", a.TextContent);
    }

    /// <summary>Il profilo sta su un altro sito: portarci via chi sta editando — lock aperto, bozza non
    /// salvata — sarebbe un modo di far perdere lavoro.</summary>
    [Fact]
    public void Si_apre_in_una_finestra_nuova_e_senza_regalare_lopener()
    {
        var a = Render(704798).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener", a.GetAttribute("rel"));
    }

    /// <summary>La forma delle colonne che hanno già «VID» scritto in intestazione (classifica, Diagnostica).</summary>
    [Fact]
    public void Solo_numero_toglie_letichetta_ma_non_il_link()
    {
        var a = Render(704798, soloNumero: true).Find("a");

        Assert.Equal("704798", a.TextContent);
        Assert.EndsWith("Id=704798", a.GetAttribute("href"));
    }

    /// <summary>La forma di Versioni e Permessi. A essere premibile è il solo VID: il nome non porta da
    /// nessuna parte, e la parentesi resta fuori dal link.</summary>
    [Fact]
    public void Col_nome_esce_nome_e_vid_ma_a_essere_link_e_solo_il_vid()
    {
        var cut = Render(704798, nome: "Mario Rossi");

        Assert.Contains("Mario Rossi (", cut.Markup);
        Assert.Equal("VID 704798", cut.Find("a").TextContent);
        Assert.EndsWith(")", cut.Markup.TrimEnd());
    }

    /// <summary>Zero non è una persona: è la riga scritta dal sistema (import, migrazione, seed). Il testo
    /// resta, il link no — dall'altra parte non c'è nessun profilo.</summary>
    [Fact]
    public void Il_vid_zero_non_e_una_porta()
    {
        var cut = Render(0);

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("VID 0", cut.Markup);
    }
}
