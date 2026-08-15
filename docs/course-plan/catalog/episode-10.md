# Episode 10 — Consistency and strictness

← [Course plan](catalog-api.md) · Previous: [Episode 9 — GitHub Actions: the loop closes](episode-9.md)

The pipeline exists. A rule introduced from here on is enforced on every push from the moment it
lands, which is the entire reason the quality gates waited nine episodes instead of arriving in
episode 2.

This episode settles three things, once, in three files at the repository root: how code is
**formatted**, how strict the **compiler** is, and where **package versions** live.

**Done when** the solution builds with zero warnings, and a deliberately sloppy line fails the
build.

## Why now and not episode 2

**A gate is only a gate if something fails because of it.**

Before there was a pipeline, `TreatWarningsAsErrors` was a local preference. A colleague with a
different IDE, a `dotnet build` with the wrong flags, or a branch nobody built could all ignore
it, and none of that would show up anywhere. After episode 9, the same property is a fact about
whether a commit can reach production.

The cost of waiting is one cleanup pass over a `Program.cs` and a single test file — close to
nothing, because the skeleton was kept deliberately empty for exactly this moment. **Say that
out loud**: the ordering only works because episodes 2–9 resisted writing features. Do this to a
codebase with 200 files and it is a week of work nobody has budgeted, which is why in practice it
never gets done at all.

---

## Step 1 — `.editorconfig`: generate it, then adjust it

Do not hand-write this file. The SDK ships a template:

```bash
dotnet new editorconfig
```

**378 lines**, at the repository root, and every one of them is a decision somebody already made
for you. The work of this step is reading them and deciding which ones to change.

> There is also `dotnet new editorconfig --empty`, which produces a three-line file. This episode
> does not use it. An empty file means writing the whole thing by hand, and a file written by hand
> is a file nobody can re-derive — the reader has no way of knowing which lines were considered and
> which were typed from memory.

### Read it before you change it

Scroll through it on camera. Six blocks, in this order:

| Block | Holds |
| --- | --- |
| Core EditorConfig options | `indent_size`, `tab_width`, `insert_final_newline` |
| .NET coding conventions (`[*.{cs,vb}]`) | `this.` qualification, `var` versus BCL types, parentheses, expression-level preferences |
| C# coding conventions (`[*.cs]`) | expression-bodied members, pattern matching, primary constructors, `using` placement |
| C# formatting rules | brace placement, indentation, ~20 `csharp_space_*` settings |
| Naming rules | 19 rules — interfaces `IPascalCase`, private fields `_camelCase`, and so on |
| Naming styles and symbol groups | the definitions those 19 rules refer to |

**The thing to say while scrolling:** this is not an editor preferences file. The C# compiler reads
it. `dotnet build` reads it. That is what makes step 2 able to turn it into build failures and
episode 11 able to turn it into a required check — and it is why thirty lines of explanation here
is proportionate.

### Then count the severities, because this is the whole point

Every style option in the file ends with a severity. Grep for them:

```bash
grep -oE ':(silent|suggestion|warning|error)$' .editorconfig | sort | uniq -c
```

```
  27 :silent
  44 :suggestion
   2 :warning
```

Two. `dotnet_style_readonly_field` and `csharp_prefer_static_local_function`. Nothing at `error`.
All 19 naming rules at `suggestion`. And:

```bash
grep -c 'dotnet_diagnostic' .editorconfig
```

```
0
```

**Sit on this for a moment, because it is the argument for everything that follows.** `silent` means
the rule is evaluated and reports nothing at all. `suggestion` shows a grey dot in an editor and is
invisible to the build. Neither is a warning, so **`TreatWarningsAsErrors` in step 2 will not touch
either of them**. 71 of 73 style rules in this file cannot fail a build, by construction.

**Say it:** "Running that command felt like configuring the project. It configured almost nothing.
That is not a criticism of the template — a default that started failing everybody's build would be
a terrible default. It just means the file the tool gives you is the *beginning* of the job."

Prove it later, in step 5, with the same sloppy code before and after.

---

### The adjustments

Twelve lines changed and four blocks added, each with a reason. Show them as a `git diff` at the
end — that is the shape of a good configuration commit, and it is only readable because the
baseline is reproducible with one command.

#### A1 — File hygiene the template does not set

```diff
  # All files
  [*]
+ charset = utf-8
+ end_of_line = lf
  indent_style = space
+ indent_size = 2
+ insert_final_newline = true
+ trim_trailing_whitespace = true
```

**Why `charset` and `insert_final_newline`.** Neither is a preference here — both fix a file that is
already in this repository. Open
`tests/BrickShare.Catalog.IntegrationTests/BrickShare.Catalog.IntegrationTests.csproj` and check it
on camera — `head -c 3 <file> | xxd` prints `efbb bf`. It carries a **UTF-8 BOM**, and it ends
**without a newline**, both left there by `dotnet new` in episode 4. Nothing in the generated
`.editorconfig` has an opinion about either.

The missing final newline is the one worth demonstrating: `git diff` prints
`\ No newline at end of file`, and the next person to append a line gets a diff that claims to
modify the last line as well as add one. It is a small permanent tax on every future review of that
file.

**Why `end_of_line = lf`, and what it costs.** The deployed artifact is a Linux container (episode
5) and CI is `ubuntu-latest` (episode 9), so LF is what the code lives as everywhere it runs.
**The honest cost:** a Windows contributor with `core.autocrlf=true` now has git and this file
disagreeing about the same bytes. That is a real friction, it is fixable with
`core.autocrlf=input`, and it is worth saying rather than discovering.

**Why `trim_trailing_whitespace`.** Trailing spaces are invisible in review and visible in every
diff. This line is what makes episode 11's `dotnet format` check catch them.

#### A2 — The template ships final newlines **off** for C#

```diff
  [*.cs]

  #### Core EditorConfig Options ####

  # Indentation and spacing
  indent_size = 4
  tab_width = 4

- # New line preferences
- insert_final_newline = false
```

**Why.** This is the one genuinely surprising default in the file, and it is easy to miss because it
sits under an innocuous heading. `[*.cs]` is more specific than `[*]`, so it wins — leaving it in
place would silently cancel the `insert_final_newline = true` just added, **for C# files only**,
which is the one file type it matters most for. Delete the two lines and let `[*]` govern.

**The transferable lesson:** in `.editorconfig`, a later and more specific section always beats an
earlier one. Adding a rule at the top does not mean it applies.

#### A3 — File types this repository has and the template does not cover

```diff
  # Xml files
  [*.xml]
  indent_size = 2

+ # Xml project and config files
+ [*.{csproj,props,targets,slnx}]
+ indent_size = 2
+
+ [*.json]
+ indent_size = 2
+
+ [*.{yml,yaml}]
+ indent_size = 2
+
+ [*.{tf,tfvars}]
+ indent_size = 2
```

**Why.** `[*.xml]` does **not** match `.csproj` — glob patterns match the literal extension, and a
project file's extension is `csproj`. So today nothing in this repository governs
`BrickShare.slnx`, either `.csproj`, the two `.props` files being added in this episode,
`docker-compose.yml`, `.github/workflows/deploy.yml` or `infra/main.tf`. That is most of the
repository.

`indent_size = 2` in `[*]` covers them all already; these sections are here so the intent is
explicit and so a future `[*]` change does not silently reformat Terraform.

#### B1 — Three style rules raised to `warning`

```diff
- csharp_prefer_braces = true:silent
+ csharp_prefer_braces = true:warning

- csharp_style_namespace_declarations = file_scoped:suggestion
+ csharp_style_namespace_declarations = file_scoped:warning

- csharp_using_directive_placement = outside_namespace:silent
+ csharp_using_directive_placement = outside_namespace:warning
```

**Why braces (`IDE0011`).** A braceless `if` body is one statement. Add a second, indent it to
match, and it runs unconditionally while looking conditional. This is a **correctness** rule wearing
a layout rule's clothes — the 2014 `goto fail` TLS bug is the famous instance and it was exactly
this shape. The template ships it `silent`, which means it does not even show in an editor.

**Why file-scoped namespaces (`IDE0161`).** Purely mechanical, no aesthetic argument on either side,
and the alternative is that a codebase ends up with both shapes and every new file is a small
decision. There is nothing to discuss, so there is no reason to leave it as advice.

**Why `using` placement (`IDE0065`).** Usings inside a namespace resolve differently from usings
outside one. Two placements in one codebase means two mental models of what is in scope, and the
difference only bites when a name is ambiguous — which is the worst time to be surprised.

#### B2 — Unused usings need a diagnostic entry, not a style option

```diff
+ #### Analyzer severities not covered by a style option ####
+ [*.cs]
+
+ dotnet_diagnostic.IDE0005.severity = warning
```

**Why a different syntax.** There is no `csharp_style_...` option for unused usings — the rule is
reachable only by ID. **Both syntaxes are severity settings and they are not interchangeable**, and
this is the single most confusing thing about `.editorconfig`:

- `csharp_prefer_braces = true:warning` — an **option** (what to prefer) with a severity attached.
- `dotnet_diagnostic.IDE0011.severity = warning` — the **diagnostic's** severity, no preference
  involved.

Where both exist, `dotnet_diagnostic` wins.

**And this one has a prerequisite** that step 2 is about to trip over: `IDE0005` will not run during
a build unless `GenerateDocumentationFile` is on. Leave that to fail on camera in a moment rather
than pre-empting it.

#### B3 — Naming: raise the rule *and* the analyzer

```diff
- dotnet_naming_rule.private_fields_should_be__camelcase.severity = suggestion
+ dotnet_naming_rule.private_fields_should_be__camelcase.severity = warning
```

```diff
  dotnet_diagnostic.IDE0005.severity = warning
+ dotnet_diagnostic.IDE1006.severity = warning
```

**Why the rule.** Naming is what drifts first, because it is the only convention that shows up in
every single line somebody writes. Note that nothing had to be *authored* here — the template
already defines the rule, the `private_fields` symbol group and the `_camelcase` style. Only the
severity changed. **Read the generated file before writing anything: most of what a team wants is
already in it, switched off.**

**Why the second line, and this is worth the time.** Raising `dotnet_naming_rule.*.severity` **is
not enough**. All naming violations surface through one analyzer, `IDE1006`, and that analyzer's own
severity is what the build honours. Prove it — a class with a badly named private field:

```csharp
public sealed class Holder<Item>
{
    private readonly string badField = "x";

    public string Bad() => badField;
}
```

With only the naming rule raised:

```
0 Warning(s)
```

Adding `dotnet_diagnostic.IDE1006.severity = warning`:

```
error IDE1006: Naming rule violation: Missing prefix: '_'
error IDE1006: Naming rule violation: Missing prefix: 'T'
```

**Say it plainly:** "A large number of teams have nineteen naming rules in their `.editorconfig`,
have raised the severity on the ones they care about, and enforce none of them. It looks configured.
Nothing is checking. This one line is the difference, and there is no error message anywhere telling
you it is missing — which is why we ran the experiment instead of trusting the file."

#### C — One rule turned off, narrowly

```diff
+ #### Test code ####
+ [tests/**/*.cs]
+
+ dotnet_diagnostic.CA1707.severity = none
```

**Why.** Without it the build fails:

```
LivenessTests.cs(9,23): error CA1707: Remove the underscores from member name
BrickShare.Catalog.IntegrationTests.HealthChecks.LivenessTests.Live_returns_ok()
```

`CA1707` — no underscores in member names — is right. A public API method called `Get_user_by_id`
is wrong. But episode 4 chose `Live_returns_ok` deliberately, because a test name should read as a
sentence describing behaviour, and that convention is worth more in the test project than the rule
is.

Two ways out. Turn `CA1707` off everywhere, and trade a real rule for a convenience. Or turn it off
**where the exception applies** and keep it in production code, which is what the path-scoped
section does.

**The general lesson, and it is the most transferable thing in this episode:** when a rule is wrong,
narrow it before you delete it. Sections cost one header line. And note this block sits at the
**bottom** of the file — by A2's rule, later and more specific wins.

#### D — A bug in the generated file

```diff
- dotnet_naming_symbols.type_parameters.applicable_kinds = namespace
+ dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter
```

**Why.** Read the three lines around it. The symbol group named `type_parameters` — the one the rule
"type parameters should be `TPascalCase`" points at — is defined as matching **namespaces**. As
shipped, that rule does not apply to type parameters at all.

**Be honest about the impact, which is small.** Nobody noticed because every naming rule in the file
is a `suggestion` that never speaks, and because `CA1715` — a different rule, from the analyzer
family, on by default — already prefixes type parameters with `T`:

```
error CA1715: Prefix generic type parameter name Item with 'T'
```

**So why fix it?** Because the file is going to be read by everyone who joins, and a line that says
one thing and does another is worse than a line that is absent. And because of what it demonstrates:
**a generated file is a starting position, not scripture.** This one has been shipping this line for
years. If a student takes one habit from this episode, it should be reading what the tool produced
rather than trusting that a Microsoft template must be right.

#### E — One change that is only taste, labelled as such

```diff
- dotnet_separate_import_directive_groups = true
+ dotnet_separate_import_directive_groups = false
```

**Why.** It inserts a blank line between `System.*` usings and the rest. With `ImplicitUsings`
enabled there are two or three usings in a typical file, so the grouping separates almost nothing
and mostly produces churn when one is added or removed.

**And say the quiet part:** this one is a preference, not correctness. Everything above had an
argument that would survive a disagreement. This one is "I find it tidier." Marking the difference
matters — a configuration where the taste changes and the correctness changes are indistinguishable
is one nobody can safely revisit.

---

### What is deliberately left alone

Not editing is also a decision, and these were all considered:

- **Allman braces** (`csharp_new_line_before_open_brace = all`) and the entire `csharp_space_*`
  block — 30-odd settings that are the .NET convention. There is no upside to differing, and a
  codebase that formats like every other C# codebase is one strangers can read.
- **`var` preferences**, expression-bodied members, pattern matching, collection expressions — a
  little over sixty rules, left at `silent` or `suggestion` on purpose. They are visible in the IDE,
  which is where taste belongs. **A rule that fails a build should be one nobody argues about**;
  promoting these would mean defending each of them in code review forever, and the first time one
  is defended badly the whole file loses authority.
- **`file_header_template = unset`** — this is where a licence header would go if the project needed
  one. It does not.
- **The `[*.{cs,vb}]` section headers.** This is a C#-only repository and narrowing them to `[*.cs]`
  would change no behaviour whatsoever. Editing generated structure for no gain makes the file
  harder to re-derive against a fresh `dotnet new editorconfig`, which is the only way anyone will
  ever audit it.

---

## Step 2 — `Directory.Build.props`

Look at the two `.csproj` files first. They share `TargetFramework`, `Nullable` and
`ImplicitUsings` — three properties, duplicated, with nothing keeping them in step. The third
project, in episode 13, would make it three copies.

Create `Directory.Build.props` at the root. MSBuild imports it automatically into **every**
project under it, including ones that do not exist yet.

```xml
<Project>

  <PropertyGroup>
    <!-- Was duplicated in both .csproj files. One place now, and the next project inherits it. -->
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- The headline. A warning nobody has to fix is a warning nobody fixes. -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>

    <!-- Makes the IDExxxx style rules from .editorconfig run during `dotnet build`,
         not only inside an editor. Without this, .editorconfig is advice. -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

    <!-- Which analyzers run, and how many of them. See the note below on why not `All`. -->
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>Recommended</AnalysisMode>

    <!-- Required by IDE0005 (unused usings) to run on build at all — a genuine
         Roslyn quirk, not a preference. See step 4. -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

</Project>
```

### `TreatWarningsAsErrors` — the argument, in one paragraph

A build with three warnings is a build somebody reads. A build with three hundred is a build
nobody reads, and the three that matter are in there somewhere. There is no gradual version of
this: warnings accumulate monotonically, because the only force that removes them is somebody
deciding to spend a day on it, and that day never comes.

**Say this out loud:** "The cure is to never let the first one survive. Not to clean them up
periodically — periodically means never. This property is what makes 'never the first one' the
default rather than a discipline."

### Why `AnalysisMode=Recommended` and not `All`

Try `All` on camera and show what happens. On this codebase — one endpoint, two health checks,
one test — it adds exactly two warnings, and both are instructive:

```
Program.cs(25,22): warning CA1515: Because an application's API isn't typically
referenced from outside the assembly, types can be made internal

LivenessTests.cs(13,30): warning CA2234: Modify 'LivenessTests.Live_returns_ok()' to
call 'HttpClient.GetAsync(Uri)' instead of 'HttpClient.GetAsync(string)'
```

`CA1515` wants `public partial class Program` made internal — which would break
`WebApplicationFactory<Program>` and the only test in the repository. `CA2234` is a defensible
preference about `Uri` overloads that most teams do not share.

Neither is *wrong* exactly. Both are contested. And that is the problem: **`All` starts the
project with a suppression file**, and a team that writes suppressions in week one writes them
forever. `Recommended` is the set Microsoft is willing to defend as broadly correct, and it is a
level a team can actually hold at zero.

**The honest framing:** this is a judgement call, not a rule. A team with strong opinions and the
appetite to curate can run `All` and win. Choosing it by default, without that appetite, produces
a codebase where every third file starts with a `#pragma` and nobody remembers why.

---

## Step 3 — `Directory.Packages.props`

Create it at the root:

```xml
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

</Project>
```

Note the element name: `PackageVersion` here, `PackageReference` in the project. **A project still
declares what it uses; it no longer declares which version.** Those are two different decisions
and they belong to two different people — the developer adding a dependency, and whoever owns
upgrades.

**The failure this prevents** is worth describing concretely, because with two projects it looks
like paperwork. Two projects reference `Npgsql`, one at 8.0.0 and one at 9.0.0. It compiles.
NuGet resolves one of them at runtime, and which one depends on the dependency graph. The
resulting bug appears in the project that did *not* get the version it asked for, is invisible in
its `.csproj`, and reproduces on some machines and not others. Central package management makes
that state unrepresentable.

**Second payoff, and this is the one that shows up weekly:** a version bump is now a one-line
diff in one reviewable file, rather than a search-and-replace across every project.

---

## Step 4 — The cleanup pass, on camera

Now delete what has moved. The API project:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web" />
```

That is the whole file. Every property it used to hold is inherited.

The test project keeps only what is genuinely its own, and the `PackageReference` items lose
their `Version` attributes:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj" />
  </ItemGroup>

</Project>
```

**And this is the file adjustment A1 was written for.** Save it, and the editor strips the UTF-8
BOM and adds the trailing newline that `dotnet new` left off in episode 4. Two invisible changes in
the same commit as a visible one — worth naming on screen so nobody reviewing the diff later
wonders where they came from.

### Now build, and get the errors

```bash
dotnet build
```

Two failures, and they arrive in sequence — fix the first, rebuild, meet the second. Both are
worth the time.

**Failure 1 — the Roslyn quirk:**

```
CSC : error EnableGenerateDocumentationFile: Set MSBuild property 'GenerateDocumentationFile'
to 'true' in project file to enable IDE0005 (Remove unnecessary usings/imports) on build
```

`IDE0005` cannot run during a build unless the compiler is also producing an XML documentation
file. There is no principled reason for this; it is a known Roslyn implementation detail
(`dotnet/roslyn#41640`) and the error message, unusually, tells you exactly what to do.

**Failure 2 — what turning that on costs:**

```
Program.cs(25,22): error CS1591: Missing XML comment for publicly visible type or member 'Program'
```

`GenerateDocumentationFile` also switches on `CS1591` — *every public member must have an XML
comment*. That is a correct rule for a NuGet package other people consume, and pure noise for an
application whose only public type is a test hook. Hence:

```xml
<NoWarn>$(NoWarn);CS1591</NoWarn>
```

**Note the `$(NoWarn);` prefix and do not skip past it.** It appends to whatever `NoWarn` already
holds rather than replacing it. Writing `<NoWarn>CS1591</NoWarn>` silently discards suppressions
set by the SDK or by a project further down, and the resulting build errors will look like they
came from nowhere. This is the standard shape for **any** MSBuild property that holds a list.

**And say what just happened, because it is the honest version of the lesson:** the first strict
setting in this episode cost two more settings to make workable. Strictness is not free, the
price is usually paid in configuration like this, and a course that showed only the clean final
file would be hiding the part students will actually hit.

Build again. `0 Warning(s), 0 Errors(s)`, and `dotnet test` still passes its one test.

---

## Step 5 — Prove the gate bites, against the file we started from

Step 1 claimed that `dotnet new editorconfig` on its own enforces almost nothing. Do not leave that
as a claim. Write some genuinely sloppy code — an unused using and a braceless `if`:

```csharp
using System.Text.Json;

// ... later ...

if (app.Environment.IsDevelopment())
    Console.WriteLine("Development mode");
```

Now run the build **twice**, against two versions of the same file.

**With the pristine generated `.editorconfig`** — `git stash` the adjustments, or keep a copy:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**With the adjusted one:**

```
Program.cs(1,1): error IDE0005: Using directive is unnecessary.
Program.cs(9,5): error IDE0011: Add braces to 'if' statement.
    2 Error(s)
```

**Say it while both are on screen:** "Same code. Same compiler. Same command. The file the SDK
generated had opinions about both of those lines and reported neither, because both were `silent`.
Twelve changed lines later they are build failures, and in the next episode they are failures on
somebody else's pull request. That progression — preference, then local rule, then enforced gate —
is the shape of every quality control worth having, and the thing that moves you along it is
severity, not adding rules."

Revert the sloppy code, rebuild green, commit and push. The pipeline from episode 9 runs the same
three jobs and stays green, because the code was already clean — but from this commit on it is
`dotnet build` in CI saying so, not us.

---

## What this episode deliberately does not do

- **No StyleCop, no SonarAnalyzer, no analyzer packages at all.** Episode 10 is the SDK's own
  analyzers and nothing else, so that when episode 11 adds a third-party package the addition is
  visibly *new* rather than more of the same. Mixing them here would blur which findings came from
  where, which is exactly the confusion that makes people turn analyzers off.
- **No `Directory.Build.targets`.** `.props` is imported before the project, `.targets` after —
  and nothing here needs to run after. The file is added when something requires it, which in this
  repository is nothing yet.
- **No `.globalconfig`.** It does much of what `.editorconfig` does, without the
  path-based sections that step 1 depends on. Two files with overlapping authority is a debugging
  problem waiting to happen.
- **No CI changes.** Episode 9's pipeline already runs `dotnet build`, so it already enforces
  everything added today, and it did not have to be touched. **That is worth pointing at**: the
  pipeline was built once and now absorbs a new rule for free. Episode 11 adds to it only where it
  genuinely cannot cover the ground — which turns out to be a smaller gap than most people expect.

## Verification

```bash
dotnet build       # 0 warnings, 0 errors
dotnet test        # 1 passed
```

Then, to see each gate work:

1. Add an unused `using` → `error IDE0005`. Remove it.
2. Add a `Version="..."` attribute back onto a `PackageReference` → restore refuses it:

   ```
   error NU1008: The following PackageReference items cannot define a value for Version: xunit.
   Projects using Central Package Management must define a Version value on a PackageVersion item.
   ```

   Remove it.
3. Delete `TargetFramework` from `Directory.Build.props` → the **solution** fails to restore with
   `error : Invalid framework identifier ''`. An ugly message, and worth showing anyway: one line
   in one file now governs both projects, and neither `.csproj` mentions a framework any more.

And one check on the `.editorconfig` itself, which is the reason it was generated rather than
written:

```bash
dotnet new editorconfig -o /tmp/baseline
diff /tmp/baseline/.editorconfig .editorconfig
```

Everything this episode decided, in one screen, against a baseline anybody can reproduce with one
command. **That is the argument for generating it**: a hand-written file has nothing to diff
against, so every line in it is equally unexplained a year later.

## Next

[Episode 11 — Gates with teeth](episode-11.md): SonarAnalyzer as a Roslyn analyzer,
`dotnet format --verify-no-changes` as a CI step, and branch protection, so a red check stops a
merge instead of merely reporting one. Everything configured
today applies on **this** machine. The next episode makes it apply to everybody.
