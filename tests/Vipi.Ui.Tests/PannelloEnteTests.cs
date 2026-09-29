using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il pannello «Ente» dell'editor della vIPI APP (revisione degli enti ATC, S52).
/// <list type="bullet">
///   <item>🔴 Un caricamento fallito faceva sparire il pannello senza una parola: l'errore stava dentro il ramo
///     dell'ente, che senza ente non si disegnava.</item>
///   <item>🔴 Il lock scaduto o preso da un altro arrivava come «errore imprevisto» col messaggio in coda.</item>
///   <item>Togliendo una posizione ancora citata dal documento, il pannello lo dice.</item>
/// </list>
/// </summary>
public class PannelloEnteTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join("|", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static readonly AtcUnitRow Pratica =
        new(7, "LIRE_APP", "Pratica Tower", "LIRR", AtcUnitMode.OwnDocument, 42, new[] { "LIRE_TWR", "LIRE_APP" });

    private sealed class EntiFinti : IAtcUnitService
    {
        public Exception? LanciaFind { get; set; }
        public Exception? LanciaTogli { get; set; }
        public Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default) =>
            LanciaFind is null ? Task.FromResult<AtcUnitRow?>(Pratica) : Task.FromException<AtcUnitRow?>(LanciaFind);
        public Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default) =>
            LanciaTogli is null ? Task.CompletedTask : Task.FromException(LanciaTogli);
        public Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default) => Task.CompletedTask;
        public Task RenameAsync(int unitId, string name, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SpostamentiFinti : IRemotizzazioneService
    {
        public Task<RemotizzazioneEsito> RemotizzaAsync(int unitId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> SpostamentoInCorsoAsync(int unitId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<int> ConcludiSpostamentiAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyDictionary<int, string>> SpostamentiInCorsoAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }

    private readonly EntiFinti _enti = new();

    private IRenderedComponent<AtcUnitPanel> Apri(bool inModifica, IAppDocumentService? app = null)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IAtcUnitService>(_enti);
        Services.AddSingleton<IRemotizzazioneService>(new SpostamentiFinti());
        Services.AddSingleton(app ?? ServizioVuoto.Di<IAppDocumentService>());
        return RenderComponent<AtcUnitPanel>(p => p.Add(x => x.Key, "LIRE_APP").Add(x => x.IsEditing, inModifica));
    }

    [Fact]
    public void Un_caricamento_fallito_si_vede()
    {
        _enti.LanciaFind = new InvalidOperationException("database giù");

        var cut = Apri(inModifica: false);

        cut.WaitForAssertion(() => Assert.Contains("database giù", cut.Markup));
    }

    [Fact]
    public async Task Il_lock_perso_si_legge_come_tale()
    {
        _enti.LanciaTogli = new EditConflictException("Lock scaduto o preso da un altro editor.");
        var cut = Apri(inModifica: true);
        cut.WaitForAssertion(() => Assert.Contains("LIRE_TWR", cut.Markup));

        await Togli(cut, "LIRE_APP");

        cut.WaitForAssertion(() => Assert.Contains("Lock scaduto o preso da un altro editor.", cut.Markup));
        Assert.DoesNotContain("Common_UnexpectedError", cut.Markup);
    }

    [Fact]
    public async Task Togliendo_una_posizione_ancora_citata_lo_dice()
    {
        var app = new AppCitazioni();
        var cut = Apri(inModifica: true, app);
        cut.WaitForAssertion(() => Assert.Contains("LIRE_TWR", cut.Markup));

        await Togli(cut, "LIRE_APP");

        cut.WaitForAssertion(() => Assert.Contains("Unit_StillCited:LIRE_APP|una configurazione", cut.Markup));
        Assert.Equal(("LIRE_APP", "LIRE_APP"), app.Chiesto);
    }

    /// <summary>La conferma in linea: il ✕ della riga, poi il tasto di conferma.</summary>
    private static async Task Togli(IRenderedComponent<AtcUnitPanel> cut, string cs)
    {
        var riga = cut.FindAll("li").First(li => li.TextContent.Contains(cs));
        await riga.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "✕").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Unit_RemoveYes"));
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Unit_RemoveYes").ClickAsync(new());
    }

    /// <summary>Solo la domanda «dove è citata»; il resto non serve al pannello.</summary>
    private sealed class AppCitazioni : IAppDocumentService
    {
        public (string, string)? Chiesto { get; private set; }
        public Task<IReadOnlyList<string>> WhereCitedAsync(string appCallsign, string callsign, CancellationToken ct = default)
        {
            Chiesto = (appCallsign, callsign);
            return Task.FromResult<IReadOnlyList<string>>(new[] { "una configurazione" });
        }

        public Task<int> EnsureAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AppCoordination> DeriveCoordinationAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AccAorView> GetAorViewAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AccAorView> GetAorViewAsync(string a, AorExtraShapes c, IReadOnlyList<AccConfiguration> g, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MinimaView> DeriveMinimaAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AorExtraShapes> GetAorCustomizationAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAorCustomizationAsync(string a, AorExtraShapes d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AppSeparationRow>> GetSeparationsAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveSeparationsAsync(string a, IReadOnlyList<AppSeparationRow> r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AppDocumentIdentity?> GetIdentityAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentProfileData> GetOverridesAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveFrequencyOrderAsync(string a, IReadOnlyList<AppFreqOrderOverride> o, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveFrequencyLinksAsync(string a, IReadOnlyList<int> s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccSectorPick>> ListSectorsAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccConfiguration>> GetConfigurationsAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveConfigurationsAsync(string a, IReadOnlyList<AccConfiguration> c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string a, IReadOnlyList<AccConfiguration> c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RegulatedSelection> GetRegulatedAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveRegulatedAsync(string a, RegulatedSelection s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string a, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(RegulatedSelection s, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
