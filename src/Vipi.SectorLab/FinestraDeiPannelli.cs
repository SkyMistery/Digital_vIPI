using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Vipi.SectorLab;

/// <summary>
/// La seconda finestra, per lavorare con due schermi (chiesto dal committente il 23 settembre): la pagina
/// <c>/pannelli</c> (Sfoglia, Problemi, la scheda, le modifiche) mentre la finestra principale tiene mappa e strati.
/// Stessa WebView2 della principale (stessa cartella dei dati, quindi stesso cookie del cancello), stesso server, stesso
/// stato. Con un secondo schermo si apre là, a tutto schermo.
/// </summary>
internal sealed class FinestraDeiPannelli : Form
{
    private readonly WebView2 _vista;
    private readonly Form _principale;

    /// <summary>Cliccati i pannelli, la mappa risale con loro (<see cref="FinestreInsieme"/>).</summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        FinestreInsieme.PortaDietro(this, _principale);
    }

    public FinestraDeiPannelli(Uri indirizzo, Diario diario, Form principale)
    {
        _principale = principale;
        Text = $"Aurora Sector Lab {Versione.Testo} — pannelli";
        Icon = principale.Icon;
        Width = 1200;
        Height = 850;

        // Sull'altro schermo, se c'è: è il motivo per cui la finestra esiste.
        var suo = Screen.FromControl(principale);
        if (Screen.AllScreens.FirstOrDefault(s => !s.Equals(suo)) is { } altro)
        {
            StartPosition = FormStartPosition.Manual;
            Location = altro.WorkingArea.Location;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }

        _vista = new WebView2
        {
            Dock = DockStyle.Fill,
            CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Program.CartellaDellUtente, "WebView2"),
            },
        };
        Controls.Add(_vista);

        _vista.CoreWebView2InitializationCompleted += (_, e) =>
        {
            if (!e.IsSuccess)
            {
                diario.Scrivi($"WebView2 dei pannelli non partita: {e.InitializationException}");
                BeginInvoke(Close);
                return;
            }

            var impostazioni = _vista.CoreWebView2.Settings;
            impostazioni.IsStatusBarEnabled = false;
#if !DEBUG
            impostazioni.AreDevToolsEnabled = false;
#endif
            // Da qui non si apre nient'altro: nessuna finestra nuova, nessuna pagina esterna.
            _vista.CoreWebView2.NewWindowRequested += (_, n) => n.Handled = true;
            _vista.CoreWebView2.NavigationCompleted += (_, n) =>
                diario.Scrivi($"pannelli: pagina caricata ok={n.IsSuccess} http={n.HttpStatusCode} {n.WebErrorStatus}");
            _vista.CoreWebView2.Navigate(indirizzo.AbsoluteUri);
        };

        Load += async (_, _) =>
        {
            try
            {
                await _vista.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                diario.Scrivi($"WebView2 dei pannelli non partita: {ex}");
                BeginInvoke(Close);
            }
        };
    }
}
