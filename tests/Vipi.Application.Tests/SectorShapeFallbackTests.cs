using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il ripiego che dà un'area ai settori (CTR/APP/MIL/FSS) che dall'anagrafica non ne hanno ricevuta.
/// ⚠️ È un <b>ripiego</b>, e la prova che conta è che si comporti come tale: non tocca chi ha già una shape.
/// </summary>
public class SectorShapeFallbackTests
{
    private const string Quadrato = "[[11.0,44.0],[11.5,44.0],[11.5,44.5],[11.0,44.5]]";
    private const string Degenere = "[[11.0,44.0],[11.5,44.0]]";     // due punti: non si disegna

    private sealed class Repo : ISectorShapeRepository
    {
        public List<SectorShapeRow> Righe = new();
        public List<ShapeWrite> Scritte = new();
        public int Promossi = 0;

        public Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorShapeRow>>(Righe);

        public Task ApplyShapeAsync(ShapeWrite w, CancellationToken ct = default)
        {
            Scritte.Add(w);
            return Task.CompletedTask;
        }

        public Task<int> PromoteDueShapesAsync(DateTime nowUtc, CancellationToken ct = default) =>
            Task.FromResult(Promossi);
    }

    private sealed class Sorgente : ISectorShapeSource
    {
        public Dictionary<string, string> Poligoni = new(StringComparer.OrdinalIgnoreCase);
        public List<(string, string)> Irrisolti = new();
        public Task<SectorShapes> GetSectorPolygonsAsync(CancellationToken ct = default) =>
            Task.FromResult(new SectorShapes(Poligoni, Irrisolti));
    }

    private static readonly IAiracService Airac = new AiracService();

    private static SectorShapeRow Riga(int id, string cs, bool haShape,
        SourceCatalog cat = SourceCatalog.Subcenter,
        string? corrente = null, ShapeSource src = ShapeSource.Source) =>
        new(cat, id, cs, cs.Split('_').Last(), haShape,
            new ShapeState(corrente, null, null, src, false));

    private static SectorShapeFallbackService Servizio(Repo r, ISectorShapeSource s) => new(r, s, Airac);

    [Fact]
    public async Task Da_l_area_a_chi_non_ce_l_ha()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", haShape: false) } };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Quadrato } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(1, esito.Applied);
        Assert.Equal(0, esito.StillWithout);
        var scritta = Assert.Single(repo.Scritte);
        Assert.Equal(SourceCatalog.Subcenter, scritta.Catalog);
        Assert.Equal(1, scritta.Id);
        Assert.Equal(Quadrato, scritta.PolygonJson);
        Assert.Null(scritta.FromCycle);   // primo riempimento: in vigore subito
    }

    /// <summary>Il cuore: la shape dell'anagrafica comanda, e il ripiego non la tocca mai.</summary>
    [Fact]
    public async Task Non_tocca_chi_ha_gia_un_area()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", haShape: true) } };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Quadrato } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(0, esito.Applied);
        Assert.Empty(repo.Scritte);
    }

    [Fact]
    public async Task Un_settore_che_il_sectorfile_non_conosce_resta_contato()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", false), Riga(2, "LIBB_ES_CTR", false) } };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Quadrato } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(1, esito.Applied);
        Assert.Equal(1, esito.StillWithout);
    }

    /// <summary>
    /// ⚠️ Un anello degenere non si scrive. Passerebbe il controllo «non è vuota» e finirebbe in colonna come
    /// una shape vera: il settore uscirebbe dai bersagli del ripiego per sempre, restando senza area e senza
    /// più nessuno che ci riprovi.
    /// </summary>
    [Fact]
    public async Task Un_poligono_che_non_si_disegna_non_si_scrive()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", false) } };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Degenere } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(0, esito.Applied);
        Assert.Equal(1, esito.StillWithout);
        Assert.Empty(repo.Scritte);
    }

    [Fact]
    public async Task Scrive_sul_catalogo_giusto()
    {
        var repo = new Repo
        {
            Righe =
            {
                Riga(7, "LIRR_NE_CTR", false),
                Riga(9, "LIRF_TW1_APP", false, SourceCatalog.AirportPosition),
            },
        };
        var src = new Sorgente
        {
            Poligoni = { ["LIRR_NE_CTR"] = Quadrato, ["LIRF_TW1_APP"] = Quadrato },
        };

        await Servizio(repo, src).ApplyAsync();

        Assert.Contains(repo.Scritte, w => w.Catalog == SourceCatalog.Subcenter && w.Id == 7);
        Assert.Contains(repo.Scritte, w => w.Catalog == SourceCatalog.AirportPosition && w.Id == 9);
    }

    [Fact]
    public async Task Il_confronto_del_callsign_ignora_le_maiuscole()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", false) } };
        var src = new Sorgente { Poligoni = { ["lirr_ne_ctr"] = Quadrato } };

        Assert.Equal(1, (await Servizio(repo, src).ApplyAsync()).Applied);
    }

    /// <summary>Sorgente muta (rete giù, indice non raggiungibile): non si tocca niente e non si lancia.</summary>
    [Fact]
    public async Task Senza_sorgente_non_succede_niente()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", false) } };

        var esito = await Servizio(repo, new Sorgente()).ApplyAsync();

        Assert.Equal(0, esito.Applied);
        Assert.Equal(1, esito.StillWithout);
        Assert.Empty(repo.Scritte);
    }

    /// <summary>Se non c'è niente da fare, la sorgente non si interroga nemmeno: sono una ventina di GET.</summary>
    [Fact]
    public async Task Con_tutte_le_aree_a_posto_la_sorgente_non_si_scomoda()
    {
        var repo = new Repo { Righe = { Riga(1, "LIRR_NE_CTR", true) } };
        var src = new SorgenteCheEsplode();

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(0, esito.Applied);
    }

    private sealed class SorgenteCheEsplode : ISectorShapeSource
    {
        public Task<SectorShapes> GetSectorPolygonsAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("la sorgente non doveva essere interrogata");
    }

    /// <summary>I punti irrisolti arrivano al chiamante: sono la causa dei settori rimasti senza area.</summary>
    [Fact]
    public async Task I_punti_irrisolti_si_riportano()
    {
        var repo = new Repo { Righe = { Riga(1, "LIMM_WS2_CTR", false) } };
        var src = new Sorgente { Irrisolti = { ("GODRA", "LIMM_WS2_CTR LIMM_WS5_CTR") } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal("GODRA", Assert.Single(esito.UnresolvedPoints).Point);
    }

    // ---- il gate AIRAC: il sectorfile corre avanti ------------------------------------------------------

    private const string Altra = "[[12.0,45.0],[12.5,45.0],[12.5,45.5],[12.0,45.5]]";

    /// <summary>
    /// ⚠️ La prima stesura riempiva solo i vuoti, e cosi' il sectorfile sarebbe stato una sorgente
    /// <b>write-once</b>: un confine ridisegnato non sarebbe mai arrivato. Ora entra — ma differito.
    /// </summary>
    [Fact]
    public async Task Una_shape_del_sectorfile_cambiata_entra_differita_al_ciclo_prossimo()
    {
        var repo = new Repo
        {
            Righe = { Riga(1, "LIRR_NE_CTR", haShape: true, corrente: Quadrato, src: ShapeSource.Sectorfile) },
        };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Altra } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(0, esito.Applied);
        Assert.Equal(1, esito.Updated);
        var w = Assert.Single(repo.Scritte);
        Assert.Equal(Altra, w.PolygonJson);
        Assert.Equal(Quadrato, w.InForce);            // quella di adesso resta per chi pubblica nel frattempo
        Assert.False(string.IsNullOrWhiteSpace(w.FromCycle));
        Assert.NotEqual(Airac.GetCycle(DateTime.UtcNow), w.FromCycle);   // il PROSSIMO, non quello corrente
    }

    /// <summary>Identica: non si tocca niente, e soprattutto non si apre un differimento per nulla.</summary>
    [Fact]
    public async Task Una_shape_identica_non_si_riscrive()
    {
        var repo = new Repo
        {
            Righe = { Riga(1, "LIRR_NE_CTR", haShape: true, corrente: Quadrato, src: ShapeSource.Sectorfile) },
        };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Quadrato } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(0, esito.Updated);
        Assert.Empty(repo.Scritte);
    }

    /// <summary>⚠️ Una shape dell'ANAGRAFICA non si tocca nemmeno se il sectorfile ne ha una diversa: quella
    /// comanda, e il ripiego non è una seconda opinione.</summary>
    [Fact]
    public async Task Il_sectorfile_non_scavalca_l_anagrafica()
    {
        var repo = new Repo
        {
            Righe = { Riga(1, "LIRR_NE_CTR", haShape: true, corrente: Quadrato, src: ShapeSource.Source) },
        };
        var src = new Sorgente { Poligoni = { ["LIRR_NE_CTR"] = Altra } };

        var esito = await Servizio(repo, src).ApplyAsync();

        Assert.Equal(0, esito.Updated);
        Assert.Empty(repo.Scritte);
    }

    [Fact]
    public async Task I_differimenti_maturati_si_chiudono_e_si_contano()
    {
        var repo = new Repo { Promossi = 3, Righe = { Riga(1, "LIRR_NE_CTR", haShape: true) } };

        var esito = await Servizio(repo, new Sorgente()).ApplyAsync();

        Assert.Equal(3, esito.Promoted);
    }
}
