using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>Quale edizione può esistere su quale campo</b> (carta vSOP militari §5-bis).
///
/// <para>Due guardie gemelle, e vanno lette insieme perché una senza l'altra è sbagliata:</para>
/// <list type="bullet">
///   <item>su un campo <b>solo militare</b> (Aviano, Ghedi, Decimomannu, Rivolto) la vIPI <b>civile</b> non
///   nasce: non c'è traffico civile da descrivere, e il documento resterebbe lì vuoto;</item>
///   <item>su un campo <b>misto</b> (Pisa, Linate, Ciampino) il vSOP <b>militare</b> nasce solo <b>dopo</b> la
///   vIPI civile: dice cosa cambia rispetto a quella, e senza non c'è il «rispetto a cosa».</item>
/// </list>
///
/// <para>⚠️ <b>Le guardie stanno nei SERVIZI</b>, non nelle tendine che le anticipano. Una tendina filtra,
/// non autorizza: chi conosce l'indirizzo dell'editor ci arriva lo stesso — è il difetto già pagato su
/// <c>/services/vsop/versions</c> il 21 agosto 2026. I test che contano davvero sono quelli che chiamano il
/// servizio, non quelli che guardano una riga d'elenco.</para>
/// </summary>
public class EdizioneGiustaPerCampoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    /// <summary>Autorizzazione permissiva: qui si provano le REGOLE sui campi, non i permessi.</summary>
    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 42;
        public string? CurrentName => "test";
    }

    private EfMilitaryDocumentService Militari() =>
        new(_db, new AiracService(), new AllowAuthz(),
            new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)),
            new EfSpecialAreaRepository(_db), new EfNavaidCatalog(_db));

    private EfAirportRepository Repo() => new(_db, new EfMediaMaintenance(_db));

    private AirportEditingService Civile() =>
        new(Repo(), new AllowAuthz(), new NienteDirectory(), new NienteDetails(), new EfImportPolicyStore(_db),
            LockAperto.Instance);

    /// <summary>La sorgente esterna non serve a queste regole: EnsureDocumentAsync non la interroga. Due
    /// finti vuoti valgono più di un mock, che qui direbbe soltanto che non è stato chiamato.</summary>
    private sealed class NienteDirectory : IAirportDirectory
    {
        public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceAirport>>(Array.Empty<SourceAirport>());
        public Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<SourceAirport?>(null);
    }

    private sealed class NienteDetails : IAirportDetailProvider
    {
        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceAtcPosition>>(Array.Empty<SourceAtcPosition>());
        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default) =>
            Task.FromResult<SourceAtcPosition?>(null);
        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceRunway>>(Array.Empty<SourceRunway>());
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIPP", Name = "Padova" };
        _db.Accs.Add(acc);
        _db.Airports.AddRange(
            // Rivolto: campo SOLO militare — l'unica edizione è il vSOP.
            new Airport { Icao = "LIPI", Name = "Rivolto", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = true },
            // Pisa: scalo civile con sedime militare — servono TUTTE E DUE, nell'ordine.
            new Airport { Icao = "LIRP", Name = "Pisa", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = false },
            // Venezia: niente di militare.
            new Airport { Icao = "LIPZ", Name = "Venezia", Acc = acc, HasMilitaryPresence = false });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- Campo SOLO militare: niente vIPI civile ------------------------------------------------------

    [Fact]
    public async Task Su_un_campo_SOLO_militare_la_vIPI_civile_NON_nasce()
    {
        // ⚠️ Questo è il test della guardia dura. Prima bastava arrivare all'indirizzo dell'editor civile
        // perché il documento nascesse: `EnsureDocumentAsync` è chiamato dall'APERTURA della pagina.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Civile().EnsureDocumentAsync("LIPI"));

        Assert.Null((await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIPI")).DocumentId);
    }

    [Fact]
    public async Task Su_un_campo_SOLO_militare_il_vSOP_nasce_SENZA_chiedere_la_civile()
    {
        // ⚠️ È l'altra metà della regola, e senza questo test la prima guardia si potrebbe «aggiustare»
        // chiedendo la civile ovunque — rendendo Aviano e Ghedi, cioè proprio i campi che un vSOP ce l'hanno,
        // gli unici a non poterlo avere.
        var id = await Militari().CreaAsync("LIPI");
        Assert.Equal(id, (await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIPI")).MilDocumentId);
    }

    [Fact]
    public async Task Una_vIPI_civile_che_ESISTE_GIA_su_un_campo_solo_militare_si_apre_lo_stesso()
    {
        // La guardia blocca la NASCITA, non l'apertura: un documento creato prima della regola (o su un campo
        // marcato dopo) deve restare leggibile e modificabile — la via d'uscita passa proprio da lì.
        var docId = await Civile().EnsureDocumentAsync("LIRP");   // Pisa: misto, quindi lecito
        var pisa = await _db.Airports.SingleAsync(a => a.Icao == "LIRP");
        pisa.IsMilitaryOnly = true;                                // marcato solo militare DOPO
        await _db.SaveChangesAsync();

        Assert.Equal(docId, await Civile().EnsureDocumentAsync("LIRP"));
    }

    // ---- Campo MISTO: prima la civile, poi il militare ------------------------------------------------

    [Fact]
    public async Task Su_un_campo_MISTO_il_vSOP_militare_non_nasce_prima_della_vIPI_civile()
    {
        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Militari().CreaAsync("LIRP"));

        // Il rifiuto dice cosa fare, non solo che no: è la differenza fra una guardia e un muro.
        // ⚠️ Si cerca «vIPI» e non «civile»: `Lingua()` sceglie sulla cultura corrente, e nella suite è
        // l'INGLESE. Un'asserzione sulla parola italiana passava solo per caso di ambiente.
        Assert.Contains("vIPI", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRP")).MilDocumentId);
    }

    [Fact]
    public async Task Su_un_campo_MISTO_basta_che_la_vIPI_civile_sia_in_BOZZA()
    {
        // ⚠️ ESISTERE, non essere pubblicata: pretendere la pubblicazione bloccherebbe il lavoro parallelo
        // sulle due edizioni, che è il caso normale su uno scalo appena aperto. La civile nasce in bozza.
        await Civile().EnsureDocumentAsync("LIRP");
        var civile = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRP");
        Assert.NotNull(civile.DocumentId);
        Assert.Equal(DocumentStatus.Draft,
            (await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == civile.DocumentId)).Status);

        var milId = await Militari().CreaAsync("LIRP");
        Assert.NotEqual(civile.DocumentId, milId);   // due documenti, due edizioni
    }

    [Fact]
    public async Task Un_campo_senza_presenza_militare_resta_rifiutato_come_prima()
    {
        // La regola nuova non ha allentato quella vecchia: su Venezia il primo rifiuto resta il suo.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Militari().CreaAsync("LIPZ"));
    }

    // ---- Ciò che l'elenco militare deve sapere per non offrire un tasto che fallisce -------------------

    [Fact]
    public async Task L_elenco_dice_se_la_vIPI_civile_c_e()
    {
        var prima = await Militari().ListAsync(perStaff: true);
        Assert.False(prima.Single(r => r.Icao == "LIRP").HaCivile);
        // Sul campo solo militare la domanda non si pone, e la risposta è comunque onesta.
        Assert.False(prima.Single(r => r.Icao == "LIPI").HaCivile);

        await Civile().EnsureDocumentAsync("LIRP");

        var dopo = await Militari().ListAsync(perStaff: true);
        Assert.True(dopo.Single(r => r.Icao == "LIRP").HaCivile);
    }

    // ---- Il ponte militare → civile, e il difetto che resta dal passato -------------------------------

    /// <summary>
    /// ⚠️ Le tre risposte servono tutte, e per ragioni diverse: <b>pubblicata</b> accende il ponte per il
    /// pubblico, <b>esiste</b> lo accende per lo staff (una civile appena nata è in bozza, e un
    /// collegamento che compare solo dopo la pubblicazione compare quando non serve più), <b>solo
    /// militare</b> dice se l'assenza del civile è la regola o un difetto.
    /// </summary>
    [Fact]
    public async Task Lo_stato_della_vIPI_civile_dice_ESISTE_PUBBLICATA_e_SOLO_MILITARE()
    {
        // Campo misto, nessuna civile: è il caso che va DETTO — un vSOP militare orfano.
        var prima = await Militari().GetCivilEditionAsync("LIRP");
        Assert.False(prima.Esiste);
        Assert.False(prima.Pubblicata);
        Assert.False(prima.SoloMilitare);

        // Creata: esiste, ma è una BOZZA. Il pubblico non deve vedere il ponte, lo staff sì.
        await Civile().EnsureDocumentAsync("LIRP");
        var bozza = await Militari().GetCivilEditionAsync("LIRP");
        Assert.True(bozza.Esiste);
        Assert.False(bozza.Pubblicata);

        // Con una release effettiva il ponte si accende anche per il pubblico.
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.Airport, TargetKey = "LIRP", VersionNumber = 1,
            ReleaseAiracCycle = "2609", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1),
            Status = ReleaseStatus.Effective, PayloadJson = "{}", CreatedByUserId = 1,
            CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var pubblicata = await Militari().GetCivilEditionAsync("LIRP");
        Assert.True(pubblicata.Esiste);
        Assert.True(pubblicata.Pubblicata);
    }

    [Fact]
    public async Task Su_un_campo_SOLO_militare_l_assenza_del_civile_e_la_REGOLA_e_lo_dice()
    {
        // ⚠️ Senza questo campo la pagina militare di Rivolto griderebbe «manca la vIPI civile» per sempre,
        // su un campo dove quella vIPI non deve esistere.
        var rivolto = await Militari().GetCivilEditionAsync("LIPI");
        Assert.False(rivolto.Esiste);
        Assert.True(rivolto.SoloMilitare);
    }

    [Fact]
    public async Task Un_ICAO_sconosciuto_non_dichiara_che_l_assenza_e_a_norma()
    {
        // ⚠️ `SoloMilitare` falso e non vero: di quel campo non si sa niente, e dire «a norma» sarebbe
        // rispondere a una domanda che non è stata posta.
        var ignoto = await Militari().GetCivilEditionAsync("ZZZZ");
        Assert.False(ignoto.Esiste);
        Assert.False(ignoto.Pubblicata);
        Assert.False(ignoto.SoloMilitare);
    }

    // ---- La lettura che le due guardie condividono ----------------------------------------------------

    [Fact]
    public async Task Lo_stato_militare_si_legge_in_una_volta_sola_e_dal_DATABASE()
    {
        var rivolto = await Repo().GetMilitaryStateAsync("lipi");   // anche minuscolo: si normalizza
        Assert.NotNull(rivolto);
        Assert.True(rivolto!.HasMilitaryPresence);
        Assert.True(rivolto.IsMilitaryOnly);
        Assert.Null(rivolto.DocumentId);
        Assert.Null(rivolto.MilDocumentId);

        await Militari().CreaAsync("LIPI");
        Assert.NotNull((await Repo().GetMilitaryStateAsync("LIPI"))!.MilDocumentId);

        // Un ICAO che non c'è risponde «non c'è», non un oggetto a zero: sono due risposte diverse.
        Assert.Null(await Repo().GetMilitaryStateAsync("ZZZZ"));
    }

    // ---- I dati dell'ANAGRAFICA su un campo solo militare ---------------------------------------------

    /// <summary>
    /// ⚠️ <b>La guardia rifiuta la vIPI civile, non i dati dell'aeroporto.</b>
    ///
    /// <para>Quote di transizione, piste e collegamenti di frequenza sono dell'<b>anagrafica dello
    /// scalo</b>: si salvano sul solo ICAO, e nessuno di quei metodi chiede un documento (i collegamenti
    /// non si provano qui perché vogliono un catalogo settori, ma sono lo stesso servizio con la stessa
    /// chiave). È ciò che rende
    /// scrivibili dall'editor del <b>vSOP militare</b> — l'unica pagina che su un campo solo militare si
    /// apra — dati che fino al 2 settembre 2026 non avevano <b>nessuna</b> porta di scrittura in tutto il
    /// sito: l'editor d'aeroporto, l'unico che li mostrava, lì rimanda indietro, quindi il rimando era un
    /// giro chiuso.</para>
    ///
    /// <para>Se un giorno una di queste pretendesse il documento civile, questo test diventa rosso — ed è
    /// l'unico posto che se ne accorgerebbe, perché la pagina che li ospita non ha altra rete.</para>
    /// </summary>
    [Fact]
    public async Task Su_un_campo_SOLO_militare_i_dati_dell_aeroporto_restano_scrivibili()
    {
        var rivolto = await _db.Airports.SingleAsync(a => a.Icao == "LIPI");

        // La pista come la lascia l'import IVAO: ident, lunghezza e rotta di SORGENTE, colonne editoriali vuote.
        _db.AirportRunways.Add(new AirportRunway { AirportId = rivolto.Id, Ident = "05", LengthM = 2990, Bearing = 50 });
        await _db.SaveChangesAsync();

        var civile = Civile();
        await civile.SaveTransitionLevelsAsync("LIPI", new[] { new TlRow(0, null, 1013, "FL60") });

        // ⚠️ Solo le colonne EDITORIALI: con la policy d'import di default le piste sono di sorgente, quindi
        // ident/lunghezza/rotta si ripassano identiche — è esattamente ciò che fa l'editor a schermo.
        await civile.SaveRunwaysAsync("LIPI",
            new[] { new RunwayRow(0, "05", 2990, 50, "2990", "2850", "ILS Z", null, null) });

        // ⚠️ La TA è di SORGENTE con la policy di default, e il rifiuto che si prende qui è quello di
        // «Sorgenti dati» — lo stesso che si prende su Linate. Non è una regola dei campi militari, ed è il
        // motivo per cui questo test non la scrive: confonderla con la guardia dell'edizione vorrebbe dire
        // credere risolto un blocco che sta altrove.
        var sorgente = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => civile.SetTransitionAltitudeAsync("LIPI", 5000));
        Assert.Contains("source", sorgente.Message, StringComparison.OrdinalIgnoreCase);

        var dati = await civile.LoadForViewAsync("LIPI");
        Assert.NotNull(dati);
        Assert.Equal("FL60", Assert.Single(dati!.TransitionLevels).Level);
        Assert.Equal("2850", Assert.Single(dati.Runways).LdaM);

        // E la vIPI civile continua a NON esistere: si sono scritti dati dello SCALO, non un'edizione.
        Assert.Null((await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIPI")).DocumentId);
    }
}
