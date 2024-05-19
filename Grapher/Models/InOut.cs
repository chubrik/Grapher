namespace Grapher;

using System.Diagnostics;

public readonly struct InOut
{
    public double In { get; }
    public double Out { get; }

    public InOut(double @in, double @out)
    {
        Debug.Assert(double.IsFinite(@in));
        Debug.Assert(double.IsFinite(@out));

        In = @in;
        Out = @out;
    }

    public bool IsZero => In == 0 && Out == 0;

    public static readonly InOut Zero = new(0, 0);
}
