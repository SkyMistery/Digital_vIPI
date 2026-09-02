using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Import;

/// <summary>
/// Che cosa c'e' dentro una colonna, e quindi <b>chi la sa leggere</b>.
///
/// <para>⚠️ E' l'unico punto in cui una tabella dichiara la propria natura. Tutto il resto della catena —
/// mappatura, anteprima, applicazione — non sa che tabella sta importando: sa solo che questa colonna e' un
/// aeroporto e quell'altra un numero. E' cio' che permette di aggiungere una tabella importabile
/// registrando una specifica, senza toccare un solo <c>switch</c>.</para>
/// </summary>
public enum TipoCella
{
    /// <summary>Testo libero: si prende com'e'.</summary>
    Testo,

    /// <summary>Un intero (un rilevamento in gradi): unita' e simboli si tolgono.</summary>
    Intero,

    /// <summary>Un numero con la virgola (una distanza in miglia).</summary>
    Decimale,

    /// <summary>Un codice ICAO da risolvere sull'archivio degli scali.</summary>
    Aeroporto,

    /// <summary>Un codice da risolvere sull'anagrafica delle radioassistenze.</summary>
    Radioassistenza,

    /// <summary>Un livello di volo o un'altitudine, riletti da <c>LevelFormatting</c>.</summary>
    Livello,

    /// <summary>Una coppia di coordinate.</summary>
    Coordinata,
}

/// <summary>Una colonna di una tabella importabile.</summary>
/// <param name="Chiave">Nome stabile, per il codice che poi costruisce le righe.</param>
/// <param name="Titolo">Come si chiama a schermo (gia' nella lingua di chi importa).</param>
/// <param name="Sinonimi">Gli altri modi in cui la colonna e' intestata nei documenti veri: <c>AIRPORT</c>,
/// <c>Aeroporto</c>, <c>ICAO</c>. Servono a riconoscere l'intestazione senza chiedere niente.</param>
public sealed record ColonnaSpec(
    string Chiave,
    string Titolo,
    TipoCella Tipo,
    bool Obbligatoria = false,
    IReadOnlyList<string>? Sinonimi = null)
{
    /// <summary>Tutti i nomi sotto cui questa colonna si riconosce, titolo compreso.</summary>
    public IEnumerable<string> Nomi => new[] { Chiave, Titolo }.Concat(Sinonimi ?? Array.Empty<string>());
}

/// <summary>
/// Che cosa serve per importare <b>una</b> tabella: le sue colonne, e — quando i separatori non ci sono —
/// come spezzare una riga.
///
/// <para>⚠️ <b>Non contiene come si salva.</b> Chi applica la proposta e' l'editor che ha aperto l'import, e
/// sa gia' scrivere le proprie righe; la specifica dice soltanto <i>com'e' fatta</i> la tabella. Metterci
/// dentro anche la scrittura vorrebbe dire dare al registro il potere di scrivere nei documenti, che e'
/// esattamente il potere che l'anteprima esiste per non dare a nessuno prima dell'approvazione.</para>
/// </summary>
/// <param name="ColonneLibere">
/// Vero per la tabella generica, dove le colonne le decide chi incolla: l'intestazione diventa le colonne e
/// ogni cella e' testo. Falso per le tabelle a colonne dichiarate.
/// </param>
/// <param name="SpezzaRiga">
/// Le <b>ancore</b>: come ricavare le celle da una riga che non ha separatori — quel che succede copiando da
/// un PDF. Null = questa tabella non ha ancore, e senza separatori non si importa.
/// </param>
public sealed record SpecImport(
    string Chiave,
    IReadOnlyList<ColonnaSpec> Colonne,
    bool ColonneLibere = false,
    Func<string, string[]?>? SpezzaRiga = null)
{
    /// <summary>La specifica della tabella generica: colonne quante e come le porta chi incolla.</summary>
    public static SpecImport Generica(string chiave = "generic") =>
        new(chiave, Array.Empty<ColonnaSpec>(), ColonneLibere: true);

    /// <summary>
    /// Una tabella a <b>colonne fisse e celle libere</b> (Nominativi, Parcheggi): le colonne le decide il
    /// profilo, il contenuto e' tutto testo e non si risolve niente da nessuna parte.
    /// <para>⚠️ I titoli arrivano gia' tradotti, e sono anche i <b>sinonimi</b> con cui si riconosce
    /// l'intestazione incollata: una tabella copiata da un SOP scritto in inglese ha le intestazioni in
    /// inglese, e chiedere a chi importa di rinominarle sarebbe la prima cosa da ridigitare.</para>
    /// </summary>
    public static SpecImport ColonneFisse(string chiave, IReadOnlyList<string> titoli) =>
        new(chiave,
            titoli.Select((t, i) => new ColonnaSpec(
                "c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), t, TipoCella.Testo))
                .ToList());
}

/*
   ⚠️ Qui c'era un registro, ed è stato tolto. L'idea era un elenco di tutte le tabelle importabili da cui
   pescare per chiave; ma i titoli delle colonne stanno nella LINGUA DI CHI GUARDA — arrivano dal
   localizzatore della pagina — e una lista statica avrebbe dovuto tenerne una copia neutra: due definizioni
   degli stessi nomi, cioè esattamente il difetto che il registro doveva evitare.

   Le specifiche vivono nelle fabbriche, che sono UN posto solo: `SpecTabelle.AeroportiAlternati`,
   `SpecTabelle.ClausoleAccordo`, `SpecImport.ColonneFisse`, `SpecImport.Generica`. Aggiungere una tabella
   importabile resta «scrivere una fabbrica e passarla al pannello»: nessuno `switch` da toccare, che era il
   punto (regola del 2 del runbook feature).

   Il giorno che servisse davvero enumerarle — un elenco a schermo delle tabelle importabili — il posto è
   questo, e le fabbriche vorranno i titoli come argomento invece che cablati.
*/
