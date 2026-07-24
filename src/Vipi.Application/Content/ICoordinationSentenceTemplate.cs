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
/// frase → {owner} {target} {airport} {stato} {fl} {point}; target → {name} {code}; airport → {name} {icao}.
/// <br/>Il placeholder <c>{airport}</c> include già la <b>relazione</b> (arrivo/partenza): per gli arrivi usa
/// <see cref="AirportArrival"/> («con destinazione …»), per le partenze <see cref="AirportDeparture"/> («in partenza da …»).</summary>
public sealed class CoordinationSentenceTemplate
{
    // {fl} include già l'eventuale «per …» (composto dal service) così, se assente, non resta un «per» orfano.
    // {airport} include già la relazione arrivo/partenza (vedi AirportArrival/AirportDeparture): NON aggiungere qui
    // «con destinazione», sennò per le partenze la frase risulterebbe errata.
    public string Template { get; init; } =
        "{owner} trasferisce a {target} il traffico {airport} {stato} {fl} su {point}.";
    public string TargetWithCode { get; init; } = "{name} {code}";
    public string TargetNoCode { get; init; } = "{name}";
    /// <summary>Aeroporto neutro (senza relazione): fallback per flussi non arrivo/partenza. Placeholder {name} {icao}.</summary>
    public string Airport { get; init; } = "{name} {icao}";
    /// <summary>Relazione aeroporto per gli ARRIVI (il traffico è destinato all'aeroporto). Placeholder {name} {icao}.</summary>
    public string AirportArrival { get; init; } = "con destinazione {name} {icao}";
    /// <summary>Relazione aeroporto per le PARTENZE (il traffico proviene dall'aeroporto). Placeholder {name} {icao}.</summary>
    public string AirportDeparture { get; init; } = "in partenza da {name} {icao}";
    public CoordinationSentenceState Stato { get; init; } = new();
    /// <summary>Fraseologia del livello ({fl}): parole/format-string usati dal composer, così è lingua-neutro.</summary>
    public CoordinationSentenceLevel Level { get; init; } = new();
    /// <summary>Clausola condizione (pista in uso / area attiva), appesa a fine frase quando il punto ha una condizione.
    /// Placeholder {label}. Vuota quando la condizione è None.</summary>
    public CoordinationSentenceCondition Condition { get; init; } = new();
    /// <summary>Reso quando il CoP è VUOTO (non compilato): distinto da «ALL». Il default globale può renderlo «—».</summary>
    public string FallbackMissingPoint { get; init; } = "tutti i punti";
    /// <summary>Reso quando il CoP è «ALL»: istruzione esplicita «tutti i punti di consegna».</summary>
    public string FallbackAllPoints { get; init; } = "tutti i punti";
    /// <summary>Reso quando il CoP è «ALL to X»: tutti i punti verso una nazione/FIR. Placeholder {dest}.</summary>
    public string FallbackAllToward { get; init; } = "tutti i punti verso {dest}";

    public static CoordinationSentenceTemplate Default { get; } = new();

    /// <summary>Template inglese (usato dalle vLOA, documenti bilaterali in EN). Stessi placeholder, testi in inglese.</summary>
    public static CoordinationSentenceTemplate English { get; } = new()
    {
        Template = "{owner} transfers to {target} the traffic {airport} {stato} {fl} over {point}.",
        AirportArrival = "inbound to {name} {icao}",
        AirportDeparture = "departing from {name} {icao}",
        Stato = new CoordinationSentenceState
        {
            Descending = "descending",
            Climbing = "climbing",
            Level = "level",
        },
        Level = new CoordinationSentenceLevel
        {
            FlBody = "at level {v}",
            FtBody = "at {v} ft",
            OrBelow = "or below",
            OrAbove = "or above",
            ForLevel = "for a level",
            ParityEven = "even",
            ParityOdd = "odd",
        },
        Condition = new CoordinationSentenceCondition
        {
            Runway = "with runway {label} in use",
            Area = "with {label} active",
            RunwayAndArea = "with runway {runway} in use and {area} active",
            Custom = "under condition {label}",
            Join = "and",
        },
        FallbackMissingPoint = "—",
        FallbackAllPoints = "all points",
        FallbackAllToward = "all points toward {dest}",
    };
}

/// <summary>Parola per lo stato verticale del traffico, scelto a mano sul punto (<see cref="Vipi.Domain.TransferVerticalState"/>).
/// Indipendente dal vincolo di livello. <c>Unspecified</c> non ha parola (frase senza stato).</summary>
public sealed class CoordinationSentenceState
{
    public string Descending { get; init; } = "in discesa";
    public string Climbing { get; init; } = "in salita";
    public string Level { get; init; } = "stabile";
}

/// <summary>Fraseologia del livello nella frase di coordinamento ({fl}). Testi lingua-specifici estratti dal composer
/// così che il template inglese (vLOA) possa renderli in EN. <see cref="FlBody"/>/<see cref="FtBody"/> usano il
/// placeholder {v} per il valore; <see cref="ForLevel"/> precede la parità quando manca un valore numerico.</summary>
public sealed class CoordinationSentenceLevel
{
    /// <summary>Corpo con unità FL, placeholder {v}: «a livello {v}».</summary>
    public string FlBody { get; init; } = "a livello {v}";
    /// <summary>Corpo con unità piedi, placeholder {v}: «a {v} ft».</summary>
    public string FtBody { get; init; } = "a {v} ft";
    /// <summary>Suffisso vincolo ≤ (AtOrBelow): «o livello inferiore».</summary>
    public string OrBelow { get; init; } = "o livello inferiore";
    /// <summary>Suffisso vincolo ≥ (AtOrAbove): «o livello superiore».</summary>
    public string OrAbove { get; init; } = "o livello superiore";
    /// <summary>Prefisso usato senza valore numerico ma con parità: «per un livello» + parità.</summary>
    public string ForLevel { get; init; } = "per un livello";
    public string ParityEven { get; init; } = "pari";
    public string ParityOdd { get; init; } = "dispari";
}

/// <summary>Clausola condizione operativa appesa a fine frase (pista in uso / area attiva / condizione libera).
/// Placeholder {label}. Lingua-specifica come il resto del template (IT default, EN nelle vLOA).</summary>
public sealed class CoordinationSentenceCondition
{
    /// <summary>Condizione di pista in uso, placeholder {label}: «con pista {label} in uso».</summary>
    public string Runway { get; init; } = "con pista {label} in uso";
    /// <summary>Condizione di area attiva, placeholder {label}: «con {label} attiva».</summary>
    public string Area { get; init; } = "con {label} attiva";
    /// <summary>Condizione combinata pista + area in AND, placeholder {runway}/{area}: «con pista {runway} in uso e {area} attiva».</summary>
    public string RunwayAndArea { get; init; } = "con pista {runway} in uso e {area} attiva";
    /// <summary>Condizione libera, placeholder {label}: «in condizione {label}».</summary>
    public string Custom { get; init; } = "in condizione {label}";
    /// <summary>Congiunzione tra clausole condizione presenti (pista/area/personalizzata): «e» (EN «and»).</summary>
    public string Join { get; init; } = "e";
}
