using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.PresentationLayer.Renderers.Chart;

namespace VisibilityAwareIndicator
{
    public class VisibilityAwareIndicator : Indicator
    {
        [InputParameter("Period", 0, 1, 500, 1, 0)]
        public int Period = 5;

        [InputParameter("Above Color")]
        public Color AboveColor = Color.Lime;

        [InputParameter("Below Color")]
        public Color BelowColor = Color.Magenta;

        private readonly object _lock = new();
        private DateTime _lastVisibilityCheck = DateTime.MinValue;
        private bool _wasVisible = true;

        public VisibilityAwareIndicator()
        {
            Name = "Visibility Aware Indicator";
            AddLineSeries("MA", Color.Cyan, 2, LineStyle.Solid);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (Count < Period)
                return;

            double ma = CalculateMA();
            SetValue(ma);

            var isVisible = CheckVisibility();
            if (_wasVisible && !isVisible)
                RestoreAllBars();
            _wasVisible = isVisible;

            if (!isVisible)
                return;

            double close = Close();
            Color targetColor = close > ma ? AboveColor : BelowColor;
            SetBarAppearance(
                new IndicatorBarAppearance
                {
                    BarColor = targetColor,
                    BorderColor = targetColor,
                    WickColor = targetColor,
                },
                0
            );
        }

        private bool CheckVisibility()
        {
            var chartWindow = CurrentChart.MainWindow as ChartWindow;
            var indicators = chartWindow.IndicatorStorageRenderer.Indicators;
            var myRenderer = indicators.FirstOrDefault(ir => ir.Indicator == this);
            bool isVisible = myRenderer?.Visible ?? true;

            if ((DateTime.UtcNow - _lastVisibilityCheck).TotalSeconds < 1)
                Core.Instance.Loggers.Log($"visibility:{isVisible}");
            _lastVisibilityCheck = DateTime.UtcNow;

            return isVisible;
        }

        private double CalculateMA()
        {
            double sum = 0;
            for (int i = 0; i < Period; i++)
                sum += GetPrice(PriceType.Close, i);
            return sum / Period;
        }

        private void RestoreAllBars()
        {
            if (CurrentChart is not Chart chart)
                return;

            var dataStyle = chart.Settings.FirstOrDefault(s => s.Name == "Data style")?.Value;
            // var bodyColor = (dataStyle as SettingItem)
            //     ?.FirstOrDefault(bc => bc.Name == "bodyColor")
            //     .Value;
            if (dataStyle is not PairColor bodyColor)
                return;

            for (int i = 0; i < Count; i++)
            {
                // Use a simple ternary to pick the color
                var color = (Close(i) >= Open(i)) ? bodyColor.Color1 : bodyColor.Color2;

                SetBarAppearance(
                    new IndicatorBarAppearance
                    {
                        BarColor = color,
                        BorderColor = color,
                        WickColor = color,
                    },
                    i
                );
            }
        }

        protected override void OnClear()
        {
            RestoreAllBars();
        }
    }
}
