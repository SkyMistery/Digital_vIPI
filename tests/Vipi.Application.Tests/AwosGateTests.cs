using Vipi.Application.Abstractions;
using Vipi.Application.Awos;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il cancello del quadro vAWOS: chi lo può aprire, che cosa elenca, quale ATIS conta.
///
/// <para>⚠️ È la parte con conseguenze, ed era provata SOLO dal vivo (rilievo della revisione del
/// 12 settembre 2026, sera). Una prova dal vivo dice che oggi funziona; non dice che domani qualcuno non
/// toglierà il filtro sui nascosti.</para>
/// </summary>
public class AwosGateTests
{
    private static ManagedDoc Doc(ReleaseTargetType tipo, string icao, bool release = true, bool nascosto = false,
                                  string titolo = "vIPI — LIBD Bari Palese") =>
        // ⚠️ `HasEffectiveRelease` è CALCOLATA (`EffectiveCycle is not null`): la release effettiva si finge
        // dando un ciclo, non alzando un flag — che è poi il modo in cui il dato vero arriva dal repository.
        new ManagedDoc(tipo, titolo, icao, "LIBB", IsPublished: true, HasDraft: false, IsHidden: nascosto,
                       ReleaseTarget: tipo, ReleaseKey: icao, DocumentId: 1,
                       EffectiveCycle: release ? "2608" : null);

    // ─── Il cancello ───────────────────────────────────────────────────────────────

    [Fact]
    public void Una_vIPI_pubblicata_apre_il_quadro()
    {
        var (vipi, vsop) = AwosGate.Pubblicati(new[] { Doc(ReleaseTargetType.Airport, "LIBD") }, "LIBD");
        Assert.True(vipi);
        Assert.False(vsop);
    }

    [Fact] // un campo solo militare si apre col suo vSOP: e' la decisione del committente
    public void Un_vSOP_pubblicato_apre_il_quadro()
    {
        var (vipi, vsop) = AwosGate.Pubblicati(new[] { Doc(ReleaseTargetType.AirportMil, "LIBG") }, "LIBG");
        Assert.False(vipi);
        Assert.True(vsop);
    }

    [Fact] // 🔴 senza release effettiva NON si apre: e' il cancello di ogni elenco pubblico
    public void Senza_release_effettiva_non_si_apre()
    {
        var (vipi, vsop) = AwosGate.Pubblicati(new[] { Doc(ReleaseTargetType.Airport, "LIBD", release: false) }, "LIBD");
        Assert.False(vipi);
        Assert.False(vsop);
    }

    [Fact] // 🔴 e un documento NASCOSTO non conta, anche se pubblicato
    public void Un_documento_nascosto_non_apre()
    {
        var (vipi, _) = AwosGate.Pubblicati(new[] { Doc(ReleaseTargetType.Airport, "LIBD", nascosto: true) }, "LIBD");
        Assert.False(vipi);
    }

    [Fact] // il documento di un ALTRO scalo non apre questo
    public void Il_documento_di_un_altro_scalo_non_apre()
    {
        var (vipi, vsop) = AwosGate.Pubblicati(new[] { Doc(ReleaseTargetType.Airport, "LIBD") }, "LIRF");
        Assert.False(vipi);
        Assert.False(vsop);
    }

    [Fact] // l'ICAO si confronta senza badare alle maiuscole
    public void L_icao_non_bada_alle_maiuscole()
    {
        var (vipi, _) = AwosGate.Pubblicati(new[] { Doc(ReleaseTargetType.Airport, "libd") }, "LIBD");
        Assert.True(vipi);
    }

    // ─── L'elenco ──────────────────────────────────────────────────────────────────

    [Fact] // 🔴 l'elenco e' lo STESSO insieme del cancello: niente scali che poi rifiutano di aprirsi
    public void L_elenco_contiene_solo_cio_che_il_cancello_lascia_passare()
    {
        var elenco = AwosGate.Elenco(new[]
        {
            Doc(ReleaseTargetType.Airport, "LIBD"),
            Doc(ReleaseTargetType.Airport, "LIRF", release: false),
            Doc(ReleaseTargetType.AirportMil, "LIBG", nascosto: true),
            Doc(ReleaseTargetType.AirportMil, "LIPA", titolo: "vSOP — LIPA Aviano"),
        });

        Assert.Equal(new[] { "LIBD", "LIPA" }, elenco.Select(a => a.Icao));
    }

    [Fact] // uno scalo con tutt'e due i documenti compare UNA volta, e le dichiara tutt'e due
    public void Uno_scalo_con_due_documenti_compare_una_volta()
    {
        var elenco = AwosGate.Elenco(new[]
        {
            Doc(ReleaseTargetType.Airport, "LIRP"),
            Doc(ReleaseTargetType.AirportMil, "LIRP"),
        });

        var solo = Assert.Single(elenco);
        Assert.True(solo.HaVipi);
        Assert.True(solo.HaVsop);
    }

    [Fact] // uno Scope che non e' un ICAO (una vLOA, un APP) non entra
    public void Solo_gli_scali_entrano_nell_elenco()
    {
        var elenco = AwosGate.Elenco(new[] { Doc(ReleaseTargetType.App, "LIBD_APP") });
        Assert.Empty(elenco);
    }

    [Fact]
    public void Il_nome_perde_il_prefisso_e_l_icao()
    {
        Assert.Equal("Bari Palese", AwosGate.NomeDalTitolo("vIPI — LIBD Bari Palese", "LIBD"));
        Assert.Equal("Aviano", AwosGate.NomeDalTitolo("vSOP — LIPA Aviano", "LIPA"));
        Assert.Equal("Crotone", AwosGate.NomeDalTitolo("LIBC Crotone", "LIBC"));
        // Un titolo che è solo l'ICAO non deve restare vuoto.
        Assert.Equal("LIBC", AwosGate.NomeDalTitolo("LIBC", "LIBC"));
    }

    // ─── L'ATIS ────────────────────────────────────────────────────────────────────

    private static OnlineAtc Atc(string callsign, string? lettera = "C", string? piste = "26") =>
        new(callsign, 1, callsign, 3, AtisLetter: lettera, AtisTimeRaw: "11:36z",
            AtisArrRunways: piste, AtisDepRunways: piste, AtisText: "…");

    [Fact] // 🔴 la postazione _ATIS batte la torre: dopo un cambio possono dire lettere diverse
    public void L_atis_dedicato_batte_la_torre()
    {
        var scelto = AwosGate.Atis(new[] { Atc("LICC_TWR"), Atc("LICC_ATIS") }, "LICC");
        Assert.Equal("LICC_ATIS", scelto!.Callsign);
    }

    [Fact] // e la torre batte tutto il resto
    public void La_torre_batte_le_altre_postazioni()
    {
        var scelto = AwosGate.Atis(new[] { Atc("LICC_APP"), Atc("LICC_TWR") }, "LICC");
        Assert.Equal("LICC_TWR", scelto!.Callsign);
    }

    [Fact] // 🔴 una postazione di UN ALTRO scalo non parla per questo
    public void L_atis_di_un_altro_scalo_non_conta()
    {
        Assert.Null(AwosGate.Atis(new[] { Atc("LIRF_TWR") }, "LICC"));
        // E nemmeno un callsign che comincia per caso con le stesse lettere senza l'underscore.
        Assert.Null(AwosGate.Atis(new[] { Atc("LICCX_TWR") }, "LICC"));
    }

    [Fact] // chi non trasmette un ATIS leggibile non viene scelto
    public void Chi_non_trasmette_non_viene_scelto()
    {
        Assert.Null(AwosGate.Atis(new[] { Atc("LICC_TWR", lettera: null, piste: null) }, "LICC"));
    }

    [Fact] // 🔴 un ATIS con DUE piste le tiene tutt'e due
    public void Le_piste_dell_atis_si_spezzano_tutte()
    {
        Assert.Equal(new[] { "16L", "16R" }, AwosGate.Piste("16L/16R"));
        Assert.Equal(new[] { "26" }, AwosGate.Piste("26"));
        Assert.Null(AwosGate.Piste(null));
    }
}
