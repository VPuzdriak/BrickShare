# BrickShare — The Idea

## The idea

LEGO sets are expensive. A large licensed set can cost more than a games console, gets built
once, sits on a shelf, and then gets taken apart. For a lot of people the appeal is the
*building*, not the owning.

BrickShare is a rental business built on that observation. The organisation buys sets once
and rents them out repeatedly. A customer pays a fraction of the retail price, builds the
set, enjoys it for a while, and brings it back. The organisation earns from the same box
many times over.

## Business model

BrickShare **owns all of its inventory**. It is a business-to-consumer rental service, not a
platform connecting people who own sets with people who want them.

This is worth stating plainly because it rules a lot out:

- There is **no owner or host role** — no one lists their own sets.
- There is **no supply-side onboarding**, no payouts to third parties, no revenue split.
- Stock is acquired centrally by the business as a purchasing decision.

Customers collect and return sets **in person**. There is no shipping, and therefore no
couriers, no tracking numbers, and no delivery addresses.

BrickShare operates a **single shop**. There is no per-location stock, no transfers between
branches, and no choice of collection point.

## Actors

**Customer** — browses the catalog, sees what is available, reserves a copy for a pickup date,
collects it, builds it, and returns it. Can see their own rental history.

**Staff** — maintain the catalog, register new physical copies into stock and retire worn
ones, hand sets over at pickup, receive them on return, inspect and grade their condition,
log missing pieces, and assess damage when something comes back in a worse state than it
left.

## Core concepts

**Catalog set** — the product as LEGO defines it: set number, name, year, theme, piece count,
image, age rating, retail price. One catalog entry, however many boxes the business owns. The
specs are recorded **once**, when the set is first catalogued, and every copy inherits them.

The lookup covers the **product facts** Rebrickable holds — name, year, theme, piece count,
image. Everything else is **typed by staff** on the prefilled form, and all of it is per
catalog set:

| Field | What it drives |
| --- | --- |
| Retail price | The deposit — retail × the copy's grade multiplier |
| Base rental price | The price of one minimum-duration rental, before grade adjustment |
| Minimum rental duration | The billing floor, and the daily rate |
| Age rating | Browsing |

Every one of these differs between a Titanic and a small set, which is why none of them is a
system-wide setting. The **only** global configuration is the condition-grade multipliers and
the 28-day maximum rental duration.

**Retail price is load-bearing data**, not decoration — it determines the deposit. It is
recorded on the catalog entry once and then used from there.

**Physical copy** — one actual box on one actual shelf. This is the concept that makes
BrickShare interesting. Copies are **not interchangeable**: a copy that has been rented
fifteen times and is missing two pieces is a different thing from a sealed one, even though
both are "set 10294". Each copy has its own identity, condition grade, completeness record
and rental history.

Copies carry a **barcode/QR label** minted by BrickShare and stuck on the box — LEGO boxes
have no per-unit serial, so identity has to be issued by us. Staff scan the label at
handover, return and inspection, which is what stops two physically identical Titanics from
being confused.

**Copy status** — where a copy is in its cycle. Only **Available** can be reserved.

| Status | Meaning |
| --- | --- |
| **Available** | On the shelf, rentable now |
| **Reserved** | Claimed by a customer who has not yet collected it |
| **On rent** | Collected, out with a customer |
| **Awaiting inspection** | Physically back, not yet checked — **not rentable** |
| **In inspection** | Being graded and counted |
| **Lost** | Written off, never returned |
| **Retired** | Too worn to rent; still held by the shop |

The gap between *Awaiting inspection* and *Available* is deliberate and matters: a box that
has just been handed back is not yet ready for the next customer. It has to be checked,
regraded and put back on the shelf first.

**Condition grade** — where a copy sits on a fixed four-tier scale:

| Grade | Meaning |
| --- | --- |
| **New** | Never rented. Sealed or as-new. |
| **Excellent** | Rented, but complete and showing no meaningful wear. |
| **Good** | Visible wear to box or pieces; complete or trivially incomplete. |
| **Fair** | Noticeably worn, or missing pieces that don't prevent the build. |

Grade is not just a damage record — it **sets the price**, so it is a commercial judgement
as much as a physical one. Two rules follow. **New is unrecoverable**: once a copy has gone
out and come back it can never be New again. And grades otherwise only move **downward**,
unless staff deliberately override after a repair or piece replacement.

**Rental price** — each grade carries a **percentage multiplier**, set by an admin and applied
across the whole catalog. A copy's rental price for one minimum duration is the catalog set's
base rental price × its grade's multiplier. From that comes the **daily rate**:

> daily rate = the copy's rental price ÷ the set's minimum rental duration

Because a copy can be regraded at inspection, the price is **fixed at the moment of
reservation** — a regrade changes what the *next* customer pays, never what an existing
reservation costs.

**Rental duration** — how long a customer may keep a set. Each catalog set defines a
**minimum**; the maximum is **28 days for every set**, and the customer does **not** choose a
length: they keep the set as long as they like, up to that maximum.

The minimum is a **commercial floor**, not an estimate. Return on or before it and you have
still paid for it; return later and you pay the daily rate for the extra days. There is **no
due date**, and consequently no such thing as being overdue — the 28-day maximum is the only
deadline that exists. A set's minimum must not exceed it.

**Why 28 and not 30.** The deposit authorization lives **30 days** from collection, and an
expired authorization **cannot be captured** — wait for it to lapse and there is nothing left
to take. Capping rentals at 28 days leaves a **two-day buffer** in which the write-off is
guaranteed to find a live hold. That is far safer than firing write-off exactly as the
authorization dies: the buffer absorbs retries, delayed jobs, timezone edges and weekend runs
without the money ever escaping.

| Day | Event |
| --- | --- |
| 0 | Collection. Deposit hold placed, valid 30 days. |
| 25 | Customer emailed: three days left to return the set. |
| 28 | **Write-off.** Deposit captured, copy lost, customer marked, staff notified. |
| 30 | The hold would have expired. Two days of headroom, deliberately unused. |

The cost of a single global maximum is that staff can no longer turn small sets over faster —
a cheap 200-piece set may now be held as long as a Titanic. That per-set lever is given up so
that every rental fits inside one deposit-hold window.

**Rental** — an agreement covering one customer and one physical copy. Moves through a
lifecycle: reserved → collected → out → returned → inspected → closed.

**Availability and the reservation horizon** — a customer can only reserve a copy that is
**on the shelf now**. A copy that is out with someone else cannot be booked, because its
return date is not something the shop controls — the holder may keep it for another three
weeks, or never bring it back. So there is no booking calendar, no overlapping dates, and no
chain of downstream bookings to unravel.

What a customer *does* choose is a **pickup date, at most 7 days ahead**. The reserved copy
sits off the shelf and unavailable to anyone else until then. Seven days is not a round
number picked for convenience: it is how long a card authorization can be relied on to
survive, and the money must still be locked when the customer arrives.

**Deposit** — an amount held against the rental to cover **loss, damage and unpaid rental
fees**. Released when the set comes back complete, undamaged and paid for; partially retained
when it is not; taken in full when the set never comes back at all.

That third purpose is deliberate and load-bearing — the final fee at return is settled out of
this hold rather than charged separately. It must therefore be part of what the customer
agrees to when reserving: a deposit described only as covering loss and damage cannot lawfully
be used to collect a rental fee.

The amount is **calculated, never typed**:

> deposit = the catalog set's retail price × the copy's condition-grade multiplier

It is therefore a property of the **copy**, not the catalog entry, and it uses the same grade
multipliers as the rental price. The reasoning is that the deposit stands in for the value of
the specific box that was lost: a customer who keeps a Fair-grade set cost the shop a worn
set, not a new one, so charging full retail would over-recover. The accepted trade-off is the
mirror image — a deposit taken on a worn copy will not fully fund a brand-new replacement.

Like the rental price, the deposit amount is **fixed at reservation**. The hold itself is
placed later, at collection.

*Known limitation, accepted for now:* retired sets can trade well above their original retail
price, so a retail-based deposit under-covers collectibles.

**When money moves** — three distinct moments, and only one of them is a charge at the time it
happens:

| Moment | What happens |
| --- | --- |
| **Reservation** | The minimum-duration rental fee is **locked** (authorized), not charged |
| **Collection** | That lock is **captured**, and the **deposit hold** is placed |
| **Return** | The **final fee** is taken **out of the deposit hold**, and the balance released |

The final fee is `daily rate × (days held − minimum days)`, and is **zero** for a set returned
on or before the minimum. Nothing is refunded for an early return.

Settling the final fee from the deposit hold rather than charging the customer again is what
removes the last failure point. A card authorization can be captured for **less** than the
amount authorized, and doing so **releases the remainder in the same operation** — normally a
limitation, but exactly right here, because the deposit was going to be released at that
moment anyway. So return is a single money operation: capture the final fee plus any damage
retention, release the rest. Return within the minimum with nothing to pay and nothing damaged
and the whole hold is simply released.

Coverage is never in doubt: a deposit is the set's full retail price, while a final fee is a
few days at the daily rate.

Locking rather than charging at reservation does the same job earlier in the flow: a cancelled
reservation releases the lock and costs nothing, and a customer who never appears has their
lock captured. **At no point in the entire lifecycle is there a fresh charge that could
decline** — every movement of money is a capture or release of something already authorized.

**Two holds exist, and they are not equally safe.** The reservation lock lives at most 7 days,
comfortably inside a standard card authorization. The deposit hold is taken for **30 days**
against a rental capped at **28**, which requires an *extended* authorization — see the note
under Product decisions.

The deposit hold now carries **two jobs at once**: insuring the copy, and settling the final
fee. That is a simplification in the normal case but it concentrates risk — if the hold is not
there when the set comes back, the shop loses its damage cover and its means of collecting
payment in the same stroke.

**Customer standing** — whether a customer is allowed to rent. A customer may be marked
**unreliable**, which **blocks them from starting new rentals** while leaving everything else
untouched: they can still sign in, browse sets, check availability and read their own
history. It restricts one capability; it is not an account suspension. It also has no effect
on a rental already in progress — a set already out still has to come back.

Every mark and every removal is **audited**: who did it, when, and why. Marks are kept as
**history**, not a single overwritable flag, so a customer marked, cleared, and marked again
has three recorded events rather than one current value.

**Waitlist** — when every copy of a set is unavailable, a customer can register interest and be
told when one is free again. Nothing is held for them. The notification fires when a copy
reaches **Available**, after inspection — announcing a set the moment it is handed back would
be telling people it is ready when it is not.

Everyone waiting is notified at once and the first to reserve gets it; the others stay on the
list. This is a deliberate trade for simplicity over a fair queue with a held copy. Waiting
is per **catalog set**, not per copy — customers want *a* Titanic, not a particular box — and
a customer leaves the list when they rent the set, or by choice.

**Inspection** — what staff do when a set comes back. Grade the condition, count the pieces,
photograph anything notable, and decide whether the deposit is released in full.

## Product decisions

Decisions already made and treated as settled:

| Area | Decision |
| --- | --- |
| Business model | The organisation owns all inventory; B2C rental. Actors are customer and staff. There is no owner role. |
| Locations | A **single shop**. No per-location stock, no transfers, no collection-point choice. |
| Fulfilment | In-store pickup. No shipping, no carrier integration. |
| Inventory granularity | Individually-tracked physical copies. Copies are **not** fungible — each has its own condition and history. |
| Copy identity | A BrickShare-minted barcode/QR label per box, scanned at handover, return and inspection. |
| Copy status | Available / Reserved / On rent / Awaiting inspection / In inspection / Lost / Retired. Only **Available** is reservable. |
| Condition grades | A fixed four-tier scale: New / Excellent / Good / Fair. |
| Catalog specs | Product facts (name, year, theme, piece count, image) fetched from **Rebrickable** by set number to prefill the form. |
| Missing spec data | Cataloguing a set **blocks** until the Rebrickable lookup succeeds. |
| Manual entry | Staff type four per-set fields: **retail price**, **base rental price**, **minimum rental duration**, **age rating**. |
| Per-set vs global | Everything set-specific is per catalog set. The only global configuration is the **grade multipliers** and the **28-day maximum rental duration**. |
| Rental duration | The customer picks **no duration**. They keep the set up to the maximum. There is **no due date** and no overdue state. |
| Duration limits | The minimum is **per set** (a billing floor). The maximum is a global **28 days**, identical for every set; staff cannot change it. A set's minimum may not exceed 28 days. |
| The 28/30 buffer | The deposit hold runs **30 days** but rentals cap at **28**, leaving two days in which write-off is guaranteed to find a live authorization. An expired hold cannot be captured, so this margin is what stops the money escaping. **Do not "tidy" the maximum up to 30.** |
| Price calculation | Base rental price × the grade multiplier gives one minimum-duration rental. Daily rate = that ÷ the minimum duration. |
| Deposit amount | **Calculated, not entered**: retail price × the copy's grade multiplier. A property of the copy. Fixed at reservation. |
| Booking horizon | A customer reserves an **available** copy for a pickup date **at most 7 days ahead**. A copy that is out cannot be booked. |
| Reserved stock | A reserved copy is unavailable to everyone else until pickup. Accepted; no per-customer reservation limit. |
| Money at reservation | The minimum-duration fee is **locked, not charged**. |
| Cancellation | Free if cancelled **24h or more** before the pickup date — the lock is released. Inside 24h it is **captured**, as for a no-show. |
| No-show | The lock is **captured** in full. The copy returns to *Available* without inspection. |
| Money at collection | The lock is captured and the **deposit hold** is placed. |
| Money at return | The **final fee** = daily rate × days beyond the minimum. Zero if returned within the minimum; nothing refunded for early return. |
| Settling the final fee | Taken **out of the deposit hold**, never charged separately. A partial capture releases the balance in the same operation, so return is one money movement. **No step in the lifecycle is a fresh charge that could decline.** |
| Deposit purpose | Covers **loss, damage and unpaid rental fees**. The third must be stated in the customer's terms at reservation, or the shop cannot lawfully settle fees from it. |
| Return warning | On **day 25** — three days before the maximum — the customer is emailed a warning. |
| Non-return | On **day 28**: deposit captured, the copy recorded as **lost**, the customer marked unreliable, and **staff notified**. One event. |
| Deposit sizing | The deposit covers the **set's value only**. It is not sized to also cover rental fees, so the days between the minimum and day 28 are never recovered on a write-off — the shop keeps the minimum fee plus the set's value and absorbs the rest. |
| Recovery | A written-off set may be brought back and accepted at **staff discretion**. Manual only. The deposit may be refunded in whole or in part. |
| Customer standing | A customer may be marked **unreliable**, blocking new rentals only. Applied automatically at write-off or manually by staff; removable by staff. Both audited. |
| Mark reason | A fixed category plus a free-text note, linked to the rental that caused it. |
| Waitlist | Customers register interest in a set with no free copies and are notified when one reaches *Available*. Nothing is held; first to reserve wins. |
| Payments | Stripe, as a real integration. |
| Deposit hold — **unverified** | The 30-day deposit hold assumes an **extended** card authorization. This is **not confirmed**: extended windows are gated on merchant category (travel, lodging, vehicle rental — which a LEGO shop likely is not), the default authorization window is around 7 days, issuers often release holds early regardless of network rules, and requiring cards excludes wallets. **Must be verified with Stripe before it is relied on.** Fallback: capture the deposit up front and refund it on return. |
| Auth | Staff sign in with organisation accounts. Customers sign in with email or a social account. |

## Use cases

Being defined incrementally, one area at a time. **This set is not yet complete** — see
*Still to define* below.

### UC-1 — Staff manage the catalog and physical stock

> *As staff, I register the LEGO sets the shop owns, without re-entering product specs for
> every physical box.*

The shop might own three Titanics. That is **one** catalog entry and **three** copies: the
specs are described once, and the copies carry only what actually differs between boxes.

**UC-1.1 — Catalogue a new set.** Staff type a LEGO set number. BrickShare looks it up on
Rebrickable and **prefills the form** with name, year, theme, piece count and image. Staff
then type the four fields Rebrickable cannot supply: **retail price**, **base rental price**,
**minimum rental duration** and **age rating**. The minimum is rejected if it exceeds 28 days.
There is no maximum to enter — it is a fixed 28 days for every set. The deposit is *not*
entered either; it is derived from the retail price.

**UC-1.2 — Register physical copies.** Staff pick an existing catalog entry and register one
or more copies against it, either individually or in a batch ("we bought 3"). Each copy is
assigned an identity and a barcode/QR label to print and stick on the box, and is given a
starting condition grade — normally *New*.

**UC-1.3 — Retire a copy.** A copy too worn or too incomplete to rent is retired. It stops
being rentable but remains in the system.

**UC-1.4 — Review stock.** Staff see, per catalog set, how many copies exist and what status
each one is in, and can look up any individual copy by scanning its label.

**UC-1.5 — Maintain pricing.** An admin adjusts the percentage multiplier attached to each
condition grade.

#### Business rules

- A catalog entry may exist with **zero copies** — a planned purchase, or a set whose every
  copy has been retired. It simply is not rentable.
- **Retiring is a state change, never a deletion.** Rental history must survive it.
- A copy **cannot be retired while a rental is active** on it.
- Cataloguing a new set **requires a successful Rebrickable lookup**. Accepted consequence:
  during a Rebrickable outage, staff cannot catalogue a set the shop has never stocked
  before. Registering further copies of an **already-catalogued** set needs no external call
  and keeps working, and a set's data is fetched once, not once per copy.
- A grade **never improves on its own**, and **New can never be regained** once a copy has
  been rented. Staff may deliberately override a grade upward after a repair.
- Changing a grade multiplier **re-prices the entire catalog**. That is intended, and the flow
  should say so plainly before it is applied.

### UC-2 — Non-return and customer standing

> *As the shop, when a customer simply keeps a set, I want to recover its value, take the
> copy out of stock, and stop that person renting again — with a record of why.*

**UC-2.1 — Warn before the deadline.** On **day 25**, three days before the 28-day maximum,
the customer is emailed: three days left to return the set, or the deposit is charged.

**UC-2.2 — Write off an unreturned rental.** **Day 28** arrives with the set still out. Four
things happen together, as one event: the deposit is **captured in full**, the copy is
recorded as **lost**, the customer is **marked unreliable** with the category *set not
returned*, a note and a link to the rental, and **staff are notified that the set is lost**.

**UC-2.3 — Mark a customer manually.** Staff mark a customer unreliable for reasons the
write-off rule doesn't catch — repeated no-shows, sets returned wrecked. Same requirement: a
category, a note, and an identified member of staff.

**UC-2.4 — A marked customer tries to rent.** The reservation is refused. Browsing,
availability, sign-in and rental history all continue to work.

**UC-2.5 — Lift a mark.** The customer sorts it out with the shop; staff remove the mark. The
removal is recorded with who did it, when, and why.

#### Business rules

- Write-off fires on **day 28**, and nowhere else. There is no due date and no overdue period
  preceding it.
- Write-off must run **while the deposit authorization is still live**. The hold lasts 30 days
  and the rental caps at 28 precisely so that this is guaranteed — an expired authorization
  cannot be captured, and waiting for the boundary would risk finding nothing there.
- **Lost is not retired.** Retiring is a decision about a worn set the shop still holds; lost
  means it is gone. They are different end states and stock figures must not merge them.
- A mark **blocks starting new rentals only**. It does not suspend the account, and it has no
  effect on a rental already out — that set is still owed back.
- **Marking and removal are both audited**, with the same detail: who, when, why.
- Marks are **history, not a boolean**. Marked, cleared, marked again is three events.
- Every mark carries a **category and a free-text note**. Categories: *set not returned /
  repeated no-shows / returned damaged / other*. The category makes it reportable; the note
  makes it defensible when a customer contests it at the counter.

### UC-3 — Find a set and reserve it

> *As a customer, I want to take out a set that is free right now, and be told when a set I
> want comes back.*

**UC-3.1 — Reserve an available copy.** The customer picks a set and a **pickup date up to 7
days ahead**. BrickShare claims one *Available* copy for them and **locks** the
minimum-duration fee — no charge yet. Rental price and deposit amount are fixed at this
moment, from that copy's grade.

**UC-3.2 — Join a waitlist.** Every copy of a set is unavailable, so the customer registers
interest in the set.

**UC-3.3 — Be notified a set is free.** A copy passes inspection and returns to *Available*.
Everyone waiting on that set is notified. Nothing is held — whoever reserves first gets it,
and the rest stay on the list.

#### Business rules

- **Only *Available* copies can be reserved.** A copy that is out cannot be booked, whatever
  date is requested.
- A reserved copy is **unavailable to everyone else** until pickup. There is no limit on how
  many reservations one customer may hold.
- Price and deposit are **fixed when the reservation is made**, from the copy's grade at that
  moment.
- The pickup date may be **at most 7 days ahead** — the fee lock has to still be alive when
  the customer arrives.
- Waitlists are per **catalog set**, not per copy, and notification fires on entry to
  *Available*, never on physical return.
- A customer marked unreliable may join a waitlist, but reserving is still refused.

### UC-4 — Collect, cancel, or fail to collect

> *As a customer I want to change my mind without penalty if I do it in good time; as the
> shop I don't want copies held off the shelf for nothing.*

**UC-4.1 — Cancel in good time.** The customer cancels in the app, **24 hours or more** before
the pickup date. The lock is released and nothing is charged. The copy returns to *Available*
and anyone waitlisted on that set is notified.

**UC-4.2 — Cancel late.** The customer cancels **less than 24 hours** before the pickup date.
The lock is **captured** — the same outcome as not turning up at all.

**UC-4.3 — Collect the set.** The customer arrives on the pickup date. Staff scan the copy's
label and hand it over. The lock is **captured**, and the **deposit hold** is placed. The copy
moves to *On rent* and the maximum-duration clock starts.

**UC-4.4 — Fail to collect.** The pickup date passes without collection. The lock is
**captured**, the reservation closes, and the copy returns to *Available* — skipping inspection
entirely, because it never left the shelf. Waitlisted customers are notified.

**UC-4.5 — Return the set and settle.** The customer brings the set back. Staff scan the label
and the copy moves to *Awaiting inspection*. The money is settled in **one operation**: the
final fee, plus anything retained for damage, is **captured from the deposit hold**, and the
balance is released. Returned within the minimum with nothing to pay and nothing damaged, and
the entire hold is released untouched.

*(The receiving and inspection flow itself — grading, counting pieces, assessing damage — is
still to be defined; only the money is settled here.)*

#### Business rules

- The rental fee is **locked at reservation and captured at collection**, never charged twice.
- Cancelling is free at **24 hours or more** before the pickup date, and costs the full
  minimum-duration fee inside that window.
- A no-show is declared at the **end of the pickup date**.
- Cancellation and no-show are the **only** routes from *Reserved* back to *Available* that
  skip the inspection states.
- The maximum-duration clock runs **from collection**, not from reservation.
- The final fee is **never charged separately** — it comes out of the deposit hold. Capturing
  part of a hold releases the rest automatically, so settlement is always a single movement.
- Consequently **no step in the rental lifecycle is a fresh charge that can decline.** Every
  movement of money is a capture or release of an authorization taken earlier.

### UC-5 — Recovery after write-off

> *As staff, I want to be able to take a set back from someone who genuinely could not return
> it in time, without pretending the write-off never happened.*

**UC-5.1 — Customer brings back a written-off set.** They come to the shop with the set and
explain. There is no way to request this in the app; it happens in person.

**UC-5.2 — Staff decide.** Staff may accept the set back or refuse. If accepted, the copy
enters *Awaiting inspection* and rejoins stock through the normal inspection path.

**UC-5.3 — Settle the deposit.** Staff may refund the deposit **in full, in part, or not at
all**, informed by what the inspection finds.

**UC-5.4 — Lift the mark, or don't.** Staff may remove the unreliable mark. It is a separate
decision from accepting the set back.

#### Business rules

- Recovery is **entirely manual and discretionary**. The customer is not entitled to it and
  cannot trigger it from the app.
- The copy re-enters stock as **Awaiting inspection**, never straight to *Available* — it has
  been outside the shop's control and must be checked.
- Accepting the set, refunding the deposit and lifting the mark are **three separate
  decisions**. Staff may do any without the others.
- All three are **audited**: who, when, why.

### Still to define

- **Customer journey** — browse and search, view rental history, notifications.
- **Staff counter flows** — receiving on return, inspection and grading, logging missing
  pieces, damage assessment against the deposit.
- **System-automated behaviour** — the return warning, no-show detection, write-off, deposit
  release.

## Open product questions

- Should a hold **released early by the issuer** be surfaced to staff? The 28/30 buffer only
  protects against the hold reaching its scheduled end — an issuer can drop one on day three,
  and no amount of margin helps. The shop would then have neither damage cover nor any way to
  settle the final fee, and would not find out until the set came back. Part of the wider
  unverified extended-authorization risk.
