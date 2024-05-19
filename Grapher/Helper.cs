namespace Grapher;

using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(T? value) where T : struct
    {
        Debug.Assert(value != null);
        return value.Value;
    }
}
