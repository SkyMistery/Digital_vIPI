using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Gli allegati nei callout (S121, 21 ottobre 2026, committente): il JSON del callout porta, accanto al titolo, gli
/// allegati della biblioteca nella stessa forma del blocco «Allegato».
/// </summary>
public class CalloutAllegatiTests
{
    private static readonly AttachmentRef Loa = new("loa-lirr-lfmm", "LoA Roma–Marsiglia");
    private static readonly AttachmentRef Carta = new("carta-lirf", null, AttachmentDisplayMode.Embedded,
        AttachmentEmbedHeight.Large, AttachmentRotation.Deg90);

    [Fact]
    public void Gli_allegati_si_scrivono_e_si_rileggono_con_modo_altezza_e_rotazione()
    {
        var json = CalloutJson.ConAllegati(null, new[] { Loa, Carta });

        Assert.Equal(new[] { Loa, Carta }, CalloutJson.Allegati(json));
        Assert.Contains("allegato:loa-lirr-lfmm", json);
    }

    /// <summary>Il titolo scritto resta: aggiungere o togliere allegati non lo tocca.</summary>
    [Fact]
    public void Il_titolo_resta_e_senza_niente_il_JSON_torna_vuoto()
    {
        var conTitolo = CalloutJson.ConAllegati("{\"title\":\"Coordinamento ridotto\"}", new[] { Loa });
        Assert.Equal("Coordinamento ridotto", CalloutJson.Titolo(conTitolo));
        Assert.Single(CalloutJson.Allegati(conTitolo));

        var senza = CalloutJson.ConAllegati(conTitolo, Array.Empty<AttachmentRef>());
        Assert.Equal("{\"title\":\"Coordinamento ridotto\"}", senza);

        // «Niente» è la stringa vuota: per gli editor vuol dire «togli», null vorrebbe dire «non toccare».
        Assert.Equal("", CalloutJson.ConAllegati(null, Array.Empty<AttachmentRef>()));
    }

    /// <summary>Un allegato illeggibile (uno schema che non è <c>allegato:</c>) si salta, e gli altri restano.</summary>
    [Fact]
    public void Un_allegato_illeggibile_si_salta()
    {
        var json = "{\"allegati\":[{\"ref\":\"javascript:alert(1)\"},{\"ref\":\"allegato:loa-lirr-lfmm\"},3]}";

        Assert.Equal(new[] { "loa-lirr-lfmm" }, CalloutJson.Allegati(json).Select(a => a.Slug));
        Assert.Empty(CalloutJson.Allegati("non è json"));
        Assert.Empty(CalloutJson.Allegati(null));
    }

    /// <summary>
    /// 🔴 Il callout con gli allegati è contenuto editoriale, non il payload di una scheda: altrimenti l'editor non lo
    /// mostrerebbe, e una scheda della pagina potrebbe scriverci sopra. Una chiave che non è sua resta una domanda per
    /// le altre regole.
    /// </summary>
    [Theory]
    [InlineData("{\"title\":\"x\"}", true)]
    [InlineData("{\"allegati\":[{\"ref\":\"allegato:loa-lirr-lfmm\"}]}", true)]
    [InlineData("{\"title\":\"x\",\"allegati\":[]}", true)]
    [InlineData("{\"title\":\"x\",\"variant\":\"y\"}", false)]
    [InlineData("{\"allegati\":\"non un elenco\"}", false)]
    [InlineData("{}", false)]
    public void Un_callout_coi_suoi_allegati_e_editoriale(string json, bool editoriale)
    {
        Assert.Equal(editoriale, SectionPayload.EEditoriale(json));
    }

    [Fact]
    public void Un_callout_col_solo_allegato_conta_come_scritto()
    {
        Assert.True(SezioniDaScrivere.JsonHaContenuto(CalloutJson.ConAllegati(null, new[] { Loa })));
    }

    /// <summary>Nelle pagine (intro, sezioni extra) un callout coi soli allegati si salva e si mostra: il testo non è
    /// obbligatorio, il contenuto sì.</summary>
    [Fact]
    public void Nelle_pagine_un_callout_coi_soli_allegati_resta()
    {
        var blocco = new ExtraBlock { Format = BlockFormat.Callout, CalloutJson = CalloutJson.ConAllegati(null, new[] { Loa }) };

        var riletti = ExtraBlocks.Parse(ExtraBlocks.Serialize(new[] { blocco }));

        var r = Assert.Single(riletti);
        Assert.Equal(new[] { Loa }, CalloutJson.Allegati(r.CalloutJson));
        Assert.Contains("LoA Roma–Marsiglia", ExtraBlocks.PlainText(ExtraBlocks.Serialize(new[] { blocco })));
        Assert.Null(ExtraBlocks.Serialize(new[] { new ExtraBlock { Format = BlockFormat.Callout } }));
    }
}
