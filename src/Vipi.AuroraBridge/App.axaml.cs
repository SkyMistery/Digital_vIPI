using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge;

public partial class App : Application
{
    private AuroraClient? _client;
    private VipiApiClient? _api;
    private CancellationTokenSource? _loop;
    private WindowsGlobalHotkey? _hotkey;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = BridgeSettings.Load();

            _client = new AuroraClient();
            _api = new VipiApiClient(new VipiApiOptions(BaseAddress: settings.SiteUrl));
            var orchestrator = new BridgeOrchestrator(
                new AuroraSession(_client), _api, settings.ToPollingOptions(), settings.OwnerOverride);

            var log = new BridgeLog();
            var model = new BridgeViewModel(orchestrator, settings, log)
            {
                // Gli eventi dell'orchestratore arrivano dal thread di polling: la UI si tocca solo dal suo.
                Post = action => Dispatcher.UIThread.Post(action),
            };

            desktop.MainWindow = new MainWindow(model, orchestrator) { Topmost = settings.AlwaysOnTop };

            _loop = new CancellationTokenSource();
            _ = orchestrator.RunAsync(_loop.Token);

            RegisterHotkey(settings, model, log);
            log.Write($"Avvio. Sito {settings.SiteUrl}, postazione {settings.OwnerOverride ?? "da #CONN"}.");

            desktop.ShutdownRequested += (_, _) =>
            {
                _loop.Cancel();
                settings.Save();
                _hotkey?.Dispose();
                _api?.Dispose();
                _ = _client?.DisposeAsync();
                log.Write("Chiusura.");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Registra la combinazione globale, se configurata. L'esito (anche negativo: la combinazione può
    /// essere già presa) finisce nel registro e nella finestra, perché una scorciatoia che non risponde e non
    /// spiega è peggio che non averla.</summary>
    private void RegisterHotkey(BridgeSettings settings, BridgeViewModel model, BridgeLog log)
    {
        var spec = settings.ResolveHotkey();
        if (spec is null)
        {
            if (settings.HotkeyEnabled && !string.IsNullOrWhiteSpace(settings.Hotkey))
                model.Notify($"Combinazione «{settings.Hotkey}» non interpretabile: serve almeno un modificatore, es. Ctrl+Alt+L.");
            return;
        }

        _hotkey = new WindowsGlobalHotkey();
        _hotkey.TryRegister(spec, () => Dispatcher.UIThread.Post(async () => await model.WriteBestAsync()));

        log.Write(_hotkey.Status ?? "Combinazione non registrata.");
        if (!_hotkey.IsRegistered && _hotkey.Status is not null) model.Notify(_hotkey.Status);
    }
}
