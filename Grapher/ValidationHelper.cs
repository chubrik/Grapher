namespace Chubrik.Grapher;

using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class ValidationHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Check(bool condition, string? message = null)
    {
        Debug.Assert(condition);

        if (!condition)
            throw new InvalidOperationException(message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(T? value) where T : class
    {
        Debug.Assert(value != null);

        return value ?? throw new InvalidOperationException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(T? value) where T : struct
    {
        Debug.Assert(value != null);

        return value ?? throw new InvalidOperationException();
    }
}
