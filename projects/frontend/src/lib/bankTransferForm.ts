export interface BankTransferDraftState {
  amount: number | null
  description: string
  submitAttempted: boolean
}

export function getVisibleBankTransferValidationMessage(
  validationMessage: string | null,
  submitAttempted: boolean,
): string | null {
  if (!submitAttempted) return null
  return validationMessage
}

export function resetBankTransferDraftAfterSuccess(): BankTransferDraftState {
  return {
    amount: null,
    description: '',
    submitAttempted: false,
  }
}
