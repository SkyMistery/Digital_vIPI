using Vipi.Application.Live;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Quando un chip d'aeroporto della vista live dice «delegato». Committente, 30 settembre 2026: da
/// <c>LIMF_WW0_APP</c>, con nessuno sotto, LIMF compariva delegato — perché quell'avvicinamento è lui stesso una
/// posizione di LIMF, e la regola era «una posizione dello scalo è online».
/// </summary>
public class ScaloDelegatoTests
{
    private static AirportPresidency Chi(params (string Callsign, SectorType Tipo)[] locali) =>
        new(locali.Select(l => new PresidingStation(l.Callsign, l.Tipo, IsAirportOwn: true)).ToList(), null);

    [Fact]
    public void Chi_guarda_non_delega_a_se_stesso()
    {
        var chi = Chi(("LIMF_WW0_APP", SectorType.App));
        Assert.False(LiveStationParts.Delegato(chi, "LIMF_WW0_APP", new[] { "LIMM_WS2_CTR", "LIMM_CTR" }));
    }

    [Fact]
    public void Una_torre_online_sotto_chi_guarda_e_una_delega()
    {
        var chi = Chi(("LIMF_TWR", SectorType.Twr), ("LIMF_WW0_APP", SectorType.App));
        Assert.True(LiveStationParts.Delegato(chi, "LIMF_WW0_APP", new[] { "LIMM_WS2_CTR" }));
    }

    [Fact]
    public void Una_posizione_dello_scalo_SOPRA_chi_guarda_non_gli_toglie_lo_scalo()
    {
        // Da LIMF_WN0_APP, figlio di LIMF_WW0_APP: il padre online non è qualcuno a cui si è delegato.
        var chi = Chi(("LIMF_WW0_APP", SectorType.App));
        Assert.False(LiveStationParts.Delegato(chi, "LIMF_WN0_APP", new[] { "LIMF_WW0_APP", "LIMM_WS2_CTR" }));
    }

    [Fact]
    public void Da_un_area_l_avvicinamento_dello_scalo_online_e_una_delega()
    {
        var chi = Chi(("LIMF_WW0_APP", SectorType.App));
        Assert.True(LiveStationParts.Delegato(chi, "LIMM_WS2_CTR", new[] { "LIMM_CTR" }));
    }

    [Fact]
    public void Nessuno_in_loco_nessuna_delega()
    {
        Assert.False(LiveStationParts.Delegato(Chi(), "LIMM_WS2_CTR", Array.Empty<string>()));
    }
}
