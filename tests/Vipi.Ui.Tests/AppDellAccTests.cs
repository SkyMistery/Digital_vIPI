using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le vIPI APP sotto la pagina di una ACC (revisione degli enti ATC, S52). La chiave pubblicata è il CODICE
/// dell'ente: a Pratica resta <c>LIRE_APP</c> anche quando quel nominativo sparisce da IVAO, e la pagina dell'ACC
/// cercava un settore con quel nome — la vIPI pubblicata spariva, mentre <c>/apps</c> la mostrava sotto la torre.
/// </summary>
public class AppDellAccTests
{
    private static SectorRow Settore(int id, string cs, SectorType tipo, int? padre = null) =>
        new(id, cs, tipo, SectorKind.Airport, cs + " name", null, 0, tipo == SectorType.App ? ApproachKind.Standalone : null,
            padre, null, null, IsActive: true, DocumentId: null, IsPrimary: false);

    private static ManagedDoc Pubblicata(string codice) =>
        new(ReleaseTargetType.App, "t", codice, "LIRR", IsPublished: true, HasDraft: false, IsHidden: false,
            ReleaseTargetType.App, codice, DocumentId: 1, EffectiveCycle: "2610");

    [Fact]
    public void Una_vipi_app_il_cui_codice_non_e_piu_un_settore_resta_sotto_la_sua_posizione()
    {
        var pratica = new AtcUnitRow(1, "LIRE_APP", "Pratica Tower", "LIRR", AtcUnitMode.OwnDocument, 9, new[] { "LIRE_TWR" });

        var elenco = AppDellAcc.Elenco(new[] { Settore(5, "LIRE_TWR", SectorType.Twr) },
            new[] { Pubblicata("LIRE_APP") }, new[] { pratica }, "LIRR");

        Assert.Equal("LIRE_TWR", Assert.Single(elenco).Callsign);
    }

    [Fact]
    public void Senza_ente_vale_il_nominativo_e_un_app_sotto_un_altro_mostrato_non_si_ripete()
    {
        var elenco = AppDellAcc.Elenco(
            new[] { Settore(1, "LICC_APP", SectorType.App), Settore(2, "LICC_E_APP", SectorType.App, padre: 1) },
            new[] { Pubblicata("LICC_APP"), Pubblicata("LICC_E_APP") }, Array.Empty<AtcUnitRow>(), "LIRR");

        Assert.Equal("LICC_APP", Assert.Single(elenco).Callsign);
    }
}
