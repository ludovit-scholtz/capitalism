export type GuideImageLocale = 'en' | 'sk' | 'de'

function normalizeGuideImageLocale(locale: string): GuideImageLocale {
  return locale === 'sk' || locale === 'de' ? locale : 'en'
}

export function localizeGuideImageUrl(imageUrl: string, locale: string): string {
  const pathSegments = imageUrl.split('/').filter(Boolean)
  if (pathSegments.length < 2) {
    return imageUrl
  }

  const [topicFolder, ...fileSegments] = pathSegments
  return `/${topicFolder}/${normalizeGuideImageLocale(locale)}/${fileSegments.join('/')}`
}

export const onboardingGuideCards = [
  {
    titleKey: 'encyclopedia.onboardingGuideStep1Title',
    bodyKey: 'encyclopedia.onboardingGuideStep1Body',
    imageUrl: '/onboarding-help/step-1-city.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep2Title',
    bodyKey: 'encyclopedia.onboardingGuideStep2Body',
    imageUrl: '/onboarding-help/step-2-industry.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep3Title',
    bodyKey: 'encyclopedia.onboardingGuideStep3Body',
    imageUrl: '/onboarding-help/step-3-product.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep4Title',
    bodyKey: 'encyclopedia.onboardingGuideStep4Body',
    imageUrl: '/onboarding-help/step-4-ipo.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep5Title',
    bodyKey: 'encyclopedia.onboardingGuideStep5Body',
    imageUrl: '/onboarding-help/step-5-factory-lot.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep6Title',
    bodyKey: 'encyclopedia.onboardingGuideStep6Body',
    imageUrl: '/onboarding-help/step-6-shop-lot.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep7Title',
    bodyKey: 'encyclopedia.onboardingGuideStep7Body',
    imageUrl: '/onboarding-help/step-7-save-progress.png',
  },
]

export const manufacturingGuideCards = [
  {
    titleKey: 'encyclopedia.manufacturingGuideStepPurchaseTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepPurchaseBody',
    resourceSlug: 'grain',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepManufactureTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepManufactureBody',
    productSlug: 'bread',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepStorageTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepStorageBody',
    productSlug: 'wooden-chair',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepPublicSalesTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepPublicSalesBody',
    productSlug: 'basic-medicine',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepUnitTypesTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepUnitTypesBody',
    imageUrl: '/onboarding-help/step-5-factory-lot.png',
  },
]

export const manufacturingGuideTopics = [
  'encyclopedia.manufacturingGuideTopicPurchase',
  'encyclopedia.manufacturingGuideTopicManufacturing',
  'encyclopedia.manufacturingGuideTopicStorage',
  'encyclopedia.manufacturingGuideTopicPublicSales',
  'encyclopedia.manufacturingGuideTopicUnitTypes',
]

export const salesShopGuideCards = [
  {
    titleKey: 'encyclopedia.salesShopGuideStepBuyBuildingTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepBuyBuildingBody',
    imageUrl: '/sales-shop-help/step-1-buy-sales-shop-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.salesShopGuideStepPurchaseUnitTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepPurchaseUnitBody',
    imageUrl: '/sales-shop-help/step-2-purchase-unit-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.salesShopGuideStepPublicSalesTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepPublicSalesBody',
    imageUrl: '/sales-shop-help/step-3-public-sales-unit-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.salesShopGuideStepMarketingTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepMarketingBody',
    imageUrl: '/sales-shop-help/step-4-marketing-unit-1920x1080.png',
  },
]

export const salesShopGuideTopics = [
  'encyclopedia.salesShopGuideTopicBuyBuilding',
  'encyclopedia.salesShopGuideTopicPurchaseUnit',
  'encyclopedia.salesShopGuideTopicPublicSalesUnit',
  'encyclopedia.salesShopGuideTopicMarketingUnit',
]

export const forexGuideCards = [
  {
    titleKey: 'encyclopedia.forexGuideStepSwapOverviewTitle',
    bodyKey: 'encyclopedia.forexGuideStepSwapOverviewBody',
    imageUrl: '/forex-help/step-1-swap-overview-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepSwapExecutionTitle',
    bodyKey: 'encyclopedia.forexGuideStepSwapExecutionBody',
    imageUrl: '/forex-help/step-2-quote-and-confirm-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepTransferTitle',
    bodyKey: 'encyclopedia.forexGuideStepTransferBody',
    imageUrl: '/forex-help/step-3-account-transfer-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepRatesTitle',
    bodyKey: 'encyclopedia.forexGuideStepRatesBody',
    imageUrl: '/forex-help/step-4-fx-rates-board-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepHistoryTitle',
    bodyKey: 'encyclopedia.forexGuideStepHistoryBody',
    imageUrl: '/forex-help/step-5-swap-history-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepGoldSwapTitle',
    bodyKey: 'encyclopedia.forexGuideStepGoldSwapBody',
    imageUrl: '/forex-help/step-6-gold-amm-swap-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepGoldPositionsTitle',
    bodyKey: 'encyclopedia.forexGuideStepGoldPositionsBody',
    imageUrl: '/forex-help/step-7-gold-amm-positions-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepGoldLiquidityTitle',
    bodyKey: 'encyclopedia.forexGuideStepGoldLiquidityBody',
    imageUrl: '/forex-help/step-8-gold-amm-liquidity-1920x1080.png',
  },
]

export const forexGuideTopics = [
  'encyclopedia.forexGuideTopicSwap',
  'encyclopedia.forexGuideTopicTransfer',
  'encyclopedia.forexGuideTopicRates',
  'encyclopedia.forexGuideTopicHistory',
  'encyclopedia.forexGuideTopicGoldSwap',
  'encyclopedia.forexGuideTopicGoldPositions',
  'encyclopedia.forexGuideTopicGoldLiquidity',
]

export const stockExchangeGuideCards = [
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepIpoTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepIpoBody',
    imageUrl: '/stock-exchange-help/step-1-ipo-plan-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepCompanyBuyTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepCompanyBuyBody',
    imageUrl: '/stock-exchange-help/step-2-company-buy-shares-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepPersonalBuyTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepPersonalBuyBody',
    imageUrl: '/stock-exchange-help/step-3-personal-buy-shares-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepSellTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepSellBody',
    imageUrl: '/stock-exchange-help/step-4-sell-shares-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepUsdForexTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepUsdForexBody',
    imageUrl: '/stock-exchange-help/step-5-usd-forex-swap-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepTaxLedgerTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepTaxLedgerBody',
    imageUrl: '/stock-exchange-help/step-6-tax-reserve-ledger-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepDividendConfigTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepDividendConfigBody',
    imageUrl: '/stock-exchange-help/step-7-dividend-config-company-settings-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepDividendPersonalTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepDividendPersonalBody',
    imageUrl: '/stock-exchange-help/step-8-dividend-effects-personal-account-1920x1080.png',
  },
]

export const stockExchangeGuideTopics = [
  'encyclopedia.stockExchangeGuideTopicIpo',
  'encyclopedia.stockExchangeGuideTopicCompanyBuy',
  'encyclopedia.stockExchangeGuideTopicPersonalBuy',
  'encyclopedia.stockExchangeGuideTopicSell',
  'encyclopedia.stockExchangeGuideTopicUsdForex',
  'encyclopedia.stockExchangeGuideTopicTax',
  'encyclopedia.stockExchangeGuideTopicDividendConfig',
  'encyclopedia.stockExchangeGuideTopicDividendPersonal',
]
