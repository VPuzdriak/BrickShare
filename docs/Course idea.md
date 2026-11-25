I am building the course for the developers who want to learn how to build applications using Azure services.
Prerequisites for the students:

- Know how to build ASP.NET Core Web APIs
- Know how to work with EF Core
- Familiar with GitHub
- Familiar with powershell

What are building: a lego rental online shop called Brickshare.
Customer can:

- Look through the catalog of lego sets
- Pick the available lego set from catalog. The shop might have multiple instances of the same set
- Reserve the set. Every set has a minimal time for reservation
- Pay with Stripe to reserve the set
- Bring back the set on due date
- Check the history of reservations on his portal

Shop admin can:

- Manage catalog and instances
- Register employees and assign the roles. Admin or Employee.
- Ban customers for breaking shop rules. Not returning set in time, beeing neglected towards received sets

Employee can:

- Give the reserved set to a customer
- Inspect the set when customer brings it back
- Accept returned set and fill the information about damages
- Charge customer additionally for overdue or damage made to the set

Technical stack:

- .NET 10
- ASP.NET Core Web Api
- Microservice architecture
- Microsoft Entra ID for admins and employees. E-mail with OTP or social logins for customers
- Postgres and Cosmos DB for databases
- Azure Blob storage for the images
- AppServices and Container Apps as hosting solutions
- Docker for packaging
- Docker compose for local development

Technical aspects:

- Central Editorconfig
- Cental Package management
- SonarAnaylyzer.NET in every project
- Treat all warnings as errors
- Github as a repo + actions
- Development, Test, Production environments

Bonus section:

- Convert everything to Aspire
- Upgrade to .NET 11
