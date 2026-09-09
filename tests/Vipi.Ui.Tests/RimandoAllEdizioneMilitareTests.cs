using Vipi.Application.Content;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 Il rimando all'edizione militare è una decisione d'INGRESSO, non una cosa che si rifà a ogni ricarico.
///
/// <para><b>Perché esistono questi test.</b> Segnalazione dal campo del 9 settembre 2026: «dopo che sposto
/// un documento verso l'alto o il basso nell'unione vengo rimandato alla home senza che clicchi nulla».
/// Non era un clic e non era un menu: <c>NavigateTo(..., forceLoad: true)</c> stava dentro
/// <c>LoadAsyncCore</c>, cioè dentro il <b>ricarico</b>, e ogni gesto sull'unione fa ricaricare l'ospite
/// (<c>UnionPanel</c> → <c>Changed</c> → <c>UnioneCambiata</c> → <c>RicaricaAsync</c> → <c>LoadAsync</c>).</para>
/// </summary>
public class RimandoAllEdizioneMilitareTests
{
    private static AirportMilitaryState SoloMilitareSenzaCivile() => new(
        HasMilitaryPresence: true, IsMilitaryOnly: true, DocumentId: null, MilDocumentId: 42);

    /// <summary>Il caso per cui il rimando esiste: si entra sull'editor civile di un campo solo militare.</summary>
    [Fact]
    public void All_ingresso_su_un_campo_solo_militare_senza_vIPI_civile_si_rimanda()
    {
        Assert.True(RimandoAllEdizioneMilitare.Serve(
            conChrome: true, giaValutato: false, SoloMilitareSenzaCivile()));
    }

    /// <summary>
    /// 🔴 Il difetto segnalato: <b>al secondo giro no</b>. È lo stesso stato di prima — quel che cambia è
    /// solo che la domanda è già stata fatta.
    /// </summary>
    [Fact]
    public void Al_RICARICO_non_si_rimanda_piu_anche_se_lo_stato_e_identico()
    {
        Assert.False(RimandoAllEdizioneMilitare.Serve(
            conChrome: true, giaValutato: true, SoloMilitareSenzaCivile()));
    }

    /// <summary>
    /// 🔴 E un MEMBRO dell'unione non rimanda mai: si porterebbe via la pagina dell'OSPITE, cioè un
    /// documento che non è suo. <c>Chrome="false"</c> è esattamente «sono un membro».
    /// </summary>
    [Fact]
    public void Un_MEMBRO_dell_unione_non_rimanda_mai()
    {
        Assert.False(RimandoAllEdizioneMilitare.Serve(
            conChrome: false, giaValutato: false, SoloMilitareSenzaCivile()));
    }

    /// <summary>Una vIPI civile che esiste è la ragione per restare: qui si edita, e nessuno si sposta.</summary>
    [Fact]
    public void Con_la_vIPI_civile_gia_esistente_non_si_rimanda()
    {
        Assert.False(RimandoAllEdizioneMilitare.Serve(
            conChrome: true, giaValutato: false,
            new AirportMilitaryState(true, IsMilitaryOnly: true, DocumentId: 7, MilDocumentId: 42)));
    }

    /// <summary>⚠️ Base sul campo ≠ campo militare: Linate, Pisa e Ciampino hanno <c>HasMilitaryPresence</c>
    /// vero e traffico civile. Solo <c>IsMilitaryOnly</c> decide.</summary>
    [Fact]
    public void Una_base_sul_campo_da_sola_non_rimanda_nessuno()
    {
        Assert.False(RimandoAllEdizioneMilitare.Serve(
            conChrome: true, giaValutato: false,
            new AirportMilitaryState(HasMilitaryPresence: true, IsMilitaryOnly: false, null, MilDocumentId: 42)));
    }

    /// <summary>ICAO sconosciuto: non si manda nessuno da nessuna parte, lo dice la pagina.</summary>
    [Fact]
    public void Su_uno_scalo_sconosciuto_non_si_rimanda()
    {
        Assert.False(RimandoAllEdizioneMilitare.Serve(conChrome: true, giaValutato: false, stato: null));
    }
}
