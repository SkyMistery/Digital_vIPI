using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Il filtro di lettura pilota / ATC (carta <c>2026-08-27-vsop-militari.md</c> §3).
///
/// <para>
/// ⚠️ <b>Non è controllo d'accesso.</b> Il documento è pubblico e la vista ATC la apre chiunque cambi
/// l'indirizzo: qui si decide che cosa <i>conviene</i> mostrare, non che cosa si <i>può</i> vedere.
/// </para>
/// </summary>
public class AudienceFilterTests
{
    private static SectionView Sez(string id, SectionAudience a, params SectionView[] figli) => new()
    {
        Id = id,
        Title = id,
        Depth = 0,
        SectionKey = "custom:" + id,
        Audience = a,
        Blocks = Array.Empty<BlockView>(),
        Children = figli,
    };

    private static string[] Titoli(IReadOnlyList<SectionView> s) => s.Select(x => x.Id).ToArray();

    // ---- La lettura del parametro --------------------------------------------------------------------

    [Theory]
    [InlineData("pilota", SectionAudience.Pilots)]
    [InlineData("PILOTA", SectionAudience.Pilots)]
    [InlineData(" atc ", SectionAudience.Controllers)]
    public void Il_parametro_si_legge_senza_pignoleria(string vista, SectionAudience atteso) =>
        Assert.Equal(atteso, AudienceFilter.Leggi(vista));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qualunque-cosa")]
    public void Un_parametro_storpiato_mostra_il_documento_INTERO(string? vista)
    {
        // ⚠️ Non una vista a caso, e non una pagina d'errore: un indirizzo sbagliato deve dare il documento
        // completo. E' il valore che non nasconde niente, quindi e' l'unico sicuro da scegliere al buio.
        Assert.Null(AudienceFilter.Leggi(vista));
    }

    [Fact]
    public void Il_giro_completo_del_parametro_torna_su_se_stesso()
    {
        foreach (var v in new SectionAudience?[] { null, SectionAudience.Pilots, SectionAudience.Controllers })
            Assert.Equal(v, AudienceFilter.Leggi(AudienceFilter.Query(v)));
    }

    // ---- Il filtro -----------------------------------------------------------------------------------

    [Fact]
    public void In_vista_TUTTO_non_si_toglie_niente()
    {
        var sezioni = new[]
        {
            Sez("a", SectionAudience.Both),
            Sez("b", SectionAudience.Pilots),
            Sez("c", SectionAudience.Controllers),
        };
        // Stessa istanza, non una copia: «tutto» non deve costare nemmeno una allocazione.
        Assert.Same(sezioni, AudienceFilter.Filtra(sezioni, null));
    }

    [Fact]
    public void Il_pilota_non_vede_le_sezioni_marcate_ATC()
    {
        var sezioni = new[]
        {
            Sez("comune", SectionAudience.Both),
            Sez("solo-pilota", SectionAudience.Pilots),
            Sez("solo-atc", SectionAudience.Controllers),
        };
        Assert.Equal(new[] { "comune", "solo-pilota" },
                     Titoli(AudienceFilter.Filtra(sezioni, SectionAudience.Pilots)));
    }

    [Fact]
    public void Il_controllore_non_vede_le_sezioni_marcate_pilota()
    {
        var sezioni = new[]
        {
            Sez("comune", SectionAudience.Both),
            Sez("solo-pilota", SectionAudience.Pilots),
            Sez("solo-atc", SectionAudience.Controllers),
        };
        Assert.Equal(new[] { "comune", "solo-atc" },
                     Titoli(AudienceFilter.Filtra(sezioni, SectionAudience.Controllers)));
    }

    [Fact]
    public void Le_sezioni_PER_TUTTI_non_si_filtrano_MAI()
    {
        // È la scelta che rende utile la funzione invece che dannosa: nascondere a un pilota il contesto
        // ATC lo farebbe leggere PEGGIO, non meglio. Il filtro toglie solo cio' che qualcuno ha marcato
        // esplicitamente per l'altro.
        var sezioni = new[] { Sez("a", SectionAudience.Both), Sez("b", SectionAudience.Both) };
        Assert.Equal(2, AudienceFilter.Filtra(sezioni, SectionAudience.Pilots).Count);
        Assert.Equal(2, AudienceFilter.Filtra(sezioni, SectionAudience.Controllers).Count);
    }

    // ---- La regola sui figli --------------------------------------------------------------------------

    [Fact]
    public void Una_sezione_filtrata_via_porta_con_se_i_FIGLI()
    {
        // ⚠️ Anche i figli «per tutti». Una sotto-sezione comune dentro un capitolo ATC non deve restare
        // ORFANA in mezzo alla pagina, senza piu' il titolo che le dava senso.
        var sezioni = new[]
        {
            Sez("capitolo-atc", SectionAudience.Controllers,
                Sez("figlia-comune", SectionAudience.Both),
                Sez("figlia-atc", SectionAudience.Controllers)),
        };
        Assert.Empty(AudienceFilter.Filtra(sezioni, SectionAudience.Pilots));
    }

    [Fact]
    public void Dentro_un_capitolo_che_resta_i_figli_si_filtrano_uno_a_uno()
    {
        var sezioni = new[]
        {
            Sez("capitolo", SectionAudience.Both,
                Sez("f-comune", SectionAudience.Both),
                Sez("f-pilota", SectionAudience.Pilots),
                Sez("f-atc", SectionAudience.Controllers)),
        };
        var filtrate = AudienceFilter.Filtra(sezioni, SectionAudience.Pilots);
        Assert.Single(filtrate);
        Assert.Equal(new[] { "f-comune", "f-pilota" }, Titoli(filtrate[0].Children));
    }

    [Fact]
    public void Il_filtro_scende_a_qualunque_profondita()
    {
        var sezioni = new[]
        {
            Sez("l0", SectionAudience.Both,
                Sez("l1", SectionAudience.Both,
                    Sez("l2-atc", SectionAudience.Controllers),
                    Sez("l2-comune", SectionAudience.Both))),
        };
        var filtrate = AudienceFilter.Filtra(sezioni, SectionAudience.Pilots);
        Assert.Equal(new[] { "l2-comune" }, Titoli(filtrate[0].Children[0].Children));
    }

    [Fact]
    public void Filtrare_non_tocca_l_originale()
    {
        var sezioni = new[]
        {
            Sez("capitolo", SectionAudience.Both,
                Sez("f-atc", SectionAudience.Controllers)),
        };
        AudienceFilter.Filtra(sezioni, SectionAudience.Pilots);
        Assert.Single(sezioni[0].Children);   // l'albero di partenza e' intatto
    }

    // ---- Quando la chip ha senso ----------------------------------------------------------------------

    [Fact]
    public void Senza_nessuna_sezione_marcata_la_chip_non_serve()
    {
        // Senza questa domanda, su OGNI documento italiano comparirebbe un selettore che non filtra niente:
        // rumore su tutte le pagine per una funzione che ne riguarda poche.
        var sezioni = new[] { Sez("a", SectionAudience.Both), Sez("b", SectionAudience.Both) };
        Assert.False(AudienceFilter.HaSezioniMarcate(sezioni));
    }

    [Fact]
    public void Basta_UNA_sezione_marcata_perche_la_chip_compaia()
    {
        Assert.True(AudienceFilter.HaSezioniMarcate(new[]
        {
            Sez("a", SectionAudience.Both),
            Sez("b", SectionAudience.Pilots),
        }));
    }

    [Fact]
    public void Una_marcatura_annidata_conta_come_le_altre()
    {
        // Se contasse solo il primo livello, un documento marcato solo nelle sotto-sezioni non avrebbe la
        // chip: il filtro esisterebbe e nessuno potrebbe usarlo.
        Assert.True(AudienceFilter.HaSezioniMarcate(new[]
        {
            Sez("a", SectionAudience.Both,
                Sez("a1", SectionAudience.Both,
                    Sez("a2", SectionAudience.Controllers))),
        }));
    }

    [Fact]
    public void Un_documento_senza_sezioni_non_ha_niente_da_filtrare()
    {
        Assert.False(AudienceFilter.HaSezioniMarcate(Array.Empty<SectionView>()));
        Assert.Empty(AudienceFilter.Filtra(Array.Empty<SectionView>(), SectionAudience.Pilots));
    }
}
