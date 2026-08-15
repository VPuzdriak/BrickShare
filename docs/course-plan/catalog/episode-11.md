# Episode 11 — Gates with teeth

← [Course plan](catalog-api.md) · Previous: [Episode 10 — Consistency and strictness](episode-10.md)

Episode 10 configured rules **on one machine**. This episode makes them fail **somebody else's
pull request**.

That is the whole reason these are two episodes and not one. Everything added yesterday is real
and enforced — as long as the person writing the code runs `dotnet build` and reads the output.
Nothing so far stops a branch being merged with a red build, because nothing so far is *required*
to be green.

**Done when** a formatting violation and an analyzer violation each fail CI, and the merge button
is greyed out until they are fixed.

## Before recording

- Episode 10 complete: `.editorconfig`, `Directory.Build.props` and `Directory.Packages.props` at
  the root, and `dotnet build` reporting zero warnings.
- Admin rights on the GitHub repository — step 4 changes repository settings.
- **Work on a branch from the start of this episode.** Step 4 makes pushing directly to `main`
  impossible, and it is much cleaner to already be on a branch when that lands than to discover it
  mid-push.

---

## Step 1 — SonarAnalyzer, as a package

Add the version to `Directory.Packages.props`:

```xml
<PackageVersion Include="SonarAnalyzer.CSharp" Version="10.32.0.713" />
```

And the reference to `Directory.Build.props` — **once, for every project in the repository,
including the ones episode 13 will add**:

```xml
  <ItemGroup>
    <PackageReference Include="SonarAnalyzer.CSharp">
      <!-- PrivateAssets=all: this is a build-time tool, not a runtime dependency.
           Without it, a project referencing this one would inherit the analyzer,
           and it would end up listed as a dependency of anything we packaged. -->
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
```

### It is an analyzer, not a service — and that distinction is the lesson

Most people meet Sonar as **SonarQube** or **SonarCloud**: a server, an account, a scanner step in
CI, and a web dashboard you open after the fact. `SonarAnalyzer.CSharp` is the same rule engine
shipped as a **Roslyn analyzer** — a NuGet package that the compiler loads. It runs in the IDE as
you type, and it runs in `dotnet build`. There is nothing to log into and nothing to keep running.

**Say what the server version actually buys**, because it is not nothing: history and trend lines,
a quality gate applied to *new* code only so a legacy codebase is not hopeless on day one, security
hotspot review with an audit trail, and one dashboard across many repositories. Those are real,
and they matter to an organisation with fifty services.

They are worth nothing here. One repository, one service, and a codebase small enough that "new
code only" and "all code" are the same set. **The governing rule of this course applies to tooling
as much as to Azure services:** if it cannot be justified by a characteristic of this workload, it
does not go in.

**And the part that matters most for a course:** the analyzer finds the problem *in the editor,
while the developer is looking at the code*. A dashboard finds it after the pull request is
already open, which is after the author has moved on. Feedback that arrives late is feedback that
gets dismissed.

### No wiring, and one restore

```bash
dotnet restore
dotnet build
```

Sonar rules default to **warning** severity, and episode 10 made warnings errors. So there is no
configuration step at all — the package is live the moment it restores. Point that out: the
`TreatWarningsAsErrors` decision from yesterday is why today's package needed one line instead of
a section.

### Two findings on a skeleton this small

```
Program.cs(22,1): error S6966: Await RunAsync instead.
Program.cs(25,22): error S1118: Add a 'protected' constructor or the 'static' keyword to
                                the class declaration.
```

**Treat these differently, on purpose.** They are the two possible responses to an analyzer, and
having one of each on screen at the same time is the most useful thing in this episode.

**S6966 is right. Take the fix.**

```csharp
await app.RunAsync();
```

`app.Run()` blocks a thread for the entire lifetime of the process, waiting on an async operation.
On a web host that is one thread out of a pool, which is why nobody notices — and it is the exact
pattern that deadlocks elsewhere in a codebase, which is why it is worth never writing. Top-level
statements support `await` directly, so the fix is one keyword and a method name.

**S1118 is wrong here. Suppress it, narrowly, with the reason written down.**

The rule says a class with no instance members should not be publicly constructible. Correct in
general. But this class exists solely so `WebApplicationFactory<Program>` can find the entry point
(episode 4), and that requires it to be public and constructible.

```csharp
// Exposes the generated entry point so WebApplicationFactory<Program> can find it in tests.
//
// S1118 wants a private constructor on a class with no instance members. This class cannot
// have one: WebApplicationFactory<Program> needs a public, constructible entry point type.
// The rule is right in general and wrong here, so it is turned off for these two lines only.
#pragma warning disable S1118
public partial class Program;
#pragma warning restore S1118
```

**Three things about that suppression, and say all three:**

1. It is **two lines wide**, not a file and not a repository. A `dotnet_diagnostic.S1118.severity
   = none` in `.editorconfig` would silence it everywhere, including in the class where it will
   one day be correct.
2. `#pragma warning restore` is not optional. Disable without restore leaks to the end of the file,
   and the next person to add code below it gets no analysis and no indication why.
3. **The comment is the point.** A suppression without a reason is indistinguishable from someone
   who could not be bothered, and six months later nobody can tell whether it is safe to remove.

`dotnet build` → `0 Warning(s), 0 Error(s)`.

---

## Step 2 — `dotnet format`, and being honest about what it adds

`dotnet format` ships with the SDK. It has three parts, and plain `dotnet format` runs all three:

| Subcommand | Fixes |
| --- | --- |
| `dotnet format whitespace` | Indentation, spacing, final newlines — the `.editorconfig` whitespace rules |
| `dotnet format style` | The `IDExxxx` code-style rules |
| `dotnet format analyzers` | Third-party analyzer diagnostics that have automatic code fixes |

`--verify-no-changes` makes it report instead of rewrite, and exit non-zero if it would have
changed anything. That is what CI runs.

### The overlap, admitted

Episode 10 set `EnforceCodeStyleInBuild`, so `style` violations already fail the build. `analyzers`
overlaps with the build too. **So what does this step actually add?**

Prove it rather than assert it. Break the indentation of one line in `Program.cs` — push it eight
spaces right and leave trailing spaces — then run both:

```bash
dotnet build
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet format --verify-no-changes
# Program.cs(9,1):  error WHITESPACE: Fix whitespace formatting. Delete 8 characters.
# Program.cs(9,75): error WHITESPACE: Fix whitespace formatting. Replace 5 characters with '\n\n'.
```

**That is the gap, and it is exactly as small as it looks.** The compiler does not care about
whitespace, so `EnforceCodeStyleInBuild` cannot catch it; a badly indented file compiles perfectly
and passes every analyzer. It is also the single most common source of noisy diffs, because the
next person's editor reformats the file on save and the pull request grows forty unrelated lines.

**Say the honest version:** "This step catches one category, and it is the least intellectually
interesting one. It is still worth a CI step, because whitespace arguments consume real review
time and this ends them permanently. Do not oversell a tool — know what it does and what it does
not."

Revert the sloppy line, or run `dotnet format` without the flag and let it fix it for you.

---

## Step 3 — `.github/workflows/ci.yml`

`deploy.yml` triggers on push to `main`. Every check it runs therefore happens **after the merge**,
which means the first person to learn that a change is broken is production.

A new file, `.github/workflows/ci.yml`:

```yaml
name: CI

# Pull requests only. Pushes to main are covered by deploy.yml, which builds and tests
# before it deploys anything.
on:
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

# One group per pull request: github.ref is refs/pull/<n>/merge here, so this only ever
# cancels an earlier run of the SAME PR, never a teammate's. A new push makes the previous
# run's answer irrelevant. Contrast deploy.yml, which uses one global group and never
# cancels, because interrupting a terraform apply is actively harmful.
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  quality:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - run: dotnet restore

      # First, because it is the fastest failure and the one that needs no thought to fix.
      # A developer whose PR is red for indentation should learn that in twenty seconds.
      - name: Verify formatting
        run: dotnet format --verify-no-changes --no-restore

      # Warnings-as-errors (episode 10) and SonarAnalyzer (step 1) both bite here.
      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release
```

### Step order is a design decision

Format, then build, then test. Each step is slower and more informative than the last, and the
cheapest failure comes first. A developer who has broken indentation *and* a test gets told about
the indentation in twenty seconds rather than after a three-minute test run — and fixing it costs
one command.

The opposite ordering is not wrong, it is just unkind. **Fail fast on the cheap thing.**

### The concurrency group is per pull request

`cancel-in-progress: true` reads alarmingly, and the first question anybody sensible asks is: *if a
colleague's pull request is building and I push to mine, do I kill their run?*

**No — and the reason is entirely in the group key.** On a `pull_request` event, `github.ref` is
`refs/pull/<number>/merge`. So the groups look like this:

```
your PR 42   →  ci-refs/pull/42/merge
their PR 43  →  ci-refs/pull/43/merge
```

Two different strings, two different groups. **`cancel-in-progress` only ever applies within a
group**, so those two runs cannot see each other. The one thing that does get cancelled is *your
own* previous run on *your own* PR, when you push a fixup before the first run has finished.

**And that is right, not merely harmless.** The superseded run is answering a question about a
commit that is no longer the head of your branch. Letting it finish occupies a runner to produce an
answer nobody will read — and, worse, can post a green check against code that is not what will be
merged.

**Show the version that would do the feared thing**, because it is one edit away:

```yaml
# WRONG — one group for the entire repository
concurrency:
  group: ci
  cancel-in-progress: true
```

With a constant key, every pull request in the repository shares one group, and each new run
cancels whichever colleague pushed first. **The group key is the whole design decision.**
`cancel-in-progress` merely says what happens to a collision; the key decides what counts as a
collision.

**Then put it beside `deploy.yml`**, which is the opposite on both settings and deliberately so:

| | `ci.yml` | `deploy.yml` |
| --- | --- | --- |
| `group` | `ci-${{ github.ref }}` — one per PR | `deploy-main` — one, globally |
| `cancel-in-progress` | `true` | `false` |

Same feature, opposite configuration, because the underlying question gets a different answer: *is a
superseded run worth finishing?* For a check on code nobody has merged, no. For a deployment, always
— episode 9's runs must **queue**, since interrupting a `terraform apply` leaves the state lease
held and the environment half-changed.

**One last thing, since step 4 is about to make this a required check:** a cancelled run reports as
`cancelled`, not `success`. A required status check stays unsatisfied, and the merge button stays
blocked until the replacement run goes green. Cancellation is not a way past the gate.

### `permissions: contents: read`, and nothing else

Compare with `deploy.yml`, which needs `id-token: write` for OIDC. This workflow touches no cloud
resources, so it gets no ability to. **Say it:** "Permissions are per workflow, and the default
should always be the smallest set that works. A CI job that can read the code and nothing else
cannot be turned into anything interesting by a malicious pull request — and pull requests come
from strangers."

---

## Step 4 — Branch protection, on camera

Everything so far still only *reports*. `ci.yml` can be red and the merge button stays green.

**Settings → Rules → Rulesets → New branch ruleset.**

- **Name:** `main`
- **Enforcement status:** Active
- **Target branches:** Include default branch
- **Restrict deletions** ✔
- **Block force pushes** ✔
- **Require a pull request before merging** ✔ — approvals required: 0
- **Require status checks to pass** ✔ → add `quality`

**Approvals set to zero, and be honest about why.** On a team it should be one or more. This is a
one-person course repository, and a rule that forces the author to approve their own pull request
teaches ceremony rather than review. The setting is there, it is the right setting for a team, and
it is turned off here for a stated reason — which is a better lesson than either silently enabling
it or silently ignoring it.

**`Block force pushes` is not decoration.** Without it, every gate above is bypassable by anyone
who can `git push --force`. A gate with a door beside it is not a gate.

**The sentence this episode exists for**, said while the ruleset page is on screen: "Until this
click, every rule in the last two episodes was something a person could choose to run. After it,
`main` cannot receive code that fails them — not by accident, not in a hurry, not at 2 a.m. That
is the entire difference between a convention and a control."

---

## Step 5 — Prove both gates

Three pull requests, in this order — two that must fail and one that must pass. Each one should be
opened, watched going red, and fixed without closing it, so the check flips to green on the same PR.

**1. Whitespace.** Indent one line wrongly, push, open a PR.

```
Program.cs(9,1): error WHITESPACE: Fix whitespace formatting. Delete 8 characters.
Error: Process completed with exit code 2.
```

The PR page shows *"Merging is blocked — required status check quality is failing."*

**2. An analyzer violation.** Something Sonar catches — an `if` returning `true`/`false`, an unused
private field, an empty `catch`:

```csharp
if (app.Environment.IsDevelopment() == true)
{
    app.MapGet("/dev", () => "dev");
}
```

```
Program.cs(9,5): error S1125: Remove the unnecessary Boolean literal
```

Format passes — the code is indented perfectly. **Build** fails. Point at which step went red and why it is a different one — the
two gates catch genuinely different things and the demo shows it rather than claims it.

**3. Clean.** Green, and the merge button is available. Merging pushes to `main`, which triggers
`deploy.yml`, which deploys — unchanged from episode 9.

**Close on that last sentence.** The delivery pipeline did not have to be modified to gain any of
this. Two workflows now exist with two jobs: one asks *should this be allowed in*, the other asks
*make what is in live*.

---

## The duplication with `deploy.yml`, admitted rather than hidden

`ci.yml` runs restore, build and test. `deploy.yml`'s `build-test` job runs restore, build and
test. Somebody will ask.

**It stays, and there is a real reason.** A pull request check does not build the commit that will
land on `main` — it builds GitHub's **merge preview**: your branch merged with the current tip of
`main`, a commit that exists only for the check. If `main` moved after your PR went green, the
merge commit is code that has never been built anywhere. `deploy.yml` building it again is not
redundancy, it is the only place that commit is ever tested.

**The DRY alternative**, worth naming so nobody thinks it was missed: extract the shared steps into
a reusable workflow with `workflow_call` and have both files call it. That is the right move at
four workflows. At two, one indirection to explain costs more than a dozen duplicated lines — and
"the deploy workflow can be read top to bottom without opening another file" has value that does
not show up in a line count.

## What this episode deliberately does not do

- **No code coverage at all** — not collected, not reported, not gated. `coverlet.collector` has
  been sitting in the test project since episode 4 and stays unused for now. There is one test in
  this repository, and any number it produced would describe a service that does nothing. **A
  measurement nobody can act on is not a measurement**, and introducing one here would teach
  students to add coverage reporting as a reflex rather than because a question needed answering.
  It arrives when there is a body of tests worth describing — after part 3, at the earliest.
- **No SonarCloud, no SonarQube.** The analyzer covers this repository's needs entirely, and a
  server would be a resource added for the syllabus rather than the workload.
- **No CodeQL, no dependency scanning, no secret scanning.** All valuable, all *security* rather
  than *quality*, and they belong in an episode that treats them as a subject instead of a
  checkbox.
- **No `terraform plan` on pull requests.** Episode 9 deferred this to "once the quality gates
  exist", and it is now possible — but a plan is only useful if someone reads it before an apply
  happens, which needs the environments and approvals from episode 30. Adding it now would produce
  a step everyone learns to scroll past, and a check nobody reads is worse than no check, because
  it looks like protection that is not there.
- **No caching of NuGet packages or build output.** The run is under two minutes. Speed is not the
  problem being solved, and cache invalidation bugs are a genuinely bad thing to introduce into the
  workflow that gates every merge.

## Verification

1. `dotnet build` locally → 0 warnings, 0 errors, with SonarAnalyzer active.
2. `dotnet format --verify-no-changes` → exits 0 and prints nothing.
3. A PR with a whitespace violation → red at the **Verify formatting** step, merge blocked.
4. A PR with a Sonar violation → red at the **Build** step, merge blocked.
5. A clean PR → green, and the merge button becomes available.
6. After merging, `deploy.yml` runs and the three URLs answer as they have since episode 3.

## Next

**Part 3 — the domain, test-first.** [Episode 12](episode-12.md) starts
the pricing rules with a failing test, and it is the first episode in this course to write a line
of business logic.

Everything from part 1 and part 2 exists so that those rules land through a loop that already
works: write it, test it, push it, it is reviewed, it is enforced, it is live. **Eleven episodes of
groundwork, and from here the groundwork never has to be revisited** — which was the claim in
episode 1, now paid for.
