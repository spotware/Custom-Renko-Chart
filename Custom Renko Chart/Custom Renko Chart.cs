using cAlgo.API;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CustomRenkoChart : Indicator
    {
        #region Fields

        private const string Name = "Custom Renko Chart";

        private readonly List<string> _objectNames = new List<string>();

        private string _chartObjectNamesSuffix;

        private CustomOhlcBar _lastBar, _previousBar;

        private Color _bullishBarBodyColor, _bullishBarWickColor, _bearishBarBodyColor, _bearishBarWickColor;

        private bool _isChartTypeValid;

        private decimal _sizeInPips, _doubleSizeInPips;

        #endregion Fields

        #region Parameters

        [Parameter("Size(Pips)", DefaultValue = 10, Group = "General")]
        public int SizeInPips { get; set; }

        [Parameter("Bullish Bar Color", DefaultValue = "Lime", Group = "Body")]
        public string BullishBarBodyColor { get; set; }

        [Parameter("Bearish Bar Color", DefaultValue = "Red", Group = "Body")]
        public string BearishBarBodyColor { get; set; }

        [Parameter("Opacity", DefaultValue = 100, MinValue = 0, MaxValue = 255, Group = "Body")]
        public int BodyOpacity { get; set; }

        [Parameter("Thickness", DefaultValue = 1, Group = "Body")]
        public int BodyThickness { get; set; }

        [Parameter("Line Style", DefaultValue = LineStyle.Solid, Group = "Body")]
        public LineStyle BodyLineStyle { get; set; }

        [Parameter("Fill", DefaultValue = true, Group = "Body")]
        public bool FillBody { get; set; }

        [Parameter("Show", DefaultValue = true, Group = "Wicks")]
        public bool ShowWicks { get; set; }

        [Parameter("Bullish Bar Color", DefaultValue = "Lime", Group = "Wicks")]
        public string BullishBarWickColor { get; set; }

        [Parameter("Bearish Bar Color", DefaultValue = "Red", Group = "Wicks")]
        public string BearishBarWickColor { get; set; }

        [Parameter("Opacity", DefaultValue = 100, MinValue = 0, MaxValue = 255, Group = "Wicks")]
        public int WicksOpacity { get; set; }

        [Parameter("Thickness", DefaultValue = 2, Group = "Wicks")]
        public int WicksThickness { get; set; }

        [Parameter("Line Style", DefaultValue = LineStyle.Solid, Group = "Wicks")]
        public LineStyle WicksLineStyle { get; set; }

        [Parameter("Open", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsOpenOutputEnabled { get; set; }

        [Parameter("High", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsHighOutputEnabled { get; set; }

        [Parameter("Low", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsLowOutputEnabled { get; set; }

        [Parameter("Close", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsCloseOutputEnabled { get; set; }

        #endregion Parameters

        #region Other properties

        public ChartArea Area
        {
            get
            {
                return IndicatorArea ?? (ChartArea)Chart;
            }
        }

        #endregion Other properties

        #region Outputs

        [Output("Open", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries Open { get; set; }

        [Output("High", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries High { get; set; }

        [Output("Low", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries Low { get; set; }

        [Output("Close", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries Close { get; set; }

        #endregion Outputs

        #region Overridden methods

        protected override void Initialize()
        {
            _chartObjectNamesSuffix = string.Format("{0}_{1}", Name, DateTime.Now.Ticks);

            var timeFrame = Chart.TimeFrame.ToString();

            if (timeFrame.StartsWith("Renko", StringComparison.Ordinal) == false)
            {
                var name = string.Format("Error_{0}", _chartObjectNamesSuffix);

                var error = string.Format("{0} Error: Current chart is not a Renko chart, please switch to a Renko chart", Name);

                Area.DrawStaticText(name, error, VerticalAlignment.Center, HorizontalAlignment.Center, Color.Red);

                return;
            }
            else if (int.Parse(timeFrame.Substring(5), CultureInfo.InvariantCulture) > SizeInPips)
            {
                var name = string.Format("Error_{0}", _chartObjectNamesSuffix);

                var error = string.Format("{0} Error: You must use a smaller Ranko chart to build a larger one", Name);

                Area.DrawStaticText(name, error, VerticalAlignment.Center, HorizontalAlignment.Center, Color.Red);

                return;
            }
            else if (timeFrame.Equals(string.Format("Renko{0}", SizeInPips), StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            _isChartTypeValid = true;

            _bullishBarBodyColor = GetColor(BullishBarBodyColor, BodyOpacity);
            _bearishBarBodyColor = GetColor(BearishBarBodyColor, BodyOpacity);

            _bullishBarWickColor = GetColor(BullishBarWickColor, WicksOpacity);
            _bearishBarWickColor = GetColor(BearishBarWickColor, WicksOpacity);

            _sizeInPips = (decimal)(SizeInPips * Symbol.PipSize);
            _doubleSizeInPips = _sizeInPips * (decimal)2.0;
        }

        public override void Calculate(int index)
        {
            if (_isChartTypeValid == false) return;

            var time = Bars.OpenTimes[index];

            if (_lastBar == null)
            {
                ChangeLastBar(time, index);
            }

            UpdateLastBar(time, index);

            FillOutputs(index, _lastBar, _previousBar);

            var bodyRange = Math.Round(_lastBar.BodyRange, Symbol.Digits, MidpointRounding.AwayFromZero);

            if (_previousBar == null && bodyRange >= _sizeInPips)
            {
                ChangeLastBar(time, index);
            }
            else if (_previousBar != null)
            {
                if (_previousBar.Type == _lastBar.Type && bodyRange >= _sizeInPips)
                {
                    ChangeLastBar(time, index);
                }
                else if (_previousBar.Type != _lastBar.Type && bodyRange >= _doubleSizeInPips)
                {
                    ChangeLastBar(time, index);
                }
            }
        }

        #endregion Overridden methods

        #region Other methods

        private void FillOutputs(int index, CustomOhlcBar lastBar, CustomOhlcBar previousBar)
        {
            if (IsOpenOutputEnabled)
            {
                var open = previousBar == null || previousBar.Type == lastBar.Type ? lastBar.Open : previousBar.Open;

                Open[index] = decimal.ToDouble(open);
            }

            if (IsHighOutputEnabled)
            {
                High[index] = lastBar.High;
            }

            if (IsLowOutputEnabled)
            {
                Low[index] = lastBar.Low;
            }

            if (IsCloseOutputEnabled)
            {
                Close[index] = decimal.ToDouble(lastBar.Close);
            }
        }

        private Color GetColor(string colorString, int alpha = 255)
        {
            var color = colorString[0] == '#' ? Color.FromHex(colorString) : Color.FromName(colorString);

            return Color.FromArgb(alpha, color);
        }

        private void DrawBar(CustomOhlcBar lastBar, CustomOhlcBar previousBar)
        {
            string objectName = string.Format("{0}.{1}", lastBar.StartTime.Ticks, _chartObjectNamesSuffix);

            var barBodyColor = lastBar.Open > lastBar.Close ? _bearishBarBodyColor : _bullishBarBodyColor;

            var open = previousBar == null || previousBar.Type == lastBar.Type ? lastBar.Open : previousBar.Open;

            lastBar.Rectangle = Area.DrawRectangle(objectName, lastBar.StartTime, decimal.ToDouble(open), lastBar.EndTime, decimal.ToDouble(lastBar.Close), barBodyColor, BodyThickness, BodyLineStyle);

            lastBar.Rectangle.IsFilled = FillBody;

            if (ShowWicks)
            {
                string upperWickObjectName = string.Format("{0}.UpperWick", objectName);
                string lowerWickObjectName = string.Format("{0}.LowerWick", objectName);

                var barHalfTimeInMinutes = (_lastBar.Rectangle.Time2 - _lastBar.Rectangle.Time1).TotalMinutes / 2;
                var barCenterTime = _lastBar.Rectangle.Time1.AddMinutes(barHalfTimeInMinutes);

                if (lastBar.Open > lastBar.Close)
                {
                    Area.DrawTrendLine(upperWickObjectName, barCenterTime, _lastBar.Rectangle.Y1, barCenterTime, lastBar.High,
                        _bearishBarWickColor, WicksThickness, WicksLineStyle);
                    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, _lastBar.Rectangle.Y2, barCenterTime, lastBar.Low,
                        _bearishBarWickColor, WicksThickness, WicksLineStyle);
                }
                else
                {
                    Area.DrawTrendLine(upperWickObjectName, barCenterTime, _lastBar.Rectangle.Y2,
                        barCenterTime, lastBar.High, _bullishBarWickColor, WicksThickness, WicksLineStyle);
                    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, _lastBar.Rectangle.Y1, barCenterTime, lastBar.Low,
                        _bullishBarWickColor, WicksThickness, WicksLineStyle);
                }
            }

            if (!_objectNames.Contains(objectName))
            {
                _objectNames.Add(objectName);
            }
        }

        private void ChangeLastBar(DateTime time, int index)
        {
            if (_lastBar != null)
            {
                if (_previousBar != null)
                {
                    _lastBar.Open = _previousBar.Type == _lastBar.Type ? _previousBar.Close : _previousBar.Open;
                }

                DrawBar(_lastBar, _previousBar);
            }

            _previousBar = _lastBar;

            _lastBar = new CustomOhlcBar
            {
                StartTime = time,
                Open = _previousBar == null ? (decimal)Bars.OpenPrices[index] : _previousBar.Close
            };
        }

        private void UpdateLastBar(DateTime time, int index)
        {
            int startIndex = Bars.OpenTimes.GetIndexByTime(_lastBar.StartTime);

            _lastBar.Close = (decimal)Bars.ClosePrices[index];
            _lastBar.High = Maximum(Bars.HighPrices, startIndex, index);
            _lastBar.Low = Minimum(Bars.LowPrices, startIndex, index);
            _lastBar.Volume = Sum(Bars.TickVolumes, startIndex, index);
            _lastBar.EndTime = time;
        }

        private double Maximum(DataSeries dataSeries, int startIndex, int endIndex)
        {
            var max = double.NegativeInfinity;

            for (var i = startIndex; i <= endIndex; i++)
            {
                max = Math.Max(dataSeries[i], max);
            }

            return max;
        }

        public double Minimum(DataSeries dataSeries, int startIndex, int endIndex)
        {
            var min = double.PositiveInfinity;

            for (var i = startIndex; i <= endIndex; i++)
            {
                min = Math.Min(dataSeries[i], min);
            }

            return min;
        }

        public static double Sum(DataSeries dataSeries, int startIndex, int endIndex)
        {
            double sum = 0;

            for (var iIndex = startIndex; iIndex <= endIndex; iIndex++)
            {
                sum += dataSeries[iIndex];
            }

            return sum;
        }

        #endregion Other methods
    }

    public class CustomOhlcBar
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public ChartRectangle Rectangle { get; set; }

        public int Index { get; set; }

        public DateTime Time { get; set; }

        public decimal Open { get; set; }

        public double High { get; set; }

        public double Low { get; set; }

        public decimal Close { get; set; }

        public double Volume { get; set; }

        public BarType Type
        {
            get
            {
                if (Open < Close)
                {
                    return BarType.Bullish;
                }
                else if (Open > Close)
                {
                    return BarType.Bearish;
                }
                else
                {
                    return BarType.Neutral;
                }
            }
        }

        public double Range
        {
            get
            {
                return High - Low;
            }
        }

        public decimal BodyRange
        {
            get
            {
                return Math.Abs(Close - Open);
            }
        }
    }

    public enum ChartPeriodType
    {
        Time,
        Ticks,
        Renko,
        Range
    }

    public enum BarType
    {
        Bullish,
        Bearish,
        Neutral
    }
}