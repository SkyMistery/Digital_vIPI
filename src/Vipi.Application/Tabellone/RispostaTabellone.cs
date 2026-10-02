namespace Vipi.Application.Tabellone;

// La risposta di GET /api/tabellone/{ICAO}, campo per campo come nel formato concordato col committente
// (FORMATO-DATI.md del 2 ottobre 2026, §2–§3). I nomi delle proprietà diventano camelCase nel JSON: cambiarli è
// cambiare il contratto col tabellone.

public sealed record RispostaTabellone(
    int Versione,
    ScaloTabellone Scalo,
    DateTimeOffset Aggiornato,
    EventoTabellone? Evento,
    string Voli,
    FontiTabellone Fonti,
    IReadOnlyList<RigaTabellone> Partenze,
    IReadOnlyList<RigaTabellone> Arrivi);

public sealed record ScaloTabellone(string Icao, string? Iata, string Nome, string FusoOrario);

public sealed record EventoTabellone(string Nome, bool Attivo);

public sealed record FontiTabellone(StatoFonte Booking, StatoFonte Whazzup, StatoFonte GateManager);

public sealed record StatoFonte(bool Ok, DateTimeOffset? Letto);

/// <summary>Una riga del tabellone. Testi già pronti per le palette: maiuscolo, senza accenti, tagliati.</summary>
public sealed record RigaTabellone(
    string Id,
    string Volo,
    string? Compagnia,
    AltroScalo Scalo,
    string? Aereo,
    DateTimeOffset? Programmato,
    DateTimeOffset? Stimato,
    string? Gate,
    bool GateCambiato,
    string Stato,
    bool Online,
    bool Prenotato);

/// <summary>L'ALTRO scalo: la destinazione per le partenze, la provenienza per gli arrivi.</summary>
public sealed record AltroScalo(string Icao, string? Iata, string Citta);
