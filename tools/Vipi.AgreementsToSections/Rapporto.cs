using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.AgreementsToSections;

/// <summary>
/// Il piano, a video, riga per riga.
///
/// <para>⚠️ Esiste perché questa conversione <b>non è invertibile riga per riga</b>: quaranta accordi diventano
/// diciassette, e il <c>Down</c> di nessuna migrazione li rifà. L'unica difesa vera è <b>guardare</b> cosa sta
/// per succedere — e per guardarlo bisogna che sia scritto in una forma che si legge, non in un conteggio.</para>
/// </summary>
public static class Rapporto
{
    public static void Stampa(ConversionPlan piano, IReadOnlyList<LegacyAgreement> legacy)
    {
        var prima = legacy.SelectMany(a => a.Clauses.Select(c => c.Id)).ToHashSet();
        var dopo = piano.Agreements.SelectMany(a => a.Sections).SelectMany(s => s.Clauses)
            .Select(c => c.ClauseId).ToHashSet();

        Console.WriteLine();
        Console.WriteLine($"Piano: {legacy.Count} accordi → {piano.Agreements.Count} coppie, "
                          + $"{piano.SectionCount} sezioni, {piano.ClauseCount} clausole.");

        foreach (var a in piano.Agreements)
        {
            var da = a.AbsorbedAgreementIds.Count > 0
                ? $"#{a.KeepAgreementId} + {string.Join(" + ", a.AbsorbedAgreementIds.Select(x => "#" + x))}"
                : $"#{a.KeepAgreementId}";
            Console.WriteLine();
            Console.WriteLine($"  {da}  →  settori {a.SideASectorId} ⇄ {a.SideBSectorId}");
            foreach (var s in a.Sections)
            {
                var apt = s.Airports.Count > 0 ? " · " + string.Join(" · ", s.Airports.Select(x => x.Icao)) : "";
                var verso = s.Direction == AgreementDirection.AtoB ? "A→B" : "B→A";
                Console.WriteLine($"      {s.Order}. {s.Kind} {verso}{apt}  —  {s.Clauses.Count} clausole"
                                  + $"  (da {string.Join(", ", s.FromAgreementIds.Select(x => "#" + x))})");
            }
        }

        if (piano.MergedTwins.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Sezioni GEMELLE unite (stesso traffico, stesso verso, stessi scali):");
            foreach (var t in piano.MergedTwins)
                Console.WriteLine($"  {string.Join(" + ", t.AgreementIds.Select(x => "#" + x))}"
                                  + $"  →  {t.Kind} {t.Direction}"
                                  + (t.Airports.Length > 0 ? $" · {t.Airports}" : ""));
        }

        if (piano.Discarded.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Gusci ELIMINATI (senza un capo e senza clausole: non c'era niente da salvare):");
            foreach (var id in piano.Discarded) Console.WriteLine($"  #{id}");
        }

        if (piano.Blocked.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚠ FERMI — vanno sistemati a mano, e finché ci sono la conversione non parte:");
            foreach (var b in piano.Blocked)
                Console.WriteLine($"  #{b.AgreementId} ({b.Clauses} clausole): {b.Reason}");
        }

        // ⚠️ La prova che conta: le clausole prima e dopo sono le STESSE righe. Un conteggio uguale non
        // basterebbe — due clausole perse e due duplicate darebbero lo stesso numero.
        Console.WriteLine();
        var perse = prima.Except(dopo).ToList();
        var apparse = dopo.Except(prima).ToList();
        if (perse.Count == 0 && apparse.Count == 0)
        {
            Console.WriteLine($"Clausole: tutte e {prima.Count} ritrovate, nessuna persa e nessuna inventata.");
        }
        else
        {
            if (perse.Count > 0) Console.WriteLine($"⚠ PERSE: {string.Join(", ", perse)}");
            if (apparse.Count > 0) Console.WriteLine($"⚠ INVENTATE: {string.Join(", ", apparse)}");
        }
    }
}
