using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// L'indirizzo <b>giusto</b> di un documento d'aeroporto quando l'ACC nella rotta non è più il suo.
///
/// <para>Le pagine d'aeroporto e di vSOP militare vivono sotto <c>/services/vsop/{acc}/…</c> ma risolvono il
/// documento per <b>ICAO</b>: l'ACC nella rotta è chrome — briciole di pane, collegamenti, titolo. Quando uno
/// scalo cambia centro, i link scritti prima continuano quindi a funzionare e a mostrare l'ACC <b>sbagliato</b>:
/// una pagina che dice il falso senza rompersi, che è il modo peggiore di sbagliare. Chi la apre da un
/// segnalibro, o da un messaggio di sei mesi fa, legge «Brindisi» su uno scalo che è di Roma.</para>
///
/// <para>⚠️ La domanda si fa al <b>catalogo delle stazioni</b>, che è già in cache di processo e si aggiorna
/// da sé quando una riga <c>Airport</c> viene salvata: nessuna query mentre la pagina si disegna, e nessun
/// rischio di toccare il <c>DbContext</c> del circuito. Vedi <see cref="IStationResolver.Airport"/>.</para>
/// </summary>
public static class RottaAeroporto
{
    /// <summary>
    /// Il codice ACC da usare al posto di quello nella rotta, o <c>null</c> se l'indirizzo va già bene — o se
    /// non si sa (ICAO sconosciuto al catalogo: in quel caso non si redirige da nessuna parte, si lascia che
    /// sia la pagina a dire «non trovato»).
    /// </summary>
    public static string? AccDaCorreggere(IStationResolver stazioni, string? accNellaRotta, string? icao)
    {
        var vero = stazioni.Airport(icao)?.AccCode;
        if (string.IsNullOrWhiteSpace(vero)) return null;
        return string.Equals(vero, (accNellaRotta ?? "").Trim(), StringComparison.OrdinalIgnoreCase) ? null : vero;
    }
}
