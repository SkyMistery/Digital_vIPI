using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Parser dei record ASCII di Aurora. Le righe di questi test sono RISPOSTE REALI catturate durante lo
/// spike F0 del 3 agosto 2026 (traffico vero, postazione LIZZ_AEW_CTR): è la ragione per cui coprono
/// anche gli scostamenti dalla wiki.
/// </summary>
public class AuroraRecordsTests
{
    private static IReadOnlyList<string> Payload(string raw)
    {
        var fields = raw.Split(';');
        return fields.Skip(1).ToList();   // via il prefisso comando, come fa AuroraClient
    }

    [Fact]
    public void Flight_plan_reale_si_legge_per_intero()
    {
        var fp = AuroraRecords.ParseFlightPlan(Payload(
            "#FP;DLH2MM;LIPE;EDDF;EDFH;0820;A320;M;I;S;SDE2E3FGIJ1LORWXYZ;F180;N0374;0212;0114;" +
            "LUMAV1U LUMAV M726 ALBET/N0425F300 DCT NEGIK;PBN/A1B1C1D1O1S2 DOF/260803;"));

        Assert.NotNull(fp);
        Assert.Equal("LIPE", fp!.Departure);
        Assert.Equal("EDDF", fp.Arrival);
        Assert.Equal("EDFH", fp.Alternate);
        Assert.Equal("A320", fp.AircraftType);
        Assert.Equal("M", fp.WakeTurbulence);
        Assert.Equal("F180", fp.CruisingAltitudeRaw);
        Assert.Equal(180, fp.CruiseFlightLevel);
        Assert.Contains("LUMAV", fp.Route);
    }

    [Fact]
    public void I_campi_regole_e_tipo_di_volo_seguono_l_ordine_reale_non_quello_della_wiki()
    {
        // Reale: «…;A320;M;I;S;…» → prima la regola di volo (I), poi il tipo (S). La wiki li dà invertiti.
        var fp = AuroraRecords.ParseFlightPlan(Payload("#FP;X;LIPE;EDDF;;0820;A320;M;I;S;S;F180;N0374;0212;0114;R;;"));

        Assert.Equal("I", fp!.FlightRules);
        Assert.Equal("S", fp.FlightType);
    }

    [Theory]
    [InlineData("F330", 330)]
    [InlineData("F080", 80)]
    [InlineData("A050", null)]      // quota in centinaia di piedi, non è un FL
    [InlineData("S1130", null)]     // metrico
    [InlineData("", null)]
    public void Il_livello_di_crociera_si_estrae_solo_dal_formato_FL(string raw, int? expected)
    {
        var fp = AuroraRecords.ParseFlightPlan(Payload($"#FP;X;LIPE;EDDF;;0820;A320;M;I;S;S;{raw};N0374;0212;0114;R;;"));

        Assert.Equal(expected, fp!.CruiseFlightLevel);
    }

    [Fact]
    public void Posizione_di_un_traffico_in_volo()
    {
        var pos = AuroraRecords.ParseTrafficPosition(Payload(
            "#TRPOS;RYR90RC;314;314;17465;436;40.155097;19.122030;1000;;;;;;;0;0;0;;1;;1524;;"));

        Assert.NotNull(pos);
        Assert.Equal(314, pos!.Heading);
        Assert.Equal(17465, pos.AltitudeFt);
        Assert.Equal(436, pos.SpeedKt);
        Assert.Equal(40.155097, pos.Latitude!.Value, 6);
        Assert.Equal(1524, pos.VerticalSpeedFpm);
        Assert.False(pos.OnGround);
        Assert.Null(pos.AssumedStation);
        Assert.Null(pos.TransferFlightLevel);   // l'XFL arriva sempre vuoto: non è scrivibile e non lo popola nessuno
    }

    [Fact]
    public void Posizione_di_un_traffico_assunto_e_selezionato()
    {
        var pos = AuroraRecords.ParseTrafficPosition(Payload(
            "#TRPOS;FDX126;213;209;37987;470;43.371027;8.679002;2000;;;250;;LIZZ_AEW_CTR;;0;1;1;;1;;-48;;"));

        Assert.Equal("250", pos!.AltitudeLabel);
        Assert.Equal("LIZZ_AEW_CTR", pos.AssumedStation);
        Assert.True(pos.IsSelected);
        Assert.False(pos.OnGround);
        Assert.Equal(-48, pos.VerticalSpeedFpm);
        Assert.True(pos.IsAssumedBy("LIZZ_AEW_CTR"));
        Assert.True(pos.IsAssumedBy("lizz_aew_ctr"));
        Assert.False(pos.IsAssumedBy("LIRR_NE_CTR"));
    }

    [Fact]
    public void Un_traffico_al_suolo_ha_il_gate_e_niente_rateo()
    {
        var pos = AuroraRecords.ParseTrafficPosition(Payload(
            "#TRPOS;DLH2MM;206;-1;130;0;44.532725;11.287560;2000;;;;;;;1;0;0;212;1;;0;;"));

        Assert.True(pos!.OnGround);
        Assert.Equal("212", pos.CurrentGate);
        Assert.Equal(0, pos.VerticalSpeedFpm);
    }

    [Fact]
    public void Nessuno_ha_assunto_un_traffico_senza_stazione()
    {
        var pos = AuroraRecords.ParseTrafficPosition(Payload(
            "#TRPOS;AAN203;273;273;35133;418;40.865284;7.927368;2000;;;;;;;0;0;0;;1;;972;;"));

        Assert.False(pos!.IsAssumedBy("LIZZ_AEW_CTR"));
        Assert.False(pos.IsAssumedBy(null));
    }

    [Fact]
    public void Piste_in_uso_con_piu_piste_e_aeroporti_senza_configurazione()
    {
        var rwy = AuroraRecords.ParseControlledRunways(Payload(
            "#CTRLRWY;LICA;10;10;LIRE;;;LIRF;25;16L:16R;LIRP;21L;03R;"));

        Assert.Equal(4, rwy.Count);

        var lirf = rwy.Single(r => r.Icao == "LIRF");
        Assert.Equal(new[] { "25" }, lirf.Departure);
        Assert.Equal(new[] { "16L", "16R" }, lirf.Arrival);

        var lire = rwy.Single(r => r.Icao == "LIRE");
        Assert.Empty(lire.Departure);
        Assert.Empty(lire.Arrival);
    }

    [Fact]
    public void La_rotta_risolta_da_Aurora_porta_i_fix_in_ordine_con_l_ETO()
    {
        var path = AuroraRecords.ParseTrafficPath(Payload(
            "#TRPATHL;RYR90RC;OLGAT:0816;OKIMO:0824;AZHIF:0835;LIME:0928;"));

        Assert.Equal(4, path.Count);
        Assert.Equal(("OLGAT", "0816"), path[0]);
        Assert.Equal(("LIME", "0928"), path[3]);
    }

    [Fact]
    public void I_punti_gia_passati_hanno_ETO_nullo()
    {
        var path = AuroraRecords.ParseTrafficPath(Payload("#TRPATHA;X;OLGAT:-;OKIMO:0824;"));

        Assert.Null(path[0].Eto);
        Assert.Equal("0824", path[1].Eto);
    }

    [Fact]
    public void Una_rotta_vuota_non_e_un_errore()
    {
        Assert.Empty(AuroraRecords.ParseTrafficPath(Payload("#TRPATHL;DLH2MM;")));
    }

    [Fact]
    public void Elenco_dei_traffici_in_range()
    {
        var list = AuroraRecords.ParseList(Payload("#TR;AAN203;DLH2MM;FDX126;"));

        Assert.Equal(new[] { "AAN203", "DLH2MM", "FDX126" }, list);
    }

    [Fact]
    public void Record_troncati_non_fanno_esplodere_i_parser()
    {
        Assert.Null(AuroraRecords.ParseFlightPlan(Payload("#FP;")));
        Assert.Null(AuroraRecords.ParseTrafficPosition(Payload("#TRPOS;")));
        Assert.Empty(AuroraRecords.ParseControlledRunways(Payload("#CTRLRWY;LIRF;25")));   // terzina incompleta

        var partial = AuroraRecords.ParseTrafficPosition(Payload("#TRPOS;X;314;314;17465;"));
        Assert.Equal(17465, partial!.AltitudeFt);
        Assert.Null(partial.AssumedStation);
    }
}
