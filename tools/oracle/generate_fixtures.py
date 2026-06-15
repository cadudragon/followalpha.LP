#!/usr/bin/env python3
"""Golden-fixture generator for the liquidity-math kernel.

Imports the vendored Elsts reference math (``reference/uniswap-v3-liquidity-math.py``), runs the
registered test cases, and writes the expected values to
``tests/FollowAlpha.LP.Domain.Tests/Golden/fixtures.json``.

The C# kernel must converge to these values within the documented tolerance — never the reverse
(AGENTS.md hard rule 3). Fixtures are committed and never edited by hand: regenerate by running

    python tools/oracle/generate_fixtures.py

Source of truth for the math is the vendored reference only; this script adds no new equations.
"""

import importlib.util
import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
REFERENCE = HERE / "reference" / "uniswap-v3-liquidity-math.py"
OUTPUT = REPO_ROOT / "tests" / "FollowAlpha.LP.Domain.Tests" / "Golden" / "fixtures.json"

# Relative tolerance for the C# (decimal) kernel vs this oracle (double). Generous against
# double<->decimal divergence and conditioning of the worst-shaped reconstructions, tight enough that
# a wrong formula (percent-level error) always fails.
RELATIVE_TOLERANCE = 1e-6


def load_reference():
    spec = importlib.util.spec_from_file_location("uniswap_v3_liquidity_math", REFERENCE)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# Registered cases. p/a/b are prices, x/y token amounts, p_move an optional second price for the
# whitepaper delta form. a or y may be None when the case derives them (examples 1 and 2).
CASES = [
    {"name": "test_1", "p": 20.0, "a": 19.027, "b": 25.993, "x": 1.0, "y": 4.0, "p_move": 22.0},
    {"name": "test_2", "p": 3227.02, "a": 1626.3, "b": 4846.3, "x": 1.0, "y": 5096.06, "p_move": 3500.0},
    {"name": "example_1", "p": 2000.0, "a": 1500.0, "b": 2500.0, "x": 2.0, "y": None, "p_move": None},
    {"name": "example_2", "p": 2000.0, "a": None, "b": 3000.0, "x": 2.0, "y": 4000.0, "p_move": None},
    {"name": "example_3", "p": 1600.88, "a": 1250.18, "b": 2499.91, "x": 0.096, "y": 89.46, "p_move": 2500.0},
    {"name": "eth_wide", "p": 2000.0, "a": 1000.0, "b": 4000.0, "x": 1.0, "y": 2000.0, "p_move": 2500.0},
    {"name": "stable", "p": 1.0, "a": 0.99, "b": 1.01, "x": 1000.0, "y": 1000.0, "p_move": 1.005},
    {"name": "price_below_range", "p": 1400.0, "a": 1500.0, "b": 2500.0, "x": 2.0, "y": 0.0, "p_move": None},
    {"name": "price_above_range", "p": 2600.0, "a": 1500.0, "b": 2500.0, "x": 0.0, "y": 5000.0, "p_move": None},
    {"name": "price_at_lower", "p": 1500.0, "a": 1500.0, "b": 2500.0, "x": 2.0, "y": 0.0, "p_move": None},
    {"name": "price_at_upper", "p": 2500.0, "a": 1500.0, "b": 2500.0, "x": 0.0, "y": 5000.0, "p_move": None},
    {"name": "narrow_range", "p": 2000.0, "a": 1999.0, "b": 2001.0, "x": 1.0, "y": 2000.0, "p_move": 2000.5},
]


def safe(fn):
    """Evaluate a kernel call, returning None if it is undefined for the case (e.g. divide by zero)."""
    try:
        return fn()
    except ZeroDivisionError:
        return None


def build_case(ref, case):
    p, a, b, x, y, p_move = case["p"], case["a"], case["b"], case["x"], case["y"], case["p_move"]
    sp, sb = math.sqrt(p), math.sqrt(b)

    # example_1 derives y from x over the range; example_2 derives a (lower bound) from the amounts.
    if a is None:
        a = ref.calculate_a2(sp, sb, x, y)
    sa = math.sqrt(a)
    if y is None:
        liquidity_for_y = ref.get_liquidity_0(x, sp, sb)
        y = ref.calculate_y(liquidity_for_y, sp, sa, sb)

    liquidity = ref.get_liquidity(x, y, sp, sa, sb)

    result = {
        "name": case["name"],
        "inputs": {"p": p, "a": a, "b": b, "x": x, "y": y},
        "sqrt": {"sp": sp, "sa": sa, "sb": sb},
        "liquidity": liquidity,
        "liquidity0": safe(lambda: ref.get_liquidity_0(x, sa, sb)),
        "liquidity1": safe(lambda: ref.get_liquidity_1(y, sa, sb)),
        "calculate_x": safe(lambda: ref.calculate_x(liquidity, sp, sa, sb)),
        "calculate_y": safe(lambda: ref.calculate_y(liquidity, sp, sa, sb)),
        "calculate_a1": safe(lambda: ref.calculate_a1(liquidity, sp, sb, x, y)),
        "calculate_a2": safe(lambda: ref.calculate_a2(sp, sb, x, y)),
        "calculate_b1": safe(lambda: ref.calculate_b1(liquidity, sp, sa, x, y)),
        "calculate_b2": safe(lambda: ref.calculate_b2(sp, sa, x, y)),
        "c_true": sb / sp,
        "d_true": sa / sp,
        "calculate_c": safe(lambda: ref.calculate_c(p, sa / sp, x, y)),
        "calculate_d": safe(lambda: ref.calculate_d(p, sb / sp, x, y)),
        "price_move": None,
    }

    if p_move is not None:
        sp1 = math.sqrt(p_move)
        spc = max(min(sp, sb), sa)
        sp1c = max(min(sp1, sb), sa)
        delta_y = (sp1c - spc) * liquidity
        delta_x = (1.0 / sp1c - 1.0 / spc) * liquidity
        result["price_move"] = {
            "p1": p_move,
            "sp1": sp1,
            "delta_x": delta_x,
            "delta_y": delta_y,
            "x1": x + delta_x,
            "y1": y + delta_y,
        }

    return result


def main():
    ref = load_reference()
    fixtures = {
        "_comment": (
            "Generated by tools/oracle/generate_fixtures.py from the vendored Elsts reference. "
            "DO NOT EDIT BY HAND. Regenerate with: python tools/oracle/generate_fixtures.py"
        ),
        "relative_tolerance": RELATIVE_TOLERANCE,
        "cases": [build_case(ref, case) for case in CASES],
    }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(fixtures, handle, indent=2)
        handle.write("\n")

    print(f"Wrote {len(CASES)} cases to {OUTPUT}")


if __name__ == "__main__":
    main()
