using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Stats;

/// <summary>Una sessione già in archivio, per quel poco che serve a decidere cosa scrivere.</summary>
public readonly record struct KnownAtcSession(
    long SessionId, int UserId, string Callsign, DateTimeOffset StartUtc, DateTimeOffset? EndUtc, long ShiftKey);

/// <summary>Riga di sessione da scrivere (creazione o aggiornamento).</summary>
public sealed record AtcSessionUpsert(
    long SessionId, int UserId, string Callsign, string? Position, string? Frequency, int Rating,
    DateTimeOffset StartUtc, int DurationSeconds, long ShiftKey, bool IsNew);

/// <summary>Sessione da chiudere: era aperta in archivio e non è più in frequenza.</summary>
public sealed record AtcSessionClosure(long SessionId, DateTimeOffset EndUtc);

/// <summary>Cosa fare dopo un giro di poll.</summary>
public sealed record AtcSessionPlan(IReadOnlyList<AtcSessionUpsert> Upserts, IReadOnlyList<AtcSessionClosure> Closures)
{
    public static readonly AtcSessionPlan Empty =
        new(Array.Empty<AtcSessionUpsert>(), Array.Empty<AtcSessionClosure>());

    public bool Nothing => Upserts.Count == 0 && Closures.Count == 0;
}

/// <summary>
/// Cosa scrivere in archivio dopo un giro di poll: quali sessioni aggiornare, quali aprire, quali chiudere.
/// Puro e deterministico — nessun I/O, nessun orologio interno (l'istante arriva dal chiamante).
///
/// <para><b>Il turno si assegna qui, alla nascita della sessione.</b> Se lo stesso VID sullo stesso callsign
/// aveva una connessione finita da poco, la nuova ne eredita la <c>ShiftKey</c>: è una caduta di linea, non
/// un turno nuovo. Misurato sulle sessioni italiane vere: succede al <b>38%</b> di loro.</para>
/// </summary>
public static class AtcSessionSync
{
    /// <summary>Distanza massima fra la fine di una connessione e l'inizio della successiva perché siano lo stesso turno.</summary>
    public static readonly TimeSpan ShiftGap = AtcShiftGrouper.DefaultGap;

    /// <summary>
    /// <paramref name="known"/> sono le sessioni che l'archivio già conosce e che possono servire: le aperte
    /// (per chiuderle) e quelle finite da poco (per il turno). Il chiamante non deve passare tutto l'archivio.
    /// </summary>
    public static AtcSessionPlan Plan(
        IReadOnlyList<SourceAtcConnection> online,
        IReadOnlyList<KnownAtcSession> known,
        DateTimeOffset now)
    {
        var perId = known.ToDictionary(k => k.SessionId);
        var upserts = new List<AtcSessionUpsert>(online.Count);
        var viste = new HashSet<long>();

        foreach (var c in online)
        {
            viste.Add(c.SessionId);
            var esiste = perId.TryGetValue(c.SessionId, out var precedente);

            upserts.Add(new AtcSessionUpsert(
                SessionId: c.SessionId,
                UserId: c.UserId,
                Callsign: c.Callsign,
                Position: c.Position,
                Frequency: c.Frequency,
                Rating: c.Rating,
                StartUtc: c.StartUtc,
                DurationSeconds: c.ConnectedSeconds,
                // Il turno si decide una volta sola: una sessione già in archivio si tiene il suo, o un
                // riavvio dell'applicazione lo riscriverebbe a ogni giro.
                ShiftKey: esiste ? precedente.ShiftKey : ShiftKeyFor(c, known),
                IsNew: !esiste));
        }

        // Chiudo quelle che l'archivio ha aperte e che non sono più in frequenza. L'istante è il nostro:
        // la fine vera la sistemerà il backfill, che legge `completedAt` dalla sorgente.
        var closures = known
            .Where(k => k.EndUtc is null && !viste.Contains(k.SessionId))
            .Select(k => new AtcSessionClosure(k.SessionId, now))
            .ToList();

        return new AtcSessionPlan(upserts, closures);
    }

    /// <summary>
    /// Turno a cui appartiene una connessione nuova: quello della connessione più recente dello stesso VID
    /// sullo stesso callsign, se è finita entro <see cref="ShiftGap"/> da questo inizio. Altrimenti la
    /// sessione apre un turno suo (chiave = il proprio id).
    /// </summary>
    private static long ShiftKeyFor(SourceAtcConnection c, IReadOnlyList<KnownAtcSession> known)
    {
        KnownAtcSession? migliore = null;

        foreach (var k in known)
        {
            if (k.UserId != c.UserId) continue;
            if (!string.Equals(k.Callsign, c.Callsign, StringComparison.OrdinalIgnoreCase)) continue;
            if (k.SessionId == c.SessionId) continue;

            // Una sessione ancora aperta non può aver ceduto il posto a questa: chiude il turno (se il
            // poller l'ha persa, sarà la chiusura di questo stesso giro a sistemarla, non un'ipotesi qui).
            if (k.EndUtc is not { } fine) continue;
            if (fine > c.StartUtc) continue;
            if (c.StartUtc - fine > ShiftGap) continue;

            if (migliore is null || fine > migliore.Value.EndUtc) migliore = k;
        }

        return migliore?.ShiftKey ?? c.SessionId;
    }
}
