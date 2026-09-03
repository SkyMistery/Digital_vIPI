using System.Text.Json;
using Vipi.Application.Content;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// I due gesti che ogni viewer documentale fa sulle sezioni: rifare il documento con altre sezioni, e leggere
/// il payload strutturato di una sezione editoriale.
///
/// <para>Stavano scritti identici in quattro pagine (aeroporto, APP, vSOP militare, vLOA). Vivono qui perché
/// li fa anche la pagina <b>unita</b>, e la Regola del 2 del <c>FEATURE-PROCESS</c> dice che al secondo
/// posto si estrae — qui i posti sarebbero diventati cinque.</para>
/// </summary>
public static class SezioniDocumentali
{
    /// <summary>
    /// Lo stesso documento con altre sezioni.
    ///
    /// <para>⚠️ <b>Ogni campo si ricopia</b>: <see cref="DocumentView"/> è una classe con <c>init</c>, e quello
    /// che non si ricopia torna al default <b>in silenzio</b> — «non bloccato» su un documento bloccato,
    /// «niente congelato» su una release. La pagina si renderebbe lo stesso, ed è per questo che il difetto
    /// non si vede. Un posto solo è anche un posto solo in cui aggiungere il campo che verrà.</para>
    /// </summary>
    public static DocumentView ConSezioni(DocumentView view, IReadOnlyList<SectionView> sezioni) =>
        ReferenceEquals(sezioni, view.Sections)
            ? view
            : new DocumentView
            {
                Title = view.Title,
                AiracCycle = view.AiracCycle,
                Sections = sezioni,
                Language = view.Language,
                LanguageLocked = view.LanguageLocked,
                Translations = view.Translations,
            };

    /// <summary>La selezione di aree regolamentate di una sezione. Assente o illeggibile = nessuna area, mai
    /// un'eccezione: un JSON malformato in archivio non deve portare giù la pagina che lo mostra.</summary>
    public static RegulatedSelection LeggiRegulated(SectionView s) =>
        Deser<RegulatedSelection>(SectionPayload.Read(s)) ?? new RegulatedSelection { OwnAuto = false };

    /// <summary>
    /// Il payload strutturato di una sezione, deserializzato. null se non c'è o non si legge.
    ///
    /// <para>⚠️ Si catturano <b>due</b> eccezioni. <c>TryGetProperty</c> su una radice che è un <b>array</b>
    /// alza <see cref="InvalidOperationException"/>, che <b>non</b> è una <see cref="JsonException"/> e passava
    /// indenne il catch messo lì per il JSON malformato: è il 500 sull'intero editor militare del 29 agosto
    /// 2026, e colpiva anche chiunque avesse in archivio una selezione d'aree nella forma legacy.</para>
    /// </summary>
    public static T? Deser<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
}
