using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// La regola che decide <b>cosa si può eliminare</b> fra ciò che arriva da una sorgente: si elimina solo
/// quel che la sorgente <b>non manda da due giri</b> — automatici o a mano, è uguale.
///
/// <para><b>Perché due e non uno.</b> Un giro può riuscire e tornare vuoto per un ente: una risposta a zero
/// elementi non è un errore, quindi lo stato viene marcato riuscito lo stesso e tutte le righe di
/// quell'ente restano senza timbro nuovo. Con la soglia a un giro solo, quella sera diventerebbero
/// eliminabili in blocco — è la stessa cosa che <see cref="SogliaTimbro.TroppiPerEssereVeri"/> impedisce di
/// <i>segnalare</i>, e qui impedisce di <i>cancellare</i>. Due giri consecutivi che tacciono sono un fatto;
/// uno solo è un'ipotesi.</para>
///
/// <para>Gemella di <see cref="SogliaTimbro"/>: quella dice quando una riga va <b>segnalata</b>, questa
/// quando può essere <b>tolta</b>. Stanno accanto perché sono due letture dello stesso timbro, e due
/// letture dello stesso metro in due posti diversi sono il modo in cui due racconti divergono.</para>
///
/// <para><b>La scorciatoia onesta.</b> I due giri sono un modo di <i>dedurre</i> l'assenza dal silenzio,
/// perché nessuno ha chiesto. Se invece si chiede — e la sorgente risponde, nomina altro, e questo non c'è —
/// l'assenza non è più dedotta ma constatata, e l'attesa non serve più: è il parametro
/// <c>provaDiAssenza</c>, che porta il verdetto di <see cref="Abstractions.ISourcePresenceProbe"/>. Vale
/// <b>solo</b> per il valore <see cref="Abstractions.SourcePresence.Assente"/>, che quella porta emette
/// soltanto dopo una controprova: vedi la sua documentazione per il perché un 404 da solo non basta.</para>
/// </summary>
public static class SogliaEliminazione
{
    /// <summary>
    /// ⚠️ <b>La trappola dei due clic.</b> Il penultimo giro riuscito è ciò che autorizza a eliminare:
    /// senza questa distanza minima basterebbe premere due volte di fila il bottone di re-import — cinque
    /// minuti — per «consumare» entrambe le conferme e rendere eliminabile mezzo catalogo. Un giro che
    /// arriva troppo presto dopo il precedente aggiorna l'ultimo timbro ma <b>non</b> fa scorrere il
    /// penultimo: la finestra delle due conferme resta larga almeno quanto questo intervallo.
    ///
    /// <para>Un'ora: la stessa distanza del ritentativo di <c>GatedImportLoop</c>, cioè il tempo dopo il
    /// quale un secondo giro è un giro vero e non un doppio clic.</para>
    /// </summary>
    public static readonly TimeSpan DistanzaMinimaFraGiri = TimeSpan.FromHours(1);

    /// <summary>
    /// Se il timbro del penultimo giro debba scorrere in avanti registrando questo successo. Falso quando
    /// il successo precedente è troppo recente (vedi <see cref="DistanzaMinimaFraGiri"/>).
    /// </summary>
    public static bool IlPenultimoScorre(DateTime? ultimoSuccessoUtc, DateTime adessoUtc) =>
        ultimoSuccessoUtc is not { } ultimo || adessoUtc - ultimo >= DistanzaMinimaFraGiri;

    /// <summary>
    /// Se una riga che porta un timbro d'import possa essere eliminata.
    /// </summary>
    /// <param name="importedAtUtc">Quando la sorgente l'ha nominata l'ultima volta. <c>null</c> = mai.</param>
    /// <param name="prevSuccessUtc">Il penultimo giro riuscito della categoria. <c>null</c> = non ce ne sono
    /// ancora due, e allora non si sa: «non lo sappiamo» non è «è sparita».</param>
    /// <param name="isManual">Riga aggiunta a mano: la sorgente non l'ha mai mandata, la regola non la
    /// riguarda e si elimina quando si vuole.</param>
    /// <param name="provaDiAssenza">
    /// Una domanda puntuale alla sorgente, fatta adesso, ha <b>constatato</b> che l'elemento non c'è. Salta
    /// l'attesa: i due giri servivano a dedurre ciò che qui è stato chiesto e risposto. ⚠️ Solo il verdetto
    /// <c>Assente</c> vale come prova — «non si sa» è false, e resta l'attesa.
    /// </param>
    public static bool Consentita(DateTime? importedAtUtc, DateTime? prevSuccessUtc, bool isManual,
        bool provaDiAssenza = false)
    {
        if (isManual) return true;

        // La constatazione batte la deduzione, e non ha bisogno di timbri: è di un istante fa. Il timbro
        // d'import dice quando la sorgente l'ha nominata l'ultima volta, e qui la sorgente ha appena detto
        // che non la nomina più — un confronto fra i due non aggiungerebbe niente.
        if (provaDiAssenza) return true;

        if (prevSuccessUtc is not { } penultimo) return false;

        // Nessun timbro e due giri riusciti alle spalle: né l'ultimo né il penultimo l'hanno nominata. Vale
        // anche per le righe più vecchie del timbro stesso — se fossero ancora nella sorgente, uno dei due
        // giri le avrebbe timbrate.
        if (importedAtUtc is not { } timbro) return true;

        return timbro < penultimo;
    }

    /// <summary>Il perché, in una frase, per chi legge lo schermo. <c>null</c> quando si può eliminare.</summary>
    public static string? MotivoDelRifiuto(DateTime? importedAtUtc, DateTime? prevSuccessUtc, bool isManual,
        bool provaDiAssenza = false)
    {
        if (Consentita(importedAtUtc, prevSuccessUtc, isManual, provaDiAssenza)) return null;
        if (prevSuccessUtc is null)
            return Lingua(
                "la sorgente è stata interrogata con successo meno di due volte: non c'è ancora abbastanza storia per dire che è sparita",
                "the source has been queried successfully fewer than two times: there is not enough history yet to say it is gone");
        return importedAtUtc is { } t
            ? Lingua($"la sorgente la manda ancora (vista l'ultima volta il {t:yyyy-MM-dd HH:mm}Z)",
                     $"the source still sends it (last seen on {t:yyyy-MM-dd HH:mm}Z)")
            : Lingua("la sorgente la manda ancora", "the source still sends it");
    }
}
