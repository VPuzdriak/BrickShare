# Episode 14 — Grades only fall

← [Course plan](catalog-api.md) · Previous: [Episode 13 — Money and identifiers](episode-13.md)

Two rules from `docs/IDEA.md` that look like validation and are actually design — and the first
object in this domain that has a life rather than a value.

**Done when** a copy's grade can fall, can rise only through the repair operation, and can never
be set back to New — each refused by the domain itself and proved by a test.

## Before recording

- Episode 13 merged: `Money`, `SetNumber` and `LabelCode` in `BrickShare.Catalog.Domain`, and
  `dotnet test` green.
- A branch.
- `docs/IDEA.md` open at *Condition grade* and UC-10.5. This episode reads them out.

**Cycles again**, as in episodes 12 and 13: every test is written before the thing it calls, and
the first red is usually a build error. One thing here is not test-driven and says so where it
appears — the namespace move in step 0.

---

## Step 0 — A small move first: `ConditionGrade` leaves `Pricing`

`ConditionGrade` currently sits in `BrickShare.Catalog.Domain.Pricing`, because in episode 12
pricing was the only thing that needed it. Something else is about to need it, and a grade was
never a pricing concept: it is a **published claim about a physical box** that *also* happens to
set a price.

Move the file to the project root, so the namespace becomes `BrickShare.Catalog.Domain`.
`PriceCalculator`, `GradeMultipliers` and the pricing tests each lose a `using`, and nothing else
changes.

Thirty seconds of work, and worth doing on camera for one sentence: **a namespace is a claim about
what a thing is, and this one had started to lie.** Left alone, the next person reads
`Pricing.ConditionGrade` and concludes the grade belongs to pricing — which is exactly backwards,
since pricing is one of the *consequences* of a grade rather than its home.

**This step is not driven by a test.** Nothing about behaviour changes; the compiler is the only
thing that has an opinion, and the existing suite staying green is the entire safety net. That is
what a refactor is, and it is worth naming as distinct from the nine cycles that follow.

---

## Step 1 — Two rules that look like validation

Read them from `docs/IDEA.md` rather than paraphrasing:

> Grade is not just a damage record — it **sets the price**, so it is a commercial judgement as
> much as a physical one. Two rules follow. **New is unrecoverable**: once a copy has gone out and
> come back it can never be New again. And grades otherwise only move **downward**, unless staff
> deliberately override after a repair or piece replacement.

And UC-10.5:

> On completion the grade **may be raised**, and the copy becomes *Available*. Subscribers are told
> it has been **repaired** and is free — a different message from an ordinary return to the shelf.

**The shape of the problem, stated before any code:** there is a general rule (grades fall) and an
exception to it (a repair may raise one). The reflex is a parameter — a `bool allowIncrease`, an
`isRepair` flag, an `if` inside the one method that does regrading. This episode argues that the
reflex is wrong, and it argues it by building the alternative and comparing.

Notice the last line of UC-10.5 while it is on screen. **The business already treats these as two
different events** — a subscriber gets a different message for "repaired and free" than for
"free". A design that collapses them into one method with a flag is not simpler than the business;
it is less accurate than the business.

---

## Step 2 — Comparing two grades (cycles 1–2)

Before anything can enforce "grades only fall", something has to be able to say which of two grades
is better. That turns out to be the trap in this episode.

### Cycle 1 — better and worse

`tests/BrickShare.Catalog.UnitTests/ConditionGradeTests.cs`:

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class ConditionGradeTests
{
    [Fact]
    public void New_is_a_better_grade_than_Excellent()
    {
        Assert.True(ConditionGrade.New.IsBetterThan(ConditionGrade.Excellent));
    }
}
```

```
error CS1061: 'ConditionGrade' does not contain a definition for 'IsBetterThan'
```

Triangulate immediately, because a method returning `true` would pass the first test:

```csharp
    [Fact]
    public void Fair_is_not_better_than_Good()
    {
        Assert.False(ConditionGrade.Fair.IsBetterThan(ConditionGrade.Good));
    }
```

Green — `src/Catalog/BrickShare.Catalog.Domain/ConditionGrade.cs`, beside the enum:

```csharp
public static class ConditionGradeExtensions
{
    public static bool IsBetterThan(this ConditionGrade grade, ConditionGrade other)
    {
        // ConditionGrade is declared best to worst, so the LOWER enum value is the BETTER
        // grade. That inversion is the whole reason no comparison in this codebase is
        // written with < or > outside this method.
        return grade < other;
    }
}
```

### The trap, and why one method exists to contain it

Look at what the enum actually is:

```csharp
public enum ConditionGrade
{
    New,        // 0
    Excellent,  // 1
    Good,       // 2
    Fair        // 3
}
```

It is declared **best to worst**, because that is how `docs/IDEA.md` lists it and how anyone would
read it. So the *lower* number is the *better* grade, and:

```csharp
if (newGrade > currentGrade)   // reads as "better", means "worse"
```

**That line is wrong in the most expensive way available** — it compiles, it type-checks, it reads
like English, and it inverts a business rule. A reviewer scanning a diff will not catch it, because
it looks exactly like what it should say.

So the comparison happens **once**, in a method whose name cannot be misread, and every rule in the
rest of the episode is written in terms of that name. `newGrade.IsBetterThan(Grade)` cannot be
misunderstood by anybody, including its author six months later.

**Three alternatives, each named with its cost**, because this is a genuine judgement call:

| Alternative | Cost |
| --- | --- |
| Reorder the enum worst-to-best so `>` means better | The declaration order stops matching how the business lists the scale, and if episode 16 ever stored ordinals, reordering would silently re-grade every copy in the database. (It is one of the arguments for storing the **name**.) |
| Explicit numeric values encoding quality (`Fair = 1 … New = 4`) | Works, but now two things must be kept in agreement — the order and the numbers — and the comparison is still an operator anybody may write inline. |
| A wrapper type implementing `IComparable` | Correct, and heavier than the problem. It also drags in CA1036 and the four comparison operators, as episode 13 found. |

The chosen version costs one extension method and one comment.

### Cycle 2 — a grade is not better than itself

```csharp
    [Fact]
    public void A_grade_is_not_better_than_itself()
    {
        Assert.False(ConditionGrade.Good.IsBetterThan(ConditionGrade.Good));
    }
```

Against a `<=` implementation this fails:

```
Assert.False() Failure
Expected: False
Actual:   True
```

Against the `<` above it passes immediately, which makes it look pointless. **It is not, and this
is worth being precise about:** the test is buying *strictness*, and step 4 spends it. Regrading a
copy to the grade it already has is a legal outcome of a deep inspection — "we looked, it is still
Good" — and if this comparison were `<=`, that perfectly ordinary event would be refused by the
domain. One character in a method nobody looks at, and a staff member cannot record a routine
inspection result.

Prove it can fail before moving on: change `<` to `<=`, watch it go red, put it back.

---

## Step 3 — `Copy` (cycle 3)

### Red

`tests/BrickShare.Catalog.UnitTests/CopyGradeTests.cs`:

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyGradeTests
{
    [Fact]
    public void A_copy_is_registered_with_a_label_and_a_starting_grade()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Equal(ConditionGrade.New, copy.Grade);
        Assert.Equal("BRK-7F3K2Q", copy.Label.Value);
    }
}
```

```
error CS0246: The type or namespace name 'Copy' could not be found
```

### Green

`src/Catalog/BrickShare.Catalog.Domain/Copy.cs`:

```csharp
namespace BrickShare.Catalog.Domain;

/// <summary>
/// One physical box on the shelf. A catalog set is described once; the shop may own three
/// of them, and this is one of the three (UC-1.2).
/// </summary>
public sealed class Copy
{
    private Copy(LabelCode label, ConditionGrade grade)
    {
        Label = label;
        Grade = grade;
    }

    public LabelCode Label { get; }

    public ConditionGrade Grade { get; private set; }

    public static Copy Register(LabelCode label, ConditionGrade startingGrade)
    {
        ArgumentNullException.ThrowIfNull(label);

        return new Copy(label, startingGrade);
    }
}
```

### Three decisions in eighteen lines

**A `class`, not a `record`.** Every type written in episode 13 was a value object: two `Money` of
`21.24` are interchangeable, and it would be strange to ask *which* `21.24` you meant. A copy is
the opposite. Two boxes both graded Good are **not** the same box — one is on the shelf and one is
in a customer's car — and if a copy's grade changes, it is still the same copy. **Identity, not
value.** That is the whole distinction, and `record`'s generated equality would quietly assert the
wrong one.

Equality is left as plain reference equality, deliberately. Episode 16 gives the entity a persisted
identity, and *that* is when an `Equals` comparing identifiers earns its place. Writing one now
would mean choosing between the label code and a database key that does not exist yet.

**A static factory named `Register`, not a public constructor.** `new Copy(...)` says a C# object
was allocated. `Copy.Register(...)` says the shop took delivery of a box and put a sticker on it,
which is UC-1.2 and the only way a copy comes into existence. The name is free and it makes the
call site describe the business.

**`private set`, and this is the first mutable state in the domain.** Everything since episode 12
has been immutable — pure functions and value objects. A copy has a *life*: it gets graded, it goes
out, it comes back worse. The setter is private because that life is only allowed to advance
through operations that enforce the rules, and the next three steps are those operations.

---

## Step 4 — Grades only fall (cycles 4–6)

### Cycle 4 — a grade can fall

```csharp
    [Fact]
    public void A_copy_can_be_regraded_downward()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        copy.Regrade(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }
```

Green is a setter and nothing else — the smallest thing that passes:

```csharp
    public void Regrade(ConditionGrade newGrade)
    {
        Grade = newGrade;
    }
```

### Cycle 5 — a grade cannot rise

```csharp
    [Fact]
    public void A_copy_cannot_be_regraded_upward()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Fair);

        Assert.Throws<InvalidOperationException>(() => copy.Regrade(ConditionGrade.Good));
    }
```

```
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.InvalidOperationException)
```

```csharp
    public void Regrade(ConditionGrade newGrade)
    {
        if (newGrade.IsBetterThan(Grade))
        {
            throw new InvalidOperationException(
                $"A copy cannot be regraded from {Grade} up to {newGrade}. Grades only fall.");
        }

        Grade = newGrade;
    }
```

**The rule is now in the domain rather than in whoever calls it**, and that is the difference this
episode is really about. A dropdown on a form that only offers lower grades is a nice touch and it
is not a rule — it is absent from the API, absent from a script, absent from a data fix run against
the database at 2 a.m. **The domain refuses illegal states no matter who asks**, and it is the only
layer that can honestly claim that.

### Cycle 6 — the routine inspection that changes nothing

```csharp
    [Fact]
    public void Regrading_to_the_same_grade_is_allowed()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);

        copy.Regrade(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }
```

Green the moment it is written, and **kept** — this is the money the strict comparison in cycle 2
was saving up for. Most deep inspections find nothing: the copy comes back, staff look at it
properly, it is still Good, and that has to be a recordable outcome rather than an error message.

Prove it can fail, as always: flip `IsBetterThan` to `<=` and this test goes red along with cycle
2's. Two tests, one character, and between them they pin the boundary from both sides — which is
what a boundary needs, because a test on only one side of it never notices when it moves.

**One grade is about to be excepted from this**, and the next cycle is where it happens: regrading
to the same grade is fine for Excellent, Good and Fair, and never fine for New. A copy being
*regraded* has by definition been inspected, and being inspected means it has been out.

---

## Step 5 — New can never be regained (cycle 7)

A copy comes back from its very first rental. Staff inspect it and record the grade as New — the
box looks untouched, after all.

```csharp
    [Fact]
    public void A_copy_cannot_be_regraded_to_New()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Throws<InvalidOperationException>(() => copy.Regrade(ConditionGrade.New));
    }
```

```
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.InvalidOperationException)
```

**Cycle 5's guard does not catch it, and the reason is worth seeing.** `New` is not *better* than
`New`, so `IsBetterThan` returns `false`, and the copy is quietly regraded to New — the exact
outcome `docs/IDEA.md` forbids, waved through by a check that is doing precisely what it was asked
to do. It was written to stop grades *rising*, and this grade did not rise.

Try the same test from `Excellent` and it passes without any new code, because `New` genuinely is
better than `Excellent`. **That is the more dangerous version of the bug**: the rule appears to be
enforced from every starting grade except the one the copy actually starts in. A test written only
from `Excellent` would have gone green and left the hole open.

So the rule gets its own refusal, checked first:

```csharp
    public void Regrade(ConditionGrade newGrade)
    {
        if (newGrade == ConditionGrade.New)
        {
            throw new InvalidOperationException(
                "New is a starting grade only. A copy that has been out cannot be New again.");
        }

        if (newGrade.IsBetterThan(Grade))
        {
            throw new InvalidOperationException(
                $"A copy cannot be regraded from {Grade} up to {newGrade}. Grades only fall.");
        }

        Grade = newGrade;
    }
```

**Two checks that overlap on purpose**, and say why: they are two different rules that happen to
agree from most starting grades — and, as the red just showed, not from all of them. "Grades only fall" would be untrue if the business ever allowed an upgrade path;
"New is unrecoverable" would still hold. Collapsing them into one condition would save a line and
lose the ability to explain, in the error message, which rule the caller broke — and an error
message is the only documentation most callers ever read.

### The insight: no flag, no history, no `HasBeenRented`

Here is the thing worth stopping for. The rule in `docs/IDEA.md` is *"once a copy has gone out and
come back it can never be New again"*, which sounds like it needs the copy to remember whether it
has been rented. A `bool hasBeenRented`. A rental count. A first-rental timestamp.

**It needs none of them.** Look at the only two ways a grade is ever set:

- `Register` — where `New` is legal, and which happens once, before the copy has ever left.
- `Regrade` / `RaiseGradeAfterRepair` — neither of which will accept `New`.

So **New is a starting grade and never a destination**, and "cannot be regained" is not enforced by
a check against history — it is enforced by there being no path back. The state the rule seemed to
require does not have to exist.

**The general move, which is worth more than this rule:** when a rule appears to need history, check
first whether it can be made unreachable instead. A rule enforced by a flag is only as good as
every future author remembering to set the flag. A rule enforced by absence cannot be forgotten,
because there is nothing to forget.

**And the honest edge**, so nobody thinks it was missed: a copy registered as Good that turns out
to be a sealed New box is a real thing that will happen, and the fix is *not* to loosen `Regrade`.
It is a **correction**, it is a different operation with a different name and probably a different
audit trail, and it would be written the day the business asks for it. Which is precisely the
argument the next step makes about repairs.

---

## Step 6 — A repair is a different operation (cycles 8–10)

### Cycle 8 — a repair can raise a grade

```csharp
    [Fact]
    public void A_repaired_copy_can_have_its_grade_raised()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Fair);

        copy.RaiseGradeAfterRepair(ConditionGrade.Good);

        Assert.Equal(ConditionGrade.Good, copy.Grade);
    }
```

```csharp
    public void RaiseGradeAfterRepair(ConditionGrade newGrade)
    {
        Grade = newGrade;
    }
```

### Cycle 9 — a repair has to be an improvement

```csharp
    [Fact]
    public void A_repair_that_does_not_improve_the_grade_is_refused()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Good);

        Assert.Throws<InvalidOperationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.Fair));
        Assert.Throws<InvalidOperationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.Good));
    }
```

```csharp
        if (!newGrade.IsBetterThan(Grade))
        {
            throw new InvalidOperationException(
                $"A repair must improve the grade. {newGrade} is not better than {Grade}.");
        }
```

**Mirror-image constraints, and that is the point.** `Regrade` refuses anything better;
`RaiseGradeAfterRepair` refuses anything not better. Neither method can do the other's job, so
neither is a way around the other. A copy that came back from the workshop *worse* is not a repair
outcome — it is a regrade, and it goes through the method that records regrades.

### Cycle 10 — a repair still cannot reach New

```csharp
    [Fact]
    public void A_repaired_copy_still_cannot_be_graded_New()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.Excellent);

        Assert.Throws<InvalidOperationException>(() => copy.RaiseGradeAfterRepair(ConditionGrade.New));
    }
```

```csharp
        if (newGrade == ConditionGrade.New)
        {
            throw new InvalidOperationException(
                "New is a starting grade only. A repair can restore a copy, never its seal.");
        }
```

Worth saying in the shop's own terms, because it is the clearest justification in the episode:
`docs/IDEA.md` explains that a *New* copy is **sealed**, and that the first customer to rent it is
the one who applies the stickers. **No repair puts the stickers back in the packet.** The premium
customers pay for New is for an unspoiled build that nobody else has done, and no amount of
workshop time recreates that.

### The version this episode is arguing against

Write it on screen, because comparing two designs beats describing one:

```csharp
// The flag version.
public void Regrade(ConditionGrade newGrade, bool allowIncrease = false)
{
    if (!allowIncrease && newGrade.IsBetterThan(Grade))
    {
        throw new InvalidOperationException("Grades only fall.");
    }

    Grade = newGrade;
}
```

It is shorter. It is one method instead of two. And it is worse in four ways worth counting out:

1. **The rule is now optional.** Every caller has the ability to turn it off, and the only thing
   standing between a Fair copy and a New one is that nobody passed `true`. A rule a caller can
   disable is a default, not a rule.
2. **The call site stops saying what happened.** `copy.Regrade(Good, allowIncrease: true)` describes
   a *mechanism*. `copy.RaiseGradeAfterRepair(Good)` describes an *event that occurred in the shop*.
   Only the second one still means something in a stack trace, a log line, or a pull request diff.
3. **The two events become indistinguishable** — and the business distinguishes them. UC-3.5 sends
   subscribers a different message when a copy comes back **repaired** than when it merely becomes
   free. With a flag, the code that has to send those messages is left inspecting a boolean to work
   out which thing happened; with two methods, it already knows.
4. **The tests get worse.** Each of the tests above is three lines and reads as a sentence. Their
   flag equivalents all call the same method with different arguments, and the name of the rule
   being tested lives in the argument list rather than in the method being called.

**The general lesson, and it transfers to almost everything:** *when a rule has an exception, model
the exception as its own operation rather than as a flag on the general one.* Both rules then stay
true as written, both are enforced without conditions, and the code keeps the vocabulary the
business already uses.

The tell to watch for: **a boolean parameter is usually a second method wearing a disguise** — and
the giveaway is that no call site ever passes a variable, only a literal `true` or `false` chosen
by whoever is calling.

---

## Step 7 — About that exception type

Every refusal in this episode throws `InvalidOperationException`, and that is **provisional**. Say
so out loud rather than letting anyone copy it as a pattern.

The problem is that it means two completely different things:

| What happened | What should reach the caller |
| --- | --- |
| Staff asked to regrade a Fair copy up to Good | The business said no. A `409` with an explanation, and nothing is broken. |
| Something dereferenced null three frames down | The code is broken. A `500`, and somebody gets paged. |

`InvalidOperationException` cannot tell those apart, so an API layer catching it has to guess — and
the guess is wrong roughly half the time in either direction.

**Episode 20 fixes this on camera**, and in that order deliberately: it puts the first real endpoint
in front of these rules, shows a perfectly reasonable staff request coming back as
`500 Internal Server Error`, and *then* introduces a domain exception type and maps it to a proper
`ProblemDetails` response. The throws written today, and their tests, get refactored there.

**Why not just do it now?** Because the distinction only becomes real when there is something on
the other side of it. A type introduced today would be justified by a promise about episode 20
rather than by anything on screen — and the course's own rule is that structure appears when
something forces it. Nothing here forces it yet. Episode 20 does, visibly, in one HTTP response.

What this episode *does* owe you is the warning, which is this section.

---

## Step 8 — Through the gates

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

PR, green, merge, deployed. The fifth episode in a row that changes no workflow, no infrastructure
and no configuration — the domain grows and the delivery pipeline does not notice.

---

## What this episode deliberately does not do

- **No copy status and no state machine.** `Available`, `On rent`, `Awaiting inspection` and the
  transitions between them are episode 15, on this same class.
- **No retirement.** It is a status change, so it goes with the status (episode 15).
- **No damage log.** UC-10.3 records what was found; that is a separate concern with its own shape,
  and it does not change a grade rule.
- **No events.** `CopyRegraded` is exactly the kind of thing the messaging module retrofits later,
  and publishing to nobody teaches nothing.
- **No persistence, no entity identity.** Episode 16 gives `Copy` a key and an `Equals` worth
  writing. Today reference equality is correct because there is nowhere for a second instance of
  the same copy to come from.
- **No endpoint**, and **no authorization**. Who is allowed to regrade is episode 28's question,
  and the answer there is a policy — not an `if` inside the domain.
- **No domain exception type** — see step 7.

## Verification

```bash
dotnet build                       # 0 warnings, 0 errors
dotnet test                        # green, ten new tests
dotnet format --verify-no-changes  # exits 0
```

Then two deliberate breakages, each of which should turn something red:

1. Change `IsBetterThan` from `<` to `<=`. **Two** tests fail — `A_grade_is_not_better_than_itself`
   and `Regrading_to_the_same_grade_is_allowed` — which is the boundary being held from both sides.
2. Delete the `newGrade == ConditionGrade.New` guard from `RaiseGradeAfterRepair`. One test fails,
   and the copy is New again — a sealed box conjured out of a repair.

## Next

[Episode 15 — The copy state machine](episode-15.md): the lifecycle from `docs/IDEA.md` — all
eight statuses, not just the seven on the typical path — with illegal moves refused by the domain
rather than merely absent from a user interface.

It also brings the rule that shapes the whole service: **a copy cannot be retired while a rental is
active on it**. Because copy status is catalog's own data, that is a local check inside one
transaction — which is the concrete example behind an abstract principle, *put the data where the
invariant is*.
