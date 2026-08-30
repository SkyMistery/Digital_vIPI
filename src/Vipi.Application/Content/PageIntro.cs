using System.Text.Json;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Una sezione dell'intro di una pagina: un titolo e i suoi blocchi (carta
/// <c>docs/feature/2026-08-30-intro-di-pagina.md</c> §3).
///
/// <para>⚠️ I blocchi sono <see cref="ExtraBlock"/>, cioè <b>gli stessi</b> che l'editor condiviso
/// (<c>DocumentBlocksEditor</c>) sa già scrivere: prosa, callout, tabella, immagine, allegato. Un secondo
/// modello di blocco vorrebbe dire un secondo editor, e due editor che devono restare uguali divergono.</para>
/// </summary>
public sealed class PageIntroSection
{
    public string Title { get; set; } = "";
    public List<ExtraBlock> Blocks { get; set; } = new();
}

/// <summary>
/// L'intro di una pagina: <b>contenuto di contorno</b> in cima a un elenco, salvato in un
/// <see cref="SharedBlock"/> e reso con la stessa macchina dei documenti.
///
/// <para>
/// ⚠️ <b>Non è un documento, e non deve diventarlo.</b> Niente release, niente ciclo AIRAC, niente
/// congelamento: si pubblica salvando. Un <see cref="Document"/> senza aeroporto né settori cadrebbe fuori da
/// tutti i descrittori di <c>IReleaseTarget</c> e sarebbe irraggiungibile <i>in silenzio</i> — lo stesso
/// guasto già pagato col catch-all dell'aeroporto. Contenuto normativo qui non ci va.
/// </para>
/// </summary>
public static class PageIntro
{
    /// <summary>
    /// La lingua in cui l'intro è <b>scritta</b>: la divisione redige in italiano.
    ///
    /// <para>⚠️ <see cref="SharedBlock"/> non ha una colonna della lingua, quindi questa è una scelta
    /// dichiarata e non un dato letto. Sta in <b>un posto solo</b> apposta: un secondo chiamante che la
    /// riscrivesse a mano sarebbe un secondo posto che può contraddire il primo, e il lettore inglese
    /// vedrebbe la memoria mancare su ogni frase senza capire perché.</para>
    /// </summary>
    public const Language Sorgente = Language.It;

    /// <summary>Il prefisso di ogni chiave d'intro: è quello che permette al giro della traduzione di
    /// riconoscere le intro fra i blocchi condivisi senza sapere quali pagine ne hanno una.</summary>
    public const string Prefisso = "page-intro:";

    /// <summary>La chiave del <see cref="SharedBlock"/> che porta l'intro di una pagina.
    /// <para>Il prefisso non è decorativo: la seconda pagina che vorrà un'intro registra una <b>chiave</b>,
    /// non un secondo meccanismo.</para></summary>
    public static string Chiave(string pagina) => Prefisso + pagina.Trim().ToLowerInvariant();

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    private sealed class Envelope { public List<PageIntroSection> sections { get; set; } = new(); }

    /// <summary>
    /// Le sezioni salvate. Vuoto o JSON che non capiamo → <b>nessuna sezione</b>.
    /// <para>⚠️ Un corpo illeggibile non diventa una sezione di prosa col JSON dentro: qui, a differenza degli
    /// extra d'aeroporto, non c'è nessun testo markdown storico da recuperare — c'è solo il rischio di
    /// stampare del JSON in cima a una pagina pubblica.</para>
    /// </summary>
    public static List<PageIntroSection> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var env = JsonSerializer.Deserialize<Envelope>(json, Opts);
            if (env?.sections is not { } sezioni) return new();
            return sezioni.Where(s => s is not null).Select(Pulisci).Where(NonVuota).ToList();
        }
        catch (JsonException) { return new(); }
    }

    /// <summary>Le sezioni in JSON. Niente da salvare → <c>null</c>, che è «l'intro non c'è».</summary>
    public static string? Serialize(IReadOnlyList<PageIntroSection> sezioni)
    {
        var pulite = sezioni.Where(s => s is not null).Select(Pulisci).Where(NonVuota).ToList();
        return pulite.Count == 0 ? null : JsonSerializer.Serialize(new Envelope { sections = pulite }, Opts);
    }

    /// <summary>
    /// L'intro come documento da rendere: la <b>proiezione</b> che i componenti dei viewer sanno già
    /// disegnare, e che il traduttore sa già tradurre.
    ///
    /// <para>⚠️ Calcolata a ogni resa e <b>mai salvata</b>: salvata c'è solo la forma che lo staff scrive.
    /// Due copie della stessa cosa divergono, e quella sbagliata sarebbe quella che il pubblico legge.</para>
    /// </summary>
    /// <param name="titolo">Titolo della vista. Non si mostra: la zona non ha una testata propria.</param>
    public static DocumentView ToView(IReadOnlyList<PageIntroSection> sezioni, string titolo = "")
    {
        var n = 0;
        var viste = new List<SectionView>();
        foreach (var s in sezioni)
        {
            var indice = ++n;
            var blocchi = new List<BlockView>();
            foreach (var b in s.Blocks)
                if (ToBlockView(b, blocchi.Count + 1, indice) is { } bv) blocchi.Add(bv);

            viste.Add(new SectionView
            {
                // L'ancora è nostra e stabile per posizione: `pi-1`. Non è `s-{id}` perché qui un id di
                // sezione non esiste — non c'è nessuna riga di DocumentSections dietro.
                Id = $"pi-{indice}",
                Title = s.Title,
                Depth = 0,
                SectionKey = $"page-intro-{indice}",
                Blocks = blocchi,
                Children = Array.Empty<SectionView>(),
            });
        }

        return new DocumentView
        {
            Title = titolo,
            AiracCycle = "",   // l'intro non ha un ciclo: non si congela
            Sections = viste,
            Language = Sorgente,
        };
    }

    /// <summary>
    /// Un blocco salvato in un blocco da rendere. <c>null</c> = da scartare, con la <b>stessa regola</b> della
    /// cottura degli extra: prosa senza testo, immagine o allegato senza riferimento non entrano nel
    /// documento, e non devono entrare nemmeno qui.
    /// </summary>
    private static BlockView? ToBlockView(ExtraBlock b, int ordine, int sezione)
    {
        // ⚠️ L'id è unico DENTRO la vista e non identifica niente in archivio: i componenti lo usano come
        // chiave di resa. Due blocchi con lo stesso id in sezioni diverse farebbero riusare a Blazor il
        // nodo sbagliato.
        var id = sezione * 1000 + ordine;

        return b.Format switch
        {
            BlockFormat.Callout when Testo(b.Text) =>
                Vista(id, BlockFormat.Callout, b.Text, null, b.CalloutKind),
            BlockFormat.Table when Testo(b.TableJson) =>
                Vista(id, BlockFormat.Table, null, b.TableJson, null),
            BlockFormat.Image when MediaRef.Parse(b.ImageJson) is not null =>
                Vista(id, BlockFormat.Image, b.Text, b.ImageJson, null),
            BlockFormat.Attachment when AttachmentRef.Parse(b.AttachmentJson) is not null =>
                Vista(id, BlockFormat.Attachment, b.Text, b.AttachmentJson, null),
            BlockFormat.Prose or BlockFormat.List when Testo(b.Text) =>
                Vista(id, BlockFormat.Prose, b.Text, null, null),
            _ => null,
        };
    }

    private static BlockView Vista(int id, BlockFormat f, string? body, string? json, CalloutKind? k) =>
        new()
        {
            Id = id,
            Format = f,
            // Sempre espanso: la compressione è una scelta editoriale del documento, e un'intro che si
            // apre chiusa non introduce niente.
            State = RenderState.Expanded,
            Body = body,
            BodyJson = json,
            CalloutKind = k,
        };

    private static bool Testo(string? s) => !string.IsNullOrWhiteSpace(s);

    /// <summary>Una sezione senza titolo E senza blocchi non esiste: non si salva e non si rende.</summary>
    private static bool NonVuota(PageIntroSection s) => Testo(s.Title) || s.Blocks.Count > 0;

    private static PageIntroSection Pulisci(PageIntroSection s) => new()
    {
        Title = (s.Title ?? "").Trim(),
        Blocks = (s.Blocks ?? new()).Where(b => b is not null).ToList(),
    };
}
