using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il pannello dell'unione nell'editor (revisione totale 3, fetta D di L11).
/// <list type="bullet">
///   <item>🔴 U-070: se <c>IsEditing</c> cambiava due volte di fila (entra in modifica, esce), il secondo ricarico
///     partiva con il primo ancora in volo sullo stesso DbContext: «A second operation», circuito giù.</item>
///   <item>🔴 U-169: un gesto prendeva solo tre tipi di eccezione; un guasto del database (o il tornello che non dà
///     il turno) usciva dal gestore.</item>
/// </list>
/// </summary>
public class PannelloUnioneUnGiroAllaVoltaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static UnionMemberView Membro(int id, string chiave) =>
        new(MemberId: id, Order: id, new ManagedDoc(ReleaseTargetType.App, chiave, chiave, "LIBB", IsPublished: true,
            HasDraft: false, IsHidden: false, ReleaseTargetType.App, chiave, id));

    /// <summary>Il servizio delle unioni, con un contatore delle operazioni sovrapposte e una lettura trattenibile.</summary>
    private sealed class UnioniContate : IDocumentUnionService
    {
        private int _dentro;
        public int MassimoInsieme { get; private set; }
        public TaskCompletionSource? Trattieni { get; set; }
        public UnionView? Unione { get; set; }
        public Exception? LanciaSposta { get; set; }

        private async Task<T> Dentro<T>(Func<Task<T>> lavoro)
        {
            var ora = Interlocked.Increment(ref _dentro);
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try { return await lavoro(); }
            finally { Interlocked.Decrement(ref _dentro); }
        }

        public Task<UnionView?> ForTargetAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Dentro(async () =>
            {
                if (Trattieni is not null) await Trattieni.Task;
                else await Task.Delay(10);
                return Unione;
            });

        public Task<IReadOnlyList<UnionCandidate>> CandidatiAsync(int documentId, CancellationToken ct = default) =>
            Dentro(async () => { await Task.Delay(10); return (IReadOnlyList<UnionCandidate>)Array.Empty<UnionCandidate>(); });

        public Task<IReadOnlyList<UnionCandidate>> CandidatiPerTargetAsync(ReleaseTargetType type, string key,
            CancellationToken ct = default) =>
            Dentro(async () => { await Task.Delay(10); return (IReadOnlyList<UnionCandidate>)Array.Empty<UnionCandidate>(); });

        public Task SpostaAsync(int memberId, int delta, CancellationToken ct = default) =>
            LanciaSposta is null ? Task.CompletedTask : Task.FromException(LanciaSposta);

        public Task<UnionView?> ForDocumentAsync(int documentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> UniscoAsync(int invitanteDocumentId, int invitatoDocumentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RimuoviMembroAsync(int memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SciogliAsync(int unionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> DocumentIdAsync(ReleaseTargetType type, string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Vipi.Application.Abstractions.UnionRow>> TutteAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private readonly UnioniContate _unioni = new();

    private IRenderedComponent<UnionPanel> Apri(bool inModifica)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IDocumentUnionService>(_unioni);
        Services.AddSingleton(ServizioVuoto.Di<IEditingService>());
        return RenderComponent<UnionPanel>(p => p
            .Add(x => x.Target, ReleaseTargetType.App)
            .Add(x => x.Key, "LIBV_APP")
            .Add(x => x.IsEditing, inModifica));
    }

    [Fact]
    public async Task Due_cambi_di_modalita_ravvicinati_non_sovrappongono_due_ricarichi()
    {
        _unioni.Trattieni = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Apri(inModifica: false);

        // Il primo ricarico è in volo: entra in modifica, e subito esce.
        var entra = cut.InvokeAsync(() => cut.SetParametersAndRender(p => p.Add(x => x.IsEditing, true)));
        var esce = cut.InvokeAsync(() => cut.SetParametersAndRender(p => p.Add(x => x.IsEditing, false)));
        await Task.Delay(50);
        _unioni.Trattieni.SetResult();
        await Task.WhenAll(entra, esce);

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        Assert.Equal(1, _unioni.MassimoInsieme);
    }

    [Fact]
    public async Task Un_guasto_imprevisto_di_un_gesto_resta_un_messaggio()
    {
        _unioni.Unione = new UnionView(1, new[] { Membro(3, "LIBV_APP"), Membro(5, "LIBV_G_APP") });
        _unioni.LanciaSposta = new InvalidOperationException("turno non arrivato dopo 30 s");
        var cut = Apri(inModifica: true);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "↓"));

        await cut.FindAll("button").First(b => b.TextContent.Trim() == "↓").ClickAsync(new());

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        cut.WaitForAssertion(() => Assert.Contains("turno non arrivato dopo 30 s", cut.Markup));
    }
}
