using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO della porta <see cref="INotamSource"/>: i NOTAM della divisione da
/// <c>/v2/divisions/{id}/notams/all</c>. Carta <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>.
///
/// <para><b>L'endpoint è pubblico</b>: nessun token, nessuno scope. Misurato il 12 ottobre 2026: 4 voci, 3 KB sul
/// filo, 0,16 s. Ogni voce porta già l'elenco degli aeroporti e dei centri che riguarda.</para>
///
/// <para>🔴 <b>L'elenco contiene anche gli scaduti</b> (uno del 31 agosto era ancora lì a ottobre), e il filtro
/// «solo attivi» della sorgente non si può usare: vedi <see cref="IvaoOptions.DivisionNotamsPathFormat"/>. Qui si
/// copia tutto com'è; chi è in vigore lo decide chi legge.</para>
///
/// <para>⚠️ I nostri NOTAM <b>non</b> cominciano tutti per <c>IT-</c> (<c>ITW004/26</c> non ha il trattino): si
/// chiede la divisione, non si filtra per nome.</para>
/// </summary>
public sealed class IvaoNotamClient : INotamSource
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;
    private readonly DivisionOptions _div;

    public IvaoNotamClient(IvaoHttp http, IOptions<IvaoOptions> opt, IOptions<DivisionOptions> div)
    {
        _http = http;
        _opt = opt.Value;
        _div = div.Value;
    }

    public async Task<IReadOnlyList<NotamDaSorgente>> GetDivisionNotamsAsync(CancellationToken ct = default)
    {
        var path = string.Format(CultureInfo.InvariantCulture, _opt.DivisionNotamsPathFormat,
                                 Uri.EscapeDataString(_div.Code));

        // Pubblico: senza token (U-025, vedi IvaoHttp.SendGetPubblicoAsync).
        using var res = await _http.SendGetPubblicoAsync(path, ct);
        // ⚠️ Anche il 404: qui non vuol dire «nessun NOTAM» (quello è un elenco vuoto, con 200) ma «percorso o
        // divisione sbagliati», e prenderlo per vuoto toglierebbe i NOTAM da ogni documento.
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"IVAO {(int)res.StatusCode} {res.StatusCode} su {path}: NOTAM non letti.", null, res.StatusCode);

        return Leggi(await res.Content.ReadAsStringAsync(ct), path);
    }

    /// <summary>
    /// Dal corpo della risposta ai NOTAM. Pubblica per poterla provare su un corpo vero senza un server.
    /// <para>⚠️ Severa sull'identità e sulle date, tollerante sul resto: una voce senza <c>id</c> o senza
    /// <c>name</c>, o con una data scritta che non si legge, fa fallire <b>tutto</b> il giro. Saltare la voce
    /// vorrebbe dire toglierla dall'archivio, e leggere «nessuna fine» al posto di una fine illeggibile vorrebbe
    /// dire mostrare per sempre un NOTAM scaduto.</para>
    /// </summary>
    public static IReadOnlyList<NotamDaSorgente> Leggi(string body, string path)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{path}: la risposta non è un elenco — NOTAM non letti.");

        var notam = new List<NotamDaSorgente>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (IvaoHttp.JsonIntId(e, "id") is not int id || IvaoHttp.JsonStr(e, "name") is not { Length: > 0 } nome)
                throw new InvalidDataException($"{path}: una voce senza id o senza name — NOTAM non letti.");

            notam.Add(new NotamDaSorgente(
                IvaoId: id,
                Nome: nome.Trim(),
                InizioUtc: Data(e, "startTime", nome, path),
                FineUtc: Data(e, "endTime", nome, path),
                // Le righe arrivano con CRLF (misurato): in archivio una forma sola.
                Testo: (IvaoHttp.JsonStr(e, "description") ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim(),
                Militare: IvaoHttp.JsonBool(e, "military"),
                Aeroporti: Codici(e, "airports", "icao"),
                Centri: Codici(e, "centers", "id"),
                AggiornatoUtc: Data(e, "updatedAt", nome, path)));
        }
        return notam;
    }

    /// <summary>Assente, <c>null</c> o stringa vuota = non detta (la sorgente scrive <c>""</c> per «senza fine»).</summary>
    private static DateTime? Data(JsonElement e, string campo, string nome, string path)
    {
        if (IvaoHttp.JsonStr(e, campo) is not { Length: > 0 } testo || string.IsNullOrWhiteSpace(testo)) return null;
        if (DateTime.TryParse(testo, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var data))
            return DateTime.SpecifyKind(data, DateTimeKind.Utc);
        throw new InvalidDataException($"{path}: {nome} ha {campo} = «{testo}», che non è una data — NOTAM non letti.");
    }

    private static IReadOnlyList<string> Codici(JsonElement e, string elenco, string campo)
    {
        if (!e.TryGetProperty(elenco, out var voci) || voci.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return voci.EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.Object ? IvaoHttp.JsonStr(v, campo) : null)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .ToList();
    }
}
