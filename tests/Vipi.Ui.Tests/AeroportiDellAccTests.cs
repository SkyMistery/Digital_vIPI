using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Quali scali compaiono sotto una ACC e con quali documenti (11 settembre 2026): il vSOP militare sta
/// nell'elenco degli aeroporti accanto alla vIPI, e un campo solo militare è un aeroporto come gli altri.
/// </summary>
public class AeroportiDellAccTests
{
    private static AirportRow Scalo(string icao, int settori = 1, bool nascosto = false,
        AirportCategory categoria = AirportCategory.Civil) =>
        new(Id: icao.GetHashCode(), Icao: icao, Name: icao + " name", Sectors: settori, IsHidden: nascosto,
            HasMilitaryPresence: categoria != AirportCategory.Civil, Category: categoria);

    private static ManagedDoc Doc(ReleaseTargetType tipo, string icao, string acc = "LIMM",
        bool inVigore = true, bool nascosto = false) =>
        new(tipo, "t", icao, acc, IsPublished: true, HasDraft: false, IsHidden: nascosto, tipo, icao, DocumentId: 1,
            EffectiveCycle: inVigore ? "2609" : null);

    [Fact]
    public void Un_campo_solo_militare_col_suo_vSOP_compare_e_porta_al_vSOP()
    {
        var elenco = AeroportiDellAcc.Elenco(
            new[] { Scalo("LIPL", categoria: AirportCategory.MilitaryOnly) },
            new[] { Doc(ReleaseTargetType.AirportMil, "LIPL") }, "LIMM");

        var voce = Assert.Single(elenco);
        Assert.False(voce.HaVipi);
        Assert.True(voce.HaVsop);
        Assert.Equal("/services/vsop/limm/mil?icao=LIPL&vista=atc", voce.Href("LIMM"));
    }

    [Fact]
    public void Un_campo_con_tutti_e_due_i_documenti_e_UNA_voce_con_due_collegamenti()
    {
        var elenco = AeroportiDellAcc.Elenco(
            new[] { Scalo("LIBV", categoria: AirportCategory.MilitaryWithCivilPresence) },
            new[] { Doc(ReleaseTargetType.Airport, "LIBV"), Doc(ReleaseTargetType.AirportMil, "LIBV") }, "LIMM");

        var voce = Assert.Single(elenco);
        Assert.True(voce.HaVipi && voce.HaVsop);
        Assert.Equal("/services/vsop/limm/airports?icao=LIBV", voce.VipiHref("LIMM"));
        // Il vSOP si apre in vista ATC: dall'ACC arriva un controllore.
        Assert.Equal("/services/vsop/limm/mil?icao=LIBV&vista=atc", voce.VsopHref("LIMM"));
        // Dove c'è posto per un collegamento solo, vince la vIPI.
        Assert.Equal(voce.VipiHref("LIMM"), voce.Href("LIMM"));
    }

    /// <summary>🔴 Il cancello di ogni elenco pubblico: release effettiva e non nascosto, per documento.</summary>
    [Fact]
    public void Senza_un_documento_pubblico_lo_scalo_non_compare()
    {
        var elenco = AeroportiDellAcc.Elenco(
            new[] { Scalo("LIML"), Scalo("LIMC"), Scalo("LIME"), Scalo("LIMF") },
            new[]
            {
                Doc(ReleaseTargetType.Airport, "LIML", inVigore: false),        // solo bozza o release futura
                Doc(ReleaseTargetType.Airport, "LIMC", nascosto: true),         // nascosto
                Doc(ReleaseTargetType.Airport, "LIME", acc: "LIRR"),            // di un'altra ACC
                // LIMF: nessun documento
            }, "LIMM");

        Assert.Empty(elenco);
    }

    /// <summary>La vIPI pretende almeno un settore, il vSOP no: un campo solo militare può non averne, ed è la
    /// regola dell'elenco nazionale.</summary>
    [Fact]
    public void Lo_scalo_senza_settori_perde_la_vIPI_ma_non_il_vSOP()
    {
        var elenco = AeroportiDellAcc.Elenco(
            new[] { Scalo("LIPL", settori: 0, categoria: AirportCategory.MilitaryWithCivilPresence) },
            new[] { Doc(ReleaseTargetType.Airport, "LIPL"), Doc(ReleaseTargetType.AirportMil, "LIPL") }, "LIMM");

        var voce = Assert.Single(elenco);
        Assert.False(voce.HaVipi);
        Assert.True(voce.HaVsop);
    }

    [Fact]
    public void Lo_scalo_nascosto_non_compare_neanche_col_vSOP()
    {
        var elenco = AeroportiDellAcc.Elenco(
            new[] { Scalo("LIPL", nascosto: true, categoria: AirportCategory.MilitaryOnly) },
            new[] { Doc(ReleaseTargetType.AirportMil, "LIPL") }, "LIMM");

        Assert.Empty(elenco);
    }

    /// <summary>⚠️ La categoria non filtra i documenti: un documento pubblicato fuori categoria resta
    /// raggiungibile finché qualcuno non lo nasconde (lo segnala la Diagnostica).</summary>
    [Fact]
    public void La_categoria_non_nasconde_un_documento_pubblicato()
    {
        var elenco = AeroportiDellAcc.Elenco(
            new[] { Scalo("LIPL", categoria: AirportCategory.MilitaryOnly) },
            new[] { Doc(ReleaseTargetType.Airport, "LIPL") }, "LIMM");

        Assert.True(Assert.Single(elenco).HaVipi);
    }

    [Fact]
    public void I_filtri_hanno_le_quattro_categorie_dal_piu_civile_al_piu_militare()
    {
        Assert.Equal(Enum.GetValues<AirportCategory>().Length, AeroportiDellAcc.OrdineDelleCategorie.Count);
        Assert.Equal(AirportCategory.Civil, AeroportiDellAcc.OrdineDelleCategorie[0]);
        Assert.Equal(AirportCategory.MilitaryOnly, AeroportiDellAcc.OrdineDelleCategorie[^1]);
    }
}
