# TDD log: heavy parcel surcharge

One business rule was built test-first, in three commits. The commit messages start with `RED:`, `GREEN:` and `REFACTOR:` so `git log` shows the history.

## The rule

A **parcel** heavier than **20 kg** pays a flat **15.00** surcharge. The surcharge is added **after** the service level multiplier (it is not multiplied). Exactly 20 kg pays no surcharge. Letters never pay it.

Finding: the brief's example said "over 30 kg", but `MaxStandardParcelKg` is already 30, so no valid parcel could ever be over 30 kg and the rule would never run. I chose 20 kg and recorded it here.

Test zone used in all tests: base 10.00 plus 2.00 per started kilogram.

## RED (`5292fd7`)

File: `ApexPost.UnitTests/Services/HeavyParcelSurchargeTests.cs`. The tests were written first and run before any production change:

| Test | Input | Expected |
|---|---|---|
| Boundary, exactly 20 kg | 20000 g, Standard | 50.00 (no surcharge) |
| Just over | 20001 g, Standard | 67.00 (21 kg: 52, plus 15) |
| 25 kg | 25000 g, Standard | 75.00 |
| 30 kg | 30000 g, Standard | 85.00 |
| Express is not multiplied | 25 kg, Express | 111.00 (60 x 1.6 = 96, plus 15) |
| Letters never surcharged | 25 kg letter | 12.00 |

Result: 6 tests, **4 failed** (the new behaviour does not exist), 2 passed (the boundary and the letter, which the old code already priced this way). A failing test for the right reason is the proof that the test is alive.

## GREEN (`d4a273b`)

The simplest change that makes the tests pass: in `ShippingPricer`, `if (type == Parcel && weight.Kilograms > 20m) raw += 15m;` after the multiplier. Numbers are written in place on purpose. Result: all unit tests pass.

## REFACTOR (`8a9e7cd`)

Behaviour stays the same, the design gets better:

* the two numbers move to `ShippingSettings` (`HeavyParcelThresholdKg = 20`, `HeavyParcelSurcharge = 15`), like the multipliers, so they can change in configuration;
* the rule moves into a small method `SurchargeFor`, which names the idea;
* three new tests prove the settings are really used (custom threshold, custom amount, amount 0 switches the rule off).

Result: the first six tests stay green without any change, which proves the refactor kept the behaviour.

## What I learned

The boundary tests (20000 g and 20001 g) are the ones that matter most: they catch `>` written as `>=`. `scripts/prove-tests-can-fail.ps1` does exactly this mutation and the suite fails.
