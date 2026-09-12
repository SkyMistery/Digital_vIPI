using Vipi.Application.Content;
using Vipi.Domain;

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
    public void Le_sezioni_sono_quarantatre()
    {
        // Il numero è nella carta e nell'indice della documentazione: se cambia, cambia in tre posti o in
        // nessuno.
        // ⚠️ Questo test l'ha già guadagnato due volte: la carta diceva ventiquattro quando erano ventisei,
        // e il commento del catalogo diceva ventisei quando erano trentadue. Un numero scritto a mano in tre
        // documenti invecchia; uno contato sul profilo no.
        // ⚠️ Quarantatré dal 6 settembre 2026: trentadue meno «qra» (fuori: non sta in nessuno dei quindici
        // PDF) più le dodici dell'indice chiesto dal SOD.
        // ⚠️ QUARANTAQUATTRO dal 10 settembre 2026: le SID (committente).
        // ⚠️ QUARANTACINQUE dall'11 settembre 2026: le regole piste (committente).
        // ⚠️ QUARANTASEI dal 12 settembre 2026: i minimi LVP (committente), subito dopo le regole piste.
        Assert.Equal(46, Tutte(Mil).Count());
    }

    // ---- Le regole piste (carta 2026-09-11-regole-piste-nel-vsop-militare.md) --------------------------

    /// <summary>
    /// Le regole piste stanno in «Dati generali», <b>subito dopo le Piste</b> e prima delle SID — deciso dal
    /// committente. Sorelle e non figlie di «Piste», come le SID.
    /// </summary>
    [Fact]
    public void Le_regole_piste_stanno_subito_DOPO_le_piste_e_prima_delle_SID()
    {
        var generali = Mil.Single(d => d.Key == "generaldata").Children!.OrderBy(d => d.Order).Select(d => d.Key).ToList();

        var piste = generali.IndexOf("runways");
        Assert.Equal("runwayrules", generali[piste + 1]);
        // ⚠️ Fra le regole e le SID sono entrati i minimi LVP (12 settembre 2026): sono le due sezioni che
        // si leggono dal METAR, e stanno vicine apposta.
        Assert.Equal("lvp", generali[piste + 2]);
        Assert.Equal("sids", generali[piste + 3]);
    }

    /// <summary>
    /// ⚠️ La STESSA chiave del profilo civile, ed è ciò che fa arrivare tutto il resto senza una riga nuova: il
    /// congelamento alla release (`AirportFrozenSectionProvider`, registrato per le due edizioni, sa già
    /// fotografare «runwayrules»), la derivazione per la vista, e la scheda delle sezioni in comune in
    /// un'unione vIPI + vSOP dello stesso scalo. Una chiave nuova che le somigliasse non avrebbe nessuna delle tre.
    /// </summary>
    [Fact]
    public void Le_regole_piste_sono_la_STESSA_sezione_derivata_del_profilo_civile()
    {
        Assert.NotNull(SectionCatalog.Find(SectionProfile.Airport, "runwayrules"));
        Assert.NotNull(SectionCatalog.Find(SectionProfile.AirportMil, "runwayrules"));
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, "runwayrules"));
        Assert.False(SectionCatalog.KeepsOwnBlocks(SectionProfile.AirportMil, "runwayrules"));
        Assert.Equal(SectionKind.Derived, SectionCatalog.KindOf("runwayrules"));
    }

    // ---- Le SID (carta 2026-09-10-sid-nel-vsop-militare.md) --------------------------------------------

    /// <summary>
    /// Le SID stanno in «Dati generali», <b>subito dopo le piste</b> — deciso dal committente. Dall'11
    /// settembre 2026 fra le due ci sono le regole piste, sempre per decisione del committente.
    /// <para>🔴 SORELLA e non figlia: è una scelta d'indice. ⚠️ Qui c'era scritto che una figlia «porterebbe il
    /// profilo oltre <c>MaxDepth</c>»: non è vero — «Dati generali › Piste › figlia» sta a profondità 2, come
    /// le soglie, e il limite è 3. Il test lo pinna perché la differenza fra le due si vede solo scendendo
    /// nell'albero.</para>
    /// </summary>
    [Fact]
    public void Le_SID_stanno_subito_DOPO_le_piste_dentro_i_dati_generali()
    {
        var generali = Mil.Single(d => d.Key == "generaldata").Children!.OrderBy(d => d.Order).Select(d => d.Key).ToList();

        Assert.Equal(new[] { "navaids", "frequencies", "diversion", "runways", "runwayrules", "lvp", "sids", "transition",
                             "callsigns", SectionKeys.AirportLayout, "parkings" }, generali);
        // E non è figlia di «Piste»: là sotto c'è solo la sotto-sezione delle soglie.
        var piste = Mil.Single(d => d.Key == "generaldata").Children!.Single(d => d.Key == "runways");
        Assert.Equal(new[] { SectionKeys.RunwayThresholds }, piste.Children!.Select(d => d.Key));
    }

    /// <summary>
    /// ⚠️ Le SID sono <b>derivate</b>, come nel profilo civile: la pagina disegna la tabella e non c'è
    /// nessun blocco di prosa da scrivere. Le code per campo restano sezioni <b>libere</b>.
    /// </summary>
    [Fact]
    public void Le_SID_sono_una_sezione_DERIVATA_senza_blocchi()
    {
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, "sids"));
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.Airport, "sids"));
        // ⚠️ La stessa CHIAVE nei due profili, e non è un dettaglio: il congelamento alla release passa per
        // la chiave, e `AirportFrozenSectionProvider` è lo stesso provider registrato due volte.
        Assert.NotNull(SectionCatalog.Find(SectionProfile.AirportMil, "sids"));
    }

    [Fact]
    public void I_contenitori_di_primo_livello_sono_sei_e_nell_ordine_del_PDF()
    {
        Assert.Equal(
            new[] { "weather", "generaldata", "groundprocedures", "flightprocedures", "regulated", "charts", "validity" },
            Mil.OrderBy(d => d.Order).Select(d => d.Key));
    }

    [Fact]
    public void Il_documento_NON_nasce_piatto()
    {
        // ⚠️ È la ragione per cui DocumentBirth ha imparato a ricorrere. Senza figli, questo profilo
        // darebbe ventiquattro sezioni di primo livello invece di sei con dentro le loro.
        Assert.Equal(7, Mil.Count);
        Assert.Equal(11, Mil.Single(d => d.Key == "generaldata").Children!.Count);
        Assert.Equal(5, Mil.Single(d => d.Key == "charts").Children!.Count);
        Assert.Equal(3, Mil.Single(d => d.Key == "groundprocedures").Children!.Count);
        Assert.Equal(9, Mil.Single(d => d.Key == "flightprocedures").Children!.Count);
        Assert.Equal(2, Mil.Single(d => d.Key == "regulated").Children!.Count);
    }

    [Fact]
    public void Nessuna_sezione_annida_oltre_il_limite()
    {
        // Il vincolo è applicativo, non del database: sforarlo darebbe un documento fuori regola, e il
        // difetto si vedrebbe solo a schermo in una TOC che non rientra.
        static int Prof(SectionDescriptor d) =>
            d.Children is { Count: > 0 } f ? 1 + f.Max(Prof) : 0;
        Assert.True(Mil.Max(Prof) <= Vipi.Domain.Entities.DocumentSection.MaxDepth);
    }

    [Fact]
    public void Il_profilo_TOCCA_il_limite_di_profondita_e_non_e_un_caso()
    {
        // ⚠️ Dal 6 settembre 2026 il profilo sta ESATTO sul bordo: «Aree di lavoro» → «Procedure generali» →
        // «Procedure di partenza» → «VFR» è profondità 3, e 3 è il massimo. Non c'è margine, e chi volesse
        // annidare sotto quelle quattro foglie non può — `DocumentBirth.Semina` alza un'eccezione alla
        // NASCITA del documento, che è il posto giusto per accorgersene.
        // Il test di sopra dice «non sfora»; questo dice «ci sta appoggiato». Sono due fatti diversi: il
        // primo resterebbe verde anche se un domani il ramo si accorciasse per sbaglio, e allora nessuno
        // saprebbe più che quel limite era una decisione.
        static int Prof(SectionDescriptor d) =>
            d.Children is { Count: > 0 } f ? 1 + f.Max(Prof) : 0;
        Assert.Equal(Vipi.Domain.Entities.DocumentSection.MaxDepth, Mil.Max(Prof));

        var aree = Mil.Single(d => d.Key == "regulated").Children!.Single(d => d.Key == "operationaltechnique");
        Assert.Equal(
            new[] { SectionKeys.DepartureProcedures, SectionKeys.ArrivalProcedures },
            aree.Children!.OrderBy(d => d.Order).Select(d => d.Key));
        foreach (var gruppo in aree.Children!)
            Assert.Equal(new[] { "VFR", "IFR" }, gruppo.Children!.OrderBy(d => d.Order).Select(d => d.Title));
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

    // 🔴 «sids» stava QUI, con la ragione «l'import SID Aurora non copre i campi militari». Il 10 settembre
    // 2026 il committente ha chiesto le SID nel vSOP, e la premessa è stata MISURATA sull'archivio vero:
    // LIBG 18, LIBN 22, LIBV 24 SID importate — tutti campi SOLO MILITARI. La ragione dell'esclusione era
    // falsa sui dati, quindi l'esclusione è caduta. (LIMN ne ha zero: la sezione lì nasce vuota, ed è vero.)
    // ⚠️ Non è stata tolta una riga: è stata ribaltata una premessa, e sta scritto qui perché fra sei mesi
    // la domanda «perché prima no?» abbia una risposta.
    [Theory]
    [InlineData("aor")]           // un aeroporto è un LUOGO: l'AoR è della torre
    [InlineData("coordination")]  // idem
    [InlineData("qra")]           // vedi sotto: l'unica che avevamo inventato noi
    public void Cio_che_e_stato_lasciato_fuori_resta_fuori(string chiave) =>
        Assert.DoesNotContain(chiave, Tutte(Mil).Select(d => d.Key));

    [Fact]
    public void QRA_e_uscita_perche_non_sta_in_nessuno_dei_quindici_PDF()
    {
        // ⚠️ Era l'unica sezione INVENTATA da noi (27 agosto 2026): nei quindici PDF «QRA» compare solo
        // come colonna, e solo sulle quattro basi di difesa aerea. L'indice chiesto dal SOD il 6 settembre
        // non la prevede, e una sezione che nasce su quindici campi per essere nascosta su undici non la
        // vuole nessuno.
        // ⚠️ Il catalogo decide la struttura solo alla NASCITA: questo test dice che i vSOP NUOVI non ce
        // l'hanno. Dai vecchi la toglie `IDocumentMaintenance.RemoveMilQraSectionsAsync`, e a quello serve
        // il suo test — che non può stare qui, perché tocca il database.
        Assert.DoesNotContain("qra", Tutte(Mil).Select(d => d.Key));
    }

    [Fact]
    public void La_bassa_quota_sta_sotto_le_AREE_non_fra_le_procedure_di_volo()
    {
        // Nei PDF è sempre sorella di partenze/arrivi dentro WORKING AREAS, e il contenuto lo spiega:
        // «Tactical Areas where BOAT can be executed». Parla di AREE.
        Assert.Contains("lowlevel", Mil.Single(d => d.Key == "regulated").Children!.Select(d => d.Key));
    }

    [Fact]
    public void La_bassa_quota_ha_un_visualizzatore_SUO_e_tiene_anche_i_propri_blocchi()
    {
        // Carta 2026-09-09-aree-boat.md: la sotto-sezione disegna mappa, elenco e tabella delle aree BOAT,
        // come fa il padre con le working areas — quindi il corpo lo produce la PAGINA.
        // ⚠️ `HostAndBlocks` e non `Host`: sotto la scheda restano i blocchi editoriali. Fino all'8
        // settembre 2026 questa sezione era prosa e basta, e chi ci aveva scritto dentro non deve perderla.
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, "lowlevel"));
        Assert.True(SectionCatalog.KeepsOwnBlocks(SectionProfile.AirportMil, "lowlevel"));

        // ⚠️ E la NATURA non cambia: non c'è niente da congelare alla release, quindi niente interruttore
        // Live/Frozen. È la stessa scelta di «regulated», ed è la lezione delle coordinate delle soglie.
        Assert.Equal(SectionKind.Editorial, SectionCatalog.KindOf("lowlevel"));
        Assert.False(SectionCatalog.IsRenderModeToggleable("lowlevel"));
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
    public void TUTTE_le_sezioni_sono_di_CATALOGO_anche_le_figlie()
    {
        // `IsFixed` decide se una sezione si può cancellare o rinominare nell'editor. Con la ricerca ferma al
        // primo livello, VENTI sezioni di catalogo su ventisei passavano per sezioni libere.
        static IEnumerable<SectionDescriptor> Tutte(IEnumerable<SectionDescriptor> d) =>
            d.SelectMany(x => new[] { x }.Concat(Tutte(x.Children ?? Array.Empty<SectionDescriptor>())));

        var chiavi = Tutte(SectionCatalog.For(SectionProfile.AirportMil)).Select(d => d.Key).ToList();

        Assert.Equal(46, chiavi.Count);
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
                                  SectionProfile.Vloa, SectionProfile.AppMil })
            Assert.False(HaFigli(SectionCatalog.For(p)), p.ToString());

        // ⚠️ Dal 3 settembre 2026 i profili annidati sono DUE: il militare e la vIPI d'aeroporto, che ha preso
        // «Carte aeroportuali» con le sue cinque raccolte. È la riga che dice a chi ne aggiungesse un terzo di
        // rileggere che cosa cambia per `IsFixed` e `IsHostRendered` — e che la nascita del documento deve
        // ricorrere (DocumentBirth.Semina).
        Assert.True(HaFigli(SectionCatalog.For(SectionProfile.AirportMil)));
        Assert.True(HaFigli(SectionCatalog.For(SectionProfile.Airport)));
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

    // ---- I parcheggi: un DATO dello scalo, non una procedura (3 settembre 2026) -----------------------

    /// <summary>
    /// I parcheggi stanno <b>in coda ai Dati generali</b> e non più in testa alle Procedure di terra.
    /// Richiesta del committente: un piazzale e i suoi stalli sono un dato del campo — come piste,
    /// radioassistenze e frequenze — non una procedura che si esegue.
    ///
    /// <para>⚠️ Il catalogo decide la struttura <b>solo alla nascita</b>: i vSOP già scritti li porta avanti
    /// <c>IDocumentMaintenance.ReparentMilParkingsAsync</c>, perché a mano nessuno potrebbe — il motore di
    /// riordino sposta soltanto fra fratelli.</para>
    /// </summary>
    [Fact]
    public void I_parcheggi_chiudono_i_dati_generali()
    {
        var generali = Mil.Single(d => d.Key == "generaldata").Children!;
        Assert.Equal("parkings", generali.OrderBy(d => d.Order).Last().Key);
    }

    [Fact]
    public void I_parcheggi_NON_stanno_piu_fra_le_procedure_di_terra()
    {
        var terra = Mil.Single(d => d.Key == "groundprocedures").Children!;
        Assert.DoesNotContain("parkings", terra.Select(d => d.Key));
        // E il gruppo che l'ha persa riparte da uno: l'ordine di catalogo è una posizione fra fratelli, e un
        // buco in testa direbbe che manca qualcosa.
        Assert.Equal(new[] { 1, 2, 3 }, terra.OrderBy(d => d.Order).Select(d => d.Order));
    }

    /// <summary>
    /// La chiave resta <b>la stessa</b>, ed è ciò che rende lo spostamento un cambio di posto e non un
    /// trapianto: il corpo, la tabella fissa <c>milparkings</c> e i blocchi si cercano per chiave.
    /// </summary>
    [Fact]
    public void Spostandola_la_chiave_non_cambia()
    {
        var parcheggi = Tutte(Mil).Single(d => d.Key == "parkings");
        Assert.Equal(SectionCatalog.KindOf("parkings"), parcheggi.Kind);
        Assert.True(SectionCatalog.IsFixed(SectionProfile.AirportMil, "parkings"));
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, "parkings"));
    }

    // ---- Le carte dello scalo (3 settembre 2026) ------------------------------------------------------

    /// <summary>
    /// «Carte aeroportuali» sta <b>prima</b> di «Validità e revisione», e la validità resta l'ultima: è il
    /// timbro che chiude il documento, non una sezione fra le altre.
    /// </summary>
    [Theory]
    [InlineData(SectionProfile.AirportMil)]
    [InlineData(SectionProfile.Airport)]
    public void Le_carte_stanno_appena_prima_della_validita(SectionProfile profilo)
    {
        var radici = SectionCatalog.For(profilo).OrderBy(d => d.Order).Select(d => d.Key).ToList();

        Assert.Equal("validity", radici[^1]);
        Assert.Equal("charts", radici[^2]);
    }

    /// <summary>
    /// Le cinque raccolte, nell'ordine chiesto. ⚠️ <b>Scritte una volta sola</b> per le due famiglie: la vIPI
    /// d'aeroporto e il vSOP militare descrivono lo stesso luogo, e due elenchi copiati sarebbero due elenchi
    /// diversi al primo ritocco.
    /// </summary>
    [Theory]
    [InlineData(SectionProfile.AirportMil)]
    [InlineData(SectionProfile.Airport)]
    public void Le_carte_hanno_le_cinque_raccolte_nello_stesso_ordine(SectionProfile profilo)
    {
        var carte = SectionCatalog.For(profilo).Single(d => d.Key == "charts").Children!;

        Assert.Equal(
            new[] { "charts:aerodrome", "charts:iac", "charts:sid", "charts:star", "charts:vfr" },
            carte.OrderBy(d => d.Order).Select(d => d.Key));
        Assert.All(carte, c => Assert.True(SectionCatalog.IsFixed(profilo, c.Key), c.Key));
    }

    /// <summary>
    /// ⚠️ Le carte NON riusano le chiavi <c>sids</c> e <c>vfr</c>, che pure direbbero la stessa parola: quelle
    /// hanno già un mestiere — le SID <b>importate</b> della vIPI d'aeroporto e la sezione VFR di un profilo di
    /// posizione — e la pagina rende il loro corpo da sé. Riusarle avrebbe messo la tabella delle SID importate
    /// dentro una raccolta di carte, e avrebbe rotto l'unicità della chiave dentro il profilo.
    /// </summary>
    [Fact]
    public void Le_carte_non_rubano_le_chiavi_delle_SID_importate()
    {
        var aeroporto = SectionCatalog.For(SectionProfile.Airport);
        Assert.Contains("sids", aeroporto.Select(d => d.Key));               // le SID importate restano dove sono
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.Airport, "sids"));
        // Le raccolte di carte sono editoriali: il corpo sono immagini e allegati nei blocchi.
        Assert.False(SectionCatalog.IsHostRendered(SectionProfile.Airport, "charts:sid"));
        Assert.Equal(SectionKind.Editorial, SectionCatalog.KindOf("charts:sid"));
    }

    // ---- L'indice chiesto dal SOD (6 settembre 2026) --------------------------------------------------

    /// <summary>
    /// Le dodici sezioni che il SOD marca «[PILOTS]». ⚠️ Scritte QUI a mano, e di proposito: è l'unico
    /// posto del progetto dove quell'elenco esiste due volte, e la seconda copia serve — un default che
    /// cambia senza che nessuno lo voglia è esattamente la classe di modifica che un test deve fermare.
    /// </summary>
    public static TheoryData<string> MarcatePiloti => new(
        "diversion", SectionKeys.RunwayThresholds, "callsigns", "parkings", SectionKeys.ApronFlow,
        "enginestart", "arming", "takeoff", SectionKeys.ArrivalRestrictions, SectionKeys.VfrJetPoints,
        "ifrsignificant", "lowlevel");

    [Theory]
    [MemberData(nameof(MarcatePiloti))]
    public void Le_sezioni_marcate_dal_SOD_nascono_per_i_PILOTI(string chiave) =>
        Assert.Equal(SectionAudience.Pilots, Tutte(Mil).Single(d => d.Key == chiave).Audience);

    [Fact]
    public void E_nessun_altra_nasce_marcata()
    {
        // Il contrario del test di sopra, e non è la stessa cosa detta due volte: quello pretende che le
        // dodici ci siano, questo che non ce ne sia una tredicesima.
        // ⚠️ Marcare `Pilots` NASCONDE alla vista ATC, e si porta dietro i figli (`AudienceFilter`). Una
        // marcatura di troppo non si vede da nessuna parte finché un controllore non apre la sua vista e
        // trova un buco.
        var marcate = Tutte(Mil).Where(d => d.Audience != SectionAudience.Both).Select(d => d.Key).ToList();
        Assert.Equal(12, marcate.Count);
        Assert.All(marcate, k => Assert.Equal(SectionAudience.Pilots,
                                              Tutte(Mil).Single(d => d.Key == k).Audience));
    }

    [Fact]
    public void Gli_altri_profili_non_hanno_sezioni_marcate()
    {
        // Il pubblico di default è per ora una faccenda dei soli vSOP militari: nessuno ha chiesto che una
        // vIPI ACC nasconda qualcosa a qualcuno. Se un giorno succede, questo test lo dice a chi legge — e
        // `ApplyCatalogAudienceDefaultsAsync` andrà riletto, perché guarda TUTTI i profili.
        foreach (var p in Enum.GetValues<SectionProfile>().Where(p => p != SectionProfile.AirportMil))
            Assert.All(Tutte(SectionCatalog.For(p)), d => Assert.Equal(SectionAudience.Both, d.Audience));
    }

    [Fact]
    public void Le_procedure_di_partenza_e_arrivo_stanno_SOLO_nel_militare()
    {
        // ⚠️ «operationaltechnique» è una chiave UNIVERSALE: sta in ACC, APP, vLOA e vIPI d'aeroporto. I
        // suoi quattro discendenti vivono nel solo registro militare perché `Children` è per profilo — se
        // un giorno finissero nel descrittore condiviso, quattro documenti si troverebbero delle procedure
        // di partenza che nessuno ha chiesto, e ci si accorgerebbe solo aprendoli.
        foreach (var p in Enum.GetValues<SectionProfile>().Where(p => p != SectionProfile.AirportMil))
        {
            var op = Tutte(SectionCatalog.For(p)).FirstOrDefault(d => d.Key == "operationaltechnique");
            if (op is not null) Assert.True(op.Children is null or { Count: 0 }, p.ToString());
        }

        var mil = Tutte(Mil).Single(d => d.Key == "operationaltechnique");
        Assert.Equal(2, mil.Children!.Count);
    }

    [Fact]
    public void Le_soglie_sono_una_sotto_sezione_di_Piste_resa_dalla_PAGINA()
    {
        // Erano la SECONDA TABELLA dentro «Piste» (`MilRunwayThresholds`, montata dal `case "runways"`);
        // dal 6 settembre 2026 sono una sezione, come le vuole il SOD. Stesso dato, stesso componente.
        var piste = Tutte(Mil).Single(d => d.Key == "runways");
        Assert.Equal(new[] { SectionKeys.RunwayThresholds }, piste.Children!.Select(d => d.Key));
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, SectionKeys.RunwayThresholds));
    }

    [Fact]
    public void Le_soglie_NON_dichiarano_una_derivazione_propria()
    {
        // ⚠️ Il test che protegge la decisione meno ovvia della carta. La tabella è palesemente derivata —
        // ThresholdLat/Lon arrivano da IVAO — eppure la chiave è EDITORIALE, perché la derivazione è quella
        // di «runways» e la release la congela lì sotto (`frozen.Get<AirportRunwaysView>("runways")`).
        // Dichiararla `Derived` darebbe DUE interruttori Live/Frozen sulla stessa tabella, che possono
        // contraddirsi: la stessa pista fotografata a due cicli diversi, una sotto l'altra.
        Assert.Equal(SectionKind.Editorial, SectionCatalog.KindOf(SectionKeys.RunwayThresholds));
        Assert.False(SectionCatalog.IsRenderModeToggleable(SectionKeys.RunwayThresholds));
        Assert.Equal(SectionKind.Derived, SectionCatalog.KindOf("runways"));
    }
}
