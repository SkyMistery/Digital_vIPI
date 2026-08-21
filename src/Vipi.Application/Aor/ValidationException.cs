namespace Vipi.Application.Aor;

/// <summary>
/// Errore di validazione semantica (input rifiutato con motivo leggibile). Usato dai service Application.
///
/// <para><b>Il messaggio è grezzo, la chiave è per chi lo mostra.</b> Un service non ha (e non deve avere)
/// una lingua d'interfaccia: gira anche dai job d'avvio e dai test, dove una cultura non c'è, e il suo
/// messaggio finisce nei log. Ma la stessa frase la legge anche un utente in pagina inglese — e fino al
/// 22 agosto 2026 la leggeva in italiano. Da qui la coppia: <see cref="Exception.Message"/> resta il testo
/// grezzo per log e test, <see cref="Key"/> è la chiave con cui la UI lo traduce.</para>
///
/// <para>È lo stesso patto di <c>ConsistencyNarrator</c> per i rilievi di diagnostica: ⚠️ chiave
/// sconosciuta ⇒ si mostra il testo grezzo, mai il nome della chiave e mai una riga vuota. La chiave è
/// facoltativa: i punti che non l'hanno ancora si comportano come prima, e la prendono quando qualcuno li
/// tocca.</para>
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }

    /// <param name="message">Testo grezzo (log, test, contesti senza cultura).</param>
    /// <param name="key">Chiave di traduzione nelle risorse condivise.</param>
    /// <param name="args">Argomenti della frase tradotta: ⚠️ sono <b>valori</b>, non chiavi.</param>
    public ValidationException(string message, string key, params object[] args) : base(message)
    {
        Key = key;
        Args = args;
    }

    /// <summary>Chiave di traduzione, se il punto che ha rifiutato ne ha dichiarata una.</summary>
    public string? Key { get; }

    /// <summary>Argomenti della frase tradotta (vuoto se la frase non ne ha).</summary>
    public object[]? Args { get; }
}
