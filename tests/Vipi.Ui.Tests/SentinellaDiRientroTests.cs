using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>La sentinella di rientro: la porta che protegge da SÉ STESSI.</b>
///
/// <para>Su una pagina interattiva un gestore <c>async</c> cede il controllo al primo <c>await</c>: il gesto
/// successivo dell'utente parte <b>mentre</b> il primo è ancora in volo. Se tutti e due toccano il
/// <c>DbContext</c> — che su un circuito Blazor è uno solo e vive per ore — il secondo lo trova occupato e
/// la pagina muore con «A second operation was started on this context instance».</para>
///
/// <para>⚠️ <b>Lo scope proprio non basta.</b> <c>OwningComponentBase</c> dà alla pagina un contesto suo, e
/// così nessun ALTRO le passa davanti; ma due letture della stessa pagina restano due letture sullo stesso
/// contesto. Le due porte servono a due cose diverse, e questa serve per la seconda (revisione del
/// 6 settembre 2026, R-016).</para>
///
/// <para>Il banco è l'archivio mondiale perché è la pagina dove la corsa è più facile da fare per davvero:
/// la ricerca parte da sola all'apertura, e il tasto «Cerca» è lì accanto.</para>
/// </summary>
public class SentinellaDiRientroTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join(",", arguments), false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private sealed class AuthzStaff : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.DivisionStaff;
        public bool IsAdmin => false;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Chi guarda";
    }

    /// <summary>
    /// Un archivio che <b>non risponde</b> alla prima chiamata finché non glielo si dice: è il modo di tenere
    /// una lettura in volo mentre ne parte un'altra, che a mano non si riesce a cronometrare.
    /// </summary>
    private sealed class ArchivioAppeso : IAtcArchiveQueries
    {
        private readonly TaskCompletionSource<AtcArchivePage> _prima = new();

        public int Chiamate { get; private set; }

        public Task<AtcArchivePage> SearchAsync(AtcArchiveFilter filter, CancellationToken ct = default)
        {
            Chiamate++;
            return _prima.Task;
        }

        public void Rispondi() =>
            _prima.TrySetResult(new AtcArchivePage(Array.Empty<AtcArchiveRow>(), 0));
    }

    private readonly ArchivioAppeso _archivio = new();

    private IRenderedComponent<AtcWorldArchivePage> Render()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new AuthzStaff());
        Services.AddSingleton<IAtcArchiveQueries>(_archivio);
        return RenderComponent<AtcWorldArchivePage>();
    }

    /// <summary>
    /// ⚠️ Il secondo gesto, con il primo ancora in volo, <b>non</b> deve produrre una seconda lettura: è
    /// esattamente la coppia che sullo stesso <c>DbContext</c> uccide la pagina.
    /// </summary>
    [Fact]
    public void Un_secondo_gesto_con_la_lettura_in_volo_non_ne_fa_partire_un_altra()
    {
        var cut = Render();

        // La ricerca d'apertura è partita e sta aspettando: una sola.
        Assert.Equal(1, _archivio.Chiamate);

        // Il tasto «Cerca» mentre quella è ancora appesa.
        cut.Find("button.btn.primary").Click();
        cut.Find("button.btn.primary").Click();

        Assert.Equal(1, _archivio.Chiamate);
    }

    /// <summary>
    /// La controprova, che conta quanto l'altra: la sentinella <b>si abbassa</b>. Una guardia che non
    /// riapre la porta non è una guardia, è una pagina che smette di cercare dopo la prima volta.
    /// </summary>
    [Fact]
    public void Finita_la_lettura_il_gesto_successivo_riparte()
    {
        var cut = Render();
        _archivio.Rispondi();
        cut.WaitForState(() => _archivio.Chiamate == 1);

        cut.Find("button.btn.primary").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, _archivio.Chiamate));
    }
}
