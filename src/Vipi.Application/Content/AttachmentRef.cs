using System.Text.Json;

namespace Vipi.Application.Content;

/// <summary>
/// Come si legge un allegato dentro un documento. La scelta è <b>per blocco</b>, non per voce di biblioteca:
/// la stessa LoA può stare incorporata nella vLOA che la riguarda e restare un semplice link in una vIPI che
/// la cita di sfuggita.
/// </summary>
public enum AttachmentDisplayMode
{
    /// <summary>
    /// Titolo, tipo, e il segnale che il clic porta fuori dal sito. È lo <b>zero</b> dell'enum, ed è voluto:
    /// ogni blocco già scritto nasce così, e il link è la forma che funziona ovunque — su un telefono, su
    /// carta, e il giorno che Google chiude l'incorporamento.
    /// </summary>
    Link,

    /// <summary>
    /// Il PDF si legge <b>dentro</b> la pagina, in un riquadro, <b>più il link sotto</b>. Per quando il
    /// documento <i>è</i> l'allegato: una LoA firmata dentro la sua vLOA.
    /// </summary>
    Embedded,
}

/// <summary>
/// Quanto è alto il riquadro del modo incorporato.
///
/// <para>⚠️ <b>Tre scaglioni e non un numero libero.</b> Un numero libero produce riquadri da 3000px, e non
/// se ne accorge nessuno finché non li apre un telefono: chi scrive misura sul proprio schermo, e quel che
/// scrive vale per tutti gli altri.</para>
/// </summary>
public enum AttachmentEmbedHeight
{
    /// <summary>Un'occhiata: sta dentro una sezione senza mangiarsela. Zero dell'enum.</summary>
    Small,

    Medium,

    /// <summary>Da leggere davvero, per quando l'allegato è il contenuto principale della sezione.</summary>
    Large,
}

/// <summary>
/// Come un blocco <c>Attachment</c> cita il suo allegato: <c>BodyJson</c> porta il <b>token</b> e il titolo
/// da mostrare, <c>Body</c> resta libero per una nota sotto il link (markdown, come la prosa).
///
/// <para>Questa classe è la <b>fonte unica</b> del formato: la usano i due editor, il viewer, la ricerca e
/// lo scanner dei riferimenti. Se il formato cambia, cambia qui e basta — è la stessa scelta di
/// <see cref="MediaRef"/>, e per la stessa ragione.</para>
///
/// <para>⚠️ <b>Nel blocco finisce il token, non l'URL.</b> Nemmeno quello nostro: se ci finisse
/// <c>/vsop/files/…</c>, spostare la rotta domani vorrebbe dire riscrivere il JSON di ogni blocco già
/// pubblicato. Il token dice <i>che cosa</i> si cita; <i>dove</i> lo si va a prendere lo decide
/// <see cref="AttachmentRules"/>, in un posto solo.</para>
///
/// <para>⚠️ E si cita lo <b>slug</b>, non l'id numerico della riga: uno slug si legge dentro il JSON e si
/// riconosce, un <c>7</c> no. È la stessa lezione dei puntatori nel JSON dei settori.</para>
/// </summary>
/// <param name="Slug">L'identità della voce di biblioteca. Senza il prefisso: quello lo mette il token.</param>
/// <param name="Mode">Link o incorporato. Vedi <see cref="AttachmentDisplayMode"/>.</param>
/// <param name="Height">Altezza del riquadro, se incorporato. Ignorata nel modo link.</param>
/// <param name="Title">
/// Quel che si legge nel link, <b>scritto nel blocco</b> e non ripreso dalla biblioteca a ogni resa.
/// <para>⚠️ È una scelta, non una copia dimenticata: il titolo è una <b>decisione editoriale del documento</b>
/// — «la LoA con Marsiglia» dentro una frase, «LoA LIRR↔LFMM (rev. 3)» in una tabella — mentre in biblioteca
/// la voce ha un nome solo. Andarlo a prendere di là vorrebbe dire che rinominare una voce riscrive il testo
/// di ogni documento che la cita, compresi quelli pubblicati.</para>
/// <para>A cambiare sotto è il <b>file</b>, non il nome: quella è la sostituzione, ed è voluta.</para>
/// </param>
public sealed record AttachmentRef(
    string Slug,
    string? Title = null,
    AttachmentDisplayMode Mode = AttachmentDisplayMode.Link,
    AttachmentEmbedHeight Height = AttachmentEmbedHeight.Medium)
{
    /// <summary>
    /// L'altezza in pixel del riquadro incorporato. ⚠️ Sta <b>qui</b> e non nel CSS: la sceglie l'editore fra
    /// tre valori, e un foglio di stile non può leggere una scelta salvata nel JSON di un blocco.
    /// </summary>
    public int HeightPx => Height switch
    {
        AttachmentEmbedHeight.Small => 320,
        AttachmentEmbedHeight.Large => 800,
        _ => 520,
    };

    /// <summary>Il token come si scrive nel JSON e nella prosa: un formato solo per tutte e due le forme.</summary>
    public string Token => AttachmentRules.TokenDi(Slug);

    /// <summary>La rotta a cui mandare chi clicca. Sempre la nostra, mai quella del deposito.</summary>
    public string Url => AttachmentRules.UrlDi(Slug);

    private sealed class Dto
    {
        public string? @ref { get; set; }
        public string? titolo { get; set; }

        /// <summary>⚠️ Il modo si scrive col NOME, non con l'ordinale: questo JSON lo legge anche un essere
        /// umano che apre il payload di una release per capire perché una pagina fa una cosa strana, e un
        /// <c>1</c> non gli dice niente. Vale anche al contrario — aggiungere un modo domani non deve
        /// reinterpretare i blocchi già scritti.</summary>
        public string? modo { get; set; }

        public string? altezza { get; set; }
    }

    /// <summary>
    /// Legge il riferimento dal JSON del blocco; <c>null</c> se manca, è illeggibile o non porta un token.
    ///
    /// <para>⚠️ Si accetta <b>solo</b> lo schema <c>allegato:</c>. Un <c>ref</c> che porta un URL qualunque
    /// non è un riferimento a un allegato: aprirlo qui vorrebbe dire far entrare un indirizzo arbitrario —
    /// <c>javascript:</c> compreso — dentro un <c>href</c> che poi costruiamo noi.</para>
    /// </summary>
    public static AttachmentRef? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(json);
            var token = dto?.@ref?.Trim();
            if (string.IsNullOrEmpty(token)) return null;
            if (!token.StartsWith(AttachmentRules.TokenPrefix, StringComparison.Ordinal)) return null;

            var slug = token[AttachmentRules.TokenPrefix.Length..];
            if (!AttachmentRules.SlugValido(slug)) return null;

            // Un modo o un'altezza che questa versione non conosce tornano al valore di riposo invece di far
            // esplodere il blocco: un documento scritto da un ramo più nuovo si legge lo stesso, in modo link.
            var modo = Enum.TryParse<AttachmentDisplayMode>(dto!.modo, ignoreCase: true, out var m)
                ? m : AttachmentDisplayMode.Link;
            var altezza = Enum.TryParse<AttachmentEmbedHeight>(dto.altezza, ignoreCase: true, out var h)
                ? h : AttachmentEmbedHeight.Medium;

            return new AttachmentRef(
                slug, string.IsNullOrWhiteSpace(dto.titolo) ? null : dto.titolo!.Trim(), modo, altezza);
        }
        catch (JsonException)
        {
            // Un blocco allegato col JSON rotto si comporta come un blocco senza allegato: si vede il
            // segnaposto nell'editor, non un'eccezione in mezzo a un documento.
            return null;
        }
    }

    public static string Serialize(AttachmentRef riferimento) =>
        JsonSerializer.Serialize(new Dto
        {
            @ref = riferimento.Token,
            titolo = riferimento.Title,
            modo = riferimento.Mode.ToString(),
            altezza = riferimento.Height.ToString(),
        });

    /// <summary>
    /// Testo indicizzabile e leggibile di un blocco allegato: il titolo e la nota, <b>mai</b> il JSON.
    /// <para>Cercare «Marseille» deve trovare la LoA; cercare una parte dello slug non deve far comparire una
    /// riga di JSON nel risultato di ricerca.</para>
    /// </summary>
    public static string TextOf(string? json, string? note)
    {
        var titolo = Parse(json)?.Title;
        return string.Join(" ", new[] { titolo, note }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
