using System;

namespace DesktopNotes
{
    public static class LocalizationManager
    {
        public static string CurrentLanguage { get; set; } = "ru";

        public static event Action? LanguageChanged;

        public static void SetLanguage(string lang)
        {
            if (lang != "ru" && lang != "en") lang = "ru";
            if (CurrentLanguage != lang)
            {
                CurrentLanguage = lang;
                LanguageChanged?.Invoke();
            }
        }

        public static string Get(string key, params object[] args)
        {
            bool isRu = CurrentLanguage == "ru";

            string text = key switch
            {
                "NewNote" => isRu ? "Новая заметка" : "New Note",
                "NewNoteTooltip" => isRu ? "Открыть ещё одно окно (+)" : "Open another window (+)",
                "ChooseFileTooltip" => isRu ? "Выбрать файл..." : "Choose file...",
                "OpenFileTooltip" => isRu ? "Открыть в Блокноте" : "Open in Notepad",
                "PinOnTooltip" => isRu ? "Закреплено поверх окон (Кликните для открепления)" : "Pinned on top (Click to unpin)",
                "PinOffTooltip" => isRu ? "Обычный режим (Кликните для закрепления поверх)" : "Normal window (Click to pin on top)",
                "LangTooltip" => isRu ? "Сменить язык (Текущий: Русский)" : "Switch language (Current: English)",
                "FontTooltip" => isRu ? "Переключить шрифт (Clean / Mono)" : "Toggle font (Clean / Mono)",
                "CloseTooltip" => isRu ? "Закрыть это окно" : "Close this window",
                "PrevPageTooltip" => isRu ? "Предыдущая заметка (←)" : "Previous note (←)",
                "NextPageTooltip" => isRu ? "Следующая заметка (→)" : "Next note (→)",
                "EmptyTitle" => isRu ? "Выберите заметку (txt или md)" : "Select a note (txt or md)",
                "EmptyMessage" => isRu 
                    ? "📄 Выберите заметку (txt или md)\n\nПеретащите сюда файл или целую папку\nили нажмите 📁 Выбрать вверху" 
                    : "📄 Select a note (txt or md)\n\nDrag & drop a file or a folder here\nor click 📁 Choose at the top",
                "DragOverlayText" => isRu ? "📂 Отпустите файл или папку сюда" : "📂 Drop file or folder here",
                "Copy" => isRu ? "Скопировать" : "Copy",
                "Copied" => isRu ? "✓ Скопировано!" : "✓ Copied!",
                "TrayTitle" => "NotionDesk Notes",
                "TrayNewNote" => isRu ? "➕ Новое окно" : "➕ New Window",
                "TrayOpenFile" => isRu ? "📁 Открыть файл..." : "📁 Open File...",
                "TrayShowAll" => isRu ? "👁 Показать все заметки" : "👁 Show All Notes",
                "TrayHideAll" => isRu ? "🙈 Скрыть все заметки" : "🙈 Hide All Notes",
                "TrayLanguage" => isRu ? "🌐 Язык / Language" : "🌐 Language / Язык",
                "TrayAutoStart" => isRu ? "⚙ Запускать с Windows" : "⚙ Start with Windows",
                "TrayExit" => isRu ? "✕ Выход" : "✕ Exit",
                _ => key
            };

            if (args != null && args.Length > 0)
            {
                try { return string.Format(text, args); } catch { return text; }
            }
            return text;
        }
    }
}
