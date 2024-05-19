namespace Chubrik.Grapher;

public readonly struct InOut(double @in, double @out)
{
    public double In { get; } = @in;
    public double Out { get; } = @out;
}
