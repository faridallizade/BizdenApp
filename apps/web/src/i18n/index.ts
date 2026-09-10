import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

const resources = {
  az: { translation: { language: 'Dil', login: 'Daxil ol', register: 'Hesab yarat', profile: 'Profilim', logout: 'Çıxış', gallery: 'Qalereya', passwordReset: 'Şifrəni sıfırla' } },
  en: { translation: { language: 'Language', login: 'Sign in', register: 'Create account', profile: 'My profile', logout: 'Sign out', gallery: 'Gallery', passwordReset: 'Reset password' } },
  ru: { translation: { language: 'Язык', login: 'Войти', register: 'Создать аккаунт', profile: 'Мой профиль', logout: 'Выйти', gallery: 'Галерея', passwordReset: 'Сбросить пароль' } }
}

const savedLanguage = window.localStorage.getItem('bizden-language')
void i18n.use(initReactI18next).init({ resources, lng: savedLanguage && ['az', 'en', 'ru'].includes(savedLanguage) ? savedLanguage : 'az', fallbackLng: 'az', interpolation: { escapeValue: false } })
i18n.on('languageChanged', language => window.localStorage.setItem('bizden-language', language))

export default i18n
