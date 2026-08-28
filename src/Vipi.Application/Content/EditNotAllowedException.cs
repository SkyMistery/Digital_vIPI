using static Vipi.Application.Messaggio;
namespace Vipi.Application.Content;

/// <summary>Sollevata quando l'utente non è autorizzato a editare (non admin e senza grant sulla ACC).</summary>
public sealed class EditNotAllowedException : Exception
{
    public EditNotAllowedException() : base(Lingua("Editing non consentito: serve un permesso sulla ACC (o ruolo admin).", "Editing not allowed: it needs a permission on the ACC (or the admin role).")) { }
}
