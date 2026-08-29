using System.Text.RegularExpressions;

namespace Vipi.Application.Content;

/// <summary>
/// Che cosa si può scrivere nei campi di una radioassistenza, e come si normalizza quel che si scrive.
/// Regole <b>pure</b>: le usano l'anagrafica (in scrittura) e l'editor (per dirlo prima di provarci), e
/// devono essere le stesse — un editor più permissivo del servizio produce un campo rosso dopo il salvataggio.
/// </summary>
public static class NavaidRules
{
    /// <summary>
    /// I tipi offerti in tendina, nell'ordine chiesto dal committente. ⚠️ È un <b>suggerimento</b>, non una
    /// chiusura: un campo può avere una radioassistenza che qui non c'è (DME, VOR/DME) e rifiutargliela
    /// vorrebbe dire mandarlo a scriverla nella prosa, dove non la trova più nessuno.
    /// </summary>
    public static readonly IReadOnlyList<string> TipiSuggeriti = new[] { "ILS", "VOR", "VORTACAN", "TACAN", "NDB" };

    /// <summary>Le nature che manda il sectorfile. Sono anche le sole identità che l'import crea.</summary>
    public const string NaturaVor = "VOR";
    public const string NaturaNdb = "NDB";

    /// <summary>Una frequenza: <c>115.25</c>, <c>390.0</c>, <c>117</c>. VHF e NDB stanno nella stessa forma —
    /// due o tre cifre, eventualmente coi decimali.</summary>
    private static readonly Regex RxFrequenza = new(@"^\d{2,3}(\.\d{1,3})?$", RegexOptions.Compiled);

    /// <summary>Un canale TACAN/DME: <c>19X</c>, <c>99Y</c>, <c>120X</c>.</summary>
    private static readonly Regex RxCanale = new(@"^\d{1,3}[XY]$", RegexOptions.Compiled);

    /// <summary>Un codice di radioassistenza: da due a cinque lettere o cifre (<c>MNL</c>, <c>OST</c>).</summary>
    private static readonly Regex RxCodice = new(@"^[A-Z0-9]{2,5}$", RegexOptions.Compiled);

    /// <summary>Un tipo o una natura: lettere, cifre e la barra di <c>VOR/DME</c>.</summary>
    private static readonly Regex RxTipo = new(@"^[A-Z0-9/ -]{2,16}$", RegexOptions.Compiled);

    /// <summary>Vuoto = «cancella il campo», ed è sempre lecito: si valida solo quel che c'è.</summary>
    public static bool FrequenzaValida(string? v) => Vuoto(v) || RxFrequenza.IsMatch(v!.Trim());

    public static bool CanaleValido(string? v) => Vuoto(v) || RxCanale.IsMatch(Norm(v));

    public static bool CodiceValido(string? v) => !Vuoto(v) && RxCodice.IsMatch(Norm(v));

    public static bool TipoValido(string? v) => Vuoto(v) || RxTipo.IsMatch(Norm(v));

    /// <summary>Maiuscolo e senza spazi ai bordi: un codice è un codice, e <c>mnl</c> e <c>MNL</c> non sono
    /// due radioassistenze. ⚠️ Vale anche in ingresso dall'import, o l'identità si sdoppierebbe lì.</summary>
    public static string Norm(string? v) => (v ?? "").Trim().ToUpperInvariant();

    /// <summary>Il valore da salvare per un campo di testo: null se vuoto, normalizzato altrimenti. Null e
    /// stringa vuota devono essere <b>la stessa cosa</b> in archivio, o «non ce l'ha» avrebbe due forme.</summary>
    public static string? Valore(string? v) => Vuoto(v) ? null : Norm(v);

    /// <summary>Come <see cref="Valore"/>, ma senza maiuscolare: le frequenze non hanno lettere e maiuscolare
    /// un numero non fa niente — serve a non far sembrare che ne faccia.</summary>
    public static string? ValoreNumerico(string? v) => Vuoto(v) ? null : v!.Trim();

    private static bool Vuoto(string? v) => string.IsNullOrWhiteSpace(v);
}
