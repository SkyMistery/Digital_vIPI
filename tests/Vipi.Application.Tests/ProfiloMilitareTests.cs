using Vipi.Application.Content;

namespace Vipi.Application.Tests;

/// <summary>
/// Il profilo di catalogo dei vSOP militari (carta <c>2026-08-27-vsop-militari.md</c> §2).
///
/// <para>
/// I quindici SOP reali hanno <b>lo stesso indice</b>, parola per parola: non è contenuto libero, è un
/// profilo. Questi test lo tengono fedele al documento vero — se qualcuno un giorno ne toglie una sezione
/// «perché non serve», deve farlo di proposito e non per distrazione.
/// </para>
/// </summary>
public class ProfiloMilitareTests
{
    private static IReadOnlyList<SectionDescriptor> Mil => SectionCatalog.For(SectionProfile.AirportMil);

    private static IEnumerable<SectionDescriptor> Tutte(IEnumerable<SectionDescriptor> d) =>
        d.SelectMany(x => new[] { x }.Concat(Tutte(x.Children ?? Array.Empty<SectionDescriptor>())));

    // ---- La forma del profilo -------------------------------------------------------------------------

    [Fact]
    public void Le_sezioni_sono_ventisei()
    {
        // Il numero è nella carta e nell'indice della documentazione: se cambia, cambia in tre posti o in
        // nessuno.
        // ⚠️ Ventisei, e questo test l'ha già guadagnato: la carta diceva ventiquattro perché il conto era
        // rimasto indietro di due quando si sono aggiunte «qra» e «lowlevel». Un numero scritto a mano in
        // tre documenti invecchia; uno contato sul profilo no.
        Assert.Equal(26, Tutte(Mil).Count());
    }

    [Fact]
    public void I_contenitori_di_primo_livello_sono_sei_e_nell_ordine_del_PDF()
    {
        Assert.Equal(
            new[] { "weather", "generaldata", "groundprocedures", "flightprocedures", "regulated", "validity" },
            Mil.OrderBy(d => d.Order).Select(d => d.Key));
    }

    [Fact]
    public void Il_documento_NON_nasce_piatto()
    {
        // ⚠️ È la ragione per cui DocumentBirth ha imparato a ricorrere. Senza figli, questo profilo
        // darebbe ventiquattro sezioni di primo livello invece di sei con dentro le loro.
        Assert.Equal(6, Mil.Count);
        Assert.Equal(6, Mil.Single(d => d.Key == "generaldata").Children!.Count);
        Assert.Equal(4, Mil.Single(d => d.Key == "groundprocedures").Children!.Count);
        Assert.Equal(8, Mil.Single(d => d.Key == "flightprocedures").Children!.Count);
        Assert.Equal(2, Mil.Single(d => d.Key == "regulated").Children!.Count);
    }

    [Fact]
    public void Nessuna_sezione_annida_oltre_il_limite()
    {
        // Il vincolo è applicativo, non del database: sforarlo darebbe un documento fuori regola, e il
        // difetto si vedrebbe solo a schermo in una TOC che non rientra.
        static int Prof(SectionDescriptor d) =>
            d.Children is { Count: > 0 } f ? 1 + f.Max(Prof) : 0;
        Assert.True(Mil.Max(Prof) < Vipi.Domain.Entities.DocumentSection.MaxDepth);
    }

    // ---- Il riuso ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("weather")]
    [InlineData("frequencies")]
    [InlineData("runways")]
    [InlineData("transition")]
    [InlineData("regulated")]
    [InlineData("operationaltechnique")]
    [InlineData("validity")]
    public void Le_chiavi_riusate_sono_le_STESSE_del_catalogo_civile(string chiave)
    {
        // Non chiavi nuove che somigliano a quelle civili: le stesse. È ciò che fa arrivare gratis il
        // motore — la mappa AoR con le chip È GIÀ quello che il PDF disegna a mano.
        Assert.Contains(chiave, Tutte(Mil).Select(d => d.Key));
        Assert.Equal(SectionCatalog.KindOf(chiave), Tutte(Mil).First(d => d.Key == chiave).Kind);
    }

    [Fact]
    public void Le_due_sezioni_che_riusano_anche_il_MOTORE_tengono_i_propri_blocchi()
    {
        // «frequencies» e «runways» sono derivate PIÙ blocchi: la parte derivabile dall'anagrafica su un
        // campo militare è la minoranza — la tabella ATC/CRC di LIPI elenca anche l'APP di un ALTRO campo
        // e i CRC/AEW, che nel catalogo settori non esistono.
        foreach (var k in new[] { "frequencies", "runways", "regulated", "validity" })
            Assert.True(SectionCatalog.KeepsOwnBlocks(SectionProfile.AirportMil, k)
                        || Tutte(Mil).First(d => d.Key == k).BodySource == SectionBodySource.HostAndBlocks,
                        $"«{k}» dovrebbe tenere anche i propri blocchi");
    }

    // ---- Quel che NON c'è, e di proposito -------------------------------------------------------------

    [Theory]
    [InlineData("aor")]           // un aeroporto è un LUOGO: l'AoR è della torre
    [InlineData("coordination")]  // idem
    [InlineData("sids")]          // l'import SID Aurora non copre i campi militari
    public void Cio_che_e_stato_lasciato_fuori_resta_fuori(string chiave) =>
        Assert.DoesNotContain(chiave, Tutte(Mil).Select(d => d.Key));

    [Fact]
    public void QRA_c_e_anche_se_nei_PDF_non_esiste_come_sezione()
    {
        // ⚠️ Contenuto NUOVO, non trascrizione: nei quindici PDF «QRA» compare solo come colonna, e solo
        // sulle quattro basi di difesa aerea. Si semina su tutti perché nascondere è un clic, mentre
        // aggiungere una sezione di catalogo non seminata non lo è.
        Assert.Contains("qra", Tutte(Mil).Select(d => d.Key));
    }

    [Fact]
    public void La_bassa_quota_sta_sotto_le_AREE_non_fra_le_procedure_di_volo()
    {
        // Nei PDF è sempre sorella di partenze/arrivi dentro WORKING AREAS, e il contenuto lo spiega:
        // «Tactical Areas where BOAT can be executed». Parla di AREE.
        Assert.Contains("lowlevel", Mil.Single(d => d.Key == "regulated").Children!.Select(d => d.Key));
    }

    // ---- L'APP militare ------------------------------------------------------------------------------

    [Fact]
    public void L_APP_militare_RIMANDA_al_civile_invece_di_ricopiarlo()
    {
        // ⚠️ Stessa ISTANZA, non un elenco uguale: due elenchi che devono restare uguali divergono, ed è
        // già successo fra VloaSections e questo registro. Il giorno che il militare avrà sezioni sue si
        // separano, e sarà una scelta.
        Assert.Same(SectionCatalog.For(SectionProfile.App), SectionCatalog.For(SectionProfile.AppMil));
    }

    // ---- Le chiavi -----------------------------------------------------------------------------------

    // ---- Il catalogo risponde anche sulle sezioni ANNIDATE (trovato a schermo il 29 agosto 2026) ------

    [Theory]
    [InlineData("frequencies")]
    [InlineData("runways")]
    [InlineData("transition")]
    public void Le_derivate_ANNIDATE_sono_RESE_DALLA_PAGINA(string chiave)
    {
        // ⚠️ È IL test del difetto visto a schermo: `SectionCatalog.Find` guardava solo il PRIMO LIVELLO del
        // profilo, e queste tre stanno sotto «Dati generali». Rispondeva `null`, quindi «non è resa dalla
        // pagina» e «non è di catalogo» — e nel documento pubblicato uscivano tre TITOLI VUOTI, perché la
        // scheda non la disegnava nessuno e i blocchi di una derivata sono vuoti per costruzione.
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, chiave),
            $"«{chiave}» è annidata sotto «generaldata»: il catalogo deve trovarla lo stesso.");
    }

    [Fact]
    public void TUTTE_le_ventisei_sezioni_sono_di_CATALOGO_anche_le_figlie()
    {
        // `IsFixed` decide se una sezione si può cancellare o rinominare nell'editor. Con la ricerca ferma al
        // primo livello, VENTI sezioni di catalogo su ventisei passavano per sezioni libere.
        static IEnumerable<SectionDescriptor> Tutte(IEnumerable<SectionDescriptor> d) =>
            d.SelectMany(x => new[] { x }.Concat(Tutte(x.Children ?? Array.Empty<SectionDescriptor>())));

        var chiavi = Tutte(SectionCatalog.For(SectionProfile.AirportMil)).Select(d => d.Key).ToList();

        Assert.Equal(26, chiavi.Count);
        Assert.All(chiavi, k => Assert.True(SectionCatalog.IsFixed(SectionProfile.AirportMil, k), k));
    }

    [Fact]
    public void La_discesa_nei_figli_non_cambia_gli_ALTRI_profili()
    {
        // La misura che rende sicura la modifica: gli unici descrittori con figli sono i quattro contenitori
        // del profilo militare. Se un giorno un altro profilo ne avesse, questo test lo dice — e chi lo
        // aggiunge deve rileggere che cosa cambia per `IsFixed` e `IsHostRendered`.
        static bool HaFigli(IEnumerable<SectionDescriptor> d) =>
            d.Any(x => x.Children is { Count: > 0 } || HaFigli(x.Children ?? Array.Empty<SectionDescriptor>()));

        foreach (var p in new[] { SectionProfile.App, SectionProfile.AccAerovia, SectionProfile.AccAppBlock,
                                  SectionProfile.Vloa, SectionProfile.Airport, SectionProfile.AppMil })
            Assert.False(HaFigli(SectionCatalog.For(p)), p.ToString());

        Assert.True(HaFigli(SectionCatalog.For(SectionProfile.AirportMil)));
    }

    [Fact]
    public void Nessuna_chiave_e_ripetuta()
    {
        // Due sezioni con la stessa chiave nello stesso documento rendono ambigua ogni lettura per chiave —
        // la cattura frozen, gli anchor di pagina, il «nascondi sezione». È già costato caro sui
        // coordinamenti vLOA.
        var chiavi = Tutte(Mil).Select(d => d.Key).ToList();
        Assert.Equal(chiavi.Count, chiavi.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Ogni_sezione_ha_un_titolo_in_italiano()
    {
        // §1d: la lingua sorgente è quella in cui si REDIGE, non quella dei PDF di partenza. Un titolo
        // vuoto o inglese qui vorrebbe dire che qualcuno ha rimesso in piedi la premessa vecchia.
        Assert.All(Tutte(Mil), d => Assert.False(string.IsNullOrWhiteSpace(d.Title)));
        Assert.Equal("Dati generali", Mil.Single(d => d.Key == "generaldata").Title);
        Assert.Equal("Piste", Tutte(Mil).First(d => d.Key == "runways").Title);
    }
}
