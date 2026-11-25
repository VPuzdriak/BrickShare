# Notifications

Customers get e-mails about their reservations, payments, and account.

| Item | Value |
|------|-------|
| Actors | Customer |
| Services | Notification Functions, Azure Communication Services |
| Architecture | [Notification Functions](../Architecture.md#371-notification-functions) |

## E-mails

| Event | E-mail |
|-------|--------|
| `ReservationConfirmed` | Reservation confirmed, pick-up deadline |
| `ReservationExpired` | Unpaid reservation expired |
| `ReturnReminderDue` | Return reminder (day before due date) |
| `ReservationOverdue` | Overdue notice (days 1–3), growing late fee |
| `ReservationLost` | Set treated as lost, deposit kept, remaining charge |
| `PaymentRefunded` | Deposit refund with amount and deducted fees |
| `SettlementActionRequired` | Payment Link for fees the saved card could not cover |
| `CustomerBanned` | Account banned, reason |

## Rules

- In Azure, e-mails go through Azure Communication Services Email. In Development they go to Mailpit through SMTP.
- A failed e-mail is retried by Service Bus, then dead-lettered.
