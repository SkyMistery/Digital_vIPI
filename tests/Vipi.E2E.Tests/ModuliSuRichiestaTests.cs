using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// I quattro moduli pesanti — mappe AoR, carte delle minime, viewer 3D, tour — non stanno nel markup di
/// ogni pagina: li carica <c>vipi-boot.js</c> quando la pagina mostra qualcosa su cui possano lavorare.
///
/// <para><b>Quanto pesavano.</b> Misurato il 27 agosto 2026: 13 029 byte compressi su OGNI pagina —
/// ricerca, incarichi, elenchi, guida, login, hub — per servire le sole schermate con una mappa o uno
/// stage 3D.</para>
///
/// <para>⚠️ Questi test guardano l'HTML servito, che è metà della verifica: l'altra metà — che dove
/// servono arrivino DAVVERO, anche dopo una navigazione «enhanced» — la fa un browser, ed è
/// <c>lazy-verifica.js</c> nella skill <c>verifica-live</c>. Nessun test qui dentro apre una pagina.</para>
/// </summary>
public sealed class ModuliSuRichiestaTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public ModuliSuRichiestaTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    private static readonly string[] SuRichiesta =
        { "vipi-aor.js", "vipi-mva.js", "vipi-aor3d.js", "vipi-tour.js" };

    /// <summary>Una pagina qualunque non deve tirarseli dietro.</summary>
    [Theory]
    [InlineData("/services/vsop")]
    [InlineData("/services/vsop/guide")]
    [InlineData("/services")]
    public async Task Nessuna_pagina_li_chiede_nel_markup(string percorso)
    {
        var html = await _factory.CreateClient().GetStringAsync(percorso);

        foreach (var modulo in SuRichiesta)
            Assert.DoesNotContain($"<script src=\"/_content/Vipi.Ui/{modulo}", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ma gli indirizzi devono esserci, sul tag di <c>vipi-boot.js</c>: sono l'unico modo che ha di
    /// trovarli. ⚠️ Se un giorno sparissero, il caricamento non darebbe errore — semplicemente le mappe
    /// non comparirebbero più, e la pagina sembrerebbe a posto.
    /// </summary>
    [Fact]
    public async Task Gli_indirizzi_dei_moduli_viaggiano_sul_tag_di_boot()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        foreach (var attributo in new[]
                 {
                     "data-aor-src", "data-mva-src", "data-aor3d-src", "data-tour-src",
                     "data-leaflet-src", "data-leaflet-css", "data-three-src",
                 })
            Assert.Contains(attributo, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'editor e le immagini restano caricati sempre, ed è una scelta: il codice C# li chiama PER NOME
    /// (<c>vipiSetDirty</c>, <c>vipiMedia.osserva</c>), e un modulo che arrivasse un istante dopo la
    /// chiamata sarebbe un guasto silenzioso. Valgono 1 653 byte compressi in due: non è un prezzo che
    /// valga quel rischio.
    /// </summary>
    [Fact]
    public async Task I_moduli_chiamati_dal_codice_restano_sempre_caricati()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        Assert.Contains("vipi-editor.js", html, StringComparison.Ordinal);
        Assert.Contains("vipi-media.js", html, StringComparison.Ordinal);
    }

    /// <summary>Il foglio dell'Aurora Profile Swapper vale per una rotta sola, e solo lì deve comparire.</summary>
    [Fact]
    public async Task Il_foglio_del_profile_swapper_sta_solo_sulla_sua_pagina()
    {
        var client = _factory.CreateClient();

        Assert.DoesNotContain("vipi-swapper.css", await client.GetStringAsync("/services/vsop"), StringComparison.Ordinal);
        Assert.Contains("vipi-swapper.css", await client.GetStringAsync("/services/profile-swapper"), StringComparison.Ordinal);
    }
}
