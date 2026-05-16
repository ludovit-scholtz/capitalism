#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <backup-root-directory>" >&2
  exit 1
fi

backup_root="$1"

if [ ! -d "$backup_root/k8s" ]; then
  echo "Missing Kubernetes backup directory: $backup_root/k8s" >&2
  exit 1
fi

if [ -f "$backup_root/k8s/namespaces.yaml" ]; then
  kubectl apply -f "$backup_root/k8s/namespaces.yaml"
fi

while IFS= read -r -d '' manifest; do
  echo "Applying manifest: $manifest"
  kubectl apply -f "$manifest"
done < <(find "$backup_root/k8s" -type f -name '*.yaml' ! -name 'namespaces.yaml' -print0 | sort -z)

echo "Kubernetes artifact restore completed."
