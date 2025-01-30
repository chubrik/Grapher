namespace Chubrik.Grapher;

using System.Diagnostics;
using static ValidationHelper;

internal sealed class Axis
{
    private const int _expIndexLimit = 300;

    private readonly int _viewAreaSize;
    private readonly int _maxViewCoord;
    private readonly int _minExpIndex;
    private readonly int _maxExpIndex;
    private readonly double _minViewValue;
    private readonly double _maxViewValue;
    private readonly double _minValueLimit;
    private readonly double _maxValueLimit;

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
    public int MinExpIndex => _minExpIndex;
    public int MaxExpIndex => _maxExpIndex;
    public double MinViewValue => _minViewValue;
    public double MaxViewValue => _maxViewValue;
    public double MinValueLimit => _minValueLimit;
    public double MaxValueLimit => _maxValueLimit;

    public Axis(
        int viewAreaSize,
        int minExpIndex,
        int maxExpIndex,
        double tryMinViewValue,
        double tryMaxViewValue,
        double minValueLimit,
        double maxValueLimit)
    {
        Check(viewAreaSize >= 2);
        Check(minExpIndex >= -_expIndexLimit);
        Check(maxExpIndex <= _expIndexLimit);
        Check(minExpIndex <= maxExpIndex);
        Check(tryMinViewValue < tryMaxViewValue);
        Check(minValueLimit < maxValueLimit);

        var minViewValue = Math.Max(tryMinViewValue, minValueLimit);
        var maxViewValue = Math.Min(tryMaxViewValue, maxValueLimit);

        _viewAreaSize = viewAreaSize;
        _maxViewCoord = viewAreaSize - 1;
        _minExpIndex = minExpIndex;
        _maxExpIndex = maxExpIndex;
        _minViewValue = minViewValue;
        _maxViewValue = maxViewValue;
        _minValueLimit = minValueLimit;
        _maxValueLimit = maxValueLimit;

        var minExpValue = Math.Pow(10, minExpIndex);
        var maxExpValue = Math.Pow(10, maxExpIndex);

        _minLog = minExpIndex;
        _linear_MaxValue = minExpValue;
        _exp_MaxValue = maxExpValue;

        // Pre Init

        var eMulExpIndexDiff = Math.E * (maxExpIndex - minExpIndex);

        _negInf_MinCoord = -2 - eMulExpIndexDiff;
        _negExp_MinCoord = -1 - eMulExpIndexDiff;
        _linear_MinCoord = -1;
        _linear_MaxCoord = 1;
        _posExp_MaxCoord = 1 + eMulExpIndexDiff;
        _posInf_MaxCoord = 2 + eMulExpIndexDiff;

        _valueToCoord = 1 / minExpValue;
        _coordToValue = minExpValue;
        _logToCoord = Math.E;
        _coordToLog = 1 / Math.E;
        _inf_MinValue_Mul_CoordDiff = _exp_MaxValue;

        // Pre Calc

        var preMinCoord = ValueToCoord_Private(minViewValue);
        var preMaxCoord = ValueToCoord_Private(maxViewValue);
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

    public bool IsEqual(Axis other)
    {
        if (_viewAreaSize != other._viewAreaSize) return false;
        if (_minExpIndex != other._minExpIndex) return false;
        if (_maxExpIndex != other._maxExpIndex) return false;
        if (_minViewValue != other._minViewValue) return false;
        if (_maxViewValue != other._maxViewValue) return false;
        if (_minValueLimit != other._minValueLimit) return false;
        if (_maxValueLimit != other._maxValueLimit) return false;
        return true;
    }

    public Axis WithViewAreaSize(int viewAreaSize)
    {
        try
        {
            return new(
                viewAreaSize: viewAreaSize,
                minExpIndex: _minExpIndex,
                maxExpIndex: _maxExpIndex,
                tryMinViewValue: _minViewValue,
                tryMaxViewValue: _maxViewValue,
                minValueLimit: _minValueLimit,
                maxValueLimit: _maxValueLimit);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message);
            return this;
        }
    }

    public Axis WithCoords(double tryMinCoord, double tryMaxCoord)
    {
        var minCoord = tryMinCoord;
        var maxCoord = tryMaxCoord;

        var minCoordLimit = _minValueLimit != double.NegativeInfinity ? ValueToCoord_Private(_minValueLimit) : _negInf_MinCoord;
        var maxCoordLimit = _maxValueLimit != double.PositiveInfinity ? ValueToCoord_Private(_maxValueLimit) : _posInf_MaxCoord;

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

        try
        {
            return new(
                viewAreaSize: _viewAreaSize,
                minExpIndex: _minExpIndex,
                maxExpIndex: _maxExpIndex,
                tryMinViewValue: minViewValue,
                tryMaxViewValue: maxViewValue,
                minValueLimit: _minValueLimit,
                maxValueLimit: _maxValueLimit);
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message);
            return this;
        }
    }

    public Axis WithMinExp(int tryMinExpDiff)
    {
        var minExp = _minExpIndex + tryMinExpDiff;

        if (Math.Abs(minExp) > _expIndexLimit)
            return this;

        var maxExp = _maxExpIndex;

        if (maxExp < minExp)
            maxExp = minExp;

        return new(
            viewAreaSize: _viewAreaSize,
            minExpIndex: minExp,
            maxExpIndex: maxExp,
            tryMinViewValue: _minViewValue,
            tryMaxViewValue: _maxViewValue,
            minValueLimit: _minValueLimit,
            maxValueLimit: _maxValueLimit);
    }

    public Axis WithMaxExp(int tryMaxExpDiff)
    {
        var maxExp = _maxExpIndex + tryMaxExpDiff;

        if (Math.Abs(maxExp) > _expIndexLimit)
            return this;

        var minExp = _minExpIndex;

        if (minExp > maxExp)
            minExp = maxExp;

        return new(
            viewAreaSize: _viewAreaSize,
            minExpIndex: minExp,
            maxExpIndex: maxExp,
            tryMinViewValue: _minViewValue,
            tryMaxViewValue: _maxViewValue,
            minValueLimit: _minValueLimit,
            maxValueLimit: _maxValueLimit);
    }

    #endregion
}
