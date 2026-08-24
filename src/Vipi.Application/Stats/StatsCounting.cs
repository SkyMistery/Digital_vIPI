using System;

namespace Vipi.Application.Stats;

/// <summary>
/// Le soglie del conteggio: cosa entra nei numeri mostrati e cosa resta solo in archivio.
///
/// <para>Sono decisioni, non dettagli tecnici, e stanno in un posto solo perché le usano sia le pagine sia
/// i giri di fondo — e perché il giorno che una cambia, cambia in un punto.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class StatsCounting
{
    /// <summary>
    /// Durata sotto la quale una connessione ATC non entra nei conteggi (committente 24-ago-2026: 60 s).
    ///
    /// <para>⚠️ Misurato su 1316 sessioni ATC italiane vere di 30 giorni: <b>419 durano meno di 5 minuti,
    /// di cui 231 meno di un minuto</b>. Contarle tutte come «sessioni» gonfia il numero di un terzo con
    /// connessioni che sono entrate e uscite. La riga si <b>scrive lo stesso</b> — è il dato che IVAO ci dà,
    /// e serve a ricostruire i turni — ma non fa numero.</para>
    /// </summary>
    public static readonly TimeSpan MinCountedSession = TimeSpan.FromSeconds(60);

    /// <summary>Vero se la connessione è abbastanza lunga da entrare nei conteggi.</summary>
    public static bool CountsAsSession(int durationSeconds) =>
        durationSeconds >= MinCountedSession.TotalSeconds;

    /// <summary>Vero se la connessione è abbastanza lunga da entrare nei conteggi.</summary>
    public static bool CountsAsSession(TimeSpan duration) => duration >= MinCountedSession;
}
