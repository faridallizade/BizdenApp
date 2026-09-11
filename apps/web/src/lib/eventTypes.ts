export type EventItem = {
  id: string
  name: string
  description?: string
  eventDate: string
  timeZone: string
  uploadStartAt: string
  uploadEndAt: string
  status: 'Draft' | 'Active' | 'Completed' | 'Archived'
  invitationCount: number
  brandColor?: string
  customMessage?: string
}

export type Invitation = {
  id: string
  label?: string
  uploadLimit: number
  reservedUploads: number
  completedUploads: number
  isActive: boolean
  expiresAt?: string
  createdAt: string
}

export const blankEvent = () => ({
  name: '', description: '', eventDate: '', timeZone: 'Asia/Baku', uploadStartAt: '', uploadEndAt: '',
  status: 'Draft' as const, brandColor: '#805742', customMessage: '',
})

export function toApiDate(value: string) { return new Date(value).toISOString() }
export function toInputDate(value: string) { return value ? new Date(value).toISOString().slice(0, 16) : '' }
export function formatDate(value: string) { return new Intl.DateTimeFormat('az-AZ', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) }
