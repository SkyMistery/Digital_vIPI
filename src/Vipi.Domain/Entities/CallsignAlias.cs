using Vipi.Domain;

namespace Vipi.Domain.Entities;

/// <summary>
/// Un nominativo <b>dismesso</b>: «questo callsign era di quel settore, fino a quel giorno».
///
/// <para><b>A cosa serve, e a cosa NO.</b> Non serve a risolvere l'identità — quella la tiene l'id della
/// sorgente (<c>AccSector.IvaoId</c> / <c>AirportSector.IvaoId</c>), e da lì in poi una rinomina è un
/// <c>UPDATE</c> che nessuno a valle nota. Serve a leggere lo <b>storico</b>, dove il callsign non è un
/// puntatore ma il <b>dato</b>: <c>AtcSession.Callsign</c> dice quale nominativo un controllore ha usato
/// quella sera, e riscriverlo sarebbe falsificare un fatto. Stessa cosa per le release già pubblicate e per
/// i tag che arrivano da Aurora.</para>
///
/// <para>⚠️ <b>Non è un terzo meccanismo.</b> La lezione di «Da fare: una lista sola» vale anche qui: questa
/// tabella risponde a una domanda sola — «di chi era questo nominativo» — e ha un lettore solo. Chi si
/// trovasse a interrogarla per sapere «qual è il callsign di questo settore» sta guardando il posto
/// sbagliato: quello è <c>Sector.Callsign</c>, ed è sempre aggiornato.</para>
/// </summary>
public class CallsignAlias
{
    public int Id { get; set; }

    /// <summary>Il nominativo dismesso (es. "LIRN_US0_APP"). Univoco: un callsign è stato di uno solo.</summary>
    public string OldCallsign { get; set; } = default!;

    /// <summary>Il nominativo che l'ha sostituito al momento della rinomina (es. "LIRN_US1_APP").
    /// ⚠️ È una fotografia, non un puntatore vivo: se il settore viene rinominato una seconda volta questo
    /// resta com'era, e la catena si risale per <see cref="SectorId"/>.</summary>
    public string NewCallsign { get; set; } = default!;

    /// <summary>Da quale dei due cataloghi veniva: senza, l'<see cref="IvaoId"/> è ambiguo (due sequenze
    /// diverse alla sorgente, con intervalli che si sovrappongono).</summary>
    public SourceCatalog Catalog { get; set; }

    /// <summary>L'identità che ha attraversato la rinomina, cioè l'id della riga alla sorgente.</summary>
    public int? IvaoId { get; set; }

    /// <summary>Il settore proiettato che portava il nominativo, se ce n'era uno. Si azzera se il settore
    /// viene eliminato: l'alias resta comunque leggibile, perché lo storico che deve spiegare è ancora lì.</summary>
    public int? SectorId { get; set; }
    public Sector? Sector { get; set; }

    /// <summary>Quando la sorgente ha cambiato il nominativo (cioè quando l'import se n'è accorto).</summary>
    public DateTime RenamedAtUtc { get; set; }
}
