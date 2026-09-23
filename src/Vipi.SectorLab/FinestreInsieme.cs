using System.Runtime.InteropServices;

namespace Vipi.SectorLab;

/// <summary>
/// Le due finestre del Lab (mappa e pannelli) tornano in primo piano insieme (chiesto dal committente il 23 settembre):
/// cliccando l'una, l'altra risale subito SOTTO di lei, sopra le altre applicazioni — senza prendersi il fuoco.
/// <para>Non con <c>Owner</c>: una finestra posseduta sta SEMPRE sopra la sua padrona, e con uno schermo solo i pannelli
/// coprirebbero la mappa per sempre. Qui nessuna delle due comanda: sale quella cliccata, l'altra le va dietro.</para>
/// </summary>
internal static class FinestreInsieme
{
    private const uint SenzaMuovere = 0x0002;       // SWP_NOSIZE
    private const uint SenzaSpostare = 0x0001;      // SWP_NOMOVE
    private const uint SenzaAttivare = 0x0010;      // SWP_NOACTIVATE
    private const uint SenzaDirloAlProprietario = 0x0200; // SWP_NOOWNERZORDER

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr finestra, IntPtr dopoDi, int x, int y, int larghezza, int altezza, uint come);

    /// <summary>Porta <paramref name="compagna"/> subito sotto <paramref name="attiva"/> nell'ordine delle finestre.</summary>
    public static void PortaDietro(Form attiva, Form? compagna)
    {
        if (compagna is null || compagna.IsDisposed || !compagna.Visible || compagna.WindowState == FormWindowState.Minimized
            || !attiva.IsHandleCreated || !compagna.IsHandleCreated)
            return;

        SetWindowPos(compagna.Handle, attiva.Handle, 0, 0, 0, 0, SenzaMuovere | SenzaSpostare | SenzaAttivare | SenzaDirloAlProprietario);
    }
}
