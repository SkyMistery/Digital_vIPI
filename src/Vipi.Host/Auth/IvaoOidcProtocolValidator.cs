using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Vipi.Host.Auth;

/// <summary>
/// Validator OIDC adattato a IVAO. Resta <b>una sola</b> deroga alla spec: nessuna validazione della
/// userinfo, che in IVAO è <c>/v2/users/me</c> e non è una userinfo OIDC (vedi il perché sul metodo).
/// <para>Il <c>nonce</c>, invece, dal 22-ago-2026 <b>si valida</b>: misurato sul flusso reale, IVAO lo
/// mette nell'id_token (e rimanda indietro lo <c>state</c>). Il parametro del costruttore resta perché
/// la deroga sia riaccendibile da config in caso di regressione lato IVAO
/// (<c>VipiAuth:RelaxProtocolValidation</c>), non perché serva oggi.</para>
/// <para>I campioni ufficiali IVAO (<c>ivaoaero/OAuth-samples</c>, cartella <c>aspnetcore7</c>) spengono
/// ancora nonce e state: sono più vecchi del cambio al loro sistema di autenticazione.</para>
/// </summary>
public sealed class IvaoOidcProtocolValidator : OpenIdConnectProtocolValidator
{
    public IvaoOidcProtocolValidator(bool shouldValidateNonce) => ShouldValidateNonce = shouldValidateNonce;

    public override void ValidateUserInfoResponse(OpenIdConnectProtocolValidationContext validationContext)
    {
        // Nessuna validazione della userinfo. Onestà su cosa si sa e cosa no: la misura del 22-ago mostra
        // che /v2/users/me un `sub` ce l'ha, uguale a quello dell'id_token, quindi il controllo standard
        // oggi PASSEREBBE. Resta spento perché non aggiunge difesa — l'access token con cui si chiama
        // /v2/users/me esce dallo stesso scambio del code, già legato da PKCE e nonce — mentre accenderlo
        // dipende da come l'handler passa l'id_token validato su questo ramo. Costo zero, rischio non zero.
    }

    protected override void ValidateNonce(OpenIdConnectProtocolValidationContext validationContext)
    {
        if (ShouldValidateNonce) base.ValidateNonce(validationContext);
    }

    private bool ShouldValidateNonce { get; }
}
