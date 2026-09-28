namespace Vipi.Domain.Entities;

/// <summary>
/// Il documento condiviso di un evento RFO, tenuto per le copie di «RFO Gate Manager» delle postazioni ATC.
/// Carta <c>docs/feature/2026-09-18-ponte-rfo-gate-manager.md</c>; contratto del programma:
/// <c>docs/SYNC-API.md</c> della repo SkyMistery/RFO-Stand-Manager.
///
/// <para>⚠️ <b>Il sito non interpreta <see cref="Data"/>.</b> È il testo JSON esatto che la postazione ha mandato,
/// e torna indietro identico: nuove funzioni del programma non devono richiedere un pacchetto del sito.</para>
///
/// <para>🔴 <b>La versione non si scrive mai da EF.</b> Controllo e scrittura stanno in UNA istruzione SQL
/// condizionata (<c>EfRfoSharedStateStore</c>): «leggo la versione, poi aggiorno» lascerebbe passare due PUT
/// simultanei, e la modifica di una postazione sparirebbe senza che nessuno se ne accorga.</para>
/// </summary>
public class RfoSharedState
{
    /// <summary><c>[a-z0-9-]{1,64}</c>, per esempio <c>lirn-20260919</c>. Chiave primaria.</summary>
    public string EventId { get; set; } = "";

    /// <summary>Parte da 1, +1 a ogni scrittura riuscita. La decide il sito, mai il client.</summary>
    public long Version { get; set; }

    /// <summary>Il JSON della postazione, byte per byte come è arrivato.</summary>
    public string Data { get; set; } = "";

    /// <summary>Chi ha scritto, detto dal client («LIRN_GND»). Al più <see cref="RfoLimits.UpdatedBy"/> caratteri.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>UTC, al millisecondo.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Una riga per ogni scrittura riuscita del documento condiviso: serve al debriefing dopo l'evento. Si scrive
/// nella stessa transazione della scrittura, quindi non esiste una versione senza la sua riga di storia.
///
/// <para>La chiave è un id proprio e non (evento, versione): se il documento viene svuotato a mano la versione
/// riparte da 1, e la storia deve tenere tutte e due le vite invece di far fallire la scrittura.</para>
/// </summary>
public class RfoSharedStateHistory
{
    public long Id { get; set; }
    public string EventId { get; set; } = "";
    public long Version { get; set; }
    public string Data { get; set; } = "";
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>I limiti del contratto, letti dal modello e dall'endpoint.</summary>
public static class RfoLimits
{
    public const int EventId = 64;
    public const int UpdatedBy = 64;

    /// <summary>Il corpo di un PUT, in byte: oltre, <c>413</c>.</summary>
    public const int CorpoMassimo = 1_048_576;

    /// <summary>
    /// Quante versioni della storia si tengono per evento: le più vecchie si tolgono a ogni scrittura. 🔴 U-104/U-121
    /// (revisione totale 3, scelta del committente il 28 settembre 2026): fino ad allora ogni scrittura aggiungeva
    /// una copia intera, fino a 1 MB, per sempre.
    /// </summary>
    public const int StoriaPerEvento = 100;

    /// <summary>Dopo quanti giorni dall'ultima scrittura di un evento la sua storia se ne va tutta: il debriefing
    /// è fatto, e restano i dati dei piloti. Stessa scelta del committente.</summary>
    public const int GiorniDiStoria = 30;

    /// <summary>Quante scritture al minuto accetta un evento, riprove dopo un 409 comprese: due al secondo, larghe
    /// per dieci postazioni. Oltre, <c>429</c> (scelta del committente, 28 settembre 2026).</summary>
    public const int ScrittureAlMinuto = 120;
}
