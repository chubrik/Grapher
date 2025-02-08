namespace Chubrik.Grapher;

using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using static ValidationHelper;

public sealed class Grapher
{
    #region Common

    private Renderer _renderer;
    private Axis _axisX;
    private Axis _axisY;

    internal Grapher(PictureBox pictureBox)
    {
        _renderer = new Renderer(pictureBox);
        _axisX = Axis.FromViewAreaSize(_renderer.ViewAreaWidth);
        _axisY = Axis.FromViewAreaSize(_renderer.ViewAreaHeight);
        InitRulers();
    }

    public static void Run(Action<Grapher> onGrapher)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new Form(onGrapher);
        Application.Run(form);
    }

    private void Render()
    {
        _renderer = _renderer.GetNew();

        RenderRulers();

        foreach (var graph in _graphs)
            RenderGraph(graph);

        foreach (var (color, markers) in _markerGroups)
            RenderMarkers(color, markers);

        _renderer.Apply();
    }

    private readonly struct Renderer(PictureBox pictureBox)
    {
        private const int _paddingSize = 10;
        private readonly Bitmap _bitmap = new(pictureBox.Width, pictureBox.Height);
        private readonly int _nativeMaxY = pictureBox.Height - _paddingSize - 1;

        public const int NativeMinX = _paddingSize;
        public const int NativeMinY = _paddingSize;
        public int NativeMaxX => pictureBox.Width - _paddingSize - 1;
        public int NativeMaxY => _nativeMaxY;
        public int ViewAreaWidth => pictureBox.Width - _paddingSize * 2;
        public int ViewAreaHeight => pictureBox.Height - _paddingSize * 2;

        public Renderer GetNew()
        {
            return new Renderer(pictureBox);
        }

        public void Apply()
        {
            var oldImage = pictureBox.Image;
            pictureBox.Image = _bitmap;
            oldImage?.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RenderPixel(int viewX, int viewY, Color color)
        {
            _bitmap.SetPixel(ViewToNativeX(viewX), ViewToNativeY(viewY), color);
        }

        #region Conversions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NativeToViewX(int nativeX)
        {
            Debug.Assert(nativeX >= 0 && nativeX < pictureBox.Width);
            return nativeX - _paddingSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NativeToViewY(int nativeY)
        {
            Debug.Assert(nativeY >= 0 && nativeY < pictureBox.Height);
            return _nativeMaxY - nativeY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ViewToNativeX(int viewX)
        {
            Debug.Assert(viewX >= 0 && viewX < ViewAreaWidth);
            return _paddingSize + viewX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ViewToNativeY(int viewY)
        {
            Debug.Assert(viewY >= 0 && viewY < ViewAreaHeight);
            return _nativeMaxY - viewY;
        }

        #endregion
    }

    #endregion

    #region Navigation

    private static readonly double _zoomFactor = Math.Pow(10, 0.25); // 1.7782794100389228
    private const double _moveFactor = 0.25;
    private const double _smoothFactor = 0.1;
    private const double _moveSmoothFactor = _moveFactor * _smoothFactor;
    private static readonly double _zoomSmoothFactor = Math.Pow(_zoomFactor, _smoothFactor);

    public void SetMeasures(Action<Measures, Measures> onMeasures)
    {
        var measuresX = new Measures();
        var measuresY = new Measures();
        onMeasures(measuresX, measuresY);

        SetX(_axisX.WithMeasures(measuresX));
        SetY(_axisY.WithMeasures(measuresY));
    }

    internal void OnResize()
    {
        var isXChanged = SetX(_axisX.WithViewAreaSize(_renderer.ViewAreaWidth));
        var isYChanged = SetY(_axisY.WithViewAreaSize(_renderer.ViewAreaHeight));

        if (isXChanged || isYChanged)
            Render();
    }

    internal void OnSetAsDefault()
    {
        _axisX.SetAsDefaults();
        _axisY.SetAsDefaults();
    }

    internal void OnReset()
    {
        var isXChanged = SetX(_axisX.WithDefaults());
        var isYChanged = SetY(_axisY.WithDefaults());

        if (isXChanged || isYChanged)
            Render();
    }

    internal void OnZoom(bool smooth, bool zoomIn, int? nativeX, int? nativeY)
    {
        var zoomFactor = zoomIn
            ? 1 / (smooth ? _zoomSmoothFactor : _zoomFactor)
            : smooth ? _zoomSmoothFactor : _zoomFactor;

        var isXChanged = nativeX != null && (zoomIn || _axisX.MinCoord < 0 || _axisX.MaxCoord > _axisX.MaxViewCoord);
        var isYChanged = nativeY != null && (zoomIn || _axisY.MinCoord < 0 || _axisY.MaxCoord > _axisY.MaxViewCoord);

        if (isXChanged)
        {
            var position = _renderer.NativeToViewX(NotNull(nativeX)) / (double)_axisX.MaxViewCoord;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var zoomedMaxViewX = _axisX.MaxViewCoord * zoomFactor;
            var newMinViewX = (_axisX.MaxViewCoord - zoomedMaxViewX) * position;
            var newMaxViewX = newMinViewX + zoomedMaxViewX;
            isXChanged = SetX(_axisX.WithViewCoords(newMinViewX, newMaxViewX));
        }

        if (isYChanged)
        {
            var position = _renderer.NativeToViewY(NotNull(nativeY)) / (double)_axisY.MaxViewCoord;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var zoomedMaxViewY = _axisY.MaxViewCoord * zoomFactor;
            var newMinViewY = (_axisY.MaxViewCoord - zoomedMaxViewY) * position;
            var newMaxViewY = newMinViewY + zoomedMaxViewY;
            isYChanged = SetY(_axisY.WithViewCoords(newMinViewY, newMaxViewY));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    internal void OnMoveLeft(bool smooth) => OnMove((int)Math.Round(_axisX.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)), nativeYDiff: 0);
    internal void OnMoveRight(bool smooth) => OnMove(-(int)Math.Round(_axisX.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)), nativeYDiff: 0);
    internal void OnMoveUp(bool smooth) => OnMove(nativeXDiff: 0, (int)Math.Round(_axisY.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)));
    internal void OnMoveDown(bool smooth) => OnMove(nativeXDiff: 0, -(int)Math.Round(_axisY.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)));

    internal void OnMove(int nativeXDiff, int nativeYDiff)
    {
        var isXChanged = nativeXDiff != 0 && ((nativeXDiff > 0 && _axisX.MinCoord < 0) || (nativeXDiff < 0 && _axisX.MaxCoord > _axisX.MaxViewCoord));
        var isYChanged = nativeYDiff != 0 && ((nativeYDiff < 0 && _axisY.MinCoord < 0) || (nativeYDiff > 0 && _axisY.MaxCoord > _axisY.MaxViewCoord));

        if (isXChanged)
        {
            var minViewX = -nativeXDiff;
            var maxViewX = _axisX.MaxViewCoord - nativeXDiff;
            isXChanged = SetX(_axisX.WithViewCoords(minViewX, maxViewX));
        }

        if (isYChanged)
        {
            var minViewY = nativeYDiff;
            var maxViewY = _axisY.MaxViewCoord + nativeYDiff;
            isYChanged = SetY(_axisY.WithViewCoords(minViewY, maxViewY));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    internal void OnRangeX(int nativeMinX, int nativeMaxX) => OnRange(nativeMinX, nativeMaxX, Renderer.NativeMinY, _renderer.NativeMaxY);
    internal void OnRangeY(int nativeMinY, int nativeMaxY) => OnRange(Renderer.NativeMinX, _renderer.NativeMaxX, nativeMinY, nativeMaxY);

    internal void OnRange(int nativeMinX, int nativeMaxX, int nativeMinY, int nativeMaxY)
    {
        if (nativeMinX == nativeMaxX || nativeMinY == nativeMaxY)
            return;

        var minViewX = _renderer.NativeToViewX(nativeMinX);
        var maxViewX = _renderer.NativeToViewX(nativeMaxX);
        var minViewY = _renderer.NativeToViewY(nativeMaxY);
        var maxViewY = _renderer.NativeToViewY(nativeMinY);

        var isXChanged = SetX(_axisX.WithViewCoords(minViewX, maxViewX));
        var isYChanged = SetY(_axisY.WithViewCoords(minViewY, maxViewY));

        if (isXChanged || isYChanged)
            Render();
    }

    internal void OnMinLogDiff(int xLogDiff, int yLogDiff)
    {
        Check(xLogDiff >= -1 && xLogDiff <= 1);
        Check(yLogDiff >= -1 && yLogDiff <= 1);

        var isXChanged = xLogDiff != 0 && SetX(_axisX.WithMinLogDiff(xLogDiff));
        var isYChanged = yLogDiff != 0 && SetY(_axisY.WithMinLogDiff(yLogDiff));

        if (isXChanged || isYChanged)
            Render();
    }

    internal void OnMaxLogDiff(int xLogDiff, int yLogDiff)
    {
        Check(xLogDiff >= -1 && xLogDiff <= 1);
        Check(yLogDiff >= -1 && yLogDiff <= 1);

        var isXChanged = xLogDiff != 0 && SetX(_axisX.WithMaxLogDiff(xLogDiff));
        var isYChanged = yLogDiff != 0 && SetY(_axisY.WithMaxLogDiff(yLogDiff));

        if (isXChanged || isYChanged)
            Render();
    }

    private int _insVersion = 0;
    private double[] _ins = [];

    private bool SetX(Axis axisX)
    {
        if (_axisX.Equals(axisX))
            return false;

        _axisX = axisX;
        _insVersion++;

        if (_ins.Length != axisX.ViewAreaSize)
            _ins = new double[axisX.ViewAreaSize];

        for (var x = 0; x <= axisX.MaxViewCoord; x++)
        {
            var @in = axisX.CoordToValue(x);
            if (@in < axisX.MinValueLimit) @in = axisX.MinValueLimit;
            if (@in > axisX.MaxValueLimit) @in = axisX.MaxValueLimit;
            _ins[x] = @in;
        }

        return true;
    }

    private bool SetY(Axis axisY)
    {
        if (_axisY.Equals(axisY))
            return false;

        _axisY = axisY;
        return true;
    }

    #endregion

    #region Rulers

    private const int _rulerGray = 64;
    private static readonly double _rulerBrightFactor = Math.Sqrt(0.5);
    private static readonly Color[] _rulerColors = new Color[3];
    private static readonly Font _rulerFont = new("Tahoma", 8);

    private static void InitRulers()
    {
        var subRulerGray = (int)Math.Round(_rulerGray * _rulerBrightFactor);
        var subSubRulerGray = (int)Math.Round(_rulerGray * _rulerBrightFactor * _rulerBrightFactor);
        _rulerColors[0] = Color.FromArgb(_rulerGray, _rulerGray, _rulerGray);
        _rulerColors[1] = Color.FromArgb(subRulerGray, subRulerGray, subRulerGray);
        _rulerColors[2] = Color.FromArgb(subSubRulerGray, subSubRulerGray, subSubRulerGray);
    }

    private void RenderRulers()
    {
        // todo

        var borderColor = _rulerColors[0];
        RenderXRuler(0, borderColor);
        RenderXRuler(_axisX.MaxViewCoord, borderColor);
        RenderYRuler(0, borderColor);
        RenderYRuler(_axisY.MaxViewCoord, borderColor);
    }

    private void RenderXRuler(int x, Color color)
    {
        var y = x % 2 == 0 ? 2 : 1;

        for (; y < _axisY.MaxViewCoord; y += 2)
            _renderer.RenderPixel(x, y, color);
    }

    private void RenderYRuler(int y, Color color)
    {
        var x = y % 2 == 0 ? 0 : 1;

        for (; x <= _axisX.MaxViewCoord; x += 2)
            _renderer.RenderPixel(x, y, color);
    }

    #endregion

    #region Graphs

    private const float _ligaBright = 0.4f;
    private readonly List<Graph> _graphs = [];

    public void AddGraph(Func<double, double> calculate, Color? color = null)
    {
        var graph = new Graph(calculate, GraphType.Default, color ?? Color.White);
        _graphs.Add(graph);
    }

    public void AddGraphInteger(Func<double, double> calculate, Color? color = null)
    {
        var graph = new Graph(calculate, GraphType.Integer, color ?? Color.White);
        _graphs.Add(graph);
    }

    private void RenderGraph(Graph graph)
    {
        var color = graph.Color;
        var calculate = graph.Calculate;
        var isIntegerType = graph.Type == GraphType.Integer;

        var ligaColor = Color.FromArgb(
            (int)(color.R * _ligaBright), (int)(color.G * _ligaBright), (int)(color.B * _ligaBright));

        var prevIn = double.NaN;
        var prevX = -1;
        var prevY = -1;

        var useCache = graph.InsVersion == _insVersion;
        var cachedOuts = useCache ? graph.CachedOuts : graph.CachedOuts = new double[_ins.Length];
        graph.InsVersion = _insVersion;

        for (var x = 0; x < _ins.Length; x++)
        {
            double @in;

            if (isIntegerType)
            {
                @in = Math.Floor(_ins[x]);

                if (@in == prevIn)
                    continue;

                prevIn = @in;
            }
            else
                @in = _ins[x];

            double @out;

            if (useCache)
                @out = cachedOuts[x];
            else
            {
                try { @out = calculate(@in); }
                catch { @out = double.NaN; }
                cachedOuts[x] = @out;
            }

            var y = _axisY.ValueToViewCoord(@out);

            if (y == null)
            {
                prevX = -1;
                continue;
            }

            RenderGraphPoint(x, y.Value, color, prevX, prevY, ligaColor);
            prevX = x;
            prevY = y.Value;
        }
    }

    private void RenderGraphPoint(int x, int y, Color color, int prevX, int prevY, Color ligaColor)
    {
        _renderer.RenderPixel(x, y, color);

        // Liga

        if (prevX == -1 || prevY == -1 || (Math.Abs(x - prevX) < 4 && Math.Abs(y - prevY) < 4))
            return;

        var xDiff = x - prevX;
        var yAbsDiff = Math.Abs(y - prevY);

        if (xDiff > yAbsDiff)
        {
            var yStep = (y - prevY) / (float)xDiff;

            for (var i = 2; i < xDiff - 1; i += 2)
                _renderer.RenderPixel(prevX + i, prevY + (int)MathF.Floor(yStep * i), ligaColor);
        }
        else
        {
            var xStep = xDiff / (float)yAbsDiff;

            if (y > prevY)
                for (var i = 2; i < yAbsDiff - 1; i += 2)
                    _renderer.RenderPixel(prevX + (int)MathF.Ceiling(xStep * i), prevY + i, ligaColor);
            else
                for (var i = 2; i < yAbsDiff - 1; i += 2)
                    _renderer.RenderPixel(prevX + (int)MathF.Floor(xStep * i), prevY - i, ligaColor);
        }
    }

    #endregion

    #region Markers

    private readonly List<(Color Color, IReadOnlyList<InOut> Markers)> _markerGroups = [];

    private void RenderMarkers(Color color, IReadOnlyList<InOut> markers)
    {
        throw new NotImplementedException();
    }

    #endregion
}
