using Vipi.Application.Content;

namespace Vipi.Application.Tests;

/// <summary>
/// Ogni titolo di catalogo esiste in tutte e due le lingue (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §4, strato 4).
///
/// <para>
/// ⚠️ <b>La guardia è STRUTTURALE, e non indovina la lingua.</b> È la lezione già pagata con i messaggi a
/// chi modifica: una scansione «a parole italiane» ne aveva mancati quattro — «Intervallo QNH invertito
/// (From &gt; To).» non ha accenti né parole funzione. Qui si pretende una <b>decisione esplicita</b> su
/// ogni voce: o una resa inglese, o l'ammissione, scritta qui sotto, che quel titolo è una sigla e resta
/// uguale.
/// </para>
///
/// <para>
/// ⚠️ E serve perché un titolo di catalogo <b>non passa mai dal traduttore automatico</b>: non è un segmento
/// del documento, quindi non entra nel corpus e la passata non lo conosce. Chi ne aggiunge uno domani e si
/// dimentica l'inglese non rompe niente — lascia solo una testata italiana in mezzo a un documento inglese,
/// che è la classe di difetto che nessuno segnala.
/// </para>
/// </summary>
public class CatalogoBilingueTests
{
    /// <summary>
    /// I titoli che restano uguali nelle due lingue perché sono <b>sigle</b>: si dicono così in frequenza e
    /// sulle carte, e tradurle sarebbe un errore, non una gentilezza. È la decisione del committente
    /// (<c>docs/design/regole-lingua.md</c>) — «Minime di vettoramento» reso «Minimum vectoring» è
    /// esattamente ciò che ha fatto rinominare quella sezione in «MRVA».
    /// </summary>
    private static readonly HashSet<string> Sigle = new(StringComparer.Ordinal)
    {
        "AOR", "MRVA", "VFR", "SID", "STAR", "METAR & TAF", "QRA / Scramble",
    };

    /// <summary>
    /// I profili i cui titoli sono scritti in italiano. La vLOA no: nasce in inglese, ed è una lettera
    /// d'accordo bilaterale — i suoi titoli sono già la versione inglese.
    ///
    /// <para>⚠️ <b>L'elenco lo dice il CATALOGO</b> (<see cref="SectionCatalog.TitoliInInglese"/>), non
    /// questa riga: stava scritto a mano qui e a quel punto un profilo nuovo restava fuori dalla guardia in
    /// silenzio — il modo esatto in cui la guardia smette di guardare. Da qui esce anche la regola che i
    /// titoli si risolvono a view-time (<c>TitoliDiCatalogo</c>), quindi i due posti devono dire la stessa
    /// cosa per costruzione.</para>
    /// </summary>
    public static TheoryData<SectionProfile> ProfiliItaliani =>
        new(Enum.GetValues<SectionProfile>().Where(p => !SectionCatalog.TitoliInInglese(p)));

    [Theory]
    [MemberData(nameof(ProfiliItaliani))]
    public void Ogni_titolo_italiano_ha_una_resa_inglese_o_e_una_sigla(SectionProfile profilo)
    {
        var senza = Tutti(SectionCatalog.For(profilo))
            .Where(d => string.IsNullOrWhiteSpace(d.TitleEn) && !Sigle.Contains(d.Title))
            .Select(d => $"{d.Key} = «{d.Title}»")
            .ToList();

        Assert.True(senza.Count == 0,
            $"Sezioni senza titolo inglese nel profilo {profilo}: {string.Join(", ", senza)}. "
            + "Scrivi `en:` sul descrittore, oppure aggiungi il titolo a `Sigle` se davvero non si traduce.");
    }

    [Theory]
    [MemberData(nameof(ProfiliItaliani))]
    public void In_inglese_il_titolo_cambia_davvero(SectionProfile profilo)
    {
        // Il contrario del test di sopra, e non è la stessa cosa detta due volte: quello pretende che il
        // campo ci sia, questo che venga USATO. Un `TitleIn` che ignorasse la lingua passerebbe il primo.
        foreach (var d in Tutti(SectionCatalog.For(profilo)).Where(d => !string.IsNullOrWhiteSpace(d.TitleEn)))
        {
            Assert.Equal(d.TitleEn, d.TitleIn("en"));
            Assert.Equal(d.Title, d.TitleIn("it"));
        }
    }

    [Fact]
    public void Senza_resa_inglese_si_ricade_sull_italiano_e_non_sul_vuoto()
    {
        // Una sigla chiesta in inglese deve tornare la sigla, non la stringa vuota: un titolo nella lingua
        // sbagliata si legge, un titolo vuoto no.
        var aor = SectionCatalog.Find(SectionProfile.App, "aor");
        Assert.NotNull(aor);
        Assert.Equal("AOR", aor!.TitleIn("en"));
        Assert.Equal("AOR", aor.TitleIn(null));
    }

    [Fact]
    public void La_sezione_delle_minime_resta_MRVA_in_tutte_e_due_le_lingue()
    {
        // Regressione con un nome e una data: il motore rendeva «Minime di vettoramento» come «Minimum
        // vectoring» — giusto a metà, e comunque non la sigla con cui la si chiama in frequenza.
        var minima = SectionCatalog.Find(SectionProfile.AccAerovia, "minima");
        Assert.NotNull(minima);
        Assert.Equal("MRVA", minima!.TitleIn("it"));
        Assert.Equal("MRVA", minima.TitleIn("en"));
    }

    private static IEnumerable<SectionDescriptor> Tutti(IEnumerable<SectionDescriptor> descrittori)
    {
        // ⚠️ Anche i FIGLI: il vSOP militare ha venti sezioni su ventisei annidate, e una guardia che
        // guardasse il solo primo livello direbbe «tutto a posto» avendone viste sei.
        foreach (var d in descrittori)
        {
            yield return d;
            foreach (var f in Tutti(d.Children ?? Array.Empty<SectionDescriptor>()))
                yield return f;
        }
    }
}
