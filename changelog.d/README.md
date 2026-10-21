# Changelog fragments

Changelog entries for unreleased work live here, one file per issue, not as
edits to `CHANGELOG.md`. At release the fragments are assembled into one
version section of `CHANGELOG.md` and deleted.

## Why fragments

Work happens in one worktree per issue, so several branches are usually open
at once. If each appended to the top of `CHANGELOG.md`, every branch would
touch the same lines of the same file and nearly every pull request would
conflict with every other. A fragment is named for its issue, so no two
branches write the same file.

## When a change needs a fragment

The changelog serves people who use the package. Write a fragment for a
change a user of the API could notice:

- a public type, method, property, overload or error code added, changed or
  removed
- a change in observable behavior, including what an id looks like and what
  generation, decoding or the validity checks accept or refuse
- a change in the supported target frameworks or in the package's
  dependencies
- a bug fix a user could have noticed
- anything breaking

Write none for tests, documentation, tooling, CI, the release workflow or gate
changes. If you are unsure, ask whether someone who only calls the public API
could tell the difference; if not, skip it.

## Format

One file per issue, named for its beads issue id, in the same pull request as
the change:

    changelog.d/uxcs-abc.md

The file holds a Keep a Changelog kind heading and one bullet per change:

```markdown
### Added

- `Uxid.IsValid` answers whether a string is a well-formed uxid.
```

- The kind headings are `### Added`, `### Changed`, `### Deprecated`,
  `### Removed`, `### Fixed` and `### Security`. A file may carry more than
  one when the issue genuinely spans them.
- Bullets are `- `, one per change, in the present tense, saying what the
  change means to a user. No nested bullets: detail belongs in the pull
  request and the commit body.
- A breaking change opens its bullet with `Breaking:` and says what a caller
  does about it, not only what broke.
- A change in the vendored conformance corpus that changes an id this package
  produces or accepts is visible to every consumer that stores ids: say what
  changes on the consumer's side.
- Examples stay domain-free: neutral resource prefixes such as `usr` or
  `cus`, and fictional `@example.com` addresses.

## At release

The release prep assembles the fragments into a new version section at the
top of `CHANGELOG.md`, below the file's title, headed
`## X.Y.Z - YYYY-MM-DD` at the level of the file's `## Unreleased` heading.
The entries are grouped under `### Added`, `### Changed`, `### Deprecated`,
`### Removed`, `### Fixed` and `### Security`, in that order, each kind
present only when a fragment carries it; within one kind, fragments go in
file-name order and each fragment's bullets keep their order and their `- `
marker. A `Breaking:` bullet keeps that word. The sections already in
`CHANGELOG.md` are not rewritten. The fragments are deleted in the same
commit.

The prep (the `<Version>` bump in `src/Riddler.Uxid/Riddler.Uxid.csproj`, the
promoted section and the deleted fragments) merges on its own, and then its
merged commit is tagged `vX.Y.Z`. The release workflow,
`.github/workflows/release.yml`, publishes on that tag push (CLAUDE.md,
"Release preps"). Because the prep consumes every fragment, it merges last: a
branch still holding an unmerged fragment when the prep lands waits for the
next release.
