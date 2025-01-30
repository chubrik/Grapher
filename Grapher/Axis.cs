namespace Chubrik.Grapher;

using System.Diagnostics;
using static ValidationHelper;

internal sealed class Axis
{
    private readonly int _viewAreaSize;
    private readonly int _maxViewCoord;
    private readonly Data _currents;
    private Data _defaults;
    private readonly Data _limits;

    private readonly double _minLog;
    private readonly double _linear_MaxValue;
    private readonly double _exp_MaxValue;

    private readonly double _negInf_MinCoord;
    private readonly double _negExp_MinCoord;
    private readonly double _linear_MinCoord;
    private readonly double _linear_MaxCoord;
    private readonly double _posExp_MaxCoord;
    private readonly double _posInf_MaxCoord;

    private readonly double _valueToCoord;
    private readonly double _coordToValue;
    private readonly double _logToCoord;
    private readonly double _coordToLog;
    private readonly double _inf_MinValue_Mul_CoordDiff;

    public int ViewAreaSize => _viewAreaSize;
    public int MaxViewCoord => _maxViewCoord;
    public double MinCoord => _negInf_MinCoord;
    public double MaxCoord => _posInf_MaxCoord;
    public double MinValueLimit => _limits.MinValue;
    public double MaxValueLimit => _limits.MaxValue;

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

        var minExpValue = Math.Pow(10, _currents.MinExp);
        var maxExpValue = Math.Pow(10, _currents.MaxExp);

        _minLog = _currents.MinExp;
        _linear_MaxValue = minExpValue;
        _exp_MaxValue = maxExpValue;

        // Pre Init

        var eMulExpDiff = Math.E * (_currents.MaxExp - _currents.MinExp);

        _negInf_MinCoord = -2 - eMulExpDiff;
        _negExp_MinCoord = -1 - eMulExpDiff;
        _linear_MinCoord = -1;
        _linear_MaxCoord = 1;
        _posExp_MaxCoord = 1 + eMulExpDiff;
        _posInf_MaxCoord = 2 + eMulExpDiff;

        _valueToCoord = 1 / minExpValue;
        _coordToValue = minExpValue;
        _logToCoord = Math.E;
        _coordToLog = 1 / Math.E;
        _inf_MinValue_Mul_CoordDiff = _exp_MaxValue;

        // Pre Calc

        var preMinCoord = ValueToCoord_Private(_currents.MinValue);
        var preMaxCoord = ValueToCoord_Private(_currents.MaxValue);
        Check(preMinCoord >= _negInf_MinCoord);
        Check(preMaxCoord <= _posInf_MaxCoord);
        Check(preMinCoord < preMaxCoord);

        var preCoordDiff = preMaxCoord - preMinCoord;
        var coordMultiplier = _maxViewCoord / preCoordDiff;
        var coordShift = -preMinCoord * coordMultiplier;

        // Final Init

        _negInf_MinCoord = _negInf_MinCoord * coordMultiplier + coordShift;
        _negExp_MinCoord = _negExp_MinCoord * coordMultiplier + coordShift;
        _linear_MinCoord = _linear_MinCoord * coordMultiplier + coordShift;
        _linear_MaxCoord = _linear_MaxCoord * coordMultiplier + coordShift;
        _posExp_MaxCoord = _posExp_MaxCoord * coordMultiplier + coordShift;
        _posInf_MaxCoord = _posInf_MaxCoord * coordMultiplier + coordShift;

        _valueToCoord *= coordMultiplier;
        _coordToValue /= coordMultiplier;
        _logToCoord *= coordMultiplier;
        _coordToLog /= coordMultiplier;
        _inf_MinValue_Mul_CoordDiff *= coordMultiplier;
    }

    public int? ValueToCoord(double value)
    {
        Check(!double.IsNaN(value));
        var coord = ValueToCoord_Private(value);
        var roundedCoord = (int)Math.Round(coord);

        if (roundedCoord >= 0 && roundedCoord <= _maxViewCoord)
            return roundedCoord;

        return null;
    }

    private double ValueToCoord_Private(double value)
    {
        Debug.Assert(!double.IsNaN(value));

        double coord;

        if (value < -_exp_MaxValue)
        {
            // Negative hyperbolic zone
            var coordRelative = -_inf_MinValue_Mul_CoordDiff / value;
            coord = _negInf_MinCoord + coordRelative;
        }
        else if (value < -_linear_MaxValue)
        {
            // Negative exponential zone
            var log = Math.Log10(-value);
            var logRelative = log - _minLog;
            var coordRelative = logRelative * _logToCoord;
            coord = _linear_MinCoord - coordRelative;
        }
        else if (value <= _linear_MaxValue)
        {
            // Linear zone
            var valueRelative = value + _linear_MaxValue;
            var coordRelative = valueRelative * _valueToCoord;
            coord = _linear_MinCoord + coordRelative;
        }
        else if (value <= _exp_MaxValue)
        {
            // Positive exponential zone
            var log = Math.Log10(value);
            var logRelative = log - _minLog;
            var coordRelative = logRelative * _logToCoord;
            coord = _linear_MaxCoord + coordRelative;
        }
        else
        {
            // Positive hyperbolic zone
            var coordRelative = _inf_MinValue_Mul_CoordDiff / value;
            coord = _posInf_MaxCoord - coordRelative;
        }

        Debug.Assert(coord >= _negInf_MinCoord && coord <= _posInf_MaxCoord);
        return coord;
    }

    public double CoordToValue(double coord)
    {
        double value;

        if (coord < _negExp_MinCoord)
        {
            // Negative hyperbolic zone
            if (coord <= _negInf_MinCoord) return double.NegativeInfinity;
            var coordRelative = coord - _negInf_MinCoord;
            value = -_inf_MinValue_Mul_CoordDiff / coordRelative;
        }
        else if (coord < _linear_MinCoord)
        {
            // Negative exponential zone
            var coordRelative = _linear_MinCoord - coord;
            var logRelative = coordRelative * _coordToLog;
            var log = _minLog + logRelative;
            value = -Math.Pow(10, log);
        }
        else if (coord <= _linear_MaxCoord)
        {
            // Linear zone
            var coordRelative = coord - _linear_MinCoord;
            var valueRelative = coordRelative * _coordToValue;
            value = valueRelative - _linear_MaxValue;
        }
        else if (coord <= _posExp_MaxCoord)
        {
            // Positive exponential zone
            var coordRelative = coord - _linear_MaxCoord;
            var logRelative = coordRelative * _coordToLog;
            var log = _minLog + logRelative;
            value = Math.Pow(10, log);
        }
        else
        {
            // Positive hyperbolic zone
            if (coord >= _posInf_MaxCoord) return double.PositiveInfinity;
            var coordRelative = _posInf_MaxCoord - coord;
            value = _inf_MinValue_Mul_CoordDiff / coordRelative;
        }

        Debug.Assert(!double.IsNaN(value));
        return value;
    }

    #region Tools

    public bool Equals(Axis other)
    {
        if (_viewAreaSize != other._viewAreaSize) return false;
        if (_currents != other._currents) return false;
        if (_defaults != other._defaults) return false;
        if (_limits != other._limits) return false;
        return true;
    }

    public static Axis FromViewAreaSize(int viewAreaSize)
    {
        const int defaultMinExp = 0;
        const int defaultMaxExp = 6;
        const int defaultAbsExpLimit = 300;

        var currents = new Data(
            minExp: defaultMinExp,
            maxExp: defaultMaxExp,
            minValue: double.NegativeInfinity,
            maxValue: double.PositiveInfinity);

        var defaults = currents;

        var limits = new Data(
            minExp: -defaultAbsExpLimit,
            maxExp: defaultAbsExpLimit,
            minValue: double.NegativeInfinity,
            maxValue: double.PositiveInfinity);

        return new Axis(
            viewAreaSize: viewAreaSize,
            currents: currents,
            defaults: defaults,
            limits: limits);
    }

    public Axis WithMeasures(Measures measures)
    {
        var minExp = _currents.MinExp;
        var maxExp = _currents.MaxExp;
        var minValue = _currents.MinValue;
        var maxValue = _currents.MaxValue;
        var minValueLimit = measures.MinValueLimit ?? _limits.MinValue;
        var maxValueLimit = measures.MaxValueLimit ?? _limits.MaxValue;

        if (measures.MinExp != null)
            minExp = Math.Max(measures.MinExp.Value, _limits.MinExp);

        if (measures.MaxExp != null)
            maxExp = Math.Min(measures.MaxExp.Value, _limits.MaxExp);

        if (measures.MinValue != null)
            minValue = Math.Max(measures.MinValue.Value, minValueLimit);

        if (measures.MaxValue != null)
            maxValue = Math.Min(measures.MaxValue.Value, maxValueLimit);

        var currents = new Data(
            minExp: minExp,
            maxExp: maxExp,
            minValue: minValue,
            maxValue: maxValue);

        var defaults = currents;

        var limits = new Data(
            minExp: _limits.MinExp,
            maxExp: _limits.MaxExp,
            minValue: minValueLimit,
            maxValue: maxValueLimit);

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

    public Axis WithCoords(double tryMinCoord, double tryMaxCoord)
    {
        if (tryMinCoord == 0 && tryMaxCoord == _maxViewCoord)
            return this;

        var minCoord = tryMinCoord;
        var maxCoord = tryMaxCoord;

        var minCoordLimit = _limits.MinValue != double.NegativeInfinity ? ValueToCoord_Private(_limits.MinValue) : _negInf_MinCoord;
        var maxCoordLimit = _limits.MaxValue != double.PositiveInfinity ? ValueToCoord_Private(_limits.MaxValue) : _posInf_MaxCoord;

        if (tryMinCoord < minCoordLimit)
        {
            minCoord = minCoordLimit;
            maxCoord = Math.Min(tryMaxCoord + (minCoordLimit - tryMinCoord), maxCoordLimit);
        }
        else if (tryMaxCoord > maxCoordLimit)
        {
            minCoord = Math.Max(tryMinCoord - (tryMaxCoord - maxCoordLimit), minCoordLimit);
            maxCoord = maxCoordLimit;
        }

        var minViewValue = CoordToValue(minCoord);
        var maxViewValue = CoordToValue(maxCoord);

        var currents = new Data(
            minExp: _currents.MinExp,
            maxExp: _currents.MaxExp,
            minValue: minViewValue,
            maxValue: maxViewValue);

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

    public Axis WithMinExp(int tryMinExpDiff)
    {
        var minExp = _currents.MinExp + tryMinExpDiff;

        if (minExp < _limits.MinExp)
            return this;

        var maxExp = _currents.MaxExp;

        if (maxExp < minExp)
            maxExp = minExp;

        var currents = new Data(
            minExp: minExp,
            maxExp: maxExp,
            minValue: _currents.MinValue,
            maxValue: _currents.MaxValue);

        return new Axis(
            viewAreaSize: _viewAreaSize,
            currents: currents,
            defaults: _defaults,
            limits: _limits);
    }

    public Axis WithMaxExp(int tryMaxExpDiff)
    {
        var maxExp = _currents.MaxExp + tryMaxExpDiff;

        if (maxExp > _limits.MaxExp)
            return this;

        var minExp = _currents.MinExp;

        if (minExp > maxExp)
            minExp = maxExp;

        var currents = new Data(
            minExp: minExp,
            maxExp: maxExp,
            minValue: _currents.MinValue,
            maxValue: _currents.MaxValue);

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

    public readonly struct Data
    {
        public readonly int MinExp;
        public readonly int MaxExp;
        public readonly double MinValue;
        public readonly double MaxValue;

        public Data(int minExp, int maxExp, double minValue, double maxValue)
        {
            Check(minExp <= maxExp);
            Check(minValue < maxValue);

            MinExp = minExp;
            MaxExp = maxExp;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public bool IsInLimits(Data limits)
        {
            if (MinExp < limits.MinExp) return false;
            if (MaxExp > limits.MaxExp) return false;
            if (MinValue < limits.MinValue) return false;
            if (MaxValue > limits.MaxValue) return false;
            return true;
        }

        public static bool operator ==(Data left, Data right)
        {
            if (left.MinExp != right.MinExp) return false;
            if (left.MaxExp != right.MaxExp) return false;
            if (left.MinValue != right.MinValue) return false;
            if (left.MaxValue != right.MaxValue) return false;
            return true;
        }

        public static bool operator !=(Data left, Data right) => !(left == right);
        public override bool Equals(object? obj) => obj is Data data && this == data;
        public override int GetHashCode() => HashCode.Combine(MinExp, MaxExp, MinValue, MaxValue);
    }
}
