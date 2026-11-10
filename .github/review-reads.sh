#!/usr/bin/env bash
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
#
# Which pull requests review.yml reads, decided on the PR's facts alone. review.yml runs this file
# as it stands on main; the tests call it the same way (NSail.Harness.Tests, ReviewReadsTests).
#
#   review-reads.sh story <branch>
#       prints the story a branch belongs to (`stack/nsail-N` or `story/nsail-N`), or nothing.
#   review-reads.sh go <event> <cross-repository> <branch> <story labels, comma-separated>
#       prints why, and exits 0 when the PR is read, 1 when it is not.
#
# The repository and the story branch are trusted, not the author: a same-repository head took
# write access to push, a fork's never did.
set -euo pipefail

story() {
  local n=""
  case "$1" in stack/nsail-*) n="${1#stack/nsail-}" ;; story/nsail-*) n="${1#story/nsail-}" ;; esac
  printf '%s' "$n" | grep -oE '^[0-9]+' || true
}

go() {
  local event="$1" cross="$2" branch="$3" labels="$4"
  if [ "$cross" != "false" ]; then
    echo "comes from a fork — never reviewed here"
    return 1
  fi
  if [ "$event" = "workflow_dispatch" ]; then
    echo "dispatched by a hand — read"
    return 0
  fi
  if [ -z "$(story "$branch")" ]; then
    echo "is not on a story branch — dispatch a read by hand if one is wanted"
    return 1
  fi
  if [[ ",$labels," == *",hand,"* ]]; then
    echo "belongs to a story a hand holds — dispatch a read by hand if one is wanted"
    return 1
  fi
  echo "is a story's — read"
  return 0
}

case "${1:-}" in
  story) story "${2:-}" ;;
  go) go "${2:-}" "${3:-}" "${4:-}" "${5:-}" ;;
  *) echo "usage: review-reads.sh story <branch> | go <event> <cross> <branch> <labels>" >&2; exit 2 ;;
esac
