using Chubrik.Grapher;
using System.Drawing;

Grapher.Run(grapher =>
{
    grapher.AddGraph(x => Math.Sin(x) * 10);
    grapher.AddGraph(x => Math.Cos(x) * 5, Color.Green);
    grapher.AddGraphInteger(x => x * -5, Color.Cyan);
});
