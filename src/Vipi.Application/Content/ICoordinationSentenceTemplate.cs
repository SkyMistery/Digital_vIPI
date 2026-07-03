namespace Vipi.Application.Content;

/// <summary>Template (default globale) della frase di coordinamento, caricato da file di progetto.
/// L'implementazione vive fuori da Application (Host/Infrastructure) e può ricaricarsi a caldo.</summary>
public interface ICoordinationSentenceTemplate
{
    /// <summary>Il template corrente (mai null: se il file manca, ritorna il default hardcoded).</summary>
    CoordinationSentenceTemplate Current { get; }
}

/// <summary>Testi del template della frase di coordinamento. Tutti i campi hanno un default sensato,
/// così un file parziale resta valido. Placeholder supportati:
/// frase → {owner} {target} {airport} {stato} {fl} {point}; target → {name} {code}; airport → {name} {icao}.</summary>
public sealed class CoordinationSentenceTemplate
{
    // {fl} include già l'eventuale «per …» (composto dal service) così, se assente, non resta un «per» orfano.
    public string Template { get; init; } =
        "{owner} trasferisce a {target} il traffico con destinazione {airport} {stato} {fl} su {point}.";
    public string TargetWithCode { get; init; } = "{name} {code}";
    public string TargetNoCode { get; init; } = "{name}";
    public string Airport { get; init; } = "{name} {icao}";
    public CoordinationSentenceState Stato { get; init; } = new();
    public string FallbackMissingPoint { get; init; } = "—";

    public static CoordinationSentenceTemplate Default { get; } = new();

    /// <summary>Override della sola frase principale (per-documento), mantenendo gli altri campi del default globale.</summary>
    public CoordinationSentenceTemplate WithTemplate(string template) => new()
    {
        Template = template,
        TargetWithCode = TargetWithCode,
        TargetNoCode = TargetNoCode,
        Airport = Airport,
        Stato = Stato,
        FallbackMissingPoint = FallbackMissingPoint,
    };
}

/// <summary>Parola per lo stato verticale del traffico, derivata dal vincolo di livello.</summary>
public sealed class CoordinationSentenceState
{
    public string AtOrBelow { get; init; } = "in discesa";
    public string AtOrAbove { get; init; } = "in salita";
    public string Exact { get; init; } = "stabile";
    public string Special { get; init; } = "";
}
