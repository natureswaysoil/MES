# Preliminary historical MES simulation — 2026-10-19

**Verdict: none of the scenarios was profitable. This is NOT NinjaTrader Strategy Analyzer validation.**

Inputs: user's TradingView OHLC CSV exports. 10-minute: 2026-04-17 through 2026-05-21 (3,364 bars); 30-minute: 2025-12-30 through 2026-04-24 (3,755 bars). Nonmatching date windows make timeframe comparisons non-controlled.

Independent Python bar simulator approximates the experimental `MES_Bounce_RiskManaged.cs` entry logic with confirmed swing extrema, EMA(9,21), Wilder-style ADX(7), exit signals on next open, conservative same-bar stop priority and next-bar entries. It is **not** exact NinjaTrader Swing()/ADX()/execution parity. No intrabar tick sequence is known, so outcomes may differ.

Assumptions: 1 MES, $5 per index point, 10-point stop, $1.50 per side commissions, 1 tick/slippage per side in baseline, 3 tick stress, trailing activated at +5 points. Portfolio P&L cannot be inferred from these tests. No contract roll or exchange/session audit done.

| Setup | 10m trades | 10m net | 10m PF | 10m max DD | 30m trades | 30m net | 30m PF | 30m max DD |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 point fixed | 7 | -$223.50 | 0.164 | $238.00 | 10 | -$188.75 | 0.554 | $369.00 |
| Breakeven +5 | 8 | -$150.25 | 0.125 | $164.75 | 10 | -$53.75 | 0.814 | $234.00 |
| BE +5 / trail 6 | 8 | -$16.50 | 0.896 | $153.25 | 10 | -$162.50 | 0.501 | $253.00 |
| BE +5 / trail 4 | 7 | -$6.00 | 0.962 | $120.75 | 10 | -$141.25 | 0.628 | $325.50 |
| BE +5 / trail 6 / 3 ticks | 8 | -$52.75 | 0.690 | $169.50 | 10 | -$197.50 | 0.420 | $270.50 |

## Key blockers
1. Neither timeframe meets positive after-cost expectancy in this small sample.
2. Different data periods prevent direct timeframe ranking and credible out-of-sample assessment.
3. Source strategy is compiled/run only in a NinjaTrader 8 environment, which has NOT occurred here.
4. The separate experimental strategy uses different signals than original PPST; improvements cannot be attributed solely to stop handling.
5. Daily loss threshold is evaluated on completed bars, not continuously; stop management also updates on completed bars.
6. Market slippage, gap fills, and broker behavior remain model approximations.

**Do not merge or live-deploy until parity tests, full-length overlapping dates, out-of-sample testing and broker-sim stop verification pass.**
