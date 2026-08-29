using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Che cosa si può scrivere in una voce di biblioteca, e come si normalizza quel che si scrive.
/// Regole <b>pure</b>: le usano il servizio (in scrittura) e la pagina (per dirlo prima di provarci), e
/// devono essere le stesse — una pagina più permissiva del servizio produce un campo rosso dopo il salvataggio.
/// </summary>
public static class AttachmentRules
{
    /// <summary>
    /// Uno slug: minuscole, cifre e trattini singoli, da 2 a 64 caratteri.
    ///
    /// <para>⚠️ Non è una scelta estetica. Lo slug si <b>batte a mano dentro la prosa</b>
    /// (<c>[LoA Marseille](allegato:loa-lirr-lfmm)</c>): maiuscole, spazi e accenti lo renderebbero un
    /// riferimento che a volte si scrive giusto e a volte no, e il link muto non dice perché.</para>
    /// </summary>
    private static readonly Regex RxSlug = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>La chiave di perimetro: un codice ACC o un ICAO d'aeroporto, maiuscolo.</summary>
    private static readonly Regex RxScopeKey = new(@"^[A-Z0-9]{2,8}$", RegexOptions.Compiled);

    /// <summary>
    /// L'id di un file presso il deposito. Charset dei file id di Drive (lettere, cifre, <c>-</c> e <c>_</c>);
    /// la lunghezza vera è 33, ma il minimo è tenuto basso perché Google l'ha già cambiata una volta.
    /// </summary>
    private static readonly Regex RxExternalId = new(@"^[A-Za-z0-9_-]{10,200}$", RegexOptions.Compiled);

    /// <summary>Lunghezza massima dello slug: la stessa della colonna.</summary>
    public const int SlugMaxLength = 64;

    public const int TitleMaxLength = 200;

    public static string Norm(string? v) => (v ?? "").Trim();

    /// <summary>Vero se lo slug ha la forma giusta. ⚠️ Non dice se è <b>libero</b>: quello lo sa solo il DB.</summary>
    public static bool SlugValido(string? slug)
    {
        var s = Norm(slug);
        return s.Length is >= 2 and <= SlugMaxLength && RxSlug.IsMatch(s);
    }

    /// <summary>
    /// Lo slug proposto a partire dal titolo, quando chi carica non ne scrive uno: «LoA Roma–Marseille» →
    /// <c>loa-roma-marseille</c>.
    ///
    /// <para>⚠️ Gli accenti si <b>traslitterano</b>, non si buttano: «Forlì» deve dare <c>forli</c> e non
    /// <c>forl</c>, che è un nome che non si riconosce più. Il trattino lungo, la sbarra e i due punti
    /// diventano un separatore, non spariscono attaccando due parole.</para>
    ///
    /// <para>La proposta è solo una proposta: resta modificabile, perché lo slug è <b>definitivo</b> — una
    /// volta citato in un documento non si cambia più.</para>
    /// </summary>
    public static string SlugDa(string? titolo)
    {
        var decomposto = Norm(titolo).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
        {
            // I segni diacritici cadono: è la traslitterazione, `à` è già diventata `a` + accento.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsAsciiLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else sb.Append('-');   // qualunque altra cosa separa: spazi, trattini lunghi, sbarre, punti
        }

        // I separatori consecutivi diventano uno solo, e ai bordi non ne resta nessuno.
        var pezzi = sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries);
        var slug = string.Join('-', pezzi);
        return slug.Length > SlugMaxLength ? slug[..SlugMaxLength].TrimEnd('-') : slug;
    }

    /// <summary>
    /// La chiave di perimetro normalizzata, o <c>null</c>.
    /// <para>⚠️ Per <see cref="AttachmentScope.Division"/> il risultato è <b>sempre</b> <c>null</c>, anche se
    /// qualcuno ha battuto qualcosa: una chiave su un perimetro che non ne ha una è un filtro che un giorno
    /// non trova più la riga.</para>
    /// </summary>
    public static string? ScopeKeyNorm(AttachmentScope scope, string? chiave)
    {
        if (scope == AttachmentScope.Division) return null;
        var k = Norm(chiave).ToUpperInvariant();
        return k.Length == 0 ? null : k;
    }

    /// <summary>Vero se la coppia perimetro/chiave sta in piedi: la divisione non vuole chiave, gli altri due
    /// la pretendono e nella forma di un codice.</summary>
    public static bool ScopeValido(AttachmentScope scope, string? chiave)
    {
        var k = ScopeKeyNorm(scope, chiave);
        return scope == AttachmentScope.Division ? k is null : k is not null && RxScopeKey.IsMatch(k);
    }

    /// <summary>
    /// Estrae l'id del file dal link che lo staffista incolla — o dall'id nudo, se ha incollato quello.
    /// Torna <c>null</c> se non c'è niente che somigli a un id.
    ///
    /// <para>Le forme che Drive produce davvero, tutte accettate:</para>
    /// <list type="bullet">
    /// <item><c>https://drive.google.com/file/d/&lt;ID&gt;/view?usp=sharing</c> — quella del tasto «Condividi»</item>
    /// <item><c>https://drive.google.com/open?id=&lt;ID&gt;</c> e <c>uc?id=&lt;ID&gt;&amp;export=download</c></item>
    /// <item><c>https://docs.google.com/document/d/&lt;ID&gt;/edit</c> — un documento nativo, non un PDF</item>
    /// </list>
    ///
    /// <para>⚠️ Si tiene <b>l'id, non l'URL</b>, e non è pignoleria: l'URL porta con sé la forma di oggi
    /// (<c>/view</c>, <c>?usp=sharing</c>) e il nome del deposito. L'id è il dato, il resto è come Google lo
    /// impacchetta questo mese — ed è il redirect nostro a ricostruire l'indirizzo, in un posto solo.</para>
    /// </summary>
    public static string? ExternalIdDa(string? linkOId)
    {
        var v = Norm(linkOId);
        if (v.Length == 0) return null;

        // L'id nudo: nessuno dei caratteri di un URL sta nel charset, quindi non c'è ambiguità.
        if (RxExternalId.IsMatch(v)) return v;

        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        // .../d/<ID>/... — la forma del tasto «Condividi», e anche quella dei documenti nativi.
        var segmenti = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segmenti.Length - 1; i++)
            if (segmenti[i] == "d" && RxExternalId.IsMatch(segmenti[i + 1]))
                return segmenti[i + 1];

        // ...?id=<ID> — le forme vecchie, che i link salvati anni fa hanno ancora.
        foreach (var coppia in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = coppia.IndexOf('=');
            if (eq <= 0) continue;
            if (!string.Equals(coppia[..eq], "id", StringComparison.OrdinalIgnoreCase)) continue;
            var valore = Uri.UnescapeDataString(coppia[(eq + 1)..]);
            if (RxExternalId.IsMatch(valore)) return valore;
        }

        return null;
    }

    /// <summary>
    /// L'indirizzo a cui mandare chi apre <c>/vsop/files/{slug}</c>: la <b>preview</b>, non il download.
    ///
    /// <para>⚠️ È l'unico posto dove sta scritto l'indirizzo del deposito, ed è anche l'unica forma che
    /// funziona <b>dentro un iframe</b> — cioè quella su cui poggia il modo «incorporato» del blocco.</para>
    ///
    /// <para>Il parametro <paramref name="provider"/> c'è pur essendo oggi un valore solo: il giorno che il
    /// deposito cambia, questa firma non cambia e il posto da toccare è uno.</para>
    /// </summary>
    public static string UrlEsterno(AttachmentProvider provider, string externalId)
    {
        _ = provider;   // un deposito solo, oggi: vedi AttachmentProvider
        return $"https://drive.google.com/file/d/{externalId}/preview";
    }
}
