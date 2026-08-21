using System.Text.Json;
using Vipi.Host.Auth;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il nome che il sito mostra dello staffista, composto dalla userinfo IVAO (<c>/v2/users/me</c>).
///
/// <para>Per mesi l'elenco permessi ha detto «UserId 704798»: non era un limite di IVAO — come diceva la
/// guida — ma lo scope <c>profile</c> che non veniva chiesto. Con quello, <c>firstName</c> e
/// <c>lastName</c> arrivano. I payload di questi test sono ritagliati da una misura reale del 22-ago-2026.</para>
///
/// <para>Il punto delicato è che qui non lancia niente: un campo sbagliato non rompe il login, toglie il
/// nome in silenzio. Da cui i casi a metà dato e il placeholder.</para>
/// </summary>
public sealed class DisplayNameFromIvaoTests
{
    private static string? Compose(string json, string? vid = "704798") =>
        VipiStandaloneAuthExtensions.ComposeDisplayName(JsonDocument.Parse(json).RootElement, vid);

    [Fact]
    public void Nome_e_cognome_veri_vincono_su_tutto()
    {
        // Payload reale (ridotto): col nickname pubblico presente, che NON deve prevalere.
        const string me = """
        {"id":704798,"firstName":"Carmine","lastName":"Granato",
         "publicNickname":"Carmine (704798)","nickname":"Carmine","centerId":"LIRR"}
        """;
        Assert.Equal("Carmine Granato", Compose(me));
    }

    [Fact]
    public void I_nomi_OIDC_standard_valgono_come_i_campi_IVAO()
    {
        // IVAO manda entrambe le coppie; se un domani restassero solo le standard, il nome regge.
        Assert.Equal("Carmine Granato",
            Compose("""{"given_name":"Carmine","family_name":"Granato"}"""));
    }

    [Theory]
    // Mezzo dato è comunque meglio del VID.
    [InlineData("""{"firstName":"Carmine"}""", "Carmine")]
    [InlineData("""{"lastName":"Granato"}""", "Granato")]
    // Spazi attorno: si potano, non si concatenano doppi.
    [InlineData("""{"firstName":"  Carmine  ","lastName":" Granato "}""", "Carmine Granato")]
    // Campi presenti ma vuoti: come se non ci fossero → si scende al nickname.
    [InlineData("""{"firstName":"","lastName":"   ","publicNickname":"Ripiego"}""", "Ripiego")]
    // Tipo inatteso (null o numero): non deve lanciare, deve solo scendere di livello.
    [InlineData("""{"firstName":null,"lastName":123,"nickname":"Ripiego"}""", "Ripiego")]
    public void Casi_a_meta_dato(string json, string atteso) => Assert.Equal(atteso, Compose(json));

    [Fact]
    public void Il_placeholder_di_IVAO_non_e_un_nome()
    {
        // "User {vid}" è ciò che IVAO mette a chi non ha scelto un nickname: null ⇒ il chiamante
        // ripiega su "UserId {vid}", che almeno non finge di essere un nome.
        Assert.Null(Compose("""{"publicNickname":"User 704798"}"""));
        Assert.Null(Compose("""{"publicNickname":"user 704798"}"""));   // e non conta il maiuscolo
    }

    [Fact]
    public void Il_nickname_col_VID_tra_parentesi_invece_e_un_nome()
    {
        // "Carmine (704798)" è la forma pubblica normale di IVAO, non un segnaposto: senza scope
        // `profile` è tutto ciò che si ha, ed è comunque più utile del VID nudo.
        Assert.Equal("Carmine (704798)", Compose("""{"publicNickname":"Carmine (704798)"}"""));
    }

    [Fact]
    public void Senza_niente_di_utile_nessun_nome()
    {
        Assert.Null(Compose("""{"id":704798,"centerId":"LIRR"}"""));
        Assert.Null(Compose("{}", vid: null));
    }

    /// <summary>
    /// Le posizioni staff decidono chi può editare: se questo claim non si forma, lo staffista entra come
    /// semplice lettore — senza un errore da nessuna parte. È il rischio della mappatura esplicita che ha
    /// sostituito <c>MapAll()</c>, quindi la forma reale del payload è fissata qui.
    /// </summary>
    [Fact]
    public void I_codici_posizione_si_estraggono_dagli_oggetti_IVAO()
    {
        const string me = """
        {"userStaffPositions":[
          {"id":"IT-AOA1","staffPositionId":"-AOA1","connectAs":"IT-AOA1","divisionId":"IT",
           "staffPosition":{"id":"-AOA1","name":"ATC Operations Advisor 1"}},
          {"id":"IT-T03","staffPositionId":"-T03","connectAs":"IT-T03","divisionId":"IT",
           "staffPosition":{"id":"-T03","name":"Division Trainer 3"}}]}
        """;
        Assert.Equal("""["IT-AOA1","IT-T03"]""", Codes(me));
    }

    [Theory]
    // Array di stringhe: forma già gestita a valle, accettata anche qui.
    [InlineData("""{"userStaffPositions":["IT-DIR"]}""", """["IT-DIR"]""")]
    // `connectAs` come riserva se un giorno mancasse `id`.
    [InlineData("""{"userStaffPositions":[{"connectAs":"IT-WM"}]}""", """["IT-WM"]""")]
    // Nessuna posizione, campo assente, o forma inattesa ⇒ null: il claim non viene proprio emesso.
    [InlineData("""{"userStaffPositions":[]}""", null)]
    [InlineData("""{"id":704798}""", null)]
    [InlineData("""{"userStaffPositions":"IT-DIR"}""", null)]
    [InlineData("""{"userStaffPositions":[{"description":null}]}""", null)]
    public void Forme_limite_delle_posizioni(string json, string? atteso) => Assert.Equal(atteso, Codes(json));

    private static string? Codes(string json) =>
        VipiStandaloneAuthExtensions.StaffPositionCodesJson(JsonDocument.Parse(json).RootElement);
}
