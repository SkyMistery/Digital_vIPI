using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// La domanda «l'editor d'aeroporto deve mandare chi è entrato all'edizione MILITARE?», da sola.
///
/// <para>🔴 <b>Perché è una funzione pura e sta fuori dal componente.</b> Stava nuda dentro
/// <c>AirportSectionsEditor.LoadAsyncCore</c>, cioè dentro il <b>ricarico</b> — e il ricarico lo fa
/// scattare ogni gesto sull'unione (<c>UnionPanel</c> → <c>Changed</c> → <c>UnioneCambiata</c> →
/// <c>RicaricaAsync</c>). Chi spostava un membro con le frecce veniva portato via <b>senza aver cliccato
/// niente</b>, perché <c>NavigateTo(..., forceLoad: true)</c> è una navigazione vera del browser.
/// Segnalato dal campo il 9 settembre 2026.</para>
///
/// <para>⚠️ Provarla montando l'editor vorrebbe dire una fixture con sei servizi, un <c>DbContext</c> e il
/// JS: è la stessa scelta già fatta per <c>UnionPanel.Filtra</c> e <c>EditorTocProjection.DaSezioni</c> —
/// la parte che si può sbagliare in silenzio si estrae e si prova da sola.</para>
/// </summary>
public static class RimandoAllEdizioneMilitare
{
    /// <param name="conChrome">Questo editor è la PAGINA. ⚠️ Falso = è un <b>membro</b> montato dentro
    /// l'unione di qualcun altro, e un membro che naviga si porta via la pagina dell'OSPITE — cioè un
    /// documento che non è suo.</param>
    /// <param name="giaValutato">La domanda è già stata fatta per questa istanza. ⚠️ Mandare qualcuno nel
    /// posto giusto è una decisione d'INGRESSO: al ricarico numero due chi guarda sta già lavorando lì, e
    /// la stessa risposta non è più una guida, è uno strappo.</param>
    /// <param name="stato">Anagrafica militare dello scalo. <c>null</c> = ICAO sconosciuto: non si manda
    /// nessuno da nessuna parte, e lo dice la pagina.</param>
    public static bool Serve(bool conChrome, bool giaValutato, AirportMilitaryState? stato) =>
        conChrome
        && !giaValutato
        // Campo SOLO militare e nessuna vIPI civile: qui non potrebbe nascere niente
        // (`EnsureDocumentAsync` lo rifiuterebbe), e un errore che non dice dove andare è peggio.
        && stato is { IsMilitaryOnly: true, DocumentId: null };
}
