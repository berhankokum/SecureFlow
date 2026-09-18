#!/usr/bin/env bash
# No curl|sh and no mutable third-party action/tool tags. Review checksums on upgrades.
set -euo pipefail
case "${1:-}" in
  gitleaks)
    url='https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_linux_x64.tar.gz'
    checksum='551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb'
    binary='gitleaks'
    ;;
  trivy)
    url='https://github.com/aquasecurity/trivy/releases/download/v0.74.0/trivy_0.74.0_Linux-64bit.tar.gz'
    checksum='2ae6fe3ee734b7fdf11335663e18c75ea12dccc76062f09f164a3b0f8be4371a'
    binary='trivy'
    ;;
  *) echo 'Expected gitleaks or trivy' >&2; exit 2 ;;
esac
tool_dir="${RUNNER_TEMP:?RUNNER_TEMP must be set}/security-tools"
mkdir -p "$tool_dir"
archive="$tool_dir/$binary.tar.gz"
curl --fail --show-error --silent --location --proto '=https' --tlsv1.2 "$url" --output "$archive"
printf '%s  %s\n' "$checksum" "$archive" | sha256sum --check --strict
tar -xzf "$archive" -C "$tool_dir" "$binary"
"$tool_dir/$binary" --version
