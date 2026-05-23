#!/usr/bin/env bash
set -euo pipefail

VERSION="0.0.2"
REMOTE_URL="https://github.com/Liberation26/C--2-FSOS-Compiler.git"
REPO_DIR="${ORYN_REPO_DIR:-$HOME/Dev/OrynFoundry}"
DOWNLOADS_DIR="${ORYN_DOWNLOADS_DIR:-$HOME/Downloads}"
ARCHIVE_PATH="${1:-}"
COMMIT_MESSAGE="Apply Oryn update ${VERSION}"
DEFAULT_BRANCH="${ORYN_GIT_BRANCH:-main}"

info() { printf '[ OK ] %s
' "$1"; }
warn() { printf '[WARN] %s
' "$1"; }
fail() { printf '[FAIL] %s
' "$1"; exit 1; }

FindLatestArchive() {
    if [ -n "$ARCHIVE_PATH" ]; then
        [ -f "$ARCHIVE_PATH" ] || fail "Archive not found: $ARCHIVE_PATH"
        printf '%s
' "$ARCHIVE_PATH"
        return 0
    fi

    [ -d "$DOWNLOADS_DIR" ] || fail "Downloads directory not found: $DOWNLOADS_DIR"

    local Latest
    Latest="$(find "$DOWNLOADS_DIR" -maxdepth 1 -type f -name 'Oryn-*.zip' -printf '%T@ %p
' 2>/dev/null | sort -nr | cut -d' ' -f2-)"
    Latest="$(printf '%s
' "$Latest" | head -n 1)"
    [ -n "$Latest" ] || fail "No Oryn-*.zip archive found in: $DOWNLOADS_DIR"
    printf '%s
' "$Latest"
}

FindChangedFilesDir() {
    local ExtractRoot="$1"
    local Found

    Found="$(find "$ExtractRoot" -type d -name ChangedFiles | head -n 1)"
    [ -n "$Found" ] || fail "ChangedFiles directory not found in extracted archive: $ExtractRoot"
    printf '%s
' "$Found"
}

EnsureGitRepository() {
    mkdir -p "$(dirname "$REPO_DIR")"

    if [ -d "$REPO_DIR/.git" ]; then
        info "Existing Git repository found: $REPO_DIR"
        return 0
    fi

    if [ ! -d "$REPO_DIR" ]; then
        info "Target directory not found. Cloning into $REPO_DIR"
        git clone "$REMOTE_URL" "$REPO_DIR"
        return 0
    fi

    if [ -z "$(find "$REPO_DIR" -mindepth 1 -maxdepth 1 2>/dev/null | head -n 1)" ]; then
        info "Target directory is empty. Cloning into $REPO_DIR"
        rmdir "$REPO_DIR"
        git clone "$REMOTE_URL" "$REPO_DIR"
        return 0
    fi

    info "Target exists but is not a Git repository. Initialising Git in place: $REPO_DIR"
    cd "$REPO_DIR"
    git init

    if git branch -M "$DEFAULT_BRANCH" >/dev/null 2>&1; then
        info "Git branch set: $DEFAULT_BRANCH"
    else
        warn "Could not rename branch to $DEFAULT_BRANCH; continuing with current branch."
    fi
}

EnsureOriginRemote() {
    cd "$REPO_DIR"

    if git remote get-url origin >/dev/null 2>&1; then
        git remote set-url origin "$REMOTE_URL"
    else
        git remote add origin "$REMOTE_URL"
    fi

    info "Git origin remote set: $REMOTE_URL"
}

CurrentBranch() {
    local Branch
    Branch="$(git rev-parse --abbrev-ref HEAD 2>/dev/null || true)"
    if [ -z "$Branch" ] || [ "$Branch" = "HEAD" ]; then
        Branch="$DEFAULT_BRANCH"
        git branch -M "$Branch" >/dev/null 2>&1 || true
    fi
    printf '%s
' "$Branch"
}

ARCHIVE_PATH="$(FindLatestArchive)"
info "Using archive: $ARCHIVE_PATH"

TMP_ROOT="$(mktemp -d /tmp/oryn-update-${VERSION}.XXXXXX)"
cleanup() {
    rm -rf "$TMP_ROOT"
}
trap cleanup EXIT

info "Extracting archive to temporary directory: $TMP_ROOT"
unzip -q "$ARCHIVE_PATH" -d "$TMP_ROOT"

CHANGED_FILES="$(FindChangedFilesDir "$TMP_ROOT")"
info "Found ChangedFiles: $CHANGED_FILES"

EnsureGitRepository
EnsureOriginRemote

cd "$REPO_DIR"
[ -d .git ] || fail "Target directory is not a git repository after initialisation: $REPO_DIR"

CURRENT_BRANCH="$(CurrentBranch)"
info "Git branch: $CURRENT_BRANCH"

info "Copying ChangedFiles into repository root: $REPO_DIR"
cp -a "$CHANGED_FILES"/. "$REPO_DIR"/

info "Staging repository changes"
git add -A

if git diff --cached --quiet; then
    warn "No changed files to commit."
    exit 0
fi

git commit -m "$COMMIT_MESSAGE"
info "Git commit created: $COMMIT_MESSAGE"

if git push -u origin "$CURRENT_BRANCH"; then
    info "Git push completed."
else
    warn "Git push failed. The local commit still exists in: $REPO_DIR"
    warn "Check GitHub authentication, or pull/merge remote changes before pushing again."
fi
