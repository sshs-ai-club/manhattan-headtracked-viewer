#!/usr/bin/env bash
# Disposable Windows cloud test target only. No runtime or test changes.
set -euo pipefail
: "${UNITY_EXE:?Unity Build Automation must provide UNITY_EXE}"
: "${IS_BUILDER:?Run only on the disposable Unity cloud build agent}"
[[ "${IS_BUILDER,,}" == "true" ]] || { echo "Not a cloud build agent" >&2; exit 1; }
[[ "${BUILDER_OS:-}" == "WINDOWS" ]] || { echo "This bootstrap requires Windows" >&2; exit 1; }
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script_path="$(cygpath -wa "$script_dir/prepare.ps1")"
unity_path="$(cygpath -wa "$UNITY_EXE")"
powershell.exe -NoProfile -NonInteractive -File "$script_path" -UnityExecutable "$unity_path"
