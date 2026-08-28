using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I titoli con un originale ufficiale non si fanno tradurre alla macchina.
///
/// <para>
/// ⚠️ Il difetto che li ha fatti nascere si è visto solo <b>a schermo</b>: «Piste» reso <i>Slopes</i> e
/// «Quote di transizione» reso <i>Transition Dimensions</i>. La macchina non poteva saperlo, noi sì —
/// quei titoli vengono dai quindici SOP, che sono scritti in inglese.
/// </para>
/// </summary>
public class TitoliUfficialiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfTranslationMemory Memoria() => new(_db);

    [Fact]
    public async Task Semina_i_titoli_e_li_marca_come_scritti_da_una_PERSONA()
    {
        var quanti = await TitoliUfficiali.SeminaAsync(Memoria());

        Assert.Equal(TitoliUfficiali.Sezioni.Count + TitoliUfficiali.Termini.Count, quanti);
        // ⚠️ Human e non Machine: sono l'ORIGINALE, non una proposta. Da Machine, il giro successivo li
        // rimanderebbe al motore e la pagina di revisione li elencherebbe fra le cose da guardare.
        Assert.All(await _db.TranslationUnits.AsNoTracking().ToListAsync(),
            u => Assert.Equal(TranslationOrigin.Human, u.Origin));
    }

    [Fact]
    public async Task Piste_diventa_Runways_e_non_quel_che_direbbe_la_macchina()
    {
        await TitoliUfficiali.SeminaAsync(Memoria());
        var memoria = await Memoria().LoadAllAsync("it", "en");

        Assert.Equal("Runways", memoria[TranslationText.Hash("Piste")]);
        Assert.Equal("Transition Altitude/Level", memoria[TranslationText.Hash("Quote di transizione")]);
        Assert.Equal("Working Areas", memoria[TranslationText.Hash("Aree di lavoro")]);

        // Le parole delle TABELLE: la macchina le sbagliava tutte, e sono le intestazioni che un
        // controllore legge per trovare il dato.
        Assert.Equal("Runway", memoria[TranslationText.Hash("Pista")]);       // era «Track»
        Assert.Equal("Apron", memoria[TranslationText.Hash("Piazzale")]);     // era «Forecourt»
        Assert.Equal("Altitude", memoria[TranslationText.Hash("Quota")]);     // era «Share»
        Assert.Equal("Bearing", memoria[TranslationText.Hash("Rilevamento")]); // era «Detection»
        Assert.Equal("Facility", memoria[TranslationText.Hash("Ente")]);      // era «Institution»
    }

    [Fact]
    public void Nessuna_voce_del_glossario_e_ripetuta()
    {
        // Due voci con la stessa sorgente e traduzioni diverse: la seconda vincerebbe in silenzio, e quale
        // sia «la seconda» dipenderebbe dall'ordine in cui sono scritte.
        var doppie = TitoliUfficiali.Sezioni.Concat(TitoliUfficiali.Termini)
            .GroupBy(t => t.It, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(doppie);
    }

    [Fact]
    public async Task Seminare_due_volte_non_scrive_due_volte()
    {
        await TitoliUfficiali.SeminaAsync(Memoria());
        Assert.Equal(0, await TitoliUfficiali.SeminaAsync(Memoria()));
        Assert.Equal(TitoliUfficiali.Sezioni.Count + TitoliUfficiali.Termini.Count,
            await _db.TranslationUnits.CountAsync());
    }

    [Fact]
    public async Task Una_correzione_umana_gia_in_memoria_VINCE_sulla_tabella()
    {
        // Chi rivede un documento ha l'ultima parola: la tabella semina ciò che manca, non ciò che c'è.
        await Memoria().SaveHumanAsync("it", "en", "Piste", "Runways (RWY)", reviewerUserId: 7);

        var quanti = await TitoliUfficiali.SeminaAsync(Memoria());

        Assert.Equal(TitoliUfficiali.Sezioni.Count + TitoliUfficiali.Termini.Count - 1, quanti);
        var memoria = await Memoria().LoadAllAsync("it", "en");
        Assert.Equal("Runways (RWY)", memoria[TranslationText.Hash("Piste")]);
    }

    [Fact]
    public async Task Una_resa_della_MACCHINA_gia_in_memoria_viene_CORRETTA()
    {
        // ⚠️ È il caso vero: quando questa tabella è nata, «Piste» → «Slopes» era GIÀ in memoria, messo lì
        // dal giro automatico. Un seme che si fermasse davanti a qualunque voce esistente non avrebbe
        // corretto niente proprio dove serviva.
        await Memoria().SaveMachineAsync("it", "en", "azure", new[] { ("Piste", "Slopes") });

        Assert.Equal(TitoliUfficiali.Sezioni.Count + TitoliUfficiali.Termini.Count,
            await TitoliUfficiali.SeminaAsync(Memoria()));

        var memoria = await Memoria().LoadAllAsync("it", "en");
        Assert.Equal("Runways", memoria[TranslationText.Hash("Piste")]);
    }

    [Fact]
    public void Ogni_titolo_del_profilo_militare_ha_il_suo_originale()
    {
        // ⚠️ La prova che tiene insieme le due liste: chi aggiunge una sezione al profilo e si dimentica
        // l'originale inglese scopre qui che la macchina la tradurrà da sé — che è come è nato il difetto.
        var tabella = TitoliUfficiali.Sezioni.Select(t => t.It).ToHashSet(StringComparer.Ordinal);
        var mancanti = Tutte(SectionCatalog.For(SectionProfile.AirportMil))
            .Select(d => d.Title)
            .Where(t => !tabella.Contains(t))
            .ToList();

        Assert.Empty(mancanti);
    }

    private static IEnumerable<SectionDescriptor> Tutte(IEnumerable<SectionDescriptor> d) =>
        d.SelectMany(x => new[] { x }.Concat(Tutte(x.Children ?? Array.Empty<SectionDescriptor>())));
}
