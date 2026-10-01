namespace Vipi.Domain.Entities;

/// <summary>
/// Chi sta controllando con un account dell'evento, salvato perché un riavvio del sito non lo faccia riscrivere
/// (committente, 1 ottobre 2026; carta <c>docs/feature/2026-10-01-account-evento.md</c> §4). Una riga per persona.
/// <para>⚠️ La copia che conta mentre il sito gira è quella in memoria (<c>AccountEventoRegistro</c>): la vista live la
/// interroga a ogni giro del feed, e una lettura dal database ogni volta non avrebbe senso. Questa tabella serve solo a
/// rimetterla in piedi all'avvio.</para>
/// </summary>
public class AccountEventoInUso
{
    /// <summary>Il VID con cui la persona è entrata nel sito: la chiave.</summary>
    public int VidPersonale { get; set; }

    /// <summary>Il VID dell'account dell'evento che sta usando.</summary>
    public int VidEvento { get; set; }

    /// <summary>Fino a quando vale (UTC): la fine dell'evento, o dodici ore.</summary>
    public DateTime ScadeUtc { get; set; }
}
