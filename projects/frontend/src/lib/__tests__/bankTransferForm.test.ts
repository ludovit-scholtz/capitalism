import { describe, expect, it } from 'vitest'
import {
  getVisibleBankTransferValidationMessage,
  resetBankTransferDraftAfterSuccess,
} from '@/lib/bankTransferForm'

describe('bankTransferForm', () => {
  it('shows validation error only after submit attempt', () => {
    expect(getVisibleBankTransferValidationMessage('Enter a positive amount.', false)).toBeNull()
    expect(getVisibleBankTransferValidationMessage('Enter a positive amount.', true)).toBe(
      'Enter a positive amount.',
    )
  })

  it('returns null when there is no validation issue', () => {
    expect(getVisibleBankTransferValidationMessage(null, true)).toBeNull()
  })

  it('resets draft state after successful transfer', () => {
    expect(resetBankTransferDraftAfterSuccess()).toEqual({
      amount: null,
      description: '',
      submitAttempted: false,
    })
  })
})
