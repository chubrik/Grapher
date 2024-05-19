namespace Chubrik.Grapher;

internal sealed class GraphJob(
    Func<double, double> calculate, GraphType type, Color color)
{
    public Func<double, double> Calculate { get; } = calculate;
    public GraphType Type { get; } = type;
    public Color Color { get; } = color;
    
    public int InsVersion { get; set; } = 0;
    public double[] CachedOuts { get; set; } = [];
}
