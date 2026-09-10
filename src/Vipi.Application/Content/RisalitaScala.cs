using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Perché quel settore, a quel gradino. È la colonna che rende la scala leggibile: senza, dice dove
/// finisce il traffico ma non se la configurazione fa quel che si voleva.</summary>
public enum RisalitaMotivo
{
    /// <summary>Il ricevente scritto nella clausola: il primo gradino, sempre.</summary>
    RiceventeScritto,

    /// <summary>Una riga di ripiego dichiarata a mano, con la sua fascia.</summary>
    RigaDichiarata,

    /// <summary>Un <b>rinvio</b>: a quel gradino ha risposto la copertura del punto.</summary>
    Copertura,

    /// <summary>Il padre di copertura, cioè la coda implicita della catena.</summary>
    Padre,

    /// <summary>Nessuno: il traffico va su UNICOM. È sempre l'ultimo gradino.</summary>
    Unicom,
}

/// <summary>Un gradino: chi raccoglie, perché, e — se la ragione è una riga con fascia — in quale fascia.</summary>
public readonly record struct RisalitaGradino(
    string Callsign, RisalitaMotivo Motivo, int? BaseFeet = null, int? TopFeet = null);

/// <summary>La scala di un punto, con la chiave per raggrupparne le identiche.</summary>
/// <param name="Cop">Il punto, come è scritto nella clausola.</param>
/// <param name="Gradini">La discesa, dal ricevente scritto fino a UNICOM.</param>
/// <param name="Esito">
/// Che cosa ha detto il rinvio, se la catena ne ha incontrato uno. <c>null</c> = nessun rinvio da sciogliere.
/// Serve a spiegare una scala <b>corta</b>: «non è un punto» e «non lo copre nessuno» sono due cose diverse.
/// </param>
public sealed record RisalitaScalaDiUnPunto(
    string Cop, IReadOnlyList<RisalitaGradino> Gradini, CoverageFallbackResult? Esito)
{
    /// <summary>La firma della scala: due punti che risalgono allo stesso modo si raggruppano su questa.</summary>
    public string Firma => string.Join(" → ", Gradini.Select(g => g.Motivo == RisalitaMotivo.Unicom
        ? TransferOnlineResolver.Unicom
        : g.Callsign));
}

/// <summary>
/// «Come risalirebbe questo coordinamento»: la discesa per intero, non un nome solo.
///
/// <para>🔴 <b>Si simula per ELIMINAZIONE, non leggendo i candidati una volta.</b>
/// <see cref="FallbackChain.Candidates"/> scioglie il rinvio una volta e poi cammina l'albero; il sistema
/// vero, a ogni richiesta, lo <b>richiede</b> con chi è online in quel momento. Quindi: si risolve con tutti
/// aperti, si <b>chiude il vincitore</b>, si richiede. Su Milano le due strade coincidono — il padre di ES2 è
/// WS2, che è anche la risposta geometrica — e su un altro albero no: una scala costruita con una chiamata
/// sola sarebbe giusta <i>per caso</i>. Carta
/// <c>docs/feature/2026-09-10-rinvio-geometrico.md</c> Parte 10.</para>
///
/// <para>Puro e deterministico, nessun I/O: il contesto lo porta il chiamante.</para>
/// </summary>
public static class RisalitaScala
{
    /// <summary>
    /// Quanti gradini al massimo. ⚠️ Non è una difesa dai cicli — quelli li chiude l'insieme dei chiusi — ma
    /// dalla scala lunghissima che un dato sporco produrrebbe in un pannello che deve restare leggibile.
    /// </summary>
    public const int MassimoGradini = 12;

    /// <summary>
    /// Quanti <b>punti</b> si possono chiedere in un colpo.
    ///
    /// <para>⚠️ Il tetto sta sui punti e non sulle clausole: una clausola porta più CoP, e ogni punto ha la
    /// <b>sua</b> scala. Contarlo sulle righe prometterebbe dieci e ne consegnerebbe trenta. Dieci è la
    /// misura che il committente ha scelto il 10 settembre 2026 — larga, con le scale identiche raggruppate,
    /// e messa qui perché un giorno andrà cambiata in un posto solo.</para>
    /// </summary>
    public const int MassimoPunti = 10;

    /// <summary>
    /// La scala di un punto.
    /// </summary>
    /// <param name="riceventeNominale">Il ricevente scritto nella clausola: il primo gradino.</param>
    /// <param name="cop">Il punto di trasferimento.</param>
    /// <param name="levelFeet">La quota <b>al trasferimento</b>, in piedi.</param>
    /// <param name="cedente">Chi consegna: lui e il suo dominio non possono raccogliere.</param>
    /// <param name="contesto">Volumi, punti e topologia. Viene rifatto a ogni giro con meno stazioni online.</param>
    public static RisalitaScalaDiUnPunto Costruisci(
        string riceventeNominale,
        string? cop,
        int? levelFeet,
        string? cedente,
        CoverageFallbackContext contesto)
    {
        var gradini = new List<RisalitaGradino>();
        if (string.IsNullOrWhiteSpace(riceventeNominale))
            return new RisalitaScalaDiUnPunto(cop ?? "", gradini, null);

        var ricevente = riceventeNominale.Trim();
        gradini.Add(new RisalitaGradino(ricevente, RisalitaMotivo.RiceventeScritto));

        var tutti = contesto.TuttiISettori;
        var chiusi = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ricevente };
        CoverageFallbackResult? esito = null;

        while (gradini.Count < MassimoGradini)
        {
            // «Tutti aperti tranne i già presi»: è la domanda «e se anche questo fosse chiuso?».
            var online = new HashSet<string>(tutti.Where(c => !chiusi.Contains(c)), StringComparer.OrdinalIgnoreCase);
            var giro = contesto.Con(online);

            // ⚠️ Il rinvio si RICHIEDE a ogni giro, con l'insieme di questo giro: è tutto il punto.
            var passi = FallbackChain.OrderedSteps(ricevente, levelFeet, contesto.Dichiarate, contesto.PadreDi,
                () =>
                {
                    var r = giro.Risolvi(cop, levelFeet, cedente, ricevente);
                    esito ??= r;   // il primo esito è quello che spiega una scala corta
                    return r.AsCandidates();
                });

            var vinto = passi.FirstOrDefault(p => !chiusi.Contains(p.TargetCallsign));
            if (vinto.TargetCallsign is null or "") break;

            gradini.Add(new RisalitaGradino(vinto.TargetCallsign, MotivoDi(vinto), vinto.BaseFeet, vinto.TopFeet));
            chiusi.Add(vinto.TargetCallsign);
        }

        gradini.Add(new RisalitaGradino(TransferOnlineResolver.Unicom, RisalitaMotivo.Unicom));
        return new RisalitaScalaDiUnPunto(cop ?? "", gradini, esito);
    }

    /// <summary>
    /// Le scale di un elenco di punti, con le <b>identiche raggruppate</b>: un blocco per firma, coi punti
    /// che la seguono, nell'ordine in cui la prima è comparsa.
    ///
    /// <para>⚠️ Il raggruppamento si fa <b>dopo</b> aver risolto: due punti diversi possono dare la stessa
    /// scala, e non lo si sa finché non si chiede.</para>
    /// </summary>
    public static IReadOnlyList<(RisalitaScalaDiUnPunto Scala, IReadOnlyList<string> Punti)> Raggruppa(
        IEnumerable<RisalitaScalaDiUnPunto> scale)
    {
        var ordine = new List<string>();
        var perFirma = new Dictionary<string, (RisalitaScalaDiUnPunto Scala, List<string> Punti)>();

        foreach (var s in scale)
        {
            var firma = s.Firma + "|" + (s.Esito?.Outcome.ToString() ?? "");
            if (!perFirma.TryGetValue(firma, out var voce))
            {
                voce = (s, new List<string>());
                perFirma[firma] = voce;
                ordine.Add(firma);
            }
            voce.Punti.Add(s.Cop);
        }

        return ordine
            .Select(f => (perFirma[f].Scala, (IReadOnlyList<string>)perFirma[f].Punti))
            .ToList();
    }

    /// <summary>Vero se, chiuso il ricevente scritto, non raccoglie nessuno: la scala è «lui, poi UNICOM».</summary>
    public static bool FinisceSubitoSuUnicom(RisalitaScalaDiUnPunto scala) =>
        scala.Gradini.Count == 2 && scala.Gradini[1].Motivo == RisalitaMotivo.Unicom;

    private static RisalitaMotivo MotivoDi(FallbackStep passo) =>
        passo.Kind == FallbackTargetKind.Coverage ? RisalitaMotivo.Copertura
        : passo.FromParent ? RisalitaMotivo.Padre
        : RisalitaMotivo.RigaDichiarata;
}
