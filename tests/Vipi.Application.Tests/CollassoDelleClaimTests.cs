using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Content;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Con che cosa <see cref="SectorVolumeMap.BuildClaims"/> collassa un settore chiuso — e perche' non e' una
/// costante.
///
/// <para>🔴 <b>Il difetto che questi test inchiodano.</b> <c>CoverageResolver</c> conosce i soli
/// <c>ParentCallsign</c>: le righe di ripiego dichiarate <b>no</b>. Con <c>ES5</c> chiuso a FL350, WS5 ed
/// ES2 aperti, la <b>catena</b> risponde WS5 (per la riga «FL325–UNL → WS5») e la <b>geometria</b>
/// risponderebbe ES2 (per il padre). Due risposte diverse alla stessa domanda: e' «due alberi» ricostruito
/// in un posto nuovo, ed e' il difetto che questa base di codice ha gia' pagato una volta.</para>
///
/// <para>⚠️ L'albero e' quello <b>di produzione</b>, letto su <c>atc.it.ivao.aero</c> il 9 settembre 2026:
/// <c>ES5</c> e' figlio di <b>ES2</b> (non di WS5, come diceva il <c>vipi.db</c> di sviluppo fino al 10
/// settembre), e la riga dichiarata su ES5 e' <c>FL325–UNL → WS5</c>. Su un albero con ES5 sotto WS5
/// questi test direbbero verde senza distinguere niente, perche' i due collassi coinciderebbero.</para>
/// </summary>
public class CollassoDelleClaimTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";

    /// <summary>FL325: il piede misurato dello strato alto di Milano.</summary>
    private const int Split = 32500;

    private const string Ovest = "[[8,44],[10,44],[10,46],[8,46]]";
    private const string Est = "[[10,44],[12,44],[12,46],[10,46]]";

    private static SectorVolumeRow Riga(string cs, string? padre, string poligono, int? basso, int? alto) =>
        new(cs, padre, SectorType.Ctr, null,
            new[] { new ShapePart(poligono, basso, alto, AirspaceDatum.Amsl, AirspaceDatum.Amsl, "", "") },
            ShapeSource.Source);

    /// <summary>L'albero di PRODUZIONE: ES5 pende da ES2, WS5 da WS2.</summary>
    private static readonly List<SectorVolumeRow> Milano = new()
    {
        Riga(Ws2, null, Ovest, 0, Split),
        Riga(Es2, Ws2, Est, 0, Split),
        Riga(Ws5, Ws2, Ovest, Split, null),
        Riga(Es5, Es2, Est, Split, null),
    };

    private static readonly Dictionary<string, string?> Padri = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ws2] = null, [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2,
    };

    /// <summary>La riga vera che sta in produzione su ES5.</summary>
    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> Dichiarate =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: Split, TopFeet: null) },
        };

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    /// <summary>Il collassatore che passa chi risolve un rinvio: la catena, alla quota del punto.</summary>
    private static Func<string, string?> CatenaA(int quotaFt, IReadOnlySet<string> online) =>
        cs => TransferOnlineResolver.FirstOnline(
            FallbackChain.Candidates(cs, quotaFt, Dichiarate, c => Padri.GetValueOrDefault(c)), online);

    private static string? PadroneDi(IReadOnlyList<SectorClaim> claims, string settore) =>
        claims.FirstOrDefault(c => c.Volume.Callsign.Equals(settore, StringComparison.OrdinalIgnoreCase))
            .SessionCallsign;

    [Fact]
    public void Senza_collassatore_ES5_chiuso_finisce_al_PADRE_e_la_riga_dichiarata_non_conta()
    {
        var claims = SectorVolumeMap.BuildClaims(Milano, Online(Ws5, Es2));

        // E' il comportamento delle statistiche, e resta questo di proposito.
        Assert.Equal(Es2, PadroneDi(claims, Es5));
    }

    [Fact]
    public void Con_la_catena_ES5_chiuso_a_FL350_finisce_a_WS5_come_dice_la_riga()
    {
        var online = Online(Ws5, Es2);
        var claims = SectorVolumeMap.BuildClaims(Milano, online, CatenaA(35000, online));

        Assert.Equal(Ws5, PadroneDi(claims, Es5));
    }

    /// <summary>
    /// ⚠️ La riga vale <b>sopra FL325</b>: sotto, la catena e la copertura per soli padri devono tornare a
    /// dire la stessa cosa. Senza questo, il test di sopra proverebbe solo che il delegato viene chiamato.
    /// </summary>
    [Fact]
    public void Sotto_la_fascia_la_riga_non_si_applica_e_i_due_collassi_coincidono()
    {
        var online = Online(Ws5, Es2);

        var perPadri = SectorVolumeMap.BuildClaims(Milano, online);
        var conCatena = SectorVolumeMap.BuildClaims(Milano, online, CatenaA(25000, online));

        Assert.Equal(Es2, PadroneDi(perPadri, Es5));
        Assert.Equal(Es2, PadroneDi(conCatena, Es5));
    }

    /// <summary>
    /// A tabella di ripieghi <b>vuota</b> il collassatore della catena e quello dei padri devono dare lo
    /// stesso identico esito, settore per settore: e' la garanzia che accendere il rinvio non muove niente
    /// finche' nessuno scrive una riga.
    /// </summary>
    [Fact]
    public void A_tabella_vuota_i_due_collassi_danno_lo_stesso_esito()
    {
        var online = Online(Ws2);
        var vuote = new Dictionary<string, IReadOnlyList<FallbackRow>>(StringComparer.OrdinalIgnoreCase);

        var perPadri = SectorVolumeMap.BuildClaims(Milano, online);
        var conCatena = SectorVolumeMap.BuildClaims(Milano, online,
            cs => TransferOnlineResolver.FirstOnline(
                FallbackChain.Candidates(cs, 35000, vuote, c => Padri.GetValueOrDefault(c)), online));

        foreach (var settore in new[] { Ws2, Es2, Ws5, Es5 })
            Assert.Equal(PadroneDi(perPadri, settore), PadroneDi(conCatena, settore));
    }
}
