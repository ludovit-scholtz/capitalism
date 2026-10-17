/** Canned admin diagnostics for the Operations overview mock API (tick timings and blocked access attempts). */
export function getOperationsDiagnosticsData(query: string, currentTick: number): Record<string, unknown> {
  const data: Record<string, unknown> = {}
  if (query.includes('tickPhaseTimings')) {
    Object.assign(data, {
      tickPhaseTimings: {
        sampledTicks: 12,
        lastTick: currentTick,
        lastTotalMs: 210,
        averageTotalMs: 190,
        maxTotalMs: 260,
        phaseBudgetMs: 100,
        phases: [
          { name: 'Tax', lastMs: 4, averageMs: 5, maxMs: 9, aboveBudget: false },
          { name: 'PublicSales', lastMs: 150, averageMs: 140, maxMs: 200, aboveBudget: true },
          { name: 'Manufacturing', lastMs: 40, averageMs: 35, maxMs: 90, aboveBudget: false },
        ],
      },
    })
  }
  if (query.includes('objectAccessDenials')) {
    Object.assign(data, {
      objectAccessDenials: {
        totalRecorded: 7,
        windowSize: 2,
        probeSignals: 1,
        byObjectType: [{ key: 'building', count: 2 }],
        byReason: [
          { key: 'not_found', count: 1 },
          { key: 'wrong_account_context', count: 1 },
        ],
        recent: [
          {
            occurredAtUtc: '2026-10-17T10:00:00Z',
            actorUserId: '11111111-1111-4111-8111-111111111111',
            apiKeyId: null,
            objectType: 'building',
            objectId: '22222222-2222-4222-8222-222222222222',
            reason: 'not_found',
            isProbeSignal: true,
          },
          {
            occurredAtUtc: '2026-10-17T09:00:00Z',
            actorUserId: '33333333-3333-4333-8333-333333333333',
            apiKeyId: '44444444-4444-4444-8444-444444444444',
            objectType: 'building',
            objectId: '55555555-5555-4555-8555-555555555555',
            reason: 'wrong_account_context',
            isProbeSignal: false,
          },
        ],
      },
    })
  }
  return data
}
