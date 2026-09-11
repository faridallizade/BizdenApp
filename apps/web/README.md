# Bizdən Web

React + TypeScript + Vite host portalı və qonaq upload/public-gallery interfeysidir.

## Əmrlər

```bash
npm install
npm run dev
npm run build
npm run lint
```

Development server eyni origin üzərindən API-yə müraciət edir. Production-da Nginx `/api` sorğularını .NET API-yə proxy edir; browser-da ayrıca API URL konfiqurasiyası tələb olunmur.

## Struktur

- `src/components/`: auth, profil, gallery, admin və UI hissələri.
- `src/lib/api.ts`: cookie/CSRF dəstəyi olan API client.
- `src/lib/qrPdf.ts`: branded QR PDF generatoru.
- `src/i18n/`: `az`, `en`, `ru` dil resursları və localStorage dil seçimi.

## Route-lar

- `/`: host dashboard
- `/q/:token`: qonaq foto upload səhifəsi
- `/g/:publicId`: PIN-li public gallery
- `/profile`: host profili
- `/admin`: admin panel (yalnız admin session)
