namespace Vipi.Application.Abstractions;

/// <summary>Un giorno del registro delle disconnessioni (vedi <see cref="IRiepilogoDisconnessioni"/>).</summary>
/// <param name="Riagganciate">Il circuito è tornato da solo: un buco di rete, chi legge non ha perso niente.</param>
/// <param name="Rifiutate">Il server non conosceva più il circuito: processo spento o ripartito.</param>
/// <param name="Fallite">Finiti i tentativi senza mai raggiungere il server.</param>
/// <param name="Abbandonate">La pagina chiusa o lasciata mentre si riprovava.</param>
/// <param name="ProcessoCambiato">Il beacon è arrivato a un processo diverso da quello che aveva servito la pagina.</param>
/// <param name="SchedaNascosta">Quando è caduta, la scheda era in secondo piano (il browser strozza i timer).</param>
/// <param name="SenzaRete">Il browser stesso diceva di essere fuori rete.</param>
/// <param name="MedianaBuco">La mediana della durata del buco, in secondi.</param>
public sealed record DisconnessioniDelGiorno(DateOnly Giorno, int Totale, int Riagganciate, int Rifiutate, int Fallite,
    int Abbandonate, int ProcessoCambiato, int SchedaNascosta, int SenzaRete, double MedianaBuco);

/// <summary>Il riepilogo dei giorni tenuti, e le pagine dove succede più spesso.</summary>
public sealed record RiepilogoDisconnessioni(IReadOnlyList<DisconnessioniDelGiorno> Giorni,
    IReadOnlyList<(string Pagina, int Volte)> Pagine);

/// <summary>
/// Le disconnessioni viste dai BROWSER (committente, 30 settembre 2026: «possiamo trovare un modo per gestire e
/// diminuire queste disconnessioni? … mettere in produzione qualche strumento per monitorarle»). Il server da solo
/// non distingue una scheda chiusa da una connessione caduta: lo sa solo il browser, che manda una riga a ogni
/// riquadro di riconnessione. Il file sta in <c>diagnostica/</c>; questo è il riassunto per la pagina Diagnostica.
/// </summary>
public interface IRiepilogoDisconnessioni
{
    RiepilogoDisconnessioni Leggi();
}
