using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// **Rete della sostituzione del modello.** Deriva i coordinamenti dai flussi VERI (fixture estratta dal
/// <c>vipi.db</c>, vedi <see cref="RealCoordinationFixture"/>) e confronta il risultato — righe di tabella e
/// frasi composte, in italiano e in inglese — con un file approvato.
/// <para>L'invariante del lavoro «Accordi di coordinamento» è che questo confronto resti verde: finché lo è,
/// vIPI ACC, vIPI APP, vLOA, vista live, stampa e matcher Aurora non possono essersi rotti, perché tutti
/// leggono queste stesse righe.</para>
/// <para>Quando il file approvato manca o non combacia, il test scrive accanto il <c>.received.txt</c> e dice
/// dov'è: si confronta a occhio, e se la differenza è voluta si sostituisce l'approvato. Un test che
/// riapprovasse da sé non sarebbe una rete, sarebbe una fotografia di qualunque cosa il codice faccia oggi.</para>
/// </summary>
public class CoordinationCharacterizationTests
{
    private static readonly CoordinationSentenceTemplate It = CoordinationSentenceTemplate.Default;

    [Fact]
    public void Real_flows_derive_the_approved_coordination()
    {
        var flows = RealCoordinationFixture.LoadFlows();
        var maps = RealCoordinationFixture.LoadMaps(flows);

        // I due blocchi che coprono i due passi della derivazione: LIBB possiede i flussi (passo 1) e riceve
        // dagli esteri (passo 2); Roma non ne possiede nessuno e vede solo ciò che le entra (passo 2 puro).
        var sb = new StringBuilder();
        sb.Append(Render("LIBB", OwnersOfAcc(maps, "Brindisi"), flows, maps, TransferFlowKindLabels.Label, It));
        sb.Append(Render("LIRR", OwnersOfAcc(maps, "Roma"), flows, maps, TransferFlowKindLabels.Label, It));
        // La vLOA rende le stesse righe col template inglese: è lì che i difetti di lingua si vedono, ed è già
        // successo (la parità attaccata con l'ordine italiano, «at level 260 even»).
        sb.Append(Render("LIBB/EN", OwnersOfAcc(maps, "Brindisi"), flows, maps, TransferFlowKindLabels.LabelEn,
                         CoordinationSentenceTemplate.English));

        Approve("real-coordination", sb.ToString());
    }

    [Fact]
    public void Fixture_carries_the_whole_archive()
    {
        // Se qualcuno riestrae il fixture da un database diverso, il resto del confronto diventa muto senza
        // dirlo: meglio che sia questa riga a parlare.
        var flows = RealCoordinationFixture.LoadFlows();
        Assert.Equal(37, flows.Count);
        Assert.Equal(78, flows.Sum(f => f.Points.Count));
    }

    // ---- resa deterministica ----

    private static IReadOnlySet<string> OwnersOfAcc(RealCoordinationFixture.Maps maps, string accName) =>
        new HashSet<string>(
            maps.AccNames.Where(kv => string.Equals(kv.Value, accName, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);

    private static string Render(string title, IReadOnlySet<string> owners, IReadOnlyList<TransferFlowRow> flows,
        RealCoordinationFixture.Maps maps, Func<TransferFlowKind, string> kindLabel, CoordinationSentenceTemplate tpl)
    {
        var entries = CoordinationDerivation.Build(flows, owners, maps.Types, maps.Names, maps.Codes,
            maps.Airports, maps.Atc, tpl);

        var sb = new StringBuilder();
        sb.Append("=== ").Append(title).Append(" · enti: ")
          .Append(string.Join(", ", owners.OrderBy(x => x, StringComparer.Ordinal)))
          .AppendLine(" ===");
        sb.Append("--- righe (").Append(entries.Count).AppendLine(") ---");

        foreach (var e in entries)
        {
            var r = e.Row;
            sb.Append(e.IsIncoming ? "<< " : ">> ")
              .Append(e.OurSectorCallsign).Append(" | ").Append(e.CounterpartCallsign)
              .Append(" (").Append(e.CounterpartType).Append(") | ").Append(e.Kind)
              .Append(" | ").AppendLine(e.AirportIcao ?? "—");
            sb.Append("   cop=").Append(r.Cop)
              .Append(" liv=").Append(r.Level)
              .Append(" next=").Append(r.Next)
              .Append(" trasf=").Append(r.Handoff)
              .Append(" livTrasf=").Append(r.HandoffLevel)
              .Append(" com=").Append(r.CommsHandoff)
              .Append(" vel=").Append(r.Speed)
              .Append(" cond=").Append(r.ConditionLabel)
              .Append(" gruppo=").Append(r.VariantGroup?.ToString() ?? "—")
              .Append('/').Append(r.VariantDepth)
              .Append(r.IsGroupWide ? "/trasversale" : "")
              .AppendLine();
            sb.Append("   «").Append(r.Sentence ?? "(nessuna frase)").AppendLine("»");
        }

        sb.AppendLine("--- albero ---");
        foreach (var s in CoordinationDerivation.BuildAccTree(entries, maps.Codes, maps.Atc, maps.Airports,
                     maps.AccNames, kindLabel))
        {
            sb.Append("  settore ").AppendLine(s.SectorLabel);
            foreach (var acc in s.Accs)
            {
                sb.Append("    acc ").AppendLine(acc.AccLabel);
                foreach (var ap in acc.Airports)
                    sb.Append("      aeroporto ").Append(ap.AirportLabel)
                      .Append(" · arrivi ").Append(ap.Arrivals.Count)
                      .Append(" · partenze ").Append(ap.Departures.Count).AppendLine();
                foreach (var ex in acc.Extras)
                    sb.Append("      extra ").Append(ex.KindLabel).Append(" · righe ").Append(ex.Rows.Count).AppendLine();
            }
        }

        return sb.AppendLine().ToString();
    }

    // ---- confronto con l'approvato ----

    private static void Approve(string name, string actual)
    {
        var dir = RealCoordinationFixture.Dir;
        var approved = Path.Combine(dir, name + ".approved.txt");
        var received = Path.Combine(dir, name + ".received.txt");

        // Fine riga normalizzata: il file approvato viaggia in git, che su Windows può riscriverlo.
        actual = actual.Replace("\r\n", "\n");

        if (!File.Exists(approved))
        {
            File.WriteAllText(received, actual);
            Assert.Fail($"Manca il file approvato «{approved}». Ne è stato scritto uno ricevuto in «{received}»: " +
                        "leggilo, e se descrive ciò che deve succedere rinominalo in .approved.txt.");
        }

        var expected = File.ReadAllText(approved).Replace("\r\n", "\n");
        if (expected == actual) return;

        File.WriteAllText(received, actual);
        Assert.Fail($"La derivazione dei flussi veri è cambiata. Confronta «{approved}» con «{received}».");
    }
}
