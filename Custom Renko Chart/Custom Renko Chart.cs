using cAlgo.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class CustomPeriodChart : Indicator
    {
        #region Fields

        private const string Name = "Custom Period Chart";

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

        [Parameter("Transparency", DefaultValue = 100, MinValue = 0, MaxValue = 255, Group = "Body")]
        public int BodyTransparency { get; set; }

        [Parameter("Thickness", DefaultValue = 1, Group = "Body")]
        public int BodyThickness { get; set; }

        [Parameter("Line Style", DefaultValue = LineStyle.Solid, Group = "Body")]
        public LineStyle BodyLineStyle { get; set; }

        [Parameter("Fill", DefaultValue = true, Group = "Body")]
        public bool FillBody { get; set; }

        [Parameter("Bullish Bar Color", DefaultValue = "Lime", Group = "Wicks")]
        public string BullishBarWickColor { get; set; }

        [Parameter("Bearish Bar Color", DefaultValue = "Red", Group = "Wicks")]
        public string BearishBarWickColor { get; set; }

        [Parameter("Transparency", DefaultValue = 100, MinValue = 0, MaxValue = 255, Group = "Wicks")]
        public int WicksTransparency { get; set; }

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

        #region Other properties

        public ChartArea Area
        {
            get
            {
                return IndicatorArea ?? (ChartArea)Chart;
            }
        }

        #endregion Other properties

        #region Overridden methods

        protected override void Initialize()
        {
            if (Chart.TimeFrame.ToString().StartsWith("Renko", StringComparison.Ordinal) == false)
            {
                Print("Current chart is not a Renko chart, please switch to a Renko chart");

                return;
            }

            _isChartTypeValid = true;

            _chartObjectNamesSuffix = string.Format("{0}_{1}", Name, DateTime.Now.Ticks);

            _bullishBarBodyColor = GetColor(BullishBarBodyColor, BodyTransparency);
            _bearishBarBodyColor = GetColor(BearishBarBodyColor, BodyTransparency);

            _bullishBarWickColor = GetColor(BullishBarWickColor, WicksTransparency);
            _bearishBarWickColor = GetColor(BearishBarWickColor, WicksTransparency);

            _sizeInPips = (decimal)(SizeInPips * Symbol.PipSize);
            _doubleSizeInPips = _sizeInPips * (decimal)2.0;
        }

        public override void Calculate(int index)
        {
            if (index < 900) return;

            if (index == 900)
            {
                Chart.DrawVerticalLine("vert", index, Color.Red);
            }

            if (_isChartTypeValid == false) return;

            var time = Bars.OpenTimes[index];

            if (_lastBar == null)
            {
                ChangeLastBar(time, index);
            }

            UpdateLastBar(time, index);

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

            if (_lastBar != null)
            {
                FillOutputs(index, _lastBar);
            }
        }

        #endregion Overridden methods

        #region Other methods

        private Color GetColor(string colorString, int alpha = 255)
        {
            var color = colorString[0] == '#' ? Color.FromHex(colorString) : Color.FromName(colorString);

            return Color.FromArgb(alpha, color);
        }

        private void DrawBar(int index, CustomOhlcBar lastBar, CustomOhlcBar previousBar)
        {
            Chart.DrawVerticalLine(index.ToString(), index, Color.Yellow);

            string objectName = string.Format("{0}.{1}", lastBar.StartTime.Ticks, _chartObjectNamesSuffix);

            var barBodyColor = lastBar.Open > lastBar.Close ? _bearishBarBodyColor : _bullishBarBodyColor;

            var open = previousBar == null || previousBar.Type == lastBar.Type ? lastBar.Open : previousBar.Open;

            lastBar.Rectangle = Area.DrawRectangle(objectName, lastBar.StartTime, decimal.ToDouble(open), lastBar.EndTime, decimal.ToDouble(lastBar.Close), barBodyColor, BodyThickness, BodyLineStyle);

            lastBar.Rectangle.IsFilled = FillBody;

            string upperWickObjectName = string.Format("{0}.UpperWick", objectName);
            string lowerWickObjectName = string.Format("{0}.LowerWick", objectName);

            //var barHalfTimeInMinutes = (_lastBar.Rectangle.Time2 - _lastBar.Rectangle.Time1).TotalMinutes / 2;
            //var barCenterTime = _lastBar.Rectangle.Time1.AddMinutes(barHalfTimeInMinutes);

            //if (bar.Open > bar.Close)
            //{
            //    Area.DrawTrendLine(upperWickObjectName, barCenterTime, _lastBar.Rectangle.Y1, barCenterTime, bar.High,
            //        _bearishBarWickColor, WicksThickness, WicksLineStyle);
            //    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, _lastBar.Rectangle.Y2, barCenterTime, bar.Low,
            //        _bearishBarWickColor, WicksThickness, WicksLineStyle);
            //}
            //else
            //{
            //    Area.DrawTrendLine(upperWickObjectName, barCenterTime, _lastBar.Rectangle.Y2,
            //        barCenterTime, bar.High, _bullishBarWickColor, WicksThickness, WicksLineStyle);
            //    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, _lastBar.Rectangle.Y1, barCenterTime, bar.Low,
            //        _bullishBarWickColor, WicksThickness, WicksLineStyle);
            //}

            if (!_objectNames.Contains(objectName))
            {
                _objectNames.Add(objectName);
            }
        }

        private void FillOutputs(int index, CustomOhlcBar bar)
        {
            if (IsOpenOutputEnabled)
            {
                Open[index] = bar.Rectangle.Y1;
            }

            if (IsHighOutputEnabled)
            {
                High[index] = bar.High;
            }

            if (IsLowOutputEnabled)
            {
                Low[index] = bar.Low;
            }

            if (IsCloseOutputEnabled)
            {
                Close[index] = bar.Rectangle.Y2;
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

                DrawBar(index, _lastBar, _previousBar);
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