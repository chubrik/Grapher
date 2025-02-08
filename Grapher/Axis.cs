namespace Chubrik.Grapher;

using System.Diagnostics;
using static ValidationHelper;

internal sealed class Axis
{
    #region Common

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

        var maxLinearValue = Math.Pow(10, _currents.MinLog);
        var maxLogValue = Math.Pow(10, _currents.MaxLog);

        _minLogDouble = _currents.MinLog;
        _maxLinearValue = maxLinearValue;
        _maxLogValue = maxLogValue;

        // Pre Init

        var eMulLogDiff = Math.E * (_currents.MaxLog - _currents.MinLog);

        _minCoord /* */ = -2 - eMulLogDiff;
        _minNegLogCoord = -1 - eMulLogDiff;
        _minLinearCoord = -1;
        _maxLinearCoord = 1;
        _maxPosLogCoord = 1 + eMulLogDiff;
        _maxCoord /* */ = 2 + eMulLogDiff;

        _valueToCoord = 1 / maxLinearValue;
        _coordToValue = maxLinearValue;
        _logToCoord = Math.E;
        _coordToLog = 1 / Math.E;
        _maxLogValue_Mul_CoordDiff = _maxLogValue;

        // Pre Calc

        var preMinViewCoord = ValueToCoord(_currents.MinViewValue);
        var preMaxViewCoord = ValueToCoord(_currents.MaxViewValue);
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
        if (_currents != other._currents) return false;
        if (_defaults != other._defaults) return false;
        if (_limits != other._limits) return false;
        return true;
    }

    public readonly struct Data
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

        public static bool operator ==(Data left, Data right)
        {
            if (left.MinLog != right.MinLog) return false;
            if (left.MaxLog != right.MaxLog) return false;
            if (left.MinViewValue != right.MinViewValue) return false;
            if (left.MaxViewValue != right.MaxViewValue) return false;
            return true;
        }

        public static bool operator !=(Data left, Data right) => !(left == right);
        public override bool Equals(object? obj) => obj is Data data && this == data;
        public override int GetHashCode() => HashCode.Combine(MinLog, MaxLog, MinViewValue, MaxViewValue);
    }

    #endregion

    #region Creation

    public static Axis FromViewAreaSize(int viewAreaSize)
    {
        const int initMinLog = 0;
        const int initMaxLog = 6;
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
        var minLog = _currents.MinLog;
        var maxLog = _currents.MaxLog;
        var minValue = _currents.MinViewValue;
        var maxValue = _currents.MaxViewValue;
        var minValueLimit = measures.MinValueLimit ?? _limits.MinViewValue;
        var maxValueLimit = measures.MaxValueLimit ?? _limits.MaxViewValue;

        if (measures.MinLog != null)
            minLog = Math.Max(measures.MinLog.Value, _limits.MinLog);

        if (measures.MaxLog != null)
            maxLog = Math.Min(measures.MaxLog.Value, _limits.MaxLog);

        if (measures.MinViewValue != null)
            minValue = Math.Max(measures.MinViewValue.Value, minValueLimit);

        if (measures.MaxViewValue != null)
            maxValue = Math.Min(measures.MaxViewValue.Value, maxValueLimit);

        var currents = new Data(
            minLog: minLog,
            maxLog: maxLog,
            minViewValue: minValue,
            maxViewValue: maxValue);

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

        var minCoordLimit = _limits.MinViewValue != double.NegativeInfinity ? ValueToCoord(_limits.MinViewValue) : _minCoord;
        var maxCoordLimit = _limits.MaxViewValue != double.PositiveInfinity ? ValueToCoord(_limits.MaxViewValue) : _maxCoord;

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
            minViewValue: minViewValue,
            maxViewValue: maxViewValue);

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

        var maxLog = _currents.MaxLog;

        if (maxLog < minLog)
            maxLog = minLog;

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

        var minLog = _currents.MinLog;

        if (minLog > maxLog)
            minLog = maxLog;

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
        if (_currents == _defaults)
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

    public int? ValueToViewCoord(double value)
    {
        Check(!double.IsNaN(value));
        var coord = ValueToCoord(value);
        var roundedCoord = (int)Math.Round(coord);

        if (roundedCoord >= 0 && roundedCoord <= _maxViewCoord)
            return roundedCoord;

        return null;
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
}
