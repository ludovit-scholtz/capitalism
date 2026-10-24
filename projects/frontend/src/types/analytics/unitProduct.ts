/** Per-tick cost and production snapshot for a MANUFACTURING unit. */
export interface UnitProductTickSnapshot {
  tick: number
  /** Labor cost charged on this tick. */
  laborCost: number
  /** Energy cost charged on this tick. */
  energyCost: number
  /** Total operating cost (labor + energy) for this tick. */
  totalCost: number
  /** Quantity of the product produced on this tick. */
  quantityProduced: number
  /**
   * Estimated revenue = quantityProduced × product.basePrice.
   * Null when base price is unavailable.
   */
  estimatedRevenue: number | null
  /**
   * Estimated profit = estimatedRevenue − totalCost.
   * Null when base price is unavailable.
   */
  estimatedProfit: number | null
}

/**
 * Product-level analytics for a MANUFACTURING unit.
 * Shows cost history, production quantity, and estimated economics per tick.
 */
export interface UnitProductAnalytics {
  buildingUnitId: string
  /** The unit type (e.g. MANUFACTURING). */
  unitType: string
  /** Product type ID being produced. Null when no product is configured. */
  productTypeId: string | null
  /** Display name of the product being produced. Null when no product data is available. */
  productName: string | null
  /** First tick in the analytics window (oldest data). */
  dataFromTick: number
  /** Last tick in the analytics window (most recent data). */
  dataToTick: number
  /** Total labor + energy cost over the analytics window. */
  totalCost: number
  /** Total units produced over the analytics window. */
  totalQuantityProduced: number
  /**
   * Estimated total revenue = totalQuantityProduced × product.basePrice.
   * Null when base price is unavailable.
   */
  estimatedRevenue: number | null
  /**
   * Estimated total profit = estimatedRevenue − totalCost.
   * Null when base price is unavailable.
   */
  estimatedProfit: number | null
  /** Per-tick snapshots ordered by tick ascending. */
  snapshots: UnitProductTickSnapshot[]
  /** ISO 4217 currency code for the city where this unit is located (e.g. "EUR", "CZK"). */
  cityCurrencyCode: string
}

/**
 * Research brand state returned by the companyBrands query.
 * Represents a brand entity accumulated by R&D research (product quality)
 * and marketing activity (brand awareness).
 */
export interface ResearchBrandState {
  id: string
  companyId: string
  name: string
  /** PRODUCT | CATEGORY | COMPANY */
  scope: string
  productTypeId: string | null
  productName: string | null
  industryCategory: string | null
  /** 0.0–1.0: Driven by marketing unit spend. Higher = stronger brand recognition with customers. */
  awareness: number
  /** 0.0–1.0: Driven by PRODUCT_QUALITY R&D. Higher = better manufactured output quality. */
  quality: number
  /** 0.0–1.0: Accumulated from sustained marketing spend. Decays when investment stops. */
  marketingQuality: number
  /**
   * 0.0–1.0: Combined brand quality blending R&D quality and marketing prestige.
   * This is the value that amplifies sales demand (up to 50% bonus at quality = 1.0).
   */
  combinedBrandQuality: number
  /**
   * Accumulated R&D research budget (game currency) invested into this product.
   * Grows each tick by fraction of PRODUCT_QUALITY unit operating costs; decays 0.1%/tick.
   * Null when no research has been performed for this product yet.
   */
  accumulatedResearchBudget?: number | null
  /**
   * Budget required to reach 100% quality when uncontested (product base-price × 1 000, min 5 000).
   * Null when not a product-scoped brand.
   */
  baseResearchBudget?: number | null
  /**
   * Highest research budget across all companies researching this same product.
   * Your quality = your budget / max(this, baseResearchBudget).
   * Null when no research exists globally for this product.
   */
  maxCompetitorBudget?: number | null
  /**
   * ≥ 1.0: Driven by BRAND_QUALITY R&D. A value of 1.5 means each unit of marketing budget
   * produces 50% more brand awareness than baseline. Does NOT directly grant awareness.
   */
  marketingEfficiencyMultiplier: number
}
