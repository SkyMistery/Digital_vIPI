namespace Vipi.Domain.Entities;

/// <summary>
/// Quante volte un documento è stato aperto in un giorno: una riga per documento e per giorno (carta
/// <c>docs/feature/2026-10-01-aperture-documenti.md</c>). Committente, 1° ottobre 2026: sulla pagina di un ACC, per
/// ogni gruppo, i tre documenti più aperti — «non importa da chi».
///
/// <para>⚠️ <b>Nessun dato di chi legge</b>: né VID né indirizzo, solo il numero. Per questo non serve una regola di
/// conservazione come quella del registro degli accessi; le righe vecchie restano e non pesano (un documento fa al
/// massimo 365 righe l'anno).</para>
///
/// <para>⚠️ <b>Per giorno e non un totale</b>: la classifica guarda gli ultimi 90 giorni, perché segua quel che si
/// usa adesso. Un totale da sempre terrebbe in cima per mesi il documento di un evento finito.</para>
/// </summary>
public class AperturaDocumento
{
    public int DocumentId { get; set; }

    /// <summary>Il giorno, in UTC, a mezzanotte.</summary>
    public DateTime Giorno { get; set; }

    public int Volte { get; set; }

    /// <summary>Il giorno di un istante: la mezzanotte UTC che lo precede.</summary>
    public static DateTime GiornoDi(DateTime utc) => DateTime.SpecifyKind(utc.ToUniversalTime().Date, DateTimeKind.Utc);
}
