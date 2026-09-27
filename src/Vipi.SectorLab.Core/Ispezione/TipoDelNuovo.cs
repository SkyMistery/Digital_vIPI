using System.Globalization;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// La domanda che «+ Nuovo record» fa prima di tutto (lotto «Subito», slice 3e; voce A1 della carta «file per file»):
/// il tipo fisso del record nuovo, coi valori e il loro significato.
/// </summary>
/// <param name="Proprieta">Il campo che il tipo scrive nel nuovo; null quando il tipo è la FORMA del record (negli
/// <c>.artcc</c> un'etichetta L e una traccia T sono record diversi, e il nuovo si copia da uno del tipo scelto).</param>
public sealed record SceltaDelTipo(string Domanda, string? Proprieta, IReadOnlyList<ValoreFisso> Valori);

/// <summary>Quale tipo chiede il nuovo record di un file, e da quale record di quel tipo si copia.</summary>
public static class TipoDelNuovo
{
    /// <summary>
    /// Il campo a tipo fisso che fa da «tipo» per ogni tipo di record: quello che in Aurora sceglie lo strato, il filtro
    /// o il tasto. I record che non sono qui (NDB, attese, settori…) nascono senza domanda, come prima.
    /// </summary>
    private static readonly Dictionary<Type, string> CampoDelTipo = new()
    {
        [typeof(Fix)] = nameof(Fix.DisplayType),
        [typeof(Vor)] = nameof(Vor.ExtraField6),
        [typeof(VfrPoint)] = nameof(VfrPoint.Type),
        [typeof(AirportInfo)] = nameof(AirportInfo.InstallationType),
        [typeof(SidProcedure)] = nameof(SidProcedure.DefaultVisible),
        [typeof(GeometricStrRecord)] = nameof(StrRecord.RecordType),
        [typeof(ProcedureStrRecord)] = nameof(StrRecord.RecordType),
        [typeof(HoldingStrRecord)] = nameof(StrRecord.RecordType),
        [typeof(Line)] = nameof(Line.Color),
        [typeof(Polygon)] = nameof(Polygon.FillColor),
    };

    /// <summary>Negli <c>.artcc</c> (A1): etichetta o traccia, col significato.</summary>
    private static readonly IReadOnlyList<ValoreFisso> DiUnArtcc =
    [
        new("L", "un nome o un testo sulla mappa, in un punto") { Voce = "L · etichetta: un nome o un testo sulla mappa" },
        new("T", "una linea per punti: confini, cerchi, gate") { Voce = "T · traccia: una linea per punti (confini, cerchi, gate)" },
    ];

    /// <summary>La domanda del nuovo record per quel file, o null se il nuovo non chiede un tipo.</summary>
    public static SceltaDelTipo? Di(FileAperto file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord { RecordDelModello.Count: > 0 } conRecord)
            return null;

        if (file.Relativo.EndsWith(".artcc", StringComparison.OrdinalIgnoreCase))
            return new SceltaDelTipo("Etichetta o traccia?", null, DiUnArtcc);

        object primo = conRecord.RecordDelModello[0];
        if (!CampoDelTipo.TryGetValue(primo.GetType(), out string? proprieta)
            || DescrizioniDeiCampi.Di(primo, file.Relativo)?.Campi.FirstOrDefault(c => c.Proprieta == proprieta) is not { } campo)
        {
            return null;
        }

        // Il vuoto («non scritto») e i valori che il manuale non ha non sono un tipo da scegliere per un record nuovo.
        var valori = campo.Valori.Where(v => v.Valore.Length > 0 && v.Valore != nameof(InstallationType.Custom)).ToList();
        return new SceltaDelTipo($"{campo.Nome} del nuovo record", proprieta, valori);
    }

    /// <summary>
    /// Il record da copiare per un nuovo di quel tipo. Quando il tipo è un CAMPO i record hanno tutti la stessa forma: si
    /// copia quello scelto (è vicino a dove l'AOD lavora) e il nuovo prende il tipo. Quando il tipo è la FORMA (L o T
    /// degli .artcc) si copia quello scelto se è di quel tipo, se no l'ultimo del file che lo è; null se non ce n'è.
    /// </summary>
    public static int? Modello(IFileConRecord file, int scelto, SceltaDelTipo scelta, string valore)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(scelta);
        var record = file.RecordDelModello;
        bool sceltoCE = scelto >= 0 && scelto < record.Count;
        if (scelta.Proprieta is not null)
            return sceltoCE ? scelto : null;
        if (sceltoCE && EDelTipo(record[scelto], scelta, valore))
            return scelto;
        for (int i = record.Count - 1; i >= 0; i--)
        {
            if (EDelTipo(record[i], scelta, valore))
                return i;
        }

        return null;
    }

    /// <summary>Vero se il record è di quel tipo.</summary>
    public static bool EDelTipo(object record, SceltaDelTipo scelta, string valore)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(scelta);
        if (scelta.Proprieta is null)
            return valore switch { "L" => record is LabelPoint, "T" => record is StaticBoundaryGroup, _ => false };

        object? suo = record.GetType().GetProperty(scelta.Proprieta)?.GetValue(record);
        string scritto = suo switch
        {
            null => "",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => suo.ToString() ?? "",
        };
        return string.Equals(scritto.Trim(), valore, StringComparison.Ordinal);
    }
}
