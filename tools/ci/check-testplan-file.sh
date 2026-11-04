#!/usr/bin/env bash
# Checks one issue's test plan file against docs/testplans/TEMPLATE.md, so the owner sees at a glance what is covered
# and what is left for him. Usage: check-testplan-file.sh <file> <reviewed: yes|no>. "reviewed" means the PR has its
# section "## Review": from then on every open case is the owner's (or waits for the release candidate, or is struck
# through), and the Status line names each open owner case. One "::error" line per problem, exit 1 if any.
set -euo pipefail

awk -v file="$1" -v reviewed="$2" '
  function problem(message) { printf "::error file=%s::%s\n", file, message; failed = 1 }
  function finish_case() {
    if (id == "") return
    if (text !~ /E2E: *(yes|no|ja|nein)/) problem(id " has no \"E2E: yes\" or \"E2E: no (reason)\".")
    if (open && !struck && text !~ /· *RC([^A-Za-z]|$)/) {
      if (text ~ /\*\*Owner:\*\*/) owner_open[++owner_count] = id
      else if (reviewed == "yes") problem(id " is open after the review but is neither ticked, **Owner:**, · RC nor struck through.")
    }
    id = ""
  }

  # A case: "- [ ] **T15-2** …" or "- [x] ~~**T15-2** …~~", continued on indented lines.
  /^- \[[ xX]\] *(~~)?\*\*(T[0-9]+|G)-[0-9]+\*\*/ {
    finish_case()
    cases++
    match($0, /(T[0-9]+|G)-[0-9]+/)
    id = substr($0, RSTART, RLENGTH)
    open = ($0 ~ /^- \[ \]/)
    struck = ($0 ~ /^- \[[ xX]\] *~~/)
    text = $0
    next
  }
  id != "" && /^[ \t]+[^ \t]/ { text = text " " $0; next }
  { finish_case() }
  /^\*\*Status:\*\*/ { status = $0; status_lines++ }
  /^\| *Test *\| *(Date|Datum) *\|/ { history = 1 }

  END {
    finish_case()
    if (cases == 0) exit failed   # A "No new test cases: …" file carries only its reason.
    if (status_lines != 1) problem("needs exactly one line \"**Status:** Automated: … · QE: … · Open for the owner: …\" above the cases.")
    else if (status !~ /Automated:.*QE:.*Open for the owner:/) problem("the Status line needs its three parts \"Automated: … · QE: … · Open for the owner: …\".")
    else {
      split(status, parts, /Open for the owner:/)
      for (i = 1; i <= owner_count; i++)
        if (parts[2] !~ ("(^|[^0-9-])" owner_open[i] "([^0-9]|$)")) problem("the Status line does not name the open owner case " owner_open[i] " under \"Open for the owner:\".")
    }
    if (!history) problem("has no History table (docs/testplans/TEMPLATE.md).")
    exit failed
  }
' "$1"
