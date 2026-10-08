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
/// L'avviso «quota di un altro settore»: una clausola scritta per un ente d'area che, su quel punto e a quella
/// quota, quel cielo non lo tiene. Carta <c>docs/feature/2026-10-04-copertura-unica.md</c> §10.
///
/// <para>Milano com'è in produzione: <c>WS2</c> ed <c>ES2</c> vanno da SFC a <b>UNL</b> — con gli alti chiusi
/// tengono tutto — e <c>WS5</c> ed <c>ES5</c> partono da FL325. È il dato che ha smentito la prima regola («quota
/// fuori dalla banda del settore scritto»): per il catalogo FL350 è dentro ES2. La domanda giusta è se c'è un
/// settore <b>più specifico</b>, che da lui pende, a tenere quel cielo.</para>
/// </summary>
public class AgreementLevelCheckTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Padova = "LIPP_CE1_CTR";
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    // Tre quadrati affiancati: ovest di Milano, est di Milano, Padova. [lon, lat].
    private const string Ovest = "[[8,44],[10,44],[10,46],[8,46]]";
    private const string Est = "[[10,44],[12,44],[12,46],[10,46]]";
    private const string Veneto = "[[12,44],[14,44],[14,46],[12,46]]";
    private const string TuttaMilano = "[[8,44],[12,44],[12,46],[8,46]]";

    private static SectorVolumeRow Riga(string cs, string? padre, string poligono, int basso, int? alto,
        SectorType tipo = SectorType.Ctr) =>
        new(cs, padre, tipo, null,
            new[] { new ShapePart(poligono, basso, alto, AirspaceDatum.Amsl, AirspaceDatum.Amsl, "", "") },
            ShapeSource.Source);

    private static readonly List<SectorVolumeRow> Settori = new()
    {
        Riga(Ws2, null, Ovest, 0, null),
        Riga(Es2, Ws2, Est, 0, null),
        Riga(Ws5, Ws2, Ovest, 32500, null),
        Riga(Es5, Es2, Est, 32500, null),
        Riga(Padova, null, Veneto, 0, null),
        // Stanno sopra tutto il cielo di tutti e pendono da ES2: se contassero, «più specifici» lo sarebbero sempre.
        Riga("LIMM_FSS", Es2, TuttaMilano, 0, 19500),
        Riga("LIMM_MIL_CTR", Es2, TuttaMilano, 0, null),
    };

    private static readonly Dictionary<string, string> Padri = Settori
        .Where(s => s.ParentCallsign is not null).ToDictionary(s => s.Callsign, s => s.ParentCallsign!, OIC);

    private static readonly Dictionary<string, IReadOnlyList<FallbackRow>> Dichiarate = new(OIC)
    {
        [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: 32500, TopFeet: null) },
    };

    private static readonly CopPositions Punti = new(new[]
    {
        ("DENTR", 45.0, 11.0),   // dentro l'est di Milano
        ("FUORI", 45.0, 12.5),   // oltre il confine: è già Padova
        ("OVEST", 45.0, 9.0),
    });

    private static IReadOnlyList<AgreementLevelWarning> Avvisi(params AgreementRow[] accordi) =>
        AgreementLevelCheck.Find(accordi, Settori, Punti, Dichiarate, cs => Padri.GetValueOrDefault(cs));

    [Fact]
    public void Scritta_per_ES2_a_FL350_ma_li_il_cielo_e_di_ES5()
    {
        var avviso = Assert.Single(Avvisi(Accordo(Es2, Padova, Clausola(1, "DENTR", 350, LevelConstraint.Exact))));

        Assert.Equal((Es2, Es5, true, "DENTR", 1), (avviso.Written, avviso.Holder, avviso.WrittenIsSender, avviso.Cop, avviso.ClauseId));
    }

    [Fact]
    public void Vale_anche_per_chi_riceve()
    {
        var avviso = Assert.Single(Avvisi(Accordo(Padova, Es2, Clausola(1, "DENTR", 350, LevelConstraint.Exact))));

        Assert.Equal((Es2, Es5, false), (avviso.Written, avviso.Holder, avviso.WrittenIsSender));
    }

    [Theory]
    [InlineData(300, LevelConstraint.Exact)]       // sotto FL325 il cielo è proprio di ES2
    [InlineData(325, LevelConstraint.Exact)]       // sul confine esatto non si giudica
    [InlineData(350, LevelConstraint.AtOrBelow)]   // «FL350 o inferiore» vale anche sotto FL325
    [InlineData(200, LevelConstraint.AtOrAbove)]   // «FL200 o superiore» comincia dove tiene lui
    public void Se_l_ente_scritto_tiene_almeno_una_delle_quote_ammesse_non_e_un_avviso(int fl, LevelConstraint vincolo)
    {
        Assert.Empty(Avvisi(Accordo(Es2, Padova, Clausola(1, "DENTR", fl, vincolo))));
    }

    [Fact]
    public void Tutta_sopra_il_confine_e_un_avviso_anche_con_un_vincolo_aperto()
    {
        Assert.Equal(Es5, Assert.Single(Avvisi(Accordo(Es2, Padova, Clausola(1, "DENTR", 330, LevelConstraint.AtOrAbove)))).Holder);
    }

    [Fact]
    public void Un_punto_fuori_dal_settore_scritto_non_dice_niente()
    {
        // «A volte il trasferimento avviene un po' fuori»: la geometria non sa di chi sia quel cielo fra i settori
        // che pendono da ES2, e tace. Dall'altra parte Padova lo tiene, ed è giusto così.
        Assert.Empty(Avvisi(Accordo(Es2, Padova, Clausola(1, "FUORI", 350, LevelConstraint.Exact))));
    }

    [Fact]
    public void Il_settore_piu_specifico_che_e_l_altro_capo_dell_accordo_non_e_un_avviso()
    {
        // WS2 → ES2: ES2 pende da WS2 e tiene quel punto, ma è il RICEVENTE — è il trasferimento, non un errore.
        Assert.Empty(Avvisi(Accordo(Ws2, Es2, Clausola(1, "DENTR", 200, LevelConstraint.Exact))));
    }

    [Fact]
    public void Fra_due_settori_della_stessa_famiglia_si_guarda_ancora_la_quota()
    {
        // WS2 → ES2 a FL350 su un punto dell'ovest: lì, a quella quota, chi cede davvero è WS5.
        var avvisi = Avvisi(Accordo(Ws2, Es2, Clausola(1, "OVEST", 350, LevelConstraint.Exact)));

        Assert.Equal((Ws2, Ws5, true), Assert.Single(avvisi.Select(a => (a.Written, a.Holder, a.WrittenIsSender))));
    }

    [Fact]
    public void Servizi_informazioni_e_militari_non_sono_mai_il_settore_piu_specifico()
    {
        // FSS e MIL pendono da ES2 e coprono quel punto a FL150: contassero, ogni clausola di ES2 sarebbe un avviso.
        Assert.Empty(Avvisi(Accordo(Es2, Padova, Clausola(1, "DENTR", 150, LevelConstraint.Exact))));
    }

    [Fact]
    public void Decide_la_quota_a_cui_il_traffico_passa_di_mano()
    {
        // Autorizzato FL350, trasferito a FL300: a FL300 il cielo è di ES2.
        var clausola = Clausola(1, "DENTR", 350, LevelConstraint.Exact) with
        {
            HandoffKind = TransferHandoffKind.Point, HandoffLevelValue = 300, HandoffLevelUnit = LevelUnit.Fl,
            HandoffLevelConstraint = LevelConstraint.Exact,
        };

        Assert.Empty(Avvisi(Accordo(Es2, Padova, clausola)));
    }

    [Theory]
    [InlineData("ALL")]       // non è un punto
    [InlineData("ZZZZZ")]     // il catalogo non sa dov'è
    public void Dove_non_c_e_un_punto_noto_la_geometria_tace(string cops)
    {
        Assert.Empty(Avvisi(Accordo(Es2, Padova, Clausola(1, cops, 350, LevelConstraint.Exact))));
    }

    [Fact]
    public void Un_livello_speciale_non_ha_una_quota_su_cui_ragionare()
    {
        var clausola = Clausola(1, "DENTR", 350, LevelConstraint.Special) with { LevelValue = null, LevelSpecial = "per aerovia" };

        Assert.Empty(Avvisi(Accordo(Es2, Padova, clausola)));
    }

    [Fact]
    public void Nel_cruscotto_le_clausole_di_una_sezione_fanno_una_voce_sola_con_quel_che_serve_a_spostarle()
    {
        var accordo = Accordo(Es2, Padova,
            Clausola(1, "DENTR", 350, LevelConstraint.Exact),
            Clausola(2, "DENTR", 370, LevelConstraint.Exact),
            Clausola(3, "DENTR", 200, LevelConstraint.Exact));

        var lacune = AgreementGaps.Find("LIMM", new[] { accordo }, Array.Empty<SuggestionSector>(), Array.Empty<string>(),
            new HashSet<string>(), new Dictionary<string, SectorType>(), Avvisi(accordo));

        var voce = Assert.Single(lacune, g => g.Kind == AgreementGapKind.LevelOutsideSector);
        Assert.Equal((2, Es2, Es5, true), (voce.Count, voce.Written, voce.Holder, voce.WrittenIsSender));
        Assert.Equal(new[] { 1, 2 }, voce.ClauseIds);
        Assert.Equal((accordo.Id, 1), (voce.AgreementId, voce.SectionId));
        Assert.Equal(2, voce.Items.Count);
        Assert.All(voce.Items, i => Assert.StartsWith("DENTR ", i));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    /// <summary>Un accordo con una sola sezione di sorvoli in cui cede <paramref name="cede"/>.</summary>
    private static AgreementRow Accordo(string cede, string riceve, params AgreementClauseRow[] clausole) => new()
    {
        Id = 7,
        OwnerAccCode = "LIMM",
        SideA = new AgreementEndpoint(1, cede),
        SideB = new AgreementEndpoint(2, riceve),
        Order = 1,
        Sections = new[]
        {
            new AgreementSectionRow
            {
                Id = 1, Kind = TransferFlowKind.Overflight, Direction = AgreementDirection.AtoB, Order = 1,
                Airports = Array.Empty<AgreementAirportRow>(), Clauses = clausole,
            },
        },
    };

    private static AgreementClauseRow Clausola(int id, string cops, int fl, LevelConstraint vincolo) => new()
    {
        Id = id, SectionId = 1, Cops = cops, LevelValue = fl, LevelUnit = LevelUnit.Fl, LevelConstraint = vincolo, Order = id,
    };
}
