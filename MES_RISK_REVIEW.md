# MES risk-control experiment (NOT LIVE-READY)

This branch adds `MES_Bounce_RiskManaged.cs` as a separate experimental strategy. The original `MES_Bounce_PPST_TurnCatcher.cs` is unchanged.

## Important: changed signal model
The experimental implementation uses Swing(10), EMA(9/21), and ADX(7), **not** the original PPST and standard-deviation triggers. Results cannot be compared as though only risk controls changed. Port the safety layer to the original PPST entry model before an apples-to-apples assessment.

## Defaults and risk
- 1 MES contract; 10-point initial stop (approx. $50 before costs)
- Move stop to entry after 5-point favorable movement
- Optional trailing stop (6 points, activates after 5 points)
- $400 daily threshold based on realized plus current unrealized strategy P&L
- Exit-only reversal behavior, no same-bar flip
- IncludeCommission enabled; configure actual commissions in NinjaTrader
- OnBarClose; bar-based stop adjustments are not intrabar protection

## Validation checklist (all currently **NOT VERIFIED**)
1. Compile NinjaScript in NinjaTrader 8 and check runtime logs.
2. Sim101 test with live MES feed, examining stop creation and all partial fills.
3. Confirm stop transitions when entry fills during a bar, gaps, disconnects, and restarts.
4. Confirm trading-session timezone (9:30–16:00 is evaluated in platform bar time) and resets.
5. Confirm daily loss is account-appropriate; strategy P&L is **not** account-wide P&L.
6. Backtest 10-minute and 30-minute datasets separately using identical date ranges.
7. Use actual commission schedule and realistic slippage (e.g. test 1–3 ticks); use high fill resolution/tick replay where appropriate.
8. Split data chronologically for in-sample vs. untouched out-of-sample analysis.
9. Report trade count, net P&L, profit factor, max drawdown, win rate, average trade, and stability across assumptions.

**No live trading or auto deployment is enabled by this change.**
