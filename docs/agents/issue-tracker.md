# Issue tracker: GitHub

Issues and PRDs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

## Conventions

- Create: `gh issue create --title "..." --body "..."`
- Read: `gh issue view <number> --comments`
- List: `gh issue list --state open`
- Comment: `gh issue comment <number> --body "..."`
- Labels: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- Close: `gh issue close <number> --comment "..."`

Infer the repository from `git remote -v`; `gh` does this automatically inside the clone.

## Pull requests as a triage surface

PRs as a request surface: no.

## Wayfinding operations

Wayfinder maps and tickets live in GitHub Issues.

- Map: issue labelled `wayfinder:map`
- Child ticket: GitHub sub-issue, labelled `wayfinder:<type>`
- Blocking: native GitHub issue dependencies
- Claim: assign the ticket to `@me`
- Resolve: comment with the answer, close the issue, then update the map's Decisions so far
