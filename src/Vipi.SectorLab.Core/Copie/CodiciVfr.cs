using System.Globalization;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>
/// I codici dei punti VFR (lotto «Subito» slice 16c, «file per file» F3): chi ne ha già uno, e quale proporre a un punto
/// nuovo. Un codice vale in tutto il sector — è il nome del fix nascosto — quindi «libero» vuol dire che non sta in
/// nessun <c>.vfi</c> né in <c>VFR_NASCOSTI.fix</c>.
/// </summary>
/// <remarks>
/// Sul fork un codice è le due lettere dello scalo, una direzione e un numero (<c>RFS3</c>, <c>BNNW1</c>), ma i codici
/// non seguono una regola di direzione (committente): il Lab non la indovina. Propone le stesse lettere del punto da
/// cui si parte e il primo numero libero dopo il suo.
/// </remarks>
public static class CodiciVfr
{
    /// <summary>Le lettere e il numero di un codice (<c>RFS3</c> → <c>RFS</c>, 3), o null se non è un codice.</summary>
    public static (string Lettere, int Numero)? Parti(string? codice)
    {
        if (!ControlloDeiVfr.EUnCodice(codice))
            return null;
        string pulito = codice!.Trim();
        int cifre = pulito.Length - pulito.TrimEnd("0123456789".ToCharArray()).Length;
        return (pulito[..^cifre], int.Parse(pulito[^cifre..], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Dove sta già quel codice — il nome del punto e il file (<c>«COLOMBO» in lirf.vfi</c>), o il file dei nascosti —
    /// o null se è libero. Guarda il modello com'è adesso, modifiche in sospeso comprese.
    /// </summary>
    public static string? ChiLoHa(SessioneAperta sessione, string codice)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        string cercato = codice.Trim();
        // Prima i punti, poi i fix nascosti: di un codice che sta da tutte e due le parti si dice il punto.
        foreach (var file in sessione.File.Values.OrderBy(f => ControlloDeiVfr.ENascosti(f.Relativo)).ThenBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (file is not IFileConRecord conRecord)
                continue;
            bool vfi = file.Relativo.EndsWith(".vfi", StringComparison.OrdinalIgnoreCase);
            if (!vfi && !ControlloDeiVfr.ENascosti(file.Relativo))
                continue;
            foreach (object record in conRecord.RecordDelModello)
            {
                if (record is VfrPoint punto && vfi && string.Equals(punto.Code.Trim(), cercato, StringComparison.Ordinal))
                    return $"«{punto.Name.Trim()}» in {Path.GetFileName(file.Relativo)}";
                if (record is Fix fix && !vfi && string.Equals(fix.Name.Trim(), cercato, StringComparison.Ordinal))
                    return $"un fix nascosto di {Path.GetFileName(file.Relativo)}";
            }
        }

        return null;
    }

    /// <summary>
    /// Il codice da proporre a un punto nuovo che parte dal record <paramref name="modello"/> (null: dall'ultimo punto
    /// del file che ha un codice): le sue lettere e il primo numero libero dopo il suo. Se il modello non ha un codice
    /// (<c>ED</c>, <c>2500</c>) torna il suo 2° campo com'è; null se il file non ha punti.
    /// </summary>
    public static string? Proposto(SessioneAperta sessione, string relativo, int? modello)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        if (sessione.File.GetValueOrDefault(relativo) is not IFileConRecord conRecord)
            return null;
        var punti = conRecord.RecordDelModello;
        var punto = modello is { } indice
            ? indice >= 0 && indice < punti.Count ? punti[indice] as VfrPoint : null
            : punti.OfType<VfrPoint>().LastOrDefault(p => ControlloDeiVfr.EUnCodice(p.Code)) ?? punti.OfType<VfrPoint>().LastOrDefault();
        if (punto is null)
            return null;
        if (Parti(punto.Code) is not (var lettere, var numero))
            return punto.Code.Trim();

        for (int n = numero + 1; n < 100; n++)
        {
            string candidato = lettere + n.ToString(CultureInfo.InvariantCulture);
            if (ChiLoHa(sessione, candidato) is null)
                return candidato;
        }

        return null;
    }
}
