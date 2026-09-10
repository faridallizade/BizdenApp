import { jsPDF } from 'jspdf'

type BrandedQrPdf = {
  eventName: string
  eventDate: string
  label?: string
  uploadLimit: number
  source: string
  link: string
  brandColor?: string
}

export function downloadBrandedQrPdf(item: BrandedQrPdf) {
  const pdf = new jsPDF({ unit: 'mm', format: 'a5' })
  const color = item.brandColor?.match(/^#[0-9a-f]{6}$/i) ? item.brandColor : '#805742'
  const rgb = [Number.parseInt(color.slice(1, 3), 16), Number.parseInt(color.slice(3, 5), 16), Number.parseInt(color.slice(5, 7), 16)] as const
  pdf.setFillColor(...rgb)
  pdf.rect(0, 0, 148, 30, 'F')
  pdf.setTextColor(255, 255, 255)
  pdf.setFont('times', 'bold')
  pdf.setFontSize(23)
  pdf.text('bizdən', 12, 15)
  pdf.setFont('helvetica', 'normal')
  pdf.setFontSize(8)
  pdf.text('ANILARINIZ, BİZDƏN.', 12, 22)
  pdf.setTextColor(78, 59, 47)
  pdf.setFont('times', 'bold')
  pdf.setFontSize(21)
  pdf.text(item.eventName, 74, 45, { align: 'center', maxWidth: 118 })
  pdf.setFont('helvetica', 'normal')
  pdf.setFontSize(10)
  pdf.text(item.label || 'Xatirələrinizi paylaşın', 74, 55, { align: 'center' })
  pdf.addImage(item.source, 'PNG', 37, 64, 74, 74)
  pdf.setFontSize(10)
  pdf.text(`Bu QR ilə ${item.uploadLimit} foto paylaşa bilərsiniz.`, 74, 148, { align: 'center' })
  pdf.setTextColor(...rgb)
  pdf.setFontSize(8)
  pdf.text(item.link, 74, 158, { align: 'center', maxWidth: 122 })
  pdf.setTextColor(123, 94, 75)
  pdf.setFontSize(8)
  pdf.text(`Tədbir tarixi: ${new Intl.DateTimeFormat('az-AZ', { dateStyle: 'medium' }).format(new Date(item.eventDate))}`, 74, 183, { align: 'center' })
  pdf.text('Kod skan edildikdə birbaşa foto yükləmə səhifəsi açılır.', 74, 190, { align: 'center' })
  pdf.save(`bizden-${(item.label || item.eventName).replace(/[^a-z0-9]+/gi, '-').toLowerCase()}.pdf`)
}
