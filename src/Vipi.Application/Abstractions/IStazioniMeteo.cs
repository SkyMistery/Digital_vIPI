namespace Vipi.Application.Abstractions;

/// <summary>
/// Da quale stazione si prende il METAR/TAF di uno scalo (15 settembre 2026, committente: LIRJ non emette un
/// METAR suo). Il dato sta sull'anagrafica (<c>Airport.MetarStationIcao</c>); questa porta lo tiene in memoria
/// per il provider meteo, che è singleton e non può leggere il database dal circuito di chi chiede.
/// </summary>
public interface IStazioniMeteo
{
    /// <summary>La stazione di riferimento dello scalo, o <c>null</c> se lo scalo usa il suo ICAO.</summary>
    Task<string?> RiferimentoDiAsync(string icao, CancellationToken ct = default);

    /// <summary>Butta la copia in memoria: la prossima domanda rilegge l'anagrafica. Da chiamare dopo una scrittura.</summary>
    void Invalida();
}
