# Stealth Quiz Helper (Gizli İmtahan Köməkçisi) — Sistem Dizaynı

## 1. Məqsəd
Windows PC mühitində test və imtahan suallarını ekrandan (həm mausla sahə seçərək şəkil kimi, həm də seçilmiş mətni `Ctrl+C` ilə kopyalayaraq) gizli şəkildə götürüb Gemini və ya OpenAI Vision/Text API vasitəsilə həll edən və yalnız `Ctrl+Shift` basılı saxlandıqda cavabı ekranda şəffaf/gizli şəkildə göstərən masaüstü proqram.

## 2. Arxitektura və Modullar
- **`config.py`**: `config.json` üzərindən ayarların oxunması və saxlanması (API açarları, seçilmiş provayder, model, şəffaflıq, göstərici mövqeyi, ekran paylaşımından gizlətmə).
- **`ai_client.py`**: Google Gemini (`gemini-2.5-flash`, `gemini-2.0-flash`) və OpenAI (`gpt-4o-mini`, `gpt-4o`) REST API müştərisi. Həm mətn, həm də PNG şəkil (Vision) sorğularını dəstəkləyir.
- **`capture.py`**: `mss` və `Pillow` ilə çox-monitorlu və DPI-uyğun səssiz ekran kəsimi (`capture_region`) və mübadilə buferindən (`clipboard`) mətn oxuma.
- **`overlay.py`**: `tkinter` + Win32 API (`WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`, `WS_EX_TRANSPARENT`, `WDA_EXCLUDEFROMCAPTURE`) əsasında:
  - Aktiv pəncərənin fokusunu itirməyən, Taskbar və Alt+Tab-da görünməyən, ekran qeydiyyatına (screen capture) düşməyən gizli cavab pəncərəsi.
  - Sual tutulduqda kursor yanında 0.3 saniyəlik mikro-nöqtə (`•`) indikatoru.
- **`hotkeys.py`**: `pynput` ilə qlobal klaviatura və maus dinləyicisi:
  - `Ctrl + Maus Sol Çəkmə`: Ekran sahəsini kəsib AI-ya göndərir.
  - `Ctrl + C`: Seçilmiş mətni AI-ya göndərir.
  - `Ctrl + Shift` (basılı saxla): Statusu (`● Hazır`, `◌ Həll edilir...`, `✓ Cavab: ...`, `✕ Xəta`) və cavabı göstərir; buraxdıqda gizlədir.
  - `Ctrl + Shift + S`: Ayarlar pəncərəsini açır.
  - `Ctrl + Shift + Q`: Proqramı tam bağlayır.
- **`settings_ui.py`**: API açarlarını daxil etmək, test etmək və görünüş ayarlarını dəyişmək üçün interfeys.
- **`main.py`**: Bütün komponentləri birləşdirən əsas giriş nöqtəsi və System Tray ikonu.
