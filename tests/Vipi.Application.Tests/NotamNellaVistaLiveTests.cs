using Vipi.Application.Content;
using Vipi.Application.Live;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Nella vista live i NOTAM si mostrano <b>solo se il documento di riferimento della postazione è pubblico</b>
/// (committente, 12 ottobre 2026: «solo a documento pubblico»; carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c> §6). Qui la domanda «è pubblico?», che è una per tutti i
/// tipi di postazione.
/// </summary>
public class NotamNellaVistaLiveTests
{
    private static ManagedDoc Doc(ReleaseTargetType tipo, string chiave, string acc, bool inVigore = true,
                                  bool nascosto = false) =>
        new(tipo, "titolo", chiave, acc, IsPublished: true, HasDraft: false, IsHidden: nascosto,
            ReleaseTarget: tipo, ReleaseKey: chiave, DocumentId: 1, EffectiveCycle: inVigore ? "2610" : null);

    private static readonly ManagedDoc[] Archivio =
    {
        Doc(ReleaseTargetType.Airport, "LIRN", "LIRR"),
        Doc(ReleaseTargetType.AirportMil, "LIBG", "LIBB"),
        Doc(ReleaseTargetType.App, "LICC_APP", "LIRR"),
        Doc(ReleaseTargetType.AccVipi, "LIMM|LIMM_WS2_CTR", "LIMM"),
        Doc(ReleaseTargetType.Airport, "LIRQ", "LIRR", inVigore: false),      // solo bozza, o release non ancora in vigore
        Doc(ReleaseTargetType.App, "LIBA_APP", "LIBB", inVigore: false),
        Doc(ReleaseTargetType.AccVipi, "LIRR|LIRR_TS_CTR", "LIRR", inVigore: false),
        Doc(ReleaseTargetType.Airport, "LIPE", "LIPP", nascosto: true),      // pubblicato e poi nascosto
    };

    /// <summary>Scalo, vSOP, APP: la chiave è quella che il descrittore mette nel riferimento.</summary>
    [Theory]
    [InlineData(ReleaseTargetType.Airport, "LIRN", "LIRR")]
    [InlineData(ReleaseTargetType.Airport, "lirn", "LIRR")]
    [InlineData(ReleaseTargetType.AirportMil, "LIBG", "LIBB")]
    [InlineData(ReleaseTargetType.App, "LICC_APP", "LIRR")]
    public void Un_documento_in_vigore_e_pubblico(ReleaseTargetType tipo, string chiave, string acc) =>
        Assert.True(LiveViewService.Pubblico(Archivio, new LiveDocRef(tipo, acc, chiave)));

    /// <summary>La vIPI di ACC ha una chiave per albero («LIMM|radice»): il riferimento della postazione porta il
    /// solo ACC, e si cerca per quello.</summary>
    [Fact]
    public void La_vIPI_di_ACC_si_cerca_per_ACC()
    {
        Assert.True(LiveViewService.Pubblico(Archivio, new LiveDocRef(ReleaseTargetType.AccVipi, "LIMM", null)));
        Assert.True(LiveViewService.Pubblico(Archivio, new LiveDocRef(ReleaseTargetType.AccVipi, "limm", null)));
    }

    /// <summary>
    /// 🔴 Il caso per cui la regola esiste: nella prima stesura LIRR_TS_CTR mostrava i NOTAM di Roma con la vIPI
    /// di LIRR non pubblicata. Un documento senza release in vigore — bozza, o pubblicazione futura — non mostra
    /// niente a nessuno, e nemmeno uno nascosto.
    /// </summary>
    [Theory]
    [InlineData(ReleaseTargetType.AccVipi, "LIRR", null)]
    [InlineData(ReleaseTargetType.Airport, "LIRR", "LIRQ")]
    [InlineData(ReleaseTargetType.App, "LIBB", "LIBA_APP")]
    [InlineData(ReleaseTargetType.Airport, "LIPP", "LIPE")]
    [InlineData(ReleaseTargetType.Airport, "LIRR", "LIRF")]      // nessun documento affatto
    [InlineData(ReleaseTargetType.AccVipi, "LIPP", null)]
    public void Senza_release_in_vigore_o_nascosto_non_e_pubblico(ReleaseTargetType tipo, string acc, string? chiave) =>
        Assert.False(LiveViewService.Pubblico(Archivio, new LiveDocRef(tipo, acc, chiave)));

    /// <summary>⚠️ Le due edizioni di uno scalo hanno la stessa chiave (l'ICAO) e si distinguono per il tipo: la
    /// vIPI civile pubblicata non rende pubblico il vSOP, e viceversa.</summary>
    [Fact]
    public void Il_tipo_conta_quanto_la_chiave()
    {
        Assert.False(LiveViewService.Pubblico(Archivio, new LiveDocRef(ReleaseTargetType.AirportMil, "LIRR", "LIRN")));
        Assert.False(LiveViewService.Pubblico(Archivio, new LiveDocRef(ReleaseTargetType.Airport, "LIBB", "LIBG")));
        Assert.False(LiveViewService.Pubblico(Archivio, new LiveDocRef(ReleaseTargetType.App, "LIMM", null)));
    }
}
