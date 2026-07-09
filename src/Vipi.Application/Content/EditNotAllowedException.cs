namespace Vipi.Application.Content;

/// <summary>Sollevata quando l'utente non è autorizzato a editare (non admin e senza grant sulla ACC).</summary>
public sealed class EditNotAllowedException : Exception
{
    public EditNotAllowedException() : base("Editing non consentito: serve un permesso sulla ACC (o ruolo admin).") { }
}
