using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Quale controllo del ground salta un'eccezione (carta «Gli aeroporti nel Lab», §2 D).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ControlloDelGround>))]
public enum ControlloDelGround
{
    /// <summary>Controllo 1: una testata del <c>.rw</c> lontana dalla soglia di AD 2.12.</summary>
    Soglia,
    /// <summary>Controllo 2: una taxiway che il sector non ha, o che ha solo lui.</summary>
    Taxiway,
    /// <summary>Controllo 3: uno stand del <c>.gts</c> lontano dal punto dell'AIP, o che nessun <c>.gts</c> ha.</summary>
    Stand,
}

/// <summary>
/// Un'eccezione dello scalo (controllo 4): una cosa che il sector fa diversa dall'AIP per scelta, col perché. Il controllo la
/// salta e lo dice. <see cref="Voce"/>: la testata («02»), la taxiway («CM»), lo stand («211»), come la scrive il controllo.
/// </summary>
public sealed record EccezioneDelGround(ControlloDelGround Controllo, string Voce, string Perche, DateOnly? Data = null);

/// <summary>
/// Le eccezioni del ground di uno scalo nel repo dell'AIP espanso: <c>eccezioni\&lt;ICAO&gt;.json</c>, accanto a
/// <c>posa\</c> e <c>correzioni\</c>, fuori dai cicli — una scelta del sector resta finché il sector non cambia. Il file dice
/// il sector, non l'AIP: sta lì perché è lì che il Lab tiene quel che sa degli scali e che si registra con «Registra l'AIP».
/// </summary>
public static class EccezioniDelGround
{
    public const string Cartella = "eccezioni";

    private const int Versione = 1;

    private sealed record SulDisco(int Versione, string Scalo, IReadOnlyList<EccezioneDelGround> Eccezioni);

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Un file del repo, non HTML: le lettere accentate del perché restano lettere, e il diff si legge.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string File(string espanso, string icao) =>
        Path.Combine(espanso, Cartella, icao.ToUpperInvariant() + ".json");

    /// <summary>Le eccezioni di uno scalo; vuoto se il file non c'è.</summary>
    /// <exception cref="InvalidDataException">Il file c'è ma non si legge.</exception>
    public static IReadOnlyList<EccezioneDelGround> Leggi(string espanso, string icao)
    {
        var file = File(espanso, icao);
        if (!System.IO.File.Exists(file))
            return [];
        try
        {
            return JsonSerializer.Deserialize<SulDisco>(System.IO.File.ReadAllText(file), Opzioni)?.Eccezioni ?? [];
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"«{Path.GetFileName(file)}» non si legge: {e.Message}", e);
        }
    }

    /// <summary>
    /// Scrive un'eccezione, al posto di quella che c'era per la stessa voce dello stesso controllo. Le eccezioni in ordine di
    /// controllo e di voce: il file cambia solo dove cambia l'eccezione.
    /// </summary>
    /// <exception cref="ArgumentException">Senza voce o senza perché: un'eccezione senza il suo perché non si capisce più.</exception>
    public static void Scrivi(string espanso, string icao, EccezioneDelGround eccezione)
    {
        ArgumentNullException.ThrowIfNull(eccezione);
        if (string.IsNullOrWhiteSpace(eccezione.Voce))
            throw new ArgumentException("L'eccezione non dice quale voce salta.", nameof(eccezione));
        if (string.IsNullOrWhiteSpace(eccezione.Perche))
            throw new ArgumentException("Scrivi il perché: il controllo lo mostrerà al posto della differenza.", nameof(eccezione));
        var pulita = eccezione with { Voce = eccezione.Voce.Trim(), Perche = eccezione.Perche.Trim() };
        var chiave = Chiave(pulita.Controllo, pulita.Voce);
        Riscrivi(espanso, icao, [.. Leggi(espanso, icao).Where(e => e.Controllo != pulita.Controllo || Chiave(e.Controllo, e.Voce) != chiave), pulita]);
    }

    /// <summary>Toglie l'eccezione di quella voce; senza eccezioni il file se ne va. Falso se non c'era.</summary>
    public static bool Togli(string espanso, string icao, ControlloDelGround controllo, string voce)
    {
        ArgumentNullException.ThrowIfNull(voce);
        var chiave = Chiave(controllo, voce);
        var prima = Leggi(espanso, icao);
        var dopo = prima.Where(e => e.Controllo != controllo || Chiave(e.Controllo, e.Voce) != chiave).ToList();
        if (dopo.Count == prima.Count)
            return false;
        Riscrivi(espanso, icao, dopo);
        return true;
    }

    /// <summary>L'eccezione per quella voce di quel controllo, o null: «2», «02» e « 02» sono la stessa testata, «07» e «7» lo stesso stand.</summary>
    public static EccezioneDelGround? Di(IEnumerable<EccezioneDelGround> eccezioni, ControlloDelGround controllo, string voce)
    {
        ArgumentNullException.ThrowIfNull(eccezioni);
        ArgumentNullException.ThrowIfNull(voce);
        var chiave = Chiave(controllo, voce);
        return eccezioni.FirstOrDefault(e => e.Controllo == controllo && Chiave(e.Controllo, e.Voce) == chiave);
    }

    /// <summary>Come il controllo confronta i nomi: le testate come AD 2 («2» è «02»), gli stand senza zeri davanti.</summary>
    private static string Chiave(ControlloDelGround controllo, string voce) => controllo switch
    {
        ControlloDelGround.Soglia => ConfrontoDiAd2.Designatore(voce),
        ControlloDelGround.Stand => StandDelloScalo.Chiave(voce),
        _ => voce.Trim().ToUpperInvariant(),
    };

    private static void Riscrivi(string espanso, string icao, List<EccezioneDelGround> eccezioni)
    {
        var file = File(espanso, icao);
        if (eccezioni.Count == 0)
        {
            if (System.IO.File.Exists(file))
                System.IO.File.Delete(file);
            return;
        }
        var ordinate = eccezioni.OrderBy(e => e.Controllo).ThenBy(e => Chiave(e.Controllo, e.Voce), StringComparer.Ordinal).ToList();
        var testo = JsonSerializer.Serialize(new SulDisco(Versione, icao.ToUpperInvariant(), ordinate), Opzioni) + "\n";
        AipEspanso.ScriviAlPosto(file, flusso => flusso.Write(System.Text.Encoding.UTF8.GetBytes(testo)));
    }
}
