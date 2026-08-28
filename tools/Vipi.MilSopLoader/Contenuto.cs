using Vipi.Domain;

namespace Vipi.MilSopLoader;

/// <summary>Un blocco da scrivere in una sezione.</summary>
/// <param name="Formato">Prosa, tabella o callout: gli unici tre che si trascrivono da un PDF.</param>
/// <param name="Testo">Corpo della prosa o del callout (MarkdownLite: <c>**grassetto**</c>, a capo, e basta).</param>
/// <param name="Json">Corpo della tabella: <c>{"columns":[…],"rows":[{"cells":[…]}]}</c>.</param>
/// <param name="Avviso">Variante del callout.</param>
public sealed record Blocco(BlockFormat Formato, string? Testo = null, string? Json = null,
                            CalloutKind? Avviso = null)
{
    public static Blocco Prosa(string testo) => new(BlockFormat.Prose, Testo: testo);

    public static Blocco Avvertenza(string testo, CalloutKind kind = CalloutKind.Warning) =>
        new(BlockFormat.Callout, Testo: testo, Avviso: kind);

    public static Blocco Tabella(string[] colonne, params string[][] righe) =>
        new(BlockFormat.Table, Json: System.Text.Json.JsonSerializer.Serialize(new
        {
            columns = colonne,
            rows = righe.Select(r => new { cells = r }).ToArray(),
        }));
}

/// <summary>Il contenuto di una sezione, per chiave di catalogo.</summary>
/// <param name="Chiave">La chiave del profilo <c>AirportMil</c>.</param>
/// <param name="Blocchi">Che cosa ci va dentro, nell'ordine.</param>
/// <param name="Destinatario">A chi si rivolge: quasi tutto è <c>Both</c>; se il PDF parla solo al pilota o
/// solo al controllore lo si dice qui, ed è la chip in cima al documento a farlo valere.</param>
public sealed record SezioneSop(string Chiave, IReadOnlyList<Blocco> Blocchi,
                                SectionAudience Destinatario = SectionAudience.Both);

/// <summary>Il SOP di un campo, trascritto.</summary>
/// <param name="Icao">Il campo.</param>
/// <param name="Fonte">Il file da cui viene, versione compresa: serve a chi rilegge.</param>
/// <param name="Sezioni">Le sezioni con contenuto. Quelle che non ci sono restano vuote, e il rendiconto lo dice.</param>
/// <param name="SenzaContenuto">Le chiavi che nel PDF ci sono ma <b>non</b> si possono trascrivere: sono FIGURE.
/// Elencarle è la parte che conta — una sezione vuota per una figura non trascritta e una sezione vuota per una
/// dimenticanza si assomigliano troppo.</param>
/// <param name="DaNascondere">Le sezioni del profilo che su <i>questo</i> campo non esistono, e vanno tolte dal
/// documento pubblico. ⚠️ Non è la stessa cosa di «vuota»: il profilo semina tutto su tutti perché nascondere è
/// un clic (carta §2), ma una sezione vuota lasciata in vista dice al lettore «qui manca qualcosa» — che su un
/// campo dove quella procedura non c'è è falso.</param>
public sealed record SopTrascritto(string Icao, string Fonte, IReadOnlyList<SezioneSop> Sezioni,
                                   IReadOnlyDictionary<string, string> SenzaContenuto,
                                   IReadOnlyList<string> DaNascondere);
