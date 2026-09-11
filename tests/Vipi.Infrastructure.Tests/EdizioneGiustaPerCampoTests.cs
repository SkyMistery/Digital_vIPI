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
/// <b>Quale edizione può esistere su quale campo</b>: lo decide la <b>categoria</b> dello scalo (carta
/// <c>2026-09-11-categorie-aeroporto.md</c>, che prende il posto della regola §5-bis della carta vSOP militari).
///
/// <list type="bullet">
///   <item><b>Civile</b> (Venezia): solo la vIPI;</item>
///   <item><b>solo militare</b> (Rivolto): solo il vSOP — non c'è traffico civile da descrivere;</item>
///   <item><b>civile con presenza militare</b> (Linate): solo la vIPI;</item>
///   <item><b>militare con presenza civile</b> (Pisa): tutti e due, <b>in qualunque ordine</b>. Fino all'11
///   settembre 2026 il vSOP nasceva solo dopo la vIPI: il committente ha tolto quel vincolo.</item>
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
            new Airport { Icao = "LIPI", Name = "Rivolto", Acc = acc, HasMilitaryPresence = true, Category = AirportCategory.MilitaryOnly },
            // Pisa: campo militare con presenza civile — tutte e due le edizioni, in qualunque ordine.
            new Airport { Icao = "LIRP", Name = "Pisa", Acc = acc, HasMilitaryPresence = true, Category = AirportCategory.MilitaryWithCivilPresence },
            // Linate: scalo civile con sedime militare — solo la vIPI.
            new Airport { Icao = "LIML", Name = "Linate", Acc = acc, HasMilitaryPresence = true, Category = AirportCategory.CivilWithMilitaryPresence },
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
        var docId = await Civile().EnsureDocumentAsync("LIRP");   // Pisa: ammette la vIPI, quindi lecito
        var pisa = await _db.Airports.SingleAsync(a => a.Icao == "LIRP");
        pisa.Category = AirportCategory.MilitaryOnly;              // marcato solo militare DOPO
        await _db.SaveChangesAsync();

        Assert.Equal(docId, await Civile().EnsureDocumentAsync("LIRP"));
    }

    // ---- Le quattro categorie, tutte: chi nasce e chi no ----------------------------------------------

    /// <summary>
    /// La tabella della carta, provata riga per riga contro i DUE servizi — non contro una tendina. Ogni caso
    /// è uno scalo nuovo, così un documento nato in una riga non fa da premessa alla successiva.
    /// </summary>
    [Theory]
    [InlineData(AirportCategory.Civil, true, false)]
    [InlineData(AirportCategory.MilitaryOnly, false, true)]
    [InlineData(AirportCategory.CivilWithMilitaryPresence, true, false)]
    [InlineData(AirportCategory.MilitaryWithCivilPresence, true, true)]
    public async Task Le_guardie_di_nascita_rispondono_per_categoria(AirportCategory categoria, bool vipiNasce, bool vsopNasce)
    {
        var acc = await _db.Accs.SingleAsync();
        _db.Airports.Add(new Airport
        {
            Icao = "LIXX", Name = "Prova", Acc = acc,
            HasMilitaryPresence = categoria != AirportCategory.Civil, Category = categoria,
        });
        await _db.SaveChangesAsync();

        // ⚠️ Il vSOP PRIMA della vIPI: è l'ordine che fino all'11 settembre era vietato sui campi misti, e
        // provarlo in questo verso è ciò che dice che il vincolo è davvero caduto.
        if (vsopNasce) await Militari().CreaAsync("LIXX");
        else await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Militari().CreaAsync("LIXX"));

        if (vipiNasce) await Civile().EnsureDocumentAsync("LIXX");
        else await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Civile().EnsureDocumentAsync("LIXX"));

        var campo = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIXX");
        Assert.Equal(vipiNasce, campo.DocumentId is not null);
        Assert.Equal(vsopNasce, campo.MilDocumentId is not null);
    }

    [Fact]
    public async Task Su_uno_scalo_civile_con_presenza_militare_il_rifiuto_dice_dove_si_cambia()
    {
        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Militari().CreaAsync("LIML"));

        // Il rifiuto dice cosa fare, non solo che no: è la differenza fra una guardia e un muro.
        // ⚠️ Si cerca la parola inglese: `Lingua()` sceglie sulla cultura corrente, e nella suite è l'INGLESE.
        Assert.Contains("category", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- L'elenco militare: i candidati sono quelli che la categoria ammette --------------------------

    [Fact]
    public async Task L_elenco_offre_i_campi_che_la_categoria_ammette_piu_quelli_che_un_vSOP_ce_l_hanno_gia()
    {
        var elenco = (await Militari().ListAsync(perStaff: true)).Select(r => r.Icao).ToList();
        Assert.Contains("LIPI", elenco);       // solo militare
        Assert.Contains("LIRP", elenco);       // militare con presenza civile
        Assert.DoesNotContain("LIML", elenco); // civile con presenza militare: nessun «Crea» da offrire
        Assert.DoesNotContain("LIPZ", elenco);

        // ⚠️ Un vSOP che c'è già, anche fuori categoria, si deve raggiungere: nato su Linate quando era in 4,
        // resta in elenco dopo che la categoria è passata a 3. La Diagnostica dice che è fuori posto.
        var linate = await _db.Airports.SingleAsync(a => a.Icao == "LIML");
        linate.Category = AirportCategory.MilitaryWithCivilPresence;
        await _db.SaveChangesAsync();
        await Militari().CreaAsync("LIML");
        linate.Category = AirportCategory.CivilWithMilitaryPresence;
        await _db.SaveChangesAsync();

        var dopo = await Militari().ListAsync(perStaff: true);
        Assert.Equal(AirportCategory.CivilWithMilitaryPresence, dopo.Single(r => r.Icao == "LIML").Categoria);
    }

    // ---- Il ponte militare → civile, e il difetto che resta dal passato -------------------------------

    /// <summary>
    /// ⚠️ Le tre risposte servono tutte, e per ragioni diverse: <b>pubblicata</b> accende il ponte per il
    /// pubblico, <b>esiste</b> lo accende per lo staff (una civile appena nata è in bozza, e un
    /// collegamento che compare solo dopo la pubblicazione compare quando non serve più), la <b>categoria</b>
    /// dice se l'assenza del civile è la regola.
    /// </summary>
    [Fact]
    public async Task Lo_stato_della_vIPI_civile_dice_ESISTE_PUBBLICATA_e_CATEGORIA()
    {
        var prima = await Militari().GetCivilEditionAsync("LIRP");
        Assert.False(prima.Esiste);
        Assert.False(prima.Pubblicata);
        Assert.Equal(AirportCategory.MilitaryWithCivilPresence, prima.Categoria);

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
        // ⚠️ Senza la categoria l'editor militare di Rivolto non saprebbe che i dati dello scalo si scrivono
        // lì, su un campo dove quella vIPI non deve esistere.
        var rivolto = await Militari().GetCivilEditionAsync("LIPI");
        Assert.False(rivolto.Esiste);
        Assert.False(rivolto.Categoria.AllowsCivil());
    }

    [Fact]
    public async Task Un_ICAO_sconosciuto_non_dichiara_che_l_assenza_e_a_norma()
    {
        // ⚠️ Civile e non «solo militare»: di quel campo non si sa niente, e dire «a norma» sarebbe
        // rispondere a una domanda che non è stata posta.
        var ignoto = await Militari().GetCivilEditionAsync("ZZZZ");
        Assert.False(ignoto.Esiste);
        Assert.False(ignoto.Pubblicata);
        Assert.True(ignoto.Categoria.AllowsCivil());
    }

    // ---- La lettura che le due guardie condividono ----------------------------------------------------

    [Fact]
    public async Task Lo_stato_militare_si_legge_in_una_volta_sola_e_dal_DATABASE()
    {
        var rivolto = await Repo().GetMilitaryStateAsync("lipi");   // anche minuscolo: si normalizza
        Assert.NotNull(rivolto);
        Assert.True(rivolto!.HasMilitaryPresence);
        Assert.Equal(AirportCategory.MilitaryOnly, rivolto.Category);
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
