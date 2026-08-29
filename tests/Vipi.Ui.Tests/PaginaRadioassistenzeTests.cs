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
/// La pagina «Radioassistenze» (<c>/services/vsop/admin/navaids</c>): l'anagrafica di divisione in un posto
/// solo.
///
/// <para>Presidia le tre cose che la pagina deve fare e che a occhio si scambiano per dettagli: il tasto
/// d'import chiede il giro <b>che rilegge</b> la sorgente (e non quello notturno, che leggerebbe una copia
/// vecchia fino a ventiquattro ore); il cestino <b>non compare</b> sulle righe della sorgente, invece di
/// comparire e rifiutare; e il chip «senza tipo» conta le righe che aspettano una persona, che è il motivo
/// per cui questa pagina esiste.</para>
/// </summary>
public class PaginaRadioassistenzeTests : TestContext
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

    /// <summary>Anagrafica finta: l'elenco che si vuole a schermo, e nient'altro.</summary>
    private sealed class AnagraficaFinta : INavaidCatalog
    {
        private readonly List<NavaidRow> _righe;
        public AnagraficaFinta(params NavaidRow[] righe) => _righe = righe.ToList();

        public Task<IReadOnlyList<NavaidRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NavaidRow>>(_righe);
        public Task<IReadOnlyList<NavaidRow>> GetManyAsync(IReadOnlyList<NavaidKey> keys, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NavaidRow>>(Array.Empty<NavaidRow>());
        public Task<NavaidRow> CreateAsync(string code, string kind, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidDelete> DeleteAsync(int id, int userId, CancellationToken ct = default) =>
            Task.FromResult(NavaidDelete.Ok);
        public Task<IReadOnlyList<string>> CitataDaAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<NavaidWrite> SetTypeAsync(int id, string? tipo, int userId, CancellationToken ct = default) =>
            Task.FromResult(NavaidWrite.Ok);
        public Task<NavaidWrite> SetFrequencyAsync(int id, string? f, int userId, CancellationToken ct = default) =>
            Task.FromResult(NavaidWrite.Ok);
        public Task<NavaidWrite> SetChannelAsync(int id, string? c, int userId, CancellationToken ct = default) =>
            Task.FromResult(NavaidWrite.Ok);
        public Task<NavaidWrite> SetCoordinatesAsync(int id, string? s, int userId, CancellationToken ct = default) =>
            Task.FromResult(NavaidWrite.Ok);
        public Task<NavaidImportOutcome> ImportFromSourceAsync(IReadOnlyList<SourceNavaid> navaids, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>Importatore finto: conta quale dei due giri gli è stato chiesto.</summary>
    private sealed class ImportatoreFinto : INavaidImporter
    {
        private readonly NavaidImportReport _esito;
        public ImportatoreFinto(NavaidImportReport esito) => _esito = esito;
        public int Notturni { get; private set; }
        public int Adesso { get; private set; }

        public Task<NavaidImportReport> RunAsync(CancellationToken ct = default)
        {
            Notturni++;
            return Task.FromResult(_esito);
        }

        public Task<NavaidImportReport> RunNowAsync(CancellationToken ct = default)
        {
            Adesso++;
            return Task.FromResult(_esito);
        }
    }

    private static NavaidRow DallaSorgente(int id, string code, string? tipo = null) =>
        new(id, code, "VHF", tipo, "115.25", "99Y", 41.5, 15.6,
            NavaidFieldOrigin.Source, NavaidFieldOrigin.Source, NavaidFieldOrigin.Source, null, null);

    private static NavaidRow Nostra(int id, string code, string? tipo = null) =>
        new(id, code, "VHF", tipo, "110.30", null, null, null,
            NavaidFieldOrigin.Manual, NavaidFieldOrigin.Manual, NavaidFieldOrigin.Manual,
            new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc), 704798);

    private IRenderedComponent<AdminNavaidsPage> Render(
        INavaidCatalog anagrafica, INavaidImporter importatore, VipiRole livello = VipiRole.Editor)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        Services.AddSingleton(anagrafica);
        Services.AddSingleton(importatore);
        return RenderComponent<AdminNavaidsPage>();
    }

    /// <summary>
    /// ⚠️ Il tasto chiede il giro <b>che rilegge la sorgente</b>. Sul giro notturno leggerebbe la copia in
    /// memoria — vecchia fino a ventiquattro ore — e risponderebbe «0 create, 0 aggiornate» a chi ha appena
    /// aggiunto l'impianto al sectorfile: uno strumento che «funziona» e convince che il dato non c'è.
    /// </summary>
    [Fact]
    public void Il_tasto_chiede_il_giro_che_rilegge_la_sorgente()
    {
        var importatore = new ImportatoreFinto(new NavaidImportReport(new NavaidImportOutcome(2, 1, 146), null, 149));
        var cut = Render(new AnagraficaFinta(DallaSorgente(1, "MNL")), importatore);

        cut.Find("nav.struct-nav button").Click();

        Assert.Equal(1, importatore.Adesso);
        Assert.Equal(0, importatore.Notturni);
        // L'esito dice i numeri, non «fatto»: è l'unica cosa che distingue un giro pieno da uno a vuoto.
        Assert.Contains("Nav_AdminImported 2 1 146 149", cut.Markup);
    }

    /// <summary>
    /// ⚠️ Le due ragioni per cui un giro non è stato fatto <b>non si riassumono</b>: la policy che esclude le
    /// radioassistenze è una decisione (si cambia in Sorgenti), la sorgente muta è un guasto. Detto «non
    /// fatto» e basta, nessuno si accorgerebbe mai che il repository è stato spostato.
    /// </summary>
    [Theory]
    [InlineData(NavaidImportSkip.Esclusa, "Nav_AdminImportExcluded")]
    [InlineData(NavaidImportSkip.SorgenteMuta, "Nav_AdminImportMute")]
    public void Un_giro_saltato_dice_quale_delle_due_ragioni(NavaidImportSkip saltato, string chiave)
    {
        var importatore = new ImportatoreFinto(new NavaidImportReport(null, saltato, 0));
        var cut = Render(new AnagraficaFinta(DallaSorgente(1, "MNL")), importatore);

        cut.Find("nav.struct-nav button").Click();

        Assert.Contains(chiave, cut.Markup);
        Assert.Contains("st-msg warn", cut.Markup);       // non verde: non è andata
    }

    /// <summary>
    /// ⚠️ Sulle righe che manda il sectorfile il cestino <b>non c'è affatto</b>: il giro dopo le ricreerebbe,
    /// e chi l'avesse premuto crederebbe di averle eliminate. Meglio che comparire e rifiutare.
    /// </summary>
    [Fact]
    public void Il_cestino_non_compare_sulle_righe_della_sorgente()
    {
        var cut = Render(
            new AnagraficaFinta(DallaSorgente(1, "MNL"), Nostra(2, "AMD")),
            new ImportatoreFinto(new NavaidImportReport(null, NavaidImportSkip.SorgenteMuta, 0)));

        var righe = cut.FindAll("table.navadm-table tbody tr").ToArray();
        Assert.Equal(2, righe.Length);
        Assert.Empty(righe[0].QuerySelectorAll("td.c-act button"));      // dalla sorgente: nessun cestino
        Assert.NotEmpty(righe[1].QuerySelectorAll("td.c-act button"));   // nostra: si può eliminare
    }

    /// <summary>
    /// Il chip «senza tipo» è la lista di lavoro della pagina: conta le righe che aspettano una persona, e
    /// filtrandolo restano solo quelle. ⚠️ La sorgente il tipo non lo sa — VOR, TACAN e VORTAC stanno nello
    /// stesso file — quindi dopo un import sono la maggioranza.
    /// </summary>
    [Fact]
    public void Il_chip_senza_tipo_conta_e_filtra()
    {
        var cut = Render(
            new AnagraficaFinta(DallaSorgente(1, "MNL"), DallaSorgente(2, "GRO", "VOR"), Nostra(3, "AMD")),
            new ImportatoreFinto(new NavaidImportReport(null, NavaidImportSkip.SorgenteMuta, 0)));

        var chip = cut.Find(".sb-chips .sh-chip");
        Assert.Equal("2", chip.QuerySelector(".chip-n")!.TextContent.Trim());

        chip.Click();

        var codici = cut.FindAll("table.navadm-table tbody td.c-code").Select(c => c.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "MNL", "AMD" }, codici);
    }

    /// <summary>Chi non è Editor non vede la tabella: il rifiuto, non una pagina che non risponde.</summary>
    [Fact]
    public void Sotto_Editor_la_pagina_dice_di_no()
    {
        var cut = Render(
            new AnagraficaFinta(DallaSorgente(1, "MNL")),
            new ImportatoreFinto(new NavaidImportReport(null, NavaidImportSkip.SorgenteMuta, 0)),
            VipiRole.DivisionStaff);

        Assert.Empty(cut.FindAll("table.navadm-table"));
        Assert.Empty(cut.FindAll("nav.struct-nav"));
        Assert.Contains("Vle_Unauthorized", cut.Markup);
    }
}
