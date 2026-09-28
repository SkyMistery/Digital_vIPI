using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Stats;

/// <summary>Esito di un giro di riempimento retroattivo.</summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
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

            // Una posizione non d'aeroporto, o una finestra coperta PER INTERO da posizioni più titolate dello stesso
            // campo: ogni movimento è di un altro. Si marca «provata» senza chiamare la sorgente.
            if (AirportBackfillPlanner.Competence(sessione.Type) == 0
                || AirportBackfillPlanner.CopertaDaAltri(sessione, concorrenti))
            {
                await _archivio.FillAirportMovementsAsync(
                    sessione.SessionId, Array.Empty<SourceAirportMovement>(), now, ct);
                saltate++;
                continue;
            }

            // 🔴 U-094 (revisione totale 3): la sessione chiede la SUA finestra, e tiene i movimenti avvenuti quando in
            // frequenza non c'era nessuno più titolato di lei. Prima una sovrapposizione anche breve le dava zero per
            // l'intera sessione, e i movimenti fuori dall'intersezione non andavano a nessuno.
            var mov = (await _sorgente.GetMovementsAsync(sessione.Icao, sessione.StartUtc, sessione.EndUtc, ct))
                .Where(m => AirportBackfillPlanner.Tiene(sessione, concorrenti, AirportCoverage.Instant(m)))
                .ToList();
            var scritte = await _archivio.FillAirportMovementsAsync(sessione.SessionId, mov, now, ct);

            riempite++;
            movimenti += scritte;
        }

        return new AirportTrafficBackfillResult(daRiempire.Count, riempite, movimenti, saltate);
    }
}
