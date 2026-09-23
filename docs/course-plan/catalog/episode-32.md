# Episode 32 — What this API says about itself

← [Course plan](catalog-api.md) · Previous: [Episode 31 — Retire is not delete](episode-31.md)

Twelve episodes ago this service had no endpoints. It now has four, spread over three resources, and
the only description of them that exists anywhere is a `.http` file in our own repository and the
C# that implements them. A client integrator — the shop's till software, the tablet app in the
stockroom, the rentals service that does not exist yet — gets nothing.

This episode generates that description from the code, and then does the part that most OpenAPI
tutorials skip: **reads the generated document line by line and asks whether it is true.** It is not,
in five distinct ways, and every one of them is a real lesson about the gap between what the type
system knows, what the serializer does, and what the document claims. Only once it is true does the
episode put a browsable page in front of it — and chooses between the two obvious ones by running
both against our document rather than by asking which is more popular.

**Done when** `/openapi/v1.json` describes four operations under three tags, every error status code
this service can produce is in it, nothing in it is a lie, and a person with no access to this
repository can read all of it in a browser.

> **Runtime: about 19 minutes**, well over the usual budget and said plainly rather than wished
> away — step 4 is five findings and step 6 compares two products. Recording it short, in order:
> cut step 6 down to the verdict and one page (~2 minutes), then let step 7's demolition demo
> become a paragraph (~2 minutes). If it wants splitting later the seam is clean: **32a** = steps
> 1–4, generate the document and audit it, ending on the five findings; **32b** = steps 5 onward,
> repair them, put a page in front of the result, and make the honesty argument.

## Before recording

- Episode 31 merged: four endpoints, and the whole suite green.
- `docker compose up` for Postgres, `dotnet user-secrets` still holding your Rebrickable key.
- A branch. **No migration, no test project, nothing in `Domain/`.**
- A browser, for step 6, and a terminal with `jq` on it. Half this episode is reading JSON out
  loud, and `jq` is the difference
  between reading it and squinting at it.
- `docs/architecture/catalog.md` open at *"API surface"* — the line that says OpenAPI comes from the
  built-in `Microsoft.AspNetCore.OpenApi`, written long before this code.

**No step of this episode is driven by a test, and the episode says so in its first minute.** A
package reference, two lines of wiring, and endpoint metadata that has no behaviour of its own are
exactly the exemption `CLAUDE.md` names. Saying it out loud matters more here than usual, because
this episode follows two heavily test-driven ones — the rule is *a test drives behaviour*, not
*every line arrives with a test*, and an episode that faked a red test for a `.csproj` edit would
be teaching cargo cult.

Every sample below names its file and where in it the code goes. Where something is edited in a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `Directory.Packages.props` | Two `PackageVersion` lines, one of them with a version number that has a story |
| `src/…/Api/BrickShare.Catalog.Api.csproj` | Two `PackageReference` lines |
| `src/…/Api/Program.cs` | `AddOpenApi` with a document transformer, `MapOpenApi`, `MapScalarApiReference`, one `ExcludeFromDescription` |
| `src/…/Api/Endpoints/CatalogLookupEndpoints.cs` | Metadata on one endpoint |
| `src/…/Api/Endpoints/CatalogSetEndpoints.cs` | Metadata on one endpoint, and a `///` comment that should never have been `///` |
| `src/…/Api/Endpoints/CopyEndpoints.cs` | Metadata on two endpoints |
| `src/…/Api/BrickShare.Catalog.Api.http` | Requests 17 and 18 |

The second package is chosen on camera in step 6, after a third one is wired up beside it and deleted.

---

## Step 1 — Four endpoints and no contract

No code in this step. Put the four routes on screen and ask the question a client integrator asks.

```
POST /api/v1/catalog/lookups
POST /api/v1/catalog/sets
POST /api/v1/catalog/sets/{setId}/copies
POST /api/v1/catalog/copies/{copyId}/retirement
```

What does the till software send to the third one? What comes back? What happens if the set is not
catalogued? Every answer to those questions currently lives in two places — the C# in this
repository, and the heads of the two of us who wrote it. **Neither is reachable from another team.**

There are three ways out and only one of them is chosen here.

| | Hand-written document | **Generated from the code** | A rendered page on top of one of them |
| --- | --- | --- | --- |
| Where the truth lives | In a file somebody remembers to edit | In the types that are already compiled | Wherever the document it renders got it |
| Wrong when | Somebody renames a field and forgets | The code cannot express something (see step 4) | Never independently — it inherits |
| Costs | A permanent maintenance habit | A package and two lines | A second package and one line |
| Fails | Silently, and confidently | Loudly on the parts it can check, silently on the parts it cannot | It shows you exactly what it was given |

**Hand-written loses immediately.** A document maintained by discipline is a document that is
accurate on the day it is written and wrong within a month, and — this is the real damage — it is
*confidently* wrong, because it looks maintained.

**The third column is not a competitor to the second — it sits on top of it**, which is why this
episode ends up shipping both. A generated JSON document is the machine-readable contract; a
rendered page is that same document made readable by a person who has never seen this repository.
Step 6 adds one, and picks between the two obvious candidates.

**And the `.http` file stays.** It is tempting to say a browsable page replaces it, and that is
wrong, because they serve different people. The `.http` file is version controlled, diffable,
reviewable and runnable from the editor that is already open — it is how *we* drive this service,
in every episode since 21. The page is how *somebody else* reads the API without cloning anything.
Two audiences, two artefacts, and the mistake would be making them compete.

**Caveman version:** shop have four door. Nobody paint sign. Man from other village want come in,
must ask shop man every time. Shop man not always here. Paint sign — but paint sign by hand, sign
lie when door move. Better: sign draw itself from door.

---

## Step 2 — Two lines, and a package that the build refuses

Wiring, not test-driven, and said out loud. Start with the package.

```bash
dotnet add src/Catalog/BrickShare.Catalog.Api package Microsoft.AspNetCore.OpenApi
```

Central package management means that lands in two files. In `Directory.Packages.props`:

```xml
<!-- Directory.Packages.props — already there, the anchor to find -->
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
```

```xml
<!-- Directory.Packages.props — paste the second line -->
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
```

```xml
<!-- src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj — already there -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
```

```xml
<!-- src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj — paste above it -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
```

Build it. **It fails, and this is the best two minutes of the episode.**

```
error NU1903: Warning As Error: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity
vulnerability, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
```

Nothing was typed wrong. `Microsoft.AspNetCore.OpenApi` 10.0.0 depends on `Microsoft.OpenApi` 2.0.0,
that transitive package has a published advisory against it, and **episode 3's
`TreatWarningsAsErrors` turned the advisory from a line of build output nobody reads into a build
that stops.** Say the sentence: this is the policy from episode 3 catching a real vulnerability in a
real dependency, nine episodes later, on a package Microsoft ships. That is what the policy was for.

The fix is a version, not a suppression:

```xml
<!-- Directory.Packages.props — replace the line just added -->
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.11" />
```

`10.0.11` brings `Microsoft.OpenApi` 2.7.5 and the build goes green. Check it on camera —
`dotnet list package --include-transitive | grep -i openapi` — because the point is not the number,
it is that **the number was chosen by looking at what it drags in.** `dotnet add package` gave us
the latest it knew about and that was not good enough.

> Worth naming: the patch line also matches the `10.0.11` already pinned for the EF Core packages.
> Keeping one ASP.NET/EF patch line across the file is not a rule, but it makes upgrades one
> decision instead of six.

**Caveman version:** man take tool from box. Tool have crack. Old rule from long ago say — no crack
tool in hut, work stop. Work stop. Man not tape crack, man take newer tool. Rule good rule.

### The two lines

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — already there, the anchor
builder.Services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — paste above it
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — already there
app.UseExceptionHandler();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — paste after it
app.UseExceptionHandler();

// Mapped in every environment, not only Development. Step 8 makes the argument.
app.MapOpenApi();
```

Run, and fetch it:

```bash
curl -s http://localhost:5080/openapi/v1.json | jq '.paths | keys'
```

Five paths, four of them ours, and a document nobody wrote.

**One name to disarm before somebody assumes it:** the `v1` in `/openapi/v1.json` is the *document
name* — `AddOpenApi()` defaults to `"v1"` — and it has nothing whatsoever to do with the `/api/v1`
prefix from episode 21. The two matching here is a coincidence. The framework does not know we
version our routes, and it will still be called `v1.json` on the day we add `/api/v2`. Episode 21
already argued that we do not need `Asp.Versioning`; this is the one place that decision shows up
in a filename.

---

## Step 3 — What came free

`curl -s http://localhost:5080/openapi/v1.json | jq` and read it, slowly. Five things arrived that
nobody typed.

```json
"/api/v1/catalog/sets/{setId}/copies": {
  "post": {
    "tags": [ "Copies" ],
    "parameters": [
      { "name": "setId", "in": "path", "required": true,
        "schema": { "type": "string", "format": "uuid" } }
    ],
    "requestBody": {
      "content": { "application/json": {
        "schema": { "$ref": "#/components/schemas/RegisterCopiesRequest" } } },
      "required": true
    },
    "responses": {
      "201": { "description": "Created", "content": { "application/json": {
        "schema": { "$ref": "#/components/schemas/RegisterCopiesResponse" } } } }
    }
  }
}
```

1. **The route and its shape**, including `format: "uuid"` on `setId` — which came from the
   `:guid` route constraint episode 29 wrote for a completely different reason.
2. **The request and response schemas**, generated from the `record`s at the bottom of the endpoint
   files. `RegisterCopiesRequest`, `CopyToRegister`, `RegisterCopiesResponse`, `CopyResponse` — all
   of them, all their properties, camelCased exactly as the wire is.
3. **`201`, with the right body type**, from `Results<Created<RegisterCopiesResponse>, …>`. This is
   episode 28's promise being kept: the `Results<…>` union is read by something other than the
   compiler at last.
4. **The enums as strings:**

   ```json
   "ConditionGrade": { "enum": [ "New", "Excellent", "Good", "Fair" ] },
   "CopyStatus": { "enum": [ "Available", "Reserved", "OnRent", "AwaitingInspection",
                             "InInspection", "InRepair", "Lost", "Retired" ] }
   ```

   Because of the `JsonStringEnumConverter` registered in episode 26. Had that call not been there,
   this document would be telling clients to send `0` and `1` — and step 4's point is that they
   would have believed it.
5. **The tags.** `"Catalog lookups"`, `"Catalog sets"`, `"Copies"` — written with `WithTags` in
   episodes 21, 27 and 29 purely because a route group looked tidier with a name on it. Nothing read
   them until this minute. **Now they are the table of contents**, and the difference between a
   reference document and a list of four routes is visible on screen rather than asserted.

**Caveman version:** man not write sign. Sign already in wood. Man only need say "show me wood".
Wood remember shape of door, shape of key, name of room — man carve name long ago, forget why,
now name make map.

---

## Step 4 — Now read it like a client integrator, not like its author

This is the centre of the episode. **A generated document is not automatically a true document.**
Five findings, in the order they appear in the JSON.

### Finding 1 — the document is called the wrong thing

```json
"info": { "title": "BrickShare.Catalog.Api | v1", "version": "1.0.0" }
```

That is an assembly name and an assembly version. Neither is a fact about the API: the assembly could
be renamed tomorrow without a single client noticing, and `1.0.0` is what the SDK defaults to when
nobody sets `<Version>`. It is the first thing a human reads and the first thing a client generator
uses to name the package it produces.

### Finding 2 — an endpoint that should not be published, and the junk it drags in

```json
"tags": [ { "name": "BrickShare.Catalog.Api" }, { "name": "Catalog sets" },
          { "name": "Catalog lookups" }, { "name": "Copies" } ]
```

A fourth tag, named after the assembly. It belongs to `GET /` — the smoke-test endpoint from episode
2, which never got a `WithTags` because it was never part of a group, **so the framework tagged it
with the assembly name.** And its response schema is worse:

```json
"AnonymousTypeOfstring": {
  "required": [ "service" ],
  "properties": { "service": { "type": [ "null", "string" ] } }
}
```

`AnonymousTypeOfstring`, because `new { service = "…" }` has no name to give. The health checks are
not in the document at all — `MapHealthChecks` produces no metadata — and `GET /` is the same kind
of thing: **ours, not the client's.** It should be absent for exactly the reason they are.

### Finding 3 — `required` is right, and it is right for a reason worth stating

```json
"CatalogueSetRequest": {
  "required": [ "lookupId", "retailPrice", "baseRentalPrice", "minimumRentalDays", "minimumAge" ],
  ...
}
```

All five. Nobody wrote an attribute. They are required because every one of them is a **non-nullable
value type** on the record, and the generator reads nullability the same way the compiler does —
which works because `<Nullable>enable</Nullable>` has been on since episode 2.

That is a genuine payoff, and it comes with a trap that has to be said on camera: **under nullable
reference types, a plain `string` property is `required` too.** The day somebody adds an optional
`string Note` to a request record, this document will tell every client that they must send it. The
fix is `string? Note`, and the point is bigger than the fix — **nullability has just stopped being a
compiler preference and become part of the published contract.**

And the subtler one, on the response side:

```json
"LookupResponse": {
  "required": [ "lookupId", …, "imageUrl", "fetchedAt" ],
  "properties": { "imageUrl": { "type": [ "null", "string" ], "format": "uri" } }
}
```

`imageUrl` is **required *and* nullable**, and both are correct: the property is always present in
the response, and its value may be `null`. In JSON Schema `required` means *the key is there*, not
*the value is useful*. Half the people reading this document will misread it, which is a good reason
for the endpoint description to say "imageUrl may be null" in words.

### Finding 4 — the document knows something about our API that we did not

```json
"retailPrice": {
  "pattern": "^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?$",
  "type": [ "number", "string" ],
  "format": "double"
}
```

Every number in this API — prices, piece counts, weights, ages — is typed **`["number","string"]`**.
The document is claiming that a client may send `"9.99"` in quotes.

Do not argue with it. Test it:

```bash
curl -i -X POST http://localhost:5080/api/v1/catalog/sets \
  -H 'content-type: application/json' \
  -d '{"lookupId":"<a real lookup id>","retailPrice":"9.99","baseRentalPrice":"2.50",
       "minimumRentalDays":"3","minimumAge":"6"}'
```

**`201`.** The document is right and we were wrong about our own service. ASP.NET Core configures
`System.Text.Json` with `JsonSerializerDefaults.Web`, which sets
`NumberHandling = AllowReadingFromString`, and that has been true since episode 21 — we simply never
knew, because nothing ever told us. It took a generated document to surface a behaviour that was
always there.

So: keep it or change it? **Keep it.** It is harmless, it is the platform default every other
ASP.NET Core service on the planet has, and — the real argument — *changing how the service
deserializes JSON in order to make its documentation tidier is the tail wagging the dog.* The
document now tells the truth about what we accept. That is what it is for.

**Caveman version:** man draw map of own cave. Map show tunnel man never know about. Man crawl in
tunnel — tunnel real. Map not wrong. Man wrong. Tunnel been there whole time.

### Finding 5 — the part nobody can see: every error, and every rule

```json
"responses": { "201": { … } }
```

That is the *whole* response list for `POST /catalog/sets`. There is no `400`, no `409`, no `422` —
and this endpoint produces all three, each one pinned by an integration test since episodes 22, 23
and 24.

Two separate reasons, and conflating them is the mistake:

- **`ProblemHttpResult` contributes no metadata.** It does not implement `IEndpointMetadataProvider`,
  so the second arm of every `Results<Created<T>, ProblemHttpResult>` union is invisible to the
  generator. `Created<T>` gives a `201`; its partner gives nothing. Episode 28's promise holds for
  half the union, and the script says which half rather than skipping past it.
- **`ValidationProblem` is not in the unions any more.** Episode 30 took it out when validation moved
  into `ValidationFilter<T>`, and that episode said the bill would arrive here. It has.

And underneath both: **the validators are entirely invisible.** The schema says `retailPrice` is any
number; `CatalogueSetRequestValidator` refuses negatives, insists `minimumRentalDays >= 1` and
`minimumAge` between 0 and 18, and caps a batch at 100 copies. **The document describes a request as
legal that the service answers with a `400`.**

Three ways to close that, and the choice is argued rather than assumed:

| | What it costs | Verdict |
| --- | --- | --- |
| Duplicate the rules as `[Range]` / `[MaxLength]` attributes | Two sources of truth for every rule, forever | **No.** They will drift, and the compiler cannot notice |
| A schema transformer that reads the FluentValidation rule sets | Real plumbing: walk `IValidator`, map rule types to keywords, handle the ones that do not map | **Not now** — nobody generates clients from this document yet. This is the thing to reach for the day somebody does |
| State the constraints in the endpoint `description` | A sentence per endpoint, hand-maintained | **Yes** — honest, cheap, and visibly hand-written, which is the point |

**Caveman version:** sign show door. Sign not show guard behind door. Man walk in, guard hit man.
Sign not lie about door — sign quiet about guard. Quiet sign still hurt man. Write guard on sign.

**The five findings on one slide before moving on.** Wrong title · a published endpoint that should
not be · nullability now public · numbers we did not know we accepted · no errors and no rules.
That list is the episode. Everything after it is repair.

---

## Step 5 — Repair

Metadata, not behaviour: still no test, still said out loud. Four endpoints, one `Program.cs`, and
one `///` that should never have been `///`.

### The document's own identity

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — already there
builder.Services.AddOpenApi();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — replace it
// The title and version belong to the API, not to the assembly. Left alone, the document calls
// itself "BrickShare.Catalog.Api | v1" and claims version 1.0.0 — an assembly name and an assembly
// version, neither of which is a fact about this API.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "BrickShare Catalog API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Staff-facing catalog service: look a set up, catalogue it, register copies of it, retire a copy.";

        return Task.CompletedTask;
    }));
```

A document transformer runs once per generated document and gets the whole `OpenApiDocument` to edit.
There are also operation and schema transformers — the same idea one level down — and the third of
them is the hook the FluentValidation option in step 4 would have used. Worth naming them all in one
sentence so students know the extension point exists; worth using exactly one of them today.

### The endpoint that is ours, not theirs

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — already there
app.MapGet("/", () => new { service = "BrickShare Catalog API" });
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — replace it
// Excluded on purpose. It is a smoke-test endpoint for us, not a published operation, and the
// health checks are already absent for exactly the same reason.
app.MapGet("/", () => new { service = "BrickShare Catalog API" })
    .ExcludeFromDescription();
```

The junk tag and `AnonymousTypeOfstring` both disappear with it. Note what was *not* done: the
endpoint was not deleted and not renamed. **It is not undocumented because it is secret — it is
undocumented because it is not part of the contract.**

### The comment that was published

This one is worth stopping on. Episode 21 wrote this, in good faith, as a note to ourselves:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — already there
/// <summary>
/// What staff send to catalogue a set. Every product fact in here is client-supplied, which is a security problem
/// </summary>
```

And here it is in the document that goes to other teams:

```json
"CatalogueSetRequest": {
  "description": "What staff send to catalogue a set. Every product fact in here is client-supplied, which is a security problem"
}
```

`GenerateDocumentationFile` — switched on in episode 3 so that IDE0005 would run — means the XML
doc comments are compiled into a file, and the OpenAPI generator reads it. **A `///` comment is
published; a `//` comment is not.** Nobody decided that; it fell out of two unrelated settings
meeting. The fix is to write the summary for the audience that actually reads it:

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace it
/// <summary>
/// What staff send to catalogue a set: the id of an earlier lookup, and the commercial terms.
/// </summary>
// Every product fact behind that lookup is client-supplied, which is a security problem — episode 38.
// That note is for us, so it is a // comment. A /// comment is published to every client.
```

**Caveman version:** man write note to self on back of sign. Sign turn around. Whole village read
note. Note say "hut wall weak". Man learn: two kind of writing — writing for self, writing for
village. Look which side of wood.

### The status codes, and the rules the schema cannot hold

Four endpoints, same shape each time.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — already there
        group.MapPost("/", LookUpAsync)
            .AddEndpointFilter<ValidationFilter<LookupRequest>>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogLookupEndpoints.cs — replace it
        group.MapPost("/", LookUpAsync)
            .AddEndpointFilter<ValidationFilter<LookupRequest>>()
            .WithSummary("Look a set up on Rebrickable")
            .WithDescription(
                "Stores the product facts for a set number as a snapshot and returns its lookupId. "
                + "setNumber is at most 32 characters and usually ends in -1.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — already there
        group.MapPost("/", CatalogueAsync)
            .AddEndpointFilter<ValidationFilter<CatalogueSetRequest>>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CatalogSetEndpoints.cs — replace it
        group.MapPost("/", CatalogueAsync)
            .AddEndpointFilter<ValidationFilter<CatalogueSetRequest>>()
            .WithSummary("Catalogue a set the shop will rent out")
            .WithDescription(
                "Turns a lookup into a catalogued set. Prices are not negative, minimumRentalDays "
                + "is at least 1, and minimumAge is between 0 and 18 — none of which the schema "
                + "below can say, because those rules live in CatalogueSetRequestValidator.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — already there
        group.MapPost("/", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterCopiesRequest>>();
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace it
        group.MapPost("/", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterCopiesRequest>>()
            .WithSummary("Register copies of a catalogued set")
            .WithDescription(
                "Registers 1 to 100 physical boxes in one transaction: all of them or none of them. "
                + "Each copy is minted a label code. Weights are in grams, 1 to 50000.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — already there
        group.MapPost("/{copyId:guid}/retirement", RetireAsync);
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Endpoints/CopyEndpoints.cs — replace it
        group.MapPost("/{copyId:guid}/retirement", RetireAsync)
            .WithSummary("Retire a copy")
            .WithDescription(
                "Takes a copy out of service without deleting it: the row, its label and its "
                + "history stay. Not idempotent — retiring a retired copy is a 409.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
```

Three things to point at while pasting.

**The retirement endpoint has no `ProducesValidationProblem`.** It has no request body — the whole
request is a route parameter, and the `:guid` constraint rejects anything else before the handler
runs. No validator, no `400`, no claim. **The metadata is per endpoint because the truth is per
endpoint**, and a copy-pasted `ProducesValidationProblem` here would have been the episode's own
first lie.

**The descriptions carry the validator rules in prose**, which is the decision made in step 4 —
visibly hand-maintained, and the second description says so out loud by naming the class the rules
actually live in. A reader who wants the real rule is told where it is.

**`ProducesProblem` sets the content type to `application/problem+json`** and points at a
`ProblemDetails` schema, which is exactly what `AddProblemDetails` from episode 22 puts on the wire.
One small honesty note while it is on screen: the generated `ProblemDetails` schema has no `traceId`
in it, because that extension is added at runtime by `CustomizeProblemDetails`. The document
under-describes the body by one field. It is worth knowing about; it is not worth a transformer.

Now regenerate and read the same endpoint again:

```json
"/api/v1/catalog/copies/{copyId}/retirement": {
  "post": {
    "tags": [ "Copies" ],
    "summary": "Retire a copy",
    "description": "Takes a copy out of service without deleting it: …",
    "responses": {
      "204": { "description": "No Content" },
      "404": { "description": "Not Found", "content": { "application/problem+json": {
                 "schema": { "$ref": "#/components/schemas/ProblemDetails" } } } },
      "409": { "description": "Conflict", "content": { "application/problem+json": {
                 "schema": { "$ref": "#/components/schemas/ProblemDetails" } } } }
    }
  }
}
```

---

## Step 6 — A page for the people who do not have the repo

The document is now correct and it is still a wall of JSON. That is the right format for a client
generator and the wrong one for the developer writing the stockroom tablet app, who wants to see
four operations, click one, and read what it answers with.

**This step lands after the repair on purpose.** A page built over the step-3 document would have
shown four untitled operations, no error codes and a schema full of surprises. The page is the
payoff for step 5, and the order makes that visible: the same renderer over the same service,
before and after, is the argument for the metadata pass.

Two candidates, and the honest way to choose is to run both. Add both packages, map both pages,
look at them, and delete the loser on camera.

```xml
<!-- Directory.Packages.props — paste under the OpenApi line from step 2 -->
    <PackageVersion Include="Scalar.AspNetCore" Version="2.17.8" />
    <PackageVersion Include="Swashbuckle.AspNetCore.SwaggerUi" Version="10.2.3" />
```

```xml
<!-- src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj — paste after the Npgsql line -->
    <PackageReference Include="Scalar.AspNetCore" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUi" />
```

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — temporarily, both of them, to compare
app.MapOpenApi();

app.MapScalarApiReference();

app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "BrickShare Catalog API v1"));
```

`Scalar.AspNetCore` needs `using Scalar.AspNetCore;` at the top. Both read the document that
`MapOpenApi()` already serves; neither generates anything. `/scalar/v1` and `/swagger/index.html`.

### What is actually different

Everything in this table was checked against the two running pages rather than taken from a README —
and one row is in it *because* the README folklore turned out to be stale.

| | **Scalar** 2.17.8 | **Swagger UI** (`Swashbuckle.AspNetCore.SwaggerUi` 10.2.3) |
| --- | --- | --- |
| Wiring | `app.MapScalarApiReference()` — an **endpoint** | `app.UseSwaggerUI(…)` — **middleware** |
| What it drags in | Nothing. No transitive packages | Nothing. **Not** Swashbuckle's generator — this is the UI half only |
| Assets | Served by the app, `/scalar/scalar.js`, 4.3 MB | Served by the app, embedded in the DLL, 1.5 MB |
| Renderer | Built for OpenAPI 3.1 | swagger-ui 5.32.7 — 3.1 support retrofitted onto a 3.0 renderer |
| Try it | Built-in client, with ready-made code samples per language | "Try it out" |
| Recognition | Newer. Some people will not have seen it | Everybody has seen it |

**The CDN row that is not in the table.** Scalar's reputation — including in its own older
documentation — is that it loads its bundle from a CDN, which would be a genuine problem for a
service behind a corporate proxy. At 2.17.8 that is simply no longer true: `curl` the page, follow
the `<script src="scalar.js">` it emits, and the 4.3 MB bundle comes from our own App Service with
no external host in it. **Half an hour of received wisdom, disproved by one `curl`** — and worth
thirty seconds on camera, because the students will have read the same blog posts.

### The three questions to ask both pages

Open each, and look for the same three things. This is the on-camera comparison, and the answers
are what should decide it — not the table above, and not taste.

1. **Do the tags become navigation?** Three groups, four operations. A page that shows them as a
   flat list has thrown away the structure episodes 21, 27 and 29 put in.
2. **Does the `summary` and `description` text from step 5 actually appear?** Those sentences carry
   the validator rules that no schema can express. If a renderer buries them, step 5 bought nothing.
3. **The deciding one: how does each render the two honest-but-awkward bits of OpenAPI 3.1 that
   step 4 uncovered?** `CatalogueSetRequest.retailPrice` is `"type": ["number","string"]`, and
   `LookupResponse.imageUrl` is **required and** `"type": ["null","string"]`. A renderer that shows
   both types is telling a client integrator the truth. A renderer that picks one and hides the
   other is prettier and wrong.

### The verdict

**Scalar**, on three grounds, and the third is the one specific to this service.

- **3.1 is its native target**, not a retrofit. Our document is 3.1.1 and — precisely because of the
  audit in step 4 — it leans on 3.1 constructs that a 3.0-shaped renderer has to approximate.
- **The client it ships** produces a request in the reader's own language. The audience for this page
  is somebody about to write code against us; the shortest path from "reading" to "calling" is worth
  a package.
- **It is an endpoint, not middleware.** `app.MapScalarApiReference().RequireAuthorization()`
  compiles; `app.UseSwaggerUI(…)` has nothing to hang that on. **Episode 38 is coming**, and when it
  does, the page is secured with the same one call as every other route in this service instead of a
  bespoke middleware branch. The wiring style is not cosmetic — it decides how much work the security
  episode is.

**Swagger UI is a legitimate choice and loses narrowly.** Its bundle is a third of the size, its
assets are embedded in the DLL rather than served as files, and *everybody already knows what it is*
— which in an organisation with fifty services and a shared convention is a better argument than any
of the three above. If your shop standardised on it, keep it. **The answer here is not "Scalar is
better"; it is "Scalar is better for a service that will put authorization on this page in six
episodes' time."**

Delete the runner-up on camera — both package lines and the `UseSwaggerUI` call — and say why the
deletion is part of the lesson: **the comparison was worth ten minutes, and carrying both forever
would not be.** Two ways to render the same document is not redundancy, it is two things to
maintain, two things to secure in episode 38, and two answers to "which page is the real one?"

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — what stays
app.MapOpenApi();

// The human-readable half. It renders the document above; it generates nothing itself, which is
// why there is no way for the two to disagree.
app.MapScalarApiReference();
```

**Caveman version:** man have map on stone. Map true now. Map still stone — heavy, hard to read for
stranger. Man get two picture-maker. Both make picture from same stone. One picture-maker draw two
colour when door have two colour. Other pick one colour, look nice, lie little. Man keep honest
one. Man also keep picture-maker who can stand behind gate later, when gate come. Then man smash
other picture-maker — two map, two thing to fix, village ask which map real.

---

## Step 7 — The lie this document is still capable of telling

`ProducesProblem(StatusCodes.Status409Conflict)` is a **claim**. Nothing checks it. Prove it, on
camera, in twenty seconds: comment out one line in `Program.cs`.

```csharp
// src/Catalog/BrickShare.Catalog.Api/Program.cs — comment this out, temporarily
builder.Services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
```

Regenerate the document. **It still advertises the `409`,** confidently, in JSON, to every client.
The service now answers a double retirement with a `500`.

Then run the tests. `RetireCopyTests.A_copy_cannot_be_retired_twice` is **red**.

That is the whole lesson of generated-from-code documentation, and it is a limit rather than a
defect: **the parts derived from types are as true as the code; the parts asserted by hand are as
true as the person who typed them.** What saves us is not the document. It is that episodes 22, 23
and 24 pinned every one of these status codes with an integration test. **The behaviour is checked
even though the document is not.**

Put the line back.

And the obvious next thought, rejected on purpose: *why not write a test that asserts the document
contains a `409`?* Because it would assert that we typed `ProducesProblem(409)`, which we can see.
It would pass just as happily with the exception handler deleted. **A test that pins a claim to
itself is a test that makes a lie harder to notice.** The test worth having is one that fires a
request and reads the status code — and that test already exists.

**Caveman version:** man carve "bear live here" on rock. Bear move away. Rock still say bear. Rock
never look. Man who check rock say rock right — man only read rock, not go in cave. Check cave.

---

## Step 8 — Why it is mapped everywhere

The `dotnet new` template wraps `MapOpenApi()` in `if (app.Environment.IsDevelopment())`. This
service does not — and neither is the page from step 6 — and the reasoning is the point.

Run it as production and ask for the document:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Catalog/BrickShare.Catalog.Api
curl -s http://localhost:5080/openapi/v1.json | jq '.info.title'
```

It is there. On purpose.

**Hiding the document is not a security control.** Every route it describes is reachable either way.
An attacker does not need our JSON — they need one URL and a few minutes. What the guard actually
achieves is: the people integrating with us, on the environment they integrate against, cannot read
the contract. It is an obstacle to exactly the audience the document exists for, and to nobody else.
Security here is **episode 38**, and it is authorization: an endpoint that refuses a request without
a valid token does not care who has read its description.

**The counter-argument, stated honestly, because it is real.** In some organisations publishing a
machine-readable map of your attack surface on a public host is a compliance question, not an
engineering one — and if that is the rule where you work, the guard is one line and you should write
it. What should not happen is writing it *by default, from a template, without knowing why*. That is
how a team ends up with a document their own partners cannot read and a security posture that
depends on people not typing URLs.

**Caveman version:** man hide map of own hut. Door still there. Thief not need map, thief push door.
Friend need map, friend now lost. Hide map — hurt friend, not hurt thief. Put lock on door instead.

---

## Step 9 — Run it

Not test-driven: a request collection is not code. Append to the end of
`BrickShare.Catalog.Api.http`, after episode 31's request 16.

```http
### BrickShare.Catalog.Api.http — append after request 16

### 17. The document this service generates about itself.
GET {{host}}/openapi/v1.json
Accept: application/json

### 18. The same thing, for a person. Open this one in a browser.
GET {{host}}/scalar/v1
```

Then the four shots that show the episode landed:

```bash
curl -s {{host}}/openapi/v1.json | jq '.info'
curl -s {{host}}/openapi/v1.json | jq '[.tags[].name]'
curl -s {{host}}/openapi/v1.json | jq '.paths | to_entries[] | {path: .key, summary: .value.post.summary, codes: (.value.post.responses | keys)}'
curl -s {{host}}/openapi/v1.json | jq '.paths | keys | length'
```

The expected output of the third one, which is the shot to linger on:

```
{ "path": "/api/v1/catalog/sets",                        "codes": ["201","400","409","422"] }
{ "path": "/api/v1/catalog/lookups",                     "codes": ["201","400","404","502"] }
{ "path": "/api/v1/catalog/sets/{setId}/copies",         "codes": ["201","400","404"] }
{ "path": "/api/v1/catalog/copies/{copyId}/retirement",  "codes": ["204","404","409"] }
```

**Four operations, four different response sets, and no two endpoints claiming the same thing.**
That is what the hand-maintained half bought — and every one of those codes is a code some episode
between 22 and 31 already pinned with a test.

---

## What this episode is not

**No Swagger UI — after comparing it**, which is a different thing from refusing it unexamined.
Step 6 wired it, read it and deleted it, and the reason it lost is written down so that a team who
disagrees can disagree with an argument rather than with a preference.

**No authentication on the page.** Anyone who reaches the service reaches `/scalar/v1`, exactly as
anyone who reaches it reaches the four endpoints it describes. That is episode 38's problem for all
five of them at once — and step 6 chose the renderer that can be included in that fix with one call.

**No generated clients.** The document is now good enough to generate a typed client from — and
nothing in this course consumes it yet, because there is no second service. When rentals arrives and
needs to call catalog, that is the episode where a generated client is a real question with a real
alternative (a small hand-written `HttpClient`, exactly like `RebrickableClient` from episode 25).

**No FluentValidation schema transformer.** Named in step 4, deliberately not built. It is
twenty-odd lines of reflection to add `minimum`, `maxLength` and `minItems` to schemas nobody
currently generates code from. **The day somebody generates a client, it pays for itself; today it
is plumbing with an audience of zero.**

**No document in CI.** There is a real technique here — commit the generated JSON, regenerate it in
the pipeline, fail the build on an unreviewed diff, and suddenly every breaking API change shows up
in code review. It is genuinely good and it belongs with the deployment episodes, where there is a
pipeline on screen to put it in.

**No `servers` block worth the name.** The document currently advertises whatever host answered the
request — `http://localhost:5080/` on a laptop. That is right for local work and wrong the moment
somebody saves the file and mails it around. Fixing it properly needs the deployed hostname, which
is a deployment concern.

**No authentication schemes**, because there is no authentication. When episode 38 adds Entra ID,
the document gains a `securitySchemes` section and every operation gains a `security` requirement —
and that is the episode where it means something rather than decorating an open API.

**No versioned document sets.** One document, named `v1` by the framework, for an API versioned by
route prefix. Episode 21 declined `Asp.Versioning` and nothing since has changed that argument.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings — and it took a version bump in step 2 to get there |
| `dotnet list package --include-transitive \| grep -i openapi` | `Microsoft.OpenApi` 2.7.5, not 2.0.0 |
| `dotnet test` | Green, unchanged. No test was added or edited this episode |
| `jq '.info.title'` | `"BrickShare Catalog API"`, not the assembly name |
| `jq '.paths \| keys \| length'` | `4`. `GET /` and the health checks are not in it |
| `jq '[.tags[].name]'` | Three tags, none of them named after the assembly |
| `jq '.components.schemas \| keys'` | No `AnonymousTypeOfstring` |
| `jq '.components.schemas.CatalogueSetRequest.description'` | The client-facing sentence — the security note is a `//` comment now |
| `jq '.paths["/api/v1/catalog/sets"].post.responses \| keys'` | `201, 400, 409, 422` |
| `jq '.components.schemas.ConditionGrade.enum'` | `New, Excellent, Good, Fair` — strings, from episode 26 |
| `ASPNETCORE_ENVIRONMENT=Production` + `curl` | The document **and** the page are served |
| `GET /scalar/v1` | The page loads, three tags become navigation, four operations under them |
| `retailPrice` and `imageUrl` on the page | Both types shown — `number \| string`, and `string \| null` on a field marked required |
| The step-5 summaries on the page | "Retire a copy", and the description that says it is not idempotent |
| `curl -s /scalar/scalar.js -o /dev/null -w '%{http_code}'` | `200`. The bundle comes from our own host, not a CDN |
| `dotnet list package --include-transitive` | `Scalar.AspNetCore`, and no Swashbuckle |
| `grep -rn "IsDevelopment" src/` | Nothing |
| `git diff --stat` | Seven files. None in `Domain/`, `Persistence/` or `tests/` |

The row to run on camera is the fourth from the bottom. **The enum values are the one part of this
document that is both derived from the code and impossible to get wrong by hand** — and they are
correct only because somebody made a deliberate wire-format decision six episodes ago, for reasons
that had nothing to do with documentation.

## Next

[Episode 33 — Themes of our own](catalog-api.md#episode-33--themes-of-our-own): a `themes` table, a
foreign key, and the first data migration in this course that moves real rows rather than creating
an empty table — plus the nesting question episode 27 deferred, which is a product decision wearing a
mapping decision's clothes.

The sentence to carry out of this one: **a generated document is only as honest as the thing that
generates it, and the interesting work is knowing which half is which.** The types were checked by a
compiler. The status codes were typed by a person. The document does not distinguish between them,
so you have to.
