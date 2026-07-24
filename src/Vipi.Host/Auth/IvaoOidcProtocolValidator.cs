using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Vipi.Host.Auth;

/// <summary>
/// Validator OIDC adattato a IVAO: l'IdP IVAO (<c>api.ivao.aero</c>) non è pienamente conforme alla spec
/// OpenID Connect (nonce/state non sempre presenti, userinfo non standard). Replica quanto fa il sito
/// ufficiale <c>Ivao.It</c> (progetto <c>Ivao.OpenIdConnect</c>): nonce opzionale e nessuna validazione
/// rigida della userinfo, altrimenti il login fallisce con errori di protocollo.
/// </summary>
public sealed class IvaoOidcProtocolValidator : OpenIdConnectProtocolValidator
{
    public IvaoOidcProtocolValidator(bool shouldValidateNonce) => ShouldValidateNonce = shouldValidateNonce;

    public override void ValidateUserInfoResponse(OpenIdConnectProtocolValidationContext validationContext)
    {
        // IVAO restituisce la userinfo in forma non-standard: nessuna validazione rigida.
    }

    protected override void ValidateNonce(OpenIdConnectProtocolValidationContext validationContext)
    {
        if (ShouldValidateNonce) base.ValidateNonce(validationContext);
    }

    private bool ShouldValidateNonce { get; }
}
