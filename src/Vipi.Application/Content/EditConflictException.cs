namespace Vipi.Application.Content;

/// <summary>
/// Sollevata quando il documento o la risorsa sono bloccati da un altro editor (o il lock è scaduto e va
/// riacquisito).
///
/// <para>⚠️ <b>È una <see cref="InvalidOperationException"/></b> dal 13 settembre 2026 (T-025). Le cinque pagine
/// di struttura mostrano a schermo le <c>InvalidOperationException</c> dei loro gesti, e un conflitto di lock non
/// lo potevano ricevere prima: nessun servizio lo sollevava. Senza questa parentela, il primo «sblocca comunque»
/// dopo la correzione avrebbe fatto uscire un'eccezione non gestita dal gestore del clic — il circuito che cade —
/// al posto del messaggio «lock scaduto o preso da un altro editor». Chi deve distinguerla (gli editor di
/// documento, che rileggono chi tiene il lock) la cattura <b>prima</b>.</para>
/// </summary>
public sealed class EditConflictException : InvalidOperationException
{
    public EditConflictException(string message) : base(message) { }
}
