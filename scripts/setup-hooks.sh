#!/bin/bash
# Setup script: Install Git hooks from scripts/hooks/ to .git/hooks/
# Run once after cloning the repository.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOKS_SOURCE="$SCRIPT_DIR/hooks"
HOOKS_TARGET="$REPO_ROOT/.git/hooks"

echo "Installing Git hooks from $HOOKS_SOURCE ..."

for hook_file in "$HOOKS_SOURCE"/*; do
    hook_name="$(basename "$hook_file")"
    target="$HOOKS_TARGET/$hook_name"

    cp "$hook_file" "$target"
    chmod +x "$target"
    echo "  Installed: $hook_name"
done

echo "Done. Git hooks are now active."
