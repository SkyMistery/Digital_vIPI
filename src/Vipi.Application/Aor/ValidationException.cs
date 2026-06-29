namespace Vipi.Application.Aor;

/// <summary>Errore di validazione semantica (input rifiutato con motivo leggibile). Usato dai service Application.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
