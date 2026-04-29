export interface ReferralCodeSummary {
  id: string
  code: string
  createdAtUtc: string
}

export interface ReferralIdentity {
  fullName: string
  taxDomicile: string
  createdAtUtc: string
}

export interface ReferralPlayerProfile {
  appliedReferralCode: string | null
  referralIdentity: ReferralIdentity | null
  referralCodes: ReferralCodeSummary[]
  hasActiveSubscription: boolean
}

export interface ReferralDashboardRow {
  code: string
  directRegistrations: number
  secondLevelRegistrations: number
  activeSubscriptions: number
  secondLevelActiveSubscriptions: number
}

interface ReferralStorageRecord {
  players: Record<string, ReferralPlayerProfile>
}

interface ApplyReferralCodeResult {
  appliedReferralCode: string
}

const REFERRAL_STORAGE_KEY = 'master_referral_program_v1'
const REFERRAL_CODE_LENGTH = 8
const REFERRAL_CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'

function createEmptyProfile(): ReferralPlayerProfile {
  return {
    appliedReferralCode: null,
    referralIdentity: null,
    referralCodes: [],
    hasActiveSubscription: false,
  }
}

function normalizePlayerKey(rawEmail: string): string {
  return rawEmail.trim().toLowerCase()
}

function readStorage(): ReferralStorageRecord {
  if (typeof window === 'undefined') {
    return { players: {} }
  }

  const raw = window.localStorage.getItem(REFERRAL_STORAGE_KEY)
  if (!raw) {
    return { players: {} }
  }

  try {
    const parsed = JSON.parse(raw) as Partial<ReferralStorageRecord>
    return {
      players: parsed.players ?? {},
    }
  } catch {
    return { players: {} }
  }
}

function writeStorage(record: ReferralStorageRecord) {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(REFERRAL_STORAGE_KEY, JSON.stringify(record))
}

function getOrCreateProfile(record: ReferralStorageRecord, email: string): ReferralPlayerProfile {
  const key = normalizePlayerKey(email)
  const existing = record.players[key]
  if (existing) {
    return existing
  }

  const created = createEmptyProfile()
  record.players[key] = created
  return created
}

function collectAllCodes(record: ReferralStorageRecord): Set<string> {
  const allCodes = new Set<string>()
  Object.values(record.players).forEach((profile) => {
    profile.referralCodes.forEach((entry) => {
      allCodes.add(entry.code)
    })
  })
  return allCodes
}

export function createRandomReferralCode(existingCodes: Set<string>): string {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    let code = ''
    for (let index = 0; index < REFERRAL_CODE_LENGTH; index += 1) {
      const randomIndex = Math.floor(Math.random() * REFERRAL_CODE_CHARS.length)
      code += REFERRAL_CODE_CHARS[randomIndex]
    }

    if (!existingCodes.has(code)) {
      return code
    }
  }

  throw new Error('Failed to generate unique referral code. Please retry.')
}

function findOwnerByCode(record: ReferralStorageRecord, code: string): string | null {
  for (const [playerKey, profile] of Object.entries(record.players)) {
    if (profile.referralCodes.some((entry) => entry.code === code)) {
      return playerKey
    }
  }

  return null
}

export function getReferralProfile(email: string): ReferralPlayerProfile {
  const record = readStorage()
  return { ...getOrCreateProfile(record, email) }
}

export function syncReferralSubscriptionStatus(email: string, isActiveSubscription: boolean) {
  const record = readStorage()
  const profile = getOrCreateProfile(record, email)
  profile.hasActiveSubscription = isActiveSubscription
  writeStorage(record)
}

export function applyReferralCode(email: string, codeInput: string): ApplyReferralCodeResult {
  const code = codeInput.trim().toUpperCase()
  if (!/^[A-Z0-9]{8}$/.test(code)) {
    throw new Error('Referral code must be 8 alphanumeric characters.')
  }

  const record = readStorage()
  const profile = getOrCreateProfile(record, email)

  if (profile.appliedReferralCode) {
    throw new Error('Referral code has already been set and cannot be changed.')
  }

  const ownerKey = findOwnerByCode(record, code)
  if (!ownerKey) {
    throw new Error('Referral code does not exist.')
  }

  if (ownerKey === normalizePlayerKey(email)) {
    throw new Error('You cannot use your own referral code.')
  }

  profile.appliedReferralCode = code
  writeStorage(record)

  return { appliedReferralCode: code }
}

export function becomeReferral(email: string, fullNameInput: string, taxDomicileInput: string) {
  const fullName = fullNameInput.trim()
  const taxDomicile = taxDomicileInput.trim()

  if (fullName.length < 2) {
    throw new Error('Name must have at least 2 characters.')
  }

  if (taxDomicile.length < 2) {
    throw new Error('Tax domicile is required.')
  }

  const record = readStorage()
  const profile = getOrCreateProfile(record, email)

  profile.referralIdentity = {
    fullName,
    taxDomicile,
    createdAtUtc: profile.referralIdentity?.createdAtUtc ?? new Date().toISOString(),
  }

  if (profile.referralCodes.length === 0) {
    const firstCode = createRandomReferralCode(collectAllCodes(record))
    profile.referralCodes.push({
      id: crypto.randomUUID(),
      code: firstCode,
      createdAtUtc: new Date().toISOString(),
    })
  }

  writeStorage(record)
}

export function createAdditionalReferralCode(email: string): ReferralCodeSummary {
  const record = readStorage()
  const profile = getOrCreateProfile(record, email)

  if (!profile.referralIdentity) {
    throw new Error('Complete referral profile first.')
  }

  const code = createRandomReferralCode(collectAllCodes(record))
  const generated: ReferralCodeSummary = {
    id: crypto.randomUUID(),
    code,
    createdAtUtc: new Date().toISOString(),
  }

  profile.referralCodes.unshift(generated)
  writeStorage(record)

  return generated
}

export function getReferralDashboard(email: string): ReferralDashboardRow[] {
  const record = readStorage()
  const ownerKey = normalizePlayerKey(email)
  const ownerProfile = getOrCreateProfile(record, email)
  const rows: ReferralDashboardRow[] = []

  ownerProfile.referralCodes.forEach((refCode) => {
    const directUsers = Object.entries(record.players)
      .filter(([key, player]) => key !== ownerKey && player.appliedReferralCode === refCode.code)
      .map(([key, player]) => ({ key, player }))

    const directKeys = new Set(directUsers.map((entry) => entry.key))
    const directCodeSet = new Set<string>()
    directUsers.forEach((entry) => {
      entry.player.referralCodes.forEach((code) => directCodeSet.add(code.code))
    })

    const secondLevelUsers = Object.entries(record.players).filter(([key, player]) => {
      if (directKeys.has(key)) {
        return false
      }

      if (key === ownerKey) {
        return false
      }

      if (!player.appliedReferralCode) {
        return false
      }

      return directCodeSet.has(player.appliedReferralCode)
    })

    const activeSubscriptions = directUsers.filter(
      (entry) => entry.player.hasActiveSubscription,
    ).length
    const secondLevelActiveSubscriptions = secondLevelUsers.filter(
      ([, player]) => player.hasActiveSubscription,
    ).length

    rows.push({
      code: refCode.code,
      directRegistrations: directUsers.length,
      secondLevelRegistrations: secondLevelUsers.length,
      activeSubscriptions,
      secondLevelActiveSubscriptions,
    })
  })

  return rows
}
