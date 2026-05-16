#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <backup-root-directory>" >&2
  exit 1
fi

backup_root="$1"

if [ ! -d "$backup_root/db" ]; then
  echo "Missing database backup directory: $backup_root/db" >&2
  exit 1
fi

while IFS= read -r -d '' dump; do
  ns="$(basename "$(dirname "$dump")")"
  pod="$(basename "$dump" .sql.gz)"

  echo "Restoring dump $dump into $ns/$pod"
  if ! kubectl get pod "$pod" -n "$ns" >/dev/null 2>&1; then
    echo "Skipping $ns/$pod (pod not found)." >&2
    continue
  fi

  if ! gzip -t "$dump"; then
    echo "Dump integrity check failed: $dump" >&2
    exit 1
  fi
  if ! gzip -cd "$dump" | kubectl exec -i -n "$ns" "$pod" -- sh -lc 'PGPASSWORD="$POSTGRES_PASSWORD" psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" "$POSTGRES_DB"'; then
    echo "Failed to decompress or restore dump: $dump" >&2
    exit 1
  fi
done < <(find "$backup_root/db" -type f -name '*.sql.gz' -print0 | sort -z)

echo "PostgreSQL restore completed."
