using System;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    // Experimental, exit-only reversal variant. Compile in NinjaTrader 8 before simulation.
    public class MES_Bounce_RiskManaged : Strategy
    {
        private Swing swing;
        private EMA fast, slow;
        private ADX adx;
        private DateTime sessionDate = DateTime.MinValue;
        private double startRealized, stopPrice, peak, trough;
        private MarketPosition lastSide = MarketPosition.Flat;
        private bool dailyHalt;

        [NinjaScriptProperty, Range(1, 100)]
        public int SwingStrength { get; set; }
        [NinjaScriptProperty, Range(1, 100)]
        public int FastEma { get; set; }
        [NinjaScriptProperty, Range(2, 200)]
        public int SlowEma { get; set; }
        [NinjaScriptProperty, Range(0, 100)]
        public double MinimumAdx { get; set; }
        [NinjaScriptProperty, Range(0.25, 100)]
        public double StopPoints { get; set; }
        [NinjaScriptProperty, Range(0, 100)]
        public double BreakevenAfterPoints { get; set; }
        [NinjaScriptProperty, Range(0, 20)]
        public int BreakevenPlusTicks { get; set; }
        [NinjaScriptProperty]
        public bool EnableTrailing { get; set; }
        [NinjaScriptProperty, Range(0.25, 100)]
        public double TrailingPoints { get; set; }
        [NinjaScriptProperty, Range(0, 100)]
        public double TrailAfterPoints { get; set; }
        [NinjaScriptProperty, Range(1, 100000)]
        public double MaxDailyLossUSD { get; set; }
        [NinjaScriptProperty]
        public bool UseRthOnly { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "MES_Bounce_RiskManaged";
                Description = "Experimental risk-managed MES swing bounce; requires simulation validation.";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.UniqueEntries;
                IsExitOnSessionCloseStrategy = true;
                StartBehavior = StartBehavior.WaitUntilFlat;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                IncludeCommission = true;
                BarsRequiredToTrade = 50;
                SwingStrength = 10;
                FastEma = 9;
                SlowEma = 21;
                MinimumAdx = 25;
                StopPoints = 10;
                BreakevenAfterPoints = 5;
                BreakevenPlusTicks = 0;
                EnableTrailing = false;
                TrailingPoints = 6;
                TrailAfterPoints = 5;
                MaxDailyLossUSD = 400;
                UseRthOnly = true;
            }
            else if (State == State.DataLoaded)
            {
                swing = Swing(SwingStrength);
                fast = EMA(FastEma);
                slow = EMA(SlowEma);
                adx = ADX(7);
            }
        }

        private double Realized()
        {
            return SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(50, Math.Max(SwingStrength * 2 + 5, SlowEma + 5))) return;

            // Calendar-day reset. For futures overnight sessions, configure session boundaries separately.
            if (Time[0].Date != sessionDate)
            {
                sessionDate = Time[0].Date;
                startRealized = Realized();
                dailyHalt = false;
            }

            if (Position.MarketPosition == MarketPosition.Flat)
            {
                lastSide = MarketPosition.Flat;
                stopPrice = double.NaN;
            }
            else
            {
                ManageStops();
            }

            double openPnL = Position.MarketPosition == MarketPosition.Flat ? 0
                : Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
            if (!dailyHalt && Realized() - startRealized + openPnL <= -MaxDailyLossUSD)
            {
                dailyHalt = true;
                if (Position.MarketPosition == MarketPosition.Long) ExitLong("DailyExitL", "Long");
                if (Position.MarketPosition == MarketPosition.Short) ExitShort("DailyExitS", "Short");
            }
            if (dailyHalt) return;
            if (UseRthOnly && (ToTime(Time[0]) < 93000 || ToTime(Time[0]) >= 160000)) return;

            double support = swing.SwingLow[0];
            double resistance = swing.SwingHigh[0];
            bool longBounce = !double.IsNaN(support) && Low[0] <= support + 2 * TickSize
                && Close[0] > support && fast[0] > slow[0] && adx[0] > MinimumAdx;
            bool shortBounce = !double.IsNaN(resistance) && High[0] >= resistance - 2 * TickSize
                && Close[0] < resistance && fast[0] < slow[0] && adx[0] > MinimumAdx;

            // Place the protective stop template BEFORE the entry order.
            int stopTicks = Math.Max(1, (int)Math.Round(StopPoints / TickSize));
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (longBounce)
                {
                    SetStopLoss("Long", CalculationMode.Ticks, stopTicks, false);
                    EnterLong(1, "Long");
                }
                else if (shortBounce)
                {
                    SetStopLoss("Short", CalculationMode.Ticks, stopTicks, false);
                    EnterShort(1, "Short");
                }
            }
            else if (Position.MarketPosition == MarketPosition.Long && fast[0] < slow[0])
                ExitLong("TrendExitL", "Long");
            else if (Position.MarketPosition == MarketPosition.Short && fast[0] > slow[0])
                ExitShort("TrendExitS", "Short");
            // Never reverse directly; exit first and evaluate a new entry on a later bar.
        }

        private void ManageStops()
        {
            MarketPosition side = Position.MarketPosition;
            double entry = Position.AveragePrice;
            if (lastSide != side)
            {
                lastSide = side;
                peak = High[0];
                trough = Low[0];
                stopPrice = side == MarketPosition.Long ? entry - StopPoints : entry + StopPoints;
            }
            peak = Math.Max(peak, High[0]);
            trough = Math.Min(trough, Low[0]);
            double favorable = side == MarketPosition.Long ? peak - entry : entry - trough;
            double target = stopPrice;

            if (BreakevenAfterPoints > 0 && favorable >= BreakevenAfterPoints)
            {
                double be = entry + (side == MarketPosition.Long ? 1 : -1) * BreakevenPlusTicks * TickSize;
                target = side == MarketPosition.Long ? Math.Max(target, be) : Math.Min(target, be);
            }
            if (EnableTrailing && favorable >= TrailAfterPoints)
            {
                double trail = side == MarketPosition.Long ? peak - TrailingPoints : trough + TrailingPoints;
                target = side == MarketPosition.Long ? Math.Max(target, trail) : Math.Min(target, trail);
            }
            target = Instrument.MasterInstrument.RoundToTickSize(target);
            // Avoid rejected stop amendments when the latest close has already crossed the desired level.
            if (side == MarketPosition.Long && target > stopPrice && target < Close[0] - TickSize)
                stopPrice = target;
            if (side == MarketPosition.Short && target < stopPrice && target > Close[0] + TickSize)
                stopPrice = target;
            if (side == MarketPosition.Long) SetStopLoss("Long", CalculationMode.Price, stopPrice, false);
            else SetStopLoss("Short", CalculationMode.Price, stopPrice, false);
        }
    }
}
