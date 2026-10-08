using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// «Con <b>questi</b> in frequenza, chi cede e chi riceve»: i flussi degli accordi risolti contro un insieme di
/// stazioni online. Puro e deterministico, nessun I/O.
///
/// <para><b>Perché sta a parte.</b> Era il corpo di <c>AgreementService.ResolveForAccAsync</c>, e lì si poteva
/// chiedere solo per chi è online <i>adesso</i>. La stessa domanda la fa chi vuole <b>provare</b> una
/// configurazione — «e se fossero aperti solo WS2 ed ES2?» — e deve ricevere la stessa risposta dalla stessa
/// funzione: un banco di prova che risolve per conto suo proverebbe un'altra cosa. Carta
/// <c>docs/feature/2026-10-04-copertura-unica.md</c>.</para>
/// </summary>
public static class TransferResolution
{
    /// <param name="flows">Le righe piatte degli accordi (<see cref="AgreementExpansion"/>).</param>
    /// <param name="topology">Padri e righe di ripiego: la catena.</param>
    /// <param name="online">Chi è in frequenza, vero o ipotizzato.</param>
    /// <param name="rinvio">
    /// Come si scioglie un rinvio «copertura del punto». ⚠️ Va costruito con lo <b>stesso</b> insieme
    /// <paramref name="online"/>: le pretese dei volumi dipendono da chi c'è.
    /// </param>
    public static IReadOnlyList<ResolvedTransferFlow> Resolve(
        IReadOnlyList<TransferFlowRow> flows, Topology topology, IReadOnlySet<string> online,
        CoverageFallbackContext rinvio)
    {
        // Catena di candidati di un settore A UNA QUOTA: sé stesso, i ripieghi dichiarati che valgono lì, poi
        // gli antenati di copertura (cross-ACC). ⚠️ La quota è quella del PUNTO, non del flusso: un flusso una
        // quota non ce l'ha, e due punti dello stesso flusso possono ricadere su due settori diversi.
        IReadOnlyList<string> Chain(string? callsign, int? quotaFt, Func<IReadOnlyList<string>>? rinvioQui = null) =>
            string.IsNullOrWhiteSpace(callsign)
                ? Array.Empty<string>()
                : FallbackChain.Candidates(callsign, quotaFt, topology.Fallbacks, topology.ParentOf, rinvioQui);

        return flows.SelectMany<TransferFlowRow, ResolvedTransferFlow>(f =>
        {
            // 🔴 Anche chi CEDE si risolve alla quota del PUNTO (4 ottobre 2026). Prima il proprietario del flusso
            // si risolveva una volta sola e senza quota, cioè per soli padri: con ES5 chiuso e WS5 aperto i suoi
            // flussi comparivano a ES2 anche per i punti a FL350, che quel cielo non lo tiene, e a WS5 non
            // arrivavano. Un flusso una quota non ce l'ha, i suoi punti sì — e due punti dello stesso flusso
            // possono avere due cedenti diversi: allora il flusso esce due volte, ognuna coi suoi punti.
            var cedenti = new Dictionary<int, string?>();
            string? Cedente(int? quota)
            {
                var chiave = quota ?? int.MinValue;
                if (!cedenti.TryGetValue(chiave, out var chi))
                    cedenti[chiave] = chi = TransferOnlineResolver.FirstOnline(Chain(f.OwningSectorCallsign, quota), online);
                return chi;
            }

            var points = f.Points.Select(p =>
            {
                // ⚠️ La quota è quella AL TRASFERIMENTO: su una riga che distingue i due eventi è la seconda a
                // dire di chi è quel cielo.
                var quota = FallbackChain.HandoffFeetOf(p);
                CoverageFallbackResult? esito = null;
                var (handler, isOnline) = TransferOnlineResolver.Resolve(
                    Chain(p.NextSectorCallsign, quota, () =>
                    {
                        esito = rinvio.Risolvi(p.Cop, quota, f.OwningSectorCallsign, p.NextSectorCallsign);
                        return esito.Value.AsCandidates();
                    }), online);
                return (Cedente: Cedente(quota), Punto: new ResolvedTransferPoint
                {
                    Point = p, ResolvedHandler = handler, IsOnline = isOnline, Coverage = esito,
                });
            }).ToList();

            ResolvedTransferFlow Risolto(string? cedente, IReadOnlyList<ResolvedTransferPoint> suoi) => new()
            {
                Flow = f,
                ResolvedOwnerCallsign = cedente ?? f.OwningSectorCallsign,
                OwnerOnline = cedente is not null,
                Points = suoi,
            };

            // Un flusso senza punti resta una riga sola, col cedente di sempre: non c'è una quota a cui chiederlo.
            if (points.Count == 0) return new[] { Risolto(Cedente(null), Array.Empty<ResolvedTransferPoint>()) };

            // Una riga per cedente, nell'ordine in cui compare: quasi sempre una sola.
            return points
                .GroupBy(x => x.Cedente ?? "", StringComparer.OrdinalIgnoreCase)
                .Select(g => Risolto(g.First().Cedente, g.Select(x => x.Punto).ToList()));
        }).ToList();
    }
}
