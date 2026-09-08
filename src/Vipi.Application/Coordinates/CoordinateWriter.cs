using System.Globalization;
using System.Text;

namespace Vipi.Application.Coordinates;

/// <summary>I due formati d'uscita, e per il sectorfile le due forme.</summary>
public enum CoordinateOutput
{
    /// <summary>DB IVAO: un vertice per riga, <c>lat:lon</c> in gradi decimali.</summary>
    DbIvao,

    /// <summary>Sectorfile, elenco punti: un vertice per riga, <c>N…;E…;</c>. È il default.</summary>
    SectorfilePunti,

    /// <summary>Sectorfile, segmenti: <c>latA;lonA;latB;lonB;TIPO;NOME;</c>, un lato per riga.</summary>
    SectorfileSegmenti,
}

/// <summary>Le scelte che accompagnano un'uscita. I valori di default sono quelli che servono più spesso.</summary>
/// <param name="Decimali">Cifre decimali del formato DB (6 o 8; il DB IVAO ne scrive 8).</param>
/// <param name="Forma">Puntata o compatta, per le due uscite sectorfile.</param>
/// <param name="Tipo">5° campo delle righe a segmenti.</param>
/// <param name="Nome">6° campo delle righe a segmenti; vuoto = non si scrive.</param>
/// <param name="ChiudiAnello">Genera il lato che riporta l'ultimo vertice sul primo. <b>Solo per i segmenti</b>.</param>
/// <param name="RipetiPrimoVertice">
/// Riscrive il primo vertice come <b>ultima riga</b> negli elenchi di vertici (DB IVAO ed elenco punti): è la
/// chiusura esplicita del poligono, quella che <b>webeye pretende</b> e senza la quale rifiuta la forma.
///
/// <para>⚠️ <b>Non è il gemello di <paramref name="ChiudiAnello"/>, ed è per questo che sono due.</b> Un
/// anello si chiude sempre, ma la chiusura si <i>scrive</i> in due modi diversi: fra i segmenti è un
/// <b>lato</b> che nell'elenco dei vertici non esiste e va generato; fra i vertici è una <b>riga ripetuta</b>,
/// cioè un dato duplicato. Un interruttore solo direbbe che sono la stessa cosa, e la prima persona che
/// spegne la chiusura per una costa si ritroverebbe cambiata anche l'altra forma.</para>
///
/// <para>⚠️ <b>Il valore predefinito è <c>false</c>, e non è pigrizia: è il formato.</b> Il DB IVAO scrive i
/// cinque vertici di R14A in cinque righe, senza ripetere il primo — è il dato vero del committente, ed è
/// quello che i test presidiano carattere per carattere. Chi ha bisogno della riga in più è **webeye**, e a
/// chiederla è la pagina, dove c'è una persona che sa in quale campo sta per incollare.</para>
/// </param>
public sealed record CoordinateWriteOptions(
    int Decimali = 8,
    DmsCoordinate.Forma Forma = DmsCoordinate.Forma.Puntata,
    string Tipo = "RESTRICT",
    string? Nome = null,
    bool ChiudiAnello = true,
    bool RipetiPrimoVertice = false)
{
    public static CoordinateWriteOptions Default { get; } = new();
}

/// <summary>
/// Scrive i vertici nei formati d'uscita. Puro e senza I/O, come il lettore.
///
/// <para>⚠️ <b>Vertici e segmenti non sono la stessa cosa</b>: il DB e l'elenco punti elencano <i>vertici</i>, la
/// forma a segmenti elenca <i>lati</i>. Cinque vertici fanno cinque lati solo perché l'ultimo torna sul primo, e
/// quel lato qui va <b>generato</b>: nell'elenco non c'è.</para>
///
/// <para>⚠️ <b>La chiusura riguarda tutt'e tre le uscite, ma si scrive in due modi</b>: fra i segmenti è un
/// <i>lato</i> generato (<see cref="CoordinateWriteOptions.ChiudiAnello"/>), fra i vertici è il primo vertice
/// <i>ripetuto</i> in fondo (<see cref="CoordinateWriteOptions.RipetiPrimoVertice"/>). Due interruttori perché
/// sono due gesti: uno aggiunge un dato che non c'era, l'altro duplica un dato che c'è già.</para>
/// </summary>
public static class CoordinateWriter
{
    public static string Write(IReadOnlyList<(double Lat, double Lon)> punti,
        CoordinateOutput formato, CoordinateWriteOptions? opzioni = null)
    {
        var o = opzioni ?? CoordinateWriteOptions.Default;
        if (punti.Count == 0) return "";

        return formato switch
        {
            CoordinateOutput.DbIvao => Db(punti, o),
            CoordinateOutput.SectorfilePunti => Punti(punti, o),
            CoordinateOutput.SectorfileSegmenti => Segmenti(punti, o),
            _ => "",
        };
    }

    /// <summary>
    /// <c>42.00777778:11.96833333</c>. ⚠️ Gli <b>zeri finali si tagliano</b> — il DB scrive <c>41.975</c>, non
    /// <c>41.97500000</c> — e il separatore decimale è il punto in ogni lingua: è un formato macchina.
    /// </summary>
    private static string Db(IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        return string.Join('\n', ConChiusura(punti, o).Select(p =>
            Decimale(p.Lat, o.Decimali) + ":" + Decimale(p.Lon, o.Decimali)));
    }

    /// <summary><c>N042.00.28.000;E011.58.06.000;</c>, un vertice per riga. Il punto e virgola finale ci vuole.</summary>
    private static string Punti(IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        return string.Join('\n', ConChiusura(punti, o).Select(p => Dms(p, o)));
    }

    /// <summary>
    /// I vertici da scrivere: quelli dati, più il <b>primo ripetuto in fondo</b> se lo si è chiesto. È la
    /// chiusura esplicita del poligono, quella che webeye pretende e senza la quale rifiuta la forma.
    ///
    /// <para>⚠️ <b>Il vertice di chiusura non è un punto in più</b>, ed è la ragione per cui il lettore lo
    /// toglie (<c>CoordinateParser.DaVertici</c>): tenerlo fra i punti falserebbe il conto dei vertici, il
    /// perimetro e ogni gesto sulla forma. Vive qui, in uscita, dove è una <b>riga</b> e non un vertice.</para>
    ///
    /// <para>⚠️ <b>Sotto i tre vertici non si chiude niente</b>: due punti sono un segmento e ripetere il
    /// primo ne farebbe un triangolo degenere; uno solo è un punto.</para>
    ///
    /// <para>⚠️ E se l'elenco che arriva lo ripete <b>già</b>, non si raddoppia: <c>Write</c> è pubblico e chi
    /// lo chiama non passa per forza dal lettore. La tolleranza è la stessa con cui il lettore riconosce la
    /// chiusura (1e-6), o le due funzioni direbbero due cose sullo stesso anello.</para>
    /// </summary>
    private static IEnumerable<(double Lat, double Lon)> ConChiusura(
        IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        if (!o.RipetiPrimoVertice || punti.Count < 3 || Stesso(punti[0], punti[^1])) return punti;
        return punti.Append(punti[0]);
    }

    private const double StessoPunto = 1e-6;

    private static bool Stesso((double Lat, double Lon) a, (double Lat, double Lon) b) =>
        Math.Abs(a.Lat - b.Lat) < StessoPunto && Math.Abs(a.Lon - b.Lon) < StessoPunto;

    /// <summary><c>latA;lonA;latB;lonB;TIPO;NOME;</c>. L'ultimo lato chiude l'anello, se richiesto.</summary>
    private static string Segmenti(IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        var coda = new StringBuilder(o.Tipo.Trim());
        coda.Append(';');
        if (!string.IsNullOrWhiteSpace(o.Nome)) coda.Append(o.Nome.Trim()).Append(';');

        var righe = new List<string>();
        var lati = o.ChiudiAnello ? punti.Count : punti.Count - 1;
        for (var i = 0; i < lati; i++)
        {
            var a = punti[i];
            var b = punti[(i + 1) % punti.Count];
            righe.Add(Dms(a, o) + Dms(b, o) + coda);
        }
        return string.Join('\n', righe);
    }

    private static string Dms((double Lat, double Lon) p, CoordinateWriteOptions o) =>
        DmsCoordinate.Format(p.Lat, isLatitudine: true, o.Forma) + ";" +
        DmsCoordinate.Format(p.Lon, isLatitudine: false, o.Forma) + ";";

    private static string Decimale(double v, int decimali)
    {
        var arrotondato = Math.Round(v, Math.Clamp(decimali, 0, 15), MidpointRounding.AwayFromZero);
        // "0.########" taglia gli zeri finali da solo: è ciò che fa il DB, e ripeterli sarebbe rumore.
        return arrotondato.ToString("0." + new string('#', Math.Clamp(decimali, 0, 15)), CultureInfo.InvariantCulture);
    }
}
