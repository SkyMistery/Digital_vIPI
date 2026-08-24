using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Fa parlare un singleton con un catalogo che vive in uno scope.
///
/// <para>Il registratore del traffico è singleton perché lo stato delle tratte in corso sta in memoria fra un
/// giro di poll e l'altro; il catalogo invece legge dal <c>DbContext</c>, che è scoped. Iniettare il secondo
/// nel primo sarebbe una <i>captive dependency</i>: un contesto tenuto vivo per giorni, con la sua cache che
/// invecchia e i suoi guai di concorrenza. Qui lo scope si apre e si chiude a ogni lettura — che è una
/// all'ora, non una al minuto.</para>
/// </summary>
public sealed class ScopedSectorVolumeCatalog : ISectorVolumeCatalog
{
    private readonly IServiceScopeFactory _scopes;

    public ScopedSectorVolumeCatalog(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var catalogo = scope.ServiceProvider.GetRequiredService<ISectorVolumeCatalog>();
        return await catalogo.GetAllAsync(ct);
    }
}
