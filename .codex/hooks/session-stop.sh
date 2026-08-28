#!/bin/bash
# Claude Code Stop hook: Log session summary when Claude finishes
# Records what was worked on for audit trail and sprint tracking

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SESSION_LOG_DIR="production/session-logs"

mkdir -p "$SESSION_LOG_DIR" 2>/dev/null

# Log recent git activity from this session (check up to 8 hours for long sessions)
RECENT_COMMITS=$(git log --oneline --since="8 hours ago" 2>/dev/null)
MODIFIED_FILES=$(git diff --name-only 2>/dev/null)

# --- Record a compact state pointer on shutdown (do NOT copy the state) ---
# active.md persists across clean exits so multi-session recovery works. The
# full state remains at its canonical path; session-log.md stores only metadata.
STATE_FILE="production/session-state/active.md"
if [ -f "$STATE_FILE" ]; then
    {
        echo "## Session Checkpoint: $TIMESTAMP"
        echo "State file: $STATE_FILE"
        echo "State lines: $(wc -l < "$STATE_FILE" 2>/dev/null | tr -d ' ')"
        echo "State bytes: $(wc -c < "$STATE_FILE" 2>/dev/null | tr -d ' ')"
        if command -v sha256sum >/dev/null 2>&1; then
            echo "State SHA-256: $(sha256sum "$STATE_FILE" | awk '{print $1}')"
        elif command -v shasum >/dev/null 2>&1; then
            echo "State SHA-256: $(shasum -a 256 "$STATE_FILE" | awk '{print $1}')"
        else
            echo "State SHA-256: unavailable"
        fi
        echo "---"
        echo ""
    } >> "$SESSION_LOG_DIR/session-log.md" 2>/dev/null
fi

if [ -n "$RECENT_COMMITS" ] || [ -n "$MODIFIED_FILES" ]; then
    {
        echo "## Session End: $TIMESTAMP"
        if [ -n "$RECENT_COMMITS" ]; then
            echo "### Commits"
            echo "$RECENT_COMMITS"
        fi
        if [ -n "$MODIFIED_FILES" ]; then
            echo "### Uncommitted Changes"
            echo "$MODIFIED_FILES"
        fi
        echo "---"
        echo ""
    } >> "$SESSION_LOG_DIR/session-log.md" 2>/dev/null
fi

exit 0
