using System.Globalization;
using System.Text.Json;

namespace Vipi.Application.Tabellone;

/// <summary>Un volo prenotato del booking di IVAO Italia (uno slot che qualcuno ha preso).</summary>
/// <param name="Vid">Chi l'ha prenotato. Serve SOLO a filtrare: nel JSON del tabellone non va.</param>
/// <param name="Eobt">Orario di partenza. ⚠️ Per chi si prenota da solo è l'orario allo scalo dell'evento anche
/// per un arrivo (vedi <see cref="BookingParser"/>).</param>
public sealed record VoloPrenotato(
    string Callsign,
    string Origine,
    string Destinazione,
    string? Aereo,
    DateTimeOffset? Eobt,
    DateTimeOffset? Eat,
    string? Gate,
    long Vid,
    string? CittaOrigine = null,
    string? IataOrigine = null,
    string? CittaDestinazione = null,
    string? IataDestinazione = null);

/// <summary>Il booking letto: o i voli, o perché non si è potuto leggere.</summary>
public sealed record LetturaBooking(IReadOnlyList<VoloPrenotato> Voli, string? Errore)
{
    public bool Ok => Errore is null;
    public static LetturaBooking Fallita(string errore) => new(Array.Empty<VoloPrenotato>(), errore);
}

/// <summary>
/// Il booking di IVAO Italia, <c>GET https://booking.it.ivao.aero/api/flights</c> con l'header <c>x-api-key</c>:
/// un array di voli. Le regole vengono dal parser del RFO Gate Manager (<c>BookingParser.cs</c>), già passato per
/// l'RFO di Napoli e il Roma RFE. Carta <c>docs/feature/2026-10-02-tabellone-partenze-arrivi.md</c> §3.
///
/// <list type="bullet">
/// <item>Risponde 200 anche sugli errori PHP: si controlla che sia un array JSON.</item>
/// <item>Non c'è un campo di stato: uno slot con <c>booked_by</c> nullo o 0 non l'ha preso nessuno e non compare mai.</item>
/// <item><c>gate</c> può valere <c>TBD</c> (e simili): nessuno stand.</item>
/// <item>Orari <c>yyyy-MM-dd HH:mm:ss</c>, da trattare come UTC.</item>
/// </list>
/// </summary>
public static class BookingParser
{
    private static readonly string[] NessunoStand = ["TBD", "TBA", "N/A", "-", "NIL", "NONE"];

    // ⚠️ I nomi dei campi di città e IATA non sono documentati (FORMATO-DATI §7: «da verificare»). Si provano i
    // candidati più probabili; se nessuno c'è, la città e la IATA vengono dall'anagrafica aeroporti di vIPI.
    private static readonly string[] CittaOrigine = ["origin_city", "origin_city_name", "origin_name"];
    private static readonly string[] IataOrigine = ["origin_iata", "origin_iata_code"];
    private static readonly string[] CittaDestinazione = ["destination_city", "destination_city_name", "destination_name"];
    private static readonly string[] IataDestinazione = ["destination_iata", "destination_iata_code"];

    public static LetturaBooking Leggi(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            return LetturaBooking.Fallita("La risposta del booking non è JSON.");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return LetturaBooking.Fallita("La risposta del booking non è un elenco di voli.");

            var voli = new List<VoloPrenotato>();
            foreach (var o in doc.RootElement.EnumerateArray())
            {
                if (o.ValueKind != JsonValueKind.Object) continue;
                if (Vid(o) is not { } vid) continue;

                var callsign = Testo(o, "callsign")?.ToUpperInvariant();
                var origine = Testo(o, "origin_icao")?.ToUpperInvariant();
                var destinazione = Testo(o, "destination_icao")?.ToUpperInvariant();
                if (callsign is null || origine is null || destinazione is null) continue;

                voli.Add(new VoloPrenotato(
                    callsign, origine, destinazione,
                    Testo(o, "aircraft_icao")?.ToUpperInvariant(),
                    Orario(Testo(o, "eobt")), Orario(Testo(o, "eat")),
                    Stand(Testo(o, "gate")), vid,
                    Primo(o, CittaOrigine), Primo(o, IataOrigine),
                    Primo(o, CittaDestinazione), Primo(o, IataDestinazione)));
            }
            return new LetturaBooking(voli, null);
        }
    }

    /// <summary>Il VID di chi ha prenotato, o null se lo slot è libero (nullo, 0, negativo, non un numero).</summary>
    private static long? Vid(JsonElement o)
    {
        if (!o.TryGetProperty("booked_by", out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt64(out var n) && n > 0 => n,
            JsonValueKind.String when long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 => n,
            _ => null,
        };
    }

    internal static string? Stand(string? grezzo)
    {
        if (string.IsNullOrWhiteSpace(grezzo)) return null;
        var v = grezzo.Trim();
        return NessunoStand.Contains(v, StringComparer.OrdinalIgnoreCase) ? null : v;
    }

    /// <summary><c>yyyy-MM-dd HH:mm:ss</c> come UTC; anche ISO completo. Altro = null.</summary>
    internal static DateTimeOffset? Orario(string? grezzo)
    {
        if (string.IsNullOrWhiteSpace(grezzo)) return null;
        return DateTimeOffset.TryParse(grezzo.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : null;
    }

    private static string? Primo(JsonElement o, string[] nomi)
    {
        foreach (var n in nomi)
            if (Testo(o, n) is { } v) return v;
        return null;
    }

    private static string? Testo(JsonElement o, string nome)
    {
        if (!o.TryGetProperty(nome, out var el)) return null;
        var v = el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
