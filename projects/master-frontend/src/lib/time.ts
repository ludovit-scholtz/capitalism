export function formatHeartbeatDistance(isoTimestamp: string, now = new Date()): string {
  const heartbeatAt = new Date(isoTimestamp)

  if (Number.isNaN(heartbeatAt.getTime())) {
    return 'unknown'
  }

  const deltaSeconds = Math.max(0, Math.floor((now.getTime() - heartbeatAt.getTime()) / 1000))

  if (deltaSeconds < 10) {
    return 'just now'
  }

  if (deltaSeconds < 60) {
    return `${deltaSeconds}s ago`
  }

  const minutes = Math.floor(deltaSeconds / 60)
  if (minutes < 60) {
    return `${minutes}m ago`
  }

  const hours = Math.floor(minutes / 60)
  return `${hours}h ago`
}