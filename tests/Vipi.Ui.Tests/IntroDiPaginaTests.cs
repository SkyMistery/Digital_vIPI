using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La zona d'intro in cima a una pagina (carta <c>2026-08-30-intro-di-pagina.md</c>).
///
/// <para>Presidia le due cose che, sbagliate, si vedrebbero solo in produzione: che un'intro <b>vuota</b> non
/// lasci un contenitore in cima a una pagina pubblica, e che quel che si legge sia <b>tradotto</b> — che è la
/// richiesta esplicita del SOD, e cadrebbe in silenzio perché una frase non tradotta si legge lo stesso, in
/// italiano.</para>
/// </summary>
public class IntroDiPaginaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Un lettore qualunque: non è staff, quindi vede solo quel che è pubblico.</summary>
    private sealed class Pubblico : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.User;
        public bool IsAdmin => false;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
    }

    /// <summary>Chi ha il permesso di scrivere: serve alle prove sull'editor.</summary>
    private sealed class Editore : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
    }

    /// <summary>Un lock già nostro: la zona si apre in modifica senza passare da «Inizia».</summary>
    private sealed class LockFinto : IResourceLockService
    {
        private static LockInfo Mio => new() { Locked = true, IsMine = true, ByUserId = 1, ByName = "test" };
        public Task<LockInfo> InspectAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> AcquireAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> HeartbeatAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task ReleaseAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task ForceUnlockAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureHeldAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class DepositoFinto : IPageIntroStore
    {
        private readonly List<PageIntroSection> _sezioni;
        public DepositoFinto(params PageIntroSection[] sezioni) => _sezioni = sezioni.ToList();

        /// <summary>Quante volte è stato chiesto di salvare.</summary>
        public int Salvataggi { get; private set; }

        public Task<IReadOnlyList<PageIntroSection>> LeggiAsync(string pagina, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PageIntroSection>>(_sezioni);

        public Task SalvaAsync(string pagina, IReadOnlyList<PageIntroSection> sezioni, string etichetta,
            CancellationToken ct = default)
        {
            Salvataggi++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Una memoria che sa quel che le si è detto, e niente rete.</summary>
    private sealed class MemoriaFinta : ITranslationMemory
    {
        private readonly Dictionary<string, KnownTranslation> _note = new(StringComparer.Ordinal);

        public MemoriaFinta Nota(string sorgente, string bersaglio)
        {
            _note[TranslationText.Hash(sorgente)] =
                new KnownTranslation(bersaglio, TranslationOrigin.Machine, false);
            return this;
        }

        public Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
            string s, string t, IReadOnlyCollection<string> hashes, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, KnownTranslation>>(
                hashes.Where(_note.ContainsKey).ToDictionary(h => h, h => _note[h], StringComparer.Ordinal));

        public Task<int> SaveMachineAsync(string s, string t, string e,
            IReadOnlyList<(string SourceText, string TargetText)> v, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveHumanAsync(string s, string t, string a, string b, int u, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
            string s, string t, TranslationOrigin? origine, int limite, string? cerca = null, int salta = 0,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TranslationReviewRow>>(Array.Empty<TranslationReviewRow>());
        public Task<int> ContaPerRevisioneAsync(
            string s, string t, TranslationOrigin? origine, string? cerca = null, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task<IReadOnlyDictionary<string, IReadOnlyList<UsoInDocumento>>> DoveSiUsanoAsync(
            IReadOnlyCollection<string> frasi, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<UsoInDocumento>>>(
                new Dictionary<string, IReadOnlyList<UsoInDocumento>>());
        public Task<IReadOnlyList<FraseConFormula>> FrasiConLaFormulaAsync(
            string s, string t, string formula, int limite, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FraseConFormula>>(Array.Empty<FraseConFormula>());
        public Task<IReadOnlyDictionary<string, int>> ContaFrasiPerFormuleAsync(
            string s, string t, IReadOnlyCollection<string> formule, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
        public Task<IReadOnlyDictionary<string, string>> LoadAllAsync(string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.Ordinal));
        public Task<IReadOnlySet<string>> LoadHumanHashesAsync(string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        public Task<(int Totale, int DaRileggere)> ContaAsync(string s, string t, CancellationToken ct = default) =>
            Task.FromResult((0, 0));
        public Task<int> DocumentiToccatiAsync(string s, CancellationToken ct = default) => Task.FromResult(0);
        public Task<long> CaratteriSpesiAsync(string e, CancellationToken ct = default) => Task.FromResult(0L);
        public Task RegistraSpesaAsync(string e, string s, string t, long c, int seg, int sc, long csc,
            DateTime now, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> FotografaSpesaPregressaAsync(
            IReadOnlyList<string> engines, DateTime now, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> ContaConLaFormulaAsync(string s, string t, string f, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task<int> DimenticaAutomaticheConLaFormulaAsync(string s, string t, string f, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private static PageIntroSection Sezione(string titolo, string testo) => new()
    {
        Title = titolo,
        Blocks = new List<ExtraBlock> { new() { Format = BlockFormat.Prose, Text = testo } },
    };

    private void Monta(IPageIntroStore deposito, ITranslationMemory? memoria = null)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Pubblico());
        Services.AddScoped(_ => deposito);
        Services.AddScoped(_ => new DocumentTranslator(memoria ?? new MemoriaFinta()));
    }

    /// <summary>⚠️ Vuota, per il pubblico non si rende NIENTE — nemmeno un contenitore. Un riquadro vuoto in
    /// cima a un elenco è spazio tolto a quel che si è venuti a leggere.</summary>
    [Fact]
    public void Senza_sezioni_il_pubblico_non_vede_nemmeno_il_contenitore()
    {
        Monta(new DepositoFinto());

        var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));

        Assert.DoesNotContain("page-intro", cut.Markup);
    }

    [Fact]
    public void Le_sezioni_si_leggono_in_cima_alla_pagina()
    {
        Monta(new DepositoFinto(Sezione("Documenti generali", "Leggere prima di controllare.")));

        var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));

        Assert.Contains("Documenti generali", cut.Markup);
        Assert.Contains("Leggere prima di controllare.", cut.Markup);
    }

    /// <summary>⚠️ È la richiesta del SOD, e cade in silenzio: una frase non tradotta si legge lo stesso, in
    /// italiano, e nessun avviso lo dice a chi guarda la pagina inglese.</summary>
    [Fact]
    public void Chi_legge_in_inglese_vede_l_intro_in_inglese()
    {
        var memoria = new MemoriaFinta()
            .Nota("Documenti generali", "General documents")
            .Nota("Leggere prima di controllare.", "Read before controlling.");
        Monta(new DepositoFinto(Sezione("Documenti generali", "Leggere prima di controllare.")), memoria);

        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));

            Assert.Contains("General documents", cut.Markup);
            Assert.Contains("Read before controlling.", cut.Markup);
            Assert.DoesNotContain("Leggere prima di controllare.", cut.Markup);
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }

    // ---- L'editor: quel che non è salvato si vede, e uscendo non si perde ---------------------------

    private DepositoFinto MontaEditor(params PageIntroSection[] sezioni)
    {
        var deposito = new DepositoFinto(sezioni);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Editore());
        Services.AddScoped<IResourceLockService>(_ => new LockFinto());
        Services.AddScoped<IPageIntroStore>(_ => deposito);
        Services.AddScoped(_ => new DocumentTranslator(new MemoriaFinta()));
        return deposito;
    }

    private static void Clicca(IRenderedComponent<PageIntroZone> cut, string testo) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(testo)).Click();

    /// <summary>⚠️ Un tasto «Salva» spento non è un avviso: dice che non c'è niente da salvare, non il
    /// contrario. Finché lo stato non si vede, chi scrive non ha modo di sapere che deve salvare.</summary>
    [Fact]
    public void Quel_che_non_e_salvato_si_vede()
    {
        MontaEditor();
        var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));

        Assert.DoesNotContain("PageIntro_Unsaved", cut.Markup);

        Clicca(cut, "PageIntro_AddSection");

        Assert.Contains("PageIntro_Unsaved", cut.Markup);
    }

    /// <summary>⚠️ Il difetto che questo chiude: «Fine modifica» è la strada naturale per «ho finito», e
    /// prima buttava via tutto <b>in silenzio</b> — il rilascio del lock faceva rileggere da archivio, e la
    /// pagina tornava com'era senza dire niente.</summary>
    [Fact]
    public void Fine_modifica_salva_invece_di_buttare_via()
    {
        var deposito = MontaEditor();
        var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));
        Clicca(cut, "PageIntro_AddSection");

        Clicca(cut, "Lock_FinishEdit");

        Assert.Equal(1, deposito.Salvataggi);
    }

    /// <summary>Senza modifiche non si scrive niente: uscire da una lettura non deve toccare l'archivio.</summary>
    [Fact]
    public void Fine_modifica_senza_modifiche_non_salva()
    {
        var deposito = MontaEditor();
        var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));

        Clicca(cut, "Lock_FinishEdit");

        Assert.Equal(0, deposito.Salvataggi);
    }

    /// <summary>Quel che la memoria non copre resta nella lingua d'origine: un'intro a chiazze si legge male
    /// ma si legge, una coi buchi mente.</summary>
    [Fact]
    public void Quel_che_manca_resta_in_italiano()
    {
        var memoria = new MemoriaFinta().Nota("Documenti generali", "General documents");
        Monta(new DepositoFinto(Sezione("Documenti generali", "Questa frase non la ha tradotta nessuno.")), memoria);

        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var cut = RenderComponent<PageIntroZone>(p => p.Add(x => x.Pagina, "mil"));

            Assert.Contains("General documents", cut.Markup);
            Assert.Contains("Questa frase non la ha tradotta nessuno.", cut.Markup);
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }
}
