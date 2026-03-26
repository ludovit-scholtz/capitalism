/**
 * Shared GraphQL API mock for Capitalism V Playwright tests.
 *
 * Usage:
 *   import { setupMockApi, makePlayer } from './helpers/mock-api'
 *   test('my test', async ({ page }) => {
 *     const state = setupMockApi(page)
 *     await page.goto('/')
 *   })
 */

import type { Page } from '@playwright/test'

export type MockPlayer = {
  id: string
  email: string
  password: string
  displayName: string
  role: 'PLAYER' | 'ADMIN'
  createdAtUtc: string
  companies: MockCompany[]
}

export type MockCompany = {
  id: string
  playerId: string
  name: string
  cash: number
  foundedAtUtc: string
  buildings: MockBuilding[]
}

export type MockBuilding = {
  id: string
  companyId: string
  cityId: string
  type: string
  name: string
  latitude: number
  longitude: number
  level: number
  powerConsumption: number
  isForSale: boolean
  askingPrice?: number | null
  pricePerSqm?: number | null
  occupancyPercent?: number | null
  totalAreaSqm?: number | null
  powerPlantType?: string | null
  powerOutput?: number | null
  mediaType?: string | null
  interestRate?: number | null
  builtAtUtc?: string
  units: MockBuildingUnit[]
  pendingConfiguration: MockBuildingConfigurationPlan | null
}

export type MockBuildingUnit = {
  id: string
  buildingId: string
  unitType: string
  gridX: number
  gridY: number
  level: number
  linkUp: boolean
  linkDown: boolean
  linkLeft: boolean
  linkRight: boolean
  linkUpLeft: boolean
  linkUpRight: boolean
  linkDownLeft: boolean
  linkDownRight: boolean
}

export type MockBuildingConfigurationPlanUnit = MockBuildingUnit & {
  ticksRequired: number
  isChanged: boolean
}

export type MockBuildingConfigurationPlan = {
  id: string
  buildingId: string
  submittedAtUtc: string
  submittedAtTick: number
  appliesAtTick: number
  totalTicksRequired: number
  units: MockBuildingConfigurationPlanUnit[]
}

export type MockCity = {
  id: string
  name: string
  countryCode: string
  latitude: number
  longitude: number
  population: number
  averageRentPerSqm: number
  resources: { resourceType: { id: string; name: string; slug: string; category: string }; abundance: number }[]
}

export type MockResourceType = {
  id: string
  name: string
  slug: string
  category: string
  basePrice: number
  weightPerUnit: number
  description: string | null
}

export type MockProductType = {
  id: string
  name: string
  slug: string
  industry: string
  basePrice: number
  baseCraftTicks: number
  isProOnly: boolean
  description: string | null
  recipes: { resourceType: { id: string; name: string }; quantity: number }[]
}

export type MockState = {
  players: MockPlayer[]
  cities: MockCity[]
  resourceTypes: MockResourceType[]
  productTypes: MockProductType[]
  currentUserId: string | null
  currentToken: string | null
  gameState: { currentTick: number; tickIntervalSeconds: number; taxCycleTicks: number; taxRate: number }
}

function cloneUnit(unit: MockBuildingUnit): MockBuildingUnit {
  return { ...unit }
}

function calculateUnitTicks(currentUnit: MockBuildingUnit | undefined, nextUnit: MockBuildingUnit): number {
  if (!currentUnit) {
    return 3
  }

  if (currentUnit.unitType !== nextUnit.unitType) {
    return 3
  }

  if (
    currentUnit.linkUp !== nextUnit.linkUp
    || currentUnit.linkDown !== nextUnit.linkDown
    || currentUnit.linkLeft !== nextUnit.linkLeft
    || currentUnit.linkRight !== nextUnit.linkRight
    || currentUnit.linkUpLeft !== nextUnit.linkUpLeft
    || currentUnit.linkUpRight !== nextUnit.linkUpRight
    || currentUnit.linkDownLeft !== nextUnit.linkDownLeft
    || currentUnit.linkDownRight !== nextUnit.linkDownRight
  ) {
    return 1
  }

  return 0
}

function applyDueBuildingUpgrades(state: MockState): void {
  for (const player of state.players) {
    for (const company of player.companies) {
      for (const building of company.buildings) {
        if (building.pendingConfiguration && building.pendingConfiguration.appliesAtTick <= state.gameState.currentTick) {
          building.units = building.pendingConfiguration.units.map((unit) => cloneUnit(unit))
          building.pendingConfiguration = null
        }
      }
    }
  }
}

// ── Factory functions ────────────────────────────────────────────────────────

export function makePlayer(overrides?: Partial<MockPlayer>): MockPlayer {
  return {
    id: 'player-1',
    email: 'player@test.com',
    password: 'TestPass1!',
    displayName: 'Test Player',
    role: 'PLAYER',
    createdAtUtc: '2026-01-01T00:00:00Z',
    companies: [],
    ...overrides,
  }
}

export function makeAdminPlayer(overrides?: Partial<MockPlayer>): MockPlayer {
  return makePlayer({
    id: 'admin-1',
    email: 'admin@test.com',
    displayName: 'Admin',
    role: 'ADMIN',
    ...overrides,
  })
}

const woodResource: MockResourceType = {
  id: 'res-wood',
  name: 'Wood',
  slug: 'wood',
  category: 'ORGANIC',
  basePrice: 10,
  weightPerUnit: 5,
  description: 'Harvested timber.',
}

const grainResource: MockResourceType = {
  id: 'res-grain',
  name: 'Grain',
  slug: 'grain',
  category: 'ORGANIC',
  basePrice: 5,
  weightPerUnit: 2,
  description: 'Cereal crops.',
}

const chemResource: MockResourceType = {
  id: 'res-chem',
  name: 'Chemical Minerals',
  slug: 'chemical-minerals',
  category: 'MINERAL',
  basePrice: 30,
  weightPerUnit: 3,
  description: 'Raw minerals for pharma.',
}

export function makeDefaultResources(): MockResourceType[] {
  return [woodResource, grainResource, chemResource]
}

export function makeBratislava(): MockCity {
  return {
    id: 'city-ba',
    name: 'Bratislava',
    countryCode: 'SK',
    latitude: 48.1486,
    longitude: 17.1077,
    population: 475000,
    averageRentPerSqm: 14,
    resources: [
      { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
      { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
    ],
  }
}

export function makeDefaultCities(): MockCity[] {
  return [
    makeBratislava(),
    {
      id: 'city-pr',
      name: 'Prague',
      countryCode: 'CZ',
      latitude: 50.0755,
      longitude: 14.4378,
      population: 1350000,
      averageRentPerSqm: 18,
      resources: [
        { resourceType: { id: 'res-wood', name: 'Wood', slug: 'wood', category: 'ORGANIC' }, abundance: 0.7 },
        { resourceType: { id: 'res-grain', name: 'Grain', slug: 'grain', category: 'ORGANIC' }, abundance: 0.6 },
      ],
    },
  ]
}

export function makeChairProduct(): MockProductType {
  return {
    id: 'prod-chair',
    name: 'Wooden Chair',
    slug: 'wooden-chair',
    industry: 'FURNITURE',
    basePrice: 45,
    baseCraftTicks: 2,
    isProOnly: false,
    description: 'A basic wooden chair.',
    recipes: [{ resourceType: { id: 'res-wood', name: 'Wood' }, quantity: 3 }],
  }
}

export function makeDefaultProducts(): MockProductType[] {
  return [
    makeChairProduct(),
    {
      id: 'prod-bread',
      name: 'Bread',
      slug: 'bread',
      industry: 'FOOD_PROCESSING',
      basePrice: 3,
      baseCraftTicks: 1,
      isProOnly: false,
      description: 'Basic wheat bread.',
      recipes: [{ resourceType: { id: 'res-grain', name: 'Grain' }, quantity: 1 }],
    },
    {
      id: 'prod-medicine',
      name: 'Basic Medicine',
      slug: 'basic-medicine',
      industry: 'HEALTHCARE',
      basePrice: 50,
      baseCraftTicks: 3,
      isProOnly: false,
      description: 'Essential pharma product.',
      recipes: [{ resourceType: { id: 'res-chem', name: 'Chemical Minerals' }, quantity: 2 }],
    },
  ]
}

// ── Mock API setup ───────────────────────────────────────────────────────────

export function setupMockApi(page: Page, initial?: Partial<MockState>): MockState {
  const state: MockState = {
    players: [],
    cities: makeDefaultCities(),
    resourceTypes: makeDefaultResources(),
    productTypes: makeDefaultProducts(),
    currentUserId: null,
    currentToken: null,
    gameState: { currentTick: 42, tickIntervalSeconds: 60, taxCycleTicks: 1440, taxRate: 15 },
    ...initial,
  }

  page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON()
    const query: string = body?.query ?? ''

    // Auth token check
    const authHeader = route.request().headers()['authorization'] ?? ''
    if (authHeader.startsWith('Bearer token-')) {
      state.currentToken = authHeader.replace('Bearer ', '')
      state.currentUserId = authHeader.replace('Bearer token-', '')
    }

    // Mutations
    if (query.includes('Register')) {
      const input = body.variables?.input
      if (state.players.some((p) => p.email === input?.email)) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'A player with this email already exists.' }] }) })
      }
      const newPlayer: MockPlayer = {
        id: `player-${Date.now()}`,
        email: input.email,
        password: input.password,
        displayName: input.displayName,
        role: 'PLAYER',
        createdAtUtc: new Date().toISOString(),
        companies: [],
      }
      state.players.push(newPlayer)
      state.currentUserId = newPlayer.id
      state.currentToken = `token-${newPlayer.id}`
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            register: {
              token: `token-${newPlayer.id}`,
              expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
              player: { ...newPlayer, password: undefined },
            },
          },
        }),
      })
    }

    if (query.includes('Login')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.email === input?.email && p.password === input?.password)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Invalid email or password.' }] }) })
      }
      state.currentUserId = player.id
      state.currentToken = `token-${player.id}`
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            login: {
              token: `token-${player.id}`,
              expiresAtUtc: new Date(Date.now() + 7200000).toISOString(),
              player: { ...player, password: undefined },
            },
          },
        }),
      })
    }

    if (query.includes('CompleteOnboarding')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const product = state.productTypes.find((p) => p.id === input.productTypeId)
      const company: MockCompany = {
        id: `company-${Date.now()}`,
        playerId: player.id,
        name: input.companyName,
        cash: 500000,
        foundedAtUtc: new Date().toISOString(),
        buildings: [
          { id: `building-factory-${Date.now()}`, companyId: '', cityId: input.cityId, type: 'FACTORY', name: `${input.companyName} Factory`, latitude: 48.15, longitude: 17.11, level: 1, powerConsumption: 2, isForSale: false, builtAtUtc: new Date().toISOString(), units: [], pendingConfiguration: null },
          { id: `building-shop-${Date.now()}`, companyId: '', cityId: input.cityId, type: 'SALES_SHOP', name: `${input.companyName} Shop`, latitude: 48.15, longitude: 17.11, level: 1, powerConsumption: 1, isForSale: false, builtAtUtc: new Date().toISOString(), units: [], pendingConfiguration: null },
        ],
      }
      player.companies.push(company)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            completeOnboarding: {
              company: { id: company.id, name: company.name, cash: company.cash },
              factory: company.buildings[0],
              salesShop: company.buildings[1],
              selectedProduct: product ?? state.productTypes[0],
            },
          },
        }),
      })
    }

    if (query.includes('CreateCompany')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const company: MockCompany = {
        id: `company-${Date.now()}`,
        playerId: player.id,
        name: input.name,
        cash: 1000000,
        foundedAtUtc: new Date().toISOString(),
        buildings: [],
      }
      player.companies.push(company)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { createCompany: company } }),
      })
    }

    if (query.includes('PlaceBuilding')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      const company = player.companies.find((c) => c.id === input.companyId)
      if (!company) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Company not found' }] }) })
      }
      const city = state.cities.find((c) => c.id === input.cityId)
      const newBuilding: MockBuilding = {
        id: `building-${Date.now()}`,
        companyId: company.id,
        cityId: input.cityId,
        type: input.type,
        name: input.name,
        latitude: city?.latitude ?? 0,
        longitude: city?.longitude ?? 0,
        level: 1,
        powerConsumption: 1,
        isForSale: false,
        builtAtUtc: new Date().toISOString(),
        units: [],
        pendingConfiguration: null,
      }
      company.buildings.push(newBuilding)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { placeBuilding: newBuilding } }),
      })
    }

    if (query.includes('StoreBuildingConfiguration')) {
      const input = body.variables?.input
      const player = state.players.find((p) => p.id === state.currentUserId)
      const building = player?.companies.flatMap((company) => company.buildings).find((candidate) => candidate.id === input?.buildingId)

      if (!building) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Building not found' }] }) })
      }

      if (building.pendingConfiguration) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'This building already has an upgrade in progress.' }] }) })
      }

      const nextUnits: MockBuildingUnit[] = (input.units ?? []).map((unit: MockBuildingUnit, index: number) => ({
        id: `pending-unit-${index}-${Date.now()}`,
        buildingId: building.id,
        unitType: unit.unitType,
        gridX: unit.gridX,
        gridY: unit.gridY,
        level: building.units.find((current) => current.gridX === unit.gridX && current.gridY === unit.gridY)?.level ?? 1,
        linkUp: unit.linkUp,
        linkDown: unit.linkDown,
        linkLeft: unit.linkLeft,
        linkRight: unit.linkRight,
        linkUpLeft: unit.linkUpLeft,
        linkUpRight: unit.linkUpRight,
        linkDownLeft: unit.linkDownLeft,
        linkDownRight: unit.linkDownRight,
      }))

      const totalTicksRequired = Math.max(
        0,
        ...nextUnits.map((unit) => calculateUnitTicks(building.units.find((current) => current.gridX === unit.gridX && current.gridY === unit.gridY), unit)),
        ...building.units
          .filter((unit) => !nextUnits.find((candidate) => candidate.gridX === unit.gridX && candidate.gridY === unit.gridY))
          .map(() => 3),
      )

      building.pendingConfiguration = {
        id: `plan-${Date.now()}`,
        buildingId: building.id,
        submittedAtUtc: new Date().toISOString(),
        submittedAtTick: state.gameState.currentTick,
        appliesAtTick: state.gameState.currentTick + totalTicksRequired,
        totalTicksRequired,
        units: nextUnits.map((unit) => ({
          ...cloneUnit(unit),
          ticksRequired: calculateUnitTicks(building.units.find((current) => current.gridX === unit.gridX && current.gridY === unit.gridY), unit),
          isChanged: calculateUnitTicks(building.units.find((current) => current.gridX === unit.gridX && current.gridY === unit.gridY), unit) > 0,
        })),
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { storeBuildingConfiguration: building.pendingConfiguration } }),
      })
    }

    // Queries - order specific handlers before generic ones
    if (query.includes('starterIndustries')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: { starterIndustries: { industries: ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE'] } },
        }),
      })
    }

    if (query.includes('productTypes')) {
      const industry = body.variables?.industry
      const filtered = industry ? state.productTypes.filter((p) => p.industry === industry) : state.productTypes
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { productTypes: filtered } }),
      })
    }

    if (query.includes('myCompanies')) {
      applyDueBuildingUpgrades(state)
      const player = state.players.find((p) => p.id === state.currentUserId)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { myCompanies: player?.companies ?? [] } }),
      })
    }

    if (query.includes('rankings')) {
      const rankings = state.players
        .filter((p) => p.role !== 'ADMIN')
        .map((p) => ({
          playerId: p.id,
          displayName: p.displayName,
          totalWealth: p.companies.reduce((sum, c) => sum + c.cash, 0),
          companyCount: p.companies.length,
        }))
        .sort((a, b) => b.totalWealth - a.totalWealth)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { rankings } }),
      })
    }

    if (query.includes('gameState')) {
      applyDueBuildingUpgrades(state)
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { gameState: state.gameState } }),
      })
    }

    if (query.includes('cities')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { cities: state.cities } }),
      })
    }

    if (query.includes('resourceTypes')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { resourceTypes: state.resourceTypes } }),
      })
    }

    if (query.includes('me')) {
      const player = state.players.find((p) => p.id === state.currentUserId)
      if (!player) {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ errors: [{ message: 'Not authenticated' }] }) })
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { me: { ...player, password: undefined } } }),
      })
    }

    // Fallback
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: null }),
    })
  })

  return state
}

// ── Login helper ─────────────────────────────────────────────────────────────

export async function loginAs(page: Page, state: MockState, player: MockPlayer): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Email').fill(player.email)
  await page.getByLabel('Password').fill(player.password)
  await page.getByRole('button', { name: 'Sign In' }).click()
  await page.waitForURL('/')
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
}
