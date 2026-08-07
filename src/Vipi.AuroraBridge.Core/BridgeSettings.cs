using System.Text.Json;

namespace Vipi.AuroraBridge.Core;

/// <summary>
/// Impostazioni del tool, salvate accanto alla cache in <c>%LOCALAPPDATA%\VipiAuroraBridge</c>.
/// Volutamente poche: quelle che un controllore può aver bisogno di cambiare a sessione aperta.
/// </summary>
public sealed class BridgeSettings
{
    /// <summary>Sito da interrogare. Si cambia per puntare a un host locale in prova.</summary>
    public string SiteUrl { get; set; } = "https://it.ivao.aero";

    /// <summary>Postazione da usare al posto di quella connessa. Vuoto = si usa <c>#CONN</c>.</summary>
    public string? OwnerOverride { get; set; }

    /// <summary>Finestra sempre in primo piano: è un tool da tenere sopra la PVD.</summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>Ogni quanto Aurora viene interrogata sulla selezione corrente.</summary>
    public int SelectionPollMs { get; set; } = 1000;

    /// <summary>Combinazione globale che scrive il candidato migliore senza lasciare la PVD (es. «Ctrl+Alt+L»).
    /// Vuota o non interpretabile = nessuna combinazione registrata.</summary>
    public string? Hotkey { get; set; } = HotkeySpec.Default.ToString();

    /// <summary>Combinazione globale attiva. Si spegne senza cancellare la combinazione scelta.</summary>
    public bool HotkeyEnabled { get; set; } = true;

    /// <summary>La combinazione interpretata, o null se spenta/illeggibile.</summary>
    public HotkeySpec? ResolveHotkey() => HotkeyEnabled ? HotkeySpec.Parse(Hotkey) : null;

    public BridgePollingOptions ToPollingOptions() =>
        new(SelectionMs: SelectionPollMs < 250 ? 250 : SelectionPollMs);

    // --- persistenza ---

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VipiAuroraBridge", "settings.json");

    /// <summary>Carica le impostazioni; qualunque problema (file assente, JSON rotto) ricade sui default:
    /// un tool operativo non deve rifiutarsi di partire per un file di configurazione malmesso.</summary>
    public static BridgeSettings Load(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            if (!File.Exists(file)) return new BridgeSettings();
            return JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(file), Json) ?? new BridgeSettings();
        }
        catch (Exception)
        {
            return new BridgeSettings();
        }
    }

    public void Save(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception) { /* impostazioni non salvate: fastidio, non guasto */ }
    }
}
