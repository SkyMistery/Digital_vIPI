using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// La vIPI ACC nella lingua di chi legge (carta <c>2026-08-27-documenti-bilingue.md</c> §7).
///
/// <para>
/// ⚠️ <b>Perché una famiglia ha bisogno di test suoi.</b> La vIPI ACC è l'unica che non arriva alla pagina
/// come <see cref="DocumentView"/>: vive a blocchi, e i suoi testi stanno in tre posti diversi — il titolo
/// del blocco, il titolo della sezione, e la parte editoriale della sezione. Fino al 28 agosto 2026 la
/// pagina non traduceva niente: il documento restava in italiano dentro un'interfaccia inglese, e nessun
/// test se ne accorgeva perché il traduttore, da solo, funzionava.
/// </para>
/// </summary>
public class AccVipiTranslatorTests
{
    private static SectionView Editoriale(string titolo, string corpo) => new()
    {
        Id = "s-1",
        Title = titolo,
        Depth = 1,
        SectionKey = "custom:abc",
        Blocks = new[]
        {
            new BlockView { Id = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = corpo },
        },
        Children = Array.Empty<SectionView>(),
    };

    private static AccVipiData Dati(AccBlock blocco) => new()
    {
        AccCode = "LIBB",
        AccName = "Brindisi",
        Blocks = new List<AccBlock> { blocco },
    };

    private static AccBlock Blocco(string titolo, params AccBlockSection[] sezioni) => new()
    {
        Key = "grp:uno",
        Kind = AccBlockKind.AppGroup,
        Title = titolo,
        MemberCallsigns = new List<string> { "LIBR_APP" },
        Sections = sezioni.ToList(),
        Configurations = new List<AccConfiguration>
        {
            new() { Key = "cfg:uno", Name = "2 settori (NE/SW)" },
        },
    };

    [Fact]
    public async Task I_titoli_e_la_prosa_passano_alla_lingua_di_chi_legge()
    {
        var memoria = new MemoriaDiTraduzioneFinta()
            .Nota("Gruppo APP di Bari", "Bari APP group")
            .Nota("Separazioni", "Separations")
            .Nota("Contatta la torre.", "Contact the tower.");

        var dati = Dati(Blocco("Gruppo APP di Bari",
            new AccBlockSection(7, "custom:abc", "Separazioni", false, Editoriale("Separazioni", "Contatta la torre."))));

        var copertura = await new AccVipiTranslator(new DocumentTranslator(memoria))
            .TranslateAsync(dati, Language.It, "en");

        var blocco = dati.Blocks[0];
        Assert.Equal("Bari APP group", blocco.Title);
        Assert.Equal("Separations", blocco.Sections[0].Title);
        Assert.Equal("Separations", blocco.Sections[0].Editorial!.Title);
        Assert.Equal("Contact the tower.", blocco.Sections[0].Editorial!.Blocks[0].Body);
        Assert.True(copertura.Completa);
        Assert.True(copertura.DaRileggere);   // nessuno l'ha riletta: la pagina lo deve dire
    }

    [Fact]
    public async Task Quel_che_non_e_lingua_non_si_tocca()
    {
        // ⚠️ Il traduttore modifica i blocchi SUL POSTO proprio per questo: ricostruirli vorrebbe dire
        // ricopiare a mano quindici campi che con la lingua non c'entrano, e il primo dimenticato tornerebbe
        // al suo default in silenzio — con la pagina che continua a rendersi.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Gruppo APP di Bari", "Bari APP group");
        var dati = Dati(Blocco("Gruppo APP di Bari",
            new AccBlockSection(7, "aor", "AoR", false, null)));

        await new AccVipiTranslator(new DocumentTranslator(memoria)).TranslateAsync(dati, Language.It, "en");

        var blocco = dati.Blocks[0];
        Assert.Equal("grp:uno", blocco.Key);
        Assert.Equal(AccBlockKind.AppGroup, blocco.Kind);
        Assert.Equal(new[] { "LIBR_APP" }, blocco.MemberCallsigns);
        Assert.Equal("cfg:uno", blocco.Configurations[0].Key);
        Assert.Equal(7, blocco.Sections[0].SectionId);
        Assert.Equal("aor", blocco.Sections[0].Key);
        Assert.Null(blocco.Sections[0].Editorial);   // sezione resa dalla pagina: niente corpo da tradurre
    }

    [Fact]
    public async Task Letta_nella_sua_lingua_non_costa_una_query()
    {
        var memoria = new MemoriaDiTraduzioneFinta();
        var dati = Dati(Blocco("Gruppo APP di Bari",
            new AccBlockSection(7, "custom:abc", "Separazioni", false, Editoriale("Separazioni", "Contatta la torre."))));

        var copertura = await new AccVipiTranslator(new DocumentTranslator(memoria))
            .TranslateAsync(dati, Language.It, "it");

        Assert.Equal(0, memoria.Letture);
        Assert.Equal(0, copertura.Segmenti);
        Assert.Equal("Gruppo APP di Bari", dati.Blocks[0].Title);
    }

    [Fact]
    public async Task La_memoria_si_interroga_UNA_volta_sola_per_tutti_i_blocchi()
    {
        // ⚠️ Una query per sezione sarebbe una corsa sul DbContext del circuito Blazor.
        var memoria = new MemoriaDiTraduzioneFinta();
        var dati = new AccVipiData
        {
            AccCode = "LIBB",
            AccName = "Brindisi",
            Blocks = new List<AccBlock>
            {
                Blocco("Uno", new AccBlockSection(1, "custom:a", "S1", false, Editoriale("S1", "a"))),
                Blocco("Due", new AccBlockSection(2, "custom:b", "S2", false, Editoriale("S2", "b"))),
            },
        };

        await new AccVipiTranslator(new DocumentTranslator(memoria)).TranslateAsync(dati, Language.It, "en");

        Assert.Equal(1, memoria.Letture);
    }

    [Fact]
    public async Task Le_traduzioni_congelate_dalla_release_vincono_sulla_memoria_viva()
    {
        // ⚠️ Senza questa preferenza una correzione fatta oggi su un altro documento cambierebbe l'inglese
        // già pubblicato di questo, sotto gli occhi di chi lo sta leggendo.
        //
        // ⚠️ E dove il congelato NON arriva, vince la memoria: qui lo snapshot porta solo il corpo, e i due
        // titoli — «Uno» e «S» — nella release erano rimasti in italiano. Prima del 28 agosto 2026 la sola
        // presenza di una voce congelata spegneva la memoria per tutto il documento, e quei titoli
        // restavano italiani per sempre dentro una pagina inglese.
        var memoria = new MemoriaDiTraduzioneFinta()
            .Nota("Contatta la torre.", "RESA CORRETTA DOPO LA PUBBLICAZIONE")
            .Nota("Uno", "One")
            .Nota("S", "S-en");
        var congelate = new Dictionary<string, Dictionary<string, FrozenTranslation>>
        {
            ["en"] = new() { [TranslationText.Hash("Contatta la torre.")] = new("Contact the tower.", false) },
        };

        var dati = Dati(Blocco("Uno",
            new AccBlockSection(1, "custom:a", "S", false, Editoriale("S", "Contatta la torre."))));

        await new AccVipiTranslator(new DocumentTranslator(memoria))
            .TranslateAsync(dati, Language.It, "en", congelate);

        // Il corpo resta quello che la release aveva fotografato, non la correzione arrivata dopo.
        Assert.Equal("Contact the tower.", dati.Blocks[0].Sections[0].Editorial!.Blocks[0].Body);
        // I titoli, che il congelato non copriva, li riempie la memoria viva.
        Assert.Equal("One", dati.Blocks[0].Title);
        Assert.Equal("S-en", dati.Blocks[0].Sections[0].Title);

        // ⚠️ Una lettura sola, e solo per le impronte SCOPERTE: quella congelata non si richiede.
        Assert.Equal(1, memoria.Letture);
        Assert.False(memoria.HaChiesto("Contatta la torre."));
    }

    [Fact]
    public async Task Senza_lingua_sul_documento_la_vIPI_ACC_si_legge_come_italiana()
    {
        // I documenti salvati prima che il campo esistesse arrivano con la lingua nulla: la vIPI nasce in
        // italiano, ed è da lì che si traduce.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Uno", "One");
        var dati = Dati(Blocco("Uno", new AccBlockSection(1, "aor", "AoR", false, null)));

        await new AccVipiTranslator(new DocumentTranslator(memoria)).TranslateAsync(dati, null, "en");

        Assert.Equal("it", memoria.UltimaSorgente);
        Assert.Equal("One", dati.Blocks[0].Title);
    }
}
