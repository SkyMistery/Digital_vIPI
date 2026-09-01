using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// I titoli delle sezioni di catalogo seguono la lingua in cui si LEGGE il documento, anche quando la
/// traduzione non gira (lingua bloccata).
///
/// <para>
/// ⚠️ <b>Perché serve una guardia e non basta la copertura di traduzione.</b> Fino al 1 settembre 2026 i
/// titoli arrivavano in inglese <b>di rimbalzo</b>, perché sono segmenti del documento e quindi passavano
/// dalla memoria di traduzione. Bloccare la lingua spegne la traduzione — sorgente e bersaglio coincidono —
/// e con lei è caduto il rimbalzo: titoli italiani dentro un documento dichiarato inglese, con la copertura
/// che diceva «niente da tradurre», cioè esattamente quel che ci si aspetta da un documento bloccato.
/// Nessun test poteva vederlo: guardavano tutti la traduzione.
/// </para>
/// </summary>
public class TitoliDiCatalogoTests
{
    private static SectionView Sez(string key, string titolo, params SectionView[] figlie) => new()
    {
        Id = $"s-{key}", Title = titolo, Depth = 0, SectionKey = key,
        Blocks = Array.Empty<BlockView>(), Children = figlie,
    };

    [Fact]
    public void Un_documento_letto_in_inglese_ha_i_titoli_di_catalogo_in_inglese()
    {
        var sezioni = new[] { Sez("separations", "Separazioni"), Sez("regulated", "Aree regolamentate") };

        var esito = TitoliDiCatalogo.Applica(sezioni, SectionProfile.App, "en");

        Assert.Equal("Separations", esito[0].Title);
        Assert.Equal("Regulated areas", esito[1].Title);
    }

    /// <summary>
    /// ⚠️ Il vSOP militare ha VENTI sezioni di catalogo su ventisei dentro quattro contenitori: una
    /// risoluzione ferma al primo livello lascerebbe italiane proprio quelle — ed è la famiglia in cui il
    /// difetto si vede di più, perché il documento è quasi tutto annidato.
    /// </summary>
    [Fact]
    public void Scende_nelle_sotto_sezioni()
    {
        var sezioni = new[]
        {
            Sez("generaldata", "Dati generali",
                Sez("navaids", "Radioassistenze"),
                Sez("runways", "Piste")),
        };

        var esito = TitoliDiCatalogo.Applica(sezioni, SectionProfile.AirportMil, "en");

        Assert.Equal("General data", esito[0].Title);
        Assert.Equal("Navigation aids", esito[0].Children[0].Title);
        Assert.Equal("Runways", esito[0].Children[1].Title);
    }

    /// <summary>Il titolo di una sezione LIBERA è prosa dell'autore: lo traduce il traduttore, non il
    /// catalogo — che di quella chiave non sa niente.</summary>
    [Fact]
    public void Le_sezioni_libere_non_si_toccano()
    {
        var libera = Sez(SectionKeys.NewCustom(), "Procedure LVP di Pratica");

        var esito = TitoliDiCatalogo.Applica(new[] { libera }, SectionProfile.AirportMil, "en");

        Assert.Equal("Procedure LVP di Pratica", esito[0].Title);
    }

    /// <summary>
    /// ⚠️ Le sigle restano uguali nelle due lingue — è la decisione del committente
    /// (<c>docs/design/regole-lingua.md</c>), e la ragione per cui «Minime di vettoramento» è stata
    /// rinominata «MRVA»: il motore la rendeva «Minimum vectoring», giusto a metà.
    /// </summary>
    [Fact]
    public void Le_sigle_restano_uguali()
    {
        var esito = TitoliDiCatalogo.Applica(new[] { Sez("minima", "MRVA"), Sez("aor", "AOR") },
            SectionProfile.App, "en");

        Assert.Equal("MRVA", esito[0].Title);
        Assert.Equal("AOR", esito[1].Title);
    }

    /// <summary>
    /// ⚠️ Le due sezioni di coordinamento della vLOA stanno nel catalogo con titolo VUOTO: il loro dipende
    /// dai codici della coppia e lo compone la pagina. Sovrascrivere vorrebbe dire cancellarlo — una testata
    /// vuota in mezzo al documento, che è peggio di una nella lingua sbagliata.
    /// </summary>
    [Fact]
    public void Un_titolo_di_catalogo_vuoto_non_cancella_quello_che_ce()
    {
        // In INGLESE, che per una vLOA è la lingua del catalogo: è lì che una resa esiste davvero, quindi è
        // lì che la guardia sul vuoto deve reggere.
        var esito = TitoliDiCatalogo.Applica(
            new[] { Sez(SectionKeys.CoordinationOut, "LIMM → LSAZ") }, SectionProfile.Vloa, "en");

        Assert.Equal("LIMM → LSAZ", esito[0].Title);
    }

    /// <summary>
    /// ⚠️ La vLOA è l'unico profilo con i titoli di catalogo in INGLESE — è una lettera d'accordo
    /// bilaterale e nasce così — quindi il catalogo non ha nessuna resa italiana da imporre. Letta in
    /// italiano non si tocca niente: l'unico che quei titoli può tradurli è il traduttore, e imporgli
    /// «Purpose» cancellerebbe la sua resa invece di correggerla.
    /// </summary>
    [Fact]
    public void Una_vloa_letta_in_italiano_lascia_fare_al_traduttore()
    {
        var tradotte = new[] { Sez("purpose", "Scopo"), Sez("frequencies", "Frequenze") };

        var esito = TitoliDiCatalogo.Applica(tradotte, SectionProfile.Vloa, "it");

        Assert.Equal("Scopo", esito[0].Title);
        Assert.Equal("Frequenze", esito[1].Title);
    }

    /// <summary>E letta in inglese — il caso della vLOA BLOCCATA — i titoli sono quelli del catalogo, che
    /// per questo profilo sono già gli inglesi.</summary>
    [Fact]
    public void Una_vloa_letta_in_inglese_torna_ai_titoli_di_catalogo()
    {
        var esito = TitoliDiCatalogo.Applica(
            new[] { Sez("purpose", "Scopo"), Sez("validity", "Validità e revisione") },
            SectionProfile.Vloa, "en");

        Assert.Equal("Purpose", esito[0].Title);
        Assert.Equal("Validity and Revision", esito[1].Title);
    }

    /// <summary>Il caso normale — si legge nella lingua in cui è scritto — non deve costare niente: stessa
    /// lista, stessi oggetti.</summary>
    [Fact]
    public void Se_non_cambia_niente_torna_la_lista_di_partenza()
    {
        var sezioni = new[] { Sez("separations", "Separazioni"), Sez("aor", "AOR") };

        Assert.Same(sezioni, TitoliDiCatalogo.Applica(sezioni, SectionProfile.App, "it"));
    }

    /// <summary>
    /// ⚠️ Ogni campo si ricopia: <c>SectionView</c> è una classe con <c>init</c>, e quello che non si
    /// ricopia torna al default in silenzio — una sezione nascosta tornerebbe visibile, una marcata «solo
    /// ATC» tornerebbe «per tutti», e la pagina si renderebbe lo stesso.
    /// </summary>
    [Fact]
    public void Riscrivere_il_titolo_non_azzera_gli_altri_campi()
    {
        var sezione = new SectionView
        {
            Id = "s-9", Title = "Separazioni", Depth = 2, SectionKey = "separations",
            IsHidden = true, BeforeParentBody = true, LeadSentence = true,
            Audience = SectionAudience.Controllers,
            Blocks = new[] { new BlockView { Id = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = "x" } },
            Children = Array.Empty<SectionView>(),
        };

        var esito = TitoliDiCatalogo.Applica(new[] { sezione }, SectionProfile.App, "en")[0];

        Assert.Equal("Separations", esito.Title);
        Assert.Equal("s-9", esito.Id);
        Assert.Equal(2, esito.Depth);
        Assert.True(esito.IsHidden);
        Assert.True(esito.BeforeParentBody);
        Assert.True(esito.LeadSentence);
        Assert.Equal(SectionAudience.Controllers, esito.Audience);
        Assert.Single(esito.Blocks);
    }

    /// <summary>
    /// ⚠️ Il profilo lo dice il BLOCCO, non la pagina: la stessa chiave <c>separations</c> è «Separazioni
    /// radar» sull'Aerovia e «Separazioni» su un gruppo APP. Un profilo solo per tutta la vIPI ACC darebbe
    /// la resa dell'altro blocco, e nessuno se ne accorgerebbe leggendo.
    /// </summary>
    [Fact]
    public void La_vipi_acc_risolve_per_blocco()
    {
        var data = new AccVipiData
        {
            AccCode = "LIMM", AccName = "Milano",
            Blocks = new List<AccBlock>
            {
                new()
                {
                    Key = "aerovia", Kind = AccBlockKind.Aerovia, Title = "Aerovia",
                    Sections = new List<AccBlockSection> { new(1, "separations", "Separazioni radar", false, null) },
                },
                new()
                {
                    Key = "grp:1", Kind = AccBlockKind.AppGroup, Title = "Milano APP",
                    Sections = new List<AccBlockSection> { new(2, "separations", "Separazioni", false, null) },
                },
            },
        };

        TitoliDiCatalogo.Applica(data, "en");

        Assert.Equal("Radar separation", data.Blocks[0].Sections[0].Title);
        Assert.Equal("Separations", data.Blocks[1].Sections[0].Title);
    }
}
