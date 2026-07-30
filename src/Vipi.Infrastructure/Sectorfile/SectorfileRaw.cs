using System.Net;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>Accesso ai file raw del repo pubblico Aurora (nessuna auth). Condiviso dagli adapter del sectorfile.</summary>
internal static class SectorfileRaw
{
    /// <summary>
    /// GET del file <paramref name="relative"/> sotto <paramref name="baseUrl"/>. Ritorna null sul 404: un file
    /// assente è un caso normale (aeroporto senza .sid, path shape non ancora pubblicato), non un errore.
    /// </summary>
    public static async Task<string?> GetTextOrNullAsync(
        HttpClient http, string baseUrl, string relative, CancellationToken ct)
    {
        var url = baseUrl.TrimEnd('/') + "/" + relative.TrimStart('/');
        using var resp = await http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }
}
