namespace Grapher;

internal sealed class GraphJob(Func<double, double> calculateOrNaN, GraphType type, Color color)
{
    public Func<double, double> CalculateOrNaN { get; } = calculateOrNaN;
    public GraphType Type { get; } = type;
    public Color Color { get; } = color;

    public double[] CachedOuts { get; set; } = [];
    public int InsVersion { get; set; } = 0;
}
