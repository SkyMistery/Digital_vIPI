namespace Vipi.Sectorfile.Shared;

/// <summary>
/// I nomi di colore che un <c>.geo</c> può usare senza definirli in <c>[DEFINE]</c>: li colora lo schema di Aurora
/// (manuale IVAO del sector, <c>[DEFINE]</c>: «predefined in the colorscheme options for GEO sections»). Ogni nome
/// porta alla sua chiave dello schema (<c>.clr</c>). Lotto «Subito», slice 4.
/// </summary>
public static class NomiDeiColoriDelGeo
{
    /// <summary>Nome del <c>.geo</c> → chiave dello schema. Senza distinzione fra maiuscole e minuscole (manuale).</summary>
    public static IReadOnlyDictionary<string, string> ChiaveDelloSchema { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["APRON"] = "APRON",
            ["APPRON"] = "APRON",           // «ancora accettato» (manuale)
            ["PARKING"] = "APRON",          // nell'esempio di [GEO]; «Apron and Parking area»
            ["BUILDING"] = "BUILDING",
            ["COAST"] = "COAST",
            ["DANGER"] = "DANGER",
            ["PIER"] = "PIER",
            ["PROHIBIT"] = "PROHIBITED",
            ["RESTRICT"] = "RESTRICTED",
            ["RUNWAY"] = "RUNWAY",
            ["STOPBAR"] = "STOPBAR",
            ["STOPLINE"] = "STOPBAR",       // «ancora accettato» (manuale)
            ["TAXI_CENTER"] = "TAXIWAYCENTER",
            ["TAXIWAY"] = "TAXIWAY",
        };

    /// <summary>La chiave dello schema di un nome del <c>.geo</c>, o <c>null</c> se non è predefinito.</summary>
    public static string? Chiave(string? nome)
        => nome is not null && ChiaveDelloSchema.TryGetValue(nome.Trim(), out string? chiave) ? chiave : null;
}
