namespace Vipi.Application.Content;

/// <summary>
/// I titoli delle sezioni di CATALOGO nella lingua in cui si sta leggendo il documento.
///
/// <para>
/// ⚠️ <b>Il titolo di una sezione di catalogo non è prosa dell'autore: è una stringa del prodotto.</b> Sta
/// scritto nel documento — <c>DocumentSection.Title</c>, seminato alla nascita da
/// <see cref="SectionDescriptor.TitleIn"/> — ma nella lingua che il documento aveva <b>in quel momento</b>,
/// e da lì non si muove più: <c>SetLanguageAsync</c> cambia la lingua del documento e non tocca i titoli, e
/// l'editor non può rimediare perché una sezione fissa <b>non si rinomina a mano</b>
/// (<c>DocumentSectionsEditor</c>: il campo di rinomina esiste solo per le sezioni libere).
/// </para>
///
/// <para>
/// ⚠️ <b>Fino al 1 settembre 2026 la stampella era il traduttore</b>, e nessuno se ne era accorto: i titoli
/// SONO segmenti del documento (<c>DocumentTranslator.SegmentiSezione</c> parte proprio da lì), quindi un
/// lettore inglese di un documento italiano li otteneva dalla memoria di traduzione. Poi è arrivata la
/// <b>lingua bloccata</b>: bloccare spegne la traduzione — sorgente e bersaglio coincidono, la passata esce
/// a <c>TranslationPass.Nessuna</c> — e con la traduzione è caduta anche la stampella. Un vSOP dichiarato
/// inglese e bloccato mostrava «Procedure generali», «Dati generali», «Piste» dentro un documento inglese.
/// </para>
///
/// <para>
/// ⚠️ <b>Perché a view-time e non riscrivendo il DB.</b> Riscrivere i titoli quando cambia la lingua del
/// documento sistemerebbe la bozza di lavoro e nient'altro: le release <b>già pubblicate</b> portano i loro
/// titoli dentro lo snapshot, e quelle non si toccano (doc 13 §9). Il lettore le vedrebbe italiane fino alla
/// ripubblicazione successiva, cioè per un ciclo AIRAC intero. Risolvendo qui, dove si legge, valgono da
/// subito — come il blocco stesso, che è una regola di servizio.
/// </para>
///
/// <para>
/// ⚠️ <b>Il catalogo VINCE anche sulla memoria di traduzione</b>, ed è voluto: «MRVA» resta «MRVA» e
/// «Minime di vettoramento» non diventa «Minimum vectoring», che è quel che il motore rispondeva. Il titolo
/// di catalogo è la resa <b>decisa</b> (<c>docs/design/regole-lingua.md</c>), la memoria è la resa
/// plausibile. Per questo la passata si applica prima e questa dopo.
/// </para>
///
/// <para>
/// Le sezioni <b>libere</b> non si toccano mai: quelle il titolo se lo scrive lo staff, ed è prosa — la
/// traduce il traduttore, come il resto del documento.
/// </para>
/// </summary>
public static class TitoliDiCatalogo
{
    /// <summary>
    /// Le stesse sezioni, con i titoli di catalogo (a qualunque profondità) nella lingua di lettura.
    /// <para>Se non cambia niente torna la lista di partenza, senza allocare: è il caso normale — un
    /// documento letto nella lingua in cui è scritto.</para>
    /// </summary>
    /// <param name="sezioni">Le sezioni da rendere, già tradotte se c'era da tradurre.</param>
    /// <param name="profilo">Il profilo di catalogo del documento: dice quali chiavi sono di catalogo.</param>
    /// <param name="lingua">La lingua di lettura («it»/«en»), cioè quella che ha deciso la pagina.</param>
    public static IReadOnlyList<SectionView> Applica(
        IReadOnlyList<SectionView>? sezioni, SectionProfile profilo, string? lingua)
    {
        if (sezioni is null) return Array.Empty<SectionView>();
        if (sezioni.Count == 0) return sezioni;

        List<SectionView>? riscritte = null;
        for (var i = 0; i < sezioni.Count; i++)
        {
            var s = sezioni[i];
            // ⚠️ Ricorsivo, e non è teoria: il vSOP militare ha VENTI sezioni di catalogo su ventisei dentro
            // quattro contenitori. Fermarsi al primo livello lascerebbe italiane proprio quelle.
            var figlie = Applica(s.Children, profilo, lingua);
            var titolo = Titolo(profilo, s.SectionKey, s.Title, lingua);

            if (ReferenceEquals(figlie, s.Children) && string.Equals(titolo, s.Title, StringComparison.Ordinal))
            {
                riscritte?.Add(s);
                continue;
            }

            if (riscritte is null)
            {
                riscritte = new List<SectionView>(sezioni.Count);
                for (var j = 0; j < i; j++) riscritte.Add(sezioni[j]);
            }

            // ⚠️ Si ricopia campo per campo perché SectionView è una classe con `init`: ogni campo non
            // ricopiato tornerebbe al suo default — e il default è sempre quello «buono», quindi la pagina
            // continuerebbe a rendersi e nessun test cadrebbe. È il guasto già pagato in DocumentTranslator.
            riscritte.Add(new SectionView
            {
                Id = s.Id,
                Title = titolo,
                Depth = s.Depth,
                SectionKey = s.SectionKey,
                IsHidden = s.IsHidden,
                BeforeParentBody = s.BeforeParentBody,
                Audience = s.Audience,
                LeadSentence = s.LeadSentence,
                Blocks = s.Blocks,
                Children = figlie,
            });
        }

        return riscritte ?? sezioni;
    }

    /// <summary>
    /// Il titolo di questa sezione nella lingua di lettura: quello di catalogo se la chiave è di catalogo e
    /// <b>il catalogo ha davvero una resa in quella lingua</b>, altrimenti quello che c'è.
    /// </summary>
    public static string Titolo(SectionProfile profilo, string? chiave, string titolo, string? lingua)
    {
        if (string.IsNullOrWhiteSpace(chiave) || SectionKeys.IsCustom(chiave)) return titolo;
        if (SectionCatalog.Find(profilo, chiave) is not { } desc) return titolo;

        var dal = Resa(desc, profilo, lingua);
        // ⚠️ Un titolo di catalogo VUOTO non è un titolo: le due sezioni di coordinamento della vLOA stanno
        // nel ChildRegistry con titolo «» perché il loro dipende dai codici della coppia, e lo compone la
        // pagina. Sovrascrivere qui vorrebbe dire cancellarlo.
        return string.IsNullOrWhiteSpace(dal) ? titolo : dal!;
    }

    /// <summary>
    /// La resa che il catalogo ha per questa lingua, o <c>null</c> se non ne ha nessuna.
    ///
    /// <para>
    /// ⚠️ <b>«Nessuna resa» non vuol dire «ripiega sull'altra lingua», qui.</b> È la differenza con
    /// <see cref="SectionDescriptor.TitleIn"/>, che ripiega apposta: quello semina un titolo dentro il
    /// documento, e un titolo nella lingua sbagliata si legge mentre uno vuoto no. Questo invece
    /// <b>scavalca</b> quel che il documento (e la traduzione) hanno già messo lì, e scavalcare con un
    /// ripiego è un peggioramento: su una vLOA — l'unico profilo con i titoli in inglese — imporrebbe
    /// «Purpose» a chi la legge in italiano, cancellando l'unica resa italiana che quel titolo può avere,
    /// cioè quella del traduttore.
    /// </para>
    ///
    /// <para>⚠️ Nell'altro verso il ripiego è invece giusto e sta in <see cref="SectionDescriptor.TitleIn"/>:
    /// su un profilo italiano <c>TitleEn</c> nullo significa <b>sigla</b> — «AOR», «SID», «MRVA» — cioè
    /// «uguale nelle due lingue», che è una resa a tutti gli effetti. Ed è quella che impedisce alla memoria
    /// di rendere «MRVA» come «Minimum vectoring», che è successo davvero.</para>
    /// </summary>
    private static string? Resa(SectionDescriptor desc, SectionProfile profilo, string? lingua)
    {
        var inglese = lingua is not null && lingua.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return SectionCatalog.TitoliInInglese(profilo)
            ? (inglese ? desc.Title : null)
            : (inglese ? desc.TitleIn("en") : desc.Title);
    }

    /// <summary>
    /// Gli stessi blocchi della vIPI ACC, con i titoli di catalogo delle loro sezioni nella lingua di
    /// lettura. <b>Sul posto</b>, come fa <c>AccVipiTranslator</c> e per la stessa ragione: un
    /// <c>AccBlock</c> porta quindici campi che con la lingua non c'entrano niente.
    /// <para>⚠️ Il profilo è per BLOCCO — Aerovia e gruppo-APP hanno cataloghi diversi («Separazioni radar»
    /// contro «Separazioni») — e lo dice il blocco stesso, non la pagina.</para>
    /// </summary>
    public static void Applica(AccVipiData data, string? lingua)
    {
        foreach (var blocco in data.Blocks)
        {
            var profilo = SectionCatalog.ProfileOfAccBlock(blocco.Kind);
            blocco.Sections = blocco.Sections
                .Select(s => s with
                {
                    Title = Titolo(profilo, s.Key, s.Title, lingua),
                    // La parte editoriale porta le SOTTO-sezioni, che possono essere di catalogo a loro volta.
                    Editorial = s.Editorial is null
                        ? null
                        : Rinomina(s.Editorial, profilo, lingua),
                })
                .ToList();
        }
    }

    /// <summary>La sezione editoriale con le sue figlie risolte: il titolo della sezione stessa lo porta già
    /// <see cref="AccBlockSection.Title"/>, che è quello che la pagina mostra.</summary>
    private static SectionView Rinomina(SectionView sezione, SectionProfile profilo, string? lingua)
    {
        var figlie = Applica(sezione.Children, profilo, lingua);
        if (ReferenceEquals(figlie, sezione.Children)) return sezione;

        return new SectionView
        {
            Id = sezione.Id,
            Title = sezione.Title,
            Depth = sezione.Depth,
            SectionKey = sezione.SectionKey,
            IsHidden = sezione.IsHidden,
            BeforeParentBody = sezione.BeforeParentBody,
            Audience = sezione.Audience,
            LeadSentence = sezione.LeadSentence,
            Blocks = sezione.Blocks,
            Children = figlie,
        };
    }
}
