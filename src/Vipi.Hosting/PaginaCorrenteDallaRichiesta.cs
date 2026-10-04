using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Vipi.Ui;

namespace Vipi.Hosting;

/// <summary>La pagina servita, dall'endpoint della richiesta in corso (vedi <see cref="IPaginaCorrente"/>).</summary>
internal sealed class PaginaCorrenteDallaRichiesta(IHttpContextAccessor http) : IPaginaCorrente
{
    public Type? Tipo => http.HttpContext?.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>()?.Type;
}
