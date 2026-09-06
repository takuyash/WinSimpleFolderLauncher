using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StylishLauncherINI
{
    public static class LanguageManager
    {
        private static string IniPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        public static string CurrentLanguage { get; private set; } = "ja";

        public static event Action LanguageChanged;

        static LanguageManager()
        {
            LoadSettings();
        }

        public static void LoadSettings()
        {
            if (File.Exists(IniPath))
            {
                var ini = IniHelper.ReadIni(IniPath);
                if (ini.ContainsKey("Language"))
                {
                    CurrentLanguage = ini["Language"];
                }
            }
        }

        public static void SaveLanguage(string lang)
        {
            CurrentLanguage = lang;
            // 他の設定を維持するため、保存はSettingsForm側で一括で行う運用に合わせます
            LanguageChanged?.Invoke();
        }

        public static string GetString(string key)
        {
            var dict = CurrentLanguage == "en" ? English : Japanese;
            return dict.ContainsKey(key) ? dict[key] : key;
        }

        private static readonly Dictionary<string, string> Japanese = new()
        {
            { "SearchPlaceholder", "ファイル / フォルダーの検索" },
            { "SettingTitle", "設定" },
            { "SettingPath", "フォルダのパス:" },
            { "SettingBrowse", "参照..." },
            { "SettingFontSize", "文字サイズ:" },
            { "SettingLang", "言語 (Language):" },
            { "SettingSave", "保存" },
            { "HelpTitle", "ヘルプ / バージョン情報" },
            { "HelpRepo", "GitHub リポジトリ" },
            { "HelpUsage", "ヘルプ / 使い方" },
            { "HelpLicense", "ライセンス" },
            { "HelpUpdate", "新しいバージョンがあります（v{0}）" },
            { "MenuOpen", "開く" },
            { "MenuSetting", "設定" },
            { "MenuHelp", "ヘルプ" },
            { "MenuExit", "終了" },
            { "MenuCopyPath", "パスをコピー" },
            { "LauncherNoPath", "フォルダが設定されていません。\n\nタスクトレイのアプリケーションから\n「設定」を行ってください。" },
            { "MsgPathReq", "パスを入力してください。" },
            { "MsgDirNotExists", "指定されたフォルダは存在しません。" },
            { "MsgSystemDirError", "ドライブ直下やシステムフォルダは登録できません。\nサブフォルダを指定してください。" },
            { "MsgAccessDenied", "このフォルダにはアクセス権がありません。\n別のフォルダを指定してください。" },
            { "MsgTooManyItems", "フォルダ内の項目数が多すぎます。\n\n上限 : {0}\n検出 : {1} 以上\n\nより小さなフォルダを指定してください。" },
            { "MsgHeavyConfirm", "このフォルダには {0} 個の項目があります。\n動作が重くなる可能性があります。\n\nそれでも登録しますか？" },
            { "MsgConfirmTitle", "確認" },
            { "MsgSaveSuccess", "保存しました。" },
            { "MsgSaveFailed", "保存に失敗しました: " },
            { "DialogSelectDir", "フォルダを選択してください" },
            { "SettingEnableHotkey", "ホットキーを有効にする" },
            { "SettingLaunchKeyCount", "連打回数 (2～5)回:" },
            { "SettingTriggerKey", "起動キー" }
        };

        private static readonly Dictionary<string, string> English = new()
        {
            { "SearchPlaceholder", "Search files / folders" },
            { "SettingTitle", "Settings" },
            { "SettingPath", "Folder Path:" },
            { "SettingBrowse", "Browse..." },
            { "SettingFontSize", "Font Size:" },
            { "SettingLang", "Language:" },
            { "SettingSave", "Save" },
            { "HelpTitle", "Help / Version Info" },
            { "HelpRepo", "GitHub Repository" },
            { "HelpUsage", "Help / Usage" },
            { "HelpLicense", "License" },
            { "HelpUpdate", "New version available (v{0})" },
            { "MenuOpen", "Open" },
            { "MenuSetting", "Settings" },
            { "MenuHelp", "Help" },
            { "MenuExit", "Exit" },
            { "MenuCopyPath", "Copy Path" },
            { "LauncherNoPath", "Folder is not configured.\n\nPlease go to \"Settings\" from the\ntray icon menu." },
            { "MsgPathReq", "Please enter a path." },
            { "MsgDirNotExists", "The specified folder does not exist." },
            { "MsgSystemDirError", "Root drives or system folders cannot be registered.\nPlease specify a subfolder." },
            { "MsgAccessDenied", "No access rights to this folder.\nPlease specify another folder." },
            { "MsgTooManyItems", "Too many items in the folder.\n\nLimit: {0}\nDetected: {1} or more\n\nPlease specify a smaller folder." },
            { "MsgHeavyConfirm", "This folder contains {0} items.\nIt may slow down the application.\n\nDo you still want to register it?" },
            { "MsgConfirmTitle", "Confirm" },
            { "MsgSaveSuccess", "Saved successfully." },
            { "MsgSaveFailed", "Failed to save: " },
            { "DialogSelectDir", "Select a folder" },
            { "SettingEnableHotkey", "Enable hotkey" },
            { "SettingLaunchKeyCount", "press count (2–5):" },
            { "SettingTriggerKey", "Trigger Key:" }

        };
    }
}