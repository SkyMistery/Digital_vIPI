using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il travaso flussi → accordi e la sua proiezione all'indietro.
///
/// <para><b>Il cancello del lavoro</b> è <see cref="Il_giro_completo_sui_dati_veri_non_cambia_niente"/>: sui 78
/// punti veri in archivio, derivare i flussi e derivare gli accordi ottenuti da quei flussi deve produrre lo
/// <b>stesso insieme</b> di righe e di frasi. Non la stessa sequenza — la fusione degli aeroporti e la
/// separazione per ricevente rimescolano l'ordine dei flussi apposta, ed è metà del punto — ma lo stesso
/// contenuto, riga per riga e parola per parola.</para>
/// </summary>
public class FlowsToAgreementsTests
{
    private static readonly CoordinationSentenceTemplate Tpl = CoordinationSentenceTemplate.Default;

    // ---- il cancello ---------------------------------------------------------------------------------

    [Fact]
    public void Il_giro_completo_sui_dati_veri_non_cambia_niente()
    {
        var flows = RealCoordinationFixture.LoadFlows();
        var maps = RealCoordinationFixture.LoadMaps(flows);
        var roundTripped = AgreementExpansion.Expand(FlowsToAgreements.Convert(flows));

        foreach (var accName in new[] { "Brindisi", "Roma" })
        {
            var owners = OwnersOfAcc(maps, accName);
            Assert.Equal(Snapshot(flows, owners, maps), Snapshot(roundTripped, owners, maps));
        }
    }

    [Fact]
    public void Il_giro_completo_conserva_ogni_riga()
    {
        var flows = RealCoordinationFixture.LoadFlows();
        var roundTripped = AgreementExpansion.Expand(FlowsToAgreements.Convert(flows));

        // Il numero di FLUSSI cambia (gli aeroporti si fondono, i riceventi misti si separano): è il lavoro.
        // Il numero di RIGHE no — una riga persa sarebbe un accordo che dice meno di prima.
        Assert.Equal(flows.Sum(f => f.Points.Count), roundTripped.Sum(f => f.Points.Count));
    }

    // ---- le tre operazioni del travaso, una per una ---------------------------------------------------

    [Fact]
    public void Un_flusso_con_riceventi_diversi_diventa_due_accordi()
    {
        // Il caso vero: gli arrivi LIRN vanno per meta' all'APP e per meta' al CTR. Sono due accordi, e il
        // modello vecchio non sapeva dirlo.
        var flow = Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN",
            Point(1, "AMSOR", 200, "LIRN_US0_APP"),
            Point(2, "LUNAR", 210, "LIRN_US0_APP"),
            Point(3, "VEGAN", 210, "LIRR_TS_CTR"));

        var a = FlowsToAgreements.Convert(new[] { flow });

        Assert.Equal(2, a.Count);
        Assert.Equal(new[] { "LIRN_US0_APP", "LIRR_TS_CTR" }, a.Select(Receiver));
        // Il primo ha due punti sullo stesso livello? No: 200 e 210. Restano due clausole.
        Assert.Equal(2, a[0].Clauses.Count);
        Assert.Single(a[1].Clauses);
    }

    [Fact]
    public void Flussi_uguali_su_aeroporti_diversi_diventano_un_accordo_con_piu_aeroporti()
    {
        var flows = new[]
        {
            Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRF", Point(1, "ASPIR", 210, "LIRR_US_CTR")),
            Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRA", Point(2, "ASPIR", 210, "LIRR_US_CTR")),
        };

        var a = Assert.Single(FlowsToAgreements.Convert(flows));
        Assert.Equal(new[] { "LIRF", "LIRA" }, a.Airports.Select(x => x.Icao));
        Assert.Single(a.Clauses);
    }

    [Fact]
    public void Un_campo_diverso_impedisce_la_fusione_degli_aeroporti()
    {
        // In archivio gli arrivi via ASPIR cambiano PARITA' da un aeroporto all'altro: fonderli perderebbe
        // meta' del dato senza dirlo, e la firma della riga esiste per impedirlo.
        var flows = new[]
        {
            Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRF", Point(1, "ASPIR", 210, "LIRR_US_CTR", parity: LevelParity.Odd)),
            Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRA", Point(2, "ASPIR", 210, "LIRR_US_CTR", parity: LevelParity.Even)),
        };

        Assert.Equal(2, FlowsToAgreements.Convert(flows).Count);
    }

    [Fact]
    public void Righe_consecutive_uguali_tranne_il_cop_diventano_una_clausola_con_l_elenco()
    {
        var flow = Flow("LIBB_ES_CTR", TransferFlowKind.Overflight, null,
            Point(1, "TIGRA", null, "LGGG_W_CTR", constraint: LevelConstraint.AtOrBelow),
            Point(2, "NOSTO", null, "LGGG_W_CTR", constraint: LevelConstraint.AtOrBelow),
            Point(3, "LATAN", null, "LGGG_W_CTR", constraint: LevelConstraint.AtOrBelow));

        var a = Assert.Single(FlowsToAgreements.Convert(new[] { flow }));
        var c = Assert.Single(a.Clauses);
        Assert.Equal("TIGRA, NOSTO, LATAN", c.Cops);
        Assert.Equal(3, CopList.Count(c.Cops));
    }

    [Fact]
    public void La_fusione_dei_punti_e_solo_fra_righe_consecutive()
    {
        // Fondere la prima e la terza salterebbe la seconda, cioe' cambierebbe l'ORDINE — che nell'outline
        // delle varianti e' la struttura, non la presentazione.
        var flow = Flow("LIBB_ES_CTR", TransferFlowKind.Overflight, null,
            Point(1, "TIGRA", 100, "LGGG_W_CTR"),
            Point(2, "NOSTO", 200, "LGGG_W_CTR"),
            Point(3, "LATAN", 100, "LGGG_W_CTR"));

        var a = Assert.Single(FlowsToAgreements.Convert(new[] { flow }));
        Assert.Equal(new[] { "TIGRA", "NOSTO", "LATAN" }, a.Clauses.Select(c => c.Cops));
    }

    [Fact]
    public void I_due_versi_non_vengono_accoppiati_dal_travaso()
    {
        // Sarebbe la fusione piu' vistosa, ed e' proprio per questo che non si fa da sola: in archivio le due
        // liste di punti NON coincidono, quindi accoppiarle vorrebbe dire scegliere quale delle due vale.
        var flows = new[]
        {
            Flow("LIBB_ES_CTR", TransferFlowKind.Overflight, null, Point(1, "TIGRA", null, "LGGG_W_CTR")),
            Flow("LGGG_W_CTR", TransferFlowKind.Overflight, null, Point(2, "TIGRA", null, "LIBB_ES_CTR")),
        };

        var a = FlowsToAgreements.Convert(flows);
        Assert.Equal(2, a.Count);
        Assert.All(a, x => Assert.All(x.Clauses, c => Assert.Equal(AgreementDirection.AtoB, c.Direction)));
    }

    [Fact]
    public void Un_accordo_senza_clausole_sopravvive_al_travaso()
    {
        // In archivio ce n'e' uno: un sorvolo di Roma NE con l'intestazione scritta e nessuna riga. Buttarlo
        // via sarebbe perdere lavoro editoriale in silenzio.
        var a = Assert.Single(FlowsToAgreements.Convert(new[] { Flow("LIRR_NE_CTR", TransferFlowKind.Overflight, null) }));
        Assert.Empty(a.Clauses);
        Assert.Single(a.Parties);   // solo il lato A: non c'e' nessun ricevente da cui dedurre il lato B
    }

    // ---- l'espansione --------------------------------------------------------------------------------

    [Fact]
    public void L_espansione_moltiplica_per_aeroporto_e_per_punto()
    {
        var flows = new[]
        {
            Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRF", Point(1, "ASPIR", 210, "LIRR_US_CTR")),
            Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRA", Point(2, "ASPIR", 210, "LIRR_US_CTR")),
        };
        var expanded = AgreementExpansion.Expand(FlowsToAgreements.Convert(flows));

        Assert.Equal(2, expanded.Count);                                  // un flusso per aeroporto
        Assert.Equal(new[] { "LIRF", "LIRA" }, expanded.Select(f => f.AirportIcao));
        Assert.All(expanded, f => Assert.Equal("LIRR_US_CTR", Assert.Single(f.Points).NextSectorCallsign));
    }

    [Fact]
    public void L_accordo_che_vale_per_piu_mittenti_si_espande_su_ognuno()
    {
        // E' la duplicazione che il modello vecchio costringeva a scrivere a mano, e che in pratica non veniva
        // scritta: l'accordo stava su un settore solo, e le vIPI degli altri restavano mute.
        var a = new AgreementRow
        {
            Id = 1, OwnerAccCode = "LIRR", TrafficKind = TransferFlowKind.Arrival, Order = 1,
            Parties = new[]
            {
                new AgreementPartyRow(AgreementSide.A, 10, "LIRR_NE_CTR", 1),
                new AgreementPartyRow(AgreementSide.A, 11, "LIRR_NC_CTR", 2),
                new AgreementPartyRow(AgreementSide.B, 20, "LIRR_US_CTR", 1),
            },
            Airports = new[] { new AgreementAirportRow("LIRF", null, 1) },
            Clauses = new[] { Clause(1, "ASPIR", 210) },
        };

        var expanded = AgreementExpansion.Expand(new[] { a });
        Assert.Equal(new[] { "LIRR_NE_CTR", "LIRR_NC_CTR" }, expanded.Select(f => f.OwningSectorCallsign));
    }

    [Fact]
    public void Il_verso_opposto_scambia_mittente_e_ricevente()
    {
        var a = new AgreementRow
        {
            Id = 1, OwnerAccCode = "LIBB", TrafficKind = TransferFlowKind.Overflight, Order = 1,
            Parties = new[]
            {
                new AgreementPartyRow(AgreementSide.A, 10, "LIBB_ES_CTR", 1),
                new AgreementPartyRow(AgreementSide.B, 20, "LGGG_W_CTR", 1),
            },
            Airports = Array.Empty<AgreementAirportRow>(),
            Clauses = new[]
            {
                Clause(1, "TIGRA", 300),
                Clause(2, "TIGRA", 310) with { Direction = AgreementDirection.BtoA },
            },
        };

        var expanded = AgreementExpansion.Expand(new[] { a });
        Assert.Equal(2, expanded.Count);
        Assert.Equal("LIBB_ES_CTR", expanded[0].OwningSectorCallsign);
        Assert.Equal("LGGG_W_CTR", Assert.Single(expanded[0].Points).NextSectorCallsign);
        Assert.Equal("LGGG_W_CTR", expanded[1].OwningSectorCallsign);
        Assert.Equal("LIBB_ES_CTR", Assert.Single(expanded[1].Points).NextSectorCallsign);
    }

    [Fact]
    public void Un_accordo_senza_ricevente_espande_comunque_le_sue_righe()
    {
        // Una riga senza ricevente finisce a UNICOM, ed e' esattamente quella che il filtro «senza ricevente»
        // deve poter trovare: farla sparire qui la renderebbe introvabile.
        var a = new AgreementRow
        {
            Id = 1, OwnerAccCode = "LIBB", TrafficKind = TransferFlowKind.Overflight, Order = 1,
            Parties = new[] { new AgreementPartyRow(AgreementSide.A, 10, "LIBB_ES_CTR", 1) },
            Airports = Array.Empty<AgreementAirportRow>(),
            Clauses = new[] { Clause(1, "GISAM", null) },
        };

        var p = Assert.Single(Assert.Single(AgreementExpansion.Expand(new[] { a })).Points);
        Assert.Null(p.NextSectorId);
        Assert.Null(p.NextSectorCallsign);
    }


    [Fact]
    public void La_misura_del_travaso_sui_dati_veri()
    {
        // Cosa il travaso fa DAVVERO all'archivio, non cosa ci si aspetta che faccia. Numeri misurati il
        // 16 agosto 2026: se cambiano, o e' cambiato l'archivio (si riestrae il fixture) o e' cambiata la
        // regola di fusione — e in quel caso va guardata, non riapprovata.
        var flows = RealCoordinationFixture.LoadFlows();
        var agreements = FlowsToAgreements.Convert(flows);

        // Piu' accordi che flussi: la separazione per ricevente ne crea piu' di quanti la fusione degli
        // aeroporti ne tolga. Non e' un difetto — sono accordi che erano scritti insieme pur non essendolo.
        Assert.Equal(41, agreements.Count);

        // 78 righe diventano 63 clausole: un quinto di scrittura in meno su un archivio piccolo e quasi tutto
        // fatto di casi singoli, e viene tutto dalla fusione dei punti consecutivi — i sorvoli di confine, che
        // dicono la stessa cosa su sette fix. Il guadagno grosso non e' qui: e' che un accordo ha UN ricevente,
        // puo' valere per piu' mittenti e ha un verso.
        Assert.Equal(63, agreements.Sum(a => a.Clauses.Count));

        Assert.Equal(4, agreements.Count(a => a.Airports.Count > 1));
        Assert.Equal(5, agreements.Count(a => a.Clauses.Any(c => CopList.Count(c.Cops) > 1)));
        // Due accordi senza lato B: sono le righe che finiscono a UNICOM, quelle che il filtro deve trovare.
        Assert.Equal(2, agreements.Count(a => a.Parties.All(p => p.Side != AgreementSide.B)));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static string Receiver(AgreementRow a) =>
        a.Parties.Single(p => p.Side == AgreementSide.B).Callsign;

    private static AgreementClauseRow Clause(int id, string cops, int? level) => new()
    {
        Id = id, Direction = AgreementDirection.AtoB, Cops = cops, LevelValue = level,
        LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.Exact, Order = id,
    };

    private static TransferPointRow Point(int id, string cop, int? level, string? next,
        LevelConstraint constraint = LevelConstraint.Exact, LevelParity parity = LevelParity.Any) => new()
    {
        Id = id, Cop = cop, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = constraint,
        Parity = parity,
        LevelText = LevelFormatting.Format(level, LevelUnit.Fl, constraint, null, parity, TransferVerticalState.Unspecified),
        NextSectorId = next is null ? null : id + 1000,
        NextSectorCallsign = next,
        Order = id,
    };

    private static TransferFlowRow Flow(string owner, TransferFlowKind kind, string? apt, params TransferPointRow[] points) => new()
    {
        Id = 1, AccCode = "LIBB", OwningSectorId = 1, OwningSectorCallsign = owner, Kind = kind,
        AirportIcao = apt, Order = 1, Points = points,
    };

    private static IReadOnlySet<string> OwnersOfAcc(RealCoordinationFixture.Maps maps, string accName) =>
        new HashSet<string>(
            maps.AccNames.Where(kv => string.Equals(kv.Value, accName, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Il contenuto derivato, ordinato: ogni riga con tutte le sue colonne e la sua frase. Ordinato perché
    /// l'ordine dei FLUSSI cambia apposta (aeroporti fusi, riceventi separati) mentre il contenuto no — e un
    /// confronto di sequenze fallirebbe per la ragione sbagliata, nascondendo quella giusta.
    /// <para>Un difetto nell'outline resta visibile lo stesso: la catena delle condizioni entra nella FRASE, e
    /// la frase è qui dentro.</para>
    /// </summary>
    private static string Snapshot(IReadOnlyList<TransferFlowRow> flows, IReadOnlySet<string> owners,
        RealCoordinationFixture.Maps maps)
    {
        var entries = CoordinationDerivation.Build(flows, owners, maps.Types, maps.Names, maps.Codes,
            CoordinationDerivation.MergeAirportNames(maps.Airports, flows), maps.Atc, Tpl);

        var lines = entries.Select(e =>
        {
            var r = e.Row;
            var sb = new StringBuilder();
            sb.Append(e.IsIncoming ? "<<" : ">>").Append('|')
              .Append(e.OurSectorCallsign).Append('|').Append(e.CounterpartCallsign).Append('|')
              .Append(e.Kind).Append('|').Append(e.AirportIcao ?? "-").Append('|')
              .Append(r.Cop).Append('|').Append(r.Level).Append('|').Append(r.Next).Append('|')
              .Append(r.Handoff).Append('|').Append(r.HandoffLevel).Append('|').Append(r.CommsHandoff).Append('|')
              .Append(r.Speed).Append('|').Append(r.ConditionLabel).Append('|')
              .Append(r.VariantDepth).Append(r.IsGroupWide ? "*" : "").Append('|')
              .Append(r.Sentence);
            return sb.ToString();
        }).OrderBy(x => x, StringComparer.Ordinal);

        return string.Join("\n", lines);
    }
}
