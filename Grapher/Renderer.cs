namespace Grapher;

using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using static Helper;

public sealed class Renderer
{
    #region Common

    private const int _marginSize = 12;
    private const double _zoomFactor = 2;
    private const double _moveFactor = 0.25;
    private const double _smoothFactor = 0.1;
    private const int _rulerGray = 64;
    private const int _rulerTextGray = 128;
    private const double _subRulerBright = 0.7;
    private const double _ligaBright = 0.4;

    private static bool _initExpMode = true;
    private static double _minInLimit = 1;
    private static double _maxInLimit = 1e24;
    private static double _minOutLimit = -1e3;
    private static double _maxOutLimit = 1e24;

    private double _minIn = _minInLimit;
    private double _maxIn = _maxInLimit;
    private double _minOut = _minOutLimit;
    private double _maxOut = _maxOutLimit;

    private readonly PictureBox _pictureBox;
    private Bitmap _bitmap = new(1, 1);
    private int _pictureBoxWidth;
    private int _pictureBoxHeight;
    private bool _inExpMode = _initExpMode;
    private bool _outExpMode = _initExpMode;
    private const int _minX = _marginSize;
    private const int _minY = _marginSize;
    private int _maxX;
    private int _maxY;

    public Renderer(PictureBox pictureBox)
    {
        _pictureBox = pictureBox;
        _pictureBox.Image = _bitmap;
        InitRulers();
    }

    public void SetMeasures(bool expMode, double minIn, double maxIn, double minOut, double maxOut)
    {
        if (minIn > maxIn || minOut > maxOut)
            throw new InvalidOperationException();

        _initExpMode = expMode;
        _minInLimit = minIn;
        _maxInLimit = maxIn;
        _minOutLimit = minOut;
        _maxOutLimit = maxOut;

        _inExpMode = expMode;
        _outExpMode = expMode;
        _minIn = minIn;
        _maxIn = maxIn;
        _minOut = minOut;
        _maxOut = maxOut;
    }

    private void Render()
    {
        _bitmap = new Bitmap(_pictureBoxWidth, _pictureBoxHeight);
        RenderRulers();

        foreach (var graphJob in _graphJobs)
            RenderGraph(graphJob);

        foreach (var (color, markers) in _markerJobs)
            RenderMarker(color, markers);

        _pictureBox.Image.Dispose();
        _pictureBox.Image = _bitmap;
    }

    #endregion

    #region Navigation

    public void OnResize()
    {
        var isWidthChanged = _pictureBoxWidth != _pictureBox.Width;
        var isHeightChanged = _pictureBoxHeight != _pictureBox.Height;

        if (!isWidthChanged && !isHeightChanged)
            return;

        if (isWidthChanged)
        {
            _pictureBoxWidth = _pictureBox.Width;
            _maxX = _pictureBoxWidth - _marginSize - 1;
            SetInRange(_minIn, _maxIn);
        }

        if (isHeightChanged)
        {
            _pictureBoxHeight = _pictureBox.Height;
            _maxY = _pictureBoxHeight - _marginSize - 1;
            SetOutRange(_minOut, _maxOut);
        }

        Render();
    }

    public void OnReset()
    {
        var isInChanged = _minIn != _minInLimit || _maxIn != _maxInLimit;
        var isOutChanged = _minOut != _minOutLimit || _maxOut != _maxOutLimit;

        if (!isInChanged && !isOutChanged)
            return;

        if (isInChanged)
            SetInRange(_minInLimit, _maxInLimit);

        if (isOutChanged)
            SetOutRange(_minOutLimit, _maxOutLimit);

        Render();
    }

    private static readonly double _smoothZoomFactor = Math.Pow(_zoomFactor, _smoothFactor);

    public void OnZoomIn(bool smooth, int x, int y) => OnZoom(smooth, isIn: true, x, y);
    public void OnZoomOut(bool smooth, int x, int y) => OnZoom(smooth, isIn: false, x, y);
    public void OnZoomInX(bool smooth, int x) => OnZoom(smooth, isIn: true, x, null);
    public void OnZoomOutX(bool smooth, int x) => OnZoom(smooth, isIn: false, x, null);
    public void OnZoomInY(bool smooth, int y) => OnZoom(smooth, isIn: true, null, y);
    public void OnZoomOutY(bool smooth, int y) => OnZoom(smooth, isIn: false, null, y);

    private void OnZoom(bool smooth, bool isIn, int? x, int? y)
    {
        var zoomFactor = isIn
            ? smooth ? _smoothZoomFactor : _zoomFactor
            : 1 / (smooth ? _smoothZoomFactor : _zoomFactor);

        void ZoomAxis(bool expMode, double posFactor, double min, double max, out double newMin, out double newMax)
        {
            if (expMode)
            {
                var minExp = ToExp(min);
                var expSize = ToExp(max) - minExp;
                var posExp = minExp + expSize / posFactor;
                var newMinExp = posExp - expSize / posFactor / zoomFactor;
                newMin = FromExp(newMinExp);
                newMax = FromExp(newMinExp + expSize / zoomFactor);
            }
            else
            {
                var size = max - min;
                var pos = min + size / posFactor;
                newMin = pos - size / posFactor / zoomFactor;
                newMax = newMin + size / zoomFactor;
            }
        }

        var isInChanged = x != null && (isIn || _minIn != _minInLimit || _maxIn != _maxInLimit);
        var isOutChanged = y != null && (isIn || _minOut != _minOutLimit || _maxOut != _maxOutLimit);

        if (!isInChanged && !isOutChanged)
            return;

        if (isInChanged)
        {
            var posInFactor = x < 0 ? 2 : (_maxX - _minX) / (double)(NotNull(x) - _minX);
            ZoomAxis(_inExpMode, posInFactor, _minIn, _maxIn, out var newMinIn, out var newMaxIn);
            SetInRange(newMinIn, newMaxIn);
        }

        if (isOutChanged)
        {
            var posOutFactor = y < 0 ? 2 : (_maxY - _minY) / (double)(_maxY - NotNull(y));
            ZoomAxis(_outExpMode, posOutFactor, _minOut, _maxOut, out var newMinOut, out var newMaxOut);
            SetOutRange(newMinOut, newMaxOut);
        }

        Render();
    }

    public void OnMoveX(int moveX) => OnMove(moveX, 0);
    public void OnMoveY(int moveY) => OnMove(0, moveY);
    public void OnMoveLeft(bool smooth) => OnMove(smooth ? _moveXSmooth : _moveX, 0);
    public void OnMoveRight(bool smooth) => OnMove(-(smooth ? _moveXSmooth : _moveX), 0);
    public void OnMoveUp(bool smooth) => OnMove(0, smooth ? _moveYSmooth : _moveY);
    public void OnMoveDown(bool smooth) => OnMove(0, -(smooth ? _moveYSmooth : _moveY));

    public void OnMove(int moveX, int moveY)
    {
        static void MoveAxis(bool expMode, double moveFactor, double min, double max, out double newMin, out double newMax)
        {
            if (expMode)
            {
                var minExp = ToExp(min);
                var maxExp = ToExp(max);
                var expDiff = (maxExp - minExp) * moveFactor;
                newMin = FromExp(minExp - expDiff);
                newMax = FromExp(maxExp - expDiff);
            }
            else
            {
                var diff = (max - min) * moveFactor;
                newMin = min - diff;
                newMax = max - diff;
            }
        }

        var isInChanged = moveX != 0 && ((moveX > 0 && _minIn != _minInLimit) || (moveX < 0 && _maxIn != _maxInLimit));
        var isOutChanged = moveY != 0 && ((moveY < 0 && _minOut != _minOutLimit) || (moveY > 0 && _maxOut != _maxOutLimit));

        if (!isInChanged && !isOutChanged)
            return;

        if (isInChanged)
        {
            var moveInFactor = moveX / (double)(_maxX - _minX);
            MoveAxis(_inExpMode, moveInFactor, _minIn, _maxIn, out var newMinIn, out var newMaxIn);
            SetInRange(newMinIn, newMaxIn);
        }

        if (isOutChanged)
        {
            var moveOutFactor = -moveY / (double)(_maxY - _minY);
            MoveAxis(_outExpMode, moveOutFactor, _minOut, _maxOut, out var newMinOut, out var newMaxOut);
            SetOutRange(newMinOut, newMaxOut);
        }

        Render();
    }

    public void OnRangeX(int minX, int maxX) => OnRange(minX, maxX, _minY, _maxY);
    public void OnRangeY(int minY, int maxY) => OnRange(_minX, _maxX, minY, maxY);

    public void OnRange(int minX, int maxX, int minY, int maxY)
    {
        var isInChanged = minX != maxX;
        var isOutChanged = minY != maxY;

        if (isInChanged)
        {
            var minIn = XToIn(minX);
            var maxIn = XToIn(maxX);
            SetInRange(minIn, maxIn);
        }

        if (isOutChanged)
        {
            var minOut = YToOut(maxY);
            var maxOut = YToOut(minY);
            SetOutRange(minOut, maxOut);
        }

        if (!isInChanged && !isOutChanged)
            return;

        Render();
    }

    public void OnToggleExpModeX(int x) => OnToggleExpModeBase(x, null);
    public void OnToggleExpModeY(int y) => OnToggleExpModeBase(null, y);
    public void OnToggleExpMode(int x, int y) => OnToggleExpModeBase(x, y);

    private void OnToggleExpModeBase(int? xOrNull, int? yOrNull)
    {
        var isInChanged = xOrNull != null;
        var isOutChanged = yOrNull != null;

        if (!isInChanged && !isOutChanged)
            throw new InvalidOperationException();

        if (isInChanged)
        {
            _inExpMode = !_inExpMode;
            SetInRange(_minIn, _maxIn);
        }

        if (isOutChanged)
        {
            _outExpMode = !_outExpMode;
            SetOutRange(_minOut, _maxOut);
        }

        Render();
    }

    private void OnToggleExpMode_Todo(int? xOrNull, int? yOrNull)
    {
        var isInChanged = xOrNull != null;
        var isOutChanged = yOrNull != null;

        if (!isInChanged && !isOutChanged)
            throw new InvalidOperationException();

        if (isInChanged)
        {
            var x = xOrNull ?? throw new InvalidOperationException();
            var xSize = _maxX - _minX;
            var posInFactor = x < 0 ? 2 : xSize / (double)(x - _minX);
            var inExpSize = _maxInExp - _minInExp;
            var inExp = _minInExp + inExpSize / posInFactor;
            double newMinIn;
            double newMaxIn;

            if (_inExpMode)
            {
                var @in = FromExp(inExp);
                var inStep = @in * inExpSize / xSize * 2.3;
                newMinIn = @in - (x - _minX) * inStep;
                newMaxIn = newMinIn + xSize * inStep;
            }
            else
            {
                //var inSize = _maxIn - _minIn;
                //var inExpStep = inExp * inExpSize / inSize / xSize * 30;
                //var inExpStep = inExp * inSize / xSize / 500;
                var inExpStep = inExp * inExpSize / xSize / 3; //todo
                var newMinInExp = inExp - (x - _minX) * inExpStep;
                var newMaxInExp = newMinInExp + xSize * inExpStep;
                newMinIn = FromExp(newMinInExp);
                newMaxIn = FromExp(newMaxInExp);
            }

            _inExpMode = !_inExpMode;
            SetInRange(newMinIn, newMaxIn);
        }

        if (isOutChanged)
        {
            var y = yOrNull ?? throw new InvalidOperationException();
            var ySize = _maxY - _minY;
            var posOutFactor = y < 0 ? 2 : ySize / (double)(_maxY - y);
            var outExpSize = _maxOutExp - _minOutExp;
            var outExp = _minOutExp + outExpSize / posOutFactor;
            double newMinOut;
            double newMaxOut;

            if (_outExpMode)
            {
                var @out = FromExp(outExp);
                var outStep = @out * outExpSize / ySize * 2.3;
                newMinOut = @out - (_maxY - y) * outStep;
                newMaxOut = newMinOut + ySize * outStep;
            }
            else
            {
                var outExpStep = outExp * outExpSize / ySize / 3; //todo
                var newMinOutExp = outExp - (_maxY - y) * outExpStep;
                var newMaxOutExp = newMinOutExp + ySize * outExpStep;
                newMinOut = FromExp(newMinOutExp);
                newMaxOut = FromExp(newMaxOutExp);
            }

            _outExpMode = !_outExpMode;
            SetOutRange(newMinOut, newMaxOut);
        }

        Render();
    }

    #endregion

    #region Rulers

    private static readonly Color[] _rulerColors = new Color[3];
    private static readonly Font _rulerFont = new("Tahoma", 8);

    private static readonly Brush _rulerBrush =
        new SolidBrush(Color.FromArgb(_rulerTextGray, _rulerTextGray, _rulerTextGray));

    private static void InitRulers()
    {
        var subRulerGray = (int)(_rulerGray * _subRulerBright);
        var subSubRulerGray = (int)(_rulerGray * _subRulerBright * _subRulerBright);
        _rulerColors[0] = Color.FromArgb(_rulerGray, _rulerGray, _rulerGray);
        _rulerColors[1] = Color.FromArgb(subRulerGray, subRulerGray, subRulerGray);
        _rulerColors[2] = Color.FromArgb(subSubRulerGray, subSubRulerGray, subSubRulerGray);
    }

    private void RenderRulers()
    {
        for (var level = 2; level >= 0; level--)
        {
            var color = _rulerColors[level];

            RenderAxisRulers(_inExpMode, level, _minIn, _maxIn, _maxX - _minX, @in =>
            {
                var x = InToX(@in);
                RenderXRuler(x, color);

                if (level <= 1)
                    RenderRulerNumber(@in, x, _maxY);
            });

            RenderAxisRulers(_outExpMode, level, _minOut, _maxOut, _maxY - _minY, @out =>
            {
                var y = OutToY(@out);
                RenderYRuler(y, color);

                if (level <= 1)
                    RenderRulerNumber(@out, _minX, y);
            });
        }

        var borderColor = _rulerColors[0];
        RenderXRuler(_minX, borderColor);
        RenderXRuler(_maxX, borderColor);
        RenderYRuler(_minY, borderColor);
        RenderYRuler(_maxY, borderColor);
    }

    private static void RenderAxisRulers(bool expMode, int level, double min, double max, int screenSize, Action<double> action)
    {
        if (expMode)
        {
            if (max > 0)
            {
                var minBase = min > 0 ? min : 1;
                var maxBase = max;
                RenderExpRulers(level, minBase, maxBase, screenSize, @out => action(@out));
            }

            if (min < 0)
            {
                var minBase = max < 0 ? -max : 1;
                var maxBase = -min;
                RenderExpRulers(level, minBase, maxBase, screenSize, @out => action(-@out));
            }
        }
        else
        {
            var step = Math.Pow(10, Math.Floor(Math.Log10((max - min) / screenSize * 1.5)) + (4 - level));
            var from = Math.Ceiling(min / step) * step;

            for (var value = from; value < max; value += step)
                action(value);
        }
    }

    private static void RenderExpRulers(int level, double min, double max, int screenSize, Action<double> action)
    {
        var minExp = ToExp(min);
        var maxExp = ToExp(max);

        if (maxExp - minExp < 0.5)
        {
            var step = Math.Pow(10, Math.Floor(Math.Log10((max - min) / screenSize * 1.1)) + (4 - level));
            var from = Math.Ceiling(min / step) * step;

            //todo HACK step бывает настолько маленьким, что не может изменить from, дальше происходит зацикливание
            if (from + step == from) step *= 2;

            for (var value = from; value < max; value += step)
                action(value);

            return;
        }

        var type = level - 1 - (int)Math.Floor(Math.Log10((maxExp - minExp) / screenSize * 10));

        if (type < 1 || type > 4)
            return;

        var powFrom = type == 1 ? FromExp(Math.Floor(minExp / 6) * 6) : FromExp(Math.Floor(minExp));
        var powStep = type == 1 ? 1000000 : 10;
        var nFrom = type == 3 ? 2 : type == 4 ? 1.1 : 1;
        var nStep = type == 3 ? 1 : type == 4 ? 0.1 : 9;
        var nTo = 10 - nStep;

        for (var pow = powFrom; pow < max; pow *= powStep)
            for (var n = nFrom; n <= nTo; n += nStep)
            {
                var value = n * pow;

                if (value < min)
                    continue;

                if (value > max)
                    return;

                action(value);
            }
    }

    private void RenderXRuler(int x, Color color)
    {
        for (var y = _minY + 1; y < _maxY; y += 2)
            _bitmap.SetPixel(x, y, color);
    }

    private void RenderYRuler(int y, Color color)
    {
        for (var x = _minX; x <= _maxX; x += 2)
            _bitmap.SetPixel(x, y, color);
    }

    private void RenderRulerNumber(double value, int x, int y)
    {
        var text = value.ToString(value > -10000 && value < 10000 ? "0.###############" : "0.############### e0");

        // https://stackoverflow.com/questions/6311545/c-sharp-write-text-on-bitmap
        var g = Graphics.FromImage(_bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.DrawString(text, _rulerFont, _rulerBrush, x + 2, y - 14);
        g.Flush();
    }

    #endregion

    #region Markers

    private readonly List<(Color color, IReadOnlyList<InOut> markers)> _markerJobs = new();

    public void AddMarkers(Color color, IReadOnlyList<InOut> markers)
    {
        _markerJobs.Add((color, markers));
    }

    private void RenderMarker(Color color, IReadOnlyList<InOut> markers)
    {
        var count = 0;

        foreach (var marker in markers)
        {
            count++;
            var x = InToX(marker.In);

            if (x < _minX || x > _maxX - 1)
                continue;

            var longMarker = count == markers.Count;
            var markerHeight = longMarker ? 8 : 4;
            var y = OutToY(marker.Out);

            if (y < _minY + 1 || y > _maxY - markerHeight - 1)
                continue;

            for (var i = 0; i < markerHeight; i++)
            {
                var resultY = y + 2 + i;
                _bitmap.SetPixel(x, resultY, color);

                if (!longMarker)
                    _bitmap.SetPixel(x - 1, resultY, color);
            }
        }
    }

    #endregion

    #region Graphs

    private readonly List<GraphJob> _graphJobs = new();

    public void AddGraphNatural(Func<double, double> calculateOrNaN, Color? color = null)
    {
        _graphJobs.Add(new(calculateOrNaN, GraphType.Natural, color ?? Color.White));
    }

    public void AddGraphFull(Func<double, double> calculateOrNaN, Color? color = null)
    {
        _graphJobs.Add(new(calculateOrNaN, GraphType.Full, color ?? Color.White));
    }

    private void RenderGraph(GraphJob graphJob)
    {
        var color = graphJob.Color;
        var calculateOrNaN = graphJob.CalculateOrNaN;
        var isIntPositive = graphJob.Type == GraphType.Natural;

        var ligaColor = Color.FromArgb(
            (int)(color.R * _ligaBright), (int)(color.G * _ligaBright), (int)(color.B * _ligaBright));

        var prevIn = double.NaN;
        var prevX = 0;
        var prevY = 0;
        var prevInToX = 0;

        var useCache = graphJob.InsVersion == _insVersion;
        var cachedOuts = useCache ? graphJob.CachedOuts : graphJob.CachedOuts = new double[_ins.Length];
        graphJob.InsVersion = _insVersion;

        for (var i = 0; i < _ins.Length; i++)
        {
            double @in;
            int x;

            if (isIntPositive)
            {
                @in = Math.Round(_ins[i]);

                if (@in == prevIn || @in <= 0)
                    continue;

                prevIn = @in;
                x = InToX(@in);
            }
            else
            {
                @in = _ins[i];
                x = _inToXs[i];
            }

            if (x == prevInToX || x < _minX || x > _maxX)
                continue;

            prevInToX = x;

            double outOrNaN;

            if (useCache)
                outOrNaN = cachedOuts[i];
            else
            {
                try { outOrNaN = calculateOrNaN(@in); }
                catch { outOrNaN = double.NaN; }
                cachedOuts[i] = outOrNaN;
            }

            if (!double.IsNaN(outOrNaN))
            {
                var y = OutToY(outOrNaN);
                RenderGraphPoint(x, y, color, prevX, prevY, ligaColor);
                prevX = x;
                prevY = y;
            }
            else
                prevX = 0;
        }
    }

    private void RenderGraphPoint(int x, int y, Color color, int prevX, int prevY, Color ligaColor)
    {
        Debug.Assert(x >= _minX && x <= _maxX);
        Debug.Assert(y >= _minY && y <= _maxY);
        _bitmap.SetPixel(x, y, color);

        // Liga

        if (prevX == 0 || (Math.Abs(x - prevX) < 4 && Math.Abs(y - prevY) < 4))
            return;

        var xDiff = x - prevX;
        var yDiffAbs = Math.Abs(y - prevY);

        if (xDiff > yDiffAbs)
        {
            var yStep = (y - prevY) / (float)xDiff;

            for (var i = 2; i < xDiff - 1; i += 2)
                _bitmap.SetPixel(prevX + i, prevY + (int)MathF.Round(yStep * i), ligaColor);
        }
        else
        {
            var xStep = xDiff / (float)yDiffAbs;

            if (y > prevY)
                for (var i = 2; i < yDiffAbs - 1; i += 2)
                    _bitmap.SetPixel(prevX + (int)MathF.Floor(xStep * i), prevY + i, ligaColor);
            else
                for (var i = 2; i < yDiffAbs - 1; i += 2)
                    _bitmap.SetPixel(prevX + (int)MathF.Ceiling(xStep * i), prevY - i, ligaColor);
        }
    }

    #endregion

    #region Tools

    private double _minInExp;
    private double _maxInExp;
    private double _minOutExp;
    private double _maxOutExp;
    private double _zoomX;
    private double _zoomY;
    private int _moveX;
    private int _moveY;
    private int _moveXSmooth;
    private int _moveYSmooth;
    private double[] _ins = Array.Empty<double>();
    private int[] _inToXs = Array.Empty<int>();
    private int _insVersion = 0;

    private readonly List<Action<double[]>> _onChangedIns = new();
    public void AddOnChangedIns(Action<double[]> action) => _onChangedIns.Add(action);

    private void SetInRange(double minIn, double maxIn)
    {
        _insVersion++;
        var xSize = _maxX - _minX;
        _moveX = (int)Math.Round(xSize * _moveFactor);
        _moveXSmooth = (int)Math.Round(xSize * _moveFactor * _smoothFactor);
        _ins = new double[xSize + 1];
        _inToXs = new int[xSize + 1];

        if (_inExpMode)
        {
            if (minIn < _minInLimit)
            {
                maxIn = Math.Min(FromExp(ToExp(maxIn) + (ToExp(_minInLimit) - ToExp(minIn))), _maxInLimit);
                minIn = _minInLimit;
            }
            else if (maxIn > _maxInLimit)
            {
                minIn = Math.Max(FromExp(ToExp(minIn) - (ToExp(maxIn) - ToExp(_maxInLimit))), _minInLimit);
                maxIn = _maxInLimit;
            }

            _minIn = minIn;
            _maxIn = maxIn;
            _minInExp = ToExp(minIn);
            _maxInExp = ToExp(maxIn);
            var inExpSize = _maxInExp - _minInExp;
            var inExpStep = inExpSize / xSize;
            _zoomX = xSize / inExpSize;

            for (var i = 0; i < _ins.Length; i++)
            {
                var @in = FromExp(_minInExp + inExpStep * i);
                _ins[i] = @in;
                _inToXs[i] = InToX(@in);
            }
        }
        else
        {
            if (minIn < _minInLimit)
            {
                maxIn = Math.Min(maxIn + (_minInLimit - minIn), _maxInLimit);
                minIn = _minInLimit;
            }
            else if (maxIn > _maxInLimit)
            {
                minIn = Math.Max(minIn - (maxIn - _maxInLimit), _minInLimit);
                maxIn = _maxInLimit;
            }

            _minIn = minIn;
            _maxIn = maxIn;
            _minInExp = ToExp(minIn);
            _maxInExp = ToExp(maxIn);
            var inSize = maxIn - minIn;
            var inStep = inSize / xSize;
            _zoomX = xSize / inSize;

            for (var i = 0; i < _ins.Length; i++)
            {
                var @in = minIn + inStep * i;
                _ins[i] = @in;
                _inToXs[i] = InToX(@in);
            }
        }

        foreach (var action in _onChangedIns)
            try { action(_ins); }
            catch (Exception exception) { Debug.Fail(exception.ToString()); }
    }

    private void SetOutRange(double minOut, double maxOut)
    {
        var ySize = _maxY - _minY;
        _moveY = (int)Math.Round(ySize * _moveFactor);
        _moveYSmooth = (int)Math.Round(ySize * _moveFactor * _smoothFactor);

        if (_outExpMode)
        {
            if (minOut < _minOutLimit)
            {
                maxOut = Math.Min(FromExp(ToExp(maxOut) + (ToExp(_minOutLimit) - ToExp(minOut))), _maxOutLimit);
                minOut = _minOutLimit;
            }
            else if (maxOut > _maxOutLimit)
            {
                minOut = Math.Max(FromExp(ToExp(minOut) - (ToExp(maxOut) - ToExp(_maxOutLimit))), _minOutLimit);
                maxOut = _maxOutLimit;
            }

            _minOut = minOut;
            _maxOut = maxOut;
            _minOutExp = ToExp(minOut);
            _maxOutExp = ToExp(maxOut);
            _zoomY = ySize / (_maxOutExp - _minOutExp);
        }
        else
        {
            if (minOut < _minOutLimit)
            {
                maxOut = Math.Min(maxOut + (_minOutLimit - minOut), _maxOutLimit);
                minOut = _minOutLimit;
            }
            else if (maxOut > _maxOutLimit)
            {
                minOut = Math.Max(minOut - (maxOut - _maxOutLimit), _minOutLimit);
                maxOut = _maxOutLimit;
            }

            _minOut = minOut;
            _maxOut = maxOut;
            _minOutExp = ToExp(minOut);
            _maxOutExp = ToExp(maxOut);
            _zoomY = ySize / (maxOut - minOut);
        }
    }

    private int InToX(double @in)
    {
        var x = _inExpMode
            ? @in >= -1 && @in <= 1
                ? _minX - _minInExp * _zoomX + @in
                : _minX + (ToExp(@in) - _minInExp) * _zoomX
            : _minX + (@in - _minIn) * _zoomX;

        return (int)Math.Round(x);
    }

    private int OutToY(double @out)
    {
        var y = _outExpMode
            ? @out >= -1 && @out <= 1
                ? _minY + _maxOutExp * _zoomY - @out
                : _minY + (_maxOutExp - ToExp(@out)) * _zoomY
            : _minY + (_maxOut - @out) * _zoomY;

        return y < _minY ? _minY : y > _maxY ? _maxY : (int)Math.Round(y);
    }

    private double XToIn(int x)
    {
        return _inExpMode
            ? FromExp(_minInExp + (x - _minX) / _zoomX)
            : _minIn + (x - _minX) / _zoomX;
    }

    private double YToOut(int y)
    {
        return _inExpMode
            ? FromExp(_maxOutExp + (_minY - y) / _zoomY)
            : _maxOut + (_minY - y) / _zoomY;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ToExp(double value) => value >= 0 ? Math.Log10(value) : -Math.Log10(-value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FromExp(double exp) => exp >= 0 ? Math.Pow(10, exp) : -Math.Pow(10, -exp);

    #endregion
}
