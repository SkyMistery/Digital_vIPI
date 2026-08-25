namespace Vipi.Application.Content;

/// <summary>
/// Il metro contro cui un <b>timbro d'import</b> è «vecchio», cioè: la sorgente ha smesso di mandare quella
/// riga di catalogo. Sta in un posto solo perché lo leggono in due — il giro notturno che apre le
/// segnalazioni e la pagina che le mostra — e due letture diverse dello stesso metro sono il modo in cui due
/// racconti divergono.
/// </summary>
public static class SogliaTimbro
{
    /// <summary>
    /// Margine prima di chiamare vecchio un timbro. Un giorno: gli import girano ogni 24 ore e un giro può
    /// slittare (ritentativo, riavvio, sorgente lenta); senza margine la prima notte storta produrrebbe una
    /// segnalazione per <b>ogni</b> riga di catalogo.
    /// </summary>
    public static readonly TimeSpan Margine = TimeSpan.FromDays(1);

    /// <summary>
    /// La soglia, o <c>null</c> se non si può dire — e allora non si dice niente: se manca l'ultimo giro
    /// riuscito di una delle due famiglie, «non lo sappiamo» non è «sono spariti tutti». È la stessa regola
    /// della guardia dell'avvio a freddo.
    ///
    /// <para>Si prende il giro più <b>vecchio</b> fra i due: col più recente si segnalerebbero le righe
    /// dell'altra famiglia solo perché il suo giro è slittato di qualche ora.</para>
    /// </summary>
    public static DateTime? Calcola(DateTime? ultimoGiroAeroporti, DateTime? ultimoGiroAcc) =>
        ultimoGiroAeroporti is not { } a || ultimoGiroAcc is not { } c
            ? null
            : (a < c ? a : c) - Margine;

    /// <summary>Quota di righe che possono risultare non più elencate senza che la cosa sia presa per buona.</summary>
    public const double QuotaSospetta = 0.25;

    /// <summary>Sotto questo numero la quota non si applica: su pochi settori uno solo la supera sempre.</summary>
    public const int MinimePerLaQuota = 5;

    /// <summary>
    /// ⚠️ <b>La guardia di massa</b>, gemella di quella della proiezione. Se un giro di import <b>riesce</b>
    /// ma torna vuoto per un ente — succede: una risposta a zero elementi non è un errore, quindi lo stato
    /// viene marcato riuscito lo stesso — tutte le righe di quell'ente restano senza timbro nuovo e il giorno
    /// dopo risulterebbero «non più elencate» in blocco. Misurato: bastava spostare in avanti l'ultimo giro
    /// per far comparire trenta settori esteri tutti insieme. Un elenco così non lo legge nessuno, e
    /// soprattutto non è vero: è il segno che il guasto sta a monte, non nelle righe.
    /// </summary>
    public static bool TroppiPerEssereVeri(int stantii, int totale) =>
        stantii >= MinimePerLaQuota && totale > 0 && (double)stantii / totale > QuotaSospetta;
}
