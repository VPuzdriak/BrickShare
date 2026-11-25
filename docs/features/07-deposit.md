# Deposit

The shop collects a deposit with every rental. After inspection, the deposit is settled against overdue, damage, and loss fees. Fees above the deposit are charged to the saved card.

| Item | Value |
|------|-------|
| Actors | Customer, system |
| Services | Payment, Reservation, Stripe |
| Architecture | [Payment Service](../Architecture.md#34-payment-service) |

## Deposit collection

Default mode is **charge and refund**. It works for every rental length.

- The Checkout Session has two line items: rental (`RentalAmount`) and deposit (`DepositAmount`). Both are paid in one PaymentIntent.
- On `checkout.session.completed`, the deposit is `Charged`. The service publishes `PaymentSucceeded` and `DepositSecured`.
- A set can be picked up only when the deposit status is `Charged` or `Held`.
- The deposit is returned as a **partial refund** of the same PaymentIntent. Stripe sends refunds to the original card automatically.
- Cost: Stripe keeps its processing fee on the refunded deposit amount. The customer sees the refund after 5–10 business days. The customer gets an e-mail when the refund is sent.
- A no-show (not picked up before the pick-up deadline) is cancelled: the rental is not refunded, the deposit is refunded (or the hold canceled).

Deposit statuses: `Pending`, `Charged`, `Held`, `Released`, `Refunded`, `PartiallyKept`, `Kept`.

## Settlement

Payment consumes `InspectionCompleted` (normal return) or `ReservationLost` and calculates `fees = overdue + damage (+ replacement value if lost)`.

| Case | Charge and refund mode | Hold mode |
|------|------------------------|-----------|
| `fees = 0` | Refund the full deposit | Cancel the hold |
| `0 < fees <= deposit` | Refund `deposit - fees` | Capture `fees` (`amount_to_capture`). Stripe releases the rest |
| `fees > deposit` | Keep the deposit, charge `fees - deposit` off-session | Capture the full hold, charge `fees - deposit` off-session |

- When all money is settled, the service publishes `SettlementCompleted`.
- If the off-session charge needs customer action or is declined, the service creates a Payment Link and publishes `SettlementActionRequired`. When the customer pays, the webhook publishes `SettlementCompleted`.
- Admins can make a manual refund, for example for a lost set that came back.
- `charge.dispute.created` flags the customer for admin review.

## Optional: hybrid deposit hold for short rentals

A card hold (authorization without capture) costs no Stripe fee when it is canceled, but it expires after about 7 days. The hybrid mode uses a hold only when the whole hold window fits:

| Condition | Deposit method |
|-----------|----------------|
| `pick-up window + RentalDays + 1 day inspection margin <= 6 days` | **Hold** (`capture_method = manual`) |
| Otherwise | **Charge and refund** (default) |

Hold flow:

1. Checkout charges only the rental, with `setup_future_usage = off_session`.
2. On `checkout.session.completed`, the service creates a deposit PaymentIntent with the saved card: `capture_method = manual`, `off_session = true`, `confirm = true`. Status becomes `Authorized`, and the service stores `ExpiresAt`.
3. If the hold fails (authentication required or declined), the service falls back to charge and refund: it e-mails a deposit-only Checkout link. The customer cannot pick up the set until the deposit is paid. `Confirmed` waits for `DepositSecured`.
4. **Safety job:** if a hold expires within 24 hours and the reservation is not settled (for example, the set is late), the service captures the full hold. The deposit is then `Charged`, and the normal refund path applies.

The mode is a configuration switch (`Payments:DepositMode = ChargeAndRefund | Hybrid`). The rest of the system does not change. Reservation only sees `DepositSecured`, and settlement works for both modes.

## Endpoints

| Method | Route | Policy | Purpose |
|--------|-------|--------|---------|
| GET | `/payments/me` | `CustomerOnly` | Customer payment history, deposit status |
| GET | `/payments?reservationId` | `EmployeeOrAdmin` | Payments and settlement of a reservation |
| POST | `/payments/{id}/refund` | `AdminOnly` | Manual refund (for example, a lost set that came back) |

## Events

| Event | Publisher | Consumers |
|-------|-----------|-----------|
| `DepositSecured` | Payment | Reservation |
| `PaymentRefunded` | Payment | Reservation, Notification |
| `SettlementCompleted` | Payment | Reservation, Notification |
| `SettlementActionRequired` | Payment | Reservation, Notification |
