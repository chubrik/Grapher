namespace Chubrik.Grapher;

using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using static ValidationHelper;

public sealed class Grapher
{
    private RenderCtx _renderCtx;
    private Axis _x;
    private Axis _y;

    private double _defaultMinIn = double.NegativeInfinity;
    private double _defaultMaxIn = double.PositiveInfinity;
    private double _defaultMinOut = double.NegativeInfinity;
    private double _defaultMaxOut = double.PositiveInfinity;
    private int _defaultMinInExpIndex = 0;
    private int _defaultMaxInExpIndex = 6;
    private int _defaultMinOutExpIndex = 0;
    private int _defaultMaxOutExpIndex = 6;

    public Grapher(PictureBox pictureBox)
    {
        _renderCtx = new RenderCtx(pictureBox);

        _x = new Axis(
            viewAreaSize: _renderCtx.ViewAreaWidth,
            minExpIndex: _defaultMinInExpIndex,
            maxExpIndex: _defaultMaxInExpIndex,
            tryMinViewValue: _defaultMinIn,
            tryMaxViewValue: _defaultMaxIn,
            minValueLimit: double.NegativeInfinity,
            maxValueLimit: double.PositiveInfinity);

        _y = new Axis(
            viewAreaSize: _renderCtx.ViewAreaHeight,
            minExpIndex: _defaultMinOutExpIndex,
            maxExpIndex: _defaultMaxOutExpIndex,
            tryMinViewValue: _defaultMinOut,
            tryMaxViewValue: _defaultMaxOut,
            minValueLimit: double.NegativeInfinity,
            maxValueLimit: double.PositiveInfinity);

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

        if (measuresX.MinExpIndex != null) _defaultMinInExpIndex = measuresX.MinExpIndex.Value;
        if (measuresX.MaxExpIndex != null) _defaultMaxInExpIndex = measuresX.MaxExpIndex.Value;
        if (measuresX.MinValue != null) _defaultMinIn = measuresX.MinValue.Value;
        if (measuresX.MaxValue != null) _defaultMaxIn = measuresX.MaxValue.Value;

        var newX = new Axis(
            viewAreaSize: _x.ViewAreaSize,
            minExpIndex: _defaultMinInExpIndex,
            maxExpIndex: _defaultMaxInExpIndex,
            tryMinViewValue: _defaultMinIn,
            tryMaxViewValue: _defaultMaxIn,
            minValueLimit: measuresX.MinValueLimit ?? _x.MinValueLimit,
            maxValueLimit: measuresX.MaxValueLimit ?? _x.MaxValueLimit);

        if (measuresY.MinExpIndex != null) _defaultMinOutExpIndex = measuresY.MinExpIndex.Value;
        if (measuresY.MaxExpIndex != null) _defaultMaxOutExpIndex = measuresY.MaxExpIndex.Value;
        if (measuresY.MinValue != null) _defaultMinOut = measuresY.MinValue.Value;
        if (measuresY.MaxValue != null) _defaultMaxOut = measuresY.MaxValue.Value;

        var newY = new Axis(
            viewAreaSize: _y.ViewAreaSize,
            minExpIndex: _defaultMinOutExpIndex,
            maxExpIndex: _defaultMaxOutExpIndex,
            tryMinViewValue: _defaultMinOut,
            tryMaxViewValue: _defaultMaxOut,
            minValueLimit: measuresY.MinValueLimit ?? _y.MinValueLimit,
            maxValueLimit: measuresY.MaxValueLimit ?? _y.MaxValueLimit);

        var isXChanged = !_x.IsEqual(newX);
        var isYChanged = !_y.IsEqual(newY);

        if (isXChanged)
            SetX(newX);

        if (isYChanged)
            SetY(newY);
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
        var isWidthChanged = _renderCtx.IsWidthChanged;
        var isHeightChanged = _renderCtx.IsHeightChanged;

        if (isWidthChanged)
            SetX(_x.WithViewAreaSize(_renderCtx.ViewAreaWidth));

        if (isHeightChanged)
            SetY(_y.WithViewAreaSize(_renderCtx.ViewAreaHeight));

        if (isWidthChanged || isHeightChanged)
            Render();
    }

    public void OnSetAsDefault()
    {
        _defaultMinIn = _x.MinViewValue;
        _defaultMaxIn = _x.MaxViewValue;
        _defaultMinOut = _y.MinViewValue;
        _defaultMaxOut = _y.MaxViewValue;
        _defaultMinInExpIndex = _x.MinExpIndex;
        _defaultMaxInExpIndex = _x.MaxExpIndex;
        _defaultMinOutExpIndex = _y.MinExpIndex;
        _defaultMaxOutExpIndex = _y.MaxExpIndex;
    }

    public void OnReset()
    {
        var newX = new Axis(
            viewAreaSize: _x.ViewAreaSize,
            minExpIndex: _defaultMinInExpIndex,
            maxExpIndex: _defaultMaxInExpIndex,
            tryMinViewValue: _defaultMinIn,
            tryMaxViewValue: _defaultMaxIn,
            minValueLimit: _x.MinValueLimit,
            maxValueLimit: _x.MaxValueLimit);

        var newY = new Axis(
            viewAreaSize: _y.ViewAreaSize,
            minExpIndex: _defaultMinOutExpIndex,
            maxExpIndex: _defaultMaxOutExpIndex,
            tryMinViewValue: _defaultMinOut,
            tryMaxViewValue: _defaultMaxOut,
            minValueLimit: _y.MinValueLimit,
            maxValueLimit: _y.MaxValueLimit);

        var isXChanged = !_x.IsEqual(newX);
        var isYChanged = !_y.IsEqual(newY);

        if (isXChanged)
            SetX(newX);

        if (isYChanged)
            SetY(newY);

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnZoom(bool smooth, bool zoomIn, int? rawX, int? rawY)
    {
        var zoomFactor = zoomIn
            ? 1 / (smooth ? _zoomSmoothFactor : _zoomFactor)
            : smooth ? _zoomSmoothFactor : _zoomFactor;

        var isXChanged = rawX != null && (zoomIn || _x.MinCoord < 0 || _x.MaxCoord > _x.MaxViewCoord);
        var isYChanged = rawY != null && (zoomIn || _y.MinCoord < 0 || _y.MaxCoord > _y.MaxViewCoord);

        if (isXChanged)
        {
            var position = _renderCtx.RawToX(NotNull(rawX)) / (double)_x.MaxViewCoord;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var newMaxViewX = _x.MaxViewCoord * zoomFactor;
            var newMinX = (_x.MaxViewCoord - newMaxViewX) * position;
            var newMaxX = newMinX + newMaxViewX;
            SetX(_x.WithCoords(newMinX, newMaxX));
        }

        if (isYChanged)
        {
            var position = _renderCtx.RawToY(NotNull(rawY)) / (double)_y.MaxViewCoord;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var newMaxViewY = _y.MaxViewCoord * zoomFactor;
            var newMinY = (_y.MaxViewCoord - newMaxViewY) * position;
            var newMaxY = newMinY + newMaxViewY;
            SetY(_y.WithCoords(newMinY, newMaxY));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnMoveLeft(bool smooth) => OnMove((int)Math.Round(_x.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)), rawYDiff: 0);
    public void OnMoveRight(bool smooth) => OnMove(-(int)Math.Round(_x.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)), rawYDiff: 0);
    public void OnMoveUp(bool smooth) => OnMove(rawXDiff: 0, (int)Math.Round(_y.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)));
    public void OnMoveDown(bool smooth) => OnMove(rawXDiff: 0, -(int)Math.Round(_y.MaxViewCoord * (smooth ? _moveSmoothFactor : _moveFactor)));

    public void OnMove(int rawXDiff, int rawYDiff)
    {
        var isXChanged = rawXDiff != 0 && ((rawXDiff > 0 && _x.MinCoord < 0) || (rawXDiff < 0 && _x.MaxCoord > _x.MaxViewCoord));
        var isYChanged = rawYDiff != 0 && ((rawYDiff < 0 && _y.MinCoord < 0) || (rawYDiff > 0 && _y.MaxCoord > _y.MaxViewCoord));

        if (isXChanged)
        {
            var minX = -rawXDiff;
            var maxX = _x.MaxViewCoord - rawXDiff;
            SetX(_x.WithCoords(minX, maxX));
        }

        if (isYChanged)
        {
            var minY = rawYDiff;
            var maxY = _y.MaxViewCoord + rawYDiff;
            SetY(_y.WithCoords(minY, maxY));
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
        SetX(_x.WithCoords(minX, maxX));
        SetY(_y.WithCoords(minY, maxY));
        Render();
    }

    public void OnMinExp(int xDiff, int yDiff)
    {
        Check(xDiff >= -1 && xDiff <= 1);
        Check(yDiff >= -1 && yDiff <= 1);

        var isXChanged = xDiff != 0;
        var isYChanged = yDiff != 0;

        if (isXChanged)
        {
            var newX = _x.WithMinExp(xDiff);

            if (!_x.IsEqual(newX))
                SetX(newX);
            else
                isXChanged = false;
        }

        if (isYChanged)
        {
            var newY = _y.WithMinExp(yDiff);

            if (!_y.IsEqual(newY))
                SetY(newY);
            else
                isYChanged = false;
        }

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnMaxExp(int xDiff, int yDiff)
    {
        Check(xDiff >= -1 && xDiff <= 1);
        Check(yDiff >= -1 && yDiff <= 1);

        var isXChanged = xDiff != 0;
        var isYChanged = yDiff != 0;

        if (isXChanged)
        {
            var newX = _x.WithMaxExp(xDiff);

            if (!_x.IsEqual(newX))
                SetX(newX);
            else
                isXChanged = false;
        }

        if (isYChanged)
        {
            var newY = _y.WithMaxExp(yDiff);

            if (!_y.IsEqual(newY))
                SetY(newY);
            else
                isYChanged = false;
        }

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
        RenderXRuler(_x.MaxViewCoord, borderColor);
        RenderYRuler(0, borderColor);
        RenderYRuler(_y.MaxViewCoord, borderColor);
    }

    private void RenderXRuler(int x, Color color)
    {
        for (var y = 2; y <= _y.MaxViewCoord - 2; y += 2)
            _renderCtx.SetPixel(x, y, color);
    }

    private void RenderYRuler(int y, Color color)
    {
        for (var x = 0; x <= _x.MaxViewCoord; x += 2)
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
                var y = _y.ValueToCoord(@out);

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
        Debug.Assert(x >= -1 && x <= _x.MaxViewCoord);
        Debug.Assert(y >= -1 && y <= _y.MaxViewCoord);
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

    private void SetX(Axis xAxis)
    {
        _x = xAxis;

        _insVersion++;

        if (_ins.Length != _x.ViewAreaSize)
            _ins = new double[_x.ViewAreaSize];

        for (var x = 0; x < _ins.Length; x++)
        {
            var @in = _x.CoordToValue(x);
            if (@in < _x.MinValueLimit) @in = _x.MinValueLimit;
            if (@in > _x.MaxValueLimit) @in = _x.MaxValueLimit;
            _ins[x] = @in;
        }
    }

    private void SetY(Axis yAxis)
    {
        _y = yAxis;
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
        public bool IsWidthChanged => pictureBox.Width != _bitmap.Width;
        public bool IsHeightChanged => pictureBox.Height != _bitmap.Height;
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
