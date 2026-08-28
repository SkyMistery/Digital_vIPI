using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione del catalogo sezioni unificato (doc refactor 08a): natura, membership per profilo, Reconcile, ricorsione.</summary>
public class SectionCatalogTests
{
    private static readonly string[] Universal =
        { "aor", "frequencies", "coordination", "regulated", "operationaltechnique", "validity" };

    /// <summary>I profili che descrivono una POSIZIONE DI CONTROLLO. È su questi che vale l'invariante delle
    /// sezioni universali: <see cref="SectionProfile.Airport"/> descrive un luogo e non ha AoR né coordinamenti
    /// (carta 2026-08-26 §1a).</summary>
    private static readonly SectionProfile[] ControlPositions =
        { SectionProfile.App, SectionProfile.AccAerovia, SectionProfile.AccAppBlock, SectionProfile.Vloa };

    [Theory]
    [InlineData("aor", SectionKind.Derived)]
    [InlineData("frequencies", SectionKind.Derived)]
    [InlineData("coordination", SectionKind.Derived)]
    [InlineData("minima", SectionKind.Derived)]   // la carta MRVA viene dal sectorfile (una per file .mva)
    [InlineData("separations", SectionKind.Editorial)]
    [InlineData("regulated", SectionKind.Editorial)]
    [InlineData("qualcosa-custom", SectionKind.Editorial)]   // sconosciuta = custom editoriale
    public void KindOf_is_single_source(string key, SectionKind expected) =>
        Assert.Equal(expected, SectionCatalog.KindOf(key));

    // Il toggle Live/Frozen (doc 10 §3a) vale solo per le sezioni derivate: la regola stava ripetuta nei tre
    // editor, ora è qui. Ogni chiave del catalogo deve rispondere in modo coerente con la propria natura.
    [Theory]
    [InlineData("aor", true)]
    [InlineData("frequencies", true)]
    [InlineData("coordination", true)]
    [InlineData("minima", true)]   // derivata dal sectorfile: si può congelare alla release
    [InlineData("sids", true)]
    [InlineData("runways", true)]        // derivata dall'anagrafica: si può congelare alla release
    [InlineData("transition", true)]
    [InlineData("weather", false)]       // derivata ma SEMPRE live: un METAR congelato è meteo scaduto
    [InlineData("validity", false)]      // idem: il timbro parla della release che si sta mostrando
    [InlineData("separations", false)]
    [InlineData("vfr", false)]
    [InlineData("una-sezione-custom", false)]   // chiave ignota = editoriale = niente toggle
    public void IsRenderModeToggleable_only_for_derived(string key, bool expected) =>
        Assert.Equal(expected, SectionCatalog.IsRenderModeToggleable(key));

    [Fact]
    public void IsRenderModeToggleable_agrees_with_KindOf_on_every_catalog_key()
    {
        // Invariante di coerenza: le due porte non possono divergere.
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
            foreach (var d in SectionCatalog.For(p))
                Assert.Equal(
                    SectionCatalog.KindOf(d.Key) == SectionKind.Derived && !SectionCatalog.IsAlwaysLive(d.Key),
                    SectionCatalog.IsRenderModeToggleable(d.Key));
    }

    [Fact]
    public void An_always_live_section_is_derived_and_never_toggleable()
    {
        // Una sezione «sempre live» che non fosse derivata sarebbe una contraddizione: non c'è niente da derivare.
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
            foreach (var d in SectionCatalog.For(p).Where(d => SectionCatalog.IsAlwaysLive(d.Key)))
            {
                Assert.Equal(SectionKind.Derived, d.Kind);
                Assert.False(SectionCatalog.IsRenderModeToggleable(d.Key));
            }
        Assert.True(SectionCatalog.IsAlwaysLive("weather"));
        Assert.True(SectionCatalog.IsAlwaysLive("validity"));
        Assert.False(SectionCatalog.IsAlwaysLive("sids"));
    }

    // ---- doc 13 §3a: chi rende il corpo lo dice il catalogo, per profilo ----

    // Rete di regressione contro il ritorno degli HashSet di pagina: se una pagina ricomincia a decidere da sé,
    // questa lista e la sua smettono di combaciare e il difetto si vede qui, non in produzione.
    [Fact]
    public void Host_rendered_sections_are_declared_per_profile()
    {
        // ⚠️ Si chiede a IsHostRendered, non a `BodySource == Host`: dal 26 agosto 2026 esiste anche
        // HostAndBlocks — la pagina disegna una scheda E la sezione tiene i suoi blocchi — e confrontare
        // l'enum a mano avrebbe lasciato «validity» fuori da questa rete senza che nessuno se ne accorgesse.
        string[] Host(SectionProfile p) => SectionCatalog.For(p)
            .Where(d => SectionCatalog.IsHostRendered(p, d.Key)).Select(d => d.Key).OrderBy(k => k).ToArray();

        Assert.Equal(
            new[] { "aor", "configurations", "coordination", "frequencies", "minima", "regulated", "separations", "validity", "vfr" },
            Host(SectionProfile.App));
        Assert.Equal(
            new[] { "aor", "configurations", "coordination", "frequencies", "minima", "regulated", "separations", "validity" },
            Host(SectionProfile.AccAerovia));   // l'Aerovia non ha il VFR
        Assert.Equal(
            new[] { "aor", "configurations", "coordination", "frequencies", "minima", "regulated", "separations", "validity", "vfr" },
            Host(SectionProfile.AccAppBlock));
        Assert.Equal(
            new[] { "aor", "coordination", "frequencies", "validity" },
            Host(SectionProfile.Vloa));   // sulla vLOA «regulated» è testo bilaterale, non un picker
        Assert.Equal(
            new[] { "frequencies", "runwayrules", "runways", "sids", "transition", "validity", "weather" },
            Host(SectionProfile.Airport));
    }

    [Fact]
    public void Validity_has_a_card_from_the_page_AND_its_own_blocks()
    {
        // Richiesta del committente (26 agosto 2026): tre campi FISSI — ciclo AIRAC, data e chi ha pubblicato —
        // che nessuno ricopia a mano. Ma sotto resta il testo scritto: su una vLOA lì ci sono il ciclo di
        // revisione concordato e il firmatario, che nessuno può derivare. È l'unica sezione con tutti e due.
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
        {
            Assert.True(SectionCatalog.IsHostRendered(p, "validity"), $"{p}: la scheda la disegna la pagina");
            Assert.True(SectionCatalog.KeepsOwnBlocks(p, "validity"), $"{p}: e i blocchi restano");
        }

        // Le sezioni che hanno ENTRAMBI sono un elenco chiuso: chi ne aggiungesse una lo sta decidendo, non
        // ereditando. Questo cancello ha gia' fatto il suo mestiere il 28 agosto 2026, fermando l'arrivo
        // del profilo militare finche' la decisione non e' stata scritta qui sotto.
        //
        // ⚠️ `AirportMil/regulated` e' DELIBERATO (carta vSOP militari §2): su un SOP le «aree di lavoro»
        // sono una mappa PIU' la prosa che le governa -- procedure generali, bassa quota -- e la mappa AoR
        // con le chip per area e' gia' quello che il PDF disegna a mano, una figura per volta. Toglierle i
        // blocchi vorrebbe dire perdere il testo; toglierle la scheda, ridisegnare a mano una mappa che
        // abbiamo gia'.
        var conEntrambi = new HashSet<(SectionProfile, string)>
        {
            (SectionProfile.AirportMil, "regulated"),
        };

        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
            foreach (var d in SectionCatalog.For(p).Where(d => d.Key != "validity"))
            {
                if (conEntrambi.Contains((p, d.Key))) continue;
                Assert.False(SectionCatalog.KeepsOwnBlocks(p, d.Key), $"{p}/{d.Key}");
            }
    }

    [Fact]
    public void The_same_key_can_be_host_rendered_in_one_profile_and_not_in_another()
    {
        // È il motivo per cui BodySource è per profilo e non globale come KindOf.
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.App, "regulated"));
        Assert.False(SectionCatalog.IsHostRendered(SectionProfile.Vloa, "regulated"));
    }

    [Fact]
    public void A_derived_section_is_always_host_rendered()
    {
        // Invariante: non esiste una sezione calcolata live il cui corpo venga dai blocchi salvati.
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
            foreach (var d in SectionCatalog.For(p).Where(d => d.Kind == SectionKind.Derived))
                Assert.True(SectionCatalog.IsHostRendered(p, d.Key), $"{p}/{d.Key}");
    }

    [Fact]
    public void Custom_sections_are_never_host_rendered_nor_fixed()
    {
        foreach (SectionProfile p in Enum.GetValues<SectionProfile>())
        {
            Assert.False(SectionCatalog.IsHostRendered(p, "custom:9f3a1c07"));
            Assert.False(SectionCatalog.IsFixed(p, "custom:9f3a1c07"));
        }
    }

    [Fact]
    public void ProfileOfAccBlock_maps_the_two_block_kinds()
    {
        Assert.Equal(SectionProfile.AccAerovia, SectionCatalog.ProfileOfAccBlock(AccBlockKind.Aerovia));
        Assert.Equal(SectionProfile.AccAppBlock, SectionCatalog.ProfileOfAccBlock(AccBlockKind.AppGroup));
    }

    [Fact]
    public void Universals_present_in_every_control_position_profile()
    {
        foreach (var p in ControlPositions)
        {
            var keys = SectionCatalog.For(p).Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var u in Universal)
                Assert.True(keys.Contains(u), $"{p} deve contenere «{u}»");
        }
    }

    [Fact]
    public void The_airport_has_no_aor_no_coordination_no_regulated()
    {
        // Carta 2026-08-26 §1a: l'aeroporto descrive un LUOGO. Area di responsabilità, accordi di coordinamento e
        // aree regolamentate appartengono alla torre e all'avvicinamento, che hanno documenti loro — scriverle
        // anche qui vorrebbe dire due verità sulla stessa cosa. Restano le due editoriali universali.
        var keys = SectionCatalog.For(SectionProfile.Airport).Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("aor", keys);
        Assert.DoesNotContain("coordination", keys);
        Assert.DoesNotContain("regulated", keys);
        Assert.Contains("operationaltechnique", keys);
        Assert.Contains("validity", keys);
    }

    [Fact]
    public void Airport_default_order_is_the_order_of_todays_page()
    {
        // Le stesse sezioni che l'aeroporto aveva già, nell'ordine in cui la pagina le mostrava quando quella
        // sequenza era cablata nel viewer. Da qui in poi è solo l'ordine di NASCITA: si riordina in editor.
        var keys = SectionCatalog.For(SectionProfile.Airport).OrderBy(d => d.Order).Select(d => d.Key).ToArray();
        Assert.Equal(
            new[] { "weather", "runwayrules", "transition", "frequencies", "runways", "sids", "operationaltechnique", "validity" },
            keys);
    }

    [Fact]
    public void Vloa_is_the_universals_plus_purpose()
    {
        // doc 13 §3c: il profilo descrive la vLOA VERA, che ha anche «Purpose» — prima il catalogo non lo sapeva
        // perché la struttura nasceva da VloaSections e questo profilo non lo leggeva nessuno.
        var keys = SectionCatalog.For(SectionProfile.Vloa).Select(d => d.Key).ToArray();
        Assert.Equal(Universal.Append("purpose").OrderBy(x => x), keys.OrderBy(x => x));
    }

    [Fact]
    public void Vloa_default_order_is_the_order_of_the_real_document()
    {
        var keys = SectionCatalog.For(SectionProfile.Vloa).OrderBy(d => d.Order).Select(d => d.Key).ToArray();
        Assert.Equal(
            new[] { "purpose", "aor", "frequencies", "operationaltechnique", "coordination", "regulated", "validity" },
            keys);
    }

    [Fact]
    public void Profile_specific_membership()
    {
        string[] Keys(SectionProfile p) => SectionCatalog.For(p).Select(d => d.Key).ToArray();

        Assert.Contains("configurations", Keys(SectionProfile.App));   // config aggiunta ad APP
        Assert.Contains("minima", Keys(SectionProfile.AccAppBlock));    // minima aggiunta ad AppBlock
        Assert.Contains("vfr", Keys(SectionProfile.App));
        Assert.DoesNotContain("vfr", Keys(SectionProfile.AccAerovia));  // Aerovia senza VFR
        Assert.DoesNotContain("separations", Keys(SectionProfile.Vloa)); // vLOA senza separazioni
    }

    [Fact]
    public void DocSection_is_recursive_with_empty_defaults()
    {
        var leaf = new DocSection("Foglia", SectionKind.Editorial);
        Assert.Empty(leaf.Blocks);
        Assert.Empty(leaf.SubSections);

        var parent = new DocSection("Padre", SectionKind.Editorial,
            Blocks: new[] { new DocBlock(Vipi.Domain.BlockFormat.Prose, Body: "testo") },
            SubSections: new[] { leaf });

        Assert.Single(parent.Blocks);
        Assert.Equal("Foglia", Assert.Single(parent.SubSections).Title);
    }

    [Fact]
    public void La_sezione_delle_minime_si_chiama_MRVA_in_ogni_profilo()
    {
        // ⚠️ È una SIGLA, e resta uguale in italiano e in inglese — come «SID» o «AOR»
        // (docs/design/regole-lingua.md). Diceva «Minime di vettoramento», che il motore rendeva
        // «Minimum vectoring»: giusto a metà, e comunque non la sigla con cui la si chiama in frequenza.
        foreach (var profilo in new[] { SectionProfile.AccAerovia, SectionProfile.AccAppBlock, SectionProfile.App })
            Assert.Equal("MRVA", SectionCatalog.Find(profilo, "minima")?.Title);
    }

    [Fact]
    public void Regulated_Opens_Collapsed_In_The_Document()
    {
        // doc 11 §3i: «Aree regolamentate» su una ACC sono decine di aree con mappa — la sezione nasce chiusa.
        Assert.True(SectionCatalog.IsInitiallyCollapsed("regulated"));
        Assert.False(SectionCatalog.IsInitiallyCollapsed("aor"));
        Assert.False(SectionCatalog.IsInitiallyCollapsed("coordination"));
        Assert.False(SectionCatalog.IsInitiallyCollapsed("custom:aaaa1111"));
    }
}
