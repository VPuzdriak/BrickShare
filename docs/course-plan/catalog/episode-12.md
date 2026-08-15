# Episode 12 — TDD, properly

← [Course plan](catalog-api.md) · Previous: [Episode 11 — Gates with teeth](episode-11.md)

The first business rule in this course, written test-first: rental price, daily rate and
deposit, all derived from a condition-grade multiplier.

Eleven episodes of groundwork end here. From this point the loop is the one part 1 built —
write it, test it, push it, it is reviewed, it is enforced, it is live — and nothing in it has
to be revisited.

**Done when** the three pricing rules are covered by unit tests that were written before the
code that satisfies them, and `dotnet test` runs both test projects green through the episode-11
gates.

## Before recording

- Episodes 10 and 11 complete: `dotnet build` reports zero warnings, `dotnet format
  --verify-no-changes` exits 0, and `main` is protected.
- **Start on a branch.** Branch protection from episode 11 means every change now lands through
  a pull request, including this one.
- `docs/IDEA.md` open at the *Rental price* and *Deposit* sections. This episode reads the
  requirements on camera rather than paraphrasing them.

---

## Step 1 — Why pricing, and why a second test project

### The subject was chosen so the technique is visible

TDD is easy to demonstrate badly, and the usual reason is a first example that needs a database.
The demo then spends its time on a repository interface and a mocking library, and what students
take away is *how to configure a mock* — which is not the subject.

Pricing has none of that. It is arithmetic over three numbers, and every one of them is in
`docs/IDEA.md`:

```
rental price = base rental price × multiplier[grade]
daily rate   = rental price ÷ minimum rental days
deposit      = retail price   × multiplier[grade]
```

No dependencies, no I/O, no clock, no randomness. A test can state the rule in one line and
check it in one line, so the only thing on screen is the **cycle**.

### The callback to episode 4, and why it matters now

Episode 4 wrote a test and said plainly, on camera, *this is not TDD* — there was no behaviour
to drive out, and the test existed to protect composition. That was a deposit of credibility,
and this is the episode it gets spent in. Say it out loud: **eight episodes ago the course
refused to claim a practice it was not using; this is the practice, and here is what it looks
like when it is real.**

A course that calls every test TDD teaches students that TDD means "there are tests", which is
the single most common misunderstanding of it in the industry.

### The project

```bash
dotnet new xunit -o tests/BrickShare.Catalog.UnitTests -n BrickShare.Catalog.UnitTests
dotnet sln BrickShare.slnx add tests/BrickShare.Catalog.UnitTests
dotnet add tests/BrickShare.Catalog.UnitTests reference src/Catalog/BrickShare.Catalog.Api
```

Delete `UnitTest1.cs` the moment the template produces it, exactly as in episode 4.

### `BrickShare.Catalog.UnitTests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj" />
  </ItemGroup>

</Project>
```

**Look at what is not in that file**, because it is the first visible dividend from episode 10.
No `TargetFramework`, no `Nullable`, no `ImplicitUsings` — `Directory.Build.props` supplies
them. No `Version` attributes — `Directory.Packages.props` supplies those, and **nothing needs
adding to it**, because every package here is one the integration test project already uses.
`dotnet new` writes versions into the template; delete them and watch the build stay green.

No `Microsoft.AspNetCore.Mvc.Testing` either. A unit test project that can boot a web host will
eventually boot one.

### Why a second project and not a folder

The integration project boots the application. Episode 17 will add Testcontainers to it, and
from then on running it means starting Docker and waiting for Postgres.

These tests must never wait for anything. Keeping them in a separate project means `dotnet test
tests/BrickShare.Catalog.UnitTests` stays a sub-second command you can run on every keystroke —
and TDD's cycle only works if the feedback is fast enough to stay inside your head. **The seam
is worth having before the slow thing arrives, not after.**

### Where the production code goes — and why there is still no `Domain` project

`src/Catalog/BrickShare.Catalog.Api/Pricing/`.

The reflex is to create `BrickShare.Catalog.Domain` right now, and the course plan deliberately
does not. **Episode 13** creates it, when `Money`, `SetNumber` and `LabelCode` make the
dependency direction worth enforcing at the project boundary. Moving three files one episode
later costs a `dotnet new classlib` and a namespace change; a project created before anything
forces it is the mistake episode 2 refused to make, and refusing it twice is the point.

---

## Step 2 — Cycle one: red, green, refactor, slowly and once

The whole loop, at recording speed, on the easiest possible case. Every later cycle goes faster
because this one did not.

### Red — and the first red is a build error

`tests/BrickShare.Catalog.UnitTests/Pricing/RentalPriceTests.cs`:

```csharp
using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class RentalPriceTests
{
    [Fact]
    public void RentalPrice_for_a_new_copy_is_the_base_price()
    {
        decimal price = PriceCalculator.RentalPrice(24.99m, ConditionGrade.New);

        Assert.Equal(24.99m, price);
    }
}
```

```
error CS0103: The name 'PriceCalculator' does not exist in the current context
error CS0103: The name 'ConditionGrade' does not exist in the current context
```

**That is a legitimate red and it should be said out loud**, because the first thing people do
when they try TDD is write the class first "so the test compiles". A test that does not compile
is a test that is failing, and it is failing for the most informative reason available: the
design it describes does not exist yet. Writing the call before the method is how the *caller*
gets to choose the signature — which is most of what TDD actually buys.

Notice what the test has already decided: a static call, a `decimal`, a grade that is a type
rather than a string, and no set object. Four design decisions, none of them announced.

### Green — write something obviously wrong

`src/Catalog/BrickShare.Catalog.Api/Pricing/ConditionGrade.cs`:

```csharp
namespace BrickShare.Catalog.Api.Pricing;

/// <summary>
/// The fixed four-tier condition scale from docs/IDEA.md. A grade is a published,
/// customer-facing claim about one physical box — and it sets the price.
/// </summary>
public enum ConditionGrade
{
    New,
    Excellent,
    Good,
    Fair
}
```

`src/Catalog/BrickShare.Catalog.Api/Pricing/PriceCalculator.cs`:

```csharp
namespace BrickShare.Catalog.Api.Pricing;

public static class PriceCalculator
{
    public static decimal RentalPrice(decimal baseRentalPrice, ConditionGrade grade)
    {
        return baseRentalPrice;
    }
}
```

```
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1
```

**That implementation is wrong and it is written on purpose.** It ignores the grade entirely.
Somebody will object, and the answer is worth having ready:

> A green from an implementation you know is wrong tells you one thing, and it is a thing you
> cannot learn any other way: **the test can pass**. A test that has only ever been red might be
> asserting something impossible, calling the wrong thing, or not running at all. Faking the
> result is how you find out the wiring works before you trust the wiring.

The `grade` parameter is unused, which in a codebase with warnings-as-errors is a fair question.
It does not fail the build — an unused *parameter* on a public method is not flagged the way an
unused local is — and it will be used within ninety seconds. Do not add a `_ = grade;` to quiet
something that is not complaining.

### Triangulate — the test the fake cannot survive

```csharp
    [Fact]
    public void RentalPrice_for_an_excellent_copy_is_discounted_by_its_multiplier()
    {
        decimal price = PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent);

        Assert.Equal(21.24m, price);
    }
```

```
Assert.Equal() Failure: Values differ
Expected: 21.24
Actual:   24.99
```

**Where 21.24 came from is not obvious, and this is the moment to be honest about it.**
`24.99 × 0.85 = 21.2415`, and 21.2415 is not an amount anybody can be charged. The number in
that assertion contains a decision that has not been made yet. **Leave it.** Step 4 is entirely
about it — the test has raised a question the requirements never answered, and letting it sit
red for a minute is better television than pretending the answer was known.

To get moving, make it pass with the multiplication and take the rounding on afterwards.

### Refactor — the real implementation

```csharp
public static class PriceCalculator
{
    public static decimal RentalPrice(decimal baseRentalPrice, ConditionGrade grade)
    {
        decimal multiplier = grade switch
        {
            ConditionGrade.New => 1.00m,
            ConditionGrade.Excellent => 0.85m,
            ConditionGrade.Good => 0.70m,
            ConditionGrade.Fair => 0.55m,
            _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Unknown condition grade.")
        };

        return Math.Round(baseRentalPrice * multiplier, 2, MidpointRounding.AwayFromZero);
    }
}
```

Both green. **And this implementation is about to be dismantled by the next test**, which is
fine and is the point — it is the simplest thing that passes what is currently asserted, and no
further design has been invented on speculation.

### The `m` suffix, and one sentence about it

Every money literal here is a `decimal`. Say only this much for now: *money is `decimal` in this
codebase, never `double`, and episode 13 demonstrates why rather than asserting it.* Resist
explaining binary floating point today — it is a genuinely good ten minutes and it is not this
episode's ten minutes.

### Test naming

`MethodOrBehaviour_Condition_Expectation`, the convention episode 4 introduced against a case
with no condition to vary. It has conditions now, and the names read as sentences:
`RentalPrice_for_an_excellent_copy_is_discounted_by_its_multiplier`. Long names in a test project
are not a smell — the name is the specification, and it is the only part of the test that appears
in a failure report.

---

## Step 3 — Cycle two: the multipliers are data, and a test proves it

### Red

```csharp
    [Fact]
    public void RentalPrice_changes_when_an_admin_edits_the_multiplier()
    {
        GradeMultipliers before = new(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });

        GradeMultipliers after = new(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.90m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });

        Assert.Equal(21.24m, PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent, before));
        Assert.Equal(22.49m, PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent, after));
    }
}
```

**No `switch` expression can pass this test**, and that is why it exists. The previous
implementation had the multipliers welded into the method, and UC-1.5 says an admin edits them.
A constant is not editable by an admin; it is editable by a developer with a deployment.

### Green

`src/Catalog/BrickShare.Catalog.Api/Pricing/GradeMultipliers.cs`:

```csharp
namespace BrickShare.Catalog.Api.Pricing;

/// <summary>
/// The percentage multiplier attached to each condition grade (UC-1.5). This is the only
/// global configuration in the catalog service, an admin edits it, and one edit re-prices
/// every copy the shop owns.
/// </summary>
public sealed class GradeMultipliers
{
    private readonly IReadOnlyDictionary<ConditionGrade, decimal> _multipliers;

    public GradeMultipliers(IReadOnlyDictionary<ConditionGrade, decimal> multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);

        foreach (ConditionGrade grade in Enum.GetValues<ConditionGrade>())
        {
            if (!multipliers.TryGetValue(grade, out decimal multiplier))
            {
                throw new ArgumentException($"No multiplier configured for grade {grade}.", nameof(multipliers));
            }

            if (multiplier <= 0m)
            {
                throw new ArgumentException($"The multiplier for grade {grade} must be greater than zero.", nameof(multipliers));
            }
        }

        // Copied, not held. The caller keeps a reference to the dictionary it passed in and
        // could otherwise change a price after this object was constructed and validated.
        _multipliers = multipliers.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    public decimal For(ConditionGrade grade)
    {
        if (!_multipliers.TryGetValue(grade, out decimal multiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "No multiplier configured for this grade.");
        }

        return multiplier;
    }
}
```

And `PriceCalculator` loses its `switch` entirely:

```csharp
public static class PriceCalculator
{
    public static decimal RentalPrice(decimal baseRentalPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);

        return Math.Round(baseRentalPrice * multipliers.For(grade), 2, MidpointRounding.AwayFromZero);
    }
}
```

The first two tests now fail to compile, and fixing them means threading a `GradeMultipliers`
through. **That churn is the cost of TDD's small steps and it should not be hidden.** Pay it back
immediately — refactor is the third step of the cycle, and it applies to test code too.

`tests/BrickShare.Catalog.UnitTests/Pricing/Multipliers.cs`:

```csharp
using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

internal static class Multipliers
{
    /// <summary>
    /// The multiplier table the pricing tests assume unless they are specifically about
    /// an admin editing one. Named so a failing test reads as "the standard table", and
    /// changed in exactly one place when the shop's numbers change.
    /// </summary>
    public static GradeMultipliers Standard()
    {
        return new GradeMultipliers(new Dictionary<ConditionGrade, decimal>
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m,
            [ConditionGrade.Fair] = 0.55m
        });
    }
}
```

Every test from here on calls `Multipliers.Standard()`. The one test that must **not** is the one
just written — its whole subject is two different tables, and a shared helper would hide the
thing it exists to show.

### Validate the whole table at construction, not the lookup at use

`For` cannot fail for any of the four real grades, because the constructor refuses to build an
object that would let it. That is deliberate: **a partially configured price table should not
exist for even one call.** If it could, every caller would need a story for what to do when a
price is unavailable, and there is no good story — you cannot rent the copy, you cannot show the
copy, and the failure would surface in whichever endpoint happened to touch that grade first.

The guard inside `For` is therefore unreachable through normal use, and it stays anyway. C#
permits `(ConditionGrade)99` — an enum is an `int` in a costume — so a cast, a bad deserialize
or a stale database row can produce a value the type system swears is impossible. One `throw` is
cheaper than finding out what a silent `0m` does to a deposit.

### What the test actually decided

Say this plainly, because it is the most transferable thing in the episode:

> That test did not check a number. It settled **where the numbers live** — and therefore that a
> price is computed, never stored.

`docs/architecture/catalog.md` reaches the same conclusion from the other end: rental price,
daily rate and deposit are all functions of the multiplier, so an admin edit re-prices the whole
catalog *instantly and totally* only if nothing was written down. Store them on the copy and
UC-1.5 becomes a bulk `UPDATE` across every row the shop owns — slow, non-atomic in effect, and
wrong for every row it misses.

**The design was in the requirements the whole time. The test is what made it arrive as a
consequence instead of a decree.**

---

## Step 4 — Cycle three: `21.2415` is not a price

Time to settle the number left hanging in step 2. `docs/IDEA.md` gives the formula and says
nothing about rounding, which is normal — requirements written by people who think in money do
not mention it, because to them "£21.24" *is* the answer.

### Red

```csharp
    [Fact]
    public void RentalPrice_is_rounded_to_whole_cents()
    {
        // 24.99 × 0.55 = 13.7445, which is not an amount anyone can be charged.
        decimal price = PriceCalculator.RentalPrice(24.99m, ConditionGrade.Fair, Multipliers.Standard());

        Assert.Equal(13.74m, price);
    }

    [Fact]
    public void RentalPrice_rounds_a_half_cent_up_and_not_to_even()
    {
        // 17.75 × 0.70 = 12.425 exactly — a midpoint, and the one case where the
        // rounding mode is visible. Banker's rounding would give 12.42.
        decimal price = PriceCalculator.RentalPrice(17.75m, ConditionGrade.Good, Multipliers.Standard());

        Assert.Equal(12.43m, price);
    }
```

### The three decisions, made on camera

**1. Two decimal places.** `docs/architecture/catalog.md` already committed to
`numeric(10,2)` in Postgres. A calculation that produces four decimals and a column that stores
two means the database rounds — silently, with its own rule, in a place nobody is looking. **Round
where the decision is visible and testable**, and the storage becomes a formality.

**2. `MidpointRounding.AwayFromZero`, written out.** This is the trap, and it should be
demonstrated rather than described. .NET's default for `Math.Round` is *banker's rounding*
(`ToEven`):

```csharp
Math.Round(12.425m, 2)                              // 12.42  ← the default
Math.Round(12.425m, 2, MidpointRounding.AwayFromZero) // 12.43  ← what a shop means
```

Banker's rounding is not a bug — it is the statistically better choice, because always rounding
halves up biases a large series of sums upward. It is also not what a price list means, not what
a customer expects, and not what anybody assumes when they read `Math.Round`. **The rule to take
away is not "always use AwayFromZero"; it is that a rounding mode is a business decision, and
the one you get by not typing anything should never be the one that surprises people.**

**3. Round at the edge, once.** Rounding is the last thing that happens in a calculation, never
an intermediate step. Round twice and the errors compound; round once and the error is bounded
at half a cent, which is the best any two-decimal price can do.

### Green

Extract the intent into a name, so the rule is stated once and read everywhere:

```csharp
public static class PriceCalculator
{
    private const int CurrencyDecimals = 2;

    public static decimal RentalPrice(decimal baseRentalPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);

        return RoundToCents(baseRentalPrice * multipliers.For(grade));
    }

    // Money is rounded away from zero, not to even. .NET's default is banker's rounding,
    // which is right for statistics and wrong for a price list — so the mode is always
    // written out rather than inherited.
    private static decimal RoundToCents(decimal amount)
    {
        return Math.Round(amount, CurrencyDecimals, MidpointRounding.AwayFromZero);
    }
}
```

---

## Step 5 — Cycle four: the daily rate, and a consequence worth protecting

### Red

`tests/BrickShare.Catalog.UnitTests/Pricing/DailyRateTests.cs`:

```csharp
using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class DailyRateTests
{
    [Fact]
    public void DailyRate_divides_the_rental_price_by_the_minimum_duration()
    {
        // Rental price 21.24 over a 7-day minimum. 21.24 ÷ 7 = 3.0342857…
        decimal dailyRate = PriceCalculator.DailyRate(24.99m, 7, ConditionGrade.Excellent, Multipliers.Standard());

        Assert.Equal(3.03m, dailyRate);
    }
}
```

### The signature is the interesting part

Two shapes were possible, and the difference is worth pausing on:

```csharp
// A — the caller supplies the rental price
decimal DailyRate(decimal rentalPrice, int minimumRentalDays);

// B — the caller supplies the same inputs as everything else
decimal DailyRate(decimal baseRentalPrice, int minimumRentalDays, ConditionGrade grade, GradeMultipliers multipliers);
```

**A is smaller and it is the wrong one.** It takes "the rental price" on trust, and there are two
plausible things a caller might hand it: the rounded price the customer was quoted, or the raw
`24.99 × 0.85 = 21.2415`. Those produce different daily rates, both look right in isolation, and
nothing in the signature says which was meant.

**B has four parameters and cannot be used incorrectly.** The division happens on a number this
method computed, so "the daily rate is derived from the *rounded* rental price" stops being a
convention someone has to know and becomes something the code does. That trade — one more
parameter for one fewer thing to get wrong — is worth taking almost every time.

### Green

```csharp
    public static decimal DailyRate(decimal baseRentalPrice, int minimumRentalDays, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1);

        // Divides the rounded rental price on purpose: that is the number the customer was
        // quoted, so the daily rate has to be derived from it and not from a longer decimal
        // nobody ever saw.
        return RoundToCents(RentalPrice(baseRentalPrice, grade, multipliers) / minimumRentalDays);
    }
```

### The consequence, asserted rather than discovered

`3.03 × 7 = 21.21`, and the rental price is `21.24`. Three cents have gone somewhere, and the
instinct of every careful developer in the audience is that something is broken.

**Nothing is broken, and `docs/IDEA.md` says why.** The minimum duration is a **commercial
floor**, not an estimate: return the set on day one or on day seven and you have paid `21.24`
either way. The daily rate is what an **extra** day costs *beyond* the minimum. It is a marginal
price, not a decomposition of the minimum fee, and the two were never required to reconcile.

So write the test that says so:

```csharp
    [Fact]
    public void DailyRate_times_the_minimum_duration_does_not_have_to_equal_the_rental_price()
    {
        GradeMultipliers multipliers = Multipliers.Standard();

        decimal rentalPrice = PriceCalculator.RentalPrice(24.99m, ConditionGrade.Excellent, multipliers);
        decimal dailyRate = PriceCalculator.DailyRate(24.99m, 7, ConditionGrade.Excellent, multipliers);

        // The minimum duration is a commercial floor: the whole rental price is paid whether
        // the set comes back on day 1 or day 7. The daily rate prices the days AFTER the
        // minimum, so this inequality is the rule working, not rounding drift.
        Assert.NotEqual(rentalPrice, dailyRate * 7);
    }
```

**A test asserting an inequality looks strange, and it is the most valuable test in the file.**
Six months from now somebody will notice those three cents, decide it is a rounding bug and
"fix" it — by rounding the daily rate up, or by deriving the rental price from the daily rate.
This test fails on both, and its name tells them why before they open it.

That is a use of tests worth naming explicitly: **a test can protect a decision, not only a
behaviour.** The comment says what the rule is; the assertion stops it being quietly reversed.

---

## Step 6 — Cycle five: the deposit, and a test that passes immediately

`tests/BrickShare.Catalog.UnitTests/Pricing/DepositTests.cs`:

```csharp
using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class DepositTests
{
    [Fact]
    public void Deposit_for_a_new_copy_is_the_full_retail_price()
    {
        decimal deposit = PriceCalculator.Deposit(199.99m, ConditionGrade.New, Multipliers.Standard());

        Assert.Equal(199.99m, deposit);
    }

    [Fact]
    public void Deposit_for_a_worn_copy_is_reduced_by_the_same_multiplier_as_the_rental_price()
    {
        // 199.99 × 0.55 = 109.99450 → 109.99
        decimal deposit = PriceCalculator.Deposit(199.99m, ConditionGrade.Fair, Multipliers.Standard());

        Assert.Equal(109.99m, deposit);
    }
}
```

```csharp
    public static decimal Deposit(decimal retailPrice, ConditionGrade grade, GradeMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);

        return RoundToCents(retailPrice * multipliers.For(grade));
    }
```

### The second test passes the moment the method compiles

Same shape as `RentalPrice`, same rounding, no new behaviour to drive. **Every real TDD session
hits this, and what you do about it is worth thirty seconds**, because the two common responses
are both wrong:

- *Delete the test* — it did not drive anything, so it looks like ceremony. But it is describing
  a business rule that has to keep holding, and the rule is not "the same as rental price", it is
  "retail × the copy's grade multiplier". Those two agree today and are free to diverge.
- *Pretend it was red* — the audience can see the timestamps.

Do the honest third thing: **prove it can fail.** Change `0.55m` to `0.50m` in the multipliers,
run it, watch it go red, put it back. Ten seconds, and now the test is known to be connected to
the code rather than merely green.

The distinction to name: **some tests drive a design, some tests describe a rule.** Both belong
in the suite; only the first is TDD doing its job, and a course that blurs them teaches students
that a test is only worth writing if it hurt.

### One modelling point while it is on screen

The deposit uses the **copy's** grade and the **set's** retail price, so it is a property of the
copy, not of the catalog entry. `docs/IDEA.md` gives the reason: the deposit stands in for the
value of the specific box that was lost, and a customer who keeps a Fair-grade set did not cost
the shop a new one. The accepted mirror image is stated there too — a deposit taken on a worn
copy will not fully fund a brand-new replacement.

---

## Step 7 — The guards, one failing test each

`tests/BrickShare.Catalog.UnitTests/Pricing/PriceCalculatorGuardTests.cs`:

```csharp
using BrickShare.Catalog.Api.Pricing;

namespace BrickShare.Catalog.UnitTests.Pricing;

public class PriceCalculatorGuardTests
{
    [Fact]
    public void DailyRate_rejects_a_minimum_duration_of_zero_days()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PriceCalculator.DailyRate(24.99m, 0, ConditionGrade.New, Multipliers.Standard()));
    }

    [Fact]
    public void RentalPrice_rejects_a_negative_base_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PriceCalculator.RentalPrice(-1.00m, ConditionGrade.New, Multipliers.Standard()));
    }

    [Fact]
    public void GradeMultipliers_rejects_a_table_missing_a_grade()
    {
        Dictionary<ConditionGrade, decimal> incomplete = new()
        {
            [ConditionGrade.New] = 1.00m,
            [ConditionGrade.Excellent] = 0.85m,
            [ConditionGrade.Good] = 0.70m
        };

        Assert.Throws<ArgumentException>(() => new GradeMultipliers(incomplete));
    }
}
```

The guards themselves, added to the existing methods:

```csharp
        ArgumentOutOfRangeException.ThrowIfNegative(baseRentalPrice);   // RentalPrice
        ArgumentOutOfRangeException.ThrowIfNegative(retailPrice);       // Deposit
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRentalDays, 1); // DailyRate
```

`ArgumentOutOfRangeException.ThrowIfNegative` and friends are .NET 8+ static throw helpers. They
read better than an `if` and, more usefully, they capture the argument name automatically via
`CallerArgumentExpression` — so the message names the parameter without anyone maintaining a
string.

### Zero is legal, negative is not

A base rental price of `0.00` passes. That is intentional and worth one line: a promotional set
that rents for nothing is a business decision the shop is entitled to make, and a domain that
refuses it is a domain arguing with its owner. A **negative** price is not a decision, it is a
bug or a bad request — the shop paying customers to take sets away.

### Why the 28-day maximum is *not* checked here

`docs/IDEA.md` says a set's minimum rental duration is rejected if it exceeds 28 days. That rule
is real, and it does not belong in a pricing function. It is a constraint on the **catalog set**,
enforced where a set is created (episode 20), and `PriceCalculator` has no business having an
opinion about it. A function that validates its neighbours' invariants is a function that has to
be updated when the neighbours change.

**The split to state, since episode 20 will make it explicit:** the edge validates a request so
the caller gets a good message; the domain refuses illegal states no matter who calls it. These
guards are the second kind. They exist so that a bug in some future endpoint fails loudly at the
calculation rather than quietly producing a negative deposit.

---

## Step 8 — Read the suite back, then push it

Before the pull request, read every test name in the project top to bottom, out loud:

```
RentalPrice_for_a_new_copy_is_the_base_price
RentalPrice_for_an_excellent_copy_is_discounted_by_its_multiplier
RentalPrice_changes_when_an_admin_edits_the_multiplier
RentalPrice_is_rounded_to_whole_cents
RentalPrice_rounds_a_half_cent_up_and_not_to_even
DailyRate_divides_the_rental_price_by_the_minimum_duration
DailyRate_times_the_minimum_duration_does_not_have_to_equal_the_rental_price
Deposit_for_a_new_copy_is_the_full_retail_price
Deposit_for_a_worn_copy_is_reduced_by_the_same_multiplier_as_the_rental_price
...
```

**That list is the specification of the pricing rules, and nobody wrote it as one.** It fell out
of five cycles and a guard pass. Point at it and say so — a suite you can read as prose is the deliverable of
doing this test-first, and it is the part that survives after the code is rewritten.

### `[Theory]`, introduced after the facts and not instead of them

The four grades are now four near-identical `[Fact]`s, which is exactly what `[Theory]` is for:

```csharp
    [Theory]
    [InlineData(ConditionGrade.New, 24.99)]
    [InlineData(ConditionGrade.Excellent, 21.24)]
    [InlineData(ConditionGrade.Good, 17.49)]
    [InlineData(ConditionGrade.Fair, 13.74)]
    public void RentalPrice_applies_the_multiplier_for_each_grade(ConditionGrade grade, double expected)
    {
        decimal price = PriceCalculator.RentalPrice(24.99m, grade, Multipliers.Standard());

        Assert.Equal((decimal)expected, price);
    }
```

**Two things to say, and the second one is the honest one.**

`[InlineData]` cannot carry a `decimal` — attribute arguments must be compile-time constants and
`decimal` is not one in the CLR's eyes. So the expected values arrive as `double` and are cast at
the assertion. That is safe *here* because these are exact two-decimal literals and the cast
happens before any arithmetic, and it is worth flagging out loud as the one place in this
codebase where a `double` touches money at all. `[MemberData]` avoids it entirely at the cost of
a static property, and swapping to it is fair.

And the caveat that matters more: **a table of inputs compresses cases and hides rules.** The
four rows above say "these numbers map to those numbers"; the two `[Fact]`s from step 2 said
*"a new copy costs the base price"* and *"an excellent copy is discounted"*. When the theory is
all that remains, the specification has quietly become a lookup table. Keep the named facts for
the rules that carry meaning and use theories for the variations around them — which is why the
theory arrives now, on top of tests that already exist, rather than instead of writing them.

### Through the gates

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

Then a pull request, and this is the payoff episode 11 promised: **nothing about the pipeline
changed.** `ci.yml` discovers the new test project because `dotnet test` walks the solution, the
formatting gate and the analyzer gate apply to the new files with no configuration, and merging
deploys. Eleven episodes of groundwork, and the first business rule in the system reached
production without anyone touching a workflow file.

---

## What this episode deliberately does not do

- **No `Money` type, no `SetNumber`, no `LabelCode`, no `Domain` project.** All four arrive in
  episode 13, and the primitive-obsession argument is much easier to make once there is real code
  passing bare `decimal`s and bare `string`s around. Today's code is the exhibit.
- **No `decimal` versus `double` demonstration.** Named in one sentence, proven in episode 13.
- **No grade transition rules.** *New is unrecoverable* and *grades only fall* are episode 14.
  `ConditionGrade` here is a four-value enum and nothing more — pricing does not care how a copy
  arrived at its grade.
- **Nothing about the default enum value.** `default(ConditionGrade)` is `New`, the most
  expensive grade, which is a trap worth knowing about — and it belongs with the copy lifecycle
  in episode 14, where a grade is actually assigned to something.
- **No persistence.** The multiplier table is four rows in Postgres from episode 16. Today it is
  a constructor parameter, which is all the domain ever needs to know about it.
- **No endpoint.** Pricing surfaces through the read API in part 6, and the admin two-phase edit
  in episode 28. A rule with no caller is fine for one episode.
- **No mocking library, and no assertion library.** There is nothing to mock — that is why this
  rule was chosen — and `Assert.Equal` is enough. Worth one sentence: comparing `decimal`s with
  `Assert.Equal` is **exact**, with no tolerance parameter, which is a small preview of why
  episode 13 cares so much about the type.
- **No coverage reporting, still.** Episode 11 deferred it until there is a body of tests worth
  describing. A dozen tests over one calculator is closer, and it is still not a number anybody
  would act on.

## Verification

```bash
dotnet build                       # 0 warnings, 0 errors, three projects
dotnet test                        # unit + integration, all green
dotnet format --verify-no-changes  # exits 0, prints nothing
```

Then the demonstration that makes UC-1.5 real rather than promised: change `Excellent` from
`0.85m` to `0.90m` in `Multipliers.Standard()` and run the suite. **Three assertions fail at
once** — the rental price fact, the `Excellent` row of the theory, and the daily rate — from a
single edited number. A deposit assertion on that grade would have moved with them, and for the
same reason: none of the three values exists anywhere except as a calculation.

That is precisely what an admin editing a multiplier does to the live catalog — every price the
shop shows, changed together, with no migration, no bulk update and no rows left behind. **The
tests going red here is the feature working.**

Put the value back.

## Next

[Episode 13 — Money and identifiers](catalog-api.md#episode-13--money-and-identifiers): `Money`,
`SetNumber` and `LabelCode` as value objects, the `double` demonstration this episode kept
promising, and the first moment the course lets a second project exist — because now something
finally forces it.
