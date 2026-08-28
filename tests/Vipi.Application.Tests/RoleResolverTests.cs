using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// La tabella di verità del <see cref="RoleResolver"/>: da posizione staff IVAO a
/// <see cref="VipiRole"/>. Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §4,
/// lavori aperti §U.
///
/// <para>⚠️ <b>Questa è la prova che deve esistere prima del cablaggio.</b> Il permesso più alto del
/// prodotto ha due modi di sbagliarsi, entrambi silenziosi: <i>nessuno è admin</i> (in produzione nessuno
/// può più editare, e non si rimedia da dentro perché assegnare permessi richiede di essere admin) e
/// <i>troppi admin</i> (controllo editoriale a chi non doveva averlo). Nessuno dei due si vede guardando
/// una schermata: si vedono solo qui.</para>
///
/// <para>I codici usati sono <b>quelli veri</b>, osservati ai login del 9 agosto 2026 — <c>IT-AOC</c>,
/// <c>IT-SOC</c>, <c>IT-T01</c>, <c>IT-FOC</c>, <c>IT-ADIR</c>, <c>IT-FOAC</c>, <c>IT-AOA1</c>,
/// <c>IT-T03</c> — e non esempi inventati: metà di questi test riguarda gente che esiste.</para>
/// </summary>
public class RoleResolverTests
{
    private const int VidFondatore = 704798;
    private const int VidQualunque = 123456;

    private static RoleResolver Resolver(AuthOptions? auth = null, DivisionOptions? division = null) =>
        new(auth ?? new AuthOptions(), division ?? new DivisionOptions());

    private static VipiRole Livello(params string[] posizioni) =>
        Resolver().Resolve(VidQualunque, posizioni);

    // ------------------------------------------------------------------ i cinque livelli, uno per volta

    [Theory]
    [InlineData("IT-DIR")]
    [InlineData("IT-ADIR")]
    [InlineData("IT-WM")]
    [InlineData("IT-AWM")]
    [InlineData("IT-AOC")]
    [InlineData("IT-AOAC")]
    [InlineData("IT-SOC")]
    [InlineData("IT-SOAC")]
    public void Gli_otto_codici_di_direzione_sono_admin(string codice) =>
        Assert.Equal(VipiRole.Admin, Livello(codice));

    [Theory]
    [InlineData("LIRR-CH")]
    [InlineData("LIMM-CH")]
    [InlineData("LIBB-ACH")]
    public void I_chief_dacc_italiani_sono_editor(string codice) =>
        Assert.Equal(VipiRole.Editor, Livello(codice));

    /// <summary>
    /// I quattro codici che il 22 agosto 2026 avevano fatto scegliere il jolly: allora erano admin,
    /// da oggi sono staff di divisione. È il cambio di sostanza di questa feature, e sta scritto qui.
    /// </summary>
    [Theory]
    [InlineData("IT-T01")]
    [InlineData("IT-T03")]
    [InlineData("IT-FOC")]
    [InlineData("IT-FOAC")]
    [InlineData("IT-AOA1")]
    public void Il_resto_dello_staff_italiano_e_division_staff_non_admin(string codice) =>
        Assert.Equal(VipiRole.DivisionStaff, Livello(codice));

    [Theory]
    [InlineData("DE-DIR")]        // direttore di un'altra divisione: staff IVAO, non nostro
    [InlineData("LFFF-CH")]       // chief di un ACC francese: il prefisso ICAO non è dei nostri
    [InlineData("HQ-WM")]
    public void Lo_staff_di_altre_divisioni_e_solo_ivao_staff(string codice) =>
        Assert.Equal(VipiRole.IvaoStaff, Livello(codice));

    [Fact]
    public void Senza_posizioni_staff_si_e_utenti_qualunque()
    {
        Assert.Equal(VipiRole.User, Livello());
        Assert.Equal(VipiRole.User, Resolver().Resolve(VidQualunque, null));
    }

    [Fact]
    public void Lanonimo_e_utente_qualunque() =>
        Assert.Equal(VipiRole.User, Resolver().Resolve((CurrentUser?)null));

    // ------------------------------------------------------------------ vince la posizione più alta

    /// <summary>
    /// Chi ha più cappelli prende il più alto, in qualunque ordine arrivino i codici. ⚠️ Un <c>IT-DIR</c>
    /// combacia <b>anche</b> col pattern dello staff di divisione: è l'ordine di valutazione, non i
    /// pattern, a decidere che è admin — e un ordine sbagliato lo declasserebbe in silenzio.
    /// </summary>
    [Theory]
    [InlineData(VipiRole.Admin, "IT-T01", "IT-DIR")]
    [InlineData(VipiRole.Admin, "IT-DIR", "IT-T01")]
    [InlineData(VipiRole.Admin, "LIRR-CH", "IT-AOC")]
    [InlineData(VipiRole.Editor, "IT-T01", "LIRR-CH")]
    [InlineData(VipiRole.Editor, "DE-DIR", "LIMM-ACH")]
    [InlineData(VipiRole.DivisionStaff, "DE-DIR", "IT-T03")]
    public void Fra_piu_posizioni_vince_la_piu_alta(VipiRole atteso, params string[] posizioni) =>
        Assert.Equal(atteso, Livello(posizioni));

    // ------------------------------------------------------------------ il fondatore

    [Fact]
    public void Il_fondatore_e_admin_anche_senza_nessuna_posizione_staff()
    {
        var auth = new AuthOptions { FounderVids = { VidFondatore } };
        Assert.Equal(VipiRole.Admin, new RoleResolver(auth, new DivisionOptions()).Resolve(VidFondatore, Array.Empty<string>()));
    }

    /// <summary>Il fondatore non si fa declassare da una posizione staff modesta: è un pavimento, non un valore.</summary>
    [Fact]
    public void Il_fondatore_e_admin_anche_con_una_posizione_bassa()
    {
        var auth = new AuthOptions { FounderVids = { VidFondatore } };
        Assert.Equal(VipiRole.Admin, new RoleResolver(auth, new DivisionOptions()).Resolve(VidFondatore, new[] { "IT-T01" }));
    }

    [Fact]
    public void Chi_non_e_fondatore_non_prende_niente_dal_fondatore()
    {
        var auth = new AuthOptions { FounderVids = { VidFondatore } };
        Assert.Equal(VipiRole.User, new RoleResolver(auth, new DivisionOptions()).Resolve(VidQualunque, Array.Empty<string>()));
    }

    /// <summary>Un VID zero o negativo non è un VID: non deve mai valere come fondatore.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_vid_non_valido_non_e_mai_fondatore(int vid)
    {
        var auth = new AuthOptions { FounderVids = { vid } };
        Assert.Equal(VipiRole.User, new RoleResolver(auth, new DivisionOptions()).Resolve(vid, Array.Empty<string>()));
    }

    // ------------------------------------------------------------------ forma dei codici

    /// <summary>I claim arrivano dal portale IVAO: maiuscole non garantite, spazi possibili.</summary>
    [Theory]
    [InlineData("it-dir")]
    [InlineData("  IT-DIR  ")]
    [InlineData("It-Dir")]
    public void Il_confronto_ignora_maiuscole_e_spazi(string codice) =>
        Assert.Equal(VipiRole.Admin, Livello(codice));

    [Fact]
    public void Le_stringhe_vuote_non_contano_come_posizione_staff() =>
        Assert.Equal(VipiRole.User, Livello("", "   "));

    /// <summary>
    /// I pattern sono ancorati: un codice che <i>contiene</i> un ruolo admin non è un ruolo admin.
    /// Senza <c>^…$</c> un <c>IT-DIRETTIVO</c> inventato diventerebbe direttore della divisione.
    /// </summary>
    [Theory]
    [InlineData("IT-DIRETTIVO")]
    [InlineData("XIT-DIR")]
    [InlineData("IT-DIR-2")]
    public void I_codici_che_somigliano_a_un_admin_non_sono_admin(string codice) =>
        Assert.NotEqual(VipiRole.Admin, Livello(codice));

    [Theory]
    [InlineData("LIRR-CHIEF")]
    [InlineData("LI-CH")]        // manca il corpo dell'ACC fra prefisso e ruolo
    public void I_codici_che_somigliano_a_un_chief_non_sono_editor(string codice) =>
        Assert.NotEqual(VipiRole.Editor, Livello(codice));

    // ------------------------------------------------------------------ configurazione

    /// <summary>
    /// <c>Auth:AdminStaffCodes</c> è l'unica lista che <b>sostituisce</b> invece di sommare: è la via per
    /// restringere l'admin senza ricompilare. Qui restringe a un codice solo, e <c>IT-DIR</c> cade.
    /// </summary>
    [Fact]
    public void I_pattern_espliciti_sostituiscono_quelli_di_default()
    {
        var auth = new AuthOptions { AdminStaffCodes = { "^IT-AOC$" } };
        var r = new RoleResolver(auth, new DivisionOptions());

        Assert.Equal(VipiRole.Admin, r.Resolve(VidQualunque, new[] { "IT-AOC" }));
        Assert.Equal(VipiRole.DivisionStaff, r.Resolve(VidQualunque, new[] { "IT-DIR" }));
    }

    /// <summary>Cambiando divisione cambia tutto senza toccare codice: è il senso di <see cref="DivisionOptions"/>.</summary>
    [Fact]
    public void Su_unaltra_divisione_valgono_i_suoi_prefissi()
    {
        var division = new DivisionOptions { Code = "DE", IcaoPrefixes = { "ED", "ET" } };
        var r = new RoleResolver(new AuthOptions(), division);

        Assert.Equal(VipiRole.Admin, r.Resolve(VidQualunque, new[] { "DE-DIR" }));
        Assert.Equal(VipiRole.Editor, r.Resolve(VidQualunque, new[] { "EDGG-CH" }));
        Assert.Equal(VipiRole.DivisionStaff, r.Resolve(VidQualunque, new[] { "DE-T01" }));
        Assert.Equal(VipiRole.IvaoStaff, r.Resolve(VidQualunque, new[] { "IT-DIR" }));
    }

    /// <summary>
    /// ⚠️ Il binder della configurazione <b>somma</b> ai default: <c>IcaoPrefixes: ["LI"]</c> in appsettings
    /// più <c>["LI"]</c> di default dà «LI» due volte. Innocuo per il confronto, ma i pattern si mostrano a
    /// schermo nella diagnostica e un elenco con doppioni fa dubitare della configurazione.
    /// </summary>
    [Fact]
    public void I_prefissi_ripetuti_non_generano_pattern_doppi()
    {
        var division = new DivisionOptions { IcaoPrefixes = { "LI" } };  // "LI" c'era già di default
        var r = new RoleResolver(new AuthOptions(), division);

        Assert.Equal(r.EditorPatterns.Distinct().Count(), r.EditorPatterns.Count);
        Assert.Equal(VipiRole.Editor, r.Resolve(VidQualunque, new[] { "LIRR-CH" }));
    }

    // ------------------------------------------------------------------ diagnostica

    /// <summary>La diagnostica non deve dire solo «sei admin», ma <b>quale codice</b> te lo dà.</summary>
    [Fact]
    public void Dice_quale_codice_ha_fatto_scattare_il_livello()
    {
        var codici = new[] { "IT-T01", "IT-AOC", "LIRR-CH" };
        var r = Resolver();

        Assert.Equal(new[] { "IT-AOC" }, r.MatchingCodes(codici, VipiRole.Admin));
        Assert.Equal(new[] { "LIRR-CH" }, r.MatchingCodes(codici, VipiRole.Editor));
        Assert.Equal(new[] { "IT-T01", "IT-AOC" }, r.MatchingCodes(codici, VipiRole.DivisionStaff));
    }

    // ------------------------------------------------------------------ l'ordine dell'enum

    /// <summary>
    /// ⚠️ <b>L'ordine è il contratto.</b> Ogni cancello del prodotto si scriverà <c>Role &gt;= X</c>: se
    /// domani qualcuno rinumerasse l'enum, i confronti resterebbero compilabili e cambierebbero significato
    /// senza che niente si accorga. Questo test è lì per accorgersene.
    /// </summary>
    [Fact]
    public void I_livelli_sono_ordinati_e_cumulativi()
    {
        Assert.True(VipiRole.User < VipiRole.IvaoStaff);
        Assert.True(VipiRole.IvaoStaff < VipiRole.DivisionStaff);
        Assert.True(VipiRole.DivisionStaff < VipiRole.Editor);
        Assert.True(VipiRole.Editor < VipiRole.Admin);

        // Un chief è anche membro della divisione: la prerogativa di sotto ce l'ha per costruzione.
        Assert.True(Livello("LIRR-CH") >= VipiRole.DivisionStaff);
        Assert.True(Livello("IT-DIR") >= VipiRole.Editor);
    }

    /// <summary>I valori finiscono in banca dati: cambiarli è una migrazione, non una rifinitura.</summary>
    [Fact]
    public void I_valori_numerici_sono_quelli_scritti_nella_carta()
    {
        Assert.Equal(0, (int)VipiRole.User);
        Assert.Equal(1, (int)VipiRole.IvaoStaff);
        Assert.Equal(2, (int)VipiRole.DivisionStaff);
        Assert.Equal(3, (int)VipiRole.Editor);
        Assert.Equal(4, (int)VipiRole.Admin);
    }
}
