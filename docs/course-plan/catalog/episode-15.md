# Episode 15 — The copy state machine

← [Course plan](catalog-api.md) · Previous: [Episode 14 — Grades only fall](episode-14.md)

A copy has a life, and this episode gives it one. Eight states, thirteen legal moves between them,
and — the part worth the episode — one business rule that turns out to need no code at all, because
it is a move that was never drawn.

**Done when** a copy moves through its lifecycle only along the paths `docs/IDEA.md` allows, every
illegal move is refused by the domain itself, and a copy that is out with a customer cannot be
retired — each proved by a test.

## Before recording

- Episode 14 merged: `Copy` with `Regrade` and `RaiseGradeAfterRepair` in
  `BrickShare.Catalog.Domain`, and `dotnet test` green.
- A branch.
- `docs/IDEA.md` open at *Copy status* (the table, not the arrows), UC-1.3, UC-5.2 and UC-10.4.
  This episode reads all four out loud, and two of them contain surprises.

**Cycles again**, as in episodes 12 to 14: every test is written before the thing it calls, and the
first red is usually a build error. **One step is not driven by a test** — the refactor in step 8 —
and it says so where it appears, because a refactor that needs a new test is not a refactor.

---

## Step 0 — Seven arrows and eight rows

The course plan describes this episode's lifecycle like this:

> `Available → Reserved → On rent → Awaiting inspection → In inspection → Available | In repair |
> Retired`

Seven states, one line, easy to read. Now open `docs/IDEA.md` and count the rows in the *Copy
status* table:

> | Status | Meaning |
> | --- | --- |
> | **Available** | On the shelf, rentable now |
> | **Reserved** | Claimed by a customer who has not yet collected it |
> | **On rent** | Collected, out with a customer |
> | **Awaiting inspection** | Back and settled at the counter, queued for deep inspection — **not rentable** |
> | **In inspection** | Being checked and graded properly |
> | **In repair** | Being repaired; neither rentable nor written off |
> | **Lost** | Written off, never returned |
> | **Retired** | Too worn to rent; still held by the shop |

Eight. **The plan's arrow chain was never the state machine** — `docs/IDEA.md` says so in the line
directly beneath it, and it is worth reading that line out because it is the whole point of this
step:

> Typical path:

*Typical.* The arrows are the story of a copy that comes back. `Lost` is the story of a copy that
does not, and it is missing from the diagram for the reason things are usually missing from
diagrams: **nobody draws the paths they do not want to think about.**

**The general move, and it is worth more than this one missing state:** when you build a state
machine, enumerate the states from the *definitions*, not from the happy path. A happy path is a
walk through a graph. It touches every state it happens to touch and tells you nothing about the
ones it does not, and every state you fail to model becomes a value that arrives at runtime with no
code expecting it.

Two things fall out of doing that here, before a line is written: `Lost` exists, and a *Reserved*
copy needs a way back to *Available* when the customer never turns up — a move the arrows also do
not show, because the arrows follow a customer who does turn up.

There is a third surprise in the source, and it is deliberately held back until step 7.

**This step is not code.** It is ten minutes of reading with a pen, and it is the only part of this
episode that could not be recovered later by a compiler.

---

## Step 1 — `CopyStatus`, and where a copy starts (cycle 1)

### Red

`tests/BrickShare.Catalog.UnitTests/CopyStatusTests.cs`:

```csharp
using BrickShare.Catalog.Domain;

namespace BrickShare.Catalog.UnitTests;

public class CopyStatusTests
{
    [Fact]
    public void A_newly_registered_copy_is_available()
    {
        Copy copy = Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);

        Assert.Equal(CopyStatus.Available, copy.Status);
    }
}
```

```
error CS0246: The type or namespace name 'CopyStatus' could not be found (are you missing a using directive or an assembly reference?)
```

### Green

`src/Catalog/BrickShare.Catalog.Domain/CopyStatus.cs`:

```csharp
namespace BrickShare.Catalog.Domain;

public enum CopyStatus
{
    Available,
    Reserved,
    OnRent,
    AwaitingInspection,
    InInspection,
    InRepair,
    Lost,
    Retired
}
```

and on `Copy`, one property and one line in the constructor:

```csharp
    private Copy(LabelCode label, ConditionGrade grade)
    {
        Label = label;
        Grade = grade;
        Status = CopyStatus.Available;
    }

    public CopyStatus Status { get; private set; }
```

### An enum that must never be compared

Episode 14 spent a page on the fact that `ConditionGrade` is *ordered* — that `New < Fair` is
meaningful, that the meaning is inverted, and that the inversion is dangerous enough to be sealed
inside one method called `IsBetterThan`.

**`CopyStatus` is the opposite kind of enum, and saying so out loud is the point of this
digression.** There is no `IsBetterThan` here and there never will be, because the question is
meaningless: *In repair* is not greater than *Reserved*. These are not points on a scale, they are
places a box can be. The declaration order above is the order `docs/IDEA.md` lists them in, chosen
for readability and nothing else — and unlike `ConditionGrade`, **reordering it would change no
behaviour**, because nothing in this codebase will ever write `<` or `>` against a status.

Worth naming the distinction, since both are `enum` and the compiler treats them identically:

| Kind | Example | What the ordinal means |
| --- | --- | --- |
| A **scale** | `ConditionGrade` | Something. Comparison is legal and must be contained. |
| A **set of names** | `CopyStatus` | Nothing. Comparison is a bug waiting to be written. |

The only operator this enum ever sees is `==`, and by the end of the episode not even that, in the
places that matter.

**Why the status is set in the constructor and not passed to `Register`.** UC-1.2 registers a box
the shop has just taken delivery of. There is no other status it could be in, so a parameter would
be a question with one legal answer — and a parameter with one legal answer is an invitation to
pass the illegal ones. A copy that needs to start somewhere else is a data migration, and episode
16 owns those.

---

## Step 2 — Out and back (cycles 2–6)

The five moves of a copy that behaves. Each one: a test naming the event, a red, then the smallest
green — which for now is a bare assignment with no guard at all.

### Cycle 2 — a copy is reserved

```csharp
    [Fact]
    public void An_available_copy_can_be_reserved()
    {
        Copy copy = Available();

        copy.Reserve();

        Assert.Equal(CopyStatus.Reserved, copy.Status);
    }
```

```
error CS1061: 'Copy' does not contain a definition for 'Reserve'
```

```csharp
    public void Reserve()
    {
        Status = CopyStatus.Reserved;
    }
```

`Available()` is a two-line private helper at the bottom of the test class, added the moment the
second test needs it:

```csharp
    private static Copy Available() =>
        Copy.Register(LabelCode.Parse("BRK-7F3K2Q"), ConditionGrade.New);
```

Small thing, said once: **the label and the grade are noise in every test in this file.** No test
here is about either of them, and repeating them twenty-one times would bury the one line of each
test that actually matters. The helper is named for what it returns — a copy that is *available* —
so the test still reads as a sentence about a starting state.

Three siblings appear over the next few cycles, each one built from the one before it rather than
by setting a status directly — which keeps every helper a **legal** starting state rather than an
arrangement of fields no real copy could be in:

```csharp
    private static Copy OnRent()
    {
        Copy copy = Available();
        copy.Reserve();
        copy.Collect();
        return copy;
    }

    private static Copy InInspection()
    {
        Copy copy = OnRent();
        copy.Return();
        copy.BeginInspection();
        return copy;
    }

    private static Copy InRepair()
    {
        Copy copy = InInspection();
        copy.SendForRepair();
        return copy;
    }
```

**This is the one benefit of a domain with no setters that nobody mentions.** There is no
`copy.Status = CopyStatus.InRepair` available to a test, so an arranged copy has to have got there
the way a real one would. It costs four lines and it means no test in this file can assert
something about a state the shop cannot actually reach.

### Cycle 3 — a reserved copy is collected

```csharp
    [Fact]
    public void A_reserved_copy_can_be_collected()
    {
        Copy copy = Available();
        copy.Reserve();

        copy.Collect();

        Assert.Equal(CopyStatus.OnRent, copy.Status);
    }
```

```csharp
    public void Collect()
    {
        Status = CopyStatus.OnRent;
    }
```

### Cycles 4 and 5 — it comes back, and it is not ready

```csharp
    [Fact]
    public void A_returned_copy_is_awaiting_inspection()
    {
        Copy copy = OnRent();

        copy.Return();

        Assert.Equal(CopyStatus.AwaitingInspection, copy.Status);
    }

    [Fact]
    public void A_returned_copy_can_be_taken_in_for_inspection()
    {
        Copy copy = OnRent();
        copy.Return();

        copy.BeginInspection();

        Assert.Equal(CopyStatus.InInspection, copy.Status);
    }
```

```csharp
    public void Return()
    {
        Status = CopyStatus.AwaitingInspection;
    }

    public void BeginInspection()
    {
        Status = CopyStatus.InInspection;
    }
```

**Stop on the first of those two for a moment**, because it is the assertion a reasonable person
gets wrong. The customer has handed the box back. The money is settled — that happened at the
counter. Every instinct says the copy is now *Available*.

`docs/IDEA.md` says otherwise, and explains itself:

> The gap between *Awaiting inspection* and *Available* is deliberate and matters: a box that has
> just been handed back is not yet ready for the next customer. The money is already settled by
> then — that happens at the counter — but the copy still has to be properly checked, regraded and
> shelved.

**Two states where one would do, and the second state is the entire feature.** Collapse them and
the next customer rents a box with a bag of pieces missing, because nobody counted them. The
lifecycle is not bureaucracy; it is where the shop's actual work happens, and UC-10.6 even puts a
two-business-day clock on this state. A state machine that models only the states a *user* would
name usually misses the ones the *business* runs on.

### Cycle 6 — back on the shelf

```csharp
    [Fact]
    public void An_inspected_copy_can_be_shelved()
    {
        Copy copy = InInspection();

        copy.Shelve();

        Assert.Equal(CopyStatus.Available, copy.Status);
    }
```

```csharp
    public void Shelve()
    {
        Status = CopyStatus.Available;
    }
```

Five methods, five assignments, no rules yet. **Every one of these tests would still pass if the
methods were callable in any order whatsoever** — a copy could be shelved straight from *On rent*,
skipping the inspection the last cycle just argued was the point of the whole design. That is what
the next step is for, and it is worth saying before writing it: *a green suite that only tests the
happy path is a suite that has agreed with you rather than checked you.*

---

## Step 3 — Illegal moves are refused (cycles 7–9)

### Cycle 7 — a copy nobody reserved cannot go out

```csharp
    [Fact]
    public void A_copy_that_nobody_reserved_cannot_be_collected()
    {
        Copy copy = Available();

        Assert.Throws<InvalidOperationException>(() => copy.Collect());
    }
```

```
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.InvalidOperationException)
```

```csharp
    public void Collect()
    {
        if (Status != CopyStatus.Reserved)
        {
            throw new InvalidOperationException(
                $"A copy can only be collected from {CopyStatus.Reserved}, not {Status}.");
        }

        Status = CopyStatus.OnRent;
    }
```

### Cycle 8 — a copy that is out cannot be reserved

```csharp
    [Fact]
    public void A_copy_that_is_out_cannot_be_reserved()
    {
        Copy copy = OnRent();

        Assert.Throws<InvalidOperationException>(() => copy.Reserve());
    }
```

```csharp
    public void Reserve()
    {
        if (Status != CopyStatus.Available)
        {
            throw new InvalidOperationException(
                $"Only an {CopyStatus.Available} copy can be reserved. This one is {Status}.");
        }

        Status = CopyStatus.Reserved;
    }
```

`docs/IDEA.md` states this one twice, which is usually a sign the business cares:

> **Copy status** — where a copy is in its cycle. Only **Available** can be reserved.

### Refused by the domain, not missing from the screen

The same argument as episode 14's, and it is worth repeating in a new shape because this is where
people quietly stop believing it.

A reservation screen that only lists available copies is good design and **it is not this rule**.
It is absent from the internal status endpoint the rentals service calls. It is absent from a
support tool. It is absent from the migration script that runs at 2 a.m. and from the console app
somebody wrote to fix a batch of mislabelled boxes.

**The domain is the only layer that gets asked every single time**, which makes it the only layer
whose refusal is worth anything. Everything above it is a convenience for whoever happens to be
looking at a screen.

### Cycle 9 — the customer who never came

The move the plan's arrows do not show, found by reading the states rather than the path:

```csharp
    [Fact]
    public void An_unclaimed_reservation_returns_the_copy_to_the_shelf()
    {
        Copy copy = Available();
        copy.Reserve();

        copy.ReleaseReservation();

        Assert.Equal(CopyStatus.Available, copy.Status);
    }
```

```csharp
    public void ReleaseReservation()
    {
        if (Status != CopyStatus.Reserved)
        {
            throw new InvalidOperationException(
                $"Only a {CopyStatus.Reserved} copy can have its reservation released. This one is {Status}.");
        }

        Status = CopyStatus.Available;
    }
```

**Without this, a reservation is a trap door.** A customer reserves a set, changes their mind, and
the box sits *Reserved* forever — unrentable, invisible to the availability count, and recoverable
only by editing the database. The happy path never reveals it, because on the happy path the
customer arrives.

**Note what is deliberately not here: any notion of *when*.** No expiry timestamp, no hold
duration, no timer. Catalog is being *told* the reservation is over; deciding that it is over is
reservations' business, and that service does not exist yet. The domain models the transition, not
the clock — and this is the shape almost every "expiry" ends up having: a rule somewhere else, and
a state change here.

---

## Step 4 — Two ways off the inspection bench (cycles 10–12)

UC-10.4, read out:

> **UC-10.4 — Shelve, repair or retire.** The copy becomes *Available*, moves to *In repair*, or is
> *Retired* if it is past saving.

### Cycle 10 — into the workshop

```csharp
    [Fact]
    public void An_inspected_copy_can_be_sent_for_repair()
    {
        Copy copy = InInspection();

        copy.SendForRepair();

        Assert.Equal(CopyStatus.InRepair, copy.Status);
    }
```

```csharp
    public void SendForRepair()
    {
        if (Status != CopyStatus.InInspection)
        {
            throw new InvalidOperationException(
                $"A copy can only be sent for repair from {CopyStatus.InInspection}, not {Status}.");
        }

        Status = CopyStatus.InRepair;
    }
```

### Cycle 11 — out of the workshop

```csharp
    [Fact]
    public void A_repaired_copy_returns_to_the_shelf()
    {
        Copy copy = InRepair();

        copy.CompleteRepair();

        Assert.Equal(CopyStatus.Available, copy.Status);
    }
```

### The obvious version, and why it is not taken

`CompleteRepair` sets the status to `Available`. `Shelve` sets the status to `Available`. They are
the same line of code, reached from two different states, and the reflex — a good reflex, normally
— is to write one method that accepts both:

```csharp
// The tempting version.
public void Shelve()
{
    if (Status is not (CopyStatus.InInspection or CopyStatus.InRepair))
    {
        throw new InvalidOperationException(...);
    }

    Status = CopyStatus.Available;
}
```

One method instead of two, and it deduplicates a genuinely identical assignment. **Episode 14 has
already supplied the argument against it**, and here it is again wearing different clothes: *the
business distinguishes these two events, so the code that has to act on them cannot afford to have
merged them.* From UC-10.5:

> On completion the grade **may be raised**, and the copy becomes *Available*. Subscribers are told
> it has been **repaired** and is free — **a different message from an ordinary return to the
> shelf**, because the copy may now be in better condition than when they subscribed.

With one method, the code that sends that notification is left inspecting the previous status to
work out which sentence to send — reconstructing, from data, a fact the caller knew for certain and
threw away. With two, it already knows, and when episode 21 publishes `CopyStatusChanged` there are
two obvious places to publish two different events from.

**The tell to watch for, and it generalises past this case:** two operations that share an
implementation are not the same operation. Ask what happens *because* of each one, not what each
one *sets*. The bodies here are one line and identical; the consequences are a repair notification
and a shelf notification, and those are not identical at all.

The duplicated assignment is the price, it is one line, and it is cheap.

### Cycle 12 — and neither is a way into the other

```csharp
    [Fact]
    public void A_copy_that_is_not_in_repair_cannot_complete_a_repair()
    {
        Copy copy = InInspection();

        Assert.Throws<InvalidOperationException>(() => copy.CompleteRepair());
    }
```

```csharp
    public void CompleteRepair()
    {
        if (Status != CopyStatus.InRepair)
        {
            throw new InvalidOperationException(
                $"A copy can only complete a repair from {CopyStatus.InRepair}, not {Status}.");
        }

        Status = CopyStatus.Available;
    }
```

**Mirror-image guards, exactly as in episode 14.** `Shelve` works only from *In inspection*;
`CompleteRepair` works only from *In repair*. Neither can do the other's job, so splitting them
into two methods did not create a way around either one — which is the property that makes two
methods safe rather than merely tidy.

---

## Step 5 — Retire, and the rule that shapes the service (cycles 13–16)

This is the episode.

> - **Retiring is a state change, never a deletion.** Rental history must survive it.
> - A copy **cannot be retired while a rental is active** on it.
>
> — `docs/IDEA.md`, UC-1.3

### Cycle 13 — a worn copy comes off the shelf

```csharp
    [Fact]
    public void A_copy_on_the_shelf_can_be_retired()
    {
        Copy copy = Available();

        copy.Retire();

        Assert.Equal(CopyStatus.Retired, copy.Status);
    }
```

```csharp
    public void Retire()
    {
        Status = CopyStatus.Retired;
    }
```

### Cycle 14 — the rule

```csharp
    [Fact]
    public void A_copy_that_is_out_on_rent_cannot_be_retired()
    {
        Copy copy = OnRent();

        Assert.Throws<InvalidOperationException>(() => copy.Retire());
    }
```

```
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.InvalidOperationException)
```

### Cycle 15 — and the one next to it

```csharp
    [Fact]
    public void A_reserved_copy_cannot_be_retired()
    {
        Copy copy = Available();
        copy.Reserve();

        Assert.Throws<InvalidOperationException>(() => copy.Retire());
    }
```

Green for both, and it is the same shape as every other guard in the file:

```csharp
    public void Retire()
    {
        if (Status is CopyStatus.Reserved or CopyStatus.OnRent)
        {
            throw new InvalidOperationException(
                $"A copy cannot be retired while a customer has it. This one is {Status}.");
        }

        Status = CopyStatus.Retired;
    }
```

### Cycle 16 — and the one that is allowed

```csharp
    [Fact]
    public void A_copy_being_inspected_can_be_retired()
    {
        Copy copy = InInspection();

        copy.Retire();

        Assert.Equal(CopyStatus.Retired, copy.Status);
    }
```

Green the moment it is written, and **kept**, because it holds the boundary from the other side.
UC-10.4 says retirement is one of the three outcomes of an inspection — in fact it is where most
retirements will actually happen, since a copy is usually condemned by the person holding it. A
guard written slightly too wide (`Status != CopyStatus.Available`, say) would pass cycles 14 and 15
and refuse the most common legitimate retirement in the shop.

**And one judgement call, admitted rather than hidden:** *Awaiting inspection* is **not** on the
list. A box can come back visibly destroyed, and it is tempting to let staff condemn it on the
spot. It is left out because UC-10.3 records the damage and UC-10.2 sets the final grade **during
the inspection**, so retiring straight off the counter skips the two steps that produce the record
of *why* the copy was retired. `BeginInspection()` then `Retire()` is two calls instead of one and
loses nothing. This is a close call, and it is the kind that should be reopened the moment somebody
in the shop complains about it — not defended because the code already says so.

### Now say what just happened, because it is the whole reason the service is drawn this way

Read cycle 14's test again. It says *a copy that is out on rent cannot be retired*. Now find the
code that checks whether a rental is active.

**There isn't any.** There is no rental. There is no rentals service, no rental table, no
`IRentalService.HasActiveRental(copyId)`, no HTTP call, no cache, no timeout, no retry. There is a
copy that knows it is `OnRent`, and a method that will not move it to `Retired` from there.

**The rule cost one enum comparison, because catalog owns the fact the rule is about.**

Now do the counterfactual on screen, because the alternative is what almost every system in the
wild actually does. Suppose copy status lived in the rentals service instead, and catalog had to
ask before retiring:

```csharp
// The version this architecture exists to avoid.
public async Task RetireAsync(CopyId id)
{
    bool isOut = await rentals.HasActiveRentalAsync(id);   // network call
    if (isOut)
    {
        throw new InvalidOperationException("Copy is on rent.");
    }

    copy.Status = CopyStatus.Retired;                      // local write
    await db.SaveChangesAsync();
}
```

Line by line, this is not a rule. It is a **race**:

1. Catalog asks rentals: is this copy out? Rentals says no.
2. In the milliseconds that follow, a customer collects the copy. It is now out.
3. Catalog, holding an answer that was true when it was given and is false now, retires it.

There is no way to close that gap by trying harder. A shorter timeout does not close it. Asking
twice does not close it. A distributed lock closes it and introduces a lock that can be held by a
process that has died. **The answer to a question asked over a network is a fact about the past**,
and an invariant enforced against a fact about the past is not enforced.

Compare what the domain does instead: it reads `Status`, decides, and writes `Status` — one object,
one transaction, and no window between the check and the write in which the world can change,
because the thing being checked is the thing being written.

**That is the principle, and it is the most portable thing in this course:** *put the data where
the invariant is.* When a rule spans two facts, those two facts want to live in one place, owned by
one service, written in one transaction. Split them and no amount of engineering afterwards makes
the rule true again — it makes it *usually* true, which is a different and much worse thing to
have.

**The cost, stated plainly, because it is real.** Catalog now holds a concept it does not otherwise
care about. `OnRent` is not a catalog idea; it is a rental idea, sitting in catalog's enum. That is
a smell, and it is the correct trade — `docs/architecture/catalog.md` names it:

> The cost is that catalog carries a concept it does not otherwise care about. It does not know
> what a rental *is*; it only knows a copy is out. That is the right amount of knowledge: enough to
> protect its own invariant, not enough to couple it to someone else's model.

Notice how little leaked. Catalog does not know who rented it, for how long, what the deposit was,
or when it is due back — the rentals service owns every one of those and catalog never asks. It
knows one bit: **the box is not here.** A service boundary is not drawn by keeping concepts apart;
it is drawn by being deliberate about the smallest amount of someone else's world you must hold in
order to keep your own promises.

**And the rule that shapes the service is not a line of code.** Look at `Retire` once more and
notice that the guard is barely doing anything: the real enforcement is that *no operation anywhere
in this class moves a copy from `OnRent` to `Retired`*. The rule is an **edge that does not exist**
in the graph. Rules made of missing edges cannot be forgotten by a future author the way a
validation branch can, which is the same argument episode 14 made about `New` being unreachable
rather than guarded — the second time in three episodes that the strongest version of a rule turned
out to be an absence.

---

## Step 6 — The copy that never came back (cycles 17–18)

The state the diagram omitted.

### Cycle 17

```csharp
    [Fact]
    public void A_copy_that_was_never_returned_is_written_off_as_lost()
    {
        Copy copy = OnRent();

        copy.WriteOffAsLost();

        Assert.Equal(CopyStatus.Lost, copy.Status);
    }
```

```csharp
    public void WriteOffAsLost()
    {
        Status = CopyStatus.Lost;
    }
```

### Cycle 18

```csharp
    [Fact]
    public void A_copy_on_the_shelf_cannot_be_written_off_as_lost()
    {
        Copy copy = Available();

        Assert.Throws<InvalidOperationException>(() => copy.WriteOffAsLost());
    }
```

```csharp
    public void WriteOffAsLost()
    {
        if (Status != CopyStatus.OnRent)
        {
            throw new InvalidOperationException(
                $"Only a copy that is {CopyStatus.OnRent} can be written off as lost. This one is {Status}.");
        }

        Status = CopyStatus.Lost;
    }
```

### Why this is not just `Retired` with a sadder name

Both are end states. Both stop the copy being rentable. Both end every subscription to it —
`docs/IDEA.md` says *Retired* and *Lost* are treated identically for that purpose. Merging them
would delete a state and a method.

`docs/IDEA.md` refuses, in one sentence worth reading out:

> **Lost is not retired.** Retiring is a decision about a worn set the shop still holds; lost means
> it is gone. They are different end states and **stock figures must not merge them**.

**One is a decision, the other is an event.** Retiring is something the shop *chose*, about a box it
can still point at. Lost is something that *happened to* the shop. Merge them and the shop can no
longer answer "how many sets have we lost this year?" — a question that drives deposit policy and
which sets get restocked — because the answer is now mixed in with routine wear. **A state machine
that merges two states merges the reports built on them**, and reports are usually where the merge
is discovered, long after it is expensive to undo.

**And this state is why a write-off never gets skipped.** `docs/IDEA.md`, on the day-28 job:

> A failed capture never blocks the rest of a write-off. The set is gone whether or not the money
> arrived; recording otherwise would leave a **lost copy sitting *On rent* forever**.

That is the bug this state prevents, described by the business before any code existed. Without
`Lost`, the only honest thing to do with an unreturned copy is leave it `OnRent` — which quietly
means every stock count is wrong, and — read step 5 again — **that copy can never be retired
either**, because the invariant is doing exactly what it was built to do to a copy that no longer
exists.

---

## Step 7 — Terminal means terminal, except when a manager says otherwise (cycles 19–21)

### Cycle 19 — nothing comes back from retirement

```csharp
    [Fact]
    public void A_retired_copy_cannot_be_reserved()
    {
        Copy copy = Available();
        copy.Retire();

        Assert.Throws<InvalidOperationException>(() => copy.Reserve());
    }
```

**Green the moment it is written, and kept.** Say why, because a test that never fails looks like a
test that is not pulling its weight: `Reserve` only ever accepted `Available`, so retirement was
already unreachable-from — for free, as a consequence of guarding the *entry* to each state rather
than the *exit* from each. This test does not drive a design; it **describes a rule** that the
design happens to give away, and it is the test that goes red on the day somebody widens `Reserve`
to be helpful.

### The third surprise in the source

Now the one held back from step 0. `Retired` and `Lost` were both called terminal, and the words
`docs/IDEA.md` uses are *terminal states*. But that phrase appears in the **subscriptions** section,
and it is describing what happens to a subscription — nothing is left to wait for, so the
subscription ends.

Read UC-5.2 and it turns out to be saying something quite different about the copy:

> **UC-5.2 — A manager decides.** A **manager** may accept the set back or refuse — it reverses an
> automatic write-off, so it is theirs to make. If accepted, the copy enters *Awaiting inspection*
> and rejoins stock through the normal inspection path.

A customer turns up three weeks late with the box. **`Lost` is not terminal.** It is terminal for
the subscription and reversible for the copy, and the two are not the same claim — which is what
happens when a word from one part of a document is carried into another.

### Cycle 20 — a copy comes back from the dead

```csharp
    [Fact]
    public void A_recovered_copy_re_enters_stock_awaiting_inspection()
    {
        Copy copy = OnRent();
        copy.WriteOffAsLost();

        copy.Recover();

        Assert.Equal(CopyStatus.AwaitingInspection, copy.Status);
    }
```

```csharp
    public void Recover()
    {
        if (Status != CopyStatus.Lost)
        {
            throw new InvalidOperationException(
                $"Only a {CopyStatus.Lost} copy can be recovered. This one is {Status}.");
        }

        Status = CopyStatus.AwaitingInspection;
    }
```

**And the destination is the assertion worth arguing about.** The obvious implementation puts the
copy back on the shelf — it is the same box, it was fine when it left. `docs/IDEA.md` refuses:

> The copy re-enters stock as **Awaiting inspection**, never straight to *Available* — it has been
> outside the shop's control and must be checked.

Which is step 2's rule again, at its strongest. A box that spent three weeks somewhere unknown is
the *last* thing that should go straight to a customer, and the state machine already has exactly
the right place to put it. **A well-drawn lifecycle answers questions it was not designed for** —
that is a reasonable test of whether the states are real or invented.

### Cycle 21 — and retirement really is the end

```csharp
    [Fact]
    public void A_retired_copy_cannot_be_recovered()
    {
        Copy copy = Available();
        copy.Retire();

        Assert.Throws<InvalidOperationException>(() => copy.Recover());
    }
```

`Retired` is now the only state in the machine with no way out, and that asymmetry is right:
retirement is a judgement the shop made about a box in its own hands and can revisit by registering
it again if it ever wants to; a write-off is a guess about the outside world, and the outside world
is entitled to prove it wrong.

---

## Step 8 — The refactor, finally earned

Ten operations are now written and every one of them has the same body:

```csharp
    public void SomeOperation()
    {
        if (Status is not <one or two states>)
        {
            throw new InvalidOperationException("...");
        }

        Status = <the new status>;
    }
```

Read the class top to bottom on camera. It is about ninety lines, of which the rules — the part
anyone actually needs — are twenty, and they are spread out so far that **the state machine cannot
be seen**. Nobody can answer "what can a copy in *In inspection* do?" without scrolling through ten
methods and reading ten `if` statements.

**This step is not driven by a test, and that is the definition of the step.** Not one line of
behaviour changes; twenty-one existing tests are the entire safety net, and if any of them goes red
the refactor was wrong. This is the first proper *refactor* leg the course has shown — episodes 12
to 14 were mostly red and green — and it is worth naming: red and green make the code work,
refactor makes it readable, and the tests written in the first two legs are what make the third one
safe to attempt at all.

### Green stays green

```csharp
    private void TransitionTo(CopyStatus to, params CopyStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new InvalidOperationException(
                $"A copy cannot go from {Status} to {to}.");
        }

        Status = to;
    }
```

and every operation collapses to one line:

```csharp
    public void Reserve() => TransitionTo(CopyStatus.Reserved, CopyStatus.Available);

    public void ReleaseReservation() => TransitionTo(CopyStatus.Available, CopyStatus.Reserved);

    public void Collect() => TransitionTo(CopyStatus.OnRent, CopyStatus.Reserved);

    public void Return() => TransitionTo(CopyStatus.AwaitingInspection, CopyStatus.OnRent);

    public void BeginInspection() => TransitionTo(CopyStatus.InInspection, CopyStatus.AwaitingInspection);

    public void Shelve() => TransitionTo(CopyStatus.Available, CopyStatus.InInspection);

    public void SendForRepair() => TransitionTo(CopyStatus.InRepair, CopyStatus.InInspection);

    public void CompleteRepair() => TransitionTo(CopyStatus.Available, CopyStatus.InRepair);

    public void WriteOffAsLost() => TransitionTo(CopyStatus.Lost, CopyStatus.OnRent);

    public void Recover() => TransitionTo(CopyStatus.AwaitingInspection, CopyStatus.Lost);

    public void Retire() =>
        TransitionTo(CopyStatus.Retired,
            CopyStatus.Available, CopyStatus.InInspection, CopyStatus.InRepair);
```

`dotnet test` — twenty-one green. **The state machine is now eleven lines and can be read as a
table**, which is what it was on the whiteboard in step 0 and should have been in the code all
along.

`Retire` is the line to linger on. Its allowed-from list contains three states, and the argument of
step 5 is now visible as *the two that are missing*: `Reserved` and `OnRent` are simply not there.
The rule that shapes the service is legible at a glance, in the one place someone would look.

### Two costs, and the second one is a real language constraint

**First: the error messages got worse.** Ten hand-written sentences (*"A copy cannot be retired
while a customer has it"*) became one generated one (*"A copy cannot go from OnRent to Retired"*).
That is a genuine loss — the hand-written version explained the rule; the generated version states
the fact. It is accepted here because the generated message still contains the two things a caller
needs to act on, and because ten near-identical strings drift: someone edits one, nobody edits the
other nine, and the messages stop agreeing with each other. **One message that is slightly worse
beats ten that are slowly diverging.**

**Second, and worth showing because it is the kind of thing that quietly forces a design:** the
first instinct is to keep the operation name in the message with `[CallerMemberName]`:

```csharp
// Does not compile.
private void TransitionTo(
    CopyStatus to,
    [CallerMemberName] string operation = "",
    params CopyStatus[] allowedFrom)
```

`params` must be the last parameter, and an optional parameter after it can never be filled in by
name at a `params` call site. The two features are mutually exclusive here. Fixes exist — a
`ReadOnlySpan<CopyStatus>` and a collection expression, or passing the name explicitly — and each
costs more than the message is worth. **Say it out loud rather than quietly rearranging:** the
message names the states instead of the operation, and the states are what the caller was asking
about.

### The version this episode does not build

Since the guard is now generic, the obvious next step is to make it public and delete the ten
methods:

```csharp
// Not this.
public void TransitionTo(CopyStatus to)
{
    if (!Legal.Contains((Status, to)))
    {
        throw new InvalidOperationException($"A copy cannot go from {Status} to {to}.");
    }

    Status = to;
}

private static readonly HashSet<(CopyStatus From, CopyStatus To)> Legal =
[
    (CopyStatus.Available, CopyStatus.Reserved),
    (CopyStatus.Reserved, CopyStatus.OnRent),
    // ...
];
```

It is shorter still, and it is **episode 14's boolean flag in a better disguise** — the argument
decides what happened. Three consequences, and they are the same three as last time:

1. **The call site stops naming a business event.** `copy.TransitionTo(CopyStatus.Lost)` describes a
   field assignment. `copy.WriteOffAsLost()` describes something that happened in the shop, and it
   is the version that still means something in a log line or a stack trace.
2. **`Shelve` and `CompleteRepair` become indistinguishable again** — both are
   `TransitionTo(Available)` — so step 4's whole argument is undone, and the notification code is
   back to inspecting the previous status to work out which message to send.
3. **Every caller can name any state.** Retirement, write-off and collection are all one method
   away from anyone holding a `Copy`, and the only thing standing between a caller and a status
   they had no business setting is a pair in a set. Authorization in episode 28 has to attach to
   *operations*; there would be one operation to attach it to.

The private helper keeps the deduplication. The public methods keep the vocabulary. **The
duplication was never the problem — the duplication was the ten `if` statements, and those are
gone.**

### And this does not contradict the API design

`docs/architecture/catalog.md` specifies an internal endpoint that looks exactly like the rejected
version:

> `POST /copies/{id}/status` — transition requested by another service […] the status transition
> endpoint is a **command**, not a `PATCH` of a status field: callers ask for a transition and the
> state machine decides, rather than asserting a new value.

Both things are true, and the resolution is worth stating because it comes up constantly. An HTTP
API is a **wire format** and has to accept a serialisable value — another service cannot POST a
method call. The endpoint receives a requested status and maps it to the named operation:

```csharp
// Episode 23, roughly.
CopyStatus.OnRent  => copy.Collect(),
CopyStatus.Lost    => copy.WriteOffAsLost(),
```

**A command-shaped API does not require a command-shaped domain.** The mapping lives in one place,
at the edge, where translating an outside vocabulary into the domain's own is exactly the job.

---

## Step 9 — About that exception type, still

Every refusal in this episode throws `InvalidOperationException`, and it is still **provisional**,
for the same reason as episode 14: the type cannot distinguish *the business said no* (a `409`, and
nothing is broken) from *the code is broken* (a `500`, and somebody gets paged).

Episode 20 fixes it on camera, once there is an endpoint in front of these rules to show a
perfectly reasonable staff request coming back as `500 Internal Server Error`. Twenty-one more
throws written today will be refactored there — which is a slightly better argument for waiting
than episode 14 could make, since the cost of deferring is now visible and measurable rather than
hypothetical.

---

## Step 10 — Through the gates

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

PR, green, merge, deployed. The sixth episode in a row that changes no workflow, no infrastructure
and no configuration — **and the last one.** The domain is now as complete as it can get without
somewhere to put it, and episode 16 opens `docker-compose.yml` for the first time since episode 4.

---

## What this episode deliberately does not do

- **No persistence, and no `retired_at`.** The architecture document gives `copies` a `retired_at`
  column, because *when* a copy was retired is a question stock reports ask. That is a database
  concern with a database default, and it arrives with the table in episode 16.
- **No optimistic concurrency.** Two staff scanning the same box at once is not hypothetical, and
  the second write should fail loudly rather than overwrite the first — but a row version needs a
  row. Episode 16. **Note the limit honestly:** every guard in this class protects one object
  in-process. Nothing here stops two threads reading `Available` and both reserving it.
- **No events.** `CopyStatusChanged` and `CopyRetired` are what the rest of the system reacts to —
  UC-3.4's "it is free" notification is triggered by a copy reaching *Available*, and UC-3.6's by a
  retirement. Publishing to nobody teaches nothing; the messaging module retrofits them, and step
  4 has already left two obvious places to publish from.
- **No join between grade and status.** `RaiseGradeAfterRepair` is still callable on a copy sitting
  on the shelf, which UC-10.5 does not allow — a repair raises a grade, and a copy that was never
  in repair was never repaired. That is a real gap, it is left open on purpose to keep this episode
  about one idea, and episode 23 closes it when the two rules meet behind an endpoint.
- **No reservation clock.** `ReleaseReservation` models the transition; deciding *when* a
  reservation has expired belongs to whoever owns reservations.
- **No damage log** (UC-10.3), **no baseline weight check** — both are things an inspection
  *records*, not moves a copy *makes*.
- **No endpoint and no authorization.** Retiring is staff-only and recovering from *Lost* is
  manager-only, and both are policies attached to operations in episode 28 — not `if` statements in
  the domain.
- **No domain exception type** — see step 9.

## Verification

```bash
dotnet build                       # 0 warnings, 0 errors
dotnet test                        # green, twenty-one new tests
dotnet format --verify-no-changes  # exits 0
```

Then three deliberate breakages, each of which should turn something red:

1. Add `CopyStatus.OnRent` to `Retire`'s allowed-from list. One test fails —
   `A_copy_that_is_out_on_rent_cannot_be_retired`. **That single argument is the entire service
   boundary argument**, which is worth sitting with: the reason catalog owns copy status is one
   enum value in one list, and it is guarded by one test.
2. Swap the allowed-from states of `Shelve` and `CompleteRepair`. Two tests fail, and they are the
   two that keep the repair path and the inspection path from collapsing into each other.
3. Change `Recover`'s destination from `AwaitingInspection` to `Available`. One test fails — the
   copy that spent three weeks nobody-knows-where goes straight to the next customer.

## Next

[Episode 16 — Postgres in Compose, EF Core mapping](episode-16.md):
Postgres joins `docker-compose.yml`, and `Money`, `ConditionGrade`, `CopyStatus` and `Copy` get a
`DbContext`, explicit `IEntityTypeConfiguration` classes and a first migration committed as a
reviewable file.

One decision made today lands there immediately. **`CopyStatus` must persist as its name, not its
ordinal** — the enum was declared in the order `docs/IDEA.md` lists the states, this episode said
out loud that reordering it changes no behaviour, and storing ordinals would make that a lie in the
most expensive way available: every copy in the database silently changes state the day somebody
tidies the enum. It is the same argument episode 14 made for `ConditionGrade`, arriving now as a
column type.
