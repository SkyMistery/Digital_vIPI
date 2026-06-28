namespace Vipi.Hosting;

/// <summary>
/// Mappa dei claim del sito ospitante → modello utente neutro della vIPI. Config-driven così il modulo
/// si aggancia a host diversi senza ricompilare: si adattano i nomi dei claim alla sezione "HostIdentity".
/// I default seguono i claim noti dell'OIDC IVAO.
/// </summary>
public sealed class HostIdentityOptions
{
    public const string SectionName = "HostIdentity";

    /// <summary>Claim che contiene il UserId (intero). Default: "id".</summary>
    public string UserIdClaim { get; set; } = "id";

    /// <summary>Claim del nome visualizzato. Si prova in ordine finché uno è valorizzato.</summary>
    public List<string> NameClaims { get; set; } = new() { "name", "given_name", "preferred_username" };

    /// <summary>Claim della FIR/centro (opzionale). Default: "centerId".</summary>
    public string FirClaim { get; set; } = "centerId";

    /// <summary>
    /// Claim delle posizioni staff. Può essere presente più volte (claim multipli) oppure una sola volta
    /// con un array JSON (es. <c>["IT-DIR","IT-WM"]</c>): entrambe le forme sono supportate.
    /// </summary>
    public string StaffPositionsClaim { get; set; } = "userStaffPositions";
}
