using Vipi.Application;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I messaggi che l'applicazione mostra a chi modifica hanno due lingue
/// (<c>docs/design/regole-lingua.md</c> R6-R7).
///
/// <para>
/// ⚠️ Non passano dalle risorse, e non è una scorciatoia: i <c>.resx</c> vivono in <c>Vipi.Ui</c>, e
/// l'applicazione non può dipendere dalla UI. Quindi il messaggio si porta dietro entrambe le lingue e
/// <see cref="Messaggio.Lingua"/> sceglie sulla cultura di chi legge.
/// </para>
/// </summary>
public class MessaggiBilingueTests
{
    [Fact]
    public void Chi_legge_in_inglese_riceve_l_inglese()
    {
        using var _ = CulturaDiProva.Inglese();
        Assert.Equal("English", Messaggio.Lingua("Italiano", "English"));
    }

    [Fact]
    public void Chi_legge_in_italiano_riceve_l_italiano()
    {
        using var _ = CulturaDiProva.Italiana();
        Assert.Equal("Italiano", Messaggio.Lingua("Italiano", "English"));
    }

    [Fact]
    public void Una_lingua_che_non_serviamo_ricade_sull_italiano()
    {
        // L'italiano è la lingua predefinita del sito: un tedesco vede quella, non una stringa vuota.
        using var _ = CulturaDiProva.Tedesca();
        Assert.Equal("Italiano", Messaggio.Lingua("Italiano", "English"));
    }

    [Fact]
    public void Un_errore_di_validazione_esce_nella_lingua_di_chi_legge()
    {
        // La prova sul giro vero: la stessa regola, letta da due persone diverse.
        var fatti = new SectorFacts(
            SectorId: 1, Callsign: "LIRF_TWR", Name: "Fiumicino Tower", AccCode: "LIRR",
            Type: SectorType.Twr, Kind: SectorKind.Airport,
            AirportId: 7, AirportIcao: "LIRF", ParentSectorId: null, ParentCallsign: null,
            IsProjected: true, CatalogoManuale: false,
            ImportedAtUtc: new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc),
            Figli: Array.Empty<ChildFacts>(),
            FigliDiCatalogo: Array.Empty<CatalogChildFacts>(),
            Documenti: Array.Empty<DocRefFacts>(),
            Accordi: Array.Empty<AgreementFacts>());

        string Motivo(bool inglese)
        {
            using var _ = inglese ? CulturaDiProva.Inglese() : CulturaDiProva.Italiana();
            return DeletionRules.PerSettore(fatti, penultimoGiro: null, dentroLoScalo: false)
                .Blocca[0].Testo;
        }

        Assert.Contains("una torre si elimina solo insieme", Motivo(inglese: false));
        Assert.Contains("a tower can only be deleted together", Motivo(inglese: true));
    }

    /// <summary>
    /// ⚠️ Non solo i <b>blocchi</b>: anche l'elenco di cosa muore e di cosa si sposta. Fino al
    /// 1 settembre 2026 quelle righe erano italiane per tutti, e la finestra di eliminazione della pagina
    /// inglese era mezza tradotta — titolo e tasti in inglese, il piano in italiano.
    /// </summary>
    [Fact]
    public void Anche_il_piano_esce_nella_lingua_di_chi_legge()
    {
        var fatti = new SectorFacts(
            SectorId: 2, Callsign: "LIMM_W_CTR", Name: "Milano West", AccCode: "LIMM",
            Type: SectorType.Ctr, Kind: SectorKind.Acc,
            AirportId: null, AirportIcao: null, ParentSectorId: 9, ParentCallsign: "LIMM_CTR",
            IsProjected: false, CatalogoManuale: true, ImportedAtUtc: null,
            Figli: new[] { new ChildFacts(3, "LIMM_W1_CTR") },
            FigliDiCatalogo: Array.Empty<CatalogChildFacts>(),
            Documenti: Array.Empty<DocRefFacts>(),
            Accordi: Array.Empty<AgreementFacts>());

        DeletionPlan Piano(bool inglese)
        {
            using var _ = inglese ? CulturaDiProva.Inglese() : CulturaDiProva.Italiana();
            return DeletionRules.PerSettore(fatti, penultimoGiro: null);
        }

        Assert.Contains("il settore LIMM_W_CTR", Piano(inglese: false).Muore[0]);
        Assert.Contains("the sector LIMM_W_CTR", Piano(inglese: true).Muore[0]);

        Assert.Equal("LIMM_W1_CTR passa sotto LIMM_CTR", Piano(inglese: false).SiSposta[0]);
        Assert.Equal("LIMM_W1_CTR moves under LIMM_CTR", Piano(inglese: true).SiSposta[0]);
    }
}
