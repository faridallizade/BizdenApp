import { useTranslation } from 'react-i18next'

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation()
  return <label className="language-switcher">{t('language')}<select value={i18n.language} onChange={event => { void i18n.changeLanguage(event.target.value) }} aria-label={t('language')}><option value="az">AZ</option><option value="en">EN</option><option value="ru">RU</option></select></label>
}
