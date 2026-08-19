using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// **La conversione al modello a sezioni, provata senza database.**
///
/// <para>I casi non sono inventati: sono quelli misurati sul <c>vipi.db</c> vero il 18 agosto 2026 — 40 accordi
/// in 17 coppie, il verso espresso <b>orientando</b> l'accordo (60 clausole su 60 <c>AtoB</c>), due gemelle
/// (<c>#26</c>/<c>#27</c>, arrivi LIBD), un guscio senza ricevente e senza clausole (<c>#41</c>), uno con due
/// capi ma vuoto (<c>#42</c>).</para>
///
/// <para>⚠️ Il valore di questi test non è la copertura: è che la conversione si può <b>smentire</b> prima di
/// toccare l'archivio, che non si può rifare.</para>
/// </summary>
public class AgreementsToSectionsTests
{
    private const int Libb = 1, Lggg = 2, Ldzo = 3, LibdApp = 4;

    [Fact]
    public void Due_accordi_a_versi_opposti_diventano_un_accordo_con_due_sezioni()
    {
        // #13 (LIBB → LGGG, sorvoli) e #32 (LGGG → LIBB, sorvoli): il «reciproco scritto a parte».
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(13, Libb, Lggg, TransferFlowKind.Overflight, order: 13, clauses: Clauses(101, 102)),
            Legacy(32, Lggg, Libb, TransferFlowKind.Overflight, order: 32, clauses: Clauses(201)),
        });

        var a = Assert.Single(plan.Agreements);
        Assert.Equal(13, a.KeepAgreementId);
        Assert.Equal(new[] { 32 }, a.AbsorbedAgreementIds);
        // Canonico: id minore = A. LIBB è A, quindi #13 resta AtoB e #32 si ribalta.
        Assert.Equal(Libb, a.SideASectorId);
        Assert.Equal(Lggg, a.SideBSectorId);
        Assert.Equal(2, a.Sections.Count);
        Assert.Equal(AgreementDirection.AtoB, a.Sections[0].Direction);
        Assert.Equal(new[] { 101, 102 }, a.Sections[0].Clauses.Select(c => c.ClauseId));
        Assert.Equal(AgreementDirection.BtoA, a.Sections[1].Direction);
        Assert.Equal(new[] { 201 }, a.Sections[1].Clauses.Select(c => c.ClauseId));
        Assert.Empty(plan.MergedTwins);
    }

    [Fact]
    public void Il_verso_si_ribalta_solo_quando_il_lato_A_vecchio_non_e_quello_canonico()
    {
        // #37: LYTV_APP (id 9) → LIBB (id 1). Il canonico è LIBB=A, quindi l'accordo scritto «loro → noi»
        // diventa una sezione BtoA. ⚠️ Se questo si rompesse, tutte le frasi direbbero il contrario di ciò che
        // c'è scritto — e nessun test di schema se ne accorgerebbe.
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(37, sideA: 9, sideB: Libb, TransferFlowKind.Arrival, order: 37, clauses: Clauses(301),
                airports: new[] { "LIBD", "LIBR" }),
        });

        var s = Assert.Single(Assert.Single(plan.Agreements).Sections);
        Assert.Equal(AgreementDirection.BtoA, s.Direction);
        Assert.Equal(new[] { "LIBD", "LIBR" }, s.Airports.Select(x => x.Icao));
    }

    [Fact]
    public void Le_gemelle_si_uniscono_in_una_sezione_sola_con_i_gruppi_rinumerati()
    {
        // #26 e #27: stessi enti, stesso tipo, stesso scalo, stesso verso. Erano le due foglie identiche che
        // l'editor mostrava come «relazione spezzata».
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(26, Libb, LibdApp, TransferFlowKind.Arrival, order: 26, airports: new[] { "LIBD" },
                clauses: new[]
                {
                    new LegacyClause(401, AgreementDirection.AtoB, 1, 1, 0),
                    new LegacyClause(402, AgreementDirection.AtoB, 2, 1, 1),
                }),
            Legacy(27, Libb, LibdApp, TransferFlowKind.Arrival, order: 27, airports: new[] { "LIBD" },
                clauses: new[]
                {
                    new LegacyClause(403, AgreementDirection.AtoB, 1, 1, 0),
                    new LegacyClause(404, AgreementDirection.AtoB, 2, 1, 1),
                }),
        });

        var s = Assert.Single(Assert.Single(plan.Agreements).Sections);
        Assert.Equal(new[] { 401, 402, 403, 404 }, s.Clauses.Select(c => c.ClauseId));
        Assert.Equal(new[] { 1, 2, 3, 4 }, s.Clauses.Select(c => c.Order));
        // I due gruppi «1» erano di accordi diversi: uniti nella stessa tabella devono restare DUE gruppi, o le
        // clausole della seconda sembrerebbero varianti della prima.
        Assert.Equal(new int?[] { 1, 1, 2, 2 }, s.Clauses.Select(c => c.VariantGroup));
        Assert.Equal(new[] { 0, 1, 0, 1 }, s.Clauses.Select(c => c.VariantDepth));

        var twin = Assert.Single(plan.MergedTwins);
        Assert.Equal(new[] { 26, 27 }, twin.AgreementIds);
        Assert.Equal("LIBD", twin.Airports);
    }

    [Fact]
    public void Le_prose_delle_gemelle_si_tengono_tutte()
    {
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(1, Libb, Lggg, TransferFlowKind.Arrival, order: 1, airports: new[] { "LIBD" },
                description: "Via ASPIR.", clauses: Clauses(1)),
            Legacy(2, Libb, Lggg, TransferFlowKind.Arrival, order: 2, airports: new[] { "LIBD" },
                description: "Solo con pista 32.", clauses: Clauses(2)),
        });

        var s = Assert.Single(Assert.Single(plan.Agreements).Sections);
        Assert.Equal("Via ASPIR. — Solo con pista 32.", s.Description);
    }

    [Fact]
    public void Un_guscio_senza_un_capo_e_senza_clausole_si_butta()
    {
        // #41: LIRR_NE_CTR, sorvoli, nessun ricevente, nessuna clausola.
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(41, sideA: 7, sideB: 0, TransferFlowKind.Overflight, order: 41),
        });

        Assert.Equal(new[] { 41 }, plan.Discarded);
        Assert.Empty(plan.Agreements);
        Assert.True(plan.CanRun);
    }

    [Fact]
    public void Un_accordo_senza_un_capo_ma_CON_clausole_ferma_la_conversione()
    {
        // Non c'è in archivio, ed è proprio per questo che serve la guardia: buttarlo perderebbe lavoro
        // editoriale in silenzio, e inventargli un capo sarebbe scrivere un accordo che nessuno ha concordato.
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(50, sideA: 7, sideB: 0, TransferFlowKind.Overflight, order: 50, clauses: Clauses(501)),
        });

        Assert.False(plan.CanRun);
        var blocked = Assert.Single(plan.Blocked);
        Assert.Equal(50, blocked.AgreementId);
        Assert.Equal(1, blocked.Clauses);
        Assert.Empty(plan.Discarded);
    }

    [Fact]
    public void Un_accordo_con_due_capi_ma_vuoto_sopravvive_come_sezione_vuota()
    {
        // #42: partenze LIBD·LIBR appena create dal committente. È lavoro in corso, non spazzatura.
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(42, LibdApp, Libb, TransferFlowKind.Departure, order: 42, airports: new[] { "LIBD", "LIBR" }),
        });

        var s = Assert.Single(Assert.Single(plan.Agreements).Sections);
        Assert.Equal(TransferFlowKind.Departure, s.Kind);
        Assert.Empty(s.Clauses);
        Assert.Equal(2, s.Airports.Count);
    }

    [Fact]
    public void Piu_enti_su_un_lato_fermano_la_conversione_invece_di_perderne_uno()
    {
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(60, Libb, Lggg, TransferFlowKind.Overflight, order: 60, clauses: Clauses(601))
                with { SideB = new[] { Lggg, Ldzo } },
        });

        Assert.False(plan.CanRun);
        Assert.Equal(60, Assert.Single(plan.Blocked).AgreementId);
    }

    [Fact]
    public void Le_sezioni_nascono_nell_ordine_in_cui_si_leggono()
    {
        // Aeroporto per aeroporto (arrivi poi partenze), poi i sorvoli nei due versi, poi il resto.
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(1, Libb, Lggg, TransferFlowKind.Overflight, order: 1, clauses: Clauses(1)),
            Legacy(2, Libb, Lggg, TransferFlowKind.Other, order: 2, clauses: Clauses(2)),
            Legacy(3, Lggg, Libb, TransferFlowKind.Arrival, order: 3, airports: new[] { "LIBD" }, clauses: Clauses(3)),
            Legacy(4, Libb, Lggg, TransferFlowKind.Departure, order: 4, airports: new[] { "LIBD" }, clauses: Clauses(4)),
            Legacy(5, Libb, Lggg, TransferFlowKind.Arrival, order: 5, airports: new[] { "LGKF" }, clauses: Clauses(5)),
        });

        var sections = Assert.Single(plan.Agreements).Sections;
        Assert.Equal(
            new[] { "Arrival/LGKF", "Arrival/LIBD", "Departure/LIBD", "Overflight/", "Other/" },
            sections.Select(s => $"{s.Kind}/{string.Join(" · ", s.Airports.Select(x => x.Icao))}"));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, sections.Select(s => s.Order));
    }

    [Fact]
    public void Un_accordo_bilaterale_gia_scritto_produce_due_sezioni_dello_stesso_tipo()
    {
        // In archivio non ce n'erano (il verso si esprimeva orientando l'accordo), ma il modello vecchio lo
        // ammetteva: le due tabelle devono restare due.
        var plan = AgreementsToSections.Plan(new[]
        {
            Legacy(70, Libb, Lggg, TransferFlowKind.Overflight, order: 70, clauses: new[]
            {
                new LegacyClause(701, AgreementDirection.AtoB, 1, null, 0),
                new LegacyClause(702, AgreementDirection.BtoA, 1, null, 0),
            }),
        });

        var sections = Assert.Single(plan.Agreements).Sections;
        Assert.Equal(2, sections.Count);
        Assert.Equal(new[] { AgreementDirection.AtoB, AgreementDirection.BtoA },
            sections.Select(s => s.Direction));
    }

    [Fact]
    public void Nessuna_clausola_si_perde_per_strada()
    {
        var legacy = new[]
        {
            Legacy(13, Libb, Lggg, TransferFlowKind.Overflight, order: 13, clauses: Clauses(101, 102, 103)),
            Legacy(32, Lggg, Libb, TransferFlowKind.Overflight, order: 32, clauses: Clauses(201, 202)),
            Legacy(26, Libb, LibdApp, TransferFlowKind.Arrival, order: 26, airports: new[] { "LIBD" },
                clauses: Clauses(301)),
            Legacy(27, Libb, LibdApp, TransferFlowKind.Arrival, order: 27, airports: new[] { "LIBD" },
                clauses: Clauses(302)),
        };

        var plan = AgreementsToSections.Plan(legacy);

        var before = legacy.SelectMany(a => a.Clauses.Select(c => c.Id)).OrderBy(x => x).ToList();
        var after = plan.Agreements.SelectMany(a => a.Sections).SelectMany(s => s.Clauses)
            .Select(c => c.ClauseId).OrderBy(x => x).ToList();
        Assert.Equal(before, after);
        Assert.Equal(before.Count, plan.ClauseCount);
    }

    // ---- fixture ------------------------------------------------------------------------------------

    private static LegacyAgreement Legacy(int id, int sideA, int sideB, TransferFlowKind kind, int order,
        IReadOnlyList<string>? airports = null, IReadOnlyList<LegacyClause>? clauses = null,
        string? description = null) =>
        new(id, OwnerAccId: 1, kind, description, order,
            sideA == 0 ? System.Array.Empty<int>() : new[] { sideA },
            sideB == 0 ? System.Array.Empty<int>() : new[] { sideB },
            (airports ?? System.Array.Empty<string>())
                .Select((icao, i) => new AgreementAirportRow(icao, null, i + 1)).ToList(),
            clauses ?? System.Array.Empty<LegacyClause>());

    private static IReadOnlyList<LegacyClause> Clauses(params int[] ids) =>
        ids.Select((id, i) => new LegacyClause(id, AgreementDirection.AtoB, i + 1, null, 0)).ToList();
}
