# Episode 13 — Money and identifiers

← [Course plan](catalog-api.md) · Previous: [Episode 12 — TDD, properly](episode-12.md)

`Money`, `SetNumber` and `LabelCode` as value objects — and the `Domain` project they finally
force into existence.

Episode 12 wrote real business rules using `decimal` for money and nothing at all for
identifiers, because neither existed yet. This episode fixes both, and the second one is the
reason the solution grows a project for the first time since episode 2.

**Done when** money cannot be a `double` anywhere in the domain, a set number and a label code
cannot be passed to each other's parameter, and `BrickShare.Catalog.Domain` refuses to compile
against an ASP.NET type.

## Before recording

- Episode 12 merged: `PriceCalculator`, `GradeMultipliers` and `ConditionGrade` in
  `src/Catalog/BrickShare.Catalog.Api/Pricing/`, and `dotnet test` green.
- A branch, as always since episode 11.

**This episode runs as cycles, like episode 12.** Every test in it is written before the type it
calls, and the first red is usually a build error against something that does not exist yet. Three
things here are *not* driven by a test and each one says so where it appears: `Money.ToString`
(step 2), the swapped-argument demonstration (step 4), and the project extraction (step 7).

**One note if you are following along rather than recording.** This episode writes the three new
types **inside the API project** and extracts `BrickShare.Catalog.Domain` in step 7, so the
namespaces in steps 2–6 read `BrickShare.Catalog.Api` and change at the end. That order is
pedagogical: the project has to be *earned* on camera, or the lesson is "always make four
projects", which is the lesson this course keeps refusing to teach. If you already know you want
the boundary, create the project first and use `BrickShare.Catalog.Domain` throughout — the code
is identical.

---

## Step 1 — The demonstration that decides the type

Do not open with an assertion. Open with the arithmetic.

`tests/BrickShare.Catalog.UnitTests/WhyMoneyIsDecimalTests.cs`:

```csharp
namespace BrickShare.Catalog.UnitTests;

/// <summary>
/// These tests assert nothing about BrickShare. They exist so that the reason money is a
/// decimal in this codebase is executable rather than folklore — and so that anyone who
/// decides to "simplify" Money to a double finds out inside a second.
/// </summary>
public class WhyMoneyIsDecimalTests
{
    [Fact]
    public void Ten_ten_pence_pieces_do_not_make_a_pound_in_a_double()
    {
        double total = 0.0;

        for (int i = 0; i < 10; i++)
        {
            total += 0.1;
        }

        Assert.NotEqual(1.0, total);
    }

    [Fact]
    public void Ten_ten_pence_pieces_make_exactly_a_pound_in_a_decimal()
    {
        decimal total = 0m;

        for (int i = 0; i < 10; i++)
        {
            total += 0.1m;
        }

        Assert.Equal(1.0m, total);
    }

    [Fact]
    public void A_deposit_calculated_in_a_double_is_not_a_deposit()
    {
        double inDouble = 199.99 * 0.55;
        decimal inDecimal = 199.99m * 0.55m;

        Assert.NotEqual((double)inDecimal, inDouble);
    }
}
```

Print the actual values while they are on screen — a passing assertion is much less persuasive
than the numbers themselves:

```
0.1 summed ten times   0.9999999999999999
0.1 + 0.2              0.30000000000000004
199.99 * 0.55          109.99450000000002   ← double
199.99 * 0.55          109.9945             ← decimal
```

### Why, in one paragraph and no more

Binary floating point stores fractions as sums of powers of two. `0.5` and `0.25` are exact;
`0.1` is not, for the same reason `1/3` has no exact decimal expansion. `double` therefore stores
*the nearest representable value* to a tenth, and the error compounds every time you add or
multiply. **This is not a bug in .NET and it is not a rounding preference — it is what the type
is.** `decimal` stores base-ten digits with an explicit scale, which is why the third line above
is exact.

**The consequence, in this business:** the third assertion is a deposit. `109.99450000000002`
rounds to the same `109.99` today, and one multiplication later it does not — and a deposit a
cent wrong is a refund that will not reconcile against Stripe. Nobody notices in testing, and the
finance team notices in month three.

### Say what this is not

*This is not an argument that `double` is bad.* It is the right type for physics, graphics,
statistics and machine learning — anywhere the inputs are measurements and a relative error of
10⁻¹⁶ is noise. Money is not a measurement. It is a **count of the smallest indivisible unit**,
and counts want exact arithmetic.

The same fork appears in Postgres in episode 16 — `real` and `double precision` against
`numeric(10,2)` — and it is the same decision for the same reason.

### And keep the test file

It asserts a property of the C# language, which normally makes for a worthless test. This one is
in the same category as episode 12's inequality test: **it protects a decision, not a behaviour.**
It is the cheapest possible defence against a future refactor that "tidies" the domain onto
`double`, and it costs three seconds of run time forever.

---

## Step 2 — `Money`, one cycle at a time

Six cycles, each one a test that fails for a reason worth seeing. The type that comes out the far
end is short enough to read in one screen, and every line of it was demanded by something.

### Cycle 1 — money is a value

`tests/BrickShare.Catalog.UnitTests/MoneyTests.cs`:

```csharp
using BrickShare.Catalog.Api;

namespace BrickShare.Catalog.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Two_amounts_of_the_same_money_are_equal()
    {
        Money price = new(24.99m);

        Assert.Equal(new Money(24.99m), price);
    }
}
```

```
error CS0246: The type or namespace name 'Money' could not be found
```

The build error is the red, exactly as in episode 12. And the test has already decided three
things: money is constructed from a `decimal`, two amounts of the same size are **equal** rather
than merely comparable, and nothing else is needed to make one.

Green is one line — `src/Catalog/BrickShare.Catalog.Api/Money.cs`:

```csharp
namespace BrickShare.Catalog.Api;

public readonly record struct Money(decimal Amount);
```

**Three keywords, and each is load-bearing.** `record` generates value equality, a matching
`GetHashCode` and a readable `ToString` — all three of which are tedious to write and easy to get
subtly wrong. `readonly` because a value object that can mutate is not a value: `21.24` does not
become `19.99` any more than `7` becomes `8`. `struct` for a reason that gets its own step later.

### Cycle 2 — a price is a whole number of cents

```csharp
    [Fact]
    public void Money_is_rounded_to_whole_cents_when_it_is_created()
    {
        Money price = new(21.2415m);

        Assert.Equal(21.24m, price.Amount);
    }
```

```
Assert.Equal() Failure: Values differ
Expected: 21.24
Actual:   21.2415
```

**And now the interesting part: the positional record cannot pass this test.** `Money(decimal
Amount)` hands the constructor parameter straight to the property, with no room to put anything in
between. To normalize the value, the shorthand has to go:

```csharp
namespace BrickShare.Catalog.Api;

public readonly record struct Money
{
    private const int CurrencyDecimals = 2;

    public Money(decimal amount)
    {
        Amount = Math.Round(amount, CurrencyDecimals);
    }

    public decimal Amount { get; }
}
```

**Say what just happened, because it is the reason to work this way:** a test changed the shape of
the type. Nobody argued that an explicit constructor is tidier than a positional record — a
requirement arrived that the shorter form was structurally incapable of meeting, and the longer
form won on the only ground that matters. That is a much better justification than taste, and it
is available only if the test comes first.

**The trade-off, admitted out loud:** `new Money(x)` can now return something that is not `x`, and
a constructor that quietly changes its argument is normally a bad smell. It is accepted here
because **there is no other legal value** — a price with four decimals cannot be charged, cannot
be stored in `numeric(10,2)`, and cannot be printed on a receipt. The alternative is a `Round()`
call every caller must remember, and "every caller must remember" is the failure mode value
objects exist to remove.

### Cycle 3 — half a cent, and the default fights back

```csharp
    [Fact]
    public void Money_rounds_a_half_cent_away_from_zero()
    {
        Money price = new(12.425m);

        Assert.Equal(12.43m, price.Amount);
    }
```

```
Assert.Equal() Failure: Values differ
Expected: 12.43
Actual:   12.42
```

**This is the best red in the episode**, because episode 12 already settled this question and the
language default has just overruled it. `Math.Round(12.425m, 2)` returns `12.42`: .NET rounds
midpoints *to even* — banker's rounding — unless told otherwise.

Green is one argument:

```csharp
        // Away from zero, never to even. .NET's default is banker's rounding, which is the
        // better choice for a long series of sums and the wrong one for a price list.
        Amount = Math.Round(amount, CurrencyDecimals, MidpointRounding.AwayFromZero);
```

Banker's rounding is not a bug — always rounding halves up biases a large series of sums upward,
and to-even does not. It is simply not what a price means, and **the mode you get by not typing
anything should never be the one that surprises people.**

Notice what the cycle bought: episode 12 asserted this rule in prose and a test; here the same
rule arrives as a failure with two numbers on screen. **A lesson repeated as an argument is
forgettable. A lesson repeated as a red test is not.**

### Cycles 4 and 5 — the two operators `PriceCalculator` needs

```csharp
    [Fact]
    public void Multiplying_money_keeps_it_in_whole_cents()
    {
        Money price = new Money(24.99m) * 0.85m;

        Assert.Equal(new Money(21.24m), price);
    }

    [Fact]
    public void Dividing_money_keeps_it_in_whole_cents()
    {
        Money dailyRate = new Money(21.24m) / 7;

        Assert.Equal(new Money(3.03m), dailyRate);
    }
```

```csharp
    public static Money operator *(Money amount, decimal factor)
    {
        return new Money(amount.Amount * factor);
    }

    public static Money operator /(Money amount, int divisor)
    {
        return new Money(amount.Amount / divisor);
    }
```

Both go through the constructor, so both come back rounded — the invariant holds across
arithmetic without either operator knowing anything about rounding. **`21.2415` has nowhere to
live**, which is the property the whole type exists to have.

The signatures are asymmetric on purpose. Multiplying money by a **ratio** gives money; dividing
money by a **count of days** gives money. Multiplying money by money would give money squared,
which is not a thing, and the type system is happy to refuse it.

### Cycle 6 — the zero that already exists

```csharp
    [Fact]
    public void The_default_value_of_money_is_zero()
    {
        Assert.Equal(Money.Zero, default);
    }
```

```csharp
    /// <summary>
    /// The same value as <c>default(Money)</c>, with a name. A set with no available copies
    /// has no starting price, and "zero" should not be spelled differently in each place
    /// that says it.
    /// </summary>
    public static readonly Money Zero = new(0m);
```

That test looks like it asserts nothing. It is asserting step 6, and it is the reason `Money` can
safely be a struct at all — hold the thought until then.

### `ToString`, and the gate a test cannot be

The finished type overrides `ToString`, and it arrives **without** a cycle. Be honest about why:

```csharp
    public override string ToString()
    {
        return Amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
```

A test asserting `"21.24"` passes on a machine whose culture uses a dot, **with or without
`CultureInfo.InvariantCulture`**. The bug it is supposed to catch — `21,24` on a server in another
locale — is invisible to that test on the developer's laptop, which is the worst possible property
for a test to have. What actually catches it is the analyzer: `CA1305` demands a format provider
and fails the build without one.

**So the rule this codebase uses is not "everything is driven by a test".** It is *know which gate
catches which bug* — tests for behaviour, analyzers for the whole category of mistakes that are
invisible on the machine where the code is written. Episode 11 built the second gate; this is the
first time it catches something a test would have missed.

And a matter of altitude while the method is on screen: this string is **machine-facing**, for
logs and serialization. Customer formatting has a currency symbol and a locale, and a domain type
does not know the reader's culture and should not pretend to.

### What is deliberately absent

**No `+`, no `-`, no `IComparable`.** `*` and `/` exist because cycles 4 and 5 demanded them.
Nothing in the catalog service adds or compares money yet — the cheapest-available-copy query
arrives in episode 25, and *that* is when ordering gets written.

This is not pedantry, and there is a receipt for it. Adding `IComparable<Money>` with a `CompareTo`
and nothing else fails the build immediately:

```
Money.cs(5,31): error CA1036: Money should define operator(s) '<, <=, >, >=' since it
                              implements IComparable
Money.cs(5,31): error S1210:  When implementing IComparable<T>, you should also override
                              <, <=, >, and >=.
```

Two analyzers, both correct, both saying the same thing: **a half-implemented comparison is worse
than none**, because `a < b` compiling while `a.CompareTo(b)` exists is how you get two orderings
that disagree. Episode 25 adds the interface *and* the four operators together, when something
needs sorting. Today it would be four operators nobody calls, written to satisfy a rule triggered
by an interface nobody needed.

**No negative check either**, and that one is an altitude decision worth stating: a *price* cannot
be negative, but *money* can — a deduction from a deposit is negative money, and a type whose
subtraction cannot produce its own values is a broken type. `Money` stays neutral; the
non-negative rule stays on the parameters that are prices.

### The finished type

```csharp
using System.Globalization;

namespace BrickShare.Catalog.Api;

/// <summary>
/// An amount of money, always in whole cents.
///
/// Assumption, stated rather than implied: BrickShare is one shop trading in one currency,
/// so this type carries an amount and no currency. A second country would mean a currency
/// here and a currency column beside every money column in the database — which is a known
/// change rather than a discovery.
/// </summary>
public readonly record struct Money
{
    private const int CurrencyDecimals = 2;

    /// <summary>
    /// The same value as <c>default(Money)</c>, with a name. A set with no available copies
    /// has no starting price, and "zero" should not be spelled differently in each place
    /// that says it.
    /// </summary>
    public static readonly Money Zero = new(0m);

    public Money(decimal amount)
    {
        // Rounded once, here, so an amount that is not a whole number of cents cannot exist.
        // Away from zero rather than .NET's default banker's rounding — see episode 12.
        Amount = Math.Round(amount, CurrencyDecimals, MidpointRounding.AwayFromZero);
    }

    public decimal Amount { get; }

    public static Money operator *(Money amount, decimal factor)
    {
        return new Money(amount.Amount * factor);
    }

    public static Money operator /(Money amount, int divisor)
    {
        return new Money(amount.Amount / divisor);
    }

    public override string ToString()
    {
        return Amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
```

Thirty lines, six of which are a comment explaining a rounding mode. Every one of them was
demanded by a test or an analyzer, and none of it was designed in advance.

---

## Step 3 — `PriceCalculator` moves onto `Money`, tests first

Refactor is the third beat of the cycle and it does not stop being test-first. **Change the
assertions, watch the calculator fail to compile, then change the calculator.**

```csharp
    [Theory]
    [InlineData(ConditionGrade.New, 24.99)]
    [InlineData(ConditionGrade.Excellent, 21.24)]
    [InlineData(ConditionGrade.Good, 17.49)]
    [InlineData(ConditionGrade.Fair, 13.74)]
    public void RentalPrice_applies_the_multiplier_for_each_grade(ConditionGrade grade, decimal expected)
    {
        Money price = PriceCalculator.RentalPrice(new Money(24.99m), grade, Multipliers.Standard());

        Assert.Equal(new Money(expected), price);
    }
```

```
error CS1503: Argument 1: cannot convert from 'BrickShare.Catalog.Api.Money' to 'decimal'
```

**Name this red for what it is**, because conflating it with the others would be dishonest: it is
*expected and uninteresting*. No new behaviour is being driven out. The tests are being retyped,
and the compiler is listing the places that have to follow. That is a different activity from
cycles 1–6, it is still worth doing in this order, and the reason is the same one that makes any
refactor safe — the tests define the target, so the production code has something to be checked
against the moment it changes.

Then the calculator:

```csharp
namespace BrickShare.Catalog.Api.Pricing;

public static class PriceCalculator
{
    public static Money RentalPrice(Money baseRentalPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice.Amount);
        ArgumentNullException.ThrowIfNull(multipliers);

        return baseRentalPrice * multipliers.For(grade);
    }

    public static Money DailyRate(
        Money baseRentalPrice,
        int minimumRentalDays,
        ConditionGrade grade,
        GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        // Divides the rounded rental price on purpose: that is the number the customer was
        // quoted, so the daily rate has to be derived from it and not from a longer decimal
        // nobody ever saw.
        return RentalPrice(baseRentalPrice, grade, multipliers) / minimumRentalDays;
    }

    public static Money Deposit(Money retailPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice.Amount);
        ArgumentNullException.ThrowIfNull(multipliers);

        return retailPrice * multipliers.For(grade);
    }
}
```

### `RoundToCents` is deleted

The whole method, and the `CurrencyDecimals` constant with it. Delete it on camera and pause
there — **that is what a value object is for.** A rule that lived in one class, and was correct
only as long as that class remembered to apply it, now holds everywhere a `Money` exists,
including in code nobody has written yet.

Episode 12 made rounding *correct*. This makes it *structural*.

### The guards read slightly worse, and that is admitted

`ThrowIfNegative(baseRentalPrice.Amount)` uses `CallerArgumentExpression` to name the parameter,
so the exception message says `baseRentalPrice.Amount` rather than `baseRentalPrice`. Mildly
ugly, entirely true, and the alternative is spelling the throw out:

```csharp
if (baseRentalPrice.Amount < 0m)
{
    throw new ArgumentOutOfRangeException(nameof(baseRentalPrice), baseRentalPrice,
        "A base rental price cannot be negative.");
}
```

Four lines for a better message. Take either — but notice the shape of the trade, because it
recurs: **the convenience helpers are built around primitives, and wrapping a primitive costs a
little of that convenience.** Pretending value objects are free is how they get oversold.

### Not one expected number changes

`24.99`, `21.24`, `17.49`, `13.74`, `12.43`, `3.03`, `199.99`, `109.99` — every value from
episode 12 survives untouched, including the `DailyRate_times_the_minimum_duration_does_not_have_to_equal_the_rental_price`
inequality, where `dailyRate * 7` now goes through the new `*` operator.

**That is the point to make while the suite goes green.** Every signature in the calculator
changed, a new type sits under every price, and behaviour did not move an inch. That confidence
is the dividend from episode 12, and it is the argument for writing tests first that no amount of
theory delivers.

`Multipliers.Standard()` is untouched: multipliers are ratios, not money. See step 8.

---

## Step 4 — Two strings the compiler cannot tell apart

**This one is a demonstration and not a test, and it cannot be anything else** — its whole point
is that the wrong version *compiles*. Write it, run it, delete it. It exists for ninety seconds.

```csharp
// Scratch — not committed.
static void RegisterCopy(string setNumber, string labelCode)
{
    Console.WriteLine($"Registering a copy of set {setNumber} with label {labelCode}");
}

string setNumber = "10294-1";
string labelCode = "BRK-7F3K2Q";

RegisterCopy(labelCode, setNumber);
```

```
Registering a copy of set BRK-7F3K2Q with label 10294-1
```

**It compiles. It runs. It is silently, permanently wrong** — a copy filed under a set number
that does not exist, and a label code that will never match the sticker on the box. No test caught
it, because a test would have to know the arguments were swapped. No reviewer caught it, because
the call site reads perfectly.

Now change the signature to `RegisterCopy(SetNumber setNumber, LabelCode labelCode)` and make the
same mistake:

```
error CS1503: Argument 1: cannot convert from 'LabelCode' to 'SetNumber'
error CS1503: Argument 2: cannot convert from 'SetNumber' to 'LabelCode'
```

**That is the entire argument for the next step**, made by demonstration rather than by the phrase
"primitive obsession". Two `string` parameters are the same type to the compiler, so it cannot
help you. Two distinct types are a rule it enforces on every call, forever, for free.

---

## Step 5 — `SetNumber` and `LabelCode`, validated differently on purpose

Both types are driven out the same way; the tests that drive them are opposites, and **the reason
is ownership**:

| | `SetNumber` | `LabelCode` |
| --- | --- | --- |
| Who decides the format | Rebrickable / LEGO | BrickShare |
| Validation | Permissive — trimmed, upper-cased, non-empty, length-capped | Strict — an exact pattern |
| What catches a bad value | The Rebrickable lookup (UC-1.1) | The regex |

### `SetNumber`, cycle 1 — a set number is a normalized string

`tests/BrickShare.Catalog.UnitTests/IdentifierTests.cs`:

```csharp
using BrickShare.Catalog.Api;

namespace BrickShare.Catalog.UnitTests;

public class SetNumberTests
{
    [Fact]
    public void A_set_number_is_trimmed_and_upper_cased()
    {
        Assert.Equal(SetNumber.Parse("10294-1"), SetNumber.Parse(" 10294-1 "));
    }
}
```

`CS0246` again, and the green is small:

```csharp
namespace BrickShare.Catalog.Api;

public sealed record SetNumber
{
    private SetNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SetNumber Parse(string value)
    {
        return new SetNumber(value.Trim().ToUpperInvariant());
    }
}
```

**A private constructor and a static `Parse`** — decided by the test, which never asks for `new
SetNumber(...)`. The only route to an instance runs through the method that normalizes it, so an
un-normalized set number cannot exist. Normalization is not cosmetic: episode 16 puts a unique
index on this column, and `" 10294-1 "` and `"10294-1"` have to be the same value or the index
does not mean what it says.

### Cycle 2 — a blank set number is not a set number

```csharp
    [Fact]
    public void A_set_number_cannot_be_blank()
    {
        Assert.False(SetNumber.TryParse("   ", out _));
    }
```

No `TryParse` exists, so the red is a build error, and the green introduces the shape the rest of
the course uses:

```csharp
    public static bool TryParse(string? value, [NotNullWhen(true)] out SetNumber? setNumber)
    {
        setNumber = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        setNumber = new SetNumber(value.Trim().ToUpperInvariant());
        return true;
    }
```

`[NotNullWhen(true)]` tells the nullable analysis that a `true` return means `setNumber` is not
null, so callers do not need a null check the language would otherwise insist on.

### Cycle 3 — `Parse` throws where `TryParse` refuses

```csharp
    [Fact]
    public void Parse_throws_where_TryParse_returns_false()
    {
        Assert.Throws<FormatException>(() => SetNumber.Parse("   "));
    }
```

The current `Parse` happily returns an empty set number, so this is a real red. The green makes
`Parse` the thin one:

```csharp
    public static SetNumber Parse(string value)
    {
        if (!TryParse(value, out SetNumber? setNumber))
        {
            throw new FormatException($"'{value}' is not a usable LEGO set number.");
        }

        return setNumber;
    }
```

**The pair mirrors `int.Parse` / `int.TryParse`**, so nobody has to learn a local convention, and
the division of labour matters later: **episode 20's edge validation calls `TryParse` and turns
`false` into a `ProblemDetails` with a good message.** Validation by `catch` is slower, loses the
field name, and reads as if a bad request were exceptional — it is the single most common request
in the system.

### Cycle 4 — a set number is not a paragraph

```csharp
    [Fact]
    public void A_set_number_cannot_be_longer_than_a_set_number()
    {
        Assert.False(SetNumber.TryParse(new string('1', 100), out _));
    }
```

```csharp
    private const int MaxLength = 32;
    ...
        if (normalized.Length > MaxLength)
        {
            return false;
        }
```

A cap, not a shape. It stops a megabyte of text arriving in a field the database will hold as
`text`, and it commits to nothing about what a set number looks like.

### Cycle 5 — the test that passes immediately, and stays

```csharp
    [Fact]
    public void A_set_number_accepts_a_shape_we_do_not_recognise()
    {
        // Deliberate. Rebrickable owns this format; the lookup in UC-1.1 is the real gate.
        Assert.True(SetNumber.TryParse("fig-001234", out _));
    }
```

Green the moment it is written, and **kept for the reason episode 12 gave**: some tests drive a
design and some describe a rule. This one describes the rule that matters most in this file, and
it is the difference between deliberate permissiveness and an oversight somebody tightens six
months from now. Prove it can fail — add a `^\d` check, watch it go red, take the check back out.

**The lesson is bigger than LEGO: do not encode a rule you do not own.** A regex for set numbers
feels like diligence and is a liability — the first `fig-001234`, `BigBen-1` or 1970s four-digit
number rejects a set the shop legitimately owns, and the bug reads as "the system says this set
does not exist" long before anyone suspects the validator. UC-1.1 already has the real gate:
cataloguing blocks until the Rebrickable lookup succeeds, and a number that does not exist fails
there with a far better error than `does not match ^\d{4,7}-\d+$`.

The finished type is the four cycles put together, with the reasoning written into the file:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace BrickShare.Catalog.Api;

/// <summary>
/// A LEGO set number as Rebrickable knows it — "10294-1", where the suffix is the variant.
///
/// Deliberately not pattern-matched: LEGO's numbering is not ours to define, and it is
/// wider and stranger than any regex written from three examples. The real gate is UC-1.1 —
/// cataloguing blocks until the Rebrickable lookup succeeds.
/// </summary>
public sealed record SetNumber
{
    private const int MaxLength = 32;

    private SetNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SetNumber Parse(string value)
    {
        if (!TryParse(value, out SetNumber? setNumber))
        {
            throw new FormatException($"'{value}' is not a usable LEGO set number.");
        }

        return setNumber;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out SetNumber? setNumber)
    {
        setNumber = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > MaxLength)
        {
            return false;
        }

        setNumber = new SetNumber(normalized);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }
}
```

### `LabelCode`, cycle 1 — the same shape, normalized

```csharp
public class LabelCodeTests
{
    [Fact]
    public void A_label_code_is_normalised_to_upper_case()
    {
        Assert.Equal("BRK-7F3K2Q", LabelCode.Parse("brk-7f3k2q").Value);
    }
}
```

The green is `SetNumber`'s shape with nothing validated yet — private constructor, `Parse`,
`TryParse`, trim and upper-case. Copying a shape that three tests already justified is not
duplication to be extracted; the two types are about to diverge in the only place that matters.

### Cycle 2 — a label code has a shape, because we chose it

```csharp
    [Fact]
    public void A_label_code_rejects_a_code_without_the_prefix()
    {
        Assert.False(LabelCode.TryParse("7F3K2Q", out _));
    }
```

Red — the cycle-1 version accepts anything. Green introduces the pattern:

```csharp
    [GeneratedRegex("^BRK-[A-Z0-9]{6}$")]
    private static partial Regex Pattern();
```

with the type becoming `public sealed partial record LabelCode` and `TryParse` calling
`Pattern().IsMatch(normalized)`.

**Strict is affordable here because the format is ours.** Nobody outside BrickShare can invent a
label code, so a value that does not match is definitively a bug or a typo, and rejecting it costs
nothing. That is the exact opposite of the argument three cycles ago, and the difference is not
taste — it is **who owns the format**.

`[GeneratedRegex]` compiles the pattern at build time through a source generator: no regex parsing
at startup, no cached-`Regex` static to get wrong, and the generated code is steppable. It needs
the type to be `partial`, which is the whole cost.

### Cycle 3 — the characters people confuse

```csharp
    [Fact]
    public void A_label_code_rejects_an_ambiguous_character()
    {
        // The letter O. If this ever passes, someone has widened the alphabet and the
        // counter is about to start mis-keying codes.
        Assert.False(LabelCode.TryParse("BRK-7F3K2O", out _));
    }
```

`[A-Z0-9]` accepts `O` happily, so this is a genuine red, and the green is a narrower alphabet:

```csharp
    // 0/O, 1/I/L and U are absent on purpose. This code is printed on a box, scanned at a
    // counter, and read down a phone when the scanner will not read it — so the characters
    // people confuse most are not in it at all.
    [GeneratedRegex("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$")]
    private static partial Regex Pattern();
```

**This is the detail worth dwelling on**, because it only comes from imagining the counter: a
member of staff reading `BRK-1O0IL5` off a scuffed sticker to a colleague on the phone will get it
wrong, and no amount of software quality prevents that. The fix is upstream of the software — do
not issue codes containing characters that look like each other. `U` goes for a different reason
(it turns up in unfortunate three-letter combinations). This is roughly the Crockford base-32
alphabet, arrived at from the shop floor rather than from a specification.

**And notice which cycle this was.** The test named the *risk* — an ambiguous character — rather
than the implementation. The alphabet can change without the test changing, and the test will keep
failing for any alphabet that lets `O` back in. A test that asserted the regex string instead
would have to be edited every time the pattern moved, and would protect nothing.

The finished type:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace BrickShare.Catalog.Api;

/// <summary>
/// The identity BrickShare issues to a physical box: "BRK-" plus six characters.
///
/// LEGO boxes carry no per-unit serial number, so a copy's identity has to be invented by
/// the shop and stuck on the outside (UC-1.2). Minting happens in episode 23; this type is
/// only the format.
/// </summary>
public sealed partial record LabelCode
{
    private LabelCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static LabelCode Parse(string value)
    {
        if (!TryParse(value, out LabelCode? labelCode))
        {
            throw new FormatException($"'{value}' is not a BrickShare label code.");
        }

        return labelCode;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out LabelCode? labelCode)
    {
        labelCode = null;

        if (value is null)
        {
            return false;
        }

        string normalized = value.Trim().ToUpperInvariant();

        if (!Pattern().IsMatch(normalized))
        {
            return false;
        }

        labelCode = new LabelCode(normalized);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }

    // 0/O, 1/I/L and U are absent from the alphabet on purpose. This code is printed on a
    // box, scanned at a counter, and read down a phone when the scanner will not read it —
    // so the two characters people confuse most are not in it at all.
    [GeneratedRegex("^BRK-[23456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$")]
    private static partial Regex Pattern();
}
```

---

## Step 6 — A struct or a class? Ask what `default(T)` means

Three value objects, and one of them is a `struct` while two are `record` classes. That looks
arbitrary and is not.

**C# lets anyone obtain `default(T)` for a struct without calling a constructor.** Array
allocation does it, an uninitialized field does it, a deserializer does it, `new T()` in a generic
method does it. So every guard written in a struct's constructor is bypassable, always.

Which turns the choice into one question: **is `default(T)` a value you are happy to accept?**

- `default(Money)` is `0.00` — a real amount of money, and the same thing `Money.Zero` names.
  Nothing is bypassed, because zero satisfies every invariant the constructor enforces (it *is*
  a whole number of cents). A struct is safe.
- `default(SetNumber)` as a struct would be an object whose `Value` is `null` — a set number that
  is not a set number, produced without touching the `Parse` that exists to prevent exactly that.
  Every consumer would then need a null check for a state the type claims is impossible.

So `SetNumber` and `LabelCode` are `sealed record` **classes**, where the only way to obtain one
is `Parse`/`TryParse`, and the absence of one is an honest `null` the nullable reference analysis
already tracks.

```csharp
Assert.Equal(Money.Zero, default);   // meaningful, and true
// SetNumber has no default. There is no such thing as an empty set number.
```

**The rule, portable to any codebase:** *a struct is safe when the zero value is a value you would
accept; otherwise use a class, because you cannot stop anyone getting the zero value.*

The allocation cost of the class version is one small object per identifier, on a service handling
a few thousand copies. Naming the cost and dismissing it with a number is the point — the answer
would be different in an inner loop, and a student should know which fact changed the answer.

---

## Step 7 — The `Domain` project, created last

Stop and count what exists: `Money.cs`, `SetNumber.cs`, `LabelCode.cs`, `ConditionGrade.cs`,
`GradeMultipliers.cs`, `PriceCalculator.cs`. Six files, and **not one of them mentions an ASP.NET
type, a `DbContext`, an `HttpContext` or a configuration binder.** They are already a domain
model. The only thing missing is anything stopping that from changing.

```bash
dotnet new classlib -o src/Catalog/BrickShare.Catalog.Domain -n BrickShare.Catalog.Domain
dotnet sln BrickShare.slnx add src/Catalog/BrickShare.Catalog.Domain
dotnet add src/Catalog/BrickShare.Catalog.Api reference src/Catalog/BrickShare.Catalog.Domain
```

Delete the template's `Class1.cs` immediately — same habit as episodes 4 and 12.

### The `.csproj` is one line, and that is the point

```xml
<Project Sdk="Microsoft.NET.Sdk" />
```

No `TargetFramework`, no `Nullable`, no analyzer reference, no package versions. `Directory.Build.props`
and `Directory.Packages.props` from episode 10 cover a project that did not exist when they were
written. **Three episodes later, a new project arrives strict, analysed and consistent, at a cost
of zero lines** — which is the argument for central configuration, made by a thing not happening.

### Move the files, rename the namespaces

Six files move; `BrickShare.Catalog.Api` becomes `BrickShare.Catalog.Domain` and
`BrickShare.Catalog.Api.Pricing` becomes `BrickShare.Catalog.Domain.Pricing`. Any IDE does this as
a rename refactor. The test project's `using` directives follow, and nothing else in the solution
changes — `Program.cs` never referenced any of it.

### And the unit test project stops depending on the web application

```bash
dotnet remove tests/BrickShare.Catalog.UnitTests reference src/Catalog/BrickShare.Catalog.Api
dotnet add    tests/BrickShare.Catalog.UnitTests reference src/Catalog/BrickShare.Catalog.Domain
```

Worth noticing rather than doing silently: the unit tests were referencing an ASP.NET Core
application in order to test arithmetic. Now they reference a class library, which is a truer
description of what they test and one fewer way for a slow, host-booting test to appear in the
fast suite by accident. `BrickShare.Catalog.IntegrationTests` keeps its reference to the API,
because booting the API is its job.

### The proof, on camera

Open `Money.cs` in the new project and type:

```csharp
using Microsoft.AspNetCore.Http;
```

```
error CS0246: The type or namespace name 'Microsoft' could not be found (are you missing a
              using directive or an assembly reference?)
```

**That is the whole episode's payoff in one error message.** Before the extraction, the domain
"did not depend on ASP.NET" as a matter of *discipline* — true today, true until somebody in a
hurry reaches for `IHttpContextAccessor` to get the current user inside a pricing rule. After it,
the dependency direction is enforced by the compiler: a class library has no framework reference
to ASP.NET, so the code simply cannot be written.

**Be honest about the limit**, because it is a fair question. Nothing stops someone adding
`<FrameworkReference Include="Microsoft.AspNetCore.App" />` to the `.csproj`. The difference is
that doing so is now a **visible line in a pull request** that somebody has to justify, instead of
a `using` nobody notices in a file among two hundred. A boundary does not have to be unbreakable
to work; it has to make crossing it a decision.

### Why not in episode 2

Say it plainly, because eleven episodes of "one project" have been building to this: the project
was created **when the code forced it**, and the forcing was visible — six files with a different
dependency profile from everything around them. Had four projects been created on day one, the
`Domain` folder would have sat empty for twelve episodes and students would have learned that
projects are something you create because a template did.

---

## Step 8 — What was deliberately *not* wrapped

`GradeMultipliers` still holds bare `decimal` multipliers. There is no `Multiplier` type, no
`Percentage`, no `RentalDays`, no `AgeRating`.

**Ask why on camera, and answer it.** Each of the three types written today was justified by a
concrete confusion:

| Type | The confusion it prevents |
| --- | --- |
| `Money` | Money as `double`, and money that is not whole cents |
| `SetNumber` / `LabelCode` | Two strings the compiler cannot tell apart — demonstrated in step 4 |

A multiplier has not been confused with anything. It is a dimensionless ratio, it appears in one
place, and no bug has ever come from it. Wrapping it would add a file, a constructor, a `.Value`
at every use site, and prevent nothing.

**Primitive obsession is a smell, not a law.** The failure mode on the far side is real and much
harder to unwind: a codebase where every `int` has a wrapper, every signature needs a glossary,
and the genuinely important distinctions — the ones in the table above — are invisible in the
noise. **Wrap what gets confused. Leave the rest alone.**

And no strongly-typed-ID source generator package, for the reason in `CLAUDE.md`: a student should
be able to read every line. Two files of about forty lines each are not a burden worth taking a
dependency to avoid.

---

## Step 9 — Through the gates

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

Two source projects and two test projects now, and `ci.yml` and `deploy.yml` are untouched for
the fourth episode running. Open the pull request, watch the checks, merge, and it deploys.

---

## What this episode deliberately does not do

- **No `CatalogSet` or `Copy` entity.** These are the *pieces* a catalog set is made of; the
  aggregates that hold them arrive with the state machine in episodes 14–15.
- **No EF Core value converters.** `Money`, `SetNumber` and `LabelCode` need mapping to
  `numeric(10,2)` and `text` and they get it in episode 16, in the episode that owns persistence.
  Designing the mapping before the `DbContext` exists would be guessing.
- **No currency.** One shop, one currency, written into `Money`'s XML doc as an assumption. The
  moment it stops being true, the compiler will point at every place that needs to change.
- **No customer-facing formatting.** No `£`, no locale. A domain type does not know the reader's
  culture and should not pretend to.
- **No validation attributes and no FluentValidation.** `TryParse` is the seam; episode 20 decides
  what an invalid request *looks like* over HTTP.
- **No label code minting.** Generating them — uniquely, without collisions, at batch registration
  — is episode 23's problem. This is the format only.

## Verification

```bash
dotnet build                       # 0 warnings, 0 errors, four projects
dotnet test                        # green — every episode-12 expectation unchanged
dotnet format --verify-no-changes  # exits 0
```

Then three demonstrations, each of which should **fail** to build:

1. `using Microsoft.AspNetCore.Http;` in a domain file → `CS0246`.
2. `RegisterCopy(labelCode, setNumber)` with the typed signature → `CS1503`, twice.
3. `public readonly record struct Money : IComparable<Money>` with only `CompareTo` →
   `CA1036` and `S1210`.

Three rules that used to be conventions, each now enforced by something that is not a person
remembering.

## Next

[Episode 14 — Grades only fall](episode-14.md): the condition-grade
rules, test-first — *New can never be regained*, grades move downward only, and a repair is a
**different operation** rather than the same regrade with a bigger argument. The first episode
where the domain refuses something a caller asks for.
