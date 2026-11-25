# Staff management

Admins register employees and assign the role `Admin` or `Employee`. Staff sign in with Microsoft Entra ID (workforce tenant).

| Item | Value |
|------|-------|
| Actors | Admin |
| Services | Customer, Microsoft Graph |
| Architecture | [Customer Service](../Architecture.md#36-customer-service), [Identity and authorization](../Architecture.md#5-identity-and-authorization) |

## Rules

- Admin enters the e-mail of an existing user in the workforce tenant.
- The service uses Microsoft Graph with its managed identity (`AppRoleAssignment.ReadWrite.All`) to assign the app role `Admin` or `Employee` on the BrickShare API enterprise application.
- `Admin` can also do all `Employee` tasks.
- Role changes take effect at the user's next sign-in.

## Endpoints

| Method | Route | Policy | Purpose |
|--------|-------|--------|---------|
| GET | `/staff` | `AdminOnly` | List staff |
| POST | `/staff` | `AdminOnly` | Register employee and assign role |
| PUT | `/staff/{id}/role` | `AdminOnly` | Change role |
| DELETE | `/staff/{id}` | `AdminOnly` | Remove role assignment |

## Events

| Direction | Events |
|-----------|--------|
| Publishes | `StaffRoleAssigned` |
