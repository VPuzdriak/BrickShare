# Customer accounts

Customers sign in with e-mail one-time passcode, Google, or Facebook through Microsoft Entra External ID. BrickShare creates the customer profile on the first call.

| Item | Value |
|------|-------|
| Actors | Customer, Employee, Admin |
| Services | Customer |
| Architecture | [Customer Service](../Architecture.md#36-customer-service), [Identity and authorization](../Architecture.md#5-identity-and-authorization) |

## Rules

- On the first call with a valid customer token, the service creates the `Customer` record (just-in-time provisioning from token claims) and publishes `CustomerRegistered`.
- The user id is always the `oid` claim. Customer-facing endpoints never take a customer id from the route. They read it from the token.
- Customer Service records overdue and damage cases from `ReservationClosed` in a simple "strikes" view that helps admins decide on bans.

## Endpoints

| Method | Route | Policy | Purpose |
|--------|-------|--------|---------|
| GET | `/customers/me` | `CustomerOnly` | Own profile, auto-created on first call |
| PUT | `/customers/me` | `CustomerOnly` | Update profile |
| GET | `/customers?search` | `EmployeeOrAdmin` | Find customers |

## Events

| Direction | Events |
|-----------|--------|
| Publishes | `CustomerRegistered` |
| Consumes | `ReservationClosed` (strikes) |
