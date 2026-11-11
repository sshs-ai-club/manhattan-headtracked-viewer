# Team development guidance

**AI agents: at the start of every session, before anything else, follow [docs/team/AGENT-ONBOARDING.md](docs/team/AGENT-ONBOARDING.md). Work out who you are working for, then open your first reply by telling them their role, their paths and what is open for them.**

- The Unity project is `ManhattanViewer`, pinned to Unity 6000.3.22f1. Commit package locks, project settings and all `.meta` files for authored assets.
- Reusable tracking, projection and physical-screen behavior belongs in the separate `head-tracked-display` package. Keep its tested Git revision identical in the UPM manifest/lock and `tools/technology-version.json`.
- Keep `ManhattanBootstrap` limited to composition. Input, streaming, rendering, UI, persistence and city conversion have separate components.
- Use meters at scale 1. Preserve city coordinates separately from the floating rendering origin. Imported vertices must be tile-local and converted exactly once.
- Tracking, window frame and surface switches each change only their named feature. Tracking OFF freezes the head offset while observations and flight continue. Surface changes must be reversible and must apply to newly streamed tiles.
- Save physical calibration, viewer preferences and position bookmarks separately. Load bookmarks only after an explicit action.
- Generated city assets and expanded source packs are never committed. Verify source SHA-256 before baking; preserve provenance and source terms. Small validation and team-authored binary assets use Git LFS.
- `tools/run.ps1` delegates to the pinned technology repository's one public CMD entry point. Do not introduce project-specific tracking logic or another CMD launcher.
- Extend motion through `IMotionInputSource` and constraints through `ITranslationConstraint`. Keep optional collision proxies disabled for normal free flight.
- Run `tools/test.ps1 -DataOnly` for portable checks and `tools/test.ps1` for Unity checks. Validate a Windows player and update measured reports when changing streaming/rendering behavior; targets are not measured results.
- Do not commit builds, caches, logs, tracker environments, downloaded face models, credentials or personal calibration.
- Keep `main` runnable. Develop features on branches and use pull requests for review.

## Game team rules (for every person and every AI agent)

Full text: [docs/team/WORKFLOW.md](docs/team/WORKFLOW.md). Who owns what: [docs/team/ROLES.md](docs/team/ROLES.md). Plan: [docs/team/ROADMAP.md](docs/team/ROADMAP.md). Game: [docs/game/GDD.md](docs/game/GDD.md). Decisions: [docs/decisions/](docs/decisions/README.md).

- Never push to `main`. Work on `<area>/<short-name>` (areas: core, swing, hardware, design, character, audio, world, docs) and open a PR.
- Stay in the area you were asked to work on. Check `docs/team/ROLES.md` before editing another area's paths; ask the owner first.
- Do not edit `ManhattanViewer/Assets/Scenes/ManhattanViewer.unity`, `Packages/` or `ProjectSettings/` unless you are the Core owner. Create content from code or prefabs.
- Every asset needs a licence row and binaries go through LFS. Do not use Marvel/Sony-branded material ([decision 0002](docs/decisions/0002-name-and-ip.md)).
- Do not commit personal or tool-specific files (agent config, plans, session notes, local paths, calibration). Do not invent test results; write "not tested" instead.
- Do not merge, tag, force-push or change repository settings. A human does that.
- If a choice affects other people, write a decision record under `docs/decisions/` and do not reopen an accepted one.

## Parallel work with AI agents

Several people run agents on this repository at the same time. A **GitHub issue is the lock** on a piece of work: whoever it is assigned to holds it. These steps keep two agents off the same files.

Before writing code:

1. **Work from an issue.** Every task from [ROADMAP](docs/team/ROADMAP.md) is one issue. If the user has none for this work, draft one (area, goal, check, paths it will touch) and ask them to create it and assign themselves. Do not start until it is assigned to your user.
2. **Check the lock.** Run `gh issue view <n>` and confirm the assignee is your user. Then run `gh pr list --state open --json number,title,author,files` and look for open PRs touching the same paths. If another person's issue or open PR covers the same files, stop and tell the user who holds them. Do not work around it, copy their change or edit their branch.
3. **One issue, one branch, one PR, one agent.** Name the branch `<area>/<short-name>`. Never run two agents on the same branch or working tree; a person running agents in parallel gives each its own `git worktree` and its own issue.

While working:

4. **Announce early.** On the first commit, push and open a **draft PR** with `Closes #<n>` and a `Paths:` list of the folders and files it changes. Update the list if the scope grows. The draft PR is how other agents see what is taken.
5. **Stay inside the listed paths.** If you need a file outside them, above all another area's paths or shared files (`ROLES.md`, `WORKFLOW.md`, `AGENTS.md`, decision records, `.github/`, asmdefs, the main scene), stop and tell the user. They ask the owner or open a separate issue.
6. **Keep up with `main`.** Before asking for review, `git fetch` and merge `origin/main` into the branch, then rerun the checks. Do not rebase or force-push a branch someone else has pulled.
7. **Release the lock.** If work stops for more than 3 days, say so on the issue. If the user drops the task, unassign it and close the draft PR. An assigned issue with no commit or comment for 7 days may be asked about and reassigned by the Game design owner.

Pull requests written with an agent:

8. **Name the tool.** The PR body states the agent and model used (for example `AI: Claude Code, claude-opus-5-5`), or `AI: none`.
9. **A human reviews every PR.** The area owner from [CODEOWNERS](.github/CODEOWNERS) approves. An agent review never counts as approval, and the person who opened the PR must have read the whole diff.
10. **Fresh-context review is recommended.** Before asking for human review, a second agent with no prior context may review the diff against the issue and this file. If you do, put a short summary of its findings in the PR. It is optional.
11. **Keep PRs under about 400 changed lines**, not counting generated files, lock files and `.meta` files. Split larger work into several issues and PRs that each still build.
