namespace Chubrik.Grapher;

using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using static ValidationHelper;

public sealed class Grapher
{
    private RenderCtx _renderCtx;
    private Axis _axisX;
    private Axis _axisY;

    public Grapher(PictureBox pictureBox)
    {
        _renderCtx = new RenderCtx(pictureBox);
        _axisX = Axis.FromViewAreaSize(_renderCtx.ViewAreaWidth);
        _axisY = Axis.FromViewAreaSize(_renderCtx.ViewAreaHeight);
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

    public void SetMeasures(Action<Measures, Measures> onMeasures)
    {
        var measuresX = new Measures();
        var measuresY = new Measures();
        onMeasures(measuresX, measuresY);

        SetX(_axisX.WithMeasures(measuresX));
        SetY(_axisY.WithMeasures(measuresY));
    }

    private void Render()
    {
        _renderCtx = _renderCtx.GetNew();

        RenderRulers();

        foreach (var graphJob in _graphJobs)
            RenderGraph(graphJob);

        foreach (var (color, markers) in _markerJobs)
            RenderMarker(color, markers);

        _renderCtx.Apply();
    }

    #region Navigation

    private const double _zoomFactor = 2;
    private const double _moveFactor = 0.25;
    private const double _smoothFactor = 0.1;
    private const double _moveSmoothFactor = _moveFactor * _smoothFactor;
    private static readonly double _zoomSmoothFactor = Math.Pow(_zoomFactor, _smoothFactor);

    public void OnResize()
    {
        var isXChanged = SetX(_axisX.WithViewAreaSize(_renderCtx.ViewAreaWidth));
        var isYChanged = SetY(_axisY.WithViewAreaSize(_renderCtx.ViewAreaHeight));

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnSetAsDefault()
    {
        _axisX.SetAsDefaults();
        _axisY.SetAsDefaults();
    }

    public void OnReset()
    {
        var isXChanged = SetX(_axisX.WithDefaults());
        var isYChanged = SetY(_axisY.WithDefaults());

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnZoom(bool smooth, bool zoomIn, int? rawX, int? rawY)
    {
        var zoomFactor = zoomIn
            ? 1 / (smooth ? _zoomSmoothFactor : _zoomFactor)
            : smooth ? _zoomSmoothFactor : _zoomFactor;

        var isXChanged = rawX != null && (zoomIn || _axisX.MinCoord < 0 || _axisX.MaxCoord > _axisX.MaxViewCoord);
        var isYChanged = rawY != null && (zoomIn || _axisY.MinCoord < 0 || _axisY.MaxCoord > _axisY.MaxViewCoord);

        if (isXChanged)
        {
            var position = _renderCtx.RawToX(NotNull(rawX)) / (double)_axisX.MaxViewCoord;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var newMaxViewX = _axisX.MaxViewCoord * zoomFactor;
            var newMinX = (_axisX.MaxViewCoord - newMaxViewX) * position;
            var newMaxX = newMinX + newMaxViewX;
            isXChanged = SetX(_axisX.WithCoords(newMinX, newMaxX));
        }

        if (isYChanged)
        {
            var position = _renderCtx.RawToY(NotNull(rawY)) / (double)_axisY.MaxViewCoord;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var newMaxViewY = _axisY.MaxViewCoord * zoomFactor;
            var newMinY = (_axisY.MaxViewCoord - newMaxViewY) * position;
            var newMaxY = newMinY + newMaxViewY;
            isYChanged = SetY(_axisY.WithCoords(newMinY, newMaxY));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnMoveLeft(bool smooth) => OnMove((int)Math.Round(_axisX.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)), rawYDiff: 0);
    public void OnMoveRight(bool smooth) => OnMove(-(int)Math.Round(_axisX.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)), rawYDiff: 0);
    public void OnMoveUp(bool smooth) => OnMove(rawXDiff: 0, (int)Math.Round(_axisY.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)));
    public void OnMoveDown(bool smooth) => OnMove(rawXDiff: 0, -(int)Math.Round(_axisY.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)));

    public void OnMove(int rawXDiff, int rawYDiff)
    {
        var isXChanged = rawXDiff != 0 && ((rawXDiff > 0 && _axisX.MinCoord < 0) || (rawXDiff < 0 && _axisX.MaxCoord > _axisX.MaxViewCoord));
        var isYChanged = rawYDiff != 0 && ((rawYDiff < 0 && _axisY.MinCoord < 0) || (rawYDiff > 0 && _axisY.MaxCoord > _axisY.MaxViewCoord));

        if (isXChanged)
        {
            var minX = -rawXDiff;
            var maxX = _axisX.MaxViewCoord - rawXDiff;
            isXChanged = SetX(_axisX.WithCoords(minX, maxX));
        }

        if (isYChanged)
        {
            var minY = rawYDiff;
            var maxY = _axisY.MaxViewCoord + rawYDiff;
            isYChanged = SetY(_axisY.WithCoords(minY, maxY));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnRangeX(int rawMinX, int rawMaxX) => OnRange(rawMinX, rawMaxX, _renderCtx.RawMinY, _renderCtx.RawMaxY);
    public void OnRangeY(int rawMinY, int rawMaxY) => OnRange(_renderCtx.RawMinX, _renderCtx.RawMaxX, rawMinY, rawMaxY);

    public void OnRange(int rawMinX, int rawMaxX, int rawMinY, int rawMaxY)
    {
        if (rawMinX == rawMaxX || rawMinY == rawMaxY)
            return;

        var minX = _renderCtx.RawToX(rawMinX);
        var maxX = _renderCtx.RawToX(rawMaxX);
        var minY = _renderCtx.RawToY(rawMaxY);
        var maxY = _renderCtx.RawToY(rawMinY);

        var isXChanged = SetX(_axisX.WithCoords(minX, maxX));
        var isYChanged = SetY(_axisY.WithCoords(minY, maxY));

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnMinExp(int xDiff, int yDiff)
    {
        Check(xDiff >= -1 && xDiff <= 1);
        Check(yDiff >= -1 && yDiff <= 1);

        var isXChanged = xDiff != 0 && SetX(_axisX.WithMinExp(xDiff));
        var isYChanged = yDiff != 0 && SetY(_axisY.WithMinExp(yDiff));

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnMaxExp(int xDiff, int yDiff)
    {
        Check(xDiff >= -1 && xDiff <= 1);
        Check(yDiff >= -1 && yDiff <= 1);

        var isXChanged = xDiff != 0 && SetX(_axisX.WithMaxExp(xDiff));
        var isYChanged = yDiff != 0 && SetY(_axisY.WithMaxExp(yDiff));

        if (isXChanged || isYChanged)
            Render();
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
            _renderCtx.SetPixel(x, y, color);
    }

    private void RenderYRuler(int y, Color color)
    {
        var x = y % 2 == 0 ? 0 : 1;

        for (; x <= _axisX.MaxViewCoord; x += 2)
            _renderCtx.SetPixel(x, y, color);
    }

    #endregion

    #region Graphs

    private const double _ligaBright = 0.4;
    private readonly List<GraphJob> _graphJobs = [];

    public void AddGraph(Func<double, double> calculate, Color? color = null)
    {
        _graphJobs.Add(new(calculate, GraphType.Default, color ?? Color.White));
    }

    public void AddGraphInteger(Func<double, double> calculate, Color? color = null)
    {
        _graphJobs.Add(new(calculate, GraphType.Integer, color ?? Color.White));
    }

    private void RenderGraph(GraphJob graphJob)
    {
        var color = graphJob.Color;
        var calculate = graphJob.Calculate;
        var isIntegerType = graphJob.Type == GraphType.Integer;

        var ligaColor = Color.FromArgb(
            (int)(color.R * _ligaBright), (int)(color.G * _ligaBright), (int)(color.B * _ligaBright));

        var prevIn = double.NaN;
        var prevX = -1;
        var prevY = -1;

        var useCache = graphJob.InsVersion == _insVersion;
        var cachedOuts = useCache ? graphJob.CachedOuts : graphJob.CachedOuts = new double[_ins.Length];
        graphJob.InsVersion = _insVersion;

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

            if (!double.IsNaN(@out))
            {
                var y = _axisY.ValueToCoord(@out);

                if (y != null)
                {
                    RenderGraphPoint(x, y.Value, color, prevX, prevY, ligaColor);
                    prevX = x;
                    prevY = y.Value;
                    continue;
                }
            }

            prevX = -1;
        }
    }

    private void RenderGraphPoint(int x, int y, Color color, int prevX, int prevY, Color ligaColor)
    {
        Debug.Assert(x >= 0 && x <= _axisX.MaxViewCoord);
        Debug.Assert(y >= 0 && y <= _axisY.MaxViewCoord);
        _renderCtx.SetPixel(x, y, color);

        // Liga

        if (prevX == -1 || prevY == -1 || (Math.Abs(x - prevX) < 4 && Math.Abs(y - prevY) < 4))
            return;

        var xDiff = x - prevX;
        var yDiffAbs = Math.Abs(y - prevY);

        if (xDiff > yDiffAbs)
        {
            var yStep = (y - prevY) / (float)xDiff;

            for (var i = 2; i < xDiff - 1; i += 2)
                _renderCtx.SetPixel(prevX + i, prevY + (int)MathF.Round(yStep * i), ligaColor);
        }
        else
        {
            var xStep = xDiff / (float)yDiffAbs;

            if (y > prevY)
                for (var i = 2; i < yDiffAbs - 1; i += 2)
                    _renderCtx.SetPixel(prevX + (int)MathF.Floor(xStep * i), prevY + i, ligaColor);
            else
                for (var i = 2; i < yDiffAbs - 1; i += 2)
                    _renderCtx.SetPixel(prevX + (int)MathF.Ceiling(xStep * i), prevY - i, ligaColor);
        }
    }

    #endregion

    #region Markers

    private readonly List<(Color color, IReadOnlyList<InOut> markers)> _markerJobs = [];

    private void RenderMarker(Color color, IReadOnlyList<InOut> markers)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Tools

    private int _insVersion = 0;
    private double[] _ins = [];

    private bool SetX(Axis axisX)
    {
        if (axisX.Equals(_axisX))
            return false;

        _axisX = axisX;
        _insVersion++;

        if (_ins.Length != _axisX.ViewAreaSize)
            _ins = new double[_axisX.ViewAreaSize];

        for (var x = 0; x < _ins.Length; x++)
        {
            var @in = _axisX.CoordToValue(x);
            if (@in < _axisX.MinValueLimit) @in = _axisX.MinValueLimit;
            if (@in > _axisX.MaxValueLimit) @in = _axisX.MaxValueLimit;
            _ins[x] = @in;
        }

        return true;
    }

    private bool SetY(Axis axisX)
    {
        if (axisX.Equals(_axisY))
            return false;

        _axisY = axisX;
        return true;
    }

    #endregion

    private readonly struct RenderCtx(PictureBox pictureBox)
    {
        private const int _paddingSize = 10;
        private readonly Bitmap _bitmap = new(pictureBox.Width, pictureBox.Height);
        private readonly int _rawMaxY = pictureBox.Height - _paddingSize - 1;

        public int RawMinX => _paddingSize;
        public int RawMaxX => pictureBox.Width - _paddingSize - 1;
        public int RawMinY => _paddingSize;
        public int RawMaxY => _rawMaxY;
        public int ViewAreaWidth => pictureBox.Width - _paddingSize * 2;
        public int ViewAreaHeight => pictureBox.Height - _paddingSize * 2;

        public int RawToX(int rawX) => rawX - RawMinX;
        public int RawToY(int rawY) => _rawMaxY - rawY;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, Color color)
        {
            Debug.Assert(x >= 0 && x < ViewAreaWidth);
            Debug.Assert(y >= 0 && y < ViewAreaHeight);
            _bitmap.SetPixel(_paddingSize + x, _rawMaxY - y, color);
        }

        public RenderCtx GetNew() => new(pictureBox);

        public void Apply()
        {
            var oldImage = pictureBox.Image;
            pictureBox.Image = _bitmap;
            oldImage?.Dispose();
        }
    }
}
