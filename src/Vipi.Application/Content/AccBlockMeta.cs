namespace Vipi.Application.Content;

/// <summary>
/// Metadata di un blocco della vIPI ACC serializzato nel <c>BodyJson</c> del blocco proprio della sezione-blocco
/// (doc refactor 08e-acc, schema "blockmeta"): identità/natura + gli override per-blocco (ordine/link frequenze,
/// sezioni nascoste). Le configurazioni e le aree attaccate vivono nei BodyJson delle sezioni
/// figlie keyed (<c>configurations</c>/<c>regulated</c>); separations/vfr idem. Il resto (titolo, ordine sezioni) è
/// nativo su <see cref="Vipi.Domain.Entities.DocumentSection"/>. Lo storage profile <c>AccProfile</c> è eliminato in 08i.
/// </summary>
public sealed class AccBlockMeta
{
    public string Key { get; set; } = "";
    public AccBlockKind Kind { get; set; }
    public List<string> MemberCallsigns { get; set; } = new();
    public List<string> HiddenSections { get; set; } = new();
    public List<AppFreqOrderOverride> FreqOrder { get; set; } = new();
    public List<string> FreqLinkCallsigns { get; set; } = new();
}
