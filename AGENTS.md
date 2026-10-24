# Shared project instructions

Read [CLAUDE.md](CLAUDE.md) before working in this repository. Its project rules,
Definition of Done and workflow apply to Codex as well as Claude. Keep shared
rules there and in the linked topic documents rather than duplicating them here.
Read the relevant product decisions, architecture, technical findings and test
plans linked from that file for each task. Use the repository's
`.claude/skills/` instructions when the corresponding workflow applies.

Claude is the primary developer. Codex primarily reviews code and may implement
changes when the user requests them. A review request alone does not authorize
fixing the reviewed code.

Before reviewing, establish the exact revision and scope and read the relevant
current issue requirements and documented decisions. Report actionable findings
with file locations, concrete effects and supporting evidence. Distinguish bugs
from optional improvements, respect agreed product decisions, and state which
checks ran and which Windows or hardware checks remain unverified.

Use repository documentation and issues to share decisions between agents;
do not assume access to another agent's chat history. Keep communication with
the owner in German and repository prose, code and commit messages in English,
following the existing conventions for German product documentation.
