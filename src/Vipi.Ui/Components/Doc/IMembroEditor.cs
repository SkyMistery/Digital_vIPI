using Vipi.Application.Content;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Quel che l'ospite di un editor UNITO può chiedere a un membro, qualunque famiglia sia (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §5b).
///
/// <para>
/// ⚠️ Esiste perché l'ospite deve poter <b>comandare N editor di famiglie diverse</b> senza sapere quale
/// sia quale: un <c>switch</c> per famiglia nella pagina ospite sarebbe il quarto posto in cui si
/// dispaccia sul tipo di documento, e la Regola del 2 del <c>FEATURE-PROCESS</c> dice di estrarre al
/// secondo. I tre componenti-corpo la implementano, e aggiungerne un quarto costa una riga.
/// </para>
/// </summary>
public interface IMembroEditor
{
    /// <summary>Il titolo del documento di questo membro: intesta il suo gruppo nell'editor unito.</summary>
    string Titolo { get; }

    /// <summary>Le sue sezioni radice, per l'indice UNICO che disegna l'ospite.</summary>
    IReadOnlyList<EditableSection> Sezioni { get; }

    /// <summary>Il guscio di questo membro: documento, lock, stato di salvataggio.</summary>
    DocumentEditorShell Guscio { get; }

    /// <summary>
    /// Prende il lock di questo membro. <c>null</c> = preso; altrimenti il <b>nome</b> di chi lo tiene.
    /// <para>⚠️ Torna un nome e non un booleano perché è quello che l'ospite deve <b>dire</b>: «non puoi
    /// modificare, lo tiene Tizio» è una risposta, «non puoi modificare» è un muro.</para>
    /// </summary>
    Task<string?> PrendiLockAsync();

    /// <summary>Molla il lock di questo membro. No-op se non era suo.</summary>
    Task RilasciaLockAsync();

    /// <summary>Ricarica questo membro: lo chiama l'ospite dopo un gesto che tocca tutti.</summary>
    Task RicaricaAsync();

    /// <summary>
    /// L'ultima domanda prima che una pubblicazione parta: <c>false</c> la annulla. Il default è «vai», e
    /// una famiglia che non ha niente da chiedere non la implementa.
    ///
    /// <para>🔴 Esiste perché il pannello di pubblicazione sta <b>solo sull'ospite</b>, e con lui la sua
    /// <c>BeforePublishAsync</c>: prima di questa riga, l'avviso «sezioni non salvate» di una vIPI
    /// d'aeroporto <b>non veniva chiesto</b> quando quell'aeroporto era un MEMBRO. Si pubblicava una
    /// fotografia senza le modifiche aperte, in silenzio — e la fotografia è quel che il pubblico legge.</para>
    /// </summary>
    Task<bool> BeforePublishAsync() => Task.FromResult(true);
}
