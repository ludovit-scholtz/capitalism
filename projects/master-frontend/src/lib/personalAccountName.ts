export type PlayerGender = 'MALE' | 'FEMALE' | 'UNSPECIFIED'

const femaleFirstNames = [
  'Alice', 'Amelia', 'Aria', 'Ava', 'Beatrice', 'Camila', 'Charlotte', 'Chloe',
  'Clara', 'Diana', 'Eleanor', 'Elise', 'Emma', 'Eva', 'Freya', 'Grace',
  'Hannah', 'Hazel', 'Iris', 'Isla', 'Jasmine', 'Lena', 'Lily', 'Luna',
  'Maya', 'Mia', 'Nora', 'Olivia', 'Ruby', 'Sofia',
]

const maleFirstNames = [
  'Adrian', 'Alexander', 'Benjamin', 'Caleb', 'Charles', 'Daniel', 'Dominic', 'Edward',
  'Elias', 'Ethan', 'Felix', 'Finn', 'Gabriel', 'Henry', 'Isaac', 'James',
  'Julian', 'Leo', 'Liam', 'Lucas', 'Marcus', 'Mason', 'Nathan', 'Noah',
  'Oliver', 'Oscar', 'Samuel', 'Theo', 'Thomas', 'William',
]

const surnames = [
  'Anderson', 'Baker', 'Carter', 'Davis', 'Evans', 'Foster', 'Garcia', 'Harris',
  'Irving', 'Jones', 'Knight', 'Lewis', 'Morgan', 'Nelson', 'Owen', 'Parker',
  'Quinn', 'Roberts', 'Smith', 'Taylor', 'Urban', 'Vargas', 'Wilson', 'Xavier',
  'Young', 'Zhang', 'Allen', 'Brooks', 'Collins', 'Duncan', 'Edwards', 'Fleming',
  'Grant', 'Hunter', 'Ingram', 'Jenkins', 'Keller', 'Lambert', 'Martinez', 'Nash',
]

const usedPersonalNames = new Set<string>()

function pickOne(items: string[]): string {
  const idx = Math.floor(Math.random() * items.length)
  return items[idx] ?? items[0]!
}

function normalizeGender(gender?: PlayerGender): PlayerGender {
  if (gender === 'MALE' || gender === 'FEMALE') return gender
  return Math.random() < 0.5 ? 'FEMALE' : 'MALE'
}

export function generatePersonalAccountName(gender?: PlayerGender): string {
  const resolvedGender = normalizeGender(gender)
  const source = resolvedGender === 'FEMALE' ? femaleFirstNames : maleFirstNames
  const maxAttempts = 50

  for (let i = 0; i < maxAttempts; i++) {
    const name = `${pickOne(source)} ${pickOne(source)} ${pickOne(surnames)}`
    if (!usedPersonalNames.has(name)) {
      usedPersonalNames.add(name)
      return name
    }
  }

  usedPersonalNames.clear()
  const fallback = `${pickOne(source)} ${pickOne(source)} ${pickOne(surnames)}`
  usedPersonalNames.add(fallback)
  return fallback
}
