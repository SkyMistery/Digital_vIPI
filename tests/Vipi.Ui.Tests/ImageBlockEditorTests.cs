using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Application.Media;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'editor del blocco immagine è scritto UNA volta e usato dai due editor di blocchi: se si rompe qui, si rompe
/// ovunque si possano aggiungere paragrafi, callout e tabelle. Copre il giro di caricamento (con il ripiego sul file
/// originale quando il browser non può rimpicciolire) e il rifiuto, che deve restare un messaggio nella pagina — non
/// un'eccezione che abbatte il circuito.
/// </summary>
public class ImageBlockEditorTests : TestContext
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeMediaStore : IMediaStore
    {
        public string? Rejection { get; set; }
        public int Saves { get; private set; }
        public long BytesReceived { get; private set; }

        public async Task<StoredMedia> SaveAsync(Stream content, string? originalFileName, CancellationToken ct = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            BytesReceived = buffer.Length;
            if (Rejection is not null) throw new ValidationException(Rejection);
            Saves++;
            return new StoredMedia(Sha, "image/png", 1600, 900, (int)buffer.Length);
        }

        public Task<MediaContent?> GetAsync(string sha256, CancellationToken ct = default) =>
            Task.FromResult<MediaContent?>(null);
    }

    private sealed class FakeMaintenance : IMediaMaintenance
    {
        public long DocumentBytes { get; set; }
        public Task<MediaUsageReport> AnalyzeAsync(CancellationToken ct = default) =>
            Task.FromResult(new MediaUsageReport(0, 0, Array.Empty<OrphanMedia>()));
        public Task<int> DeleteOrphansAsync(IReadOnlyList<string> sha256, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task<long> DocumentImageBytesAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult(DocumentBytes);
    }

    private readonly FakeMediaStore _store = new();
    private readonly FakeMaintenance _manutenzione = new();

    public ImageBlockEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IMediaStore>(_ => _store);
        Services.AddScoped<IMediaMaintenance>(_ => _manutenzione);
        Services.AddSingleton<IOptions<MediaOptions>>(Options.Create(
            new MediaOptions { MaxUploadBytes = 3 * 1024 * 1024, MaxBytesPerDocument = 1024 }));
        // Niente browser: vipiMedia.osserva non aggancia nulla e il file arriva a .NET come l'ha scelto l'utente.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<ImageBlockEditor> Render(string? imageJson, Action<string?>? onJson = null, Action<string?>? onCaption = null) =>
        RenderComponent<ImageBlockEditor>(p => p
            .Add(x => x.ImageJson, imageJson)
            .Add(x => x.ImageJsonChanged, j => onJson?.Invoke(j))
            .Add(x => x.CaptionChanged, c => onCaption?.Invoke(c)));

    [Fact]
    public void Senza_immagine_mostra_area_di_caricamento_e_il_limite()
    {
        var cut = Render(null);

        Assert.Single(cut.FindAll("label.img-drop input[type=file]"));
        Assert.Single(cut.FindAll("figure.img-ph"));      // segnaposto, non un'immagine rotta
        Assert.Contains("Img_MaxSize 3 MB", cut.Markup);  // il limite mostrato viene dall'opzione, non da un letterale
        Assert.Empty(cut.FindAll(".img-fields"));         // niente alt/didascalia finché non c'è la foto
    }

    [Fact]
    public void Con_immagine_mostra_figura_alt_e_didascalia()
    {
        var cut = Render(MediaRef.Serialize(new MediaRef(Sha, "Torre", 1600, 900)));

        Assert.Equal("/vsop/media/" + Sha, cut.Find("figure.doc-img img").GetAttribute("src"));
        Assert.Equal("Torre", cut.Find(".img-fields input").GetAttribute("value"));   // il primo campo è l'alt
    }

    [Fact]
    public void Caricare_un_file_salva_e_restituisce_il_riferimento_all_host()
    {
        string? json = null;
        var cut = Render(null, onJson: j => json = j);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("byte-di-prova", "foto.png"));

        Assert.Equal(1, _store.Saves);
        Assert.Equal(Sha, MediaRef.Parse(json)!.MediaId);
        Assert.Equal(1600, MediaRef.Parse(json)!.Width);   // le misure arrivano dal deposito, non dal client
    }

    [Fact]
    public void Un_file_rifiutato_diventa_un_messaggio_non_un_circuito_caduto()
    {
        _store.Rejection = "L'immagine pesa 8 MB: il limite è 3 MB.";
        string? json = null;
        var cut = Render(null, onJson: j => json = j);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("troppo grande", "grande.png"));

        Assert.Contains("il limite è 3 MB", cut.Markup);
        Assert.Null(json);                                  // niente riferimento salvato
    }

    [Fact]
    public void Rimuovere_l_immagine_manda_all_host_la_stringa_vuota()
    {
        // Vuoto, non null: null per l'host significa «non toccare». I byte restano nel deposito, perché una
        // release gia' pubblicata continua a citarne lo sha.
        string? json = "non toccato";
        var cut = Render(MediaRef.Serialize(new MediaRef(Sha)), onJson: j => json = j);

        cut.Find(".img-fields button").Click();

        Assert.Equal("", json);
    }

    [Fact]
    public void Cambiare_alt_conserva_lo_stesso_sha()
    {
        string? json = null;
        var cut = Render(MediaRef.Serialize(new MediaRef(Sha, "vecchio", 800, 600)), onJson: j => json = j);

        cut.Find(".img-fields input").Change("nuovo testo alternativo");

        var media = MediaRef.Parse(json)!;
        Assert.Equal(Sha, media.MediaId);
        Assert.Equal("nuovo testo alternativo", media.Alt);
        Assert.Equal(800, media.Width);
    }

    [Fact]
    public void Sostituire_l_immagine_non_eredita_l_alt_della_precedente()
    {
        // L'alt descriveva un'altra foto: tenerlo sarebbe una descrizione falsa per chi non vede l'immagine.
        string? json = null;
        var cut = Render(MediaRef.Serialize(new MediaRef(Sha, "vecchia descrizione", 800, 600)), onJson: j => json = j);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("nuovo contenuto", "nuova.png"));

        Assert.Null(MediaRef.Parse(json)!.Alt);
    }

    // --- quota per documento ---

    [Fact]
    public void Con_la_quota_esaurita_il_caricamento_viene_rifiutato_con_i_due_numeri()
    {
        _manutenzione.DocumentBytes = 1000;                       // quota 1024: resta quasi niente
        string? json = null;
        var cut = RenderComponent<ImageBlockEditor>(p => p
            .Add(x => x.DocumentId, 42)
            .Add(x => x.ImageJsonChanged, j => json = j));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(new string('x', 500), "foto.png"));

        Assert.Contains("occupano gia", cut.Markup);
        Assert.Contains("1 KB", cut.Markup);                      // il tetto, letto dall'opzione
        Assert.Null(json);                                        // niente riferimento salvato
        Assert.Equal(0, _store.Saves);                            // e niente asset orfano nel deposito
    }

    [Fact]
    public void Senza_documento_la_quota_non_si_applica()
    {
        // Blocchi che vivono in memoria prima di finire in un documento: non c'e' quota a cui riferirsi.
        _manutenzione.DocumentBytes = 999999;
        string? json = null;
        var cut = RenderComponent<ImageBlockEditor>(p => p.Add(x => x.ImageJsonChanged, j => json = j));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("piccola", "foto.png"));

        Assert.Equal(1, _store.Saves);
        Assert.NotNull(json);
    }
}
