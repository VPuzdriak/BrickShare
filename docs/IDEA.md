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

**Customer** — registers with an email address or a social account, browses and searches the
catalog, sees what is available, reserves a copy for a pickup date, collects it, builds it,
follows what it is costing them, and returns it. Can see their own rental history, and their
own standing if they have been blocked from renting.

**Staff** — maintain the catalog, register new physical copies into stock and retire worn
ones, hand sets over at pickup, receive them on return, inspect and grade their condition,
log missing pieces, and assess damage when something comes back in a worse state than it
left.

**Manager** — a member of staff with authority over the **exceptional** decisions: accepting a
written-off set back into stock, and lifting a customer's unreliable mark. Both reverse
something the system decided automatically, so both need a person accountable for them. The
manager is also who hears about it when an inspection runs past its deadline.

**Admin** — owns **global configuration**: the condition-grade multipliers that price the
entire catalog. A separate role because a single edit here re-prices every copy the shop owns.

The division is between kinds of decision, not seniority. Staff run the shop; the manager
reverses automatic decisions; the admin changes the rules everything else operates under.

## Core concepts

**Catalog set** — the product as LEGO defines it: set number, name, year, theme, piece count,
image, age rating, retail price. One catalog entry, however many boxes the business owns. The
specs are recorded **once**, when the set is first catalogued, and every copy inherits them.

The lookup covers the **product facts** Rebrickable holds — name, year, theme, piece count,
image, and the set's **minifigures**. The minifigure list becomes the starting point for the
set's **unique-piece checklist**: the items staff verify individually when a set comes back,
because they are valuable out of all proportion to their weight. Staff extend it with sticker
sheets, printed parts and anything else worth checking by hand.

Everything else is **typed by staff** on the prefilled form, and all of it is per catalog set:

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

Each copy also has a **baseline weight**, recorded when it is registered and still known to be
complete. Every return is weighed against it, which is the fastest way to notice a whole bag
of pieces missing. It is a property of the **copy**, not the catalog set — a replacement
manual or a repacked box shifts the number, and a shared reference would make those copies
read as short on every single return. The comparison allows a **tolerance**, since packaging
and humidity move grams without anything being lost.

**Copy status** — where a copy is in its cycle. Only **Available** can be reserved.

| Status | Meaning |
| --- | --- |
| **Available** | On the shelf, rentable now |
| **Reserved** | Claimed by a customer who has not yet collected it |
| **On rent** | Collected, out with a customer |
| **Awaiting inspection** | Back and settled at the counter, queued for deep inspection — **not rentable** |
| **In inspection** | Being checked and graded properly |
| **In repair** | Being repaired; neither rentable nor written off |
| **Lost** | Written off, never returned |
| **Retired** | Too worn to rent; still held by the shop |

The gap between *Awaiting inspection* and *Available* is deliberate and matters: a box that
has just been handed back is not yet ready for the next customer. The money is already
settled by then — that happens at the counter — but the copy still has to be properly checked,
regraded and shelved. Typical path:

> On rent → Awaiting inspection → In inspection → **Available**
>
> …or → **In repair** → Available, if something is worth fixing
>
> …or → **Retired**, if it is past saving

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

A grade is set at **deep inspection**, never at the counter — the counter check is deliberately
too quick to judge condition properly. It may also go **back up** after a successful repair,
which is the one case where the downward-only rule is overridden on purpose.

**Stickers and the grade.** A *New* copy is sealed, so its stickers are unapplied: the first
customer to rent it is the one who applies them, and everyone after inherits their work. That
is much of what separates *New* from *Excellent*, and it is the honest justification for the
New premium — the customer is paying for the unspoiled build, not merely a less-worn box.

Two rules follow, and the line between them is one staff can apply while a customer waits:

- **Torn stickers are damage.** Deducted from the deposit, and the grade drops.
- **Stickers that are not perfectly straight are not.** Applying them was the customer's to do
  and doing it imperfectly costs them nothing.

Grade is also **published**. Customers browse copy by copy and weigh condition against price,
so a grade is a public claim about a specific box rather than an internal note — which is why
the scale above is written in language a customer can act on. Regrading a copy changes what
every prospective renter sees about it. Sticker state is not shown separately; the grade
already carries it.

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
placed later, at collection, on **a card the customer presents at the counter** — not on the
card they used online to reserve.

That means collection touches **two separate authorizations**, and they must not be conflated:
the rental fee can only be captured against the authorization taken online at reservation,
while the deposit hold is a fresh authorization on the counter card. They may be the same
piece of plastic; they are not the same authorization.

*Known limitation, accepted for now:* retired sets can trade well above their original retail
price, so a retail-based deposit under-covers collectibles.

**When money moves** — three distinct moments, and only one of them is a charge at the time it
happens:

| Moment | What happens |
| --- | --- |
| **Reservation** | The minimum-duration rental fee is **locked** (authorized) on the customer's card, not charged |
| **Collection** | That lock is **captured**, and the **deposit hold** is placed on a card presented at the counter |
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

**The customer is told.** A blocked customer sees that they cannot rent and which **category**
applies — set not returned, repeated no-shows, returned damaged, other — and is directed to the
shop, since staff lifting the mark is the only route back. The staff **free-text note stays
internal**: it exists so a decision can be defended at the counter, not to be published at the
person it describes. Being refused with no explanation would only send them to the shop
anyway, with nobody knowing why.

**Subscription** — a standing request to be told when **one specific copy** becomes rentable.
A customer who wants *that* box — the New-grade one, or the cheapest — subscribes to it and
waits. Subscriptions are always to a copy; there is no way to subscribe to a set as a whole.

**Nothing is held.** The notification fires when the copy reaches **Available**, after
inspection — announcing it the moment it is handed back over the counter would be telling
people it is ready when it is not, and its grade and price may still change during inspection.
Everyone subscribed to that copy is notified at once and the first to reserve wins; the rest
stay subscribed. A deliberate trade for simplicity over a fair queue with a held copy.

A subscriber follows the copy's whole life, not only the moment it frees up. They hear when it
goes **into repair**, when it comes back **repaired and available**, and when it is **gone for
good**:

| What happens to the copy | The subscriber is told |
| --- | --- |
| Reaches *Available* | It is free — reserve it before someone else does |
| Enters *In repair* | It is being repaired; still coming, just later |
| *Available* again after repair | It has been **repaired** and is free |
| *Retired* or *Lost* | It is **gone**, and the subscription ends |

**Terminal states end the subscription.** A retired copy is too worn to rent again and a lost
one is never coming back — there is nothing left to wait for, so the subscription is removed
rather than left hanging on a notification that can never arrive. Both are treated identically
because to a waiting customer they are the same thing: that box is not coming.

Because subscriptions are per copy, a customer whose copy dies is left with nothing to wait
on. So that message **points back at the set**, where they can reserve an available copy or
subscribe to a different one — otherwise the copy-only model leaves them at a dead end.

Subscriptions cost nothing and hold nothing, so there is **no limit** on them — unlike
reservations and rentals, which are capped. A customer drops off when they rent the copy, when
the copy dies, or by choice.

A customer who does not mind which box they get simply subscribes to several copies of the
same set.

**Inspection** — what staff do when a set comes back. It happens in **two stages**, because
the two jobs it has to do want opposite things from time.

| | **Counter check** | **Deep inspection** |
| --- | --- | --- |
| When | Immediately, customer present | Afterwards, customer gone |
| For | Settling the money | Judging the copy |
| Output | A deduction the customer signs | Grade, price, damage log |
| Pace | Minutes | Unhurried, up to 2 business days |

Money has to be **fast, final and agreed** — the customer is standing there and there is only
one capture available. Grading has to be **accurate**, which takes longer than anyone will
wait. Splitting them lets each be done properly.

The counter check is four quick tests, each catching what the others miss:

| Test | Catches | Blind to |
| --- | --- | --- |
| **Weigh** against the copy's baseline | A whole bag gone | One small piece |
| **Check the minifigures** against the set's list | High-value loss that weighs almost nothing | Bulk loss of common bricks |
| **Glance over the pieces** | Obvious breakage | Anything inside a sealed bag |
| **Glance over the stickers** | Tearing | Subtleties |

The minifigure check is the one that earns its place: a rare minifigure can be worth more than
the rest of the set and would never register on a scale.

**Deep inspection produces the customer-facing output** — the grade, the completeness and the
photographs that appear on the set's page. Photographs are therefore being taken for
prospective customers, not only as evidence for a colleague.

One distinction matters. A photograph showing **what the box looks like now** is product
information and is published. A photograph taken to **justify a deduction against a named
customer** is evidence about that person's rental and stays **internal** — publishing it would
leave one customer's dispute on a public page indefinitely.

**What "complete" means.** Under this procedure it means *passed the counter checks and the
deep inspection*, not *every piece individually counted*. Nobody counts nine thousand Titanic
pieces. The set's page should not imply a precision the procedure does not deliver.

**Opening days, and how days are counted** — BrickShare keeps a calendar of the days the shop
is **actually open**, closures included. It is used for one thing: deciding which pickup dates
a customer may choose. Offering a date the shop is shut would let someone reserve, be unable
to collect, and lose their fee to a no-show for a date the system itself suggested.

That calendar is **not** the same as a *business day*, and the difference is deliberate:

| Clock | Counts | Used for |
| --- | --- | --- |
| **Calendar days** | Every day, weekends and holidays included | The rental clock — day 25 and day 28 |
| **Business days** | Monday to Friday; public holidays **not** excluded | The inspection deadline |
| **Opening days** | Days the shop is genuinely open, closures included | Which pickup dates may be chosen |

The rental clock must be calendar days because it tracks a card authorization that does not
pause at weekends — count it any other way and day 28 drifts past the 30-day hold, destroying
the buffer that guarantees the deposit can still be captured. The inspection deadline is staff
work, so it is measured in working days. And pickup dates need real opening days, because a
customer cannot collect from a shut shop whatever the calendar says.

Three notions rather than one, each doing a job the others cannot. Collapsing them would
reintroduce a bug in whichever rule lost out.

## Product decisions

Decisions already made and treated as settled:

| Area | Decision |
| --- | --- |
| Business model | The organisation owns all inventory; B2C rental. There is no owner role. |
| Roles | **Customer**, **staff**, **manager** and **admin**. Manager takes the exceptional decisions — accepting a written-off set back, lifting an unreliable mark — and receives inspection escalations. Admin owns global configuration. |
| Inspection | **Two stages.** A fast **counter check** with the customer present decides the money; a **deep inspection** afterwards decides the grade. Split because the money must be fast and agreed, and grading must be accurate. |
| Counter check | Weigh against the copy's baseline, check the set's unique pieces, glance over pieces and stickers. Deduct for damage found. |
| Signed verdict | The customer **signs digitally** on the staff device before leaving. Stored against the rental with the findings, the amount and a timestamp. |
| Later findings | Anything found at deep inspection is **logged but never charged** — the counter capture already released the hold, so nothing remains to take. The shop absorbs what the counter check missed. |
| Grading | Set at **deep inspection**, never at the counter. May **rise only after a repair**. |
| Repair | A copy may be repaired rather than retired. New **In repair** status; on completion the grade may be raised and the copy returns to *Available*. |
| Inspection deadline | **2 business days** from return. Beyond that the **manager** is notified. |
| Business days | **Monday to Friday.** Public holidays are *not* excluded — a bank holiday eats into the SLA. Chosen for simplicity. |
| Three clocks | **Calendar days** for the rental (day 25, day 28), **business days** for the inspection deadline, **opening days** for choosing a pickup date. Deliberately distinct; collapsing them breaks whichever rule loses out. |
| Pickup dates | Only days the shop is **actually open** may be chosen, closures included. Otherwise a customer could be no-showed for a date the system offered them. |
| Job timing | **No-show and write-off run after the shop closes**, giving the customer the whole trading day. The write-off **also runs on days the shop never opened** — deferring it would push past the 30-day hold and break the 28/30 buffer. |
| Failed capture | A day-28 write-off whose capture fails still marks the copy *Lost*, marks the customer and removes subscriptions. The **manager is alerted**. Stock and standing stay truthful even when payment does not. |
| Baseline weight | Recorded **per copy** at registration while known-complete, compared on every return with a tolerance. |
| Unique-piece checklist | **Per catalog set.** Minifigures prefilled from Rebrickable; staff add sticker sheets and printed parts. |
| Stickers | A *New* copy's stickers are unapplied — the first renter applies them. **Torn is damage** (deducted, grade drops); **not perfectly straight is not**. |
| Ruined set | A copy retired because of customer damage costs a **staff-assessed amount, up to the deposit**, decided at the counter. |
| Locations | A **single shop**. No per-location stock, no transfers, no collection-point choice. |
| Fulfilment | In-store pickup. No shipping, no carrier integration. |
| Inventory granularity | Individually-tracked physical copies. Copies are **not** fungible — each has its own condition and history. |
| Copy identity | A BrickShare-minted barcode/QR label per box, scanned at handover, return and inspection. |
| Copy status | Available / Reserved / On rent / Awaiting inspection / In inspection / Lost / Retired. Only **Available** is reservable. |
| Condition grades | A fixed four-tier scale: New / Excellent / Good / Fair. |
| Catalog specs | Product facts (name, year, theme, piece count, image, **minifigures**) fetched from **Rebrickable** by set number to prefill the form. The minifigure list seeds the set's unique-piece checklist. |
| Missing spec data | Cataloguing a set **blocks** until the Rebrickable lookup succeeds. |
| Manual entry | Staff type four per-set fields: **retail price**, **base rental price**, **minimum rental duration**, **age rating**. |
| Per-set vs global | Everything set-specific is per catalog set. The only global configuration is the **grade multipliers** and the **28-day maximum rental duration**. |
| Rental duration | The customer picks **no duration**. They keep the set up to the maximum. There is **no due date** and no overdue state. |
| Duration limits | The minimum is **per set** (a billing floor). The maximum is a global **28 days**, identical for every set; staff cannot change it. A set's minimum may not exceed 28 days. |
| The 28/30 buffer | The deposit hold runs **30 days** but rentals cap at **28**, leaving two days in which write-off is guaranteed to find a live authorization. An expired hold cannot be captured, so this margin is what stops the money escaping. **Do not "tidy" the maximum up to 30.** |
| Price calculation | Base rental price × the grade multiplier gives one minimum-duration rental. Daily rate = that ÷ the minimum duration. |
| Deposit amount | **Calculated, not entered**: retail price × the copy's grade multiplier. A property of the copy. Fixed at reservation. |
| Booking horizon | A customer reserves an **available** copy for a pickup date **at most 7 days ahead**. A copy that is out cannot be booked. |
| Reserved stock | A reserved copy is unavailable to everyone else until pickup. Accepted as a cost of doing business. |
| Commitment cap | A customer may hold at most **3 commitments at once** — reserved, out on rent, or a mix. Reserving a fourth is refused **up front**, so nobody can pay a lock on a copy they would then be barred from collecting. |
| Money at reservation | The minimum-duration fee is **locked, not charged**. |
| Cancellation | Free if cancelled **24h or more** before the pickup date — the lock is released. Inside 24h it is **captured**, as for a no-show. |
| No-show | The lock is **captured** in full. The copy returns to *Available* without inspection. |
| Money at collection | The lock is captured **on the card used online at reservation**, and the **deposit hold** is placed on **a card presented at the counter**. Two separate authorizations, not one payment method. |
| No card at collection | The set is **not handed over**. It stays reserved until the end of the pickup date; the customer may return that day with a card. Uncollected by day's end, it is a **no-show**. |
| Running cost | The customer sees **live** what they owe: days held, cost so far, and days remaining until day 28. |
| Blocked customers | A blocked customer is **told**, and shown the mark's **category**. The staff free-text note stays internal. |
| Money at return | The **final fee** = daily rate × days beyond the minimum. Zero if returned within the minimum; nothing refunded for early return. |
| Settling the final fee | Taken **out of the deposit hold**, never charged separately. A partial capture releases the balance in the same operation, so return is one money movement. **No step in the lifecycle is a fresh charge that could decline.** |
| Deposit purpose | Covers **loss, damage and unpaid rental fees**. The third must be stated in the customer's terms at reservation, or the shop cannot lawfully settle fees from it. |
| Return warning | On **day 25** — three days before the maximum — the customer is emailed a warning. |
| Non-return | On **day 28**: deposit captured, the copy recorded as **lost**, the customer marked unreliable, and **staff notified**. One event. |
| Deposit sizing | The deposit covers the **set's value only**. It is not sized to also cover rental fees, so the days between the minimum and day 28 are never recovered on a write-off — the shop keeps the minimum fee plus the set's value and absorbs the rest. |
| Recovery | A written-off set may be brought back and accepted at **staff discretion**. Manual only. The deposit may be refunded in whole or in part. |
| Customer standing | A customer may be marked **unreliable**, blocking new rentals only. Applied automatically at write-off or manually by staff; removable by staff. Both audited. |
| Mark reason | A fixed category plus a free-text note, linked to the rental that caused it. |
| Copy-level reservation | **The customer chooses the copy**, not the system. Condition and price differ between boxes, so a reservation is never silently substituted. |
| Contention | Two customers may want the same copy. **First to reserve wins**; the other is told and shown the remaining copies. |
| Search results | Each set shows its **available copy count** and a **starting price** — the cheapest copy reservable right now. |
| Set detail | Lists **every copy**, available or not, with grade, completeness, photographs, price and deposit. |
| Published condition | Grade, completeness and **current-condition** photographs are customer-facing. **Damage-evidence** photographs tied to a specific rental stay internal. |
| Subscriptions | Always to a **specific copy**, never to a set. Nothing is held, first to reserve wins. **Unlimited** — they tie up neither stock nor money. Someone with no preference subscribes to several copies. |
| Subscription lifecycle | Subscribers are told when the copy is **free**, **being repaired**, **repaired and free**, or **gone**. |
| Terminal states | *Retired* and *Lost* both **end the subscription** — nothing is left to wait for. The message links back to the set, since a copy-only subscription otherwise leaves the customer at a dead end. |
| Payments | Stripe, as a real integration. |
| Deposit hold — **unverified** | The 30-day deposit hold assumes an **extended** card authorization. This is **not confirmed**: extended windows are gated on merchant category (travel, lodging, vehicle rental — which a LEGO shop likely is not), the default authorization window is around 7 days, issuers often release holds early regardless of network rules, and requiring cards excludes wallets. **Must be verified with Stripe before it is relied on.** Fallback: capture the deposit up front and refund it on return. |
| Auth | Staff sign in with organisation accounts. Customers sign in with email or a social account. |

## Use cases

Eleven use cases covering the customer, staff and the shop's own scheduled behaviour. See
*Coverage* at the end for how they divide up.

### UC-1 — Staff manage the catalog and physical stock

> *As staff, I register the LEGO sets the shop owns, without re-entering product specs for
> every physical box.*

The shop might own three Titanics. That is **one** catalog entry and **three** copies: the
specs are described once, and the copies carry only what actually differs between boxes.

**UC-1.1 — Catalogue a new set.** Staff type a LEGO set number. BrickShare looks it up on
Rebrickable and **prefills the form** with name, year, theme, piece count, image and the set's
**minifigures**. Staff then type the four fields Rebrickable cannot supply: **retail price**,
**base rental price**, **minimum rental duration** and **age rating**. The minimum is rejected
if it exceeds 28 days. There is no maximum to enter — it is a fixed 28 days for every set. The
deposit is *not* entered either; it is derived from the retail price.

Staff also review the **unique-piece checklist**, seeded with the minifigures from the lookup,
and add sticker sheets, printed parts or anything else worth verifying by hand when a copy
comes back.

**UC-1.2 — Register physical copies.** Staff pick an existing catalog entry and register one
or more copies against it, either individually or in a batch ("we bought 3"). Each copy is
assigned an identity and a barcode/QR label to print and stick on the box, and is given a
starting condition grade — normally *New*.

**UC-1.3 — Retire a copy.** A copy too worn or too incomplete to rent is retired. It stops
being rentable but remains in the system. Anyone subscribed to it is told it is gone and
their subscription is removed.

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
Anyone subscribed to that copy is told it is gone and their subscription is removed.

**UC-2.3 — Mark a customer manually.** Staff mark a customer unreliable for reasons the
write-off rule doesn't catch — repeated no-shows, sets returned wrecked. Same requirement: a
category, a note, and an identified member of staff.

**UC-2.4 — A marked customer tries to rent.** The reservation is refused. Browsing,
availability, sign-in and rental history all continue to work.

**UC-2.5 — Lift a mark.** The customer sorts it out with the shop; a **manager** removes the
mark — it reverses a decision the system made automatically, so it needs someone accountable.
The
removal is recorded with who did it, when, and why.

#### Business rules

- Write-off fires on **day 28**, and nowhere else. There is no due date and no overdue period
  preceding it. Days are **calendar days** from collection.
- It runs **after the shop closes**, so the customer has the whole trading day to bring the
  set back — and **still runs on days the shop never opened**. Waiting for the next open day
  could push a Saturday write-off to Monday, which is day 30: exactly when the authorization
  dies. The 28/30 buffer only works if the job actually runs on day 28.
- **If the capture fails**, the rest still happens. The copy is *Lost*, the customer is marked,
  subscriptions are removed — the set is gone whether or not the money arrived — and the
  **manager is alerted** that the deposit could not be taken. Stock and standing must stay
  truthful even when payment does not.
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

### UC-3 — Reserve a set, or wait for one

> *As a customer, I want to take out a set that is free right now, and be told when a set I
> want comes back.*

**UC-3.1 — Reserve a chosen copy.** From a set's page the customer picks **a specific
available copy** and a **pickup date up to 7 days ahead**, chosen from the days the shop is
actually open. That copy is claimed for them and the minimum-duration fee is **locked** — no
charge yet. Rental price and deposit are fixed at this moment, from the grade of the copy they
chose.

**UC-3.2 — Subscribe to a copy.** The customer wants *that* box — the New-grade one, or the
cheapest — and it is not available. They subscribe to the copy itself. Someone who does not
mind which box they get subscribes to several copies of the set.

**UC-3.3 — Be notified a copy is free.** A copy passes inspection and reaches *Available*.
Everyone subscribed to that copy is notified. Nothing is held — whoever reserves first gets
it, and the rest stay subscribed.

**UC-3.4 — Be told the copy is delayed.** The copy goes for repair instead of straight back on
the shelf. Subscribers are told it is being repaired — still coming, just later.

**UC-3.5 — Be told the copy is repaired.** It returns to *Available* after repair. Subscribers
are told it has been **repaired** and is free, rather than simply that it is free.

**UC-3.6 — Be told the copy is gone.** The copy is retired or written off as lost. Subscribers
are told it is gone, their **subscription is removed**, and the message points them back at the
set so they can reserve another copy or subscribe to a different one.

#### Business rules

- **The customer chooses the copy.** BrickShare never substitutes a different one, because the
  choice was theirs — condition and price differ between boxes.
- **Only *Available* copies can be reserved.** A copy that is out, reserved, in inspection,
  lost or retired can only be subscribed to.
- Two customers may want the same copy at once. **First to reserve wins**; the other is told
  that copy has gone and shown what is left. Nothing is silently swapped.
- A reserved copy is **unavailable to everyone else** until pickup.
- A customer may hold at most **3 commitments** — reservations and rentals combined. The
  fourth is refused when reserving, never at collection.
- **Subscriptions are always to a copy**, never to a set, and are **unlimited** — they hold no
  stock and no money, so they count toward nothing.
- Price and deposit are **fixed when the reservation is made**, from the copy's grade at that
  moment.
- The pickup date may be **at most 7 days ahead** — the fee lock has to still be alive when
  the customer arrives.
- **Only days the shop is open may be chosen.** Offering a closed day would let a customer
  reserve, be unable to collect, and lose their fee to a no-show for a date the system itself
  suggested.
- Notification fires on entry to *Available*, **never on physical return** — a copy handed
  back is not yet rentable, and inspection may still change its grade and price.
- Subscribers follow the copy's whole life: **free**, **being repaired**, **repaired and
  free**, or **gone**.
- **Terminal states end the subscription.** *Retired* and *Lost* are treated identically —
  from a waiting customer's point of view that box is not coming, so the subscription is
  removed rather than left waiting on a notification that can never arrive.
- The "gone" message **links back to the set**. Because subscriptions are per copy, a customer
  whose copy dies would otherwise be left with nothing to wait on.
- A customer marked unreliable may subscribe, but reserving is still refused.

### UC-4 — Collect, cancel, or fail to collect

> *As a customer I want to change my mind without penalty if I do it in good time; as the
> shop I don't want copies held off the shelf for nothing.*

**UC-4.1 — Cancel in good time.** The customer cancels in the app, **24 hours or more** before
the pickup date. The lock is released and nothing is charged. The copy returns to *Available*
and anyone subscribed to that copy is notified.

**UC-4.2 — Cancel late.** The customer cancels **less than 24 hours** before the pickup date.
The lock is **captured** — the same outcome as not turning up at all.

**UC-4.3 — Collect the set.** The customer arrives on the pickup date. Staff scan the copy's
label, the customer presents a card, and the set is handed over. Two payment actions happen
here: the reservation lock is **captured on the card used online**, and the **deposit hold** is
placed on the **card presented at the counter**. The copy moves to *On rent* and the 28-day
clock starts.

**UC-4.4 — Arrive without a usable card.** No card, no deposit hold, so **no handover**. The
set stays reserved and the customer may come back with a card before the pickup date ends.
Nothing is charged and nothing is decided at this point — if the day ends with the set still
uncollected, UC-4.5 simply applies.

**UC-4.5 — Fail to collect.** The pickup date passes without collection. The lock is
**captured**, the reservation closes, and the copy returns to *Available* — skipping inspection
entirely, because it never left the shelf. Subscribers to that copy are notified.

**UC-4.6 — Return the set.** The customer brings the set back and staff check it there and
then, deducting for any damage found, before settling the money in a single capture. Covered
in full by **UC-9**; the copy then queues for deep inspection under **UC-10**.

#### Business rules

- The rental fee is **locked at reservation and captured at collection**, never charged twice.
- Collection involves **two authorizations**: the fee is captured against the one taken online
  at reservation, and the deposit hold is a fresh one on the counter card. A single stored
  payment method does not cover both.
- **No card means no handover**, but no penalty either — the reservation simply stands until
  the end of the pickup date like any other.
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

**UC-5.2 — A manager decides.** A **manager** may accept the set back or refuse — it reverses
an automatic write-off, so it is theirs to make. If accepted, the copy
enters *Awaiting inspection* and rejoins stock through the normal inspection path.

**UC-5.3 — Settle the deposit.** Staff may refund the deposit **in full, in part, or not at
all**, informed by what the inspection finds.

**UC-5.4 — Lift the mark, or don't.** A **manager** may remove the unreliable mark. It is a
separate decision from accepting the set back.

#### Business rules

- Recovery is **entirely manual and discretionary**. The customer is not entitled to it and
  cannot trigger it from the app.
- The copy re-enters stock as **Awaiting inspection**, never straight to *Available* — it has
  been outside the shop's control and must be checked.
- Accepting the set, refunding the deposit and lifting the mark are **three separate
  decisions**, and any may be taken without the others. Accepting the set and lifting the mark
  are a **manager's**; refunding the deposit, once the set is accepted, is routine **staff**
  work informed by the inspection.
- All three are **audited**: who, when, why.

### UC-6 — Account and access

> *As a customer, I want to sign up quickly, and if I am not allowed to rent I want to know
> that before I try.*

**UC-6.1 — Register.** The customer creates an account with an **email address or a social
account**. No account is needed to browse.

**UC-6.2 — Sign in.**

**UC-6.3 — Provide a card.** Reserving locks money, so a card is required before the first
reservation can be made. This is the **online** card, used for the reservation lock only — the
deposit is taken on a card presented at the counter.

**UC-6.4 — See own standing.** A customer who has been marked unreliable sees that they cannot
currently rent, and which **category** applies. They are directed to the shop, since only staff
can lift it.

#### Business rules

- Browsing and searching need **no account**. Reserving does.
- A card is required to reserve, not to register.
- A blocked customer sees the mark's **category only**. The staff note stays internal.
- Blocking affects renting alone — sign-in, browsing and history all keep working.

### UC-7 — Find a set

> *As a customer, I want to find a set I fancy building and know whether I can have it now.*

**UC-7.1 — Browse the catalog.** The customer looks through the sets BrickShare stocks.

**UC-7.2 — Search and filter.** By name or set number, and by the attributes that matter when
choosing something to build: theme, piece count, age rating, price, and whether it is available
right now.

Each result shows the set, **how many copies are available**, and a **starting price** — the
cheapest copy anyone could reserve right now.

**UC-7.3 — View a set.** Product facts (name, year, theme, piece count, image, age rating),
the minimum rental duration, and **every copy the shop owns** — not only the free ones.

**UC-7.4 — Compare the copies.** For each copy: its **condition grade**, how **complete** it
is, **photographs** of its actual state, its **rental price** and its **deposit**. Prices and
deposits differ between copies because grades do, so this is the screen where the customer
decides whether a worn copy at a lower price is the better deal.

**UC-7.5 — Act on a copy.** Available copies can be **reserved**. Any copy, available or not,
can be **subscribed to**. Someone with no preference between boxes subscribes to several
copies of the set.

#### Business rules

- A set's **available count** is its copies in *Available* status. Reserved copies do not
  count — they are already spoken for.
- The **starting price** is the cheapest **available** copy. It never advertises a price
  nobody can act on, so copies that are out or in inspection are excluded even if cheaper.
- Every copy is listed, **including unavailable ones**. Customers need to see a copy in order
  to subscribe to it, and to understand what the set will look like when one frees up.
- Published per copy: grade, completeness and **current-condition photographs** from the most
  recent inspection. Photographs taken as **damage evidence** against a particular rental stay
  internal.
- A copy's price is **fixed only at reservation**. Listed prices are current but a regrade can
  change them until the moment a copy is claimed.
- A set with no available copies still shows in full — otherwise its copies could not be
  subscribed to.

### UC-8 — Follow a rental

> *As a customer, I want to know what this is costing me and how long I have left, without
> having to work it out myself.*

**UC-8.1 — See the active rental.** For each set out: when it was collected, how many days it
has been held, **what is owed so far**, and how many days remain until day 28.

**UC-8.2 — Be warned before the deadline.** On day 25 the customer is emailed that three days
remain (see UC-2.1).

**UC-8.3 — View rental history.** Past rentals with what each one finally cost, and any
deposit retained.

#### Business rules

- **What is owed is computable at any moment**, not only at return. Until the minimum duration
  is passed it is zero, then it grows by the daily rate.
- The running total is **what the return would cost today**, excluding any damage — damage can
  only be judged at inspection.
- A customer may hold at most **3 commitments at once** — reserved, out, or a mix. Reserving a
  fourth is refused at the point of reserving, never at collection.
- History survives everything: retired copies, lost copies and lifted marks all keep their
  place in it.

### UC-9 — Return at the counter

> *As staff, I want to check a returned set quickly enough that the customer can wait, settle
> the money once, and have them agree to it before they leave.*

**UC-9.1 — Receive the set.** Staff scan the copy's label and pull up the rental.

**UC-9.2 — Weigh it.** The set is weighed against the copy's baseline. A reading below
baseline, beyond tolerance, says something substantial is missing.

**UC-9.3 — Check the unique pieces.** Staff work the set's checklist — its minifigures, and
whatever else was added when it was catalogued.

**UC-9.4 — Glance over pieces and stickers.** A brief look for broken, chewed or water-marked
pieces, and for torn stickers.

**UC-9.5 — Record the verdict and deduct.** Staff record what they found and how much, if
anything, is retained from the deposit.

**UC-9.6 — The customer signs.** The verdict is signed digitally on the staff device and
stored against the rental with the findings, the amount and a timestamp.

**UC-9.7 — Settle.** One capture takes the final fee plus any retention from the deposit
hold; the balance is released. Returned within the minimum with nothing retained, and the
whole hold is released untouched.

**UC-9.8 — The customer leaves.** The copy moves to *Awaiting inspection*.

#### Business rules

- The counter check is the **only** moment money is decided. There is one capture per rental,
  and a partial capture releases the rest of the hold.
- The customer **signs before leaving**. That signature is the shop's evidence that the
  deduction was agreed, and it is why the check happens with them present.
- **Torn stickers are damage; crooked ones are not.**
- The counter check **does not set the grade** — it is deliberately too quick to judge
  condition. Grading is UC-10.
- Because settlement is final here, **the counter check has to be good enough to catch
  anything expensive.** The four tests exist for that reason.

### UC-10 — Deep inspection, repair and shelving

> *As staff, I want to judge a returned copy properly once the customer has gone, and get it
> back on the shelf at an honest grade.*

**UC-10.1 — Inspect properly.** The copy moves to *In inspection* and is checked without
time pressure.

**UC-10.2 — Regrade and reprice.** The new condition grade is set, which determines what the
next customer pays.

**UC-10.3 — Log the damage.** Anything found is recorded against the copy. **The customer is
not charged for it.**

**UC-10.4 — Shelve, repair or retire.** The copy becomes *Available*, moves to *In repair*, or
is *Retired* if it is past saving. Subscribers are told which: that it is free, that it is
being repaired, or that it is gone — and a retirement **removes their subscription**.

**UC-10.5 — Repair.** Staff repair the copy: replacing a missing piece, sourcing a lost
minifigure. On completion the grade **may be raised**, and the copy becomes *Available*.
Subscribers are told it has been **repaired** and is free — a different message from an
ordinary return to the shelf, because the copy may now be in better condition than when they
subscribed.

**UC-10.6 — Escalate a slow inspection.** Inspection should finish within **2 business days**
of the return. Beyond that, the **manager** is notified.

#### Business rules

- **Nothing found here costs the customer anything.** This is not only policy: the counter
  capture already released the balance of the hold, so there is nothing left to take. The shop
  absorbs whatever the counter check missed.
- The **grade is set here**, never at the counter.
- A grade may **rise only after a repair**, and only by deliberate staff action. Otherwise
  grades move downward.
- A copy is not rentable until it reaches *Available* — *Awaiting inspection*, *In inspection*
  and *In repair* are all unavailable.
- **Photographs published** to customers show the copy's current condition. Photographs taken
  as evidence for a counter deduction stay internal.
- **Business days are Monday to Friday.** Public holidays are not excluded — a bank holiday
  eats into the two days even though the shop was shut. Chosen for simplicity over precision.
- A copy retired because of a customer's damage costs them a **staff-assessed amount, up to
  the deposit**, decided at the counter under UC-9.

### UC-11 — What the system does on its own

> *As the shop, I want the deadlines to enforce themselves, and I want to know when that
> fails.*

Four behaviours run on a clock. Everything else the system does is **event-triggered** —
subscriber notifications when a copy changes status, the lock released on cancellation, the
confirmations after each step — and belongs with the flow that causes it.

| # | Fires | Does | If it fails |
| --- | --- | --- | --- |
| **UC-11.1** Return warning | Day 25 of a rental | Emails the customer: three days left | Retried; nothing irreversible depends on it |
| **UC-11.2** Write-off | Day 28, after closing | Captures the deposit, marks the copy *Lost*, marks the customer, removes subscriptions, notifies staff | **Everything but the capture still happens**, and the manager is alerted |
| **UC-11.3** No-show | End of the pickup date, after closing | Captures the fee lock, closes the reservation, returns the copy to *Available*, notifies subscribers | Retried; the copy stays reserved until it succeeds |
| **UC-11.4** Inspection escalation | 2 business days after a return | Notifies the manager | Retried |

#### Business rules

- **No-show and write-off run after the shop closes.** Both take money or end something, so
  the customer gets the whole trading day first — to walk in, or to bring the set back.
- **The write-off runs on closed days too.** If day 28 is a Saturday and the job waited for the
  next open day it would fire on Monday — day 30, when the authorization expires. The whole
  point of capping rentals at 28 against a 30-day hold is that the capture finds something
  live, and that only holds if the job runs on the day.
- The **day-25 warning** and the **inspection escalation** are not tied to closing time.
  Nothing changes if they run overnight.
- **Day 25 and day 28 are calendar days** from collection. The **inspection deadline is
  business days**. Two clocks, deliberately.
- **A failed capture never blocks the rest of a write-off.** The set is gone whether or not the
  money arrived; recording otherwise would leave a lost copy sitting *On rent* forever.
- Nothing releases a deposit on a timer. **Deposit settlement happens at the counter** in UC-9,
  with the customer present.

### Notifications

**To the customer:**

| Notification | When |
| --- | --- |
| Reservation confirmed | A copy is claimed and the fee locked |
| Cancellation confirmed | Cancelled 24h or more before pickup; lock released |
| Late cancellation charged | Cancelled inside 24h; lock captured |
| No-show charged | The pickup date ended uncollected |
| Collection receipt | Set handed over, fee captured, deposit held |
| Return deadline warning | **Day 25** — three days left |
| Set written off | **Day 28** — deposit captured, copy lost |
| Return settled | Signed verdict, final fee and any deduction taken from the hold, balance released |
| Subscribed copy is free | That copy reaches *Available* |
| Subscribed copy is being repaired | That copy enters *In repair* |
| Subscribed copy is repaired and free | That copy reaches *Available* from *In repair* |
| Subscribed copy is gone | That copy is *Retired* or *Lost*. The subscription is removed and the message links back to the set |

**To staff:**

| Notification | When | To |
| --- | --- | --- |
| Set written off | **Day 28** — a copy is lost | Staff |
| Inspection overdue | A return is still uninspected after **2 business days** | Manager |
| Deposit capture failed | A day-28 write-off could not take the deposit — the hold was already gone | Manager |

A lost set, a stalled inspection and a deposit that could not be taken are all somebody's
problem, not just a status change — which is why each has a person attached.

### Coverage

The use cases are complete. Staff have the catalog and stock (UC-1), the counter (UC-9) and
inspection, repair and shelving (UC-10). Customers have accounts (UC-6), finding a set (UC-7),
reserving and subscribing (UC-3), collecting, cancelling and returning (UC-4), and following a
rental (UC-8). The shop has non-return and customer standing (UC-2), recovery (UC-5), and the
scheduled behaviours (UC-11).

## Open product questions

- Should a hold **released early by the issuer** be surfaced to staff? The 28/30 buffer only
  protects against the hold reaching its scheduled end — an issuer can drop one on day three,
  and no amount of margin helps. The shop would then have neither damage cover nor any way to
  settle the final fee, and would not find out until the set came back. Part of the wider
  unverified extended-authorization risk.
