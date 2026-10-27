# Tall Crawl economy (v3)

Goal: the cheapest upgrade/skin takes a handful of runs; buying everything with coins takes several weeks.

## Income
| Source | Amount |
|---|---|
| Collected coin on the track | exactly **1** coin (`gameManager.CoinValue` is a const 1; pickup guarded against double triggers) |
| Distance bonus | 1 coin / 100 m (+1 per "Climb Bonus" upgrade level) |
| x2 ad on results | doubles the **distance bonus only**, never the collected coins, once per run |
| Gems from distance | 1 gem / 2,000 m in one run |
| Daily reward (day 1-7) | 15, 20, 25, 30, 40, 60 coins, day 7 = 1 gem |
| Shop free ad | 20 coins |
| Starting coins | 50 |

## Costs
| Item | Price |
|---|---|
| Upgrade level 1..5 | 250, 600, 1,200, 2,200, 3,500 |
| All upgrades (Boost 5, Heart 2, Climb Bonus 3, Head Start 3) | 12,700 |
| Coin skins | Mint Drop 800, Peachy 1,500, Berry 3,000, Frost 5,000 (10,300 total) |
| Revive | 5 gems or an ad, gives **one** life |

## Gem packs (IAP, dummy IDs, 10 gems per USD)
`com.zakriya.tallcrawl.gems_10` $1 = 10, `gems_50` $5 = 50, `gems_100` $10 = 100, `gems_200` $20 = 200.

## Pacing (verified by `TCSelfTest.Run`)
Average run ~600 m with ~30 coins = ~36 coins. First upgrade after ~7 runs.
A player doing 20 runs + 3 ads + daily reward a day earns ~930 coins/day, so the full coin unlock (~23,000) takes ~25 days.
