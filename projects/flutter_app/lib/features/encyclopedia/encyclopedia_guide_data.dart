// Static guide content for the Encyclopedia's 5 topic tabs, ported from
// `projects/frontend/src/lib/encyclopediaGuideData.ts` +
// `projects/frontend/src/i18n/locales/en.ts` (the `encyclopedia.*Guide*`
// keys) — English text only, matching this app's existing convention of not
// running most UI copy through per-language translation (see CLAUDE.md's
// Flutter section). Deliberately trimmed from the web: no screenshot
// images — the web's `/onboarding-help/*.png` etc. guide images aren't
// bundled as Flutter assets, so cards are text-only reference cards rather
// than illustrated walkthroughs.
library;

class EncyclopediaGuideCard {
  const EncyclopediaGuideCard({required this.title, required this.body});

  final String title;
  final String body;
}

class EncyclopediaGuideTopic {
  const EncyclopediaGuideTopic({
    required this.slug,
    required this.navLabel,
    required this.title,
    required this.subtitle,
    this.topicsHeading,
    this.topics = const [],
    required this.cards,
  });

  final String slug;
  final String navLabel;
  final String title;
  final String subtitle;
  final String? topicsHeading;
  final List<String> topics;
  final List<EncyclopediaGuideCard> cards;
}

const onboardingGuideTopic = EncyclopediaGuideTopic(
  slug: 'onboarding-help',
  navLabel: 'Onboarding help',
  title: 'Onboarding Help',
  subtitle: 'A complete walkthrough of every onboarding decision from city selection to saved account progress.',
  cards: [
    EncyclopediaGuideCard(
      title: 'Step 1 - Choose your city',
      body:
          'City choice comes first now because it fixes your starting currency, active account context, and available lot market before you commit to an industry. Treat this as an operating-environment choice, not just a cosmetic location pick. Your city determines the currency used for IPO funding, land prices, and the first bank-account context you will manage. Pick a city whose scale and familiarity help you learn quickly, then keep the later choices consistent with that local market. This first decision anchors every later onboarding screen, so make it deliberately and read the city card as a business setup summary rather than a decorative label.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 2 - Choose your industry',
      body:
          'Once the city is fixed, choose the business fantasy you want to learn first: Furniture, Food Processing, or Healthcare. This step controls your first supply chain, your starter product pool, and the economic logic you need to understand during the first few ticks. Do not optimize for abstract difficulty alone; choose the industry whose production story you can reason about clearly. A good onboarding industry is one where you can explain to yourself what gets bought, what gets produced, and what will be sold before you click forward. The more clearly you understand that chain now, the faster your first pricing and expansion decisions will become evidence-based later.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 3 - Choose your first product',
      body:
          'Product selection turns your industry fantasy into a concrete launch plan. The chosen starter product drives the factory configuration, the shop setup, the benchmark price shown in the guide, and the first material flow you need to keep alive. Read the product card as a commitment to a specific operating loop, not as a flavor choice. Your goal is not to find a perfect forever product; it is to pick a clean first chain that helps you learn sourcing, production, and sales without unnecessary confusion. Once selected, use the rest of the onboarding steps to build a company around that one clear commercial path.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 4 - Choose IPO plan',
      body:
          'This screen balances control against runway. A lower raise preserves founder ownership but leaves less working capital for lot purchases, unit setup, and early operating mistakes. A higher raise gives stronger liquidity and more room to recover from poor pricing or temporary stock imbalances, but it dilutes your founder stake. There is no universally best option; the right choice depends on your confidence and play style. If this is your first run, extra cash can reduce stress and help you learn faster. If you already know the setup sequence, a tighter raise can keep ownership stronger. Read the ownership and public-float values as strategic levers, not just numbers, because they shape both your short-term safety and long-term wealth trajectory.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 5 - Buy first factory lot',
      body:
          'Now you commit to physical production by purchasing your first factory-capable lot. This decision converts your strategy into an operational footprint and immediately affects your remaining cash buffer. Do not optimize for prestige or district name; optimize for survivability across the next setup steps. You still need enough money for your sales shop lot and for basic operating pressure after activation. Use the lot list or map to compare options quickly, then choose a location that keeps your launch plan financially flexible. A slightly cheaper lot often creates a better first week because you can absorb sourcing costs and adjust configuration without panic. Treat this purchase as the anchor of your supply chain, not as an isolated real-estate decision.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 6 - Buy first sales-shop lot',
      body:
          'This final onboarding purchase opens your go-to-market path. The shop lot is where production becomes revenue, so this step marks the transition from setup to live execution. After buying the lot, your next priority is clean PUBLIC_SALES configuration: set a realistic minimum price, ensure inventory can arrive from upstream flow, and watch the next ticks for your first real sale record. Early pricing should favor learning and turnover, not maximum margin, because fast feedback helps you correct issues sooner. Once the first sales appear, you can tighten margins and test demand elasticity with controlled changes. If this step is handled well, your company enters a stable loop where each tick produces data you can use to compound growth.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 7 - Save progress to your account',
      body:
          'The final guest step is now explicit: save the prepared company into your real account. When you press Save and Launch, Biatec must be allowed to create files in your Google Drive because that permission is used to create and manage the wallet file securing your account. If you deny that permission, authentication cannot finish correctly and you will need to retry sign-in with consent again. Read this screen carefully, confirm what progress will be preserved, and grant the Drive permission when Google asks. This is the handoff from sandbox planning into a persistent player account, so the permission prompt is part of the onboarding, not an unrelated external step.',
    ),
  ],
);

const factoryLayoutGuideTopic = EncyclopediaGuideTopic(
  slug: 'factory-layout-help',
  navLabel: 'Factory layout help',
  title: 'Factory Layout Help',
  subtitle: 'Topic-based setup guide for building, linking, and operating your first profitable factory chain.',
  topicsHeading: 'Factory setup topics',
  topics: [
    'PURCHASE unit setup and input-price discipline',
    'MANUFACTURING unit configuration and line consistency',
    'STORAGE buffering and anti-starvation flow control',
    'PUBLIC_SALES pricing, demand response, and iteration',
    'What every factory-relevant unit type actually does',
  ],
  cards: [
    EncyclopediaGuideCard(
      title: 'PURCHASE: Source inputs safely',
      body:
          'The PURCHASE unit is your defensive control point against bad input economics. Configure it with a realistic maximum price and a minimum quality floor so your factory does not consume expensive or weak inputs that destroy margins downstream. Keep this unit linked directly to MANUFACTURING to avoid disconnected flow where stock accumulates but production stays idle. Monitor how often purchasing succeeds each tick; repeated misses usually mean your cap is too strict for current market conditions. Raise limits gradually rather than in large jumps, then re-check final sales margin before keeping the change. A disciplined PURCHASE setup stabilizes the entire chain by protecting cost predictability, which is more valuable in early growth than chasing occasional low-price outliers.',
    ),
    EncyclopediaGuideCard(
      title: 'MANUFACTURING: Convert inputs to output',
      body:
          'MANUFACTURING is where your chain creates value, so configuration must stay simple and consistent. Assign one clear product to each active line and verify every required input path is connected before expecting throughput. If output is inconsistent, inspect upstream availability first, then confirm the line is not blocked by mismatched settings. Product quality is inherited from input quality and improved over time through better sourcing and research choices, so avoid frequent recipe thrashing during early ticks. Keep one line stable long enough to collect meaningful demand and margin data before scaling complexity. When this unit is tuned well, it becomes a predictable converter of purchased inputs into sellable inventory, making your pricing and expansion decisions evidence-based instead of reactive.',
    ),
    EncyclopediaGuideCard(
      title: 'STORAGE: Buffer your chain',
      body:
          'STORAGE protects your revenue loop from timing mismatches between purchasing, production, and sales. Without a buffer, one delayed input tick can cascade into manufacturing pauses and then empty retail stock. Configure storage as an intentional shock absorber: keep enough inventory to survive short purchasing volatility, but avoid overstock that traps cash and hides weak demand. Use storage trends as a diagnostic signal. Rising stock with weak sales suggests your price may be too high; flat stock with frequent shortages suggests sourcing or manufacturing capacity is too tight. In early game, stability beats maximum utilization. A healthy STORAGE policy keeps your chain resilient, preserves customer-facing availability, and gives you time to adjust strategy without abrupt revenue drop-offs.',
    ),
    EncyclopediaGuideCard(
      title: 'PUBLIC_SALES: Price and release',
      body:
          'PUBLIC_SALES is the market-facing unit where all upstream work becomes realized cash flow. Start with a realistic minimum price near market reference, then adjust in small controlled steps after observing several ticks. If units sell out instantly, test a modest price increase to capture margin; if inventory piles up, lower price carefully or improve perceived value through quality. Keep this unit supplied, because no pricing strategy works when shelves are empty. Track quantity sold, revenue, and gross profit together rather than optimizing one metric in isolation. The goal is a repeatable operating band where demand remains active and margin remains healthy. Consistent PUBLIC_SALES iteration turns your first factory from a static setup into a learning system that compounds over time.',
    ),
    EncyclopediaGuideCard(
      title: 'Unit type reference: what each one does',
      body:
          'Factory setup gets easier once each unit type has a clear job. PURCHASE secures inputs within price and quality limits. MANUFACTURING converts connected inputs into product output. STORAGE absorbs timing shocks and protects sales continuity. B2B_SALES pushes goods to other company buildings when you want internal distribution. PUBLIC_SALES exposes inventory to city demand at your configured minimum price. PRODUCT_QUALITY improves objective product quality through focused investment, while BRAND_QUALITY supports premium pricing by strengthening market perception. BRANDING and MARKETING reinforce awareness and demand momentum outside pure production throughput. MINING is the extraction counterpart for raw deposits when your chain starts from owned resources. Treat each unit as a specialized role in one connected pipeline, not as independent toggles, and your operating decisions become predictable and measurable.',
    ),
  ],
);

const salesShopGuideTopic = EncyclopediaGuideTopic(
  slug: 'sales-shop-help',
  navLabel: 'Sales shop setup help',
  title: 'Sales Shop Setup Walkthrough',
  subtitle:
      'Step-by-step guide for buying your first sales shop, configuring purchase flow, opening public sales, and activating marketing for reliable demand growth.',
  topicsHeading: 'Sales shop setup topics',
  topics: [
    'Buy and place the sales-shop building in a suitable lot',
    'Configure the PURCHASE unit to pull stock into the shop',
    'Configure PUBLIC_SALES for price, visibility, and first revenue',
    'Configure MARKETING to improve demand quality and consistency',
  ],
  cards: [
    EncyclopediaGuideCard(
      title: 'Step 1 - Buy the sales-shop building',
      body:
          'The sales shop starts as an execution hub, not a cosmetic building. In the buy-building flow, choose SALES_SHOP and prioritize a lot you can fund without draining operating reserve. The right purchase leaves enough company cash for immediate unit setup and early stock movement. Treat this as go-to-market infrastructure: your factory may produce perfectly, but without a correctly placed and funded shop, revenue never materializes. After purchase, confirm city context, affordability, and that the building appears in your company portfolio. Do not overpay for prestige location during first launch; survivability and setup speed matter more than vanity. A stable first shop creates the shortest path from inventory to real city demand and measurable cash flow.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 2 - Configure the PURCHASE unit',
      body:
          'Your shop PURCHASE unit controls inbound stock quality and cost before products ever reach customers. Configure product selection and sourcing so inventory arrives predictably from your intended upstream chain. If max price is too low, the unit starves and shelves stay empty; if too high, gross margin collapses even when sales volume looks strong. Set reasonable limits and validate that the unit is linked correctly to your shop flow so goods can move into sale-ready positions. Watch first ticks for signs of instability: repeated zero inflow means sourcing constraints are too strict or links are wrong. PURCHASE in sales shops is a margin gatekeeper. Good configuration protects profitability and prevents false conclusions caused by stock-outs.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 3 - Configure the PUBLIC_SALES unit',
      body:
          'PUBLIC_SALES is where your shop becomes visible to city demand. Set sale visibility to public and choose a minimum price near market reality so early ticks generate signal, not noise. If pricing is too aggressive, inventory stagnates and you cannot validate demand assumptions. If pricing is too low, volume may look healthy while contribution margin stays weak. Start from a balanced baseline, then iterate in small increments after several ticks. Keep the unit stocked and linked so demand has something to buy every cycle. Track quantity sold, realized revenue, and gross profit together. PUBLIC_SALES should be tuned as a controlled experiment, not a one-time toggle. The first stable configuration gives you the feedback loop needed for confident scaling.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 4 - Configure the MARKETING unit',
      body:
          'MARKETING extends sales performance beyond raw price mechanics by improving discoverability and demand momentum over time. Configure marketing with a realistic budget aligned to your current cash runway, then monitor whether improved demand quality offsets spend. Underfunded marketing often does nothing measurable; oversized budgets can silently burn cash before operational bottlenecks are solved. Activate marketing only after PURCHASE and PUBLIC_SALES are stable, so budget amplifies a working system instead of masking broken flow. In each review window, compare incremental volume and margin versus marketing cost. Keep adjustments gradual and evidence-driven. In early growth, the goal is consistency: use marketing to smooth demand volatility, strengthen repeat sales behavior, and create predictable conditions for price optimization and expansion.',
    ),
  ],
);

const forexTradingGuideTopic = EncyclopediaGuideTopic(
  slug: 'forex-trading-help',
  navLabel: 'Forex trading help',
  title: 'Forex Trading Walkthrough',
  subtitle:
      'Complete guide to swap execution, account transfers, city FX rates, trade history interpretation, and Gold AMM operations including swap, positions, and liquidity.',
  topicsHeading: 'Forex topics',
  topics: [
    'Swap tab structure, account context, and currency direction planning',
    'Transfers between your own bank accounts and why they matter before swaps',
    'City-driven FX board, base-currency reading, and post-fee conversion understanding',
    'Swap history validation, rate audit, and execution timing review',
    'Gold AMM swap flow for converting fiat to XAU and back',
    'Gold AMM positions tab, claimable fees, and ownership share reading',
    'Gold AMM liquidity provisioning, pool creation, and removal discipline',
  ],
  cards: [
    EncyclopediaGuideCard(
      title: 'Step 1 - Understand the Swap tab context first',
      body:
          'Start every forex session by confirming account context and currency direction before entering any amount. In Capitalism, swap behavior changes when you trade from bank accounts instead of legacy cash balances, so the source and destination selectors are the first risk-control checkpoint. Verify city context badge, available source balance, and destination currency visibility. If you rush this step, you may quote from the wrong owner account and then wonder why stock or building actions still fail later. The goal is not speed, but correctness: source currency, destination currency, owner context, and intended use case must align. This teaches players to read the swap panel like an operations dashboard, not a simple exchange widget, which prevents expensive downstream errors.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 2 - Request quote and execute with fee awareness',
      body:
          'After amount entry, always request a quote and review the full execution card before confirming. The quote is where rate, fee amount, and expected receive value become explicit and auditable. Players should compare quoted output with the target action they are funding, such as a USD stock trade or a city-currency building purchase. If output is too low, adjust amount instead of confirming blindly. Once you execute, read the success banner and verify both updated balances to confirm settlement happened in the expected accounts. Treat this process as a mini settlement checklist: quoted rate accepted, fee understood, output sufficient, and post-swap balances validated. Following this discipline keeps forex swaps predictable and avoids accidental underfunding of the next gameplay step.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 3 - Use account transfers before or after swaps',
      body:
          'The transfer tab is not cosmetic; it is a core liquidity-routing tool between your own bank accounts in the same currency. Players use it to reposition money to the account that will actually execute a trade, pay building costs, or absorb tax and operating pressure. Choose source and destination accounts carefully, because currency mismatch is intentionally blocked and same-account transfers are rejected. Enter amount, optional description, and submit only after checking available balance on the source side. This workflow is especially important when you operate multiple companies and personal accounts simultaneously. Without transfers, you may hold enough total money but still fail transactions due to local account shortage. Strong transfer habits make forex and stock flows smoother and reduce unnecessary failed actions.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 4 - Read the city FX board correctly',
      body:
          'The rates tab explains why the same nominal amount behaves differently across cities and currencies. Focus first on the base currency banner, then read each target rate and the post-fee effective rate column. This teaches players that executable value is slightly lower than headline mid-rate due to swap fee mechanics. Use the board to estimate purchasing power before opening buy-building, stock, or loan actions. Good players compare destinations, not just one rate line, because strategy changes with regional currency strength. Also check timestamp and source notes so you know whether rates are current and valid for planning. When understood properly, this board becomes a tactical planning instrument that prevents mispriced expectations and supports better cross-city investment timing.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 5 - Audit your own swap history',
      body:
          'History is where discipline becomes measurable. Each row captures from amount, to amount, execution rate, fee, and tick timing, allowing you to audit how effective your conversions were over time. Players should use this view after major trading sessions to detect repeated fee drag, poor timing, or wrong-direction swaps done under pressure. Comparing several rows helps you refine conversion habits and decide when to batch larger swaps instead of many small ones. History also supports debugging: if a later action fails for insufficient balance, you can verify whether the expected conversion actually happened and in which currency. Treat this tab as your personal treasury logbook. Consistent review turns forex from reactive clicking into a repeatable capital-management process.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 6 - Execute Gold AMM swaps intentionally',
      body:
          'Gold AMM swap mode lets you convert fiat currency to XAU or sell XAU back into fiat through constant-product liquidity pools. Before requesting quote, verify direction, selected fiat currency pool, input amount, and available balance, because insufficient fiat or insufficient available gold will block execution. The quote includes slippage, fee percent, and implied price, so players can compare whether the trade fits their objective: hedge, speculation, or liquidity repositioning. After confirm, read the success state and updated gold/fiat balances to ensure settlement happened exactly as expected. This step introduces a different market microstructure than simple forex rates, so learning to read pool-driven pricing and slippage is essential for advanced treasury behavior in the game economy.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 7 - Track AMM positions and claimable value',
      body:
          'The Positions tab shows where your liquidity is currently deployed and what value you can reclaim from each pool. Players should review liquidity shares, percentage ownership, claimable fiat, and claimable gold before making further swaps or withdrawals. This is critical because blocked gold inside pools is not immediately available for new swaps, and ignoring that constraint leads to confusing insufficient-gold errors. Use this view to decide whether to keep earning pool fees, partially remove liquidity, or rotate into another currency pool. Position monitoring is also your risk dashboard for AMM exposure: it reflects how your assets are split between free balances and pooled balances. Strong players check positions after volatility spikes so pool participation remains aligned with current strategy.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 8 - Add or remove liquidity with a plan',
      body:
          'Liquidity management is the strategic layer of Gold AMM. In Add Liquidity mode, you can join an existing pool or create a new one for a fiat currency, supplying fiat and gold according to pool ratio constraints. Entering liquidity blindly can lock too much gold and reduce your immediate flexibility for swaps or other spending priorities. Before adding, estimate what portion of total reserves you are willing to lock and how long you intend to stay exposed. On removal, use share fraction carefully and verify expected returned fiat and gold amounts. The objective is controlled participation: earn fees without compromising treasury responsiveness. This walkthrough helps players treat liquidity actions as portfolio decisions, not one-click experiments, which keeps both profitability and operational liquidity healthy.',
    ),
  ],
);

const stockExchangeGuideTopic = EncyclopediaGuideTopic(
  slug: 'stock-exchange-help',
  navLabel: 'Stock exchange help',
  title: 'Stock Exchange Walkthrough',
  subtitle:
      'End-to-end guide from onboarding IPO choices to share trading, USD settlement setup, forex swap preparation, and tax-reserve discipline in personal ledgers.',
  topicsHeading: 'Stock exchange topics',
  topics: [
    'IPO choice during onboarding and what it changes for your first listed company',
    'Buying shares through a company account using company cash and USD settlement',
    'Buying shares through your personal account and portfolio construction',
    'Selling shares, bid/ask execution, and liquidity-aware exits',
    'USD settlement account requirement and forex swap preparation',
    'Tax reserve mechanics and personal-ledger review after sales',
    'Dividend payout configuration in company settings and yearly distribution policy',
    'How dividends appear in personal account, portfolio income, and spending power',
  ],
  cards: [
    EncyclopediaGuideCard(
      title: 'Step 1 - Choose IPO plan in onboarding',
      body:
          'Your stock-exchange story starts before you ever open the Stock Exchange page. In onboarding, the IPO plan decides how much outside capital your first company raises and how much founder ownership you keep. A larger raise gives better operating runway for early mistakes and expansion, but your founder stake is diluted sooner. A smaller raise keeps stronger ownership but can create cash pressure when you need land, setup, and first-cycle inventory at the same time. Treat this as governance plus liquidity design, not just a one-click onboarding choice. The chosen plan influences future control thresholds, merger eligibility pace, and how much flexibility your company has before you begin personal and corporate share trading decisions.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 2 - Buy shares with a company account',
      body:
          'When you trade as a company, every order is executed from the company context selected in the top navbar and settles through a USD bank account owned by that company. This is materially different from personal investing because corporate cash, risk, and strategic intent are involved. Company purchases can support control strategies, defensive ownership, or long-term capital deployment, but they also reduce operating liquidity available for buildings, wages, and production buffers. Before pressing Buy, verify account context, selected settlement account, ask price, estimated cost, and current float conditions in the trade panel. Company-level ownership contributes to combined control ratio, so these trades directly shape claim-control and merge pathways. Use company buys deliberately, not as passive portfolio activity.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 3 - Buy shares with your personal account',
      body:
          'Personal-account buying is your direct investment layer, separate from company treasury strategy. In person mode, the trade panel uses your personal spendable cash and records ownership in your personal portfolio, where each holding tracks quantity, ownership ratio, live share price, and market value. This is ideal for targeted exposure to firms you do not want your operating company to fund directly. Focus on position sizing first: define quantity from available cash, then compare ask price against your intended horizon before execution. Because portfolio valuation changes every tick with quoted prices, avoid interpreting one snapshot as final performance. Personal buys are strongest when treated as a measured capital-allocation plan tied to risk tolerance, not as short-term reaction to one visible price move.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 4 - Sell shares using bid/ask discipline',
      body:
          'Selling shares executes at the bid side, so your realized exit value depends on quantity, spread, and market conditions at that tick. In the trade panel, review estimated proceeds before confirming the order and ensure you are selling from the intended account context. Personal-account sells and company-account sells are not equivalent operationally: they affect different balances and reporting surfaces. A good sell workflow uses staged exits rather than panic liquidation, especially when spreads widen. Confirm owned-share quantity, evaluate whether the position still supports your strategic objective, then sell in controlled increments when needed. The important behavior is repeatable execution discipline: read bid/ask, size carefully, and preserve optionality for future entries, control claims, or operating needs that may arise after the sale.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 5 - Prepare USD settlement and forex swap',
      body:
          'Stock trades require a USD settlement account for the active trade context. If the stock panel shows no USD settlement account, your next stop is Forex and banking setup, not another trade click. Use Forex to convert available currency into USD and ensure the resulting balance exists in the correct owner context: personal for person-account trading, company for company-account trading. After swap execution, re-open the stock trade panel and explicitly select the right settlement account so orders can clear. This requirement prevents hidden currency mismatch and keeps exchange accounting consistent. Operationally, it means share trading is a two-layer process: first liquidity preparation in currency space, then execution in the stock market. Treat USD readiness as a prerequisite checklist item for every trade session.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 6 - Review tax reserve in personal ledger',
      body:
          'After personal-account share sales, the system reserves 15% of proceeds into a tax reserve that is blocked from spending until tax-year settlement. This is visible both in stock-exchange confirmations and in the Personal Ledger, where available cash and tax reserve are separated explicitly. The key operational mistake to avoid is treating gross personal cash as fully tradable capital right after a profitable sale. Always read available cash, not only total cash, before planning follow-up buys. In the ledger, review reserve history, trade entries, and dividend flows together to understand true deployable liquidity. A disciplined review loop here protects you from overcommitting funds, keeps your next trade sizing realistic, and prevents avoidable insufficient-funds failures caused by ignored reserve mechanics.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 7 - Configure dividend payout ratio in company settings',
      body:
          'Dividend behavior begins in Company Settings, where you define the dividend payout ratio that controls how much profit is distributed versus retained. A higher ratio transfers more value to shareholders each tax cycle, improving investor income visibility but leaving less retained cash for growth, buildings, and resilience. A lower ratio keeps more capital inside the company for expansion and risk absorption, but may reduce immediate shareholder reward and market attractiveness for income-focused investors. This setting should match your strategy stage: growth-heavy firms often retain more, mature cash generators can distribute more. Always review payout ratio together with operating needs, debt pressure, and upcoming investments. This is exactly where players intentionally manage income policy rather than relying on accidental defaults.',
    ),
    EncyclopediaGuideCard(
      title: 'Step 8 - Read dividend impact in personal account',
      body:
          'After dividend policy is active and a tax-year distribution runs, effects become visible in personal account surfaces. In stock exchange and related portfolio views, dividend history records company source, game year, per-share payout, and total amount credited to your personal settlement balance. This matters because dividend income increases deployable capital without requiring share sales, preserving ownership while still improving liquidity. Players should track dividend inflow separately from trading gains, since each has different risk profile and planning use. If expected dividends do not appear, check whether you held shares at distribution time and whether company payout ratio supported meaningful distribution. This step teaches investors to evaluate dividend cash flow as a deliberate strategy lever, not an incidental bonus, improving long-term portfolio discipline.',
    ),
  ],
);

/// All 5 static guide topics, in the same order as the web's `topicMenu`
/// (before the always-last "Resources definition" GraphQL-backed tab).
const List<EncyclopediaGuideTopic> encyclopediaGuideTopics = [
  onboardingGuideTopic,
  factoryLayoutGuideTopic,
  salesShopGuideTopic,
  forexTradingGuideTopic,
  stockExchangeGuideTopic,
];
