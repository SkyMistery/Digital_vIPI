using System.Collections;
using System.Reflection;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// La copia di un record del motore, per il record nuovo «nella forma dei vicini» (carta F3, slice 8).
/// <para>Un record nuovo non si inventa da zero: nasce <b>copiando il vicino</b>, così ha già i campi di struttura
/// che in quel file valgono per tutti (l'ICAO, la pista, il tipo di settore, il colore) e le righe escono nella
/// forma giusta. Poi l'AOD cambia quel che deve — è più corto che chiedergli dieci campi vuoti, e non si può
/// sbagliare su ciò che non si tocca.</para>
/// <para>Si copiano i campi che si scrivono e il <b>contenuto</b> degli elenchi (i vertici): l'elenco nuovo è un
/// altro oggetto, sennò i due record si dividerebbero i punti e spostarne uno li sposterebbe tutti e due.</para>
/// </summary>
public static class CopiaDelRecord
{
    public static object Di(object record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var tipo = record.GetType();
        object copia = Activator.CreateInstance(tipo)
            ?? throw new InvalidOperationException($"Il record {tipo.Name} non si sa costruire vuoto.");

        foreach (var proprieta in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (proprieta.GetIndexParameters().Length > 0)
                continue;

            object? valore = Leggi(proprieta, record);
            if (valore is null)
                continue;

            if (proprieta.CanWrite && proprieta.SetMethod?.IsPublic == true)
            {
                proprieta.SetValue(copia, valore);
                continue;
            }

            // Un elenco in sola lettura (i vertici, i segmenti): si riempie quello del nuovo, voce per voce.
            if (valore is IList dalVecchio && Leggi(proprieta, copia) is IList delNuovo && !delNuovo.IsReadOnly)
            {
                delNuovo.Clear();
                foreach (object? voce in dalVecchio)
                    delNuovo.Add(voce);
            }
        }

        return copia;
    }

    private static object? Leggi(PropertyInfo proprieta, object da)
    {
        try
        {
            return proprieta.GetValue(da);
        }
        catch (TargetInvocationException)
        {
            // Una proprietà calcolata che inciampa non deve portarsi via la copia.
            return null;
        }
    }
}
