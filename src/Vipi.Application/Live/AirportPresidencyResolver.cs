using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Live;

/// <summary>Una postazione che presiede: il callsign, il suo ruolo, e se è dell'aeroporto o lo copre dall'alto.</summary>
public sealed record PresidingStation(string Callsign, SectorType Type, bool IsAirportOwn);

/// <summary>
/// Chi controlla un aeroporto <b>adesso</b>.
///
/// <para><see cref="Local"/> sono le postazioni dell'aeroporto online, dalla più locale alla più estesa
/// (DEL → GND → TWR → APP): la risposta non è una sola perché non lo è nemmeno la domanda — al gate serve
/// il ground, in avvicinamento la torre. <see cref="Covering"/> è chi copre tutto il resto risalendo la
/// gerarchia, quando una posizione locale non c'è. Se non c'è nessuno dei due, <see cref="Unicom"/>.</para>
/// </summary>
public sealed record AirportPresidency(
    IReadOnlyList<PresidingStation> Local,
    PresidingStation? Covering)
{
    /// <summary>Nessuno online, né in loco né risalendo: il traffico si autocoordina.</summary>
    public bool Unicom => Local.Count == 0 && Covering is null;

    /// <summary>Vero se qualcuno, in loco o sopra, è online.</summary>
    public bool AnyOnline => !Unicom;
}

/// <summary>
/// Risponde a «chi controlla questo aeroporto adesso», che è la domanda per cui esiste il prodotto e a cui
/// finora si rispondeva solo per i punti di trasferimento.
///
/// <para><b>Perché non basta guardare i callsign che cominciano con l'ICAO.</b> È ciò che faceva la vista
/// live: diceva <i>se</i> qualcuno c'era, non <i>chi</i>, e soprattutto non risaliva — se a Crotone non c'è
/// nessuno ma l'avvicinamento che la copre è online, la risposta utile è «chiama quello», non «nessuno».
/// È lo stesso difetto che i trasferimenti avevano prima della risalita del Round 20.</para>
///
/// <para><b>La regola di confronto è condivisa</b> con <see cref="TransferOnlineResolver"/>: un callsign
/// online copre un candidato per uguaglianza, per segmento o per sottostringa lunga. Riusarla — invece di
/// riscriverla — è ciò che impedisce a due schermate di dare risposte diverse sullo stesso settore, e la
/// diagnostica sorveglia già i callsign che si confondono fra loro.</para>
///
/// <para>Puro e deterministico: gli ingredienti (posizioni dell'aeroporto e catena degli antenati) li
/// prepara il chiamante, che sa da dove leggerli.</para>
/// </summary>
public static class AirportPresidencyResolver
{
    /// <param name="positions">Posizioni dell'aeroporto (callsign + ruolo), in qualunque ordine.</param>
    /// <param name="ancestors">Catena di copertura dal padre dell'aeroporto in su, già ordinata.</param>
    /// <param name="online">Callsign online adesso.</param>
    public static AirportPresidency Resolve(
        IReadOnlyList<(string Callsign, SectorType Type)> positions,
        IReadOnlyList<string> ancestors,
        IReadOnlySet<string> online)
    {
        var locali = positions
            .Where(p => !string.IsNullOrWhiteSpace(p.Callsign))
            .Where(p => TransferOnlineResolver.FirstOnline(new[] { p.Callsign }, online) is not null)
            // Rung: più alto = più locale (DEL 30 → APP 5). Si mostra dal gate verso l'alto.
            .OrderByDescending(p => AirportPositionLadder.Rung(p.Type))
            .ThenBy(p => p.Callsign, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PresidingStation(p.Callsign, p.Type, IsAirportOwn: true))
            .ToList();

        // Chi copre ciò che in loco non è presidiato: il primo antenato online, nell'ordine della catena.
        // ⚠️ Gli antenati possono includere una posizione DELL'AEROPORTO — il padre di uno scalo è spesso il
        // suo stesso avvicinamento — e allora comparirebbe due volte, in Local e come copertura. Si salta:
        // ciò che è in loco è già detto sopra.
        var tipiPerCallsign = positions.ToDictionary(p => p.Callsign, p => p.Type, StringComparer.OrdinalIgnoreCase);
        PresidingStation? copertura = null;
        foreach (var a in ancestors)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            if (tipiPerCallsign.ContainsKey(a)) continue;
            if (TransferOnlineResolver.FirstOnline(new[] { a }, online) is null) continue;

            copertura = new PresidingStation(a, tipiPerCallsign.GetValueOrDefault(a, SectorType.Ctr), IsAirportOwn: false);
            break;
        }

        return new AirportPresidency(locali, copertura);
    }
}
