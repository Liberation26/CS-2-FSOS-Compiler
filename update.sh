#!/usr/bin/env bash
set -euo pipefail

UPDATE_VERSION="0.2.9"
REMOTE_URL="https://github.com/Liberation26/OrynFoundry"
REPO_DIR="${ORYN_REPO_DIR:-$HOME/Dev/OrynFoundry}"
DOWNLOADS_DIR="${ORYN_DOWNLOADS_DIR:-$HOME/Downloads}"
ARCHIVE_PATH="${1:-}"
COMMIT_MESSAGE="Apply Oryn update ${UPDATE_VERSION}"
DEFAULT_BRANCH="${ORYN_GIT_BRANCH:-main}"
SELF_RESET_FLAG="${ORYN_UPDATE_SELF_RESET:-0}"

info() { printf '[ OK ] %s\n' "$1"; }
warn() { printf '[WARN] %s\n' "$1"; }
fail() { printf '[FAIL] %s\n' "$1"; exit 1; }

info "Oryn update.sh version ${UPDATE_VERSION}"
info "Target repository directory: ${REPO_DIR}"
info "Downloads directory: ${DOWNLOADS_DIR}"
info "GitHub remote: ${REMOTE_URL}"

RequireTool() {
    command -v "$1" >/dev/null 2>&1 || fail "Required tool not found: $1"
}

ScriptPath() {
    local SourcePath="${BASH_SOURCE[0]}"
    if [ -z "$SourcePath" ]; then
        fail "Could not determine update.sh path. Run update.sh as a file, not via stdin."
    fi
    if command -v realpath >/dev/null 2>&1; then
        realpath "$SourcePath"
    else
        local Dir
        Dir="$(cd "$(dirname "$SourcePath")" && pwd)"
        printf '%s/%s\n' "$Dir" "$(basename "$SourcePath")"
    fi
}

VersionKeyFromPath() {
    local FileName Major Minor Patch
    FileName="$(basename "$1")"
    if [[ "$FileName" =~ ^Oryn-([0-9]+)\.([0-9]+)\.([0-9]+)\.zip$ ]]; then
        Major="${BASH_REMATCH[1]}"
        Minor="${BASH_REMATCH[2]}"
        Patch="${BASH_REMATCH[3]}"
        printf '%010d.%010d.%010d' "$Major" "$Minor" "$Patch"
        return 0
    fi
    return 1
}

VersionTextFromPath() {
    local FileName
    FileName="$(basename "$1")"
    if [[ "$FileName" =~ ^Oryn-([0-9]+\.[0-9]+\.[0-9]+)\.zip$ ]]; then
        printf '%s\n' "${BASH_REMATCH[1]}"
        return 0
    fi
    return 1
}

FindHighestArchive() {
    if [ -n "$ARCHIVE_PATH" ]; then
        [ -f "$ARCHIVE_PATH" ] || fail "Archive not found: $ARCHIVE_PATH"
        printf '%s\n' "$ARCHIVE_PATH"
        return 0
    fi

    [ -d "$DOWNLOADS_DIR" ] || fail "Downloads directory not found: $DOWNLOADS_DIR"

    local BestPath=""
    local BestKey=""
    local Candidate Key

    while IFS= read -r -d '' Candidate; do
        if Key="$(VersionKeyFromPath "$Candidate")"; then
            if [ -z "$BestKey" ] || [[ "$Key" > "$BestKey" ]]; then
                BestKey="$Key"
                BestPath="$Candidate"
            fi
        fi
    done < <(find "$DOWNLOADS_DIR" -maxdepth 1 -type f -name 'Oryn-*.zip' -print0 2>/dev/null)

    [ -n "$BestPath" ] || fail "No versioned Oryn-x.y.z.zip archive found in: $DOWNLOADS_DIR"
    printf '%s\n' "$BestPath"
}

FindChangedFilesDir() {
    local ExtractRoot="$1"
    local Found

    Found="$(find "$ExtractRoot" -type d -name ChangedFiles | sort | head -n 1)"
    [ -n "$Found" ] || fail "ChangedFiles directory not found in extracted archive: $ExtractRoot"
    printf '%s\n' "$Found"
}

FindArchiveUpdateScript() {
    local ExtractRoot="$1"
    local Found

    Found="$(find "$ExtractRoot" -mindepth 2 -maxdepth 3 -type f -name update.sh | sort | head -n 1)"
    if [ -z "$Found" ]; then
        Found="$(find "$ExtractRoot" -type f -path '*/ChangedFiles/update.sh' | sort | head -n 1)"
    fi

    [ -n "$Found" ] || fail "update.sh not found in extracted archive: $ExtractRoot"
    printf '%s\n' "$Found"
}

ResetSelfIfDifferent() {
    local ExtractRoot="$1"
    local ArchiveUpdate CurrentScript

    ArchiveUpdate="$(FindArchiveUpdateScript "$ExtractRoot")"
    CurrentScript="$(ScriptPath)"

    if cmp -s "$ArchiveUpdate" "$CurrentScript"; then
        info "update.sh is already current."
        return 0
    fi

    if [ "$SELF_RESET_FLAG" = "1" ]; then
        warn "Different update.sh found after self-reset; continuing to avoid a reset loop."
        return 0
    fi

    info "Different update.sh found in archive. Resetting updater before applying ChangedFiles."
    cp "$ArchiveUpdate" "$CurrentScript"
    chmod +x "$CurrentScript"
    info "update.sh reset complete. Restarting updater version from archive."
    exec env ORYN_UPDATE_SELF_RESET=1 "$CurrentScript" "$ARCHIVE_PATH"
}

EnsureGitRepository() {
    mkdir -p "$(dirname "$REPO_DIR")"

    if [ -d "$REPO_DIR/.git" ]; then
        info "Existing Git repository found: $REPO_DIR"
        return 0
    fi

    if [ ! -d "$REPO_DIR" ]; then
        info "Target directory not found. Cloning into $REPO_DIR"
        git clone "$REMOTE_URL" "$REPO_DIR" || {
            warn "Clone failed. Creating a fresh repository locally instead."
            mkdir -p "$REPO_DIR"
            git -C "$REPO_DIR" init
        }
    else
        info "Target exists without .git; initialising Git in place: $REPO_DIR"
        git -C "$REPO_DIR" init
    fi

    [ -d "$REPO_DIR/.git" ] || fail "Could not initialise Git repository at: $REPO_DIR"

    if git -C "$REPO_DIR" branch -M "$DEFAULT_BRANCH" >/dev/null 2>&1; then
        info "Git branch set: $DEFAULT_BRANCH"
    else
        warn "Could not rename branch to $DEFAULT_BRANCH; continuing with current branch."
    fi
}

EnsureOriginRemote() {
    [ -d "$REPO_DIR/.git" ] || fail "Git repository is missing after setup: $REPO_DIR"

    if git -C "$REPO_DIR" remote get-url origin >/dev/null 2>&1; then
        git -C "$REPO_DIR" remote set-url origin "$REMOTE_URL"
    else
        git -C "$REPO_DIR" remote add origin "$REMOTE_URL"
    fi

    info "Git origin remote set: $REMOTE_URL"
}

EnsureGitIdentity() {
    if ! git -C "$REPO_DIR" config user.name >/dev/null 2>&1; then
        git -C "$REPO_DIR" config user.name "Oryn Update"
        info "Git user.name set locally: Oryn Update"
    fi

    if ! git -C "$REPO_DIR" config user.email >/dev/null 2>&1; then
        git -C "$REPO_DIR" config user.email "oryn-update@local"
        info "Git user.email set locally: oryn-update@local"
    fi
}

CurrentBranch() {
    local Branch
    Branch="$(git -C "$REPO_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || true)"
    if [ -z "$Branch" ] || [ "$Branch" = "HEAD" ]; then
        Branch="$DEFAULT_BRANCH"
        git -C "$REPO_DIR" branch -M "$Branch" >/dev/null 2>&1 || true
    fi
    printf '%s\n' "$Branch"
}

LaunchRunqemu() {
    local RunQemuScript="$REPO_DIR/Runqemu.sh"
    if [ -f "$RunQemuScript" ]; then
        chmod +x "$RunQemuScript" || fail "Could not mark Runqemu.sh executable: $RunQemuScript"
    fi

    if [ -x "$RunQemuScript" ]; then
        info "Launching Runqemu.sh to build and run the freestanding kernel."
        "$RunQemuScript"
    else
        fail "Runqemu.sh was not found or is not executable after update: $RunQemuScript"
    fi
}

RequireTool git
RequireTool unzip
RequireTool find
RequireTool sort
RequireTool cmp
RequireTool cp
RequireTool chmod

ARCHIVE_PATH="$(FindHighestArchive)"
ARCHIVE_VERSION="$(VersionTextFromPath "$ARCHIVE_PATH" || true)"
info "Highest Oryn archive selected: $ARCHIVE_PATH"
if [ -n "$ARCHIVE_VERSION" ]; then
    info "Archive version: $ARCHIVE_VERSION"
fi

TMP_ROOT="$(mktemp -d /tmp/oryn-update-${UPDATE_VERSION}.XXXXXX)"
cleanup() {
    rm -rf "$TMP_ROOT"
}
trap cleanup EXIT

info "Extracting archive to temporary directory: $TMP_ROOT"
unzip -q "$ARCHIVE_PATH" -d "$TMP_ROOT"

ResetSelfIfDifferent "$TMP_ROOT"

CHANGED_FILES="$(FindChangedFilesDir "$TMP_ROOT")"
info "Found ChangedFiles: $CHANGED_FILES"

EnsureGitRepository
EnsureOriginRemote
EnsureGitIdentity

CURRENT_BRANCH="$(CurrentBranch)"
info "Git branch: $CURRENT_BRANCH"

info "Copying ChangedFiles into repository root: $REPO_DIR"
mkdir -p "$REPO_DIR"
cp -a "$CHANGED_FILES"/. "$REPO_DIR"/

info "Staging repository changes"
git -C "$REPO_DIR" add -A

if git -C "$REPO_DIR" diff --cached --quiet; then
    warn "No changed files to commit."
    LaunchRunqemu
    exit 0
fi

git -C "$REPO_DIR" commit -m "$COMMIT_MESSAGE"
info "Git commit created: $COMMIT_MESSAGE"

if git -C "$REPO_DIR" push -u origin "$CURRENT_BRANCH"; then
    info "Git push completed."
else
    warn "Git push failed. The local commit still exists in: $REPO_DIR"
    warn "Check GitHub authentication, or pull/merge remote changes before pushing again."
fi

LaunchRunqemu
