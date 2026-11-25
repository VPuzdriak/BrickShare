# BrickShare features

Each file describes one feature: rules, endpoints, events, and flows. The technical design is in [Architecture.md](../Architecture.md). Implementation status is tracked in [Progress.md](../Progress.md).

| # | Feature | Actors | Services |
|---|---------|--------|----------|
| 01 | [Catalog management](01-catalog-management.md) | Admin | Catalog, Media Functions, Rebrickable |
| 02 | [Catalog browsing and search](02-catalog-browsing-and-search.md) | Customer | Catalog, Reservation |
| 03 | [Customer accounts](03-customer-accounts.md) | Customer, staff | Customer |
| 04 | [Staff management](04-staff-management.md) | Admin | Customer, Microsoft Graph |
| 05 | [Customer bans](05-customer-bans.md) | Admin, system | Customer, Reservation |
| 06 | [Reservation and payment](06-reservation-and-payment.md) | Customer, staff | Reservation, Payment |
| 07 | [Deposit](07-deposit.md) | Customer, system | Payment |
| 08 | [Pick-up](08-pickup.md) | Employee, system | Reservation, Rental Timer Functions |
| 09 | [Return and inspection](09-return-and-inspection.md) | Employee, Customer | Reservation, Inspection, Payment, Catalog |
| 10 | [Overdue and lost sets](10-overdue-and-lost-sets.md) | System, staff | Rental Timer Functions, Reservation, Payment, Customer, Catalog |
| 11 | [Notifications](11-notifications.md) | Customer | Notification Functions |

## Coverage of the course idea

| Requirement from [Course idea.md](../Course%20idea.md) | Feature |
|-----------------------------------------------------------|---------|
| Customer looks through the catalog | [02](02-catalog-browsing-and-search.md) |
| Pick an available set, multiple instances | [02](02-catalog-browsing-and-search.md), [06](06-reservation-and-payment.md) |
| Reserve with minimal reservation time | [06](06-reservation-and-payment.md) |
| Pay with Stripe | [06](06-reservation-and-payment.md), [07](07-deposit.md) |
| Bring back on due date | [09](09-return-and-inspection.md), [10](10-overdue-and-lost-sets.md) |
| Reservation history on the portal | [06](06-reservation-and-payment.md) |
| Admin manages catalog and instances | [01](01-catalog-management.md) |
| Admin registers employees and assigns roles | [04](04-staff-management.md) |
| Admin bans customers | [05](05-customer-bans.md) |
| Employee gives the reserved set | [08](08-pickup.md) |
| Employee inspects and accepts the returned set, records damages | [09](09-return-and-inspection.md) |
| Employee charges for overdue or damage | [07](07-deposit.md), [09](09-return-and-inspection.md) |
| Protection against non-returned sets (added) | [07](07-deposit.md), [10](10-overdue-and-lost-sets.md) |
