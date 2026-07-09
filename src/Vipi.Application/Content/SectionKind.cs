namespace Vipi.Application.Content;

/// <summary>
/// Natura di una sezione nel catalogo unificato (doc refactor 08a): <see cref="Editorial"/> = contenuto salvato
/// (blocchi testo/tabella/callout + sotto-sezioni), <see cref="Derived"/> = calcolata live da un renderer per key
/// (aor/frequencies/coordination/minima), mai salvata.
/// </summary>
public enum SectionKind
{
    Editorial,
    Derived,
}
