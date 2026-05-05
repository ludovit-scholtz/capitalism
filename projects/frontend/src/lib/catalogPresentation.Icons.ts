import type { ProductType } from '@/types'

type SupportedLocale = 'en' | 'sk' | 'de'

const tokenTranslations: Record<Exclude<SupportedLocale, 'en'>, Record<string, string>> = {
  sk: {
    adapter: 'adaptér',
    aid: 'pomoc',
    amplifier: 'zosilňovač',
    allergy: 'alergický',
    animal: 'zvierací',
    antiseptic: 'antiseptikum',
    assembly: 'montážna',
    bags: 'vrecia',
    bakery: 'pekárenská',
    bandages: 'obväzy',
    basic: 'základný',
    battery: 'batériový',
    beam: 'nosník',
    bed: 'posteľ',
    bench: 'lavica',
    biscuit: 'sušienkový',
    board: 'doska',
    bookshelf: 'knižnica',
    box: 'krabica',
    bread: 'chlieb',
    breadcrumbs: 'strúhanka',
    bran: 'otrubové',
    bulb: 'žiarovka',
    bunk: 'poschodová',
    cabinet: 'skrinka',
    cable: 'káblový',
    cake: 'koláčová',
    calculator: 'kalkulačka',
    cereal: 'cereálne',
    cell: 'článok',
    chair: 'stolička',
    circuit: 'obvod',
    coffee: 'konferenčný',
    cold: 'chladiaci',
    column: 'stĺp',
    components: 'komponenty',
    compression: 'kompresný',
    contact: 'kontakt',
    control: 'ovládací',
    cotton: 'bavlnené',
    cough: 'kašľový',
    crackers: 'krekry',
    crib: 'postieľka',
    desk: 'stôl',
    dining: 'jedálenský',
    disinfectant: 'dezinfekčné',
    display: 'displej',
    door: 'dvere',
    dressing: 'ošetrenie',
    dresser: 'komoda',
    duct: 'kanál',
    electronic: 'elektronický',
    fasteners: 'spojovací materiál',
    feed: 'krmivo',
    filing: 'spisová',
    first: 'prvá',
    flour: 'múka',
    frame: 'rám',
    gate: 'bránový',
    gauze: 'gáza',
    glass: 'sklenený',
    gloves: 'rukavice',
    gold: 'zlatý',
    grain: 'obilné',
    grille: 'mriežka',
    healing: 'hojivá',
    home: 'domáci',
    hub: 'uzol',
    industrial: 'priemyselný',
    ingot: 'ingot',
    insulation: 'izolácia',
    instant: 'okamžitý',
    iron: 'železný',
    junction: 'spojovacia',
    kit: 'súprava',
    lamp: 'lampa',
    led: 'led',
    masks: 'masky',
    medical: 'zdravotnícke',
    medicine: 'liek',
    meter: 'merací',
    mineral: 'minerálny',
    mix: 'zmes',
    module: 'modul',
    nails: 'klince',
    network: 'sieťový',
    nightstand: 'nočný stolík',
    noodles: 'rezance',
    office: 'kancelársky',
    ointment: 'masť',
    pack: 'balenie',
    packs: 'balenia',
    pallet: 'paleta',
    pain: 'proti bolesti',
    pancake: 'palacinková',
    panel: 'panel',
    pasta: 'cestoviny',
    patio: 'terasový',
    pipe: 'rúrková',
    planks: 'dosky',
    porridge: 'kašová',
    power: 'napájací',
    premix: 'premix',
    rack: 'regál',
    radio: 'rádio',
    railing: 'zábradlie',
    relay: 'relé',
    relief: 'úľava',
    roll: 'rolka',
    roof: 'strecha',
    roofing: 'strešný',
    router: 'router',
    safety: 'bezpečnostný',
    saline: 'soľný',
    sandwich: 'sendvičový',
    scaffold: 'lešenie',
    screws: 'skrutky',
    section: 'sekcia',
    semolina: 'krupica',
    sensor: 'senzor',
    set: 'sada',
    sheet: 'plech',
    signal: 'signálový',
    silicon: 'kremíkový',
    snack: 'snack',
    sofa: 'pohovkový',
    soft: 'mäkký',
    solar: 'solárny',
    speaker: 'reproduktor',
    steel: 'oceľový',
    stool: 'stolička',
    storage: 'skladovací',
    supplement: 'doplnok',
    support: 'podporný',
    surgical: 'chirurgické',
    swabs: 'tyčinky',
    table: 'stôl',
    tape: 'páska',
    tile: 'škridla',
    toast: 'toastový',
    touch: 'dotykový',
    tv: 'tv',
    ventilation: 'ventilačná',
    vitamin: 'vitamínové',
    wafer: 'wafer',
    wall: 'stenový',
    wardrobe: 'šatník',
    warehouse: 'skladový',
    window: 'okno',
    wipes: 'obrúsky',
    wood: 'drevo',
    wooden: 'drevený',
    wound: 'rana',
    wrap: 'ovínadlo',
  },
  de: {
    adapter: 'adapter',
    aid: 'hilfe',
    amplifier: 'verstärker',
    allergy: 'allergie',
    animal: 'tier',
    antiseptic: 'antiseptikum',
    assembly: 'montage',
    bags: 'säcke',
    bakery: 'bäckerei',
    bandages: 'bandagen',
    basic: 'basis',
    battery: 'batterie',
    beam: 'träger',
    bed: 'bett',
    bench: 'bank',
    biscuit: 'keks',
    board: 'platte',
    bookshelf: 'bücherregal',
    box: 'box',
    bread: 'brot',
    breadcrumbs: 'paniermehl',
    bran: 'kleie',
    bulb: 'glühbirne',
    bunk: 'etagen',
    cabinet: 'schrank',
    cable: 'kabel',
    cake: 'kuchen',
    calculator: 'rechner',
    cereal: 'getreide',
    cell: 'zelle',
    chair: 'stuhl',
    circuit: 'schaltkreis',
    coffee: 'kaffee',
    cold: 'kalt',
    column: 'säule',
    components: 'komponenten',
    compression: 'kompression',
    contact: 'kontakt',
    control: 'steuer',
    cotton: 'baumwoll',
    cough: 'husten',
    crackers: 'cracker',
    crib: 'bettchen',
    desk: 'schreibtisch',
    dining: 'speise',
    disinfectant: 'desinfektion',
    display: 'display',
    door: 'tür',
    dressing: 'verband',
    dresser: 'kommode',
    duct: 'kanal',
    electronic: 'elektronik',
    fasteners: 'befestiger',
    feed: 'futter',
    filing: 'akten',
    first: 'erste',
    flour: 'mehl',
    frame: 'rahmen',
    gate: 'tor',
    gauze: 'gaze',
    glass: 'glas',
    gloves: 'handschuhe',
    gold: 'gold',
    grain: 'getreide',
    grille: 'gitter',
    healing: 'heil',
    home: 'home',
    hub: 'zentrale',
    industrial: 'industrie',
    ingot: 'ingot',
    insulation: 'dämm',
    instant: 'sofort',
    iron: 'eisen',
    junction: 'verteiler',
    kit: 'set',
    lamp: 'lampe',
    led: 'led',
    masks: 'masken',
    medical: 'medizin',
    medicine: 'medizin',
    meter: 'meter',
    mineral: 'mineral',
    mix: 'mischung',
    module: 'modul',
    nails: 'nägel',
    network: 'netzwerk',
    nightstand: 'nachttisch',
    noodles: 'nudeln',
    office: 'büro',
    ointment: 'salbe',
    pack: 'packung',
    packs: 'packungen',
    pallet: 'palette',
    pain: 'schmerz',
    pancake: 'pfannkuchen',
    panel: 'panel',
    pasta: 'pasta',
    patio: 'terrassen',
    pipe: 'rohr',
    planks: 'bretter',
    porridge: 'brei',
    power: 'strom',
    premix: 'vormischung',
    rack: 'regal',
    radio: 'radio',
    railing: 'geländer',
    relay: 'relais',
    relief: 'linderung',
    roll: 'rolle',
    roof: 'dach',
    roofing: 'dach',
    router: 'router',
    safety: 'sicherheits',
    saline: 'salz',
    sandwich: 'sandwich',
    scaffold: 'gerüst',
    screws: 'schrauben',
    section: 'abschnitt',
    semolina: 'grieß',
    sensor: 'sensor',
    set: 'satz',
    sheet: 'blech',
    signal: 'signal',
    silicon: 'silizium',
    snack: 'snack',
    sofa: 'sofa',
    soft: 'weich',
    solar: 'solar',
    speaker: 'lautsprecher',
    steel: 'stahl',
    stool: 'hocker',
    storage: 'lager',
    supplement: 'ergänzung',
    support: 'stütz',
    surgical: 'chirurgisch',
    swabs: 'stäbchen',
    table: 'tisch',
    tape: 'band',
    tile: 'ziegel',
    toast: 'toast',
    touch: 'touch',
    tv: 'tv',
    ventilation: 'lüftung',
    vitamin: 'vitamin',
    wafer: 'wafer',
    wall: 'wand',
    wardrobe: 'kleiderschrank',
    warehouse: 'lager',
    window: 'fenster',
    wipes: 'tücher',
    wood: 'holz',
    wooden: 'holz',
    wound: 'wund',
    wrap: 'wickel',
  },
}

const visualsByIndustry: Record<string, { background: string; accent: string }> = {
  FURNITURE: { background: '#8B5A2B', accent: '#D4A373' },
  FOOD_PROCESSING: { background: '#B45309', accent: '#F59E0B' },
  HEALTHCARE: { background: '#7C3AED', accent: '#A78BFA' },
  ELECTRONICS: { background: '#0EA5E9', accent: '#67E8F9' },
  CONSTRUCTION: { background: '#374151', accent: '#9CA3AF' },
}
const defaultProductVisual = { background: '#8B5A2B', accent: '#D4A373' }

export function translateSlug(slug: string, locale: Exclude<SupportedLocale, 'en'>): string {
  return slug
    .split('-')
    .map((token) => tokenTranslations[locale][token] ?? token)
    .map((value) => value.charAt(0).toUpperCase() + value.slice(1))
    .join(' ')
}

export function getProductImageUrl(product: Pick<ProductType, 'slug' | 'industry' | 'name'>): string {
  const { icon, background, accent } = getProductVisual(product.slug, product.industry)
  return createEmojiImageDataUrl(icon, background, accent)
}

function getProductVisual(slug: string, industry: string): { icon: string; background: string; accent: string } {
  const base = visualsByIndustry[industry] ?? defaultProductVisual

  if (/(chair|bench|stool)/.test(slug)) return { icon: '🪑', ...base }
  if (/(bed|crib|bunk)/.test(slug)) return { icon: '🛏️', ...base }
  if (/(table|desk|stand)/.test(slug)) return { icon: '🛋️', ...base }
  if (/(bread|toast|sandwich)/.test(slug)) return { icon: '🍞', ...base }
  if (/(pasta|noodles)/.test(slug)) return { icon: '🍝', ...base }
  if (/(flour|semolina|premix|mix)/.test(slug)) return { icon: '🥣', ...base }
  if (/(medicine|tablet|vitamin|supplement)/.test(slug)) return { icon: '💊', ...base }
  if (/(bandages|gauze|wipes|tape|wrap)/.test(slug)) return { icon: '🩹', ...base }
  if (/(wafer|circuit|module|router|calculator|relay|display|hub)/.test(slug)) return { icon: '💻', ...base }
  if (/(lamp|bulb)/.test(slug)) return { icon: '💡', ...base }
  if (/(battery)/.test(slug)) return { icon: '🔋', ...base }
  if (/(beam|panel|sheet|column|railing|rack|door|duct|pipe|window|tile|frame)/.test(slug)) return { icon: '🏗️', ...base }
  if (/(nails|screws|fasteners)/.test(slug)) return { icon: '🔩', ...base }
  if (/(pallet|planks|wood)/.test(slug)) return { icon: '🪵', ...base }
  if (/(gloves|masks)/.test(slug)) return { icon: '🧤', ...base }
  if (/(antiseptic|ointment|syrup|saline)/.test(slug)) return { icon: '🧴', ...base }
  if (/(speaker|radio|signal|adapter|power)/.test(slug)) return { icon: '🔌', ...base }
  if (/(solar)/.test(slug)) return { icon: '☀️', ...base }

  return { icon: industryIcon(industry), ...base }
}

function industryIcon(industry: string): string {
  switch (industry) {
    case 'FOOD_PROCESSING':
      return '🍽️'
    case 'HEALTHCARE':
      return '💉'
    case 'ELECTRONICS':
      return '🧠'
    case 'CONSTRUCTION':
      return '🧱'
    default:
      return '🏭'
  }
}

function createEmojiImageDataUrl(icon: string, backgroundColor: string, accentColor: string): string {
  const safeIcon = escapeSvgText(icon)
  const safeBackgroundColor = sanitizeHexColor(backgroundColor)
  const safeAccentColor = sanitizeHexColor(accentColor)
  const gradientId = `g-${hashValue(`${safeIcon}-${safeBackgroundColor}-${safeAccentColor}`)}`
  const svg = `
    <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 160 160'>
      <defs>
        <linearGradient id='${gradientId}' x1='0' y1='0' x2='1' y2='1'>
          <stop offset='0%' stop-color='${safeBackgroundColor}'/>
          <stop offset='100%' stop-color='${safeAccentColor}'/>
        </linearGradient>
      </defs>
      <rect width='160' height='160' rx='28' fill='url(#${gradientId})'/>
      <circle cx='80' cy='80' r='48' fill='rgba(255,255,255,0.18)'/>
      <text x='80' y='96' text-anchor='middle' font-size='56'>${safeIcon}</text>
    </svg>
  `

  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`
}

function sanitizeHexColor(value: string): string {
  return /^#[0-9a-f]{6}$/i.test(value) ? value : '#4B5563'
}

function escapeSvgText(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
}

function hashValue(value: string): string {
  let hash = 0
  for (let index = 0; index < value.length; index += 1) {
    hash = ((hash << 5) - hash) + value.charCodeAt(index)
    hash |= 0
  }

  return Math.abs(hash).toString(36)
}
