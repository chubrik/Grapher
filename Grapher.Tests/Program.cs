static class Program
{
    static void Main()
    {
        Grapher.Grapher.Start(renderer =>
        {
            renderer.SetMeasures(true, 1, 1e12, -1e6, 1e6);
            renderer.AddGraphFull(x => Math.Sin(x) * 1000);
        });
    }
}
