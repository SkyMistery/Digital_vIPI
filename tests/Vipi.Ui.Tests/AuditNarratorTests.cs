using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il formattatore che trasforma una riga di audit in una frase, condiviso fra <c>/vsop/admin/audit</c> e il
/// pannello «storia» di <c>/vsop/versioni</c>.
///
/// <para>Presidia due cose che una regressione renderebbe mute: che il registro sappia ancora leggere il
/// <b>vocabolario vecchio</b> (<c>Archive</c> per la revoca di un permesso, la chiave <c>acc</c> minuscola),
/// e che una riga con dettagli assenti o illeggibili non faccia saltare la pagina. Un registro che non si
/// apre è peggio di un registro brutto.</para>
/// </summary>
public class AuditNarratorTests
{
    /// <summary>Localizer che rende la chiave e ci appende gli argomenti: le asserzioni parlano di chiavi e
    /// di valori, non di traduzioni — che cambiano.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + "(" + string.Join("|", arguments) + ")", resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static readonly KeyLocalizer L = new();

    private static AuditEntry Riga(AuditAction azione, string tipo, string id, string? dettagli) =>
        new(1, 704798, azione, tipo, id, new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc), dettagli);

    [Theory]
    [InlineData(AuditAction.Publish, "DocumentVersion", AuditNarrator.Categoria.Pubblicazione)]
    [InlineData(AuditAction.Discard, "DocumentVersion", AuditNarrator.Categoria.Bozza)]
    [InlineData(AuditAction.Delete, "Document", AuditNarrator.Categoria.Documento)]
    [InlineData(AuditAction.Update, "Document", AuditNarrator.Categoria.Documento)]
    [InlineData(AuditAction.Create, "EditGrant", AuditNarrator.Categoria.Permesso)]
    [InlineData(AuditAction.ForceUnlock, "EditResourceLock", AuditNarrator.Categoria.Lock)]
    [InlineData(AuditAction.HierarchyChange, "AirportPosition", AuditNarrator.Categoria.Gerarchia)]
    public void Ogni_evento_cade_nella_sua_famiglia(AuditAction azione, string tipo, AuditNarrator.Categoria attesa) =>
        Assert.Equal(attesa, AuditNarrator.CategoriaDi(Riga(azione, tipo, "1", null)));

    /// <summary>
    /// ⚠️ La revoca di un permesso è stata <c>Archive</c> fino al 22 agosto 2026 e <c>Delete</c> dopo, e la
    /// chiave dell'ACC nei dettagli è passata da <c>acc</c> minuscola a <c>Acc</c>. Le righe vecchie non si
    /// riscrivono: devono leggersi, e dire la <b>stessa</b> frase delle nuove.
    /// </summary>
    [Fact]
    public void Revoca_vecchia_e_nuova_dicono_la_stessa_frase()
    {
        var vecchia = Riga(AuditAction.Archive, "EditGrant", "3", "{\"UserId\":555003,\"acc\":\"LIRR\"}");
        var nuova = Riga(AuditAction.Delete, "EditGrant", "3", "{\"UserId\":555003,\"Acc\":\"LIRR\"}");

        Assert.Equal(AuditNarrator.Frase(vecchia, L), AuditNarrator.Frase(nuova, L));
        Assert.Contains("Audit_Fr_GrantRevoke", AuditNarrator.Frase(nuova, L));
        Assert.Contains("LIRR", AuditNarrator.Frase(vecchia, L));
        Assert.Equal("LIRR", AuditNarrator.Acc(vecchia));   // la chiave minuscola si legge lo stesso
    }

    /// <summary>Concedere e revocare non sono lo stesso atto e non dicono la stessa frase.</summary>
    [Fact]
    public void Concessione_e_revoca_non_si_confondono()
    {
        var add = Riga(AuditAction.Create, "EditGrant", "3", "{\"UserId\":555003,\"Acc\":\"LIRR\"}");
        Assert.Contains("Audit_Fr_GrantAdd", AuditNarrator.Frase(add, L));
    }

    /// <summary>Il bersaglio è il nome quando la riga ce l'ha, l'Id interno solo quando non c'è altro.</summary>
    [Fact]
    public void Il_bersaglio_preferisce_il_nome_allId()
    {
        var conNome = Riga(AuditAction.Delete, "Document", "7", "{\"Title\":\"vIPI — Roma ACC\",\"Acc\":\"LIRR\"}");
        Assert.Equal("vIPI — Roma ACC", AuditNarrator.Bersaglio(conNome, L));

        var vecchia = Riga(AuditAction.Publish, "DocumentVersion", "32", "{\"Id\":10,\"VersionNumber\":2}");
        Assert.Contains("Audit_DocN", AuditNarrator.Bersaglio(vecchia, L));
    }

    /// <summary>
    /// Le righe scritte prima del 22 agosto 2026 portano solo l'Id del documento, e su una riga di
    /// pubblicazione l'<c>EntityId</c> è la <b>versione</b>: l'Id del documento va pescato dai dettagli,
    /// altrimenti la mappa dei titoli cercherebbe il documento sbagliato.
    /// </summary>
    [Theory]
    [InlineData("DocumentVersion", "32", "{\"Id\":10,\"VersionNumber\":2}", 10)]           // publish: doc in «Id»
    [InlineData("DocumentVersion", "44", "{\"DocumentId\":7,\"VersionNumber\":3}", 7)]     // discard: doc in «DocumentId»
    [InlineData("Document", "5", "{\"Hidden\":true}", 5)]                                  // qui l'EntityId È il documento
    [InlineData("EditGrant", "3", "{\"UserId\":555003,\"Acc\":\"LIRR\"}", null)]           // non parla di documenti
    public void Sa_di_quale_documento_parla_la_riga(string tipo, string id, string dettagli, int? atteso) =>
        Assert.Equal(atteso, AuditNarrator.DocumentoDi(Riga(AuditAction.Publish, tipo, id, dettagli)));

    /// <summary>
    /// Con la mappa dei titoli una riga vecchia smette di dire «documento #10» e dice il nome. ⚠️ Ma il titolo
    /// <b>scritto nella riga</b> vince sulla mappa: è quello che il documento aveva al momento dell'atto, e se
    /// nel frattempo è stato rinominato il registro deve raccontare il passato, non il presente.
    /// </summary>
    [Fact]
    public void La_mappa_dei_titoli_riempie_le_righe_vecchie_ma_non_riscrive_la_storia()
    {
        var titoli = new Dictionary<int, string> { [10] = "vIPI — Roma ACC" };

        var senzaTitolo = Riga(AuditAction.Publish, "DocumentVersion", "32", "{\"Id\":10,\"VersionNumber\":2}");
        Assert.Equal("vIPI — Roma ACC", AuditNarrator.Bersaglio(senzaTitolo, L, titoli));

        var rinominato = Riga(AuditAction.Publish, "DocumentVersion", "33", "{\"Id\":10,\"Title\":\"vIPI Roma (vecchio nome)\"}");
        Assert.Equal("vIPI Roma (vecchio nome)", AuditNarrator.Bersaglio(rinominato, L, titoli));
    }

    /// <summary>Un documento eliminato non sta più nella mappa: lì il nome ce l'ha solo la riga di audit.</summary>
    [Fact]
    public void Documento_eliminato_resta_leggibile_dalla_sua_riga()
    {
        var vuota = new Dictionary<int, string>();
        var eliminazione = Riga(AuditAction.Delete, "Document", "10", "{\"Title\":\"vIPI — Roma ACC\",\"Releases\":3}");
        Assert.Equal("vIPI — Roma ACC", AuditNarrator.Bersaglio(eliminazione, L, vuota));

        // Una riga troppo vecchia per aver registrato il nome, su un documento che non c'è più: resta l'Id.
        var pubblicazioneOrfana = Riga(AuditAction.Publish, "DocumentVersion", "9", "{\"Id\":10,\"VersionNumber\":1}");
        Assert.Contains("Audit_DocN", AuditNarrator.Bersaglio(pubblicazioneOrfana, L, vuota));
    }

    /// <summary>Nascondere e rimettere a vista sono due frasi diverse: il verso dell'atto è il fatto.</summary>
    [Fact]
    public void Nascondi_e_mostra_hanno_frasi_opposte()
    {
        var nascondi = Riga(AuditAction.Update, "Document", "7", "{\"Title\":\"X\",\"Hidden\":true}");
        var mostra = Riga(AuditAction.Update, "Document", "7", "{\"Title\":\"X\",\"Hidden\":false}");
        Assert.Contains("Audit_Fr_DocHide", AuditNarrator.Frase(nascondi, L));
        Assert.Contains("Audit_Fr_DocShow", AuditNarrator.Frase(mostra, L));
    }

    /// <summary>Il cambio di gerarchia dice da dove a dove, e «staccato» è un valore, non un buco.</summary>
    [Fact]
    public void La_gerarchia_dice_da_dove_a_dove()
    {
        var r = Riga(AuditAction.HierarchyChange, "AirportPosition", "12",
            "{\"Nodo\":\"LIRP_GND\",\"Acc\":\"LIRR\",\"Da\":\"LIRP_TWR\",\"A\":null}");
        var frase = AuditNarrator.Frase(r, L);
        Assert.Contains("LIRP_TWR", frase);
        Assert.Contains("Audit_NoParent", frase);
        Assert.Equal("LIRP_GND", AuditNarrator.Bersaglio(r, L));
    }

    /// <summary>Il lock forzato serve a dire A CHI è stato tolto: senza quel nome la riga non vale niente.</summary>
    [Fact]
    public void Il_lock_forzato_nomina_chi_lo_teneva()
    {
        var r = Riga(AuditAction.ForceUnlock, "Document", "7",
            "{\"Title\":\"X\",\"HeldByUserId\":555001,\"HeldByName\":\"Giulia Bianchi\"}");
        Assert.Contains("Giulia Bianchi", AuditNarrator.Frase(r, L));
    }

    /// <summary>
    /// ⚠️ Dettagli assenti, vuoti o illeggibili non sono un motivo per rompere la pagina: il JSON del registro
    /// è stato scritto in momenti diversi da versioni diverse dell'app.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non è json")]
    [InlineData("[1,2,3]")]
    public void Dettagli_illeggibili_non_fanno_saltare_niente(string? dettagli)
    {
        var r = Riga(AuditAction.Publish, "DocumentVersion", "32", dettagli);
        Assert.False(string.IsNullOrWhiteSpace(AuditNarrator.Frase(r, L)));
        Assert.False(string.IsNullOrWhiteSpace(AuditNarrator.Bersaglio(r, L)));
        Assert.Null(AuditNarrator.Acc(r));
    }
}
