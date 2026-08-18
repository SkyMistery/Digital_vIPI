using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

// ---- ciò che si legge: il modello VECCHIO, ridotto a ciò che serve per convertirlo ------------------

/// <summary>Un accordo com'era prima del 18 agosto 2026: due elenchi di parti, <b>un</b> tipo, <b>un</b> gruppo
/// di aeroporti, le clausole col verso addosso.</summary>
public sealed record LegacyAgreement(
    int Id, int OwnerAccId, TransferFlowKind Kind, string? Description, int Order,
    IReadOnlyList<int> SideA, IReadOnlyList<int> SideB,
    IReadOnlyList<AgreementAirportRow> Airports, IReadOnlyList<LegacyClause> Clauses);

/// <summary>Una clausola com'era: di lei serve solo la <b>posizione</b> — i dati non si toccano, la riga si
/// riappende a una sezione.</summary>
public sealed record LegacyClause(int Id, AgreementDirection Direction, int Order, int? VariantGroup, int VariantDepth);

// ---- ciò che si scrive: il piano ---------------------------------------------------------------------

/// <summary>Una clausola nella sua nuova casa: stessa riga, altro padre e altra posizione.</summary>
public sealed record PlannedClause(int ClauseId, int Order, int? VariantGroup, int VariantDepth);

/// <summary>Una sezione da creare, con le clausole che ci finiscono dentro.</summary>
/// <param name="FromAgreementIds">Da quali accordi vecchi viene: uno solo, o più d'uno se erano <b>gemelle</b>.</param>
public sealed record PlannedSection(
    TransferFlowKind Kind, AgreementDirection Direction, string? Description, int Order,
    IReadOnlyList<AgreementAirportRow> Airports, IReadOnlyList<PlannedClause> Clauses,
    IReadOnlyList<int> FromAgreementIds);

/// <summary>Un accordo nella forma nuova: la coppia canonica, e le sezioni che raccolgono i vecchi accordi.</summary>
/// <param name="KeepAgreementId">L'accordo vecchio la cui <b>riga si riusa</b>: gli id sopravvivono alla
/// conversione, così un link o un segnalibro continuano a puntare a qualcosa.</param>
/// <param name="AbsorbedAgreementIds">Gli altri accordi della stessa coppia: le loro righe spariscono.</param>
public sealed record PlannedAgreement(
    int KeepAgreementId, int OwnerAccId, int SideASectorId, int SideBSectorId, int Order,
    IReadOnlyList<PlannedSection> Sections, IReadOnlyList<int> AbsorbedAgreementIds);

/// <summary>Un accordo che la conversione <b>non</b> sa dove mettere, e perché.</summary>
public sealed record BlockedAgreement(int AgreementId, string Reason, int Clauses);

/// <summary>Il piano completo, più tutto ciò che serve a raccontarlo prima di eseguirlo.</summary>
/// <param name="Discarded">Gusci buttati via: senza un capo <b>e</b> senza clausole. Non c'è niente da salvare.</param>
/// <param name="Blocked">⚠️ Accordi con clausole ma senza due capi: la conversione <b>non procede</b> finché
/// esistono. Buttarli via perderebbe lavoro editoriale, e inventargli un capo sarebbe scrivere un accordo che
/// nessuno ha concordato.</param>
/// <param name="MergedTwins">Sezioni gemelle unite: stesso tipo, stesso verso, stessi scali.</param>
public sealed record ConversionPlan(
    IReadOnlyList<PlannedAgreement> Agreements,
    IReadOnlyList<int> Discarded,
    IReadOnlyList<BlockedAgreement> Blocked,
    IReadOnlyList<MergedTwin> MergedTwins)
{
    public bool CanRun => Blocked.Count == 0;
    public int SectionCount => Agreements.Sum(a => a.Sections.Count);
    public int ClauseCount => Agreements.Sum(a => a.Sections.Sum(s => s.Clauses.Count));
}

/// <summary>Due (o più) vecchi accordi finiti nella stessa sezione perché dicevano la stessa cosa.</summary>
public sealed record MergedTwin(IReadOnlyList<int> AgreementIds, TransferFlowKind Kind, AgreementDirection Direction,
    string Airports);

/// <summary>
/// **La conversione dal modello di ferragosto a quello a sezioni.** Fonde e ripulisce (decisione del
/// committente, 18 agosto 2026).
///
/// <para><b>Pura, e non SQL.</b> La fusione richiede di canonizzare la coppia, ribaltare i versi e unire le
/// gemelle: scriverla due volte in due dialetti — SQLite e MySQL — sarebbe due volte il rischio per lo stesso
/// risultato, su un archivio che <b>non si può rifare</b>. Qui si calcola un <b>piano</b>, lo si legge, e solo
/// dopo qualcuno lo esegue.</para>
///
/// <para><b>Cosa fa, in cinque righe.</b> Raggruppa gli accordi per coppia non orientata di enti; sceglie A e B
/// canonici (id minore = A); trasforma ogni vecchio accordo in una sezione, ribaltando il verso se il suo lato A
/// non è il nostro; unisce le sezioni identiche; butta i gusci vuoti e senza capo.</para>
///
/// <para>⚠️ <b>L'ordine si conserva a ogni livello</b>, ed è l'unica ragione per cui la rete di
/// caratterizzazione può ancora dire «la derivazione non è cambiata»: le clausole mantengono la sequenza,
/// le sezioni ereditano l'ordine del vecchio accordo, gli accordi quello del più vecchio della coppia. Un
/// riordino qui farebbe diventare rossa la rete per un motivo che non è un difetto — e la tentazione sarebbe
/// riapprovare.</para>
/// </summary>
public static class AgreementsToSections
{
    /// <summary>Il piano di conversione. Non tocca niente: dice cosa succederebbe.</summary>
    public static ConversionPlan Plan(IReadOnlyList<LegacyAgreement> agreements)
    {
        var blocked = new List<BlockedAgreement>();
        var discarded = new List<int>();
        var twins = new List<MergedTwin>();
        var pairs = new Dictionary<(int A, int B), List<LegacyAgreement>>();

        foreach (var a in agreements.OrderBy(x => x.Id))
        {
            // Più enti su un lato: in archivio non è mai successo, ma se succedesse la conversione non deve
            // scegliere da sé quale tenere — perderebbe un capo di un accordo scritto da qualcuno.
            if (a.SideA.Count > 1 || a.SideB.Count > 1)
            {
                blocked.Add(new BlockedAgreement(a.Id,
                    "ha più di un ente su un lato: il modello nuovo ne ammette uno, e sceglierlo non è una " +
                    "decisione della conversione.", a.Clauses.Count));
                continue;
            }

            var sideA = a.SideA.FirstOrDefault();
            var sideB = a.SideB.FirstOrDefault();
            if (sideA == 0 || sideB == 0)
            {
                // Un guscio senza un capo e senza clausole non porta niente: sparisce, e il rapporto lo dice.
                // Con clausole dentro, invece, si ferma tutto: quelle righe sono lavoro editoriale.
                if (a.Clauses.Count == 0) discarded.Add(a.Id);
                else blocked.Add(new BlockedAgreement(a.Id,
                    "ha clausole ma manca un capo: il modello nuovo pretende entrambi, e inventarne uno " +
                    "sarebbe scrivere un accordo che nessuno ha concordato.", a.Clauses.Count));
                continue;
            }

            if (sideA == sideB)
            {
                blocked.Add(new BlockedAgreement(a.Id,
                    "ha lo stesso ente sui due lati: non è una relazione.", a.Clauses.Count));
                continue;
            }

            var key = (A: Math.Min(sideA, sideB), B: Math.Max(sideA, sideB));
            if (!pairs.TryGetValue(key, out var bucket)) pairs[key] = bucket = new List<LegacyAgreement>();
            bucket.Add(a);
        }

        var planned = new List<PlannedAgreement>();

        foreach (var (key, bucket) in pairs.OrderBy(p => p.Value.Min(x => x.Order)).ThenBy(p => p.Value[0].Id))
        {
            var keep = bucket.OrderBy(x => x.Id).First();
            var sections = new List<PlannedSection>();

            foreach (var old in bucket.OrderBy(x => x.Order).ThenBy(x => x.Id))
            {
                // ⚠️ Il verso: se il lato A del vecchio accordo NON è quello canonico, tutti i suoi versi si
                // ribaltano. È la sola operazione che rimette a posto i tre reciproci scritti in accordi
                // separati (#13/#32, #17/#28, #23/#38) senza chiedere a nessuno quale dei due valesse.
                var flip = old.SideA[0] != key.A;

                foreach (var perDirection in DirectionsOf(old))
                {
                    var direction = flip ? SectionDirection.Flip(perDirection.Key) : perDirection.Key;
                    var clauses = perDirection.OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();
                    sections.Add(new PlannedSection(old.Kind, direction, Blank(old.Description), old.Order,
                        old.Airports.OrderBy(x => x.Order).ToList(),
                        clauses.Select(c => new PlannedClause(c.Id, c.Order, c.VariantGroup, c.VariantDepth)).ToList(),
                        new[] { old.Id }));
                }
            }

            sections = MergeTwins(sections, twins);

            // L'ordine imposto si applica UNA VOLTA qui, così l'archivio nasce già ordinato come si legge — e
            // l'`Order` salvato resta un numero crescente, non l'ordine ereditato di due accordi diversi.
            var ordered = sections
                .OrderBy(s => AgreementSectionOrder.KeyOf(s.Kind, s.Direction, Label(s.Airports), s.Order,
                                                          s.FromAgreementIds[0]))
                .Select((s, i) => s with { Order = i + 1 })
                .ToList();

            planned.Add(new PlannedAgreement(
                keep.Id, keep.OwnerAccId, key.A, key.B, keep.Order, ordered,
                bucket.Where(x => x.Id != keep.Id).Select(x => x.Id).OrderBy(x => x).ToList()));
        }

        return new ConversionPlan(planned, discarded, blocked, twins);
    }

    /// <summary>
    /// Le clausole di un vecchio accordo, raggruppate per verso. Un accordo <b>senza</b> clausole produce
    /// comunque una sezione, nel verso in cui era scritto: è lavoro in corso (in archivio, le partenze
    /// LIBD·LIBR appena create dal committente), non spazzatura.
    /// </summary>
    private static IEnumerable<IGrouping<AgreementDirection, LegacyClause>> DirectionsOf(LegacyAgreement a) =>
        a.Clauses.Count > 0
            ? a.Clauses.GroupBy(c => c.Direction).OrderBy(g => g.Key)
            : new[] { new EmptyGroup(AgreementDirection.AtoB) };

    /// <summary>
    /// Le sezioni che dicono la stessa cosa diventano una: stesso tipo, stesso verso, stessi scali. Le clausole
    /// si accodano nell'ordine dei vecchi accordi e i gruppi di varianti si <b>rinumerano</b> — riusarli
    /// farebbe sembrare le clausole arrivate varianti di quelle che c'erano già.
    /// </summary>
    private static List<PlannedSection> MergeTwins(List<PlannedSection> sections, List<MergedTwin> log)
    {
        var merged = new List<PlannedSection>();

        foreach (var bucket in sections.GroupBy(s => (s.Kind, s.Direction, Airports: Label(s.Airports))))
        {
            var parts = bucket.ToList();
            if (parts.Count == 1) { merged.Add(parts[0]); continue; }

            log.Add(new MergedTwin(parts.SelectMany(p => p.FromAgreementIds).OrderBy(x => x).ToList(),
                bucket.Key.Kind, bucket.Key.Direction, bucket.Key.Airports));

            var clauses = new List<PlannedClause>();
            var order = 0;
            var nextGroup = 0;
            foreach (var part in parts)
            {
                var map = new Dictionary<int, int>();
                foreach (var c in part.Clauses)
                {
                    int? g = null;
                    if (c.VariantGroup is int old)
                    {
                        if (!map.TryGetValue(old, out var mapped)) map[old] = mapped = ++nextGroup;
                        g = mapped;
                    }
                    clauses.Add(c with { Order = ++order, VariantGroup = g });
                }
            }

            // Le prose si tengono tutte: due sezioni gemelle possono averne una ciascuna, e scegliere quale
            // buttare non è una decisione della conversione.
            var descriptions = parts.Select(p => p.Description).Where(d => d is not null)
                .Distinct(StringComparer.Ordinal).ToList();

            merged.Add(parts[0] with
            {
                Description = descriptions.Count == 0 ? null : string.Join(" — ", descriptions),
                Clauses = clauses,
                FromAgreementIds = parts.SelectMany(p => p.FromAgreementIds).OrderBy(x => x).ToList(),
            });
        }

        return merged;
    }

    private static string Label(IReadOnlyList<AgreementAirportRow> airports) =>
        string.Join(" · ", airports.Select(a => a.Icao.Trim().ToUpperInvariant()).OrderBy(x => x, StringComparer.Ordinal));

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Un verso senza clausole: serve a far nascere la sezione di un accordo ancora vuoto.</summary>
    private sealed class EmptyGroup : IGrouping<AgreementDirection, LegacyClause>
    {
        public EmptyGroup(AgreementDirection key) => Key = key;
        public AgreementDirection Key { get; }
        public IEnumerator<LegacyClause> GetEnumerator() => Enumerable.Empty<LegacyClause>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
