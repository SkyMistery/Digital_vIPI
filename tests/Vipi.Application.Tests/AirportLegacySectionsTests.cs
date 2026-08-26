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

        Assert.Equal(
            new[] { "weather", "transition", "frequencies", "runways", "sids", AirportLegacySections.ExtraKey },
            v.Select(s => s.SectionKey));
        // ...e ai titoli di catalogo: chi legge non deve trovarsi mezzo documento in inglese.
        Assert.Equal("Quote di transizione", v.Single(s => s.SectionKey == "transition").Title);
        Assert.Equal("Frequenze", v.Single(s => s.SectionKey == "frequencies").Title);
        Assert.Equal("Piste", v.Single(s => s.SectionKey == "runways").Title);
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
    public void Una_lista_vuota_da_comunque_il_meteo()
    {
        // Documento senza sezioni (o snapshot rotto): il meteo è live, non ha bisogno di niente per esistere.
        var v = AirportLegacySections.ForView(Array.Empty<SectionView>());
        Assert.Equal("weather", Assert.Single(v).SectionKey);
    }
}
