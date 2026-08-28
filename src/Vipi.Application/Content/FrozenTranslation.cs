using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.Application.Content;

/// <summary>
/// Una traduzione <b>congelata</b> in una release: il testo, e se una persona l'aveva riletta al momento
/// della pubblicazione (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §6).
///
/// <para>
/// ⚠️ <b>Perché il timbro viaggia con il testo.</b> Fino al 28 agosto 2026 lo snapshot portava la sola
/// stringa, e il viewer non poteva fare altro che dichiarare tutto «non revisionato». Conseguenza misurata
/// leggendo il codice: l'avviso «traduzione automatica, non revisionata» <b>non si spegneva mai</b> su un
/// documento pubblicato — nemmeno quando ogni singola frase era stata corretta a mano. Lo staff correggeva
/// nel pannello, ripubblicava, e l'avviso restava: un giro di revisione senza uscita, cioè un giro che
/// nessuno fa una seconda volta.
/// </para>
///
/// <para>
/// ⚠️ <b>Sbagliare per eccesso di cautela resta la regola</b>, e non è cambiata: quel che arriva senza
/// timbro si dichiara <b>non riletto</b>. Dichiarare riletta una frase che nessuno ha guardato, su un
/// documento operativo, è l'errore che non si può fare — e gli snapshot pubblicati prima di oggi arrivano
/// tutti senza timbro, quindi restano marcati finché non si ripubblica. È la regola di ogni altra
/// correzione editoriale.
/// </para>
/// </summary>
/// <param name="Text">La traduzione, come la release l'ha fotografata.</param>
/// <param name="Reviewed">Se una persona l'aveva riletta. Falso → la vista la marca «non revisionata».</param>
[JsonConverter(typeof(FrozenTranslationJsonConverter))]
public sealed record FrozenTranslation(string Text, bool Reviewed)
{
    /// <summary>Vero se c'è davvero un testo: uno snapshot rotto o troncato non deve <b>cancellare</b> la
    /// frase originale, deve solo non avere niente da dire.</summary>
    public bool HasText => !string.IsNullOrEmpty(Text);
}

/// <summary>
/// Legge <b>due forme</b> e ne scrive <b>una</b>.
///
/// <para>
/// ⚠️ <b>Le release già pubblicate non si riscrivono.</b> I loro snapshot portano la traduzione come
/// stringa nuda (<c>"impronta": "testo"</c>) e vanno continuati a leggere: sono documenti in vigore, e
/// l'unico modo di aggiornarli è ripubblicarli — cioè una decisione del loro editor, non un effetto
/// collaterale di un rilascio del codice. Una stringa nuda vale <c>Reviewed: false</c>, che è quello che
/// quello snapshot poteva dire.
/// </para>
///
/// <para>
/// In scrittura la forma è <b>sempre</b> l'oggetto <c>{"t": …, "r": …}</c>, anche quando il timbro è falso:
/// due forme in uscita vorrebbero dire che la forma di un dato dipende dal suo valore, e chi legge il JSON
/// a mano — che è il modo in cui si guarda uno snapshot quando qualcosa non torna — vedrebbe la metà dei
/// segmenti in un modo e metà nell'altro senza capire perché.
/// </para>
///
/// <para>⚠️ <b>Non solleva su una forma che non conosce.</b> Uno snapshot è un documento in vigore: se
/// arriva un numero, un array o un <c>null</c> dove ci va un testo, si legge «niente di congelato» e la
/// frase resta nella lingua sorgente. Un documento a chiazze si legge male ma si legge; un documento che
/// non si apre non si legge affatto.</para>
/// </summary>
internal sealed class FrozenTranslationJsonConverter : JsonConverter<FrozenTranslation>
{
    private const string NomeTesto = "t";
    private const string NomeRiletta = "r";

    /// <summary>
    /// ⚠️ <b>Serve, e senza si prende un <c>NullReferenceException</c>.</b> Di default
    /// <c>System.Text.Json</c> non chiama affatto il convertitore su un token <c>null</c>: infila un
    /// <c>null</c> nel dizionario e va avanti. Poi, in <c>DocumentTranslator</c>, la prima cosa che si fa
    /// di una voce congelata è chiederle se ha un testo — e quella riga esplode su una pagina pubblica,
    /// per un solo <c>null</c> in fondo a uno snapshot. Con questo, un <c>null</c> arriva qui e diventa
    /// «niente di congelato», come ogni altra forma che non si sa leggere.
    /// </summary>
    public override bool HandleNull => true;

    public override FrozenTranslation Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __)
    {
        // La forma VECCHIA: la stringa nuda delle release pubblicate prima del 28 agosto 2026.
        if (reader.TokenType == JsonTokenType.String)
            return new FrozenTranslation(reader.GetString() ?? "", false);

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            // Forma sconosciuta: si consuma per intero e si dichiara «niente di congelato». Consumarla è
            // obbligatorio, non cortesia — un lettore lasciato a metà di un valore rompe tutto il resto
            // dello snapshot, cioè trasforma un segmento illeggibile in un documento illeggibile.
            reader.Skip();
            return new FrozenTranslation("", false);
        }

        string testo = "";
        var riletta = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var nome = reader.GetString();
            if (!reader.Read()) break;

            if (string.Equals(nome, NomeTesto, StringComparison.OrdinalIgnoreCase))
                testo = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "";
            else if (string.Equals(nome, NomeRiletta, StringComparison.OrdinalIgnoreCase))
                riletta = reader.TokenType == JsonTokenType.True;
            else
                reader.Skip();   // campo che questa versione non conosce: si ignora, non si inciampa
        }

        return new FrozenTranslation(testo, riletta);
    }

    public override void Write(Utf8JsonWriter writer, FrozenTranslation valore, JsonSerializerOptions _)
    {
        // Con HandleNull acceso il convertitore riceve anche i null in scrittura: non è una forma che
        // produciamo, ma una voce nulla non deve far fallire il salvataggio di una release.
        if (valore is null) { writer.WriteNullValue(); return; }

        writer.WriteStartObject();
        writer.WriteString(NomeTesto, valore.Text);
        writer.WriteBoolean(NomeRiletta, valore.Reviewed);
        writer.WriteEndObject();
    }
}
