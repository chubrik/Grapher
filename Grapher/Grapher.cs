namespace Chubrik.Grapher;

using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using static ValidationHelper;

public sealed class Grapher
{
    private const int _paddingSize = 10;
    private readonly PictureBox _pictureBox;
    private int _pictureBoxWidth;
    private int _pictureBoxHeight;
    private int _viewAreaWidth;
    private int _viewAreaHeight;
    private int _viewAreaMaxX;
    private int _viewAreaMaxY;
    private Bitmap _bitmap;
    private int _bitmapMaxY;
    private Axis _x = Axis.Default;
    private Axis _y = Axis.Default;

    public static void Run(Action<Grapher> onGrapher)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new Form(onGrapher);
        Application.Run(form);
    }

    public Grapher(PictureBox pictureBox)
    {
        _pictureBox = pictureBox;
        _pictureBox.Image = _bitmap = new Bitmap(_pictureBox.Width, _pictureBox.Height);
        _pictureBoxWidth = _pictureBox.Width;
        _pictureBoxHeight = _pictureBox.Height;
        _viewAreaWidth = _pictureBoxWidth - _paddingSize * 2;
        _viewAreaHeight = _pictureBoxHeight - _paddingSize * 2;
        _viewAreaMaxX = _viewAreaWidth - 1;
        _viewAreaMaxY = _viewAreaHeight - 1;
        _bitmapMaxY = _pictureBoxHeight - _paddingSize - 1;
        InitRulers();
    }

    private double _defaultMinIn = Axis.Default.MinViewValue;
    private double _defaultMaxIn = Axis.Default.MaxViewValue;
    private double _defaultMinOut = Axis.Default.MinViewValue;
    private double _defaultMaxOut = Axis.Default.MaxViewValue;
    private int _defaultMinInExpIndex = Axis.Default.MinExpIndex;
    private int _defaultMaxInExpIndex = Axis.Default.MaxExpIndex;
    private int _defaultMinOutExpIndex = Axis.Default.MinExpIndex;
    private int _defaultMaxOutExpIndex = Axis.Default.MaxExpIndex;

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
            viewAreaSize: _viewAreaWidth,
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
            viewAreaSize: _viewAreaHeight,
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
        _bitmap = new Bitmap(_pictureBox.Width, _pictureBox.Height);
        RenderRulers();

        foreach (var graphJob in _graphJobs)
            RenderGraph(graphJob);

        foreach (var (color, markers) in _markerJobs)
            RenderMarker(color, markers);

        NotNull(_pictureBox.Image).Dispose();
        _pictureBox.Image = _bitmap;
    }

    #region Navigation

    private const double _zoomFactor = 2;
    private const double _moveFactor = 0.25;
    private const double _smoothFactor = 0.1;
    private const double _moveSmoothFactor = _moveFactor * _smoothFactor;
    private static readonly double _zoomSmoothFactor = Math.Pow(_zoomFactor, _smoothFactor);

    public void OnResize()
    {
        var isWidthChanged = _pictureBox.Width != _pictureBoxWidth;
        var isHeightChanged = _pictureBox.Height != _pictureBoxHeight;

        if (isWidthChanged)
        {
            _pictureBoxWidth = _pictureBox.Width;
            _viewAreaWidth = _pictureBoxWidth - _paddingSize * 2;
            _viewAreaMaxX = _viewAreaWidth - 1;
            SetX(_x.WithViewAreaSize(_viewAreaWidth));
        }

        if (isHeightChanged)
        {
            _pictureBoxHeight = _pictureBox.Height;
            _viewAreaHeight = _pictureBoxHeight - _paddingSize * 2;
            _viewAreaMaxY = _viewAreaHeight - 1;
            _bitmapMaxY = _pictureBoxHeight - _paddingSize - 1;
            SetY(_y.WithViewAreaSize(_viewAreaHeight));
        }

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
            viewAreaSize: _viewAreaWidth,
            minExpIndex: _defaultMinInExpIndex,
            maxExpIndex: _defaultMaxInExpIndex,
            tryMinViewValue: _defaultMinIn,
            tryMaxViewValue: _defaultMaxIn,
            minValueLimit: _x.MinValueLimit,
            maxValueLimit: _x.MaxValueLimit);

        var newY = new Axis(
            viewAreaSize: _viewAreaHeight,
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
        var x = rawX == -1 ? (double)_pictureBoxWidth / 2 : (double?)rawX;
        var y = rawY == -1 ? (double)_pictureBoxHeight / 2 : (double?)rawY;

        var zoomFactor = zoomIn
            ? 1 / (smooth ? _zoomSmoothFactor : _zoomFactor)
            : smooth ? _zoomSmoothFactor : _zoomFactor;

        var isXChanged = x != null && (zoomIn || _x.MinCoord < 0 || _x.MaxCoord > _viewAreaMaxX);
        var isYChanged = y != null && (zoomIn || _y.MinCoord < 0 || _y.MaxCoord > _viewAreaMaxY);

        if (isXChanged)
        {
            var position = (NotNull(x) - _paddingSize) / _viewAreaMaxX;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var newViewAreaMaxX = _viewAreaMaxX * zoomFactor;
            var newMinCoord = (_viewAreaMaxX - newViewAreaMaxX) * position;
            var newMaxCoord = newMinCoord + newViewAreaMaxX;
            SetX(_x.WithCoords(newMinCoord, newMaxCoord));
        }

        if (isYChanged)
        {
            var position = 1 - (NotNull(y) - _paddingSize) / _viewAreaMaxY;
            if (position < 0) position = 0;
            if (position > 1) position = 1;
            var newViewAreaMaxY = _viewAreaMaxY * zoomFactor;
            var newMinCoord = (_viewAreaMaxY - newViewAreaMaxY) * position;
            var newMaxCoord = newMinCoord + newViewAreaMaxY;
            SetY(_y.WithCoords(newMinCoord, newMaxCoord));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnMoveLeft(bool smooth) => OnMove((int)Math.Round(_viewAreaMaxX * (smooth ? _moveSmoothFactor : _moveFactor)), moveY: 0);
    public void OnMoveRight(bool smooth) => OnMove(-(int)Math.Round(_viewAreaMaxX * (smooth ? _moveSmoothFactor : _moveFactor)), moveY: 0);
    public void OnMoveUp(bool smooth) => OnMove(moveX: 0, (int)Math.Round(_viewAreaMaxY * (smooth ? _moveSmoothFactor : _moveFactor)));
    public void OnMoveDown(bool smooth) => OnMove(moveX: 0, -(int)Math.Round(_viewAreaMaxY * (smooth ? _moveSmoothFactor : _moveFactor)));

    public void OnMove(int moveX, int moveY)
    {
        var isXChanged = moveX != 0 && ((moveX > 0 && _x.MinCoord < 0) || (moveX < 0 && _x.MaxCoord > _viewAreaMaxX));
        var isYChanged = moveY != 0 && ((moveY < 0 && _y.MinCoord < 0) || (moveY > 0 && _y.MaxCoord > _viewAreaMaxY));

        if (isXChanged)
        {
            var newMinCoord = -moveX;
            var newMaxCoord = -moveX + _viewAreaMaxX;
            SetX(_x.WithCoords(newMinCoord, newMaxCoord));
        }

        if (isYChanged)
        {
            var newMinCoord = moveY;
            var newMaxCoord = moveY + _viewAreaMaxY;
            SetY(_y.WithCoords(newMinCoord, newMaxCoord));
        }

        if (isXChanged || isYChanged)
            Render();
    }

    public void OnRangeX(int minX, int maxX) => OnRange(minX, maxX, _paddingSize, _paddingSize + _viewAreaMaxX);
    public void OnRangeY(int minY, int maxY) => OnRange(_paddingSize, _paddingSize + _viewAreaMaxY, minY, maxY);

    public void OnRange(int minX, int maxX, int minY, int maxY)
    {
        if (minX == maxX || minY == maxY)
            return;

        {
            var newMinCoord = minX - _paddingSize;
            var newMaxCoord = maxX - _paddingSize;
            SetX(_x.WithCoords(newMinCoord, newMaxCoord));
        }
        {
            var newMinCoord = _bitmapMaxY - maxY;
            var newMaxCoord = _bitmapMaxY - minY;
            SetY(_y.WithCoords(newMinCoord, newMaxCoord));
        }

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
        RenderXRuler(_viewAreaMaxX, borderColor);
        RenderYRuler(0, borderColor);
        RenderYRuler(_viewAreaMaxY, borderColor);
    }

    private void RenderXRuler(int x, Color color)
    {
        for (var y = 2; y <= _viewAreaMaxY - 2; y += 2)
            SetPixel(x, y, color);
    }

    private void RenderYRuler(int y, Color color)
    {
        for (var x = 0; x <= _viewAreaMaxX; x += 2)
            SetPixel(x, y, color);
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
        Debug.Assert(x >= -1 && x <= _viewAreaMaxX);
        Debug.Assert(y >= -1 && y <= _viewAreaMaxY);
        SetPixel(x, y, color);

        // Liga

        if (prevX == -1 || prevY == -1 || (Math.Abs(x - prevX) < 4 && Math.Abs(y - prevY) < 4))
            return;

        var xDiff = x - prevX;
        var yDiffAbs = Math.Abs(y - prevY);

        if (xDiff > yDiffAbs)
        {
            var yStep = (y - prevY) / (float)xDiff;

            for (var i = 2; i < xDiff - 1; i += 2)
                SetPixel(prevX + i, prevY + (int)MathF.Round(yStep * i), ligaColor);
        }
        else
        {
            var xStep = xDiff / (float)yDiffAbs;

            if (y > prevY)
                for (var i = 2; i < yDiffAbs - 1; i += 2)
                    SetPixel(prevX + (int)MathF.Floor(xStep * i), prevY + i, ligaColor);
            else
                for (var i = 2; i < yDiffAbs - 1; i += 2)
                    SetPixel(prevX + (int)MathF.Ceiling(xStep * i), prevY - i, ligaColor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetPixel(int x, int y, Color color)
    {
        _bitmap.SetPixel(_paddingSize + x, _bitmapMaxY - y, color);
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

        if (_ins.Length != _viewAreaWidth)
            _ins = new double[_viewAreaWidth];

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
}
