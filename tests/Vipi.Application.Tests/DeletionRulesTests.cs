using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le <b>politiche di protezione</b> dell'eliminazione (carta del 26 agosto 2026, §2). Sono regole pure —
/// dai fatti esce il piano — e questi test le fissano una per una, perché è l'unico posto dove si può
/// leggere, senza database, che cosa il sistema promette prima di cancellare qualcosa.
/// </summary>
public class DeletionRulesTests
{
    private static readonly DateTime Adesso = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Penultimo = Adesso.AddDays(-1);
    private static readonly DateTime Vecchio = Adesso.AddDays(-5);   // due giri non l'hanno nominato

    private static SectorFacts Settore(
        string callsign = "LIRR_W_CTR",
        SectorType tipo = SectorType.Ctr,
        SectorKind kind = SectorKind.Acc,
        int? airportId = null,
        int? parentId = null,
        string? parentCallsign = null,
        bool proiettato = true,
        bool catalogoManuale = false,
        DateTime? timbro = null,
        IReadOnlyList<ChildFacts>? figli = null,
        IReadOnlyList<DocRefFacts>? documenti = null,
        IReadOnlyList<AgreementFacts>? accordi = null) =>
        new(1, callsign, "Roma Ovest", "LIRR", tipo, kind, airportId, airportId is null ? null : "LIRF",
            parentId, parentCallsign, proiettato, catalogoManuale, timbro ?? Vecchio,
            figli ?? Array.Empty<ChildFacts>(),
            documenti ?? Array.Empty<DocRefFacts>(),
            accordi ?? Array.Empty<AgreementFacts>());

    private static DocRefFacts Documento(bool ancoraQui = false, bool restaAncorato = true,
        IReadOnlyList<int>? parti = null, IReadOnlyList<BlockRefFacts>? blocchi = null) =>
        new(7, "vIPI Roma", ancoraQui, parti ?? Array.Empty<int>(),
            blocchi ?? Array.Empty<BlockRefFacts>(), restaAncorato);

    // ── D1: la gerarchia ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void I_figli_passano_al_nonno()
    {
        var p = DeletionRules.PerSettore(
            Settore(parentId: 42, parentCallsign: "LIRR_CTR",
                figli: new[] { new ChildFacts(10, "LIRF_APP"), new ChildFacts(11, "LIRA_APP") }),
            Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal(42, p.Azioni.NuovoPadreDeiFigli);
        Assert.Equal(new[] { 10, 11 }, p.Azioni.FigliDaRiappendere);
        Assert.All(p.SiSposta, r => Assert.Contains("passa sotto LIRR_CTR", r));
    }

    [Fact]
    public void I_figli_di_una_radice_diventano_radici()
    {
        var p = DeletionRules.PerSettore(
            Settore(parentId: null, parentCallsign: null, figli: new[] { new ChildFacts(10, "LIRF_APP") }),
            Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Null(p.Azioni.NuovoPadreDeiFigli);
        Assert.Contains("diventa radice", Assert.Single(p.SiSposta));
    }

    // ── D2/D3: i documenti ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Un_documento_che_resta_ancorato_si_sgancia_e_va_rivisto()
    {
        var p = DeletionRules.PerSettore(
            Settore(documenti: new[] { Documento(ancoraQui: true, restaAncorato: true) }), Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal(new[] { 7 }, p.Azioni.DocumentiDaMarcare);
        Assert.Contains("perde questo settore", Assert.Single(p.DaRivedere));
    }

    [Fact]
    public void Se_il_settore_e_l_ultimo_aggancio_ci_si_ferma()
    {
        var p = DeletionRules.PerSettore(
            Settore(documenti: new[] { Documento(ancoraQui: true, restaAncorato: false) }), Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Contains("elimina prima il documento", Assert.Single(p.Blocca).Testo);
        Assert.Empty(p.Azioni.DocumentiDaMarcare);
    }

    [Fact]
    public void Una_parte_di_vloa_si_toglie_se_ne_resta_un_altra()
    {
        var p = DeletionRules.PerSettore(
            Settore(documenti: new[] { Documento(parti: new[] { 33 }, restaAncorato: true) }), Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal(new[] { 33 }, p.Azioni.PartiDaEliminare);
        Assert.Contains("perde una parte", Assert.Single(p.DaRivedere));
    }

    // ── D4: i blocchi, le tre vie ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Un_blocco_che_lo_ha_come_estremo_si_elimina_perche_sarebbe_falso()
    {
        var p = DeletionRules.PerSettore(
            Settore(documenti: new[]
            {
                Documento(blocchi: new[] { new BlockRefFacts(90, "Coordinamenti", Scope: false, Estremo: true) }),
            }), Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal(new[] { 90 }, p.Azioni.BlocchiDaEliminare);
        Assert.Empty(p.Azioni.BlocchiDaSganciare);
        Assert.Contains(p.Muore, m => m.Contains("passaggio da o verso"));
    }

    [Fact]
    public void Un_blocco_che_lo_ha_solo_come_ambito_resta_sganciato()
    {
        var p = DeletionRules.PerSettore(
            Settore(documenti: new[]
            {
                Documento(blocchi: new[] { new BlockRefFacts(91, "Procedure", Scope: true, Estremo: false) }),
            }), Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal(new[] { 91 }, p.Azioni.BlocchiDaSganciare);
        Assert.Empty(p.Azioni.BlocchiDaEliminare);
        Assert.Contains("senza il settore a cui era riferito", Assert.Single(p.DaRivedere));
    }

    [Fact]
    public void Un_blocco_su_un_documento_che_perde_l_ultimo_aggancio_blocca()
    {
        var p = DeletionRules.PerSettore(
            Settore(documenti: new[]
            {
                Documento(restaAncorato: false,
                    blocchi: new[] { new BlockRefFacts(92, "Procedure", Scope: true, Estremo: false) }),
            }), Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Empty(p.Azioni.BlocchiDaSganciare);
    }

    // ── D5: gli accordi ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Un_accordo_blocca_sempre_e_dice_quale()
    {
        var p = DeletionRules.PerSettore(
            Settore(accordi: new[] { new AgreementFacts(5, "LIRR_CTR ↔ LIMM_CTR", "/x") }), Penultimo);

        Assert.False(p.Eliminabile);
        var b = Assert.Single(p.Blocca);
        Assert.Contains("LIRR_CTR ↔ LIMM_CTR", b.Testo);
        Assert.Equal("/x", b.Href);
    }

    // ── D6: la torre ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void La_torre_non_si_elimina_da_sola()
    {
        var p = DeletionRules.PerSettore(
            Settore("LIRF_TWR", SectorType.Twr, SectorKind.Airport, airportId: 3), Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Contains("solo insieme all'intero aeroporto", Assert.Single(p.Blocca).Testo);
    }

    [Fact]
    public void La_torre_cade_insieme_allo_scalo()
    {
        var p = DeletionRules.PerSettore(
            Settore("LIRF_TWR", SectorType.Twr, SectorKind.Airport, airportId: 3), Penultimo,
            dentroLoScalo: true);

        Assert.True(p.Eliminabile);
    }

    [Fact]
    public void Gli_altri_settori_di_scalo_si_eliminano_da_soli()
    {
        var p = DeletionRules.PerSettore(
            Settore("LIRF_GND", SectorType.Gnd, SectorKind.Airport, airportId: 3), Penultimo);

        Assert.True(p.Eliminabile);
    }

    // ── D8: la sorgente ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Un_settore_che_la_sorgente_manda_ancora_non_si_elimina()
    {
        var p = DeletionRules.PerSettore(Settore(timbro: Adesso.AddHours(-1)), Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Contains("la manda ancora", Assert.Single(p.Blocca).Testo);
    }

    [Fact]
    public void Un_settore_aggiunto_a_mano_non_conosce_la_regola_delle_due_chiamate()
    {
        // Riga di catalogo manuale (es. un APP estero catalogato in Confinanti): la sorgente non l'ha mai
        // mandata, e il timbro non dice niente su di lei.
        var p = DeletionRules.PerSettore(
            Settore(catalogoManuale: true, timbro: Adesso), Penultimo);

        Assert.True(p.Eliminabile);
    }

    [Fact]
    public void Un_settore_non_proiettato_non_conosce_la_regola_delle_due_chiamate()
    {
        var p = DeletionRules.PerSettore(Settore(proiettato: false, timbro: Adesso), Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Empty(p.Azioni.CallsignDiCatalogoDaTogliere);
    }

    [Fact]
    public void Di_un_settore_proiettato_si_toglie_anche_la_riga_di_catalogo()
    {
        // Togliere solo la proiezione lo farebbe tornare al primo sync, e chi guarda lo vedrebbe risorgere.
        var p = DeletionRules.PerSettore(Settore(), Penultimo);

        Assert.Equal(new[] { "LIRR_W_CTR" }, p.Azioni.CallsignDiCatalogoDaTogliere);
    }

    // ── D7: l'aeroporto ──────────────────────────────────────────────────────────────────────────────

    private static AirportFacts Scalo(int? documentId = null, params SectorFacts[] settori) =>
        new(3, "LIRF", "Roma Fiumicino", "LIRR", Vecchio, documentId,
            documentId is null ? null : "vIPI LIRF", settori);

    [Fact]
    public void Con_lo_scalo_muoiono_tutti_i_suoi_settori()
    {
        var p = DeletionRules.PerAeroporto(
            Scalo(null,
                Settore("LIRF_TWR", SectorType.Twr, SectorKind.Airport, airportId: 3) with { SectorId = 1 },
                Settore("LIRF_GND", SectorType.Gnd, SectorKind.Airport, airportId: 3) with { SectorId = 2 }),
            Penultimo, Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal(new[] { 1, 2 }, p.Azioni.SettoriDaEliminare);
        Assert.Equal(3, p.Azioni.AeroportoDaEliminare);
        Assert.Contains(p.Muore, m => m.Contains("l'aeroporto LIRF"));
    }

    [Fact]
    public void Il_documento_dello_scalo_si_elimina_prima_a_mano()
    {
        var p = DeletionRules.PerAeroporto(Scalo(documentId: 12), Penultimo, Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Contains("elimina prima il documento", Assert.Single(p.Blocca).Testo);
    }

    [Fact]
    public void Uno_scalo_che_la_sorgente_manda_ancora_non_si_elimina()
    {
        var f = Scalo() with { LastSeenAtUtc = Adesso };
        var p = DeletionRules.PerAeroporto(f, Penultimo, Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Contains("la manda ancora", Assert.Single(p.Blocca).Testo);
    }

    [Fact]
    public void Un_settore_dello_scalo_che_blocca_blocca_tutto_lo_scalo()
    {
        var p = DeletionRules.PerAeroporto(
            Scalo(null, Settore("LIRF_APP", SectorType.App, SectorKind.Airport, airportId: 3,
                documenti: new[] { Documento(ancoraQui: true, restaAncorato: false) })),
            Penultimo, Penultimo);

        Assert.False(p.Eliminabile);
    }

    // ── La ACC ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Una_acc_piena_si_svuota_prima()
    {
        var p = DeletionRules.PerAcc(new AccFacts("LIRR", "Roma", Vecchio, Settori: 12, Aeroporti: 4), Penultimo);

        Assert.False(p.Eliminabile);
        Assert.Equal(2, p.Blocca.Count);
        Assert.Contains(p.Blocca, b => b.Testo.Contains("12 settori"));
        Assert.Contains(p.Blocca, b => b.Testo.Contains("4 aeroporti"));
    }

    [Fact]
    public void Una_acc_vuota_e_dimenticata_dalla_sorgente_si_elimina()
    {
        var p = DeletionRules.PerAcc(new AccFacts("LIRR", "Roma", Vecchio, 0, 0), Penultimo);

        Assert.True(p.Eliminabile);
        Assert.Equal("LIRR", p.Azioni.AccDaEliminare);
    }

    // ── Il documento ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Il_documento_dice_cosa_si_perde_e_non_lo_blocca_nessuno()
    {
        var p = DeletionRules.PerDocumento(new DocumentFacts(
            7, "vIPI Roma", DocumentType.Vipi, Pubblicato: true, Release: 3,
            new[] { "LIRR_CTR" }, "LIRF"));

        Assert.True(p.Eliminabile);
        Assert.Equal(7, p.Azioni.DocumentoDaEliminare);
        Assert.Contains(p.Muore, m => m.Contains("3 pubblicazioni"));
        Assert.Contains(p.Muore, m => m.Contains("LIRR_CTR"));
        Assert.Contains(p.Muore, m => m.Contains("LIRF"));
    }
}
