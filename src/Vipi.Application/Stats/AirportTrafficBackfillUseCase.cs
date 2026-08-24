using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>Esito di un giro di riempimento retroattivo.</summary>
public sealed record AirportTrafficBackfillResult(int Examined, int Filled, int Movements, int Skipped);

/// <summary>
/// Ricostruisce il traffico delle sessioni <b>d'aeroporto già passate</b>, che il campionamento dal vivo non
/// può aver visto perché è nato dopo.
///
/// <para><b>Cosa copre e cosa no.</b> Gli aeroporti sì, gli ACC no: la sorgente racconta i movimenti di uno
/// scalo, non quelli di un settore d'area. Per gli ACC il passato resta senza traffico e si popola vivendo —
/// ed è meglio di un numero inventato.</para>
///
/// <para><b>Perché costa.</b> Una chiamata per sessione: la finestra è quella della singola connessione.
/// Da qui il tetto per giro, che spalma il recupero dell'arretrato su più notti invece di fare migliaia di
/// richieste in una volta.</para>
/// </summary>
public sealed class AirportTrafficBackfillUseCase
{
    private readonly IAirportTrafficSource _sorgente;
    private readonly IAtcTrafficStore _archivio;
    private readonly IImportPolicyStore _policy;

    public AirportTrafficBackfillUseCase(
        IAirportTrafficSource sorgente, IAtcTrafficStore archivio, IImportPolicyStore policy)
    {
        _sorgente = sorgente;
        _archivio = archivio;
        _policy = policy;
    }

    public async Task<AirportTrafficBackfillResult> RunAsync(
        DateTimeOffset notBefore, int max, DateTimeOffset now, CancellationToken ct = default)
    {
        // Stesso gate della raccolta, prima di qualunque chiamata: è la stessa categoria di policy.
        var policy = await _policy.GetAsync(ct);
        if (!policy.IsImported(ImportCategory.AtcSessions))
            return new AirportTrafficBackfillResult(0, 0, 0, 0);

        var (daRiempire, concorrenti) = await _archivio.GetAirportSessionsToFillAsync(notBefore, max, ct);
        if (daRiempire.Count == 0) return new AirportTrafficBackfillResult(0, 0, 0, 0);

        int riempite = 0, movimenti = 0, saltate = 0;

        foreach (var sessione in daRiempire)
        {
            ct.ThrowIfCancellationRequested();

            // Se in quella finestra c'era una posizione più titolata sullo stesso campo, i movimenti sono
            // suoi: questa si marca come «provata» senza chiamare la sorgente, o li conteremmo due volte.
            if (AirportBackfillPlanner.Owner(sessione, concorrenti) != sessione.SessionId)
            {
                await _archivio.FillAirportMovementsAsync(
                    sessione.SessionId, Array.Empty<SourceAirportMovement>(), now, ct);
                saltate++;
                continue;
            }

            var mov = await _sorgente.GetMovementsAsync(sessione.Icao, sessione.StartUtc, sessione.EndUtc, ct);
            var scritte = await _archivio.FillAirportMovementsAsync(sessione.SessionId, mov, now, ct);

            riempite++;
            movimenti += scritte;
        }

        return new AirportTrafficBackfillResult(daRiempire.Count, riempite, movimenti, saltate);
    }
}
