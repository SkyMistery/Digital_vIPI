using System.Text.Json;

namespace Vipi.Application.Content;

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
/// <param name="Title">
/// Quel che si legge nel link, <b>scritto nel blocco</b> e non ripreso dalla biblioteca a ogni resa.
/// <para>⚠️ È una scelta, non una copia dimenticata: il titolo è una <b>decisione editoriale del documento</b>
/// — «la LoA con Marsiglia» dentro una frase, «LoA LIRR↔LFMM (rev. 3)» in una tabella — mentre in biblioteca
/// la voce ha un nome solo. Andarlo a prendere di là vorrebbe dire che rinominare una voce riscrive il testo
/// di ogni documento che la cita, compresi quelli pubblicati.</para>
/// <para>A cambiare sotto è il <b>file</b>, non il nome: quella è la sostituzione, ed è voluta.</para>
/// </param>
public sealed record AttachmentRef(string Slug, string? Title = null)
{
    /// <summary>Il token come si scrive nel JSON e nella prosa: un formato solo per tutte e due le forme.</summary>
    public string Token => AttachmentRules.TokenDi(Slug);

    /// <summary>La rotta a cui mandare chi clicca. Sempre la nostra, mai quella del deposito.</summary>
    public string Url => AttachmentRules.UrlDi(Slug);

    private sealed class Dto
    {
        public string? @ref { get; set; }
        public string? titolo { get; set; }
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

            return new AttachmentRef(slug, string.IsNullOrWhiteSpace(dto!.titolo) ? null : dto.titolo!.Trim());
        }
        catch (JsonException)
        {
            // Un blocco allegato col JSON rotto si comporta come un blocco senza allegato: si vede il
            // segnaposto nell'editor, non un'eccezione in mezzo a un documento.
            return null;
        }
    }

    public static string Serialize(AttachmentRef riferimento) =>
        JsonSerializer.Serialize(new Dto { @ref = riferimento.Token, titolo = riferimento.Title });

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
