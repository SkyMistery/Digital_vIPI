using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «Come risalirebbe questo coordinamento»: la discesa per intero. Carta
/// <c>docs/feature/2026-09-10-rinvio-geometrico.md</c> Parte 10.
///
/// <para>Stessa geometria di <see cref="CoverageFallbackTests"/>, e per la stessa ragione: la forma e' quella
/// vera di Milano — ovest e est divisi a lon 10, MIL sovrapposto su tutta la FIR, l'FSS SFC–FL195.</para>
/// </summary>
public class RisalitaScalaTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Mil = "LIMM_MIL_CTR", Ane = "LIMC_ANE_APP", Es0 = "LIPX_ES0_APP", Fss = "LIMM_FSS";
    private const int Split = 32500;

    private const string Ovest = "[[8,44],[10,44],[10,46],[8,46]]";
    private const string Est = "[[10,44],[12,44],[12,46],[10,46]]";
    private const string Fir = "[[8,44],[12,44],[12,46],[8,46]]";
    private const string Tma = "[[9,44.5],[11,44.5],[11,45.5],[9,45.5]]";

    private static SectorVolumeRow Riga(string cs, string? padre, SectorType tipo, string poligono,
        int? basso, int? alto) =>
        new(cs, padre, tipo, null,
            new[] { new ShapePart(poligono, basso, alto, AirspaceDatum.Amsl, AirspaceDatum.Amsl, "", "") },
            ShapeSource.Source, "LIMM");

    private static readonly List<SectorVolumeRow> Settori = new()
    {
        Riga(Ws2, null, SectorType.Ctr, Ovest, 0, Split),
        Riga(Es2, Ws2, SectorType.Ctr, Est, 0, Split),
        Riga(Ws5, Ws2, SectorType.Ctr, Ovest, Split, null),
        Riga(Es5, Es2, SectorType.Ctr, Est, Split, null),
        Riga(Mil, Ws2, SectorType.Ctr, Fir, 0, null),
        Riga(Fss, Ws2, SectorType.Ctr, Fir, 0, 19500),
        Riga(Ane, Ws2, SectorType.App, Tma, 0, 19500),
        Riga(Es0, Es2, SectorType.App, Tma, 0, 19500),
    };

    private static readonly Dictionary<string, string?> Padri = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2, [Mil] = Ws2, [Fss] = Ws2, [Ane] = Ws2, [Es0] = Es2,
    };

    private static readonly CopPositions Punti = new(new[]
    {
        ("GHE", 45.0, 10.5),      // est
        ("TOP", 45.0, 9.0),       // ovest
        ("NELAB", 45.2, 10.8),    // est, un altro
        ("ZZZZZ", 45.0, 30.0),    // fuori da ogni poligono
    });

    private static CoverageFallbackContext Contesto(
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>>? righe = null)
    {
        var tutti = new HashSet<string>(Settori.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
        return new CoverageFallbackContext(Settori, tutti, Punti,
            righe ?? RinvioSuMil, cs => Padri.GetValueOrDefault(cs));
    }

    /// <summary>La riga che si scrive sul MIL: una sola, per tutti i punti.</summary>
    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> RinvioSuMil =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Mil] = new[] { new FallbackRow("", null, null, FallbackTargetKind.Coverage) },
        };

    private static RisalitaScalaDiUnPunto Scala(string cop, string cedente = Es0, int? quota = 14000,
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>>? righe = null) =>
        RisalitaScala.Costruisci(Mil, cop, quota, cedente, Contesto(righe));

    private static string[] Nomi(RisalitaScalaDiUnPunto s) => s.Gradini.Select(g => g.Callsign).ToArray();

    // =====================================================================================================

    /// <summary>
    /// La scala di Ghedi: il ricevente scritto, poi la copertura del punto (est ⇒ ES2), poi — chiuso ES2 —
    /// ancora la copertura, che ora risponde WS2, e infine UNICOM.
    /// </summary>
    [Fact]
    public void Da_est_la_scala_scende_MIL_ES2_WS2_UNICOM()
    {
        var s = Scala("GHE");

        Assert.Equal(new[] { Mil, Es2, Ws2, "UNICOM" }, Nomi(s));
        Assert.Equal(RisalitaMotivo.RiceventeScritto, s.Gradini[0].Motivo);
        Assert.Equal(RisalitaMotivo.Copertura, s.Gradini[1].Motivo);
        Assert.Equal(RisalitaMotivo.Unicom, s.Gradini[3].Motivo);
    }

    /// <summary>Da ovest la scala e' piu' corta: il punto sta solo in WS2.</summary>
    [Fact]
    public void Da_ovest_la_scala_scende_MIL_WS2_UNICOM()
    {
        var s = Scala("TOP");

        Assert.Equal(new[] { Mil, Ws2, "UNICOM" }, Nomi(s));
    }

    /// <summary>
    /// 🔴 <b>La prova che la scala NON e' la lista dei candidati.</b> Al secondo gradino il rinvio si
    /// <b>richiede</b> con ES2 chiuso, e risponde WS2 — che qui coincide col padre di ES2, e per questo su
    /// Milano le due strade sembrano la stessa. La differenza si vede dal MOTIVO: il terzo gradino e'
    /// «copertura», non «padre».
    /// </summary>
    [Fact]
    public void Il_gradino_dopo_viene_da_un_rinvio_RICHIESTO_non_dal_padre()
    {
        var s = Scala("GHE");

        Assert.Equal(Ws2, s.Gradini[2].Callsign);
        Assert.Equal(RisalitaMotivo.Copertura, s.Gradini[2].Motivo);
    }

    /// <summary>Senza rinvii scritti la scala e' quella dei padri, e i motivi lo dicono.</summary>
    [Fact]
    public void A_tabella_vuota_la_scala_e_quella_dei_padri()
    {
        var vuote = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase);

        var s = Scala("GHE", righe: vuote);

        Assert.Equal(new[] { Mil, Ws2, "UNICOM" }, Nomi(s));
        Assert.Equal(RisalitaMotivo.Padre, s.Gradini[1].Motivo);
    }

    /// <summary>Una riga dichiarata con fascia porta la sua fascia nel gradino: e' quel che si vuole leggere.</summary>
    [Fact]
    public void Una_riga_dichiarata_porta_la_sua_fascia()
    {
        var righe = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase)
        {
            [Mil] = new[] { new FallbackRow(Es5, BaseFeet: 0, TopFeet: Split) },
        };

        var s = Scala("GHE", righe: righe);

        Assert.Equal(Es5, s.Gradini[1].Callsign);
        Assert.Equal(RisalitaMotivo.RigaDichiarata, s.Gradini[1].Motivo);
        Assert.Equal(0, s.Gradini[1].BaseFeet);
        Assert.Equal(Split, s.Gradini[1].TopFeet);
    }

    /// <summary>
    /// ⚠️ Un CoP che non e' un punto: il rinvio non risponde, resta il padre — e l'esito dice PERCHE', o chi
    /// legge crede a un guasto.
    /// </summary>
    [Fact]
    public void Un_CoP_che_non_e_un_punto_lascia_la_scala_dei_padri_e_lo_dice()
    {
        var s = Scala("Y01-Y12");

        Assert.Equal(new[] { Mil, Ws2, "UNICOM" }, Nomi(s));
        Assert.Equal(RisalitaMotivo.Padre, s.Gradini[1].Motivo);
        Assert.Equal(CoverageFallbackOutcome.NotAPoint, s.Esito!.Value.Outcome);
    }

    [Fact]
    public void Un_punto_che_nessuno_copre_lo_dice()
    {
        var s = Scala("ZZZZZ");

        Assert.Equal(CoverageFallbackOutcome.NobodyCovers, s.Esito!.Value.Outcome);
    }

    /// <summary>Un ricevente senza ripieghi e senza padre: chiuso lui, UNICOM. E' il caso che il rilievo pesca.</summary>
    [Fact]
    public void Chi_non_ha_ne_ripieghi_ne_padre_finisce_subito_su_UNICOM()
    {
        var vuote = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase);
        var contesto = new CoverageFallbackContext(Settori,
            new HashSet<string>(Settori.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase),
            Punti, vuote, _ => null);

        var s = RisalitaScala.Costruisci(Ws2, "GHE", 14000, Es0, contesto);

        Assert.Equal(new[] { Ws2, "UNICOM" }, Nomi(s));
        Assert.True(RisalitaScala.FinisceSubitoSuUnicom(s));
    }

    // =====================================================================================================
    //  Il raggruppamento
    // =====================================================================================================

    /// <summary>
    /// ⚠️ Le scale identiche si raggruppano, e il raggruppamento si fa <b>dopo</b> aver risolto: due punti
    /// diversi (GHE e NELAB) danno la stessa scala, e non lo si sa finche' non si chiede.
    /// </summary>
    [Fact]
    public void Le_scale_identiche_stanno_in_un_blocco_solo()
    {
        var scale = new[] { Scala("GHE"), Scala("NELAB"), Scala("TOP") };

        var blocchi = RisalitaScala.Raggruppa(scale);

        Assert.Equal(2, blocchi.Count);
        Assert.Equal(new[] { "GHE", "NELAB" }, blocchi[0].Punti);
        Assert.Equal(new[] { "TOP" }, blocchi[1].Punti);
    }

    /// <summary>
    /// ⚠️ Due scale con gli STESSI gradini ma un esito diverso restano due blocchi: la frase da mostrare
    /// cambia, e raggrupparle direbbe che c'e' una cosa sola da fare mentre ce ne sono due.
    /// </summary>
    [Fact]
    public void Stessi_gradini_ma_esito_diverso_restano_due_blocchi()
    {
        var blocchi = RisalitaScala.Raggruppa(new[] { Scala("TOP"), Scala("Y01-Y12") });

        Assert.Equal(2, blocchi.Count);
        Assert.Equal(Nomi(blocchi[0].Scala), Nomi(blocchi[1].Scala));   // gradini identici
    }

    [Fact]
    public void Il_tetto_dei_punti_e_dieci_e_sta_in_un_posto_solo() =>
        Assert.Equal(10, RisalitaScala.MassimoPunti);
}
