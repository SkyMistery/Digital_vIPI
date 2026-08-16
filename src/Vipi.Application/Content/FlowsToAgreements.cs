using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Converte i **flussi** storici (<see cref="TransferFlowRow"/>) negli **accordi** che prendono il loro posto.
/// Funzione pura: nessun IO, così il travaso si prova sui dati veri senza un database.
///
/// <para><b>Tre operazioni, in quest'ordine.</b></para>
/// <list type="number">
/// <item><b>Si separa per ricevente.</b> Un flusso poteva contenere righe che consegnano a enti diversi — in
/// archivio gli arrivi LIRN vanno per metà all'APP e per metà al CTR — e quelli sono <b>due accordi</b>, non
/// uno. Il modello vecchio non sapeva dirlo; questo lo dice, ed è la prima cosa che il travaso rende
/// vera.</item>
/// <item><b>Si fondono gli aeroporti.</b> Flussi identici in tutto tranne l'aeroporto diventano un accordo solo
/// con più aeroporti — che è come lo scrivono i documenti veri («LIRF-LIRA-LIRU-LIRE»).</item>
/// <item><b>Si fondono i punti.</b> Righe <b>consecutive</b> identiche in tutto tranne il CoP diventano una
/// clausola con l'elenco dei punti.</item>
/// </list>
///
/// <para><b>Cosa NON fa, apposta.</b> Non accoppia i due versi di un accordo bilaterale. Sarebbe la fusione più
/// vistosa — i sorvoli LIBB→LGGG e LGGG→LIBB sono chiaramente lo stesso accordo — ed è proprio per questo che
/// non si fa da sola: le due liste di punti in archivio <b>non coincidono</b> (BELIX di qua, OLGAT di là), quindi
/// accoppiarle vorrebbe dire scegliere quale delle due è quella giusta. Non è una decisione da migrazione:
/// gli accordi nascono a un verso, e il cruscotto delle lacune elenca le coppie candidate perché sia un
/// editore a unirle guardandole.</para>
///
/// <para><b>La fusione dei punti è per righe CONSECUTIVE</b> e non per «tutte quelle uguali»: nell'outline
/// delle varianti l'ordine È la struttura, e riordinare per raggruppare riassegnerebbe un'eccezione a un'altra
/// alternativa — senza nessun errore. È il difetto più pericoloso di quest'area, e vale anche qui.</para>
/// </summary>
public static class FlowsToAgreements
{
    public static IReadOnlyList<AgreementRow> Convert(IReadOnlyList<TransferFlowRow> flows)
    {
        // 1) Ogni flusso si spezza in blocchi per ricevente, mantenendo l'ordine relativo delle righe.
        var pieces = flows.SelectMany(SplitByReceiver).ToList();

        // 2) Blocchi identici in tutto tranne l'aeroporto diventano un accordo solo. La chiave porta la FIRMA
        //    completa delle righe: due flussi che differiscono per un solo campo — in archivio la parità, che
        //    cambia da un aeroporto all'altro sugli stessi arrivi via ASPIR — NON sono lo stesso accordo, e
        //    fonderli perderebbe metà del dato senza dirlo.
        var groups = new List<(string Key, List<Piece> Items)>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var p in pieces)
        {
            var key = GroupKey(p);
            if (!index.TryGetValue(key, out var at))
            {
                index[key] = groups.Count;
                groups.Add((key, new List<Piece>()));
                at = groups.Count - 1;
            }
            groups[at].Items.Add(p);
        }

        var agreements = new List<AgreementRow>(groups.Count);
        var nextId = 1;

        foreach (var (_, items) in groups)
        {
            var first = items[0];

            var parties = new List<AgreementPartyRow>
            {
                new(AgreementSide.A, first.OwnerSectorId, first.OwnerCallsign, 1),
            };
            if (first.ReceiverCallsign is not null)
                parties.Add(new AgreementPartyRow(AgreementSide.B, first.ReceiverSectorId!.Value,
                                                  first.ReceiverCallsign, 1));

            // Aeroporti nell'ordine di prima apparizione; i flussi senza aeroporto non ne producono nessuno.
            var airports = items
                .Where(x => !string.IsNullOrWhiteSpace(x.AirportIcao))
                .GroupBy(x => x.AirportIcao!, StringComparer.OrdinalIgnoreCase)
                .Select((g, i) => new AgreementAirportRow(g.Key, g.Select(x => x.AirportName)
                                                                 .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)), i + 1))
                .ToList();

            agreements.Add(new AgreementRow
            {
                Id = nextId,
                OwnerAccCode = first.AccCode,
                TrafficKind = first.Kind,
                Description = first.Description,
                Order = nextId,
                Parties = parties,
                Airports = airports,
                // 3) Le righe del primo blocco: gli altri sono identici per costruzione (stessa firma).
                Clauses = MergeClauses(first.Points),
            });
            nextId++;
        }

        return agreements;
    }

    /// <summary>Un pezzo di flusso con un solo ricevente: l'unità che diventa (o entra in) un accordo.</summary>
    private sealed record Piece(
        string AccCode, int OwnerSectorId, string OwnerCallsign, TransferFlowKind Kind,
        string? AirportIcao, string? AirportName, string? Description,
        int? ReceiverSectorId, string? ReceiverCallsign, IReadOnlyList<TransferPointRow> Points);

    /// <summary>
    /// Spezza il flusso in blocchi per ricevente. I blocchi escono nell'ordine di prima apparizione del
    /// ricevente, e dentro ognuno le righe conservano il loro ordine relativo — che nell'outline è la struttura.
    /// </summary>
    private static IEnumerable<Piece> SplitByReceiver(TransferFlowRow f)
    {
        var buckets = new List<(string? Callsign, int? Id, List<TransferPointRow> Points)>();

        foreach (var p in f.Points.OrderBy(x => x.Order))
        {
            var b = buckets.FirstOrDefault(x => string.Equals(x.Callsign, p.NextSectorCallsign, StringComparison.OrdinalIgnoreCase));
            if (b.Points is null)
            {
                b = (p.NextSectorCallsign, p.NextSectorId, new List<TransferPointRow>());
                buckets.Add(b);
            }
            b.Points.Add(p);
        }

        // Un flusso senza righe è comunque un accordo: l'intestazione l'ha scritta qualcuno, e buttarla via
        // perché non ha ancora clausole significherebbe perdere lavoro editoriale in silenzio. In archivio ce
        // n'è uno (un sorvolo di Roma NE mai compilato).
        if (buckets.Count == 0)
            buckets.Add((null, null, new List<TransferPointRow>()));

        foreach (var b in buckets)
            yield return new Piece(f.AccCode, f.OwningSectorId, f.OwningSectorCallsign, f.Kind,
                                   f.AirportIcao, f.AirportName, f.Description, b.Id, b.Callsign, b.Points);
    }

    /// <summary>
    /// La chiave con cui due pezzi diventano lo stesso accordo con due aeroporti: tutto tranne l'aeroporto,
    /// <b>firma delle righe compresa</b>. La firma include ogni campo, perché ogni campo è una cosa che
    /// l'accordo dice — e ciò che non entra nella chiave viene fuso, cioè perso.
    /// </summary>
    private static string GroupKey(Piece p)
    {
        var sb = new StringBuilder();
        sb.Append(p.AccCode).Append('')
          .Append(p.OwnerCallsign).Append('')
          .Append(p.Kind).Append('')
          .Append(p.ReceiverCallsign ?? "").Append('')
          .Append(p.Description ?? "").Append('');
        foreach (var x in p.Points) sb.Append(PointSignature(x)).Append('');
        return sb.ToString();
    }

    /// <summary>Tutti i campi della riga <b>tranne il CoP</b> — che è ciò che la fusione dei punti mette in
    /// elenco — e tranne il ricevente, già nella chiave del pezzo.</summary>
    private static string ClauseSignature(TransferPointRow p)
    {
        var sb = new StringBuilder();
        void A(object? v) => sb.Append(v switch
        {
            null => "",
            bool b => b ? "1" : "0",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            var other => other.ToString(),
        }).Append('');

        A(p.LevelValue); A(p.LevelUnit); A(p.LevelConstraint); A(p.LevelSpecial); A(p.Parity); A(p.VerticalState);
        A(p.ConditionLabel); A(p.ConditionRefId); A(p.ConditionAreaLabel); A(p.ConditionCustomLabel);
        A(p.HandoffKind); A(p.HandoffLabel); A(p.HandoffLevelValue); A(p.HandoffLevelUnit); A(p.HandoffLevelConstraint);
        A(p.CommsHandoffKind); A(p.CommsHandoffLabel); A(p.SpeedValue); A(p.SpeedConstraint);
        A(p.VariantGroup); A(p.VariantDepth); A(p.IsGroupWide);
        return sb.ToString();
    }

    private static string PointSignature(TransferPointRow p) => p.Cop + '' + ClauseSignature(p);

    /// <summary>
    /// Le righe diventano clausole, fondendo le **consecutive** che differiscono solo per il CoP. Consecutive e
    /// non «tutte le uguali»: l'ordine è la struttura dell'outline, e raggrupparle spostandole riassegnerebbe
    /// un'eccezione a un'altra alternativa senza che nessuno se ne accorga.
    /// </summary>
    private static IReadOnlyList<AgreementClauseRow> MergeClauses(IReadOnlyList<TransferPointRow> points)
    {
        var clauses = new List<AgreementClauseRow>();
        var cops = new List<string>();
        TransferPointRow? open = null;
        var order = 1;

        void Close()
        {
            if (open is null) return;
            clauses.Add(ToClause(open, CopList.Format(cops), order++));
            open = null;
            cops.Clear();
        }

        foreach (var p in points)
        {
            if (open is not null && CanMerge(open, p))
            {
                cops.Add(p.Cop);
                continue;
            }
            Close();
            open = p;
            cops.Add(p.Cop);
        }
        Close();

        return clauses;
    }

    /// <summary>
    /// Due righe consecutive si fondono in una clausola con l'elenco dei punti solo se dicono la stessa cosa —
    /// e se <b>nessuna delle due sta in un gruppo di varianti</b>.
    /// <para>⚠️ La seconda metà non è una cautela: due varianti appena create sono identiche in tutto (la
    /// condizione è esattamente ciò che chi le ha create deve ancora scrivere), quindi la fusione le
    /// scambierebbe per la stessa riga scritta due volte e <b>scioglierebbe il gruppo</b>. Dentro un gruppo
    /// ogni riga è un'alternativa distinta anche quando non ha ancora niente da dire di diverso.</para>
    /// </summary>
    private static bool CanMerge(TransferPointRow a, TransferPointRow b) =>
        a.VariantGroup is null && b.VariantGroup is null && ClauseSignature(a) == ClauseSignature(b);

    private static AgreementClauseRow ToClause(TransferPointRow p, string cops, int order) => new()
    {
        Id = p.Id,
        // Il travaso non accoppia i versi: ogni accordo nasce a un verso solo, dal mittente al ricevente.
        Direction = AgreementDirection.AtoB,
        Cops = cops,
        LevelValue = p.LevelValue,
        LevelUnit = p.LevelUnit,
        LevelConstraint = p.LevelConstraint,
        LevelSpecial = p.LevelSpecial,
        Parity = p.Parity,
        VerticalState = p.VerticalState,
        ConditionLabel = p.ConditionLabel,
        ConditionRefId = p.ConditionRefId,
        ConditionAreaLabel = p.ConditionAreaLabel,
        ConditionCustomLabel = p.ConditionCustomLabel,
        HandoffKind = p.HandoffKind,
        HandoffLabel = p.HandoffLabel,
        HandoffLevelValue = p.HandoffLevelValue,
        HandoffLevelUnit = p.HandoffLevelUnit,
        HandoffLevelConstraint = p.HandoffLevelConstraint,
        CommsHandoffKind = p.CommsHandoffKind,
        CommsHandoffLabel = p.CommsHandoffLabel,
        SpeedValue = p.SpeedValue,
        SpeedConstraint = p.SpeedConstraint,
        VariantGroup = p.VariantGroup,
        VariantDepth = p.VariantDepth,
        IsGroupWide = p.IsGroupWide,
        Order = order,
    };
}
