namespace Vipi.Application.Content;

/// <summary>
/// Tutto ciò che un testo cita e che si risolve al momento di mostrarlo: le <b>procedure</b> (§A73, §A80) e i
/// <b>dati</b> — frequenze, nominativi, piste, punti (carta <c>2026-09-20-riferimenti-ai-dati.md</c>).
///
/// <para>⚠️ Esiste per non avere due porte. I riferimenti si sostituiscono in cinque punti — il disegno dei
/// blocchi, l'anteprima dell'editor, la resa Markdown, l'indice della ricerca, la release in vigore — e ogni
/// meccanismo nuovo che si aggiungesse per conto suo dovrebbe ricordarsi di entrare in tutti e cinque. Ne basta
/// uno dimenticato perché in quella pagina il riferimento esca grezzo, e nessun test lo vede.</para>
/// </summary>
/// <param name="Procedure">I nomi di oggi delle procedure citate.</param>
/// <param name="Dati">I valori di oggi dei dati citati.</param>
public sealed record RiferimentiRisolti(NomiProcedura Procedure, ValoriDato Dati)
{
    /// <summary>Niente risolto: i riferimenti escono col loro ripiego — l'ultimo nome visto, o la chiave.</summary>
    public static RiferimentiRisolti Vuoto { get; } = new(NomiProcedura.Vuoto, ValoriDato.Vuoto);

    /// <summary>Solo le procedure: la forma che avevano i chiamanti prima dei dati.</summary>
    public static RiferimentiRisolti Di(NomiProcedura procedure) => new(procedure, ValoriDato.Vuoto);
}

/// <summary>La porta unica della sostituzione: un testo entra col riferimento, esce col valore di oggi.</summary>
public static class Riferimenti
{
    /// <summary>Vero se il testo contiene almeno un riferimento, di qualunque famiglia.</summary>
    public static bool Contiene(string? testo) =>
        RiferimentiProcedura.Contiene(testo) || RiferimentiDato.Contiene(testo);

    /// <summary>
    /// Il testo con ogni riferimento sostituito. <paramref name="risolti"/> <c>null</c> = solo i ripieghi, che
    /// è quel che serve a chi rende senza aver risolto niente (l'indice della ricerca, una resa di servizio):
    /// un riferimento non esce mai grezzo.
    /// </summary>
    public static string? Sostituisci(string? testo, RiferimentiRisolti? risolti)
    {
        var s = RiferimentiProcedura.Sostituisci(testo, risolti?.Procedure);
        return RiferimentiDato.Sostituisci(s, risolti?.Dati);
    }
}
