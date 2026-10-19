using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aip;
using Aip.Pdf;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Come si scrive un campo nel modulo: il testo, e come diventa il valore del modello.</summary>
public enum GenereDelCampo
{
    /// <summary>Testo su una riga.</summary>
    Testo,

    /// <summary>Testo su più righe (note, procedure).</summary>
    TestoLungo,

    /// <summary>Una coordinata come la scrive l'AIP: <c>414800N 0123000E</c> o <c>41°48'00"N 012°30'00"E</c>.</summary>
    Posizione,

    /// <summary>Una voce di un elenco fisso (il tipo di una radioassistenza, il verso di un'attesa).</summary>
    Scelta,

    /// <summary>Un numero intero (la rotta d'avvicinamento di un'attesa).</summary>
    Intero,

    /// <summary>Sì o no.</summary>
    SiNo,

    /// <summary>Un limite verticale come lo scrive l'AIP: <c>FL 110</c>, <c>3000 FT AMSL</c>.</summary>
    Limite,

    /// <summary>Una distanza col suo testo: <c>16 KM</c>, <c>5 NM</c>.</summary>
    Distanza,

    /// <summary>Un elenco di parole separate da virgole (le rotte di un punto).</summary>
    Elenco,

    /// <summary>Una parte composta (i tratti di una rotta, le piste di uno scalo), nel JSON del modello.</summary>
    Json,
}

/// <summary>Un campo del modulo: il nome nel JSON del modello, l'etichetta, il genere, se è obbligatorio, le scelte.</summary>
public sealed record CampoDelModulo(string Nome, string Etichetta, GenereDelCampo Genere, bool Obbligatorio = false, IReadOnlyList<string>? Scelte = null);

/// <summary>
/// Il modulo di una voce dell'AIP espanso che non è un'area (carta «Le correzioni dell'AIP espanso», slice 3): punti,
/// radioassistenze, luci, pericoli, attese, rotte, scali. Un modulo solo per tutti, coi campi di ogni tipo: si riempie
/// dalla voce (<see cref="Valori"/>) e torna una voce (<see cref="Voce"/>), letta come il modello la legge.
/// <para>🔴 Un campo che l'AOD non ha toccato resta <b>com'era</b> nel modello, non riscritto dal testo: una coordinata
/// riscritta in secondi e riletta può spostarsi di un'inezia, e diventerebbe una correzione che nessuno ha fatto.</para>
/// <para>I campi che il modulo non mostra (la pagina dell'AIP) restano quelli della voce di prima.</para>
/// </summary>
public static partial class ModuloDellaVoce
{
    private static IReadOnlyList<string> Scelte<T>() where T : struct, Enum =>
        [.. Enum.GetValues<T>().Select(v => JsonNamingPolicy.CamelCase.ConvertName(v.ToString()))];

    /// <summary>I campi del modulo di quel tipo, nell'ordine in cui si mostrano.</summary>
    public static IReadOnlyList<CampoDelModulo> Campi(CorrectionKind tipo) => tipo switch
    {
        CorrectionKind.Point =>
        [
            new("section", "Sezione", GenereDelCampo.Testo, true),
            new("designator", "Identificativo", GenereDelCampo.Testo, true),
            new("position", "Posizione", GenereDelCampo.Posizione, true),
            new("routes", "Rotte (separate da virgole)", GenereDelCampo.Elenco),
            new("procedures", "Procedure", GenereDelCampo.TestoLungo),
            new("fra", "FRA", GenereDelCampo.Testo),
            new("remarks", "Note", GenereDelCampo.TestoLungo),
        ],
        CorrectionKind.Navaid =>
        [
            new("section", "Sezione", GenereDelCampo.Testo, true),
            new("designator", "Sigla", GenereDelCampo.Testo, true),
            new("name", "Nome", GenereDelCampo.Testo, true),
            new("type", "Tipo com'è stampato", GenereDelCampo.Testo, true),
            new("kind", "Tipo", GenereDelCampo.Scelta, true, Scelte<NavaidKind>()),
            new("military", "Militare", GenereDelCampo.SiNo),
            new("frequency", "Frequenza", GenereDelCampo.Testo),
            new("channel", "Canale", GenereDelCampo.Testo),
            new("position", "Posizione", GenereDelCampo.Posizione, true),
            new("magneticVariation", "Declinazione", GenereDelCampo.Testo),
            new("elevation", "Elevazione", GenereDelCampo.Testo),
            new("coverage", "Copertura", GenereDelCampo.Testo),
            new("fra", "FRA", GenereDelCampo.Testo),
            new("remarks", "Note", GenereDelCampo.TestoLungo),
            new("components", "Apparati (JSON)", GenereDelCampo.Json),
        ],
        CorrectionKind.Light =>
        [
            new("section", "Sezione", GenereDelCampo.Testo, true),
            new("name", "Nome", GenereDelCampo.Testo, true),
            new("position", "Posizione", GenereDelCampo.Posizione),
            new("type", "Tipo", GenereDelCampo.Testo),
            new("characteristics", "Caratteristiche", GenereDelCampo.Testo),
            new("hours", "Orari", GenereDelCampo.Testo),
            new("intensity", "Intensità", GenereDelCampo.Testo),
            new("remarks", "Note", GenereDelCampo.TestoLungo),
        ],
        CorrectionKind.Hazard =>
        [
            new("section", "Sezione", GenereDelCampo.Testo, true),
            new("kind", "Tipo", GenereDelCampo.Scelta, true, Scelte<HazardKind>()),
            new("name", "Nome", GenereDelCampo.Testo, true),
            new("position", "Posizione", GenereDelCampo.Posizione, true),
            new("radius", "Raggio", GenereDelCampo.Distanza),
            new("vertical", "Quota", GenereDelCampo.Testo),
            new("hours", "Orari", GenereDelCampo.Testo),
            new("remarks", "Note", GenereDelCampo.TestoLungo),
        ],
        CorrectionKind.Holding =>
        [
            new("section", "Sezione", GenereDelCampo.Testo, true),
            new("fix", "Punto", GenereDelCampo.Testo, true),
            new("fixType", "Tipo della radioassistenza (ENR 3.4.2)", GenereDelCampo.Testo),
            new("bearing", "Riferimento", GenereDelCampo.Testo),
            new("minimum", "Quota minima", GenereDelCampo.Limite, true),
            new("turn", "Virata", GenereDelCampo.Scelta, true, Scelte<TurnDirection>()),
            new("inboundTrack", "Rotta d'avvicinamento (°)", GenereDelCampo.Intero, true),
            new("noteRefs", "Rimandi", GenereDelCampo.Testo),
            new("remarks", "Note", GenereDelCampo.TestoLungo),
            new("affected", "Rotte e procedure interessate", GenereDelCampo.TestoLungo),
        ],
        CorrectionKind.Route =>
        [
            new("section", "Sezione", GenereDelCampo.Testo, true),
            new("designator", "Identificativo", GenereDelCampo.Testo, true),
            new("navigation", "Navigazione", GenereDelCampo.Testo),
            new("points", "Punti (JSON)", GenereDelCampo.Json, true),
            new("segments", "Tratti (JSON)", GenereDelCampo.Json, true),
        ],
        CorrectionKind.Aerodrome =>
        [
            new("icao", "ICAO", GenereDelCampo.Testo, true),
            new("name", "Nome", GenereDelCampo.Testo, true),
            new("arp", "Punto di riferimento (ARP)", GenereDelCampo.Posizione),
            new("elevation", "Elevazione", GenereDelCampo.Testo),
            new("referenceTemperature", "Temperatura di riferimento", GenereDelCampo.Testo),
            new("magneticVariation", "Declinazione", GenereDelCampo.Testo),
            new("annualChange", "Variazione annua", GenereDelCampo.Testo),
            new("traffic", "Traffico", GenereDelCampo.Testo),
            new("runways", "Piste (JSON)", GenereDelCampo.Json),
            new("declaredDistances", "Distanze dichiarate (JSON)", GenereDelCampo.Json),
            new("communications", "Frequenze (JSON)", GenereDelCampo.Json),
            new("navigationAids", "Radioassistenze di AD 2.19 (JSON)", GenereDelCampo.Json),
            new("ils", "ILS (JSON)", GenereDelCampo.Json),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Le aree hanno il loro modulo (ModuloDellArea)."),
    };

    /// <summary>Il testo di ogni campo per la voce (null = il modulo vuoto di «Aggiungi»).</summary>
    public static IReadOnlyDictionary<string, string> Valori(CorrectionKind tipo, object? voce)
    {
        var nodo = voce is null ? new JsonObject() : AipCorrections.ToNode(voce);
        var valori = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var campo in Campi(tipo))
            valori[campo.Nome] = Testo(campo, nodo[campo.Nome]);
        return valori;
    }

    /// <summary>
    /// La voce del modulo, o il perché non si può fare. <paramref name="prima"/>: la voce che si corregge (null = nuova).
    /// </summary>
    public static (object? Voce, string? Perche) Voce(CorrectionKind tipo, IReadOnlyDictionary<string, string?> valori, object? prima)
    {
        ArgumentNullException.ThrowIfNull(valori);
        var nodo = prima is null ? new JsonObject() : AipCorrections.ToNode(prima);
        var diPrima = Valori(tipo, prima);
        foreach (var campo in Campi(tipo))
        {
            var testo = valori.GetValueOrDefault(campo.Nome)?.Trim() ?? "";
            // Non toccato: resta il valore del modello (vedi sopra).
            if (prima is not null && testo == diPrima[campo.Nome].Trim())
                continue;
            if (testo.Length == 0)
            {
                if (campo.Obbligatorio && campo.Genere != GenereDelCampo.Json)
                    return (null, $"Manca «{campo.Etichetta}».");
                if (campo.Genere == GenereDelCampo.Json)
                    nodo[campo.Nome] = new JsonArray();
                else
                    nodo.Remove(campo.Nome);
                continue;
            }

            var (valore, no) = Valore(campo, testo);
            if (no is not null)
                return (null, $"«{campo.Etichetta}»: {no}");
            nodo[campo.Nome] = valore;
        }

        object? voce;
        try
        {
            voce = nodo.Deserialize(CorrezioniDellAip.Tipo(tipo), AipJson.Options);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return (null, $"La voce non torna: {e.Message}");
        }

        if (voce is null)
            return (null, "La voce è vuota.");
        if (prima is not null && AipCorrections.ToNode(voce).ToJsonString() == AipCorrections.ToNode(prima).ToJsonString())
            return (null, "Non è cambiato niente: la voce è uguale a quella di adesso.");
        return (voce, null);
    }

    private static string Testo(CampoDelModulo campo, JsonNode? valore)
    {
        // Un modulo vuoto ha già una scelta fatta (la prima) e «no»: un campo obbligatorio a tendina non resta vuoto.
        if (valore is null)
            return campo.Genere switch { GenereDelCampo.Scelta => campo.Scelte?.FirstOrDefault() ?? "", GenereDelCampo.SiNo => "false", _ => "" };
        return campo.Genere switch
        {
            GenereDelCampo.Posizione => Coordinata(valore["lat"]!.GetValue<double>(), valore["lon"]!.GetValue<double>()),
            GenereDelCampo.Limite => valore["text"]?.GetValue<string>() ?? "",
            GenereDelCampo.Distanza => $"{valore["value"]!.GetValue<double>().ToString(CultureInfo.InvariantCulture)} {valore["unit"]!.GetValue<string>().ToUpperInvariant()}",
            GenereDelCampo.Elenco => string.Join(", ", valore.AsArray().Select(v => v!.GetValue<string>())),
            GenereDelCampo.Json => valore.ToJsonString(AipJson.Options),
            GenereDelCampo.SiNo => valore.GetValue<bool>() ? "true" : "false",
            _ => valore.GetValueKind() == JsonValueKind.String ? valore.GetValue<string>() : valore.ToJsonString(),
        };
    }

    private static (JsonNode? Valore, string? Perche) Valore(CampoDelModulo campo, string testo)
    {
        switch (campo.Genere)
        {
            case GenereDelCampo.Posizione:
                return GeometryText.ReadPoint(testo) is { } p && GeometryText.ReadPoints(testo).Count == 1
                    ? (JsonSerializer.SerializeToNode(p, AipJson.Options), null)
                    : (null, "una coordinata come la scrive l'AIP (414800N 0123000E), una sola.");
            case GenereDelCampo.Scelta:
                return campo.Scelte?.Contains(testo) == true ? (JsonValue.Create(testo), null) : (null, $"una di: {string.Join(", ", campo.Scelte ?? [])}.");
            case GenereDelCampo.Intero:
                return int.TryParse(testo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? (JsonValue.Create(n), null) : (null, "un numero intero.");
            case GenereDelCampo.SiNo:
                return bool.TryParse(testo, out var b) ? (JsonValue.Create(b), null) : (null, "sì o no.");
            case GenereDelCampo.Limite:
                return (JsonSerializer.SerializeToNode(LimitText.Parse(testo), AipJson.Options), null);
            case GenereDelCampo.Distanza:
                if (RxDistanza().Match(testo) is not { Success: true } m)
                    return (null, "un numero e l'unità: 16 KM, 5 NM, 300 M.");
                var unita = m.Groups["u"].Value.ToUpperInvariant() switch { "NM" => DistanceUnit.Nm, "KM" => DistanceUnit.Km, _ => DistanceUnit.M };
                var d = new Distance(double.Parse(m.Groups["v"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture), unita);
                return (JsonSerializer.SerializeToNode(d, AipJson.Options), null);
            case GenereDelCampo.Elenco:
                return (new JsonArray([.. testo.Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(v => (JsonNode)JsonValue.Create(v))]), null);
            case GenereDelCampo.Json:
                try
                {
                    return (JsonNode.Parse(testo), null);
                }
                catch (JsonException e)
                {
                    return (null, $"il JSON non si legge ({e.Message}).");
                }
            default:
                return (JsonValue.Create(testo), null);
        }
    }

    [GeneratedRegex(@"^\s*(?<v>\d+(?:[.,]\d+)?)\s*(?<u>NM|KM|M)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxDistanza();

    /// <summary>Una posizione nella forma compatta dell'AIP: <c>414800N 0123000E</c>, coi decimali dei secondi se ci sono.</summary>
    public static string Coordinata(double lat, double lon) =>
        $"{Parte(Math.Abs(lat), 2)}{(lat < 0 ? 'S' : 'N')} {Parte(Math.Abs(lon), 3)}{(lon < 0 ? 'W' : 'E')}";

    private static string Parte(double gradi, int cifre)
    {
        // In centesimi di secondo, per non avere mai «60» secondi dall'arrotondamento.
        var centesimi = (long)Math.Round(gradi * 360000, MidpointRounding.AwayFromZero);
        var g = centesimi / 360000;
        var p = centesimi % 360000 / 6000;
        var s = centesimi % 6000 / 100.0;
        return g.ToString(new string('0', cifre), CultureInfo.InvariantCulture) + p.ToString("00", CultureInfo.InvariantCulture)
               + s.ToString("00.##", CultureInfo.InvariantCulture);
    }
}
