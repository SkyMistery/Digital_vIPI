using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Come il viewer legge un documento d'aeroporto scritto PRIMA della carta 2026-08-26.
///
/// <para>⚠️ Il caso che conta è lo <b>snapshot di release</b>: quello non si riscrive mai (doc 13 §9), quindi la
/// riconciliazione d'avvio non lo tocca — e la pagina pubblica di ogni aeroporto non ancora ripubblicato è
/// esattamente quello. Senza questa riconciliazione a view-time perdeva le tabelle vere e il <b>meteo</b>, che
/// prima della carta la pagina disegnava <i>fuori</i> dal documento e quindi c'era sempre.</para>
/// </summary>
public class AirportLegacySectionsTests
{
    private static SectionView Sez(string id, string key, string titolo, params string[] corpi) => new()
    {
        Id = id, Title = titolo, Depth = 0, SectionKey = key,
        Blocks = corpi.Select((b, i) => new BlockView
        {
            Id = i + 1, Format = Vipi.Domain.BlockFormat.Prose, State = RenderState.Expanded, Body = b,
        }).ToList(),
        Children = Array.Empty<SectionView>(),
    };

    /// <summary>Uno snapshot com'è davvero in archivio: chiavi casuali e titoli inglesi, con le tabelle dentro.</summary>
    private static IReadOnlyList<SectionView> SnapshotCotto() => new[]
    {
        Sez("s-462", SectionKeys.NewCustom(), "Transition levels", "tabella TL cotta"),
        Sez("s-463", "frequencies", "Frequencies", "tabella frequenze cotta"),
        Sez("s-464", SectionKeys.NewCustom(), "Runways", "tabella piste cotta"),
        Sez("s-465", "sids", "SID"),
        Sez("s-466", AirportLegacySections.ExtraKey, "Remarks", "attenzione al raccordo B"),
    };

    [Fact]
    public void Le_sezioni_cotte_tornano_alle_chiavi_di_catalogo()
    {
        var v = AirportLegacySections.ForView(SnapshotCotto());

        // ⚠️ In coda c'è anche «validity», che lo snapshot non aveva: è sempre-live come il meteo, e la regola
        // vale per tutte e due. Va al suo posto di catalogo — che è l'ultimo.
        Assert.Equal(
            new[] { "weather", "transition", "frequencies", "runways", "sids", AirportLegacySections.ExtraKey, "validity" },
            v.Select(s => s.SectionKey));
        // ...e ai titoli di catalogo: chi legge non deve trovarsi mezzo documento in inglese.
        Assert.Equal("Quote di transizione", v.Single(s => s.SectionKey == "transition").Title);
        Assert.Equal("Frequenze", v.Single(s => s.SectionKey == "frequencies").Title);
        Assert.Equal("Piste", v.Single(s => s.SectionKey == "runways").Title);
    }

    [Fact]
    public void Anche_la_validita_si_aggiunge_se_manca()
    {
        // «Validità e revisione» è sempre-live come il meteo: il suo timbro parla della release che si sta
        // mostrando, quindi non è mai parte della verità di uno snapshot e non ha senso che manchi.
        var v = AirportLegacySections.ForView(SnapshotCotto());

        var val = Assert.Single(v, s => s.SectionKey == "validity");
        Assert.Equal("Validità e revisione", val.Title);
        Assert.Empty(val.Blocks);
    }

    [Fact]
    public void Il_meteo_si_aggiunge_se_manca_e_va_al_suo_posto()
    {
        // ⚠️ È il difetto vero: nello snapshot vecchio la sezione meteo non esiste, perché prima della carta il
        // riquadro METAR/TAF lo disegnava la pagina fuori dal documento. Senza questa riga sparisce dal pubblico.
        var v = AirportLegacySections.ForView(SnapshotCotto());

        var meteo = Assert.Single(v, s => s.SectionKey == "weather");
        Assert.Equal(0, v.ToList().IndexOf(meteo));      // primo, come lo vuole il catalogo e come stava prima
        Assert.Empty(meteo.Blocks);
        Assert.Equal("s-weather", meteo.Id);             // ancora stabile: gli id veri sono «s-{numero}»
    }

    [Fact]
    public void Le_sezioni_cotte_perdono_i_blocchi_perche_il_corpo_lo_produce_la_pagina()
    {
        // Lasciarglieli dentro vorrebbe dire la tabella DUE volte: quella cotta e quella derivata.
        var v = AirportLegacySections.ForView(SnapshotCotto());

        Assert.Empty(v.Single(s => s.SectionKey == "transition").Blocks);
        Assert.Empty(v.Single(s => s.SectionKey == "runways").Blocks);
        // La sezione LIBERA invece li tiene tutti: quello è contenuto editoriale, e nessuno lo rideriva.
        Assert.Single(v.Single(s => s.SectionKey == AirportLegacySections.ExtraKey).Blocks);
    }

    [Fact]
    public void Un_documento_gia_nuovo_non_viene_toccato()
    {
        var nuovo = SectionCatalog.For(SectionProfile.Airport)
            .OrderBy(d => d.Order)
            .Select((d, i) => Sez($"s-{100 + i}", d.Key, d.Title))
            .ToList();

        var v = AirportLegacySections.ForView(nuovo);

        Assert.Equal(nuovo.Select(s => s.SectionKey), v.Select(s => s.SectionKey));
        Assert.Equal(nuovo.Select(s => s.Id), v.Select(s => s.Id));   // nessun meteo sintetico in più
    }

    [Fact]
    public void Runways_e_Runway_rules_non_si_confondono()
    {
        // Il confronto per sottostringa le scambierebbe, e quale vince dipenderebbe dall'ordine di iterazione.
        var v = AirportLegacySections.ForView(new[]
        {
            Sez("s-1", SectionKeys.NewCustom(), "Runways"),
            Sez("s-2", SectionKeys.NewCustom(), "Runway rules"),
        });

        Assert.Equal("runways", v.Single(s => s.Id == "s-1").SectionKey);
        Assert.Equal("runwayrules", v.Single(s => s.Id == "s-2").SectionKey);
    }

    [Fact]
    public void Una_sola_sezione_per_chiave()
    {
        // Alcuni archivi hanno release ripetute con la stessa sezione due volte: la seconda resta libera, o si
        // vedrebbero due «Piste» che rendono la stessa tabella derivata.
        var v = AirportLegacySections.ForView(new[]
        {
            Sez("s-1", SectionKeys.NewCustom(), "Runways"),
            Sez("s-2", SectionKeys.NewCustom(), "Piste"),
        });

        Assert.Equal("runways", v.Single(s => s.Id == "s-1").SectionKey);
        Assert.True(SectionKeys.IsCustom(v.Single(s => s.Id == "s-2").SectionKey));
    }

    [Fact]
    public void Una_sezione_libera_vera_non_diventa_una_sezione_di_catalogo()
    {
        // Le sezioni libere nascono dal trasloco degli extra e dall'editor: se una si chiamasse «Piste» non è
        // affar nostro... ma il titolo è l'unico appiglio che uno snapshot cotto lascia, quindi il rischio si
        // accetta e si documenta. Qui si presidia il caso opposto: chiave di catalogo ⇒ non si tocca.
        var v = AirportLegacySections.ForView(new[] { Sez("s-1", "operationaltechnique", "Procedure generali", "testo") });

        var s = Assert.Single(v, x => x.Id == "s-1");
        Assert.Equal("operationaltechnique", s.SectionKey);
        Assert.Single(s.Blocks);   // editoriale: i blocchi restano
    }

    [Fact]
    public void Il_titolo_di_catalogo_SCAVALCA_quello_della_sezione_anche_se_tradotto()
    {
        // ⚠️ CARATTERIZZAZIONE, non desiderio: questo metodo riporta ogni sezione di catalogo al titolo
        // CABLATO del catalogo, che è in italiano. È voluto — una sezione fissa non si rinomina a mano — ma
        // ha una conseguenza che si vede solo a schermo: se il documento è già stato TRADOTTO, il titolo
        // tradotto viene buttato via qui, e la pagina torna a dire «Regole piste» in mezzo alla prosa
        // inglese (visto su LIBC il 28 agosto 2026, con la copertura che dichiarava «tutto tradotto»).
        //
        // Per questo il viewer d'aeroporto ripassa le sezioni dalla traduzione DOPO questa chiamata. Se un
        // giorno quel secondo giro sembrasse di troppo, è questo test a dire perché c'è.
        var v = AirportLegacySections.ForView(new[] { Sez("s-1", "runwayrules", "Runway rules", "corpo") });

        var s = Assert.Single(v, x => x.Id == "s-1");
        Assert.Equal("Regole piste", s.Title);
    }

    [Fact]
    public void Una_lista_vuota_da_comunque_le_sezioni_sempre_live()
    {
        // Documento senza sezioni (o snapshot rotto): meteo e validità sono live, non hanno bisogno di niente
        // per esistere — l'uno viene dal NOAA, l'altro dalla release.
        var v = AirportLegacySections.ForView(Array.Empty<SectionView>());
        Assert.Equal(new[] { "weather", "validity" }, v.Select(s => s.SectionKey));
    }
}
