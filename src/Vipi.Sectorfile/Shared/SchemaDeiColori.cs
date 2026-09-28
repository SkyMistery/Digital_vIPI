using System.Drawing;

namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Uno schema di colori di Aurora (<c>ColorSchemes\*.clr</c>, fuori dal sector; nel sector solo il
/// <c>PAR2090.clr</c> di <c>[COLORSCHEME]</c>): righe <c>CHIAVE=valore</c>. Le chiavi sono di Aurora
/// (<c>ARTCC</c>, <c>AIRWAYLOW</c>, <c>SID</c>, <c>COAST</c>, <c>TAXIWAY</c>…); i valori sono colori
/// (<see cref="Colori"/>) o impostazioni (<c>ACC_SOLID=0</c>, <c>VORSYMBOL=«</c>: <see cref="Altri"/>).
/// </summary>
public sealed class SchemaDeiColori
{
    private readonly Dictionary<string, Color?> _colori = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _altri = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le chiavi di colore; <c>null</c> = <c>clNone</c>, Aurora non disegna.</summary>
    public IReadOnlyDictionary<string, Color?> Colori => _colori;

    /// <summary>Le chiavi che non sono colori, col valore come è scritto.</summary>
    public IReadOnlyDictionary<string, string> Altri => _altri;

    /// <summary>Aggiunge o sostituisce una chiave di colore (vince l'ultima, come nel <c>.def</c>).</summary>
    public void Aggiungi(string chiave, Color? colore)
    {
        ArgumentException.ThrowIfNullOrEmpty(chiave);
        _altri.Remove(chiave);
        _colori[chiave] = colore;
    }

    /// <summary>Aggiunge o sostituisce una chiave che non è un colore.</summary>
    public void AggiungiAltro(string chiave, string valore)
    {
        ArgumentException.ThrowIfNullOrEmpty(chiave);
        _colori.Remove(chiave);
        _altri[chiave] = valore ?? string.Empty;
    }

    /// <summary>Il colore di una chiave: vero se lo schema la ha (anche <c>clNone</c>, che dà <c>null</c>).</summary>
    public bool TryColore(string chiave, out Color? colore)
    {
        if (chiave is not null && _colori.TryGetValue(chiave, out colore))
            return true;
        colore = null;
        return false;
    }
}
