using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Combinazione globale e registro locale. Il parser della combinazione sta in Core proprio perché è la parte
/// che può sbagliare: la registrazione vera sono tre chiamate Win32, il difficile è capire cosa ha scritto l'utente.
/// </summary>
public class HotkeyAndLogTests
{
    [Theory]
    [InlineData("Ctrl+Alt+L", HotkeyModifiers.Control | HotkeyModifiers.Alt, 'L')]
    [InlineData("ctrl+alt+l", HotkeyModifiers.Control | HotkeyModifiers.Alt, 'L')]
    [InlineData("  Ctrl + Shift + K ", HotkeyModifiers.Control | HotkeyModifiers.Shift, 'K')]
    [InlineData("Control+Alt+9", HotkeyModifiers.Control | HotkeyModifiers.Alt, '9')]
    [InlineData("Win+Alt+T", HotkeyModifiers.Win | HotkeyModifiers.Alt, 'T')]
    [InlineData("Cmd+J", HotkeyModifiers.Win, 'J')]
    public void Le_combinazioni_valide_si_interpretano(string text, HotkeyModifiers modifiers, char key)
    {
        var spec = HotkeySpec.Parse(text);

        Assert.NotNull(spec);
        Assert.Equal(modifiers, spec!.Modifiers);
        Assert.Equal(key, spec.Key);
    }

    [Theory]
    [InlineData("L")]              // senza modificatori ruberebbe un tasto a tutto il sistema
    [InlineData("Ctrl")]           // nessun tasto
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+F5")]        // tasti funzione non supportati: meglio rifiutare che indovinare
    [InlineData("Ctrl+A+B")]       // ambigua
    [InlineData("Ctrl++")]
    [InlineData("")]
    [InlineData(null)]
    public void Le_combinazioni_inaccettabili_danno_null_non_eccezioni(string? text)
    {
        Assert.Null(HotkeySpec.Parse(text));
    }

    [Fact]
    public void La_combinazione_si_riscrive_in_forma_canonica()
    {
        Assert.Equal("Ctrl+Alt+L", HotkeySpec.Parse("alt+ctrl+l")!.ToString());
        Assert.Equal("Ctrl+Alt+L", HotkeySpec.Default.ToString());
    }

    [Fact]
    public void Il_codice_del_tasto_coincide_col_virtual_key_di_Windows()
    {
        Assert.Equal(0x4C, HotkeySpec.Parse("Ctrl+Alt+L")!.VirtualKey);   // VK_L
        Assert.Equal(0x39, HotkeySpec.Parse("Ctrl+Alt+9")!.VirtualKey);   // VK_9
    }

    [Fact]
    public void Le_impostazioni_risolvono_la_combinazione_solo_se_attiva()
    {
        Assert.NotNull(new BridgeSettings { Hotkey = "Ctrl+Alt+L", HotkeyEnabled = true }.ResolveHotkey());
        Assert.Null(new BridgeSettings { Hotkey = "Ctrl+Alt+L", HotkeyEnabled = false }.ResolveHotkey());
        Assert.Null(new BridgeSettings { Hotkey = "sciocchezze", HotkeyEnabled = true }.ResolveHotkey());
    }

    [Fact]
    public void Il_registro_annota_scritture_e_rifiuti()
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vipi-log-{Guid.NewGuid():N}.log");
        try
        {
            var log = new BridgeLog(file);
            log.WroteLabel("AZA123", "210", "ASPIR", ok: true);
            log.WroteLabel("AZA123", "210", "ASPIR", ok: false, error: "Traffic not assumed.");

            var text = File.ReadAllText(file);
            Assert.Contains("SCRITTO  AZA123  «210»  (CoP ASPIR)", text);
            Assert.Contains("RIFIUTATO AZA123", text);
            Assert.Contains("Traffic not assumed.", text);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Un_percorso_impossibile_non_fa_esplodere_il_registro()
    {
        var log = new BridgeLog(System.IO.Path.Combine("Z:\\", "cartella-che-non-esiste", "bridge.log"));

        log.Write("prova");   // deve semplicemente non fare nulla
    }
}
