using System.Collections;
using System.Reflection;

namespace Vipi.Ui.Tests;

/// <summary>
/// Un servizio che risponde vuoto a tutto: <c>Task</c> compiuti, numeri a zero, elenchi e dizionari vuoti, il resto
/// <c>null</c>. Serve a montare una pagina che chiede cinque servizi quando il test ne guarda uno solo.
/// <para>⚠️ Non per il servizio sotto prova: quello va scritto a mano, perché è lì che il test dice qualcosa.</para>
/// </summary>
public class ServizioVuoto : DispatchProxy
{
    public static T Di<T>() where T : class => Create<T, ServizioVuoto>();

    protected override object? Invoke(MethodInfo? m, object?[]? a)
    {
        var tipo = m!.ReturnType;
        if (tipo == typeof(Task)) return Task.CompletedTask;
        if (tipo == typeof(ValueTask)) return ValueTask.CompletedTask;
        if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var dentro = tipo.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(dentro)
                .Invoke(null, new[] { Vuota(dentro) });
        }
        return Vuota(tipo);
    }

    private static object? Vuota(Type t)
    {
        if (t == typeof(void) || t == typeof(string)) return null;
        if (t.IsValueType) return Activator.CreateInstance(t);
        if (t.IsArray) return Array.CreateInstance(t.GetElementType()!, 0);
        if (t.IsGenericType && typeof(IEnumerable).IsAssignableFrom(t))
        {
            var args = t.GetGenericArguments();
            return args.Length == 2
                ? Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))
                : Array.CreateInstance(args[0], 0);
        }
        return null;
    }
}
