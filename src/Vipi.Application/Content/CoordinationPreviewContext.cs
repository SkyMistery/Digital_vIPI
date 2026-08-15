using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Mappe di risoluzione + template per comporre le frasi di coordinamento lato editor (anteprima live).
/// Caricato una sola volta alla scelta dell'ACC così la pagina può comporre le frasi in locale (funzione pura
/// <see cref="CoordinationSentences.Compose"/>) senza un round-trip per ogni tasto. Stesse mappe usate dalla
/// derivazione reale (<c>AccDerivationService.DeriveCoordinationAsync</c>) → l'anteprima combacia con l'output.</summary>
public sealed record CoordinationPreviewContext(
    IReadOnlyDictionary<string, SectorType> Types,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyDictionary<string, string> Codes,
    IReadOnlyDictionary<string, string> Airports,
    IReadOnlyDictionary<string, string> Atc,
    CoordinationSentenceTemplate Template)
{
    /// <summary>Compone la frase per un punto (owner→next). Ritorna null se i dati sono incompleti
    /// (senza mittente/ricevente, o arrivo/partenza senza aeroporto): come la derivazione reale.</summary>
    /// <param name="conditions">Catena delle condizioni, dalla capofila dell'outline alla riga in anteprima:
    /// l'editor deve mostrare la frase che il documento renderà, e quella cumula gli antenati.</param>
    public string? Compose(
        string ownerCallsign, string? nextCallsign, string? airportIcao, TransferFlowKind kind,
        LevelConstraint constraint, int? levelValue, LevelUnit levelUnit, string? levelSpecial,
        LevelParity parity, TransferVerticalState verticalState, string cop,
        IReadOnlyList<ConditionClause> conditions,
        TransferHandoffFacet? facet = null) =>
        CoordinationSentences.Compose(Template, Types, Names, Codes, Airports, Atc,
            ownerCallsign, nextCallsign ?? "", airportIcao,
            constraint, levelValue, levelUnit, levelSpecial, parity, cop, kind,
            conditions, verticalState, facet);
}
