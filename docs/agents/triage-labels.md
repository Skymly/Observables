# Triage Labels

User-local skills speak in terms of five canonical triage roles. This file maps those roles to the **live** label strings on `Skymly/Observables`. Do not invent aliases, and do not use `question` or other GitHub defaults as substitutes.

| Role in user-local skills  | Label in our tracker | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this table. `gh issue edit --add-label` must use a name from the right-hand column; those five labels exist on this repo.

Edit the right-hand column only after creating or renaming the tracker labels so `gh label list -R Skymly/Observables` still matches.
