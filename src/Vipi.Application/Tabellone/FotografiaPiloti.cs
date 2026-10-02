using Vipi.Application.Abstractions;

namespace Vipi.Application.Tabellone;

/// <summary>
/// I piloti dell'ultima fotografia di rete, per il tabellone partenze/arrivi. Li pubblica il poller IVAO dalla
/// <b>stessa</b> lettura del whazzup che già fa una volta al minuto: il tabellone non ne aggiunge un'altra
/// (committente, 2 ottobre 2026). Singleton; pubblicazione atomica di un riferimento immutabile, come
/// <c>OnlineAtcCache</c>. Carta <c>docs/feature/2026-10-02-tabellone-partenze-arrivi.md</c>.
///
/// <para>⚠️ Non scade da sola: la regola dei tre minuti (§7 del formato) la applica il tabellone guardando
/// <see cref="Istantanea.AsOf"/>, che è la data in cui la sorgente ha <b>generato</b> la fotografia.</para>
/// </summary>
public sealed class FotografiaPiloti
{
    public sealed record Istantanea(IReadOnlyList<SourcePilotFix> Piloti, DateTimeOffset AsOf);

    public static readonly Istantanea Vuota = new(Array.Empty<SourcePilotFix>(), DateTimeOffset.MinValue);

    private Istantanea _corrente = Vuota;

    public Istantanea Corrente => Volatile.Read(ref _corrente);

    public void Pubblica(IReadOnlyList<SourcePilotFix> piloti, DateTimeOffset asOf) =>
        Volatile.Write(ref _corrente, new Istantanea(piloti, asOf));
}
