namespace Vipi.Application.Content;

/// <summary>Sollevata quando il documento è bloccato da un altro editor (o il lock è scaduto e va riacquisito).</summary>
public sealed class EditConflictException : Exception
{
    public EditConflictException(string message) : base(message) { }
}
