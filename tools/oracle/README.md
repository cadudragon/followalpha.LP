# Oracle — golden fixture generation

`reference/` vendors the relevant files from Atis Elsts' `uniswap-v3-liquidity-math` (see `reference/README-upstream.md` for attribution; equations from the public technical note "Liquidity Math in Uniswap v3"). Vendored so that any agent working from a clean checkout can run the oracle without access to the original machine. Internal research use; if this project is ever redistributed, reimplement or clarify licensing (no upstream LICENSE file).

Task for Phase 1 (see `docs/IMPLEMENTATION-PLAN.md`): write `generate_fixtures.py` here that imports the functions from `reference/uniswap-v3-liquidity-math.py`, runs the registered test cases (`test_1`, `test_2`, `example_1..3`, plus additional cases as needed), and writes expected values to `tests/FollowAlpha.LP.Domain.Tests/Golden/fixtures.json`. Fixtures are committed; the C# kernel must match them within documented tolerances.

Rules (from `AGENTS.md`): the oracle is never a runtime dependency; product code never shells out to Python; fixtures are never edited by hand.
