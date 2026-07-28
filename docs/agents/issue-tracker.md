# Issue tracker: GitHub (multi-repo)

Issues and PRDs live as GitHub issues. Use the `gh` CLI for all operations. **Which repository** depends on what the work touches — see [Companion repos](#companion-repos).

## Conventions

- Create: `gh issue create -R <owner>/<repo> --title "..." --body "..."`
- Read: `gh issue view <number> -R <owner>/<repo> --comments`
- List: `gh issue list -R <owner>/<repo> --state open`
- Comment: `gh issue comment <number> -R <owner>/<repo> --body "..."`
- Labels: `gh issue edit <number> -R <owner>/<repo> --add-label "..."` / `--remove-label "..."`
- Close: `gh issue close <number> -R <owner>/<repo> --comment "..."`

Inside a clone, `gh` defaults to that clone's remote. **When the target is a companion repo, always pass `-R`.** Prefer full URLs in narration (`https://github.com/Skymly/Observables.Docs/issues/N`) so cross-repo links stay unambiguous.

## Companion repos

| Surface | Repository | When to open issues **there** |
|---------|------------|-------------------------------|
| Library / generators / eng / maintainer `docs/` / ADR | [Skymly/Observables](https://github.com/Skymly/Observables) | Default for mattpocock/skills flows (`/to-tickets`, `/wayfinder`, `/triage`, `/qa`, …) when the work is in this clone |
| User docs (VitePress) | [Skymly/Observables.Docs](https://github.com/Skymly/Observables.Docs) | Flow involves **user-facing Docs** (guides, `diagnostics.md`, zh pages, site config) |
| Samples / smoke demos | [Skymly/Observables.Samples](https://github.com/Skymly/Observables.Samples) | Flow involves **Samples** (`Observables.Samples.<Feature>`, RegistrationDemo / LiveDemo, samples CI) |
| Downstream showcase | [Skymly/GitPulse](https://github.com/Skymly/GitPulse) | **Only** when the work explicitly changes GitPulse; do **not** auto-file library/Docs/Samples tickets there |

### Routing rules (skills must follow)

1. **Classify each ticket** (or map) by the primary surface it delivers. Create the issue in that surface's repository via `-R`.
2. **Cross-cutting work** (e.g. new domain needs library + Docs + Samples): publish **separate issues in each involved repo**, linked in bodies (`Related: https://github.com/Skymly/Observables/issues/N`). Do **not** put Docs/Samples implementation tasks only in the library repo.
3. **Wayfinder maps** for library admission / architecture stay in **Observables**. Maps whose destination is Docs-only or Samples-only live in that companion repo.
4. **Blocking edges** across repos cannot use GitHub native dependencies; record them as URL links in the issue body (`Blocked by: https://github.com/.../issues/N`) and treat those URLs as the canonical blockers when computing the frontier.
5. **GitPulse** is a consumer showcase, not a third sync peer of the Docs/Samples trio. Library tickets mention it only when the change must be validated there; file GitPulse issues only for GitPulse code/docs work.

## Pull requests as a triage surface

PRs as a request surface: no.

## When a skill says "publish to the issue tracker"

Create a GitHub issue in the repository selected by [Companion repos](#companion-repos) / [Routing rules](#routing-rules-skills-must-follow).

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> -R <owner>/<repo> --comments` (include `-R` when the ticket is not in the current clone's remote).

## Wayfinding operations

Wayfinder maps and tickets live in GitHub Issues of the **target** repo above.

- Map: issue labelled `wayfinder:map`
- Child ticket: GitHub sub-issue, labelled `wayfinder:<type>`
- Blocking: native GitHub issue dependencies **within the same repo**; cross-repo blockers use body URL links (see routing rule 4)
- Claim: assign the ticket to `@me`
- Resolve: comment with the answer, close the issue, then update the map's Decisions so far
