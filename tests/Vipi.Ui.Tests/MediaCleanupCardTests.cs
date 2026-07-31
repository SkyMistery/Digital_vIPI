using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Media;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La card di pulizia governa una cancellazione definitiva: quel che conta è che non si possa cancellare senza aver
/// prima guardato l'elenco, e che dopo la cancellazione l'elenco venga riletto dal servizio invece di essere
/// svuotato a mano (se qualcosa è tornato in uso, il servizio non l'ha cancellato e la pagina deve dirlo).
/// </summary>
public class MediaCleanupCardTests : TestContext
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Altro = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeMaintenance : IMediaMaintenance
    {
        public List<OrphanMedia> Orphans { get; set; } = new();
        public int TotalCount { get; set; } = 3;
        public long TotalBytes { get; set; } = 3 * 1024 * 1024;

        public int Analisi { get; private set; }
        public List<string> Cancellati { get; } = new();
        /// Simula «nel frattempo è tornata in uso»: il servizio non la cancella e l'elenco resta.
        public bool RifiutaCancellazione { get; set; }

        public Task<MediaUsageReport> AnalyzeAsync(CancellationToken ct = default)
        {
            Analisi++;
            return Task.FromResult(new MediaUsageReport(TotalCount, TotalBytes, Orphans.ToList()));
        }

        public long DocumentBytes { get; set; }
        public Task<long> DocumentImageBytesAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult(DocumentBytes);

        public Task<int> DeleteOrphansAsync(IReadOnlyList<string> sha256, CancellationToken ct = default)
        {
            if (RifiutaCancellazione) return Task.FromResult(0);
            Cancellati.AddRange(sha256);
            Orphans.RemoveAll(o => sha256.Contains(o.Sha256));
            return Task.FromResult(sha256.Count);
        }
    }

    private readonly FakeMaintenance _servizio = new();

    public MediaCleanupCardTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddScoped<IMediaMaintenance>(_ => _servizio);
    }

    private static OrphanMedia Orfana(string sha, string nome, int byteSize = 820 * 1024) =>
        new(sha, nome, byteSize, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc), 704798);

    [Fact]
    public void Prima_di_analizzare_non_si_puo_cancellare()
    {
        var cut = RenderComponent<MediaCleanupCard>();

        Assert.Contains("MediaClean_Analyze", cut.Markup);
        Assert.DoesNotContain("MediaClean_Delete", cut.Markup);
        Assert.Equal(0, _servizio.Analisi);          // la card non interroga il DB al primo render
    }

    [Fact]
    public void Senza_orfane_niente_pulsante_di_cancellazione()
    {
        var cut = RenderComponent<MediaCleanupCard>();

        cut.Find("button").Click();

        Assert.Contains("MediaClean_NoneUnused", cut.Markup);
        Assert.DoesNotContain("MediaClean_Delete", cut.Markup);
    }

    [Fact]
    public void Con_orfane_mostra_elenco_spazio_e_pulsante()
    {
        _servizio.Orphans.Add(Orfana(Sha, "foto-torre.png"));
        _servizio.Orphans.Add(Orfana(Altro, "schema-hold.png", 640 * 1024));
        var cut = RenderComponent<MediaCleanupCard>();

        cut.Find("button").Click();

        Assert.Contains("foto-torre.png", cut.Markup);
        Assert.Contains("schema-hold.png", cut.Markup);
        Assert.Contains("820 KB", cut.Markup);
        // Solo il conteggio: il separatore decimale dipende dalla cultura in cui gira la pagina.
        Assert.Contains("MediaClean_FoundUnused 2 1", cut.Markup);
        Assert.Contains(" MB", cut.Markup);
        Assert.Contains("MediaClean_Delete", cut.Markup);
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public void Con_una_sola_immagine_il_testo_va_al_singolare()
    {
        // A schermo si leggeva «1 images are not referenced». In una pagina che qualcuno guarda prima di
        // cancellare per sempre, la frase deve essere scritta bene.
        _servizio.TotalCount = 1;
        _servizio.Orphans.Add(Orfana(Sha, "foto-torre.png"));
        var cut = RenderComponent<MediaCleanupCard>();

        cut.Find("button").Click();

        Assert.Contains("MediaClean_Total_One", cut.Markup);
        Assert.Contains("MediaClean_FoundUnused_One", cut.Markup);

        cut.Find("button.danger").Click();
        Assert.Contains("MediaClean_DeletePrompt_One", cut.Markup);
        cut.Find("span.inline-confirm button.danger").Click();
        Assert.Contains("MediaClean_Deleted_One", cut.Markup);
    }

    [Fact]
    public void Ogni_riga_mostra_l_anteprima_della_foto()
    {
        // Davanti a un nome come «immagine1.png» nessuno sa se quella foto serviva: la miniatura e' il motivo per
        // cui l'elenco si guarda prima di cancellare.
        _servizio.Orphans.Add(Orfana(Sha, "foto-torre.png"));
        var cut = RenderComponent<MediaCleanupCard>();

        cut.Find("button").Click();

        var img = cut.Find("tbody tr img.img-thumb");
        Assert.Equal("/vsop/media/" + Sha, img.GetAttribute("src"));
        Assert.Equal("", img.GetAttribute("alt"));   // decorativa: il nome del file e' gia' nella colonna accanto
    }

    [Fact]
    public void Un_asset_senza_nome_file_si_riconosce_dallo_sha()
    {
        _servizio.Orphans.Add(Orfana(Sha, nome: null!));
        var cut = RenderComponent<MediaCleanupCard>();

        cut.Find("button").Click();

        Assert.Contains(Sha[..12], cut.Markup);
    }

    [Fact]
    public void La_cancellazione_chiede_conferma_e_solo_dopo_cancella()
    {
        _servizio.Orphans.Add(Orfana(Sha, "foto-torre.png"));
        var cut = RenderComponent<MediaCleanupCard>();
        cut.Find("button").Click();

        // Il primo click apre la domanda: niente è ancora stato cancellato.
        cut.Find("button.danger").Click();
        Assert.Empty(_servizio.Cancellati);
        Assert.Contains("MediaClean_DeletePrompt_One", cut.Markup);

        cut.Find("span.inline-confirm button.danger").Click();
        Assert.Equal(new[] { Sha }, _servizio.Cancellati);
        Assert.Contains("MediaClean_Deleted_One", cut.Markup);
    }

    [Fact]
    public void Dopo_la_cancellazione_l_elenco_si_rilegge_dal_servizio()
    {
        // Se una foto è tornata in uso fra l'elenco e il clic, il servizio non la cancella: la pagina deve
        // continuare a mostrarla invece di fingere che sia sparita.
        _servizio.Orphans.Add(Orfana(Sha, "foto-torre.png"));
        _servizio.RifiutaCancellazione = true;
        var cut = RenderComponent<MediaCleanupCard>();
        cut.Find("button").Click();

        cut.Find("button.danger").Click();
        cut.Find("span.inline-confirm button.danger").Click();

        Assert.Equal(2, _servizio.Analisi);                  // una prima, una dopo
        Assert.Contains("foto-torre.png", cut.Markup);       // ancora lì
        Assert.Contains("MediaClean_Deleted 0", cut.Markup);
    }
}
