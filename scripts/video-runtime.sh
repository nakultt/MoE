#!/usr/bin/env bash
set -euo pipefail

command_name="${1:-}"
pid_file="${2:-}"

if [[ -z "${command_name}" || -z "${pid_file}" ]]; then
  echo "Usage: video-runtime.sh <start|stop> <pid-file> [command ...]" >&2
  exit 2
fi

case "${command_name}" in
  start)
    shift 2
    if [[ "$#" -eq 0 ]]; then
      echo "A runtime command is required." >&2
      exit 2
    fi

    mkdir -p "$(dirname "${pid_file}")"
    printf '%s\n' "$$" > "${pid_file}"
    export FASTVIDEO_ATTENTION_BACKEND="TORCH_SDPA"
    export FASTVIDEO_CPU_OFFLOAD_SKIP_ROOT="1"
    export PYTORCH_CUDA_ALLOC_CONF="expandable_segments:True"
    exec "$@"
    ;;
  stop)
    if [[ -f "${pid_file}" ]]; then
      runtime_pid="$(tr -cd '0-9' < "${pid_file}")"
      if [[ -n "${runtime_pid}" ]] && kill -0 "${runtime_pid}" 2>/dev/null; then
        kill -TERM "${runtime_pid}"
      fi
      rm -f "${pid_file}"
    fi
    ;;
  *)
    echo "Unknown command: ${command_name}" >&2
    exit 2
    ;;
esac
