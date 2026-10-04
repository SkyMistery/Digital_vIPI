namespace Vipi.Ui;

/// <summary>
/// Se il sito si legge solo dopo il login IVAO (committente, 30 settembre 2026). Lo decide l'HOST — il modulo non sa
/// se ha un login suo, uno dell'host che lo monta o nessuno — e lo chiede la porta d'ingresso: a chi non è entrato
/// mostra il solo «Entra con IVAO». Il cancello vero sta nell'host (<c>CancelloDelLogin</c>); questo non chiude
/// niente, dice soltanto che cosa disegnare.
/// </summary>
/// <param name="Obbligatorio">Vero se chi non è entrato non legge niente.</param>
public sealed record AccessoConLogin(bool Obbligatorio)
{
    /// <summary>Senza host che lo dica: aperto, come il sito è sempre stato.</summary>
    public static AccessoConLogin Aperto { get; } = new(false);
}
