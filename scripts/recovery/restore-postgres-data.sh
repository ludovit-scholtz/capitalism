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

  gzip -cd "$dump" | kubectl exec -i -n "$ns" "$pod" -- sh -lc 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U "$POSTGRES_USER" postgres'
done < <(find "$backup_root/db" -type f -name '*.sql.gz' -print0 | sort -z)

echo "PostgreSQL restore completed."
