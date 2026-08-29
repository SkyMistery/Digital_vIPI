using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La pagina «Biblioteca allegati» (<c>/services/vsop/admin/attachments</c>), slice 2 della carta del
/// 25 agosto 2026.
///
/// <para>Presidia le quattro cose che a occhio sembrano dettagli e non lo sono: l'elenco mostra <b>tutto</b>
/// (il catch-22 già pagato con gli APP, dove si vedevano solo i pubblicati e il primo non si poteva creare);
/// i due assi filtrano <b>in AND</b>; lo slug si <b>propone</b> dal titolo ma chi lo scrive comanda; e i
/// rifiuti restano <b>distinti</b>, perché slug occupato e link illeggibile si correggono in due modi
/// diversi.</para>
/// </summary>
public class PaginaAllegatiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeAuthz : IEditAuthorizationService
    {
        public FakeAuthz(VipiRole livello) => Role = livello;
        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    /// <summary>Biblioteca finta: tiene l'elenco, e ricorda l'ultima bozza che le è stata passata.</summary>
    private sealed class BibliotecaFinta : IAttachmentLibrary
    {
        private readonly List<AttachmentRow> _righe;
        private readonly AttachmentCreate _esito;

        public BibliotecaFinta(AttachmentCreate esito = AttachmentCreate.Ok, params AttachmentRow[] righe)
        {
            _esito = esito;
            _righe = righe.ToList();
        }

        public AttachmentDraft? Ultima { get; private set; }

        public Task<IReadOnlyList<AttachmentRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentRow>>(_righe);

        public Task<AttachmentRow?> BySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_righe.FirstOrDefault(r => r.Slug == slug));

        public Task<(AttachmentReplace Esito, AttachmentRow? Riga)> ReplaceAsync(
            string slug, string link, string? note, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();   // la pagina passa dal servizio, non da qui

        public Task<AttachmentDelete> DeleteAsync(string slug, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();   // idem

        public Task<(AttachmentCreate Esito, AttachmentRow? Riga)> CreateAsync(
            AttachmentDraft draft, int userId, CancellationToken ct = default)
        {
            Ultima = draft;
            if (_esito != AttachmentCreate.Ok) return Task.FromResult<(AttachmentCreate, AttachmentRow?)>((_esito, null));

            var riga = Riga(_righe.Count + 1, draft.Slug, draft.Title, draft.Kind, draft.Scope, draft.ScopeKey);
            _righe.Add(riga);
            return Task.FromResult<(AttachmentCreate, AttachmentRow?)>((AttachmentCreate.Ok, riga));
        }
    }

    /// <summary>Uso finto: chi cita cosa, come lo direbbe il servizio che legge i quattro posti veri.</summary>
    private sealed class UsoFinto : IAttachmentUsage
    {
        private readonly Dictionary<string, AttachmentUsage> _uso;

        public UsoFinto(params (string Slug, AttachmentCitation[] Citazioni)[] righe) =>
            _uso = righe.ToDictionary(r => r.Slug, r => new AttachmentUsage(r.Slug, r.Citazioni),
                StringComparer.Ordinal);

        public Task<IReadOnlyDictionary<string, AttachmentUsage>> AllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AttachmentUsage>>(_uso);

        public Task<IReadOnlyList<AttachmentCitation>> WhereUsedAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentCitation>>(
                _uso.TryGetValue(slug, out var u) ? u.Citations : Array.Empty<AttachmentCitation>());
    }

    /// <summary>Cura finta: ricorda che cosa le è stato chiesto, e con che link.</summary>
    private sealed class CuraFinta : IAttachmentCuration
    {
        private readonly AttachmentReplace _esito;
        private readonly AttachmentDelete _esitoDelete;
        private readonly AttachmentCitation[] _impattati;

        public CuraFinta(AttachmentReplace esito = AttachmentReplace.Ok,
            AttachmentDelete esitoDelete = AttachmentDelete.Ok,
            params AttachmentCitation[] impattati)
        {
            _esito = esito;
            _esitoDelete = esitoDelete;
            _impattati = impattati;
        }

        public string? Slug { get; private set; }
        public string? Link { get; private set; }
        public string? Nota { get; private set; }
        public string? Eliminato { get; private set; }

        public Task<IReadOnlyList<AttachmentCitation>> ImpactPreviewAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentCitation>>(_impattati);

        public Task<AttachmentReplacementOutcome> ReplaceAsync(
            string slug, string link, string? note, int userId, CancellationToken ct = default)
        {
            Slug = slug; Link = link; Nota = note;

            var riga = _esito == AttachmentReplace.Ok ? Riga(1, slug, "LoA Roma–Marseille", versione: 3) : null;
            return Task.FromResult(new AttachmentReplacementOutcome(_esito, riga, _impattati));
        }

        public Task<AttachmentDeletionOutcome> DeleteAsync(string slug, int userId, CancellationToken ct = default)
        {
            Eliminato = slug;
            return Task.FromResult(new AttachmentDeletionOutcome(_esitoDelete, _impattati));
        }
    }

    private static AttachmentRow Riga(int id, string slug, string titolo,
        AttachmentKind tipo = AttachmentKind.Loa, AttachmentScope ambito = AttachmentScope.Division,
        string? chiave = null, int versione = 1) =>
        new(id, slug, titolo, tipo, ambito, chiave, null, versione, versione,
            AttachmentProvider.Drive, "1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW",
            new DateTime(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc));

    private IRenderedComponent<AdminAttachmentsPage> Render(
        IAttachmentLibrary biblioteca, VipiRole livello = VipiRole.Editor, IAttachmentUsage? uso = null,
        IAttachmentCuration? cura = null)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        Services.AddSingleton(biblioteca);
        Services.AddSingleton(uso ?? new UsoFinto());
        Services.AddSingleton(cura ?? new CuraFinta());
        return RenderComponent<AdminAttachmentsPage>();
    }

    /// <summary>
    /// ⚠️ L'elenco mostra anche la voce che non cita nessuno. Un elenco delle sole voci citate renderebbe
    /// irraggiungibile la <b>prima</b> voce caricata: è il catch-22 già pagato con l'elenco degli APP.
    /// </summary>
    [Fact]
    public void Lelenco_mostra_anche_la_voce_che_non_cita_nessuno()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")));

        Assert.Contains("LoA Roma–Marseille", cut.Markup);
        Assert.Single(cut.FindAll("table.res-table tbody tr"));
    }

    /// <summary>Lo slug si mostra <b>come si cita</b>: è quello che si copia dentro un documento, e mostrarlo
    /// nudo vorrebbe dire lasciar indovinare il prefisso.</summary>
    [Fact]
    public void Lo_slug_si_mostra_come_si_cita()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")));

        Assert.Contains("allegato:loa-lirr-lfmm", cut.Markup);
    }

    /// <summary>
    /// ⚠️ Anche di qui si passa dalla <b>nostra</b> rotta: l'indirizzo del deposito non compare in nessun
    /// link, nemmeno in una pagina di servizio. È la stessa ragione per cui non compare nei documenti —
    /// il giorno che il deposito cambia, non c'è niente da riscrivere.
    /// </summary>
    [Fact]
    public void Il_tasto_apri_passa_dalla_nostra_rotta()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")));

        var link = cut.Find("table.res-table tbody a");
        Assert.Equal("/vsop/files/loa-lirr-lfmm", link.GetAttribute("href"));
        Assert.DoesNotContain("drive.google.com", cut.Markup);
    }

    /// <summary>La pagina è degli Editor: chi ha meno vede il rifiuto, non una tabella che non risponde.</summary>
    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    [InlineData(VipiRole.DivisionStaff)]
    public void Chi_non_edita_vede_il_rifiuto_non_la_tabella(VipiRole livello)
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")), livello);

        Assert.Empty(cut.FindAll("table.res-table"));
        Assert.Contains("Vle_Unauthorized", cut.Markup);
    }

    /// <summary>I due assi sono due filtri, e si compongono in AND: «le LoA di Roma» è tipo + ambito.</summary>
    [Fact]
    public void I_due_assi_filtrano_insieme()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok,
            Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille", AttachmentKind.Loa, AttachmentScope.Acc, "LIRR"),
            Riga(2, "loa-limm-lsas", "LoA Milano–Svizzera", AttachmentKind.Loa, AttachmentScope.Acc, "LIMM"),
            Riga(3, "circolare-01", "Circolare 01", AttachmentKind.Circular, AttachmentScope.Division)));

        Assert.Equal(3, cut.FindAll("table.res-table tbody tr").Count);

        // Tipo «LoA»: la circolare esce.
        cut.FindAll("button.sh-chip").ToArray()[(int)AttachmentKind.Loa].Click();
        Assert.Equal(2, cut.FindAll("table.res-table tbody tr").Count);

        // …più l'ambito «ACC»: restano le due LoA, che sono entrambe d'ACC.
        var chipAmbito = cut.FindAll("button.sh-chip").ToArray();
        chipAmbito[Enum.GetValues<AttachmentKind>().Length + (int)AttachmentScope.Acc].Click();
        Assert.Equal(2, cut.FindAll("table.res-table tbody tr").Count);
        Assert.DoesNotContain("Circolare 01", cut.Markup);
    }

    /// <summary>Un chip già acceso si spegne al secondo clic: senza, un filtro scelto per sbaglio non si toglie
    /// più se non ricaricando la pagina.</summary>
    [Fact]
    public void Il_chip_si_spegne_al_secondo_clic()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok,
            Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille"),
            Riga(2, "circolare-01", "Circolare 01", AttachmentKind.Circular)));

        cut.FindAll("button.sh-chip").ToArray()[(int)AttachmentKind.Loa].Click();
        Assert.Single(cut.FindAll("table.res-table tbody tr"));

        cut.FindAll("button.sh-chip").ToArray()[(int)AttachmentKind.Loa].Click();
        Assert.Equal(2, cut.FindAll("table.res-table tbody tr").Count);
    }

    /// <summary>La ricerca guarda tutto quel che si legge: «Marseille» trova la LoA tanto quanto «loa-lirr».</summary>
    [Fact]
    public void La_ricerca_guarda_titolo_e_slug()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok,
            Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille"),
            Riga(2, "circolare-01", "Circolare 01", AttachmentKind.Circular)));

        cut.Find("input.htree-search").Input("marseille");
        Assert.Single(cut.FindAll("table.res-table tbody tr"));

        cut.Find("input.htree-search").Input("circolare-01");
        Assert.Single(cut.FindAll("table.res-table tbody tr"));
    }

    /// <summary>Lo slug si propone dal titolo: chi carica non deve inventarsi una forma che poi la pagina rifiuta.</summary>
    [Fact]
    public void Il_titolo_propone_lo_slug()
    {
        var biblioteca = new BibliotecaFinta();
        var cut = Render(biblioteca);

        // Il campo slug non si tocca: resta la proposta.
        cut.FindAll(".mil-add input").ToArray()[0].Input("LoA Roma–Marseille");

        cut.Find(".mil-add input[placeholder='Att_LinkPh']")
           .Change("https://drive.google.com/file/d/1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW/view");
        cut.Find(".mil-add button.primary").Click();

        Assert.Equal("loa-roma-marseille", biblioteca.Ultima!.Slug);
    }

    /// <summary>⚠️ …ma chi lo scrive comanda: dopo che lo slug è stato battuto a mano, il titolo non lo tocca
    /// più. Lo slug è definitivo, e riscriverlo sotto le dita sarebbe il modo di citare una voce diversa da
    /// quella che si credeva.</summary>
    [Fact]
    public void Lo_slug_scritto_a_mano_non_viene_riscritto_dal_titolo()
    {
        var biblioteca = new BibliotecaFinta();
        var cut = Render(biblioteca);

        cut.FindAll(".mil-add input").ToArray()[1].Input("loa-lirr-lfmm");
        cut.FindAll(".mil-add input").ToArray()[0].Input("LoA Roma–Marseille");

        cut.Find(".mil-add input[placeholder='Att_LinkPh']")
           .Change("https://drive.google.com/file/d/1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW/view");
        cut.Find(".mil-add button.primary").Click();

        Assert.Equal("loa-lirr-lfmm", biblioteca.Ultima!.Slug);
        Assert.Equal("LoA Roma–Marseille", biblioteca.Ultima.Title);
    }

    /// <summary>
    /// ⚠️ I rifiuti restano distinti. Uno slug occupato si corregge cambiando slug, un link illeggibile
    /// ricopiando il link: un «non valido» solo li manderebbe a indovinare quale delle due cose sistemare.
    /// </summary>
    [Theory]
    [InlineData(AttachmentCreate.SlugOccupato, "Att_ErrSlugTaken")]
    [InlineData(AttachmentCreate.SlugNonValido, "Att_ErrSlug")]
    [InlineData(AttachmentCreate.LinkNonValido, "Att_ErrLink")]
    [InlineData(AttachmentCreate.TitoloMancante, "Att_ErrTitle")]
    [InlineData(AttachmentCreate.AmbitoNonValido, "Att_ErrScope")]
    public void Ogni_rifiuto_dice_la_sua_cosa(AttachmentCreate esito, string chiave)
    {
        var cut = Render(new BibliotecaFinta(esito));

        cut.Find(".mil-add button.primary").Click();

        Assert.Contains(chiave, cut.Find(".st-msg").TextContent);
        Assert.Contains("warn", cut.Find(".st-msg").ClassName);
    }

    /// <summary>Andata bene, la voce compare nell'elenco e i campi si svuotano: altrimenti il secondo
    /// caricamento parte con dentro i dati del primo.</summary>
    [Fact]
    public void La_voce_creata_compare_e_i_campi_si_svuotano()
    {
        var cut = Render(new BibliotecaFinta());

        cut.FindAll(".mil-add input").ToArray()[0].Input("LoA Roma–Marseille");
        cut.Find(".mil-add input[placeholder='Att_LinkPh']")
           .Change("https://drive.google.com/file/d/1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW/view");
        cut.Find(".mil-add button.primary").Click();

        Assert.Contains("LoA Roma–Marseille", cut.Find("table.res-table").TextContent);
        Assert.Contains("ok", cut.Find(".st-msg").ClassName);
        Assert.Equal("", cut.FindAll(".mil-add input").ToArray()[0].GetAttribute("value"));
        Assert.Equal("", cut.FindAll(".mil-add input").ToArray()[1].GetAttribute("value"));
    }

    /// <summary>
    /// ⚠️ «Citato da» dice <b>quali</b> documenti cambiano, non quanti: è l'unica informazione con cui si
    /// decide se sostituire o cancellare, e chiederla non deve costare un cambio di pagina.
    /// </summary>
    [Fact]
    public void Il_conteggio_apre_lelenco_di_chi_cita()
    {
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")),
            uso: new UsoFinto(("loa-lirr-lfmm", new[]
            {
                new AttachmentCitation(AttachmentCitationSource.Release, "vIPI Fiumicino",
                    "/services/vsop/lirr/airports/editor?icao=LIRF", true, "2609"),
            })));

        Assert.Empty(cut.FindAll("ul.att-cited"));

        cut.Find("table.res-table tbody button").Click();

        var voce = cut.Find("ul.att-cited li");
        Assert.Contains("vIPI Fiumicino", voce.TextContent);
        Assert.Contains("Att_CitedAirac 2609", voce.TextContent);
        Assert.Equal("/services/vsop/lirr/airports/editor?icao=LIRF",
            cut.Find("ul.att-cited li a").GetAttribute("href"));
    }

    /// <summary>Il chip «mai usate» è il modo di tenere pulita la biblioteca: senza, una voce caricata per
    /// sbaglio non si distingue più da una che serve.</summary>
    [Fact]
    public void Il_chip_mai_usate_lascia_solo_quelle_che_non_cita_nessuno()
    {
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok,
                Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille"),
                Riga(2, "circolare-01", "Circolare 01", AttachmentKind.Circular)),
            uso: new UsoFinto(("loa-lirr-lfmm", new[]
            {
                new AttachmentCitation(AttachmentCitationSource.Document, "vIPI Fiumicino"),
            })));

        Assert.Equal(2, cut.FindAll("table.res-table tbody tr").Count);

        cut.FindAll("button.sh-chip").ToArray()[^1].Click();

        // ⚠️ Si materializza prima di indicizzare: con questa coppia bUnit/AngleSharp l'indicizzatore della
        // collezione aggiornabile non esiste a runtime, e il test morirebbe con un MissingMethodException
        // che non dice niente di quel che sta provando.
        var righe = cut.FindAll("table.res-table tbody tr").ToArray();
        Assert.Single(righe);
        Assert.Contains("Circolare 01", righe[0].TextContent);
    }

    /// <summary>⚠️ Il chip resta a schermo anche a zero, disabilitato: uno che compare col contatore sposta
    /// quel che gli sta accanto, e chi stava per cliccare clicca un'altra cosa.</summary>
    [Fact]
    public void Il_chip_mai_usate_resta_a_schermo_anche_a_zero()
    {
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")),
            uso: new UsoFinto(("loa-lirr-lfmm", new[]
            {
                new AttachmentCitation(AttachmentCitationSource.Document, "vIPI Fiumicino"),
            })));

        var chip = cut.FindAll("button.sh-chip").ToArray()[^1];
        Assert.Contains("Att_Unused", chip.TextContent);
        Assert.True(chip.HasAttribute("disabled"));
    }

    // ---- sostituzione ------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>La conferma dice QUALI documenti cambiano, prima che si prema.</b> È l'unica informazione con
    /// cui si decide: il link segue sempre la versione corrente, quindi un documento già <b>pubblicato</b>
    /// mostrerà il file nuovo senza che nessuno l'abbia toccato. Un «sei sicuro?» non direbbe niente.
    /// </summary>
    [Fact]
    public void La_conferma_elenca_i_documenti_che_cambiano()
    {
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")),
            uso: new UsoFinto(("loa-lirr-lfmm", new[]
            {
                new AttachmentCitation(AttachmentCitationSource.Release, "vIPI Fiumicino",
                    "/services/vsop/lirr/airports/editor?icao=LIRF", true, "2609", 7),
            })));

        cut.Find("button[title='Att_Replace']").Click();

        var pannello = cut.Find(".att-replace");
        Assert.Contains("Att_ReplaceImpact 1", pannello.TextContent);
        Assert.Contains("vIPI Fiumicino", pannello.TextContent);
        Assert.Contains("Att_CitedAirac 2609", pannello.TextContent);
    }

    /// <summary>Una voce che non cita nessuno lo dice: «cambia solo la biblioteca» è una risposta, il
    /// silenzio no.</summary>
    [Fact]
    public void Senza_citazioni_la_conferma_lo_dice()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")));

        cut.Find("button[title='Att_Replace']").Click();

        Assert.Contains("Att_ReplaceNoImpact", cut.Find(".att-replace").TextContent);
    }

    [Fact]
    public void La_sostituzione_passa_link_e_nota_al_servizio()
    {
        var cura = new CuraFinta();
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")),
            cura: cura);

        cut.Find("button[title='Att_Replace']").Click();

        // ⚠️ Si ricerca l'elemento DOPO ogni render: il primo `Change` rende di nuovo il componente, e la
        // collezione presa prima porta gestori che nel nuovo albero non esistono più.
        cut.FindAll(".att-replace input").ToArray()[0]
            .Change("https://drive.google.com/file/d/1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW/view");
        cut.FindAll(".att-replace input").ToArray()[1].Change("rifirmata dopo modifica CoP");
        cut.Find(".att-replace-actions button.primary").Click();

        Assert.Equal("loa-lirr-lfmm", cura.Slug);
        Assert.Contains("1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW", cura.Link);
        Assert.Equal("rifirmata dopo modifica CoP", cura.Nota);
    }

    /// <summary>L'esito dice la versione nuova e <b>quanti documenti sono stati segnalati</b>: è la metà utile
    /// del messaggio, perché dice che di quel cambiamento è rimasta una traccia su cui qualcuno tornerà.</summary>
    [Fact]
    public void Lesito_dice_la_versione_e_quanti_documenti_sono_segnalati()
    {
        var impattato = new AttachmentCitation(AttachmentCitationSource.Release, "vIPI Fiumicino",
            null, true, "2609", 7);
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")),
            cura: new CuraFinta(AttachmentReplace.Ok, AttachmentDelete.Ok, impattato));

        cut.Find("button[title='Att_Replace']").Click();
        cut.Find(".att-replace-actions button.primary").Click();

        var msg = cut.Find(".st-msg");
        Assert.Contains("Att_Replaced", msg.TextContent);
        Assert.Contains("3", msg.TextContent);   // la versione nuova
        Assert.Contains("ok", msg.ClassName);
    }

    /// <summary>⚠️ Il non-evento si distingue: rimettere lo stesso file non è un errore, ma non è nemmeno una
    /// sostituzione — e dirlo evita che qualcuno aspetti una segnalazione che non arriverà.</summary>
    [Theory]
    [InlineData(AttachmentReplace.Invariato, "Att_ReplaceSame")]
    [InlineData(AttachmentReplace.LinkNonValido, "Att_ErrLink")]
    [InlineData(AttachmentReplace.NonTrovata, "Att_ReplaceGone")]
    public void Ogni_rifiuto_della_sostituzione_dice_la_sua_cosa(AttachmentReplace esito, string chiave)
    {
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")),
            cura: new CuraFinta(esito));

        cut.Find("button[title='Att_Replace']").Click();
        cut.Find(".att-replace-actions button.primary").Click();

        Assert.Contains(chiave, cut.Find(".st-msg").TextContent);
        Assert.Contains("warn", cut.Find(".st-msg").ClassName);
        // Il pannello resta aperto: c'è qualcosa da correggere, e chiuderlo farebbe ricominciare da capo.
        Assert.Single(cut.FindAll(".att-replace"));
    }

    /// <summary>⚠️ I campi si svuotano a ogni apertura: un link rimasto da una sostituzione annullata verrebbe
    /// confermato su un'altra voce senza che nessuno lo rilegga.</summary>
    [Fact]
    public void Annullare_svuota_i_campi()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")));

        cut.Find("button[title='Att_Replace']").Click();
        cut.FindAll(".att-replace input").ToArray()[0].Change("https://drive.google.com/file/d/AAAAAAAAAAAA/view");
        cut.Find(".att-replace-actions button.ghost").Click();

        Assert.Empty(cut.FindAll(".att-replace"));

        cut.Find("button[title='Att_Replace']").Click();
        Assert.Equal("", cut.FindAll(".att-replace input").ToArray()[0].GetAttribute("value"));
    }

    // ---- eliminazione ------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ La guardia dice <b>che cosa resta rotto</b> prima di chiedere conferma. Un «sei sicuro?» senza
    /// questo elenco è una domanda a cui non si può rispondere — e qui la risposta cambia: un conto è togliere
    /// una voce che non cita nessuno, un altro lasciare tre documenti con un link morto.
    /// </summary>
    [Fact]
    public void La_guardia_elenca_chi_resta_col_link_morto()
    {
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")),
            uso: new UsoFinto(("loa-lirr-lfmm", new[]
            {
                new AttachmentCitation(AttachmentCitationSource.Release, "vIPI Fiumicino", null, true, "2609", 7),
            })));

        cut.Find("button[title='Att_Delete']").Click();

        var pannello = cut.Find(".att-replace");
        Assert.Contains("Att_DeleteImpact 1", pannello.TextContent);
        Assert.Contains("vIPI Fiumicino", pannello.TextContent);
        // E si dice che il file sul deposito resta: è la domanda che si fa chiunque prema quel tasto.
        Assert.Contains("Att_DeleteKeepsFile", pannello.TextContent);
    }

    [Fact]
    public void Senza_citazioni_la_guardia_lo_dice()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")));

        cut.Find("button[title='Att_Delete']").Click();

        Assert.Contains("Att_DeleteNoImpact", cut.Find(".att-replace").TextContent);
    }

    /// <summary>Niente si elimina al primo clic: il tasto apre la guardia, la conferma è un secondo gesto.</summary>
    [Fact]
    public void Il_primo_clic_apre_la_guardia_e_non_elimina()
    {
        var cura = new CuraFinta();
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")), cura: cura);

        cut.Find("button[title='Att_Delete']").Click();

        Assert.Null(cura.Eliminato);
        Assert.Single(cut.FindAll(".att-replace"));
    }

    [Fact]
    public void La_conferma_elimina_e_dice_quanti_restano_da_correggere()
    {
        var orfano = new AttachmentCitation(AttachmentCitationSource.Release, "vIPI Fiumicino", null, true, "2609", 7);
        var cura = new CuraFinta(AttachmentReplace.Ok, AttachmentDelete.Ok, orfano);
        var cut = Render(
            new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA Roma–Marseille")),
            cura: cura);

        cut.Find("button[title='Att_Delete']").Click();
        cut.Find(".att-replace-actions button.danger").Click();

        Assert.Equal("loa-lirr-lfmm", cura.Eliminato);
        var msg = cut.Find(".st-msg");
        Assert.Contains("Att_Deleted", msg.TextContent);
        Assert.Contains("ok", msg.ClassName);
    }

    /// <summary>Le due conferme non stanno aperte insieme sulla stessa riga: sono due domande diverse, e viste
    /// insieme sembrano una sola.</summary>
    [Fact]
    public void Aprire_una_conferma_chiude_laltra()
    {
        var cut = Render(new BibliotecaFinta(AttachmentCreate.Ok, Riga(1, "loa-lirr-lfmm", "LoA")));

        cut.Find("button[title='Att_Replace']").Click();
        Assert.Single(cut.FindAll(".att-replace-fields"));

        cut.Find("button[title='Att_Delete']").Click();
        Assert.Empty(cut.FindAll(".att-replace-fields"));
        Assert.Single(cut.FindAll(".att-replace-actions button.danger"));
    }

    /// <summary>La chiave d'ambito compare solo quando serve: la divisione non ne ha una, e un campo che
    /// c'è ma non conta si compila lo stesso.</summary>
    [Fact]
    public void Il_codice_dambito_compare_solo_per_acc_e_scalo()
    {
        var cut = Render(new BibliotecaFinta());

        Assert.Empty(cut.FindAll(".mil-add input[placeholder='LIRR']"));

        cut.FindAll(".mil-add select").ToArray()[1].Change(nameof(AttachmentScope.Acc));
        Assert.Single(cut.FindAll(".mil-add input[placeholder='LIRR']"));
    }
}
