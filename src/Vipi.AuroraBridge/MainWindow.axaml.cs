using Avalonia.Controls;
using Avalonia.Interactivity;
using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge;

/// <summary>
/// Finestra unica del tool. Deliberatamente senza logica: legge dal <see cref="BridgeViewModel"/> e gli
/// rimanda i gesti dell'utente. Ogni decisione (cosa si può scrivere, cosa mostrare) sta nel modello, in Core.
/// </summary>
public partial class MainWindow : Window
{
    private readonly BridgeViewModel _model;
    private readonly BridgeOrchestrator _orchestrator;

    // Il costruttore senza argomenti serve solo all'anteprima del designer XAML.
    public MainWindow() : this(null!, null!) { }

    public MainWindow(BridgeViewModel model, BridgeOrchestrator orchestrator)
    {
        // Generato dal compilatore XAML: è QUESTO a creare i campi dei controlli con x:Name. Riscriverlo a mano
        // (AvaloniaXamlLoader.Load) fa compilare tutto ma lascia i campi a null, e la finestra muore al primo uso.
        InitializeComponent();

        _model = model;
        _orchestrator = orchestrator;
        DataContext = model;

        if (model is null) return;

        SiteBox.Text = model.Settings.SiteUrl;
        OwnerBox.Text = model.Settings.OwnerOverride ?? "";
        HotkeyBox.Text = model.Settings.Hotkey ?? "";
        HotkeyEnabledBox.IsChecked = model.Settings.HotkeyEnabled;
        PinButton.IsChecked = model.Settings.AlwaysOnTop;

        model.Apply(orchestrator.Current);
    }

    private async void OnRefresh(object? sender, RoutedEventArgs e) => await _model.RefreshAsync();

    private async void OnClear(object? sender, RoutedEventArgs e) => await _model.ClearAsync();

    private async void OnWrite(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CandidateRow row })
            await _model.WriteAsync(row);
    }

    private void OnTogglePin(object? sender, RoutedEventArgs e)
    {
        Topmost = PinButton.IsChecked == true;
        _model.Settings.AlwaysOnTop = Topmost;
    }

    private void OnToggleSettings(object? sender, RoutedEventArgs e) =>
        SettingsPanel.IsVisible = !SettingsPanel.IsVisible;

    /// <summary>Salva le impostazioni. Sito e postazione toccano oggetti creati all'avvio (client HTTP,
    /// orchestratore), quindi il cambio si applica al riavvio: dirlo è meglio che far finta di niente.</summary>
    private void OnSaveSettings(object? sender, RoutedEventArgs e)
    {
        _model.Settings.SiteUrl = string.IsNullOrWhiteSpace(SiteBox.Text) ? "https://it.ivao.aero" : SiteBox.Text!.Trim();
        _model.Settings.OwnerOverride = string.IsNullOrWhiteSpace(OwnerBox.Text) ? null : OwnerBox.Text!.Trim();
        _model.Settings.Hotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? null : HotkeyBox.Text!.Trim();
        _model.Settings.HotkeyEnabled = HotkeyEnabledBox.IsChecked == true;
        _model.Settings.Save();

        SettingsPanel.IsVisible = false;
        _model.Notify("Impostazioni salvate. Sito, postazione e scorciatoia si applicano al riavvio del tool.");
        _ = _orchestrator;   // riferimento tenuto per i comandi futuri della finestra
    }
}
