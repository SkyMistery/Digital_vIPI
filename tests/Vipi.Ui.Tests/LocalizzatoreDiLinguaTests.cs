using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Application.Tests;
using Vipi.Ui;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le etichette dell'interfaccia dentro un documento a <b>lingua bloccata</b> (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §4, strato 3).
///
/// <para>
/// ⚠️ <b>Perché è un oggetto avvolto e non un parametro.</b> Le etichette che stanno DENTRO un documento —
/// le intestazioni delle tabelle derivate, le chip, i cartellini — sono <c>L["…"]</c> in <b>126 file
/// razor</b>. Passarle una lingua a mano vorrebbe dire toccarli tutti e sperare che la prossima pagina
/// scritta se ne ricordi; e chi se ne dimenticasse non romperebbe niente, lascerebbe solo una tabella con
/// l'intestazione nella lingua sbagliata in mezzo a un documento nell'altra.
/// </para>
/// </summary>
public class LocalizzatoreDiLinguaTests
{
    /// <summary>Il localizzatore «di sempre», che risponde in modo riconoscibile: se la sua risposta esce,
    /// vuol dire che il wrapper ha delegato invece di scavalcarlo.</summary>
    private sealed class Standard : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, "DALL-INTERNO");
        public LocalizedString this[string name, params object[] arguments] => new(name, "DALL-INTERNO");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }

    private static (LocalizzatoreDiLingua L, ReadingLanguageContext Ctx) Monta()
    {
        var ctx = new ReadingLanguageContext();
        return (new LocalizzatoreDiLingua(new Standard(), ctx), ctx);
    }

    [Fact]
    public void Senza_lingua_imposta_delega_al_localizzatore_di_sempre()
    {
        // ⚠️ È la metà che conta di più: una funzione spenta deve somigliare a una funzione spenta. Fuori
        // da un documento bloccato questo oggetto non deve nemmeno passare dal ResourceManager.
        using var _ = CulturaDiProva.Italiana();
        var (l, _ctx) = Monta();

        Assert.Equal("DALL-INTERNO", l["Common_Print"].Value);
    }

    [Fact]
    public void Con_la_lingua_del_documento_risponde_in_QUELLA_lingua()
    {
        // Il caso vero: sito italiano, documento bloccato in inglese. L'etichetta che finisce nella testata
        // di una tabella del documento deve uscire in inglese.
        using var _ = CulturaDiProva.Italiana();
        var (l, ctx) = Monta();
        ctx.Fissa("en");

        var atteso = RisorseCondivise.Testo("Common_Print", System.Globalization.CultureInfo.GetCultureInfo("en"));
        Assert.Equal(atteso, l["Common_Print"].Value);
        Assert.NotEqual("DALL-INTERNO", l["Common_Print"].Value);
    }

    [Fact]
    public void Imporre_la_lingua_che_gia_si_legge_non_scavalca_niente()
    {
        // Documento italiano bloccato, letto da chi guarda il sito in italiano: non c'è niente da imporre,
        // e il wrapper deve togliersi di mezzo invece di rifare lo stesso lavoro per un'altra strada.
        using var _ = CulturaDiProva.Italiana();
        var (l, ctx) = Monta();
        ctx.Fissa("it");

        Assert.Equal("DALL-INTERNO", l["Common_Print"].Value);
    }

    [Fact]
    public void Una_chiave_che_non_esiste_torna_la_chiave_e_lo_dichiara()
    {
        // Come fa il localizzatore standard: a schermo si vede un nome tecnico invece del vuoto, e chi lo
        // vede capisce subito che manca una riga nel resx.
        using var _ = CulturaDiProva.Italiana();
        var (l, ctx) = Monta();
        ctx.Fissa("en");

        var s = l["Chiave_Che_Non_Esiste_Davvero"];
        Assert.Equal("Chiave_Che_Non_Esiste_Davvero", s.Value);
        Assert.True(s.ResourceNotFound);
    }

    [Fact]
    public void Le_stringhe_del_SITO_restano_nella_lingua_di_chi_guarda()
    {
        // ⚠️ Il contrappeso, ed è la ragione per cui StringheDelSito esiste: l'avviso «questo documento è
        // pubblicato solo in inglese» lo legge chi sta guardando il sito in italiano. Detto in inglese non
        // servirebbe proprio a lui.
        using var _ = CulturaDiProva.Italiana();
        var (_l, ctx) = Monta();
        ctx.Fissa("en");

        var sito = new StringheDelSito();
        var italiano = RisorseCondivise.Testo("Lang_LockedTitle", System.Globalization.CultureInfo.GetCultureInfo("it"));
        Assert.Equal(italiano, sito["Lang_LockedTitle"]);
    }
}
