using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab;

// «Application» sarebbe Vipi.Application: vedi Program.cs.
using WinForms = System.Windows.Forms;

/// <summary>
/// La finestra: una WebView2 a tutta superficie che mostra il server locale. Nient'altro — le pagine stanno in
/// Vipi.SectorLab.Ui, la logica in Vipi.SectorLab.Core.
/// </summary>
internal sealed class Finestra : Form
{
    /// <summary>Oltre questo tempo l'autoprova si dà per fallita: in F0 il circuito era vivo in 0,6 s.</summary>
    private const int AttesaDellAutoprovaMs = 60_000;

    private readonly WebView2 _vista;
    private readonly Diario _diario;
    private readonly bool _autoprova;

    /// <summary>Con una cartella aperta all'avvio, l'autoprova aspetta che la MAPPA abbia disegnato, non solo il circuito.</summary>
    private readonly bool _attendeLaMappa;

    /// <summary>Il codice d'uscita: 0 finché niente va storto; con l'autoprova diventa 0 solo col circuito vivo.</summary>
    public int Esito { get; private set; }

    /// <summary>Il Lab, per dirgli quando i pannelli sono in un'altra finestra (due schermi). Nullo: niente seconda finestra.</summary>
    private readonly SessioneDelLab? _lab;

    private readonly Uri _ingresso;
    private FinestraDeiPannelli? _pannelli;

    public Finestra(Uri ingresso, Diario diario, bool autoprova, bool attendeLaMappa = false, SessioneDelLab? lab = null)
    {
        _diario = diario;
        _lab = lab;
        _ingresso = ingresso;
        _autoprova = autoprova;
        _attendeLaMappa = attendeLaMappa;
        Esito = autoprova ? 1 : 0;

        Text = $"Aurora Sector Lab {Versione.Testo}";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        if (Icon.ExtractAssociatedIcon(WinForms.Application.ExecutablePath) is { } icona)
            Icon = icona;

        _vista = new WebView2
        {
            Dock = DockStyle.Fill,
            // 🔴 Di base la WebView2 tiene i suoi dati ACCANTO all'eseguibile: in una cartella senza permessi di
            // scrittura (Programmi, una chiavetta protetta) non parte. Carta F3 §7.
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
                _diario.Scrivi($"WebView2 non partita: {e.InitializationException}");
                Chiudi(esito: 2);
                return;
            }
            _diario.Scrivi("WebView2 pronta");
            var impostazioni = _vista.CoreWebView2.Settings;
            impostazioni.IsStatusBarEnabled = false;
#if !DEBUG
            impostazioni.AreDevToolsEnabled = false;
#endif
            _vista.CoreWebView2.WebMessageReceived += AllArrivoDiUnMessaggio;
            _vista.CoreWebView2.NewWindowRequested += AllaRichiestaDiUnaFinestra;
            // Uno script o un foglio che non arriva non lo dice nessuno: la pagina resta lì, ferma, senza errori
            // (è la trappola 2 di F0). Ogni risposta andata male finisce nel diario.
            _vista.CoreWebView2.WebResourceResponseReceived += (_, r) =>
            {
                if (r.Response.StatusCode >= 400)
                    _diario.Scrivi($"risposta {r.Response.StatusCode} per {r.Request.Uri}");
            };
            _vista.CoreWebView2.NavigationCompleted += (_, n) =>
                _diario.Scrivi($"pagina caricata: ok={n.IsSuccess} http={n.HttpStatusCode} {n.WebErrorStatus}");
            _vista.CoreWebView2.Navigate(ingresso.AbsoluteUri);
        };

        Load += async (_, _) =>
        {
            try
            {
                await _vista.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                _diario.Scrivi($"WebView2 non partita: {ex}");
                Chiudi(esito: 2);
            }
        };

        if (autoprova)
        {
            var sveglia = new System.Windows.Forms.Timer { Interval = AttesaDellAutoprovaMs };
            sveglia.Tick += (_, _) =>
            {
                sveglia.Stop();
                _diario.Scrivi($"autoprova: circuito non vivo dopo {AttesaDellAutoprovaMs / 1000} s");
                Chiudi(esito: 1);
            };
            sveglia.Start();
        }
    }

    /// <summary>
    /// Una window.open della pagina. L'unica finestra che il Lab apre è quella dei pannelli, sul suo stesso server
    /// (<c>/pannelli</c>): tutto il resto si ferma qui, e nella WebView2 non si apre niente.
    /// </summary>
    private void AllaRichiestaDiUnaFinestra(object? mittente, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var indirizzo)
            || Uri.Compare(indirizzo, _ingresso, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0
            || !string.Equals(indirizzo.AbsolutePath, "/pannelli", StringComparison.OrdinalIgnoreCase)
            || _lab is null)
        {
            _diario.Scrivi($"finestra nuova rifiutata: {e.Uri}");
            return;
        }

        if (_pannelli is { IsDisposed: false })
        {
            _pannelli.Activate();
            return;
        }

        _pannelli = new FinestraDeiPannelli(indirizzo, _diario, this);
        _pannelli.FormClosed += (_, _) =>
        {
            _pannelli = null;
            _lab.PannelliAperti(false);
        };
        _pannelli.Show();
        _lab.PannelliAperti(true);
    }

    /// <summary>Cliccata la mappa, i pannelli (se ci sono) risalgono con lei.</summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        FinestreInsieme.PortaDietro(this, _pannelli);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // I pannelli senza la mappa non servono a niente: si chiudono con lei.
        _pannelli?.Close();
        base.OnFormClosing(e);
    }

    private void AllArrivoDiUnMessaggio(object? mittente, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string testo = e.TryGetWebMessageAsString() ?? "";
        _diario.Scrivi($"dalla pagina: {testo}");

        // «Pannelli qui»: chiudendo la finestra, il FormClosed dice al Lab che i pannelli sono tornati.
        if (testo == "pannelli: chiudi")
        {
            _pannelli?.Close();
            return;
        }

        if (!_autoprova)
            return;
        // Con una cartella aperta all'avvio la prova non finisce col circuito vivo: finisce quando la mappa ha
        // disegnato, perché è quella la cosa che si sta misurando (slice 4).
        bool finita = _attendeLaMappa
            ? testo.StartsWith("mappa disegnata", StringComparison.Ordinal)
            : testo == "pronto";
        if (finita)
            Chiudi(esito: 0);
        else if (testo.StartsWith("errore:", StringComparison.Ordinal))
            Chiudi(esito: 1);
    }

    private void Chiudi(int esito)
    {
        Esito = esito;
        BeginInvoke(Close);
    }
}
