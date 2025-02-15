﻿namespace Chubrik.Grapher;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ValidationHelper;

internal sealed class Axis
{
    #region Common

    private static readonly double _log10E = Math.Log10(Math.E); // 0.43429448190325182
    private static readonly double _antiLog10E = 1 / _log10E;    // 2.3025850929940459

    private readonly int _viewAreaSize;
    private readonly int _maxViewCoord;
    private readonly Data _currents;
    private /*    */ Data _defaults;
    private readonly Data _limits;

    private readonly double _minLogDouble;
    private readonly double _maxLinearValue;
    private readonly double _maxLogValue;

    private readonly double _minCoord;
    private readonly double _minNegLogCoord;
    private readonly double _minLinearCoord;
    private readonly double _maxLinearCoord;
    private readonly double _maxPosLogCoord;
    private readonly double _maxCoord;

    private readonly double _valueToCoord;
    private readonly double _coordToValue;
    private readonly double _logToCoord;
    private readonly double _coordToLog;
    private readonly double _maxLogValue_Mul_CoordDiff;

    public int ViewAreaSize => _viewAreaSize;
    public int MaxViewCoord => _maxViewCoord;
    public double MinCoord => _minCoord;
    public double MaxCoord => _maxCoord;
    public double MinValueLimit => _limits.MinViewValue;
    public double MaxValueLimit => _limits.MaxViewValue;

    private Axis(int viewAreaSize, Data currents, Data defaults, Data limits)
    {
        Check(viewAreaSize >= 2);
        Check(currents.IsInLimits(limits));
        Check(defaults.IsInLimits(limits));

        _viewAreaSize = viewAreaSize;
        _maxViewCoord = viewAreaSize - 1;
        _currents = currents;
        _defaults = defaults;
        _limits = limits;

        var maxLinearValue = Math.Pow(10, currents.MinLog);
        var maxLogValue = Math.Pow(10, currents.MaxLog);

        _minLogDouble = currents.MinLog;
        _maxLinearValue = maxLinearValue;
        _maxLogValue = maxLogValue;

        // Pre Init

        var preLogZoneSize = _antiLog10E * (currents.MaxLog - currents.MinLog);

        _minCoord /* */ = -2 - preLogZoneSize;
        _minNegLogCoord = -1 - preLogZoneSize;
        _minLinearCoord = -1;
        _maxLinearCoord = 1;
        _maxPosLogCoord = 1 + preLogZoneSize;
        _maxCoord /* */ = 2 + preLogZoneSize;

        _valueToCoord = 1 / maxLinearValue;
        _coordToValue = maxLinearValue;
        _logToCoord = _antiLog10E;
        _coordToLog = _log10E;
        _maxLogValue_Mul_CoordDiff = _maxLogValue;

        // Pre Calc

        var preMinViewCoord = ValueToCoord(currents.MinViewValue);
        var preMaxViewCoord = ValueToCoord(currents.MaxViewValue);
        Check(preMinViewCoord >= _minCoord);
        Check(preMaxViewCoord <= _maxCoord);
        Check(preMinViewCoord < preMaxViewCoord);

        var preViewCoordDiff = preMaxViewCoord - preMinViewCoord;
        var coordMultiplier = _maxViewCoord / preViewCoordDiff;
        var coordShift = -preMinViewCoord * coordMultiplier;

        // Final Init

        _minCoord /* */ = _minCoord /* */ * coordMultiplier + coordShift;
        _minNegLogCoord = _minNegLogCoord * coordMultiplier + coordShift;
        _minLinearCoord = _minLinearCoord * coordMultiplier + coordShift;
        _maxLinearCoord = _maxLinearCoord * coordMultiplier + coordShift;
        _maxPosLogCoord = _maxPosLogCoord * coordMultiplier + coordShift;
        _maxCoord /* */ = _maxCoord /* */ * coordMultiplier + coordShift;

        _valueToCoord *= coordMultiplier;
        _coordToValue /= coordMultiplier;
        _logToCoord *= coordMultiplier;
        _coordToLog /= coordMultiplier;
        _maxLogValue_Mul_CoordDiff *= coordMultiplier;
    }

    public bool Equals(Axis other)
    {
        if (_viewAreaSize != other._viewAreaSize) return false;
        if (!_currents.Equals(other._currents)) return false;
        if (!_defaults.Equals(other._defaults)) return false;
        if (!_limits.Equals(other._limits)) return false;
        return true;
    }

    private readonly struct Data
    {
        public readonly int MinLog;
        public readonly int MaxLog;
        public readonly double MinViewValue;
        public readonly double MaxViewValue;

        public Data(int minLog, int maxLog, double minViewValue, double maxViewValue)
        {
            Check(minLog <= maxLog);
            Check(minViewValue < maxViewValue);

            MinLog = minLog;
            MaxLog = maxLog;
            MinViewValue = minViewValue;
            MaxViewValue = maxViewValue;
        }

        public bool IsInLimits(Data limits)
        {
            if (MinLog < limits.MinLog) return false;
            if (MaxLog > limits.MaxLog) return false;
            if (MinViewValue < limits.MinViewValue) return false;
            if (MaxViewValue > limits.MaxViewValue) return false;
            return true;
        }

        public bool Equals(Data other)
        {
            if (MinLog != other.MinLog) return false;
            if (MaxLog != other.MaxLog) return false;
            if (MinViewValue != other.MinViewValue) return false;
            if (MaxViewValue != other.MaxViewValue) return false;
            return true;
        }
    }

    #endregion

    #region Creation

    public static Axis FromViewAreaSize(int viewAreaSize)
    {
        const int initMinLog = 0;
        const int initMaxLog = 3;
        const int constAbsLogLimit = 300;

        var currents = new Data(
            minLog: initMinLog,
            maxLog: initMaxLog,
            minViewValue: double.NegativeInfinity,
            maxViewValue: double.PositiveInfinity);

        var defaults = currents;

        var limits = new Data(
            minLog: -constAbsLogLimit,
            maxLog: constAbsLogLimit,
            minViewValue: double.NegativeInfinity,
            maxViewValue: double.PositiveInfinity);

        return new Axis(
            viewAreaSize: viewAreaSize,
            currents: currents,
            defaults: defaults,
            limits: limits);
    }

    public Axis WithMeasures(Measures measures)
    {
        var minLog = measures.MinLog ?? _currents.MinLog;
        var maxLog = measures.MaxLog ?? _currents.MaxLog;
        var minViewValue = measures.MinViewValue ?? _currents.MinViewValue;
        var maxViewValue = measures.MaxViewValue ?? _currents.MaxViewValue;
        var minValueLimit = measures.MinValueLimit ?? _limits.MinViewValue;
        var maxValueLimit = measures.MaxValueLimit ?? _limits.MaxViewValue;

        var currents = new Data(
            minLog: Math.Max(minLog, _limits.MinLog),
            maxLog: Math.Min(maxLog, _limits.MaxLog),
            minViewValue: Math.Max(minViewValue, minValueLimit),
            maxViewValue: Math.Min(maxViewValue, maxValueLimit));

        var defaults = currents;

        var limits = new Data(
            minLog: _limits.MinLog,
            maxLog: _limits.MaxLog,
            minViewValue: minValueLimit,
            maxViewValue: maxValueLimit);

        try
        {
            return new Axis(
                viewAreaSize: _viewAreaSize,
                currents: currents,
                defaults: defaults,
                limits: limits);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message);
            return this;
        }
    }

    public Axis WithViewAreaSize(int viewAreaSize)
    {
        if (viewAreaSize == _viewAreaSize)
            return this;

        try
        {
            return new Axis(
                viewAreaSize: viewAreaSize,
                currents: _currents,
                defaults: _defaults,
                limits: _limits);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message);
            return this;
        }
    }

    public Axis WithViewCoords(double minViewCoord, double maxViewCoord)
    {
        if (minViewCoord == 0 && maxViewCoord == _maxViewCoord)
            return this;

        var minCoordLimit = ValueToCoord(_limits.MinViewValue);
        var maxCoordLimit = ValueToCoord(_limits.MaxViewValue);

        var adjustedMinViewCoord = minViewCoord;
        var adjustedMaxViewCoord = maxViewCoord;

        if (minViewCoord < minCoordLimit)
        {
            adjustedMinViewCoord = minCoordLimit;
            adjustedMaxViewCoord = Math.Min(maxViewCoord + (minCoordLimit - minViewCoord), maxCoordLimit);
        }
        else if (maxViewCoord > maxCoordLimit)
        {
            adjustedMinViewCoord = Math.Max(minViewCoord - (maxViewCoord - maxCoordLimit), minCoordLimit);
            adjustedMaxViewCoord = maxCoordLimit;
        }

        var minViewValue = CoordToValue(adjustedMinViewCoord);
        var maxViewValue = CoordToValue(adjustedMaxViewCoord);

        var currents = new Data(
            minLog: _currents.MinLog,
            maxLog: _currents.MaxLog,
            minViewValue: Math.Max(minViewValue, _limits.MinViewValue),
            maxViewValue: Math.Min(maxViewValue, _limits.MaxViewValue));

        try
        {
            return new Axis(
                viewAreaSize: _viewAreaSize,
                currents: currents,
                defaults: _defaults,
                limits: _limits);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message);
            return this;
        }
    }

    public Axis WithMinLogDiff(int minLogDiff)
    {
        var minLog = _currents.MinLog + minLogDiff;

        if (minLog < _limits.MinLog)
            return this;

        var maxLog = Math.Max(minLog, _currents.MaxLog);

        var currents = new Data(
            minLog: minLog,
            maxLog: maxLog,
            minViewValue: _currents.MinViewValue,
            maxViewValue: _currents.MaxViewValue);

        return new Axis(
            viewAreaSize: _viewAreaSize,
            currents: currents,
            defaults: _defaults,
            limits: _limits);
    }

    public Axis WithMaxLogDiff(int maxLogDiff)
    {
        var maxLog = _currents.MaxLog + maxLogDiff;

        if (maxLog > _limits.MaxLog)
            return this;

        var minLog = Math.Min(_currents.MinLog, maxLog);

        var currents = new Data(
            minLog: minLog,
            maxLog: maxLog,
            minViewValue: _currents.MinViewValue,
            maxViewValue: _currents.MaxViewValue);

        return new Axis(
            viewAreaSize: _viewAreaSize,
            currents: currents,
            defaults: _defaults,
            limits: _limits);
    }

    public void SetAsDefaults()
    {
        _defaults = _currents;
    }

    public Axis WithDefaults()
    {
        if (_defaults.Equals(_currents))
            return this;

        var currents = _defaults;

        return new Axis(
            viewAreaSize: _viewAreaSize,
            currents: currents,
            defaults: _defaults,
            limits: _limits);
    }

    #endregion

    #region Conversions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? ValueToViewCoord(double value)
    {
        if (double.IsNaN(value))
            return null;

        var coord = ValueToCoord(value);
        return CoordToViewCoord(coord);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int? CoordToViewCoord(double coord)
    {
        var viewCoord = (int)Math.Round(coord);

        if (unchecked((uint)viewCoord) > _maxViewCoord)
            return null;

        return viewCoord;
    }

    private double ValueToCoord(double value)
    {
        Debug.Assert(!double.IsNaN(value));

        double coord;

        if (value < -_maxLogValue)
        {
            // Negative hyperbolic zone
            var coordRelative = -_maxLogValue_Mul_CoordDiff / value;
            coord = _minCoord + coordRelative;
        }
        else if (value < -_maxLinearValue)
        {
            // Negative logarithmic zone
            var log = Math.Log10(-value);
            var logRelative = log - _minLogDouble;
            var coordRelative = logRelative * _logToCoord;
            coord = _minLinearCoord - coordRelative;
        }
        else if (value <= _maxLinearValue)
        {
            // Linear zone
            var valueRelative = value + _maxLinearValue;
            var coordRelative = valueRelative * _valueToCoord;
            coord = _minLinearCoord + coordRelative;
        }
        else if (value <= _maxLogValue)
        {
            // Positive logarithmic zone
            var log = Math.Log10(value);
            var logRelative = log - _minLogDouble;
            var coordRelative = logRelative * _logToCoord;
            coord = _maxLinearCoord + coordRelative;
        }
        else
        {
            // Positive hyperbolic zone
            var coordRelative = _maxLogValue_Mul_CoordDiff / value;
            coord = _maxCoord - coordRelative;
        }

        Debug.Assert(coord >= _minCoord && coord <= _maxCoord);
        return coord;
    }

    public double CoordToValue(double coord)
    {
        double value;

        if (coord < _minNegLogCoord)
        {
            // Negative hyperbolic zone
            if (coord <= _minCoord) return double.NegativeInfinity;
            var coordRelative = coord - _minCoord;
            value = -_maxLogValue_Mul_CoordDiff / coordRelative;
        }
        else if (coord < _minLinearCoord)
        {
            // Negative logarithmic zone
            var coordRelative = _minLinearCoord - coord;
            var logRelative = coordRelative * _coordToLog;
            var log = _minLogDouble + logRelative;
            value = -Math.Pow(10, log);
        }
        else if (coord <= _maxLinearCoord)
        {
            // Linear zone
            var coordRelative = coord - _minLinearCoord;
            var valueRelative = coordRelative * _coordToValue;
            value = valueRelative - _maxLinearValue;
        }
        else if (coord <= _maxPosLogCoord)
        {
            // Positive logarithmic zone
            var coordRelative = coord - _maxLinearCoord;
            var logRelative = coordRelative * _coordToLog;
            var log = _minLogDouble + logRelative;
            value = Math.Pow(10, log);
        }
        else
        {
            // Positive hyperbolic zone
            if (coord >= _maxCoord) return double.PositiveInfinity;
            var coordRelative = _maxCoord - coord;
            value = _maxLogValue_Mul_CoordDiff / coordRelative;
        }

        Debug.Assert(!double.IsNaN(value));
        return value;
    }

    #endregion

    #region Rulers

    private const double _rulersMinCoordStepForDeep = 20;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<Ruler> GetVisibleRulers()
    {
        return GetAllRulers().Where(x => x.IsVisible);
    }

    private List<Ruler> GetAllRulers()
    {
        var rulers = new List<Ruler>();
        var minViewValue = _currents.MinViewValue;
        var maxViewValue = _currents.MaxViewValue;

        var isPositive = Math.Abs(minViewValue) <= maxViewValue;
        var isZeroVisible = minViewValue <= 0 && maxViewValue >= 0;

        if (isZeroVisible)
        {
            if (isPositive)
                minViewValue = 0;
            else
                maxViewValue = 0;
        }
        else
        {
            var viewValueCoeff = isPositive
                ? maxViewValue / minViewValue
                : minViewValue / maxViewValue;

            if (viewValueCoeff <= 2)
            {
                var linearRulers = GetLinearRulers(minViewValue, maxViewValue);
                rulers.AddRange(linearRulers);
                return rulers;
            }
        }

        var isLinearVisible = minViewValue < _maxLinearValue && maxViewValue > -_maxLinearValue;

        if (isLinearVisible)
        {
            var minLinearValue = Math.Max(minViewValue, -_maxLinearValue);
            var maxLinearValue = Math.Min(maxViewValue, _maxLinearValue);
            var linearRulers = GetLinearRulers(minLinearValue, maxLinearValue);
            rulers.AddRange(linearRulers);
        }

        // ...
        var logRulers1 = GetLogRulers(log: 0, isPositive: true);
        var logRulers2 = GetLogRulers(log: 1, isPositive: true);
        var logRulers3 = GetLogRulers(log: 2, isPositive: true);
        rulers.AddRange(logRulers1);
        rulers.AddRange(logRulers2);
        rulers.AddRange(logRulers3);

        if (isZeroVisible)
        {
            var zeroRuler = Ruler.FromRelativeCoord(this, value: 0, relativeCoord: _minCoord);
            var oppositeRulers = rulers.Select(x => x.GetNegative()).ToList();
            rulers.Add(zeroRuler);
            rulers.AddRange(oppositeRulers);
        }

        //var isFlipped = !isPositive && isZeroVisible;

        //// Если на экране преобладают отрицательные значения, переворачиваем границы видимой области,
        //// чтобы вычисления всегда были направлены в положительную сторону.
        //if (isFlipped)
        //    (minViewValue, maxViewValue) = (-maxViewValue, -minViewValue);

        //// Если ноль в видимой области
        //if (minViewValue < 0 && maxViewValue > 0)
        //{
        //    var positiveRulers = new List<Ruler>();

        //    var linearRulers = GetLinearRulers(0, Math.Min(maxViewValue, _maxLinearValue));
        //    positiveRulers.AddRange(linearRulers);

        //    // Если логарифмическая зона в видимой области
        //    if (_maxLinearValue <= maxViewValue)
        //    {
        //        // Проходимся по порядку по каждому сегменту логарифмической зоны.
        //        // Сохраняем каждую границу как линейку 1 уровня.
        //        // По пути запускаем рекурсии 2 уровня, в каждом сегменте значение в 10 раз больше.
        //        // (Для гиперполической зоны рекурсия особая.)
        //    }

        //    var zeroRuler = Ruler.FromRelativeCoord(this, value: 0, relativeCoord: _minCoord);
        //    rulers.Add(zeroRuler);
        //    rulers.AddRange(positiveRulers);
        //    rulers.AddRange(positiveRulers.Select(x => x.GetNegative())); // Копируем в отрицательную сторону
        //    return rulers;
        //}
        // Ноль не в видимой области

        // Определяем, с какой стороны абсолютное значение больше, и используем это направление для дальнейших расчётов.

        { // Две или более границ из логарифмической зоны в видимой области.
            // Сохраняем каждую границу как линейку 0 уровня.
            // По пути запускаем рекурсии 1 уровня, в каждом сегменте значение в 10 раз больше.
            // (Для гиперполической зоны рекурсия особая.)
        }
        { // Одна из границ логарифмической зоны в видимой области.
            // Сохраняем границу как линейку 0 уровня.
            // Берём разницу значений между границами видимой области, но не более чем значение границы.
            // Берём логарифм и округляем вниз. Далее две рекурсии 1 уровня, значения одинаковые.
            // (Для гиперполической зоны рекурсия особая.)
        }
        // Ни одной из границ логарифмической зоны нет в видимой области.

        { // Мы в линейной или логарифмической зоне.
            // Берём разницу значений между границами видимой области.
            // Берём логарифм и округляем вниз. Далее рекурсия 0 уровня.
        }
        { // Мы в гиперболической зоне.
            // ...
        }

        return rulers;
    }

    private List<Ruler> GetLogRulers(int log, bool isPositive)
    {
        Check(log >= _currents.MinLog && log < _currents.MaxLog);

        var fromValue = isPositive ? Math.Pow(10, log) : -Math.Pow(10, log);
        var toValue = fromValue * 10;
        var toAbsValue = Math.Abs(toValue);
        var step = fromValue;

        var rulers = new List<Ruler>();
        var to = Ruler.FromWeight(this, toValue, weight: 1); // todo
        var from = to.GetRelative(fromValue);
        var prev = from;

        var count = 0;

        for (var value = from.Value + step; Math.Abs(value) <= toAbsValue; value += step)
        {
            rulers.Add(prev);

            var ruler = ++count == 4
                ? to.GetRelative(value)
                : prev.GetRelative(value);

            var needDeeper = Math.Abs(ruler.Coord - prev.Coord) >= _rulersMinCoordStepForDeep;

            if (needDeeper)
            {
                var deepRulers = isPositive
                    ? GetLinearRulers(minValue: prev.Value, maxValue: value)
                    : GetLinearRulers(minValue: value, maxValue: prev.Value);

                rulers.AddRange(deepRulers);
            }

            prev = ruler;
        }

        return rulers;
    }

    private List<Ruler> GetLinearRulers(double minValue, double maxValue)
    {
        Check(minValue < maxValue);

        var step = Math.Pow(10, Math.Ceiling(Math.Log10(maxValue - minValue)));
        var alignedMinValue = Math.Floor(minValue / step) * step;
        var alignedMaxValue = Math.Ceiling(maxValue / step) * step;
        var min = Ruler.FromWeight(this, alignedMinValue, weight: 1); // todo
        var max = Ruler.FromWeight(this, alignedMaxValue, weight: 1); // todo

        return GetLinearRulers(min: min, max: max, step: step);
    }

    private List<Ruler> GetLinearRulers(Ruler min, Ruler max, double step)
    {
        Check(min.Value < max.Value);

        var min_ = min;
        var max_ = max;
        var step_ = step;

        var rulers = new List<Ruler>();
        var needDeeper = true;

        while (needDeeper)
        {
            var count = 0;
            var weight1 = 0f;
            var weight5 = 0f;
            var nextMin = min_;
            var each1 = true;

            for (var value = min_.Value + step_; value < max_.Value; value += step_)
            {
                count++;

                if (count % 10 == 0)
                    continue;

                Ruler ruler;
                var needToAdd = true;

                if (count == 1)
                {
                    ruler = min_.GetRelative(value);
                    weight1 = ruler.Weight;

                    if (weight1 == 0)
                        break;

                    needDeeper = ruler.Coord - min_.Coord >= _rulersMinCoordStepForDeep;
                    each1 = ruler.Coord - min_.Coord >= _rulersMinCoordStepForDeep / 2;

                    if (!each1)
                        continue;
                }
                else if (count % 5 == 0)
                    ruler = Ruler.FromWeight(this, value, weight: weight5);
                else
                {
                    ruler = Ruler.FromWeight(this, value, weight: weight1);
                    needToAdd = each1;

                    if (count == (each1 ? 3 : 2))
                        weight5 = min_.GetRelative(value).Weight;
                }

                if (ruler.Coord <= -0.5)
                    nextMin = ruler;
                else
                if (ruler.Coord >= _maxViewCoord + 0.5)
                {
                    max_ = ruler;
                    break;
                }
                else if (needToAdd)
                    rulers.Add(ruler);
            }

            min_ = nextMin;
            step_ *= 0.1;
        }

        return rulers;
    }

    public sealed class Ruler
    {
        private Axis Axis { get; }
        public double Value { get; }
        public double Coord { get; }
        public float Weight { get; }
        public int ViewCoord { get; }

        public Ruler(Axis axis, double value, double coord, float weight)
        {
            Debug.Assert(weight >= 0 && weight <= 1);

            Axis = axis;
            Value = value;
            Coord = coord;
            Weight = weight;
            ViewCoord = axis.CoordToViewCoord(coord) ?? -1;
        }

        public bool IsVisible => ViewCoord != -1;

        public Ruler GetRelative(double value)
        {
            return FromRelativeCoord(axis: Axis, value: value, relativeCoord: Coord);
        }

        public Ruler GetNegative()
        {
            var value = -Value;
            var coord = Axis.ValueToCoord(value);
            return new Ruler(Axis, value: value, coord: coord, weight: Weight);
        }

        public static Ruler FromRelativeCoord(Axis axis, double value, double relativeCoord)
        {
            var coord = axis.ValueToCoord(value);
            var weight = MathF.Min((float)Math.Abs(coord - relativeCoord) / axis._maxViewCoord, 1);
            return new Ruler(axis, value: value, coord: coord, weight: weight);
        }

        public static Ruler FromWeight(Axis axis, double value, float weight)
        {
            var coord = axis.ValueToCoord(value);
            return new Ruler(axis, value: value, coord: coord, weight: weight);
        }
    }

    #endregion
}
