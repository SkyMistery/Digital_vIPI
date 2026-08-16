using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un ente a un capo dell'accordo. Il callsign è ciò che la derivazione usa davvero (ragiona per
/// callsign); l'id serve alle scritture e a distinguere «parte c'è» da «parte manca».</summary>
public sealed record AgreementPartyRow(AgreementSide Side, int SectorId, string Callsign, int Order);
