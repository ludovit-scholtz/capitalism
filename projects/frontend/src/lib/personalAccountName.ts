/**
 * Personal account name generator for player registration.
 *
 * Generates a three-part fictional name (Firstname Middlename Lastname) using
 * the `unique-names-generator` package's built-in first-name dictionary plus a
 * curated list of international surnames.  The generator tracks used names per
 * session to avoid showing repeats; call `resetPersonalNameSession()` to clear
 * the session (e.g. when the player navigates away).
 *
 * Design rules:
 * - Names must sound realistic but NOT be the player's actual name (enforced by
 *   the UI warning "Don't use your real name").
 * - Three words: Firstname + Middlename + Lastname.
 * - Every word starts with a capital letter.
 * - Surnames span diverse origins to reflect the global player base.
 */

import { uniqueNamesGenerator, names } from 'unique-names-generator'

/**
 * Curated list of international surnames covering diverse origins.
 * Must have no duplicates and must contain at least 100 entries.
 */
export const surnames: string[] = [
  'Anderson', 'Baker', 'Carter', 'Davis', 'Evans', 'Foster', 'Garcia', 'Harris',
  'Irving', 'Jones', 'Knight', 'Lewis', 'Morgan', 'Nelson', 'Owen', 'Parker',
  'Quinn', 'Roberts', 'Smith', 'Taylor', 'Urban', 'Vargas', 'Wilson', 'Xavier',
  'Young', 'Zhang', 'Allen', 'Brooks', 'Collins', 'Duncan', 'Edwards', 'Fleming',
  'Grant', 'Hunter', 'Ingram', 'Jenkins', 'Keller', 'Lambert', 'Martinez', 'Nash',
  'Pierce', 'Rivera', 'Santos', 'Torres', 'Vance', 'Walker', 'Yamamoto', 'Zhou',
  'Archer', 'Bell', 'Cooper', 'Drake', 'Ellis', 'Fisher', 'Gordon', 'Hayes',
  'Irwin', 'Jacobs', 'Kim', 'Lane', 'Mitchell', 'Norton', 'Ortega', 'Patel',
  'Reynolds', 'Stone', 'Turner', 'Upton', 'Vicente', 'Warren', 'York', 'Zimmerman',
  'Adler', 'Baxter', 'Crawford', 'Dean', 'Erikson', 'Ford', 'Graham', 'Hayden',
  'Jensen', 'Knox', 'Lucas', 'Morrison', 'Nguyen', 'Okafor', 'Porter', 'Reyes',
  'Sato', 'Tanaka', 'Underwood', 'Vasquez', 'Webb', 'Yamazaki', 'Zucker',
  'Hoffman', 'Kowalski', 'Nowak', 'Petrov', 'Romero', 'Schreiber', 'Tobin',
  'Volkov', 'Watanabe', 'Xu', 'Yilmaz', 'Zielinski',
]

/** Session-local set of full names already shown to the current player. */
const usedPersonalNames = new Set<string>()

/**
 * Picks a random surname from the curated list.
 */
function pickSurname(): string {
  const idx = Math.floor(Math.random() * surnames.length)
  return surnames[idx] ?? surnames[0]!
}

/**
 * Generates a single three-part personal account name.
 * Does NOT check for session uniqueness; use `generatePersonalAccountName` for that.
 */
function generateRawName(): string {
  const firstName = uniqueNamesGenerator({ dictionaries: [names], style: 'capital' })
  const middleName = uniqueNamesGenerator({ dictionaries: [names], style: 'capital' })
  const lastName = pickSurname()
  return `${firstName} ${middleName} ${lastName}`
}

/**
 * Generates a unique three-part personal account name suggestion.
 *
 * Avoids repeating names already returned in the current session (tracked in
 * `usedPersonalNames`).  After 50 failed attempts to find a fresh name the
 * session is cleared automatically so the caller always receives a usable result.
 *
 * @returns A string of the form "Firstname Middlename Lastname".
 */
export function generatePersonalAccountName(): string {
  const maxAttempts = 50

  for (let i = 0; i < maxAttempts; i++) {
    const name = generateRawName()
    if (!usedPersonalNames.has(name)) {
      usedPersonalNames.add(name)
      return name
    }
  }

  // Exhaustion fallback: clear session and return a fresh name.
  usedPersonalNames.clear()
  const name = generateRawName()
  usedPersonalNames.add(name)
  return name
}

/**
 * Clears the session uniqueness tracking set.
 * Call when the player navigates away from the registration page or explicitly
 * requests a fresh session (e.g. component unmount).
 */
export function resetPersonalNameSession(): void {
  usedPersonalNames.clear()
}
