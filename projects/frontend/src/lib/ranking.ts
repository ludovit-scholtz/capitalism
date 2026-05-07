export function calculateRankPage(playerRank: number | null | undefined, itemsPerPage: number): number {
  if (!playerRank || playerRank < 1 || itemsPerPage < 1) {
    return 1
  }

  return Math.ceil(playerRank / itemsPerPage)
}

export function isActivePlayer(rowPlayerId: string | null | undefined, currentPlayerId: string | null): boolean {
  return !!rowPlayerId && !!currentPlayerId && rowPlayerId === currentPlayerId
}
