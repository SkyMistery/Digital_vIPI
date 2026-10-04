using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le richieste dal campo (S56, committente, 29 settembre 2026; carta <c>docs/design/piano-segnalazioni.md</c>): un
/// utente IVAO connesso scrive a chi fa i documenti, e riceve una risposta. Sul database vero: il servizio risale
/// dal documento che la pagina conosce (famiglia + chiave) alla pubblicazione che il lettore aveva davanti.
/// </summary>
public class RichiesteDalCampoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _docId;
    private DateTime _adesso = new(2026, 9, 29, 18, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI Fiumicino", Language = Language.It, LastUpdatedAiracCycle = "2610", Status = DocumentStatus.Published };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docId = doc.Id;
        _db.Airports.Add(new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = acc, DocumentId = doc.Id });
        var versione = new DocumentVersion { DocumentId = doc.Id, VersionNumber = 1, AiracCycle = "2610", Status = DocumentStatus.Published };
        _db.DocumentVersions.Add(versione);
        await _db.SaveChangesAsync();
        doc.CurrentVersionId = versione.Id;
        _db.DocumentSections.Add(new DocumentSection { DocumentVersionId = versione.Id, Title = "Piste", Order = 1, Depth = 0, SectionKey = "runways" });
        // Due pubblicazioni: la 3 in vigore, la 4 programmata per dopo. Il lettore stava leggendo la 3.
        foreach (var (n, quando) in new[] { (3, _adesso.AddDays(-5)), (4, _adesso.AddDays(20)) })
            _db.DocReleases.Add(new DocRelease
            {
                TargetType = ReleaseTargetType.Airport, TargetKey = "LIRF", VersionNumber = n, ReleaseAiracCycle = "2610",
                ReleaseEffectiveUtc = quando, Status = ReleaseStatus.Scheduled, PayloadJson = "{}",
            });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private FieldRequestService Servizio(Authz chi) => new(new EfFieldRequestRepository(_db), new EfEditorTaskRepository(_db),
        TestReleaseTargets.AdminRepo(_db), TestReleaseTargets.Registry(_db), TestReleaseTargets.ReleaseRepo(_db),
        new EfAccDerivationRepository(_db), chi, () => _adesso);

    private static readonly Authz Utente = new(VipiRole.User, 111111, "Mario Pilota");
    private static readonly Authz Anonimo = new(VipiRole.User, null, null);
    private static readonly Authz Staff = new(VipiRole.Editor, 704798, "Carmine");

    private Task<int> ApriAsync(Authz chi, string corpo = "La pista 16L è chiusa, non 16R") =>
        Servizio(chi).ApriAsync(new FieldRequestInput(ReleaseTargetType.Airport, "LIRF", "runways", FieldRequestKind.Errore, corpo));

    [Fact]
    public async Task Da_una_sezione_pubblica_nasce_una_richiesta_col_rilascio_che_il_lettore_aveva_davanti()
    {
        var contesto = await Servizio(Utente).ContestoAsync(ReleaseTargetType.Airport, "LIRF", "runways");
        Assert.Equal((_docId, "vIPI Fiumicino", "Piste"), (contesto!.DocumentId, contesto.DocumentTitle, contesto.SectionTitle));

        var id = await ApriAsync(Utente);

        var mia = Assert.Single(await Servizio(Utente).MieAsync());
        Assert.Equal((id, _docId, "runways", 3, FieldRequestStatus.Nuova, "Mario Pilota"),
            (mia.Id, mia.DocumentId, mia.SectionKey, mia.ReleaseNumber, mia.Status, mia.ReporterName));
        Assert.Empty(await Servizio(new Authz(VipiRole.User, 222222, "Altro")).MieAsync());   // «le mie» sono solo mie
    }

    /// <summary>
    /// Dal tasto in barra (committente, 30 settembre 2026): una segnalazione generica porta la PAGINA, e la presa in
    /// carico ne fa il titolo dell'incarico.
    /// </summary>
    [Fact]
    public async Task Una_segnalazione_di_pagina_porta_la_pagina_fino_all_incarico()
    {
        var id = await Servizio(Utente).ApriAsync(new FieldRequestInput(null, null, null, FieldRequestKind.Errore,
            "La classifica non si carica", "/services/stats/division?p=30"));

        var r = Assert.Single(await Servizio(Staff).CodaAsync());
        Assert.Equal((id, "/services/stats/division?p=30", (int?)null), (r.Id, r.PageUrl, r.DocumentId));

        var incarico = await Servizio(Staff).PrendiInCaricoAsync(id);
        var titolo = await _db.EditorTasks.Where(x => x.Id == incarico).Select(x => x.Title).SingleAsync();
        Assert.Contains("/services/stats/division?p=30", titolo);
    }

    /// <summary>La pagina arriva dall'indirizzo, cioè da chiunque: si tiene solo un percorso del sito.</summary>
    [Theory]
    [InlineData("https://evil.example/x", "")]
    [InlineData("//evil.example/x", "")]
    [InlineData("javascript:alert(1)", "")]
    [InlineData("  /services/vsop/lirr  ", "/services/vsop/lirr")]
    [InlineData(null, "")]
    public void La_pagina_si_tiene_solo_se_e_un_percorso_del_sito(string? arriva, string resta) =>
        Assert.Equal(resta, FieldRequestRules.Pagina(arriva));

    [Fact]
    public async Task Chi_non_e_connesso_non_scrive_e_un_testo_vuoto_o_lunghissimo_si_rifiuta()
    {
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => ApriAsync(Anonimo));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => ApriAsync(Utente, "   "));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => ApriAsync(Utente, new string('x', 2001)));
        Assert.False(await _db.FieldRequests.AnyAsync());
    }

    [Fact]
    public async Task Dieci_in_attesa_bastano_e_dieci_al_giorno_pure()
    {
        // ⚠️ Dal 30 settembre 2026 i due tetti valgono uguale (dieci): a fermare è il PRIMO controllo, le aperte, e il
        // messaggio lo dice. Il tetto delle 24 ore si vede quando lo staff ha già risposto a tutte.
        Assert.Equal((10, 10), (FieldRequestRules.MaxAperte, FieldRequestRules.MaxAlGiorno));
        for (var i = 0; i < FieldRequestRules.MaxAperte; i++) await ApriAsync(Utente);
        var aperte = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => ApriAsync(Utente));
        Assert.Matches("in attesa|waiting", aperte.Message);   // la lingua dei messaggi è quella di chi legge

        // Lo staff risponde a tutte: le aperte non fermano più, ma nelle 24 ore ne sono già state scritte dieci.
        foreach (var r in await Servizio(Staff).CodaAsync()) await Servizio(Staff).RisolviAsync(r.Id, "Corretto, grazie.");
        var giorno = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => ApriAsync(Utente));
        Assert.Matches("24 ore|24 hours", giorno.Message);

        _adesso = _adesso.AddDays(1).AddMinutes(1);
        await ApriAsync(Utente);   // il giorno dopo sì
    }

    [Fact]
    public async Task Presa_in_carico_nasce_un_incarico_e_ogni_chiusura_ha_la_sua_frase()
    {
        var id = await ApriAsync(Utente);
        await Assert.ThrowsAsync<EditNotAllowedException>(() => Servizio(Utente).CodaAsync());
        await Assert.ThrowsAsync<EditNotAllowedException>(() => Servizio(Utente).PrendiInCaricoAsync(id));

        var incarico = await Servizio(Staff).PrendiInCaricoAsync(id);
        Assert.Equal(incarico, await Servizio(Staff).PrendiInCaricoAsync(id));   // due volte: lo stesso incarico
        var t = await _db.EditorTasks.AsNoTracking().SingleAsync(x => x.Id == incarico);
        Assert.Equal((id, 704798, ReleaseTargetType.Airport, "LIRF"), (t.FromRequestId!.Value, t.AssigneeUserId, t.TargetType!.Value, t.TargetKey));
        Assert.Equal(FieldRequestStatus.PresaInCarico, (await Servizio(Staff).CodaAsync()).Single().Status);

        // D4: la risposta non è un optional del rifiuto, è il rifiuto.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Servizio(Staff).RespingiAsync(id, " "));
        await Servizio(Staff).RespingiAsync(id, "La 16L è aperta: il NOTAM è scaduto ieri.");
        var mia = (await Servizio(Utente).MieAsync()).Single();
        Assert.Equal((FieldRequestStatus.Respinta, "La 16L è aperta: il NOTAM è scaduto ieri.", "Carmine"), (mia.Status, mia.Reply, mia.HandledByName));
        Assert.Empty(await Servizio(Staff).CodaAsync());
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Servizio(Staff).RisolviAsync(id, "ripensamento"));

        // Decisione del committente (29-set): chiudendo la richiesta si chiude anche il suo incarico, che
        // altrimenti restava in «Da fare» dimenticato.
        var chiuso = await _db.EditorTasks.AsNoTracking().SingleAsync(x => x.Id == incarico);
        Assert.Equal(EditorTaskStatus.Done, chiuso.Status);
        Assert.NotNull(chiuso.CompletedUtc);
    }

    [Fact]
    public async Task Un_doppione_rimanda_all_altra_richiesta_da_solo()
    {
        var prima = await ApriAsync(Utente);
        var seconda = await ApriAsync(new Authz(VipiRole.User, 333333, "Luigi"));

        await Servizio(Staff).DoppioneAsync(seconda, prima, null);

        var r = (await Servizio(Staff).CodaAsync(ancheChiuse: true)).Single(x => x.Id == seconda);
        Assert.Equal((FieldRequestStatus.Doppione, prima), (r.Status, r.DuplicateOfId!.Value));
        Assert.Contains($"#{prima}", r.Reply);
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => Servizio(Staff).DoppioneAsync(prima, prima, null));
    }

    /// <summary>
    /// Eliminare una richiesta (committente, 30 settembre 2026): solo l'Admin, aperta o chiusa. Se era presa in carico,
    /// il suo incarico si chiude — o resterebbe in «Da fare» su una domanda che non c'è più.
    /// </summary>
    [Fact]
    public async Task Solo_l_admin_elimina_e_l_incarico_legato_si_chiude()
    {
        var id = await ApriAsync(Utente);
        var incarico = await Servizio(Staff).PrendiInCaricoAsync(id);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => Servizio(Staff).EliminaAsync(id));   // Editor: no
        Assert.True(await _db.FieldRequests.AnyAsync(r => r.Id == id));

        await Servizio(new Authz(VipiRole.Admin, 704798, "Carmine")).EliminaAsync(id);
        Assert.False(await _db.FieldRequests.AnyAsync(r => r.Id == id));
        Assert.Equal(EditorTaskStatus.Done, (await _db.EditorTasks.SingleAsync(t => t.Id == incarico)).Status);
    }

    /// <summary>
    /// La pulizia automatica (committente, d'accordo con IT-HQ): le CHIUSE da più di tre mesi se ne vanno, le chiuse
    /// recenti e le APERTE restano — anche un'aperta vecchia, perché è lavoro che aspetta una risposta.
    /// </summary>
    [Fact]
    public async Task La_pulizia_toglie_solo_le_chiuse_da_piu_di_tre_mesi()
    {
        var vecchiaChiusa = await ApriAsync(Utente, "vecchia chiusa");
        var recenteChiusa = await ApriAsync(Utente, "recente chiusa");
        var vecchiaAperta = await ApriAsync(Utente, "vecchia aperta");
        await Servizio(Staff).RisolviAsync(vecchiaChiusa, "fatto");
        await Servizio(Staff).RespingiAsync(recenteChiusa, "no");

        var soglia = DateTime.UtcNow.AddMonths(-FieldRequestRules.MesiDiConservazione);
        foreach (var r in await _db.FieldRequests.ToListAsync())
        {
            if (r.Id == vecchiaChiusa) r.HandledUtc = soglia.AddDays(-1);
            if (r.Id == recenteChiusa) r.HandledUtc = soglia.AddDays(1);
            r.CreatedUtc = soglia.AddDays(-30);                        // tutte nate da tanto: conta la chiusura
        }
        await _db.SaveChangesAsync();

        Assert.Equal(1, await new EfFieldRequestRepository(_db).PotaChiuseAsync(soglia));
        Assert.Equal(new[] { recenteChiusa, vecchiaAperta }.Order(), (await _db.FieldRequests.Select(r => r.Id).ToListAsync()).Order());
    }

    private sealed class Authz(VipiRole livello, int? vid, string? nome) : IEditAuthorizationService
    {
        public VipiRole Role => livello;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => vid;
        public string? CurrentName => nome;
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
    }
}
