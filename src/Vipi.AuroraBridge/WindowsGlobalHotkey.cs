using System.Runtime.InteropServices;
using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge;

/// <summary>
/// Combinazione globale su Windows, per scrivere il livello senza staccare le mani dalla PVD di Aurora.
///
/// Usa <c>RegisterHotKey</c> con finestra nulla e un thread proprio che gira un message loop: i messaggi
/// <c>WM_HOTKEY</c> finiscono nella coda del thread che ha registrato. È la strada pulita — l'alternativa
/// (<c>SetWindowsHookEx</c>/WH_KEYBOARD_LL) intercetterebbe **ogni** tasto del sistema, che per un tool che
/// deve solo reagire a una combinazione è tanto sproporzionato quanto sgradevole.
///
/// Fuori da Windows non fa nulla e lo dichiara: il pulsante nella finestra resta l'unica via.
/// </summary>
public sealed class WindowsGlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;
    private const int HotkeyId = 0xB11D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    private Thread? _thread;
    private uint _threadId;
    private volatile bool _running;

    /// <summary>Esito della registrazione, da mostrare all'utente: una combinazione già presa da un altro
    /// programma fallisce in silenzio se non lo si dice.</summary>
    public string? Status { get; private set; }

    public bool IsRegistered { get; private set; }

    /// <summary>Registra la combinazione. <paramref name="onPressed"/> viene invocato dal thread della
    /// scorciatoia: chi lo riceve deve rimbalzare sulla UI da sé.</summary>
    public bool TryRegister(HotkeySpec spec, Action onPressed)
    {
        if (!OperatingSystem.IsWindows())
        {
            Status = "Combinazione globale non disponibile su questo sistema: usa il pulsante nella finestra.";
            return false;
        }

        var ready = new ManualResetEventSlim(false);
        _running = true;

        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            IsRegistered = RegisterHotKey(IntPtr.Zero, HotkeyId, (uint)spec.Modifiers, (uint)spec.VirtualKey);
            Status = IsRegistered
                ? $"Combinazione {spec} attiva."
                : $"Combinazione {spec} non registrabile: probabilmente la usa già un altro programma.";
            ready.Set();

            if (!IsRegistered) return;

            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WmHotkey && msg.wParam.ToInt32() == HotkeyId)
                {
                    try { onPressed(); }
                    catch (Exception) { /* la scorciatoia non deve poter abbattere il tool */ }
                }
            }

            UnregisterHotKey(IntPtr.Zero, HotkeyId);
        })
        {
            IsBackground = true,
            Name = "vipi-bridge-hotkey",
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));

        return IsRegistered;
    }

    public void Dispose()
    {
        _running = false;
        if (_threadId != 0) PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }
}
