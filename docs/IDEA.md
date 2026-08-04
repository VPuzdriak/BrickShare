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
branches, and no choice of collection point — if a copy exists and is free, it can be
collected.

## Actors

**Customer** — browses the catalog, sees what is currently available, reserves a set, pays
the rental fee and a refundable deposit, collects the set, builds it, and returns it. May
extend a rental. Can see their own rental history.

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
| Base rental price | The rental price — base × the copy's grade multiplier |
| Rental period | The due date, and the pro-rata daily rate for extensions |
| Age rating | Browsing |
| Minimum extension days | The floor on a single extension request |
| Maximum total days out | The ceiling on how long a copy may be out |

Every one of these differs between a Titanic and a small set, which is why none of them is a
system-wide setting. The **only** global pricing configuration is the set of condition-grade
multipliers.

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
regraded and put back on the shelf first. Only that last step makes it rentable again.

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

**Rental price** — the catalog set carries a **base price**; each grade carries a
**percentage multiplier** set by an admin and applied across the whole catalog. A copy's
price is base × its grade's multiplier. Because a copy can be regraded at inspection, the
price is **fixed at the moment of reservation** — a regrade changes what the *next* customer
pays, never what an existing booking costs.

**Rental period** — how long a customer keeps the set. This is **per catalog set**, not one
global number: the Titanic takes far longer to assemble than a small Horizon set, and giving
both the same fortnight would be arbitrary. Staff set the period when the set is catalogued.
The clock runs **from collection** — due date is collection date plus the set's period.

A set's rental period can never exceed its own **maximum total days out**. Otherwise a rental
would breach that ceiling the moment it was collected, before any extension existed. Both
figures are typed on the same form, so this is checked as the set is catalogued.

**Rental** — an agreement covering one customer and one physical copy for a period of time.
Moves through a lifecycle: reserved → paid → collected → active → returned → inspected →
closed.

**Availability is "now", never a future date.** A customer reserves a copy that is free at
that moment; nobody books a date range against a copy that is currently out. The shop cannot
promise a return date it does not control — the customer holding the set may extend it, bring
it back late, or never bring it back at all. So there is no booking calendar, no overlapping
dates, and no chain of downstream bookings to unravel when one rental runs long.

**Extension** — more time on a rental the customer already holds, requested through the app.
Extensions are sold in days and priced **pro-rata**: the daily rate is the copy's rental price
divided by the set's rental period, multiplied by the days asked for. It is charged when the
extension is granted.

Two limits apply, and both are **properties of the catalog set**, typed by staff — not
system-wide settings: a **minimum extension days** per request, and a **maximum total days
out**. A single rule governs whether a request needs a human:

> An extension is granted **automatically** when it is at least that set's minimum, keeps the
> rental inside that set's maximum total days out, and the rental is not already overdue. A
> request that would exceed the maximum, or one on an overdue rental, goes to **staff**, who
> may grant it as an exception.

So the ceiling is enforced by the system but a person can still override it. The maximum
counts **total days out across all extensions**, not the number of extensions — three short
extensions and one long one should be judged the same way. Extending moves the due date, and
the write-off threshold moves with it. Extending never changes the deposit: the deposit
reflects what the copy is worth, and time does not alter that.

Refusing automatic extensions on overdue rentals is deliberate — otherwise extending becomes
a quiet way to escape being written off.

**Reaching the maximum does not end the rental.** It only means no further extension will be
granted automatically. When a rental reaches its set's ceiling, **the customer and staff are
both notified** — the customer is told they cannot extend again and must return by the due
date. After that the ordinary path applies: due, then overdue, then written off one further
rental period later. Warning first is what makes that write-off defensible.

*Worked example.* A set catalogued with a 7-day rental period, a 2-day minimum extension and
a 30-day maximum: extensions come in 2-day-or-larger steps, are auto-granted up to 30 total
days out, and at day 30 the customer is notified. If the set never comes back it is overdue
from day 31 and written off on day 37.

**Waitlist** — when every copy of a set is out, a customer can register interest and be told
when one is free again. Nothing is held for them. The notification fires when a copy reaches
**Available**, after inspection — announcing a set the moment it is handed back would be
telling people it is ready when it is not.

Everyone waiting is notified at once and the first to reserve gets it; the others stay on the
list. This is a deliberate trade for simplicity over a fair queue with a held copy. Waiting
is per **catalog set**, not per copy — customers want *a* Titanic, not a particular box — and
a customer leaves the list when they rent the set, or by choice.

**Deposit** — a refundable amount held against the rental to cover loss or damage. Released
when the set comes back complete and undamaged; partially retained when it does not; kept in
full when the set never comes back at all.

The amount is **calculated, never typed**:

> deposit = the catalog set's retail price × the copy's condition-grade multiplier

It is therefore a property of the **copy**, not the catalog entry, and it uses the same grade
multipliers as the rental price. The reasoning is that the deposit stands in for the value of
the specific box that was lost: a customer who keeps a Fair-grade set cost the shop a worn
set, not a new one, so charging full retail would over-recover. The accepted trade-off is the
mirror image — a deposit captured on a worn copy will not fully fund a brand-new replacement.

Like the rental price, the deposit is **fixed at the moment of reservation**. A regrade
afterwards never changes what an existing booking is holding.

The deposit is held from reservation until the copy comes back or is written off, so the
longest it can ever be held is:

> maximum hold = the set's maximum total days out + the set's rental period

For a set with a 30-day maximum and a 7-day period that is **37 days**. Both inputs are per
catalog set, so this is not one system-wide number — a set with a longer period holds money
for longer. It is worth stating plainly because holding a payment authorization open is not
free and not indefinite, and the ceiling on it is a business decision made at cataloguing.

*Known limitation, accepted for now:* retired sets can trade well above their original retail
price, so a retail-based deposit under-covers collectibles.

**Customer standing** — whether a customer is allowed to rent. A customer may be marked
**unreliable**, which **blocks them from starting new rentals** while leaving everything else
untouched: they can still sign in, browse sets, check availability and read their own
history. It restricts one capability; it is not an account suspension. It also has no effect
on a rental already in progress — a set already out still has to come back.

Every mark and every removal is **audited**: who did it, when, and why. Marks are kept as
**history**, not a single overwritable flag, so a customer marked, cleared, and marked again
has three recorded events rather than one current value.

**Inspection** — what staff do when a set comes back. Grade the condition, count the pieces,
photograph anything notable, and decide whether the deposit is released in full.

## Product decisions

Decisions already made and treated as settled:

| Area | Decision |
| --- | --- |
| Business model | The organisation owns all inventory; B2C rental. Actors are customer and staff. There is no owner role. |
| Inventory granularity | Individually-tracked physical copies. Copies are **not** fungible — each has its own condition and history. |
| Fulfilment | In-store pickup. No shipping, no carrier integration. |
| Locations | A **single shop**. No per-location stock, no transfers, no collection-point choice. |
| Pricing | Per-rental fee plus a refundable deposit. |
| Price calculation | Base price on the catalog set × a percentage multiplier per condition grade. Multipliers are **global** admin configuration, not per set. |
| Deposit amount | **Calculated, not entered**: the set's retail price × the copy's grade multiplier. A property of the copy, using the same multipliers as rental price. |
| Rental period | **Per catalog set**, set at cataloguing. Runs from collection, not from booking. |
| Condition grades | A fixed four-tier scale: New / Excellent / Good / Fair. |
| Non-return | Deposit captured in full, the copy recorded as **lost**, and the customer marked unreliable. One event. |
| Write-off trigger | Proportional to the set's own rental period — overdue by one further period. The multiplier is admin configuration, not hard-coded. |
| Customer standing | A customer may be marked **unreliable**, blocking new rentals only. Applied automatically at write-off or manually by staff; removable by staff. Both marking and removal are audited. |
| Mark reason | A fixed category plus a free-text note, linked to the rental that caused it. |
| Catalog specs | Product facts (name, year, theme, piece count, image) fetched from **Rebrickable** by set number and used to prefill the form. |
| Manual entry | Staff type six per-set fields: **retail price**, **base rental price**, **rental period**, **age rating**, **minimum extension days**, **maximum total days out**. Rebrickable carries none of these. |
| Per-set vs global | Everything set-specific is **per catalog set**. The **only** global configuration is the condition-grade multipliers. |
| Period validation | A set's **rental period must not exceed its maximum total days out**, checked when the set is catalogued. |
| Deposit hold duration | Bounded by the set's **maximum total days out + rental period** — per set, not one system-wide number. |
| Booking semantics | **No forward booking.** A customer reserves a copy that is available now. A copy cannot be booked while it is out, because its return date is not guaranteed. |
| Copy status | Available / Reserved / On rent / Awaiting inspection / In inspection / Lost / Retired. Only **Available** is reservable; a returned copy is not rentable until it has been inspected and put back on the shelf. |
| Extension pricing | **Pro-rata**: the copy's rental price ÷ the set's rental period × days requested. Sold in days, with a **minimum per request set per catalog set**. Charged when granted. |
| Extension approval | **Automatic** while it stays inside that set's maximum total days out and the rental is not overdue; otherwise it goes to **staff**, who may override. |
| Extension limit | A **maximum total days out, per catalog set**, counted across all extensions rather than a maximum number of them. |
| Reaching the maximum | Does **not** end the rental. The customer and staff are notified; the ordinary due → overdue → write-off path then applies. |
| Waitlist | Customers register interest in a set with no free copies and are notified when one reaches *Available*. Nothing is held; first to reserve wins. Per catalog set, not per copy. |
| Missing spec data | Cataloguing a set **blocks** until the Rebrickable lookup succeeds — an entry cannot exist without verified data. |
| Copy identity | A BrickShare-minted barcode/QR label per box, scanned at handover, return and inspection. |
| Payments | Stripe, as a real integration. Deposits are authorization holds, captured only if needed. |
| Auth | Staff sign in with organisation accounts. Customers sign in with email or a social account. |

## Use cases

Being defined incrementally, one area at a time. **This set is not yet complete** — see
*Still to define* below. No architecture or implementation work starts until it is.

### UC-1 — Staff manage the catalog and physical stock

> *As staff, I register the LEGO sets the shop owns, without re-entering product specs for
> every physical box.*

The shop might own three Titanics. That is **one** catalog entry and **three** copies: the
specs are described once, and the copies carry only what actually differs between boxes.

**UC-1.1 — Catalogue a new set.** Staff type a LEGO set number. BrickShare looks it up on
Rebrickable and **prefills the form** with name, year, theme, piece count and image. Staff
then type the six fields Rebrickable cannot supply: **retail price**, **base rental price**,
**rental period**, **age rating**, **minimum extension days** and **maximum total days out**.
The rental period is rejected if it exceeds the maximum total days out. The deposit is *not*
entered — it is derived from the retail price. The entry is saved.

**UC-1.2 — Register physical copies.** Staff pick an existing catalog entry and register one
or more copies against it, either individually or in a batch ("we bought 3"). Each copy is
assigned an identity and a barcode/QR label to print and stick on the box, and is given a
starting condition grade — normally *New*.

**UC-1.3 — Retire a copy.** A copy too worn or too incomplete to rent is retired. It stops
being rentable but remains in the system.

**UC-1.4 — Review stock.** Staff see, per catalog set, how many copies exist and what status
each one is in — available, reserved, on rent, awaiting inspection, in inspection, lost or
retired — and can look up any individual copy by scanning its label.

**UC-1.5 — Maintain pricing.** An admin adjusts the percentage multiplier attached to each
condition grade.

#### Business rules

- A catalog entry may exist with **zero copies** — a planned purchase, or a set whose every
  copy has been retired. It simply is not rentable.
- **Retiring is a state change, never a deletion.** Rental history must survive it.
- A copy **cannot be retired while a rental is active** on it. Get it back first.
- Cataloguing a new set **requires a successful Rebrickable lookup**. Accepted consequence:
  during a Rebrickable outage, staff cannot catalogue a set the shop has never stocked
  before. Registering further copies of an **already-catalogued** set needs no external call
  and keeps working, and a set's data is fetched once, not once per copy.
- A grade **never improves on its own**, and **New can never be regained** once a copy has
  been rented. Staff may deliberately override a grade upward after a repair or piece
  replacement.
- Changing a grade multiplier **re-prices the entire catalog**. That is intended, and the
  flow should say so plainly before it is applied.
- Price is **fixed when a reservation is made**. A later regrade never changes the cost of a
  booking that already exists.

### UC-2 — Non-return and customer standing

> *As the shop, when a customer simply keeps a set, I want to recover its value, take the
> copy out of stock, and stop that person renting again — with a record of why.*

**UC-2.1 — Write off an unreturned rental.** A rental that stays out past its write-off
threshold is declared never-returned. Three things happen together, as one event: the deposit
is **captured in full**, the copy is recorded as **lost**, and the customer is **marked
unreliable** with the category *set not returned*, a note, and a link to the rental.

**UC-2.2 — Mark a customer manually.** Staff mark a customer unreliable for reasons the
write-off rule doesn't catch — repeated lateness, sets returned wrecked. Same requirement: a
category, a note, and an identified member of staff.

**UC-2.3 — A marked customer tries to rent.** The reservation is refused. Browsing,
availability, sign-in and rental history all continue to work.

**UC-2.4 — Lift a mark.** The customer approaches the shop and sorts it out; staff remove the
mark. The removal is recorded with who did it, when, and why.

#### Business rules

- The write-off threshold is **proportional to the set's own rental period** — a rental is
  written off once it is overdue by one further period. The multiplier is admin
  configuration, not a constant in the code.
- **Lost is not retired.** Retiring is a decision about a worn set the shop still holds; lost
  means it is gone. They are different end states and stock figures must not merge them.
- A mark **blocks starting new rentals only**. It does not suspend the account, and it has no
  effect on a rental already out — that set is still owed back.
- **Marking and removal are both audited**, with the same detail: who, when, why.
- Marks are **history, not a boolean**. Marked, cleared, marked again is three events.
- Every mark carries a **category and a free-text note**. The category makes it reportable;
  the note makes it defensible when a customer contests it at the counter.

### UC-3 — Reserve, extend and wait

> *As a customer, I want to take out a set that is free right now, keep it longer if I need
> to, and be told when a set I want comes back.*

**UC-3.1 — Reserve an available copy.** The customer picks a set and BrickShare claims one
*Available* copy for them. Rental price and deposit are fixed at this moment, from that
copy's grade. Copies that are out, awaiting inspection, lost or retired cannot be reserved,
and no future date can be booked against them.

**UC-3.2 — Request an extension.** The customer asks for more days through the app, at or
above that set's minimum extension days. If the extension keeps the rental inside that set's
maximum total days out and the rental is not overdue, it is granted immediately and charged
pro-rata. Otherwise it goes to staff.

**UC-3.3 — Staff decide an escalated extension.** Staff see requests that exceed the set's
maximum or sit on an overdue rental, and grant or refuse them.

**UC-3.6 — Reaching the maximum days out.** A rental hits its set's ceiling. The customer is
notified that no further extension will be granted automatically and the set must be returned
by the due date; staff are notified too. The rental itself continues — nothing is captured
and nobody is marked at this point.

**UC-3.4 — Join a waitlist.** Every copy of a set is out, so the customer registers interest
in the set.

**UC-3.5 — Be notified a set is free.** A copy passes inspection and returns to *Available*.
Everyone waiting on that set is notified. Nothing is held — whoever reserves first gets it,
and the rest stay on the list.

#### Business rules

- **Only *Available* copies can be reserved**, and only for now — there is no forward booking.
- A returned copy is **not rentable until it has been inspected** and put back on the shelf.
  *Awaiting inspection* exists precisely to stop a box being handed straight back out.
- Price and deposit are **fixed when the reservation is made**, from the copy's grade at that
  moment.
- Extensions are sold in **days, at or above that catalog set's minimum extension days**,
  priced **pro-rata** from the copy's rental price and the set's rental period, and **charged
  when granted**.
- The maximum counts **total days out across all extensions**, not the number of them, and is
  a property of the catalog set rather than a system-wide setting.
- **Reaching the maximum does not end the rental.** It stops automatic extensions and
  notifies the customer and staff; the ordinary due → overdue → write-off path continues.
- **Overdue rentals never extend automatically.** Otherwise extending would be a way to avoid
  being written off.
- Extending moves the due date and the **write-off threshold moves with it**.
- Extending **never changes the deposit** — it reflects the copy's value, which time does not
  alter.
- Waitlists are per **catalog set**, not per copy, and notification fires on entry to
  *Available*, never on physical return.
- A customer marked unreliable may join a waitlist, but reserving is still refused.

### Still to define

- **Customer journey** — browse and search, pay, collect, return, view history, receive
  reminders. (Reserving, extending and waitlists are covered by UC-3.)
- **Staff counter flows** — handover at pickup, receiving on return, inspection and grading,
  logging missing pieces, damage assessment against the deposit.
- **System-automated behaviour** — expiring unclaimed reservations, reminders before a rental
  is due, flagging overdue rentals, releasing deposits.

## Open product questions

Unresolved, and needed before the remaining use cases can be pinned down:

- Is the rental fee charged **at reservation or at collection**? The deposit hold and the fee
  need not happen at the same moment, and the answer decides what a customer loses if they
  never turn up.
- How long does an **unclaimed reservation** hold a copy before it expires and the copy goes
  back on the shelf? Without a limit, a reservation nobody collects takes a copy out of
  circulation indefinitely.
