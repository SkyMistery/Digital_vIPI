using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Esito dell'import anagrafica aeroporti (auto-assegnazione): quanti aeroporti assegnati alla loro ACC
/// + eventuali aeroporti il cui import settori è fallito (<see cref="Failures"/>), che il chiamante logga.
/// </summary>
/// <param name="Refreshed">Aeroporti già in archivio i cui campi anagrafici (presenza militare, IATA, quota,
/// variazione magnetica) sono cambiati in questo giro. Sta separato da <paramref name="Assigned"/> perché
/// risponde a un'altra domanda: quello dice quanti scali sono ENTRATI, questo quanti erano già dentro e sono
/// stati CORRETTI — e a regime il primo è zero mentre il secondo no.</param>
/// <param name="AccDivergences">
/// Aeroporti che la <b>sorgente</b> mette sotto un centro diverso dal nostro. Si segnalano e basta: un cambio
/// di ACC è una decisione — chi lo esegue riaggancia i padri e riguarda i documenti — e un import che
/// spostasse da sé porterebbe con sé i settori di un aeroporto mentre qualcuno ci sta scrivendo sopra.
/// </param>
public sealed record AirportImportResult(int Assigned, IReadOnlyList<AirportImportFailure> Failures, int Refreshed = 0,
    IReadOnlyList<AirportAccDivergence>? AccDivergences = null)
{
    /// <summary>Le divergenze, mai null.</summary>
    public IReadOnlyList<AirportAccDivergence> Divergenze => AccDivergences ?? Array.Empty<AirportAccDivergence>();
}

/// <summary>
/// «Qui sta sotto <paramref name="Nostro"/>, la sorgente lo mette sotto <paramref name="Sorgente"/>.» Nasce
/// dal fatto che l'assegnazione è ADDITIVA: l'ACC di un aeroporto già in archivio non lo tocca più nessuno,
/// quindi una riassegnazione fatta da IVAO resterebbe invisibile per sempre.
/// </summary>
public sealed record AirportAccDivergence(string Icao, string Nome, string Nostro, string Sorgente);

/// <summary>
/// Il confronto, in un posto solo: lo fa il giro d'import (per segnalarlo nel registro) e lo fa la pagina di
/// gestione aeroporti (per mostrarlo a chi guarda), e le due risposte devono coincidere.
/// </summary>
public static class AirportAccDivergences
{
    /// <param name="nostri">Gli aeroporti in archivio: ICAO, nome, e il codice dell'ACC a cui stanno.</param>
    /// <param name="sorgente">L'anagrafica esterna.</param>
    public static IReadOnlyList<AirportAccDivergence> Trova(
        IEnumerable<(string Icao, string Nome, string Acc)> nostri, IEnumerable<SourceAirport> sorgente)
    {
        var perIcao = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in sorgente)
        {
            var acc = (a.AccCode ?? "").Trim();
            // ⚠️ Un ACC vuoto nella sorgente NON è una divergenza: è un dato che non c'è, e trattarlo da
            // disaccordo riempirebbe la pagina di segnalazioni su scali di cui non si sa nulla.
            if (acc.Length > 0) perIcao[a.Icao.Trim()] = acc;
        }

        return nostri
            .Where(a => perIcao.TryGetValue(a.Icao, out var src)
                        && !string.Equals(src, a.Acc, StringComparison.OrdinalIgnoreCase))
            .Select(a => new AirportAccDivergence(a.Icao, a.Nome, a.Acc, perIcao[a.Icao]))
            .OrderBy(d => d.Icao, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}


/// <summary>
/// Che cosa si è mosso spostando un aeroporto di ACC: serve a chi deve segnalarne l'impatto sui documenti —
/// il centro che lo perde e quello che lo prende raccontano tutt'e due una copertura che è cambiata.
/// </summary>
/// <param name="Callsigns">Le posizioni dell'aeroporto, come si chiamano: sono loro il legame coi documenti.</param>
public sealed record AirportMoved(string Icao, string Nome, string DaAcc, string AAcc, IReadOnlyList<string> Callsigns);
