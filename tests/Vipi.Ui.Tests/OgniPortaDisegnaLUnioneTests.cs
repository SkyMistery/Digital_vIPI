using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'ordine di una pagina unita lo decide la <b>porta</b> da cui si entra (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §13, 10 settembre 2026): primo il documento della
/// porta, gli altri di seguito nell'<b>ordine memorizzato</b>, senza di lui.
///
/// <para>Sostituisce <c>RimandoAllOspiteTests</c>, che pinnava il rimando della §4 — la vista pubblica di un
/// membro che mandava alla pagina dell'ospite. Quel rimando non esiste più: non c'è un ospite, e nessuna
/// porta manda altrove.</para>
///
/// <para>🔴 <b>Perché la regola sta in una funzione pura e non nel caricatore.</b> Sbagliarla non dà nessun
/// errore: la pagina disegna gli stessi documenti in un ordine che nessuno ha chiesto, e chi guarda non ha
/// modo di accorgersene se non conoscendo l'ordine giusto. È la stessa forma dei tre difetti seri della
/// supervisione — una cosa <b>falsa a schermo</b>, senza rossi.</para>
/// </summary>
public class OgniPortaDisegnaLUnioneTests
{
    private static ManagedDoc Doc(int id, ReleaseTargetType tipo, string chiave, string titolo) =>
        new(tipo, titolo, chiave, "LIRR", IsPublished: true, HasDraft: false, IsHidden: false, tipo, chiave, id);

    /// <summary>
    /// Il caso chiesto dal committente: la vIPI d'aeroporto, il vSOP dello stesso scalo e il suo
    /// avvicinamento, memorizzati in quest'ordine.
    /// <para>⚠️ vIPI e vSOP hanno la <b>stessa</b> chiave di release — l'ICAO — e si distinguono per il solo
    /// tipo. È il caso che rende obbligatoria la coppia famiglia+chiave in <see cref="UnionView.Di"/>.</para>
    /// </summary>
    private static UnionView Tre() =>
        new(1, new[]
        {
            new UnionMemberView(1, 0, Doc(26, ReleaseTargetType.Airport, "LIMN", "vIPI — LIMN")),
            new UnionMemberView(2, 1, Doc(28, ReleaseTargetType.AirportMil, "LIMN", "vSOP MIL — LIMN")),
            new UnionMemberView(3, 2, Doc(3, ReleaseTargetType.App, "LIMN_APP", "Cameri Approach")),
        });

    [Fact]
    public void Dalla_vIPI_si_legge_vIPI_poi_vSOP_poi_APP()
    {
        var unione = Tre();
        var mio = unione.Di(ReleaseTargetType.Airport, "LIMN");

        Assert.NotNull(mio);
        Assert.Equal(new[] { 28, 3 }, unione.AltriDa(mio!.DocumentId).Select(m => m.DocumentId));
    }

    [Fact]
    public void Dal_vSOP_si_legge_vSOP_poi_vIPI_poi_APP()
    {
        var unione = Tre();
        var mio = unione.Di(ReleaseTargetType.AirportMil, "LIMN");

        Assert.NotNull(mio);
        // ⚠️ Gli altri tengono l'ordine memorizzato FRA LORO: la vIPI resta prima dell'APP anche se chi
        // guarda è entrato dal vSOP. La porta si toglie dalla fila, non la rimescola.
        Assert.Equal(new[] { 26, 3 }, unione.AltriDa(mio!.DocumentId).Select(m => m.DocumentId));
    }

    [Fact]
    public void Dall_APP_si_legge_APP_poi_vIPI_poi_vSOP()
    {
        var unione = Tre();
        var mio = unione.Di(ReleaseTargetType.App, "LIMN_APP");

        Assert.NotNull(mio);
        Assert.Equal(new[] { 26, 28 }, unione.AltriDa(mio!.DocumentId).Select(m => m.DocumentId));
    }

    /// <summary>
    /// 🔴 La coppia famiglia+chiave, e non la sola chiave. Un aeroporto e il suo vSOP militare hanno la
    /// stessa chiave di release: confrontarla da sola farebbe disegnare alla pagina civile l'unione vista
    /// dalla porta del militare — stessi documenti, ordine sbagliato, nessun errore.
    /// </summary>
    [Fact]
    public void La_porta_si_riconosce_da_FAMIGLIA_E_CHIAVE_insieme()
    {
        var unione = Tre();

        Assert.Equal(26, unione.Di(ReleaseTargetType.Airport, "LIMN")!.DocumentId);
        Assert.Equal(28, unione.Di(ReleaseTargetType.AirportMil, "LIMN")!.DocumentId);
    }

    /// <summary>⚠️ La chiave si confronta senza guardare le maiuscole: le rotte la portano com'è scritta
    /// nell'indirizzo, e un ICAO minuscolo è un indirizzo valido.</summary>
    [Fact]
    public void La_chiave_si_confronta_senza_guardare_le_maiuscole()
    {
        Assert.Equal(26, Tre().Di(ReleaseTargetType.Airport, "limn")!.DocumentId);
    }

    /// <summary>
    /// ⚠️ Un documento che l'unione non descrive non è una porta: <c>null</c>, e la pagina disegna sé stessa
    /// e basta. Succede per davvero — un membro il cui descrittore manca si <b>salta</b> in proiezione — e
    /// senza questa risposta la pagina cadrebbe invece di mostrare quel che sa mostrare.
    /// </summary>
    [Fact]
    public void Un_documento_che_l_unione_non_descrive_non_e_una_porta()
    {
        Assert.Null(Tre().Di(ReleaseTargetType.App, "LIRF_APP"));
        Assert.Null(Tre().Di(ReleaseTargetType.Airport, "LIRF"));
    }

    /// <summary>
    /// Il fatto misurato che ha deciso il modello: <b>LIBV Gioia del Colle ha DUE APP</b> non remotizzati.
    /// Entrando dal secondo, il primo resta davanti al vSOP — è l'ordine memorizzato, l'unico che sappia
    /// dire quale dei due APP viene prima.
    /// </summary>
    [Fact]
    public void Con_DUE_APP_dello_stesso_scalo_l_ordine_memorizzato_regge()
    {
        var unione = new UnionView(2, new[]
        {
            new UnionMemberView(1, 0, Doc(24, ReleaseTargetType.AirportMil, "LIBV", "vSOP MIL — LIBV")),
            new UnionMemberView(2, 1, Doc(3, ReleaseTargetType.App, "LIBV_APP", "Gioia del Colle Approach")),
            new UnionMemberView(3, 2, Doc(5, ReleaseTargetType.App, "LIBV_G_APP", "Gioia del Colle Approach G")),
        });

        var mio = unione.Di(ReleaseTargetType.App, "LIBV_G_APP");

        Assert.NotNull(mio);
        Assert.Equal(new[] { 24, 3 }, unione.AltriDa(mio!.DocumentId).Select(m => m.DocumentId));
    }

    /// <summary>⚠️ Due membri soli: da ognuna delle due porte ne resta uno. È il caso normale, e il
    /// controllo che <c>AltriDa</c> non torni mai la porta stessa.</summary>
    [Fact]
    public void La_porta_non_compare_mai_fra_gli_altri()
    {
        var unione = Tre();

        foreach (var m in unione.Members)
            Assert.DoesNotContain(unione.AltriDa(m.DocumentId), a => a.DocumentId == m.DocumentId);
    }
}
