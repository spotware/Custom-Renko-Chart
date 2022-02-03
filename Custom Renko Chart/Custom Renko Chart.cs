using cAlgo.API;
using System;
using System.Globalization;
using System.Linq;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CustomRenkoChart : Indicator
    {
        #region Fields

        private const string Name = "Custom Renko Chart";

        private const string TimeFrameNamePrefix = "Renko";

        private string _chartObjectNamesSuffix;

        private CustomOhlcBar _lastBar, _previousBar;

        private Color _bullishBarBodyColor, _bullishBarWickColor, _bearishBarBodyColor, _bearishBarWickColor;

        private bool _isChartTypeValid;

        private decimal _sizeInPips, _doubleSizeInPips;

        private Bars _bars;

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

            var timeFrameName = Chart.TimeFrame.ToString();

            if (timeFrameName.StartsWith(TimeFrameNamePrefix, StringComparison.OrdinalIgnoreCase) == false)
            {
                var name = string.Format("Error_{0}", _chartObjectNamesSuffix);

                var error = string.Format("{0} Error: Current chart is not a Renko chart, please switch to a Renko chart", Name);

                Area.DrawStaticText(name, error, VerticalAlignment.Center, HorizontalAlignment.Center, Color.Red);

                return;
            }
            else if (Convert.ToInt32(timeFrameName.Substring(TimeFrameNamePrefix.Length), CultureInfo.InvariantCulture) > SizeInPips)
            {
                var name = string.Format("Error_{0}", _chartObjectNamesSuffix);

                var error = string.Format("{0} Error: Your current chart size must be smaller than your set size", Name);

                Area.DrawStaticText(name, error, VerticalAlignment.Center, HorizontalAlignment.Center, Color.Red);

                return;
            }
            else if (timeFrameName.Equals(string.Format("{0}{1}", TimeFrameNamePrefix, SizeInPips), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            else if (SizeInPips % Convert.ToInt32(timeFrameName.Substring(TimeFrameNamePrefix.Length), CultureInfo.InvariantCulture) == 0)
            {
                _bars = Bars;
            }
            else
            {
                var timeFrame = GetTimeFrame(SizeInPips, "renko");

                _bars = GetTimeFrameBars(timeFrame);
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

            var otherBarsIndex = _bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

            var time = _bars.OpenTimes[otherBarsIndex];

            if (_lastBar == null)
            {
                ChangeLastBar(time, otherBarsIndex);
            }

            for (int barIndex = _lastBar.Index; barIndex <= otherBarsIndex; barIndex++)
            {
                OnBar(_bars.OpenTimes[barIndex], barIndex, index);
            }

            DrawBar(_lastBar, _previousBar);
        }

        #endregion Overridden methods

        #region Other methods

        private void OnBar(DateTime time, int barIndex, int chartBarsIndex)
        {
            UpdateLastBar(time, barIndex);

            FillOutputs(chartBarsIndex, _lastBar, _previousBar);

            var bodyRange = Math.Round(_lastBar.BodyRange, Symbol.Digits, MidpointRounding.AwayFromZero);

            if (_previousBar == null && bodyRange >= _sizeInPips)
            {
                ChangeLastBar(time, barIndex);
            }
            else if (_previousBar != null)
            {
                if (_previousBar.Type == _lastBar.Type && bodyRange >= _sizeInPips)
                {
                    ChangeLastBar(time, barIndex);
                }
                else if (_previousBar.Type != _lastBar.Type && bodyRange >= _doubleSizeInPips)
                {
                    ChangeLastBar(time, barIndex);
                }
            }
        }

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
            string objectName = string.Format("{0}.{1}", lastBar.Id, _chartObjectNamesSuffix);

            var barBodyColor = lastBar.Open > lastBar.Close ? _bearishBarBodyColor : _bullishBarBodyColor;

            double open = double.NaN;

            if (previousBar == null || previousBar.Type == lastBar.Type)
            {
                open = decimal.ToDouble(lastBar.Open);
            }
            else if ((previousBar.Type == BarType.Bullish && lastBar.Close < _previousBar.Open) || (previousBar.Type == BarType.Bearish && lastBar.Close > _previousBar.Open))
            {
                open = decimal.ToDouble(previousBar.Open);
            }

            var close = decimal.ToDouble(lastBar.Close);

            if (open != double.NaN)
            {
                var bodyRectangle = Area.DrawRectangle(objectName, lastBar.StartTime, open, lastBar.EndTime, close, barBodyColor, BodyThickness, BodyLineStyle);

                bodyRectangle.IsFilled = FillBody;
            }

            if (ShowWicks)
            {
                string upperWickObjectName = string.Format("{0}.UpperWick", objectName);
                string lowerWickObjectName = string.Format("{0}.LowerWick", objectName);

                var barHalfTimeInMinutes = (lastBar.EndTime - lastBar.StartTime).TotalMinutes / 2;
                var barCenterTime = lastBar.StartTime.AddMinutes(barHalfTimeInMinutes);

                if (lastBar.Open > lastBar.Close)
                {
                    Area.DrawTrendLine(upperWickObjectName, barCenterTime, open, barCenterTime, lastBar.High,
                        _bearishBarWickColor, WicksThickness, WicksLineStyle);
                    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, close, barCenterTime, lastBar.Low,
                        _bearishBarWickColor, WicksThickness, WicksLineStyle);
                }
                else
                {
                    Area.DrawTrendLine(upperWickObjectName, barCenterTime, close,
                        barCenterTime, lastBar.High, _bullishBarWickColor, WicksThickness, WicksLineStyle);
                    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, open, barCenterTime, lastBar.Low,
                        _bullishBarWickColor, WicksThickness, WicksLineStyle);
                }
            }
        }

        private void ChangeLastBar(DateTime time, int index)
        {
            if (_lastBar != null && _previousBar != null)
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
                Id = _previousBar == null ? 0 : _previousBar.Id + 1,
                StartTime = time,
                Index = index,
                Open = _previousBar == null ? (decimal)_bars.OpenPrices[index] : _previousBar.Close
            };
        }

        private void UpdateLastBar(DateTime time, int index)
        {
            int startIndex = _bars.OpenTimes.GetIndexByTime(_lastBar.StartTime);

            _lastBar.Close = (decimal)_bars.ClosePrices[index];
            _lastBar.High = Maximum(_bars.HighPrices, startIndex, index);
            _lastBar.Low = Minimum(_bars.LowPrices, startIndex, index);
            _lastBar.EndTime = time;
            _lastBar.Index = index;
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

        private double Minimum(DataSeries dataSeries, int startIndex, int endIndex)
        {
            var min = double.PositiveInfinity;

            for (var i = startIndex; i <= endIndex; i++)
            {
                min = Math.Min(dataSeries[i], min);
            }

            return min;
        }

        private TimeFrame GetTimeFrame(int sizeInPips, string type)
        {
            var timeFrames = (from timeFrame in TimeFrame.GetType().GetFields()
                              where timeFrame.Name.StartsWith(type, StringComparison.OrdinalIgnoreCase)
                              let timeFrameSize = Convert.ToInt32(timeFrame.Name.Substring(type.Length))
                              where timeFrameSize <= sizeInPips && sizeInPips % timeFrameSize == 0
                              orderby timeFrameSize descending
                              select new Tuple<TimeFrame, int>(timeFrame.GetValue(null) as TimeFrame, timeFrameSize)).ToArray();

            var bestFitTimeFrame = timeFrames.FirstOrDefault(timeFrame => timeFrame.Item2 <= sizeInPips && sizeInPips % timeFrame.Item2 == 0);

            if (bestFitTimeFrame != null) return bestFitTimeFrame.Item1;

            var smallestTimeFrame = timeFrames.LastOrDefault();

            if (smallestTimeFrame != null) return smallestTimeFrame.Item1;

            throw new InvalidOperationException(string.Format("Couldn't find a proper time frame for your provided size ({0} Pips) and type ({1}).", sizeInPips, type));
        }

        private Bars GetTimeFrameBars(TimeFrame timeFrame)
        {
            var bars = MarketData.GetBars(timeFrame);

            if (bars.Count == 0)
            {
                throw new InvalidOperationException(string.Format("Couldn't load the {0} time frame bars", timeFrame));
            }

            var numberOfLoadedBars = bars.Count;

            while (numberOfLoadedBars > 0 && bars[0].OpenTime > Bars[0].OpenTime)
            {
                numberOfLoadedBars = bars.LoadMoreHistory();
            }

            return bars;
        }

        #endregion Other methods
    }

    public class CustomOhlcBar
    {
        public int Id { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int Index { get; set; }

        public decimal Open { get; set; }

        public double High { get; set; }

        public double Low { get; set; }

        public decimal Close { get; set; }

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

        public decimal BodyRange
        {
            get
            {
                return Math.Abs(Close - Open);
            }
        }
    }

    public enum BarType
    {
        Bullish,
        Bearish,
        Neutral
    }
}