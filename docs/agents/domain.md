# Domain Docs

## Before exploring

Read:

- `CONTEXT.md` at the repository root, if present
- `docs/adr/` for relevant architectural decisions

If these files do not exist, proceed silently. Create `CONTEXT.md` lazily when `/domain-modeling` resolves the first domain term.

## Layout

This is a single-context repository:

/
├── CONTEXT.md
├── docs/adr/
└── source projects/

Use vocabulary from `CONTEXT.md` when it exists. If a needed concept is absent, surface it for `/domain-modeling`.

When output conflicts with an existing ADR, call out the conflict explicitly instead of silently overriding it.
