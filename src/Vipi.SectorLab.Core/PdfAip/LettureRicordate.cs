using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// L'esito dell'ultima lettura di ogni ciclo, ricordato nei dati del Lab (26 ottobre, committente: «quando chiudo e poi
/// riapro, la lista delle cose da sistemare sull'AIP su cui sto lavorando: per averla devo rileggere l'AIP»):
/// <c>letture\2611.json</c>. Non va nel repo dell'AIP espanso: è di questo PC, come il ciclo di lavoro.
/// <para>Ricorda la lettura com'era: le correzioni fatte dopo (AJO aggiunta) si vedono nelle segnalazioni solo rileggendo,
/// come prima del riavvio.</para>
/// </summary>
public static class LettureRicordate
{
    public const string Cartella = "letture";

    /// <summary>Cambia quando cambia la forma del file: una lettura di prima non si legge, e si rilegge l'AIP.</summary>
    private const int Versione = 1;

    /// <summary>Il file della lettura di un ciclo dentro i dati del Lab.</summary>
    public static string File(string dati, string ciclo) => Path.Combine(dati, Cartella, ciclo + ".json");

    private sealed record SulDisco(int Versione, string Espanso, DateTime Letta, EsitoDellAipEspanso Esito);

    // I campi servono alle date delle pagine, che sono coppie (data, pagine); i tipi per nome, così un tipo nuovo in mezzo
    // non sposta quelli di una lettura già scritta.
    private static readonly JsonSerializerOptions Opzioni = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Scrive l'esito di una lettura dell'AIP espanso <paramref name="espanso"/>, letta adesso.</summary>
    /// <exception cref="IOException">Il file non si scrive.</exception>
    public static void Scrivi(string dati, string espanso, EsitoDellAipEspanso esito, DateTime letta)
    {
        ArgumentNullException.ThrowIfNull(esito);
        var testo = JsonSerializer.Serialize(new SulDisco(Versione, espanso, letta, esito), Opzioni);
        AipEspanso.ScriviAlPosto(File(dati, esito.Ciclo), flusso => flusso.Write(System.Text.Encoding.UTF8.GetBytes(testo)));
    }

    /// <summary>
    /// La lettura ricordata di un ciclo, se è di quella cartella dell'AIP espanso; null se non c'è, è di un'altra cartella o
    /// non si legge (si rilegge l'AIP, come prima).
    /// </summary>
    public static (EsitoDellAipEspanso Esito, DateTime Letta)? Leggi(string dati, string espanso, string ciclo)
    {
        var file = File(dati, ciclo);
        try
        {
            if (!System.IO.File.Exists(file)
                || JsonSerializer.Deserialize<SulDisco>(System.IO.File.ReadAllText(file), Opzioni) is not { } letta
                || letta.Versione != Versione || letta.Esito is null || letta.Esito.Ciclo != ciclo || !Stessa(letta.Espanso, espanso))
                return null;
            return (letta.Esito, letta.Letta);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Toglie la lettura di un ciclo tolto.</summary>
    public static void Togli(string dati, string ciclo)
    {
        var file = File(dati, ciclo);
        if (System.IO.File.Exists(file))
            System.IO.File.Delete(file);
    }

    private static bool Stessa(string? una, string altra) =>
        una is not null && string.Equals(Path.GetFullPath(una).TrimEnd('\\', '/'), Path.GetFullPath(altra).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
