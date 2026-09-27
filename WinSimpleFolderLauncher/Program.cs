using System.Diagnostics;
using System.Runtime.InteropServices;
using WinSimpleFolderLauncher.Forms;
using WinSimpleFolderLauncher.Helpers;

namespace WinSimpleFolderLauncher
{
    static class Program
    {

        // ==========================================
        // 二重起動防止用 Mutex
        // ==========================================
        private static Mutex _mutex;

        // --- Win32 API Definitions ---
        private const int WH_KEYBOARD_LL = 13; // OS全体のキー入力を受け取るモード
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104; // Alt絡みのキー押す
        private const int WM_SYSKEYUP = 0x0105; // Alt絡みのキー離す
        private const int VK_LSHIFT = 0xA0; // 左Shift
        private const int VK_RSHIFT = 0xA1; // 右Shift

        private const int MOD_CONTROL = 0x0002; // Ctrl
        private const int MOD_SHIFT = 0x0004; // Shift
        private const int WM_HOTKEY = 0x0312; // ホットキー押された時に届くWindowsメッセージ
        private const int HOTKEY_ID_CTRL_SHIFT_I = 9001; // ホットキー識別ID

        private static DateTime _lastKeyTime = DateTime.MinValue;
        private static int _pressCount = 0;
        private static bool _isKeyPressed = false;

        // ==========================================
        // 設定キャッシュ（毎キー入力での設定ファイル読み込みを避けるため）
        // ==========================================
        private static bool _hotKeyEnabled = true;
        private static int _shiftPressCount = 2;
        private static string _triggerKey = "Shift";

        // キーボードフック
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        // キーボードフックを解除する
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        // 次のフック処理へイベントを渡す
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // フック登録時の自分のID取得
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // ホットキー登録
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        // ホットキー登録解除
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // フィールド
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private const int DOUBLE_PRESS_MS = 300; //0.3秒
        private static LauncherForm _launcher;
        // どこからでも参照できるアイコンオブジェクト
        public static Icon AppIcon;

        private static string IniPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        /// <summary>
        /// Launcher以外のフォームが開いているか確認
        /// </summary>
        private static bool IsOtherFormOpen()
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f is LauncherForm) continue;
                if (f.Visible) return true;
            }
            return false;
        }

        /// <summary>
        /// ホットキー有効判定（キャッシュ済みの値を返す。実際の読み込みは ApplyConfig / ReloadConfig で行う）
        /// </summary>
        private static bool IsHotKeyEnabled() => _hotKeyEnabled;

        /// <summary>
        /// Shift連打回数取得（キャッシュ済みの値を返す）
        /// </summary>
        private static int GetShiftPressCount() => _shiftPressCount;

        /// <summary>
        /// 起動トリガーキー取得（キャッシュ済みの値を返す）
        /// </summary>
        private static string GetTriggerKey() => _triggerKey;

        /// <summary>
        /// 読み込んだ ini の内容を設定キャッシュに反映する
        /// </summary>
        private static void ApplyConfig(Dictionary<string, string> ini)
        {
            _hotKeyEnabled = ini.TryGetValue("EnableHotKey", out var enabledStr)
                && bool.TryParse(enabledStr, out var enabled)
                    ? enabled
                    : true;

            _shiftPressCount = ini.TryGetValue("ShiftPressCount", out var countStr)
                && int.TryParse(countStr, out var count)
                    ? Math.Max(2, Math.Min(5, count))
                    : 2;

            _triggerKey = ini.TryGetValue("TriggerKey", out var trigger) ? trigger : "Shift";
        }

        /// <summary>
        /// 設定ファイルを読み直してキャッシュを更新する。
        /// 設定画面を保存して閉じたタイミングなど、ini変更後に呼び出す。
        /// （例: LauncherForm の「設定」メニューで SettingsForm.ShowDialog() の後に Program.ReloadConfig() を呼ぶ）
        /// </summary>
        public static void ReloadConfig()
        {
            try
            {
                var ini = File.Exists(IniPath) ? IniHelper.ReadIni(IniPath) : new Dictionary<string, string>();
                ApplyConfig(ini);
            }
            catch
            {
                // 読み込み失敗時は安全側のデフォルトにフォールバック
                _hotKeyEnabled = true;
                _shiftPressCount = 2;
                _triggerKey = "Shift";
            }
        }

        [STAThread]
        static void Main()
        {

            // ==========================================
            // 二重起動防止
            // ==========================================
            bool createdNew;

            _mutex = new Mutex(
                true,
                "WinSimpleFolderLauncher_SingleInstance",
                out createdNew);

            // すでに起動している場合は何もせず終了
            if (!createdNew)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // アイコンを一度だけ読み込む
            AppIcon = LoadIcon("icon.ico");

            var ini = IniHelper.ReadIni(IniPath);
            string rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";

            // 起動時に一度だけ設定を読み込み、以降はキャッシュを参照する
            ApplyConfig(ini);

            _launcher = new LauncherForm(rootPath);
            IntPtr forceHandle = _launcher.Handle;
            _launcher.Hide();

            _hookID = SetHook(_proc);

            MessageWindow messageWindow = new MessageWindow();
            RegisterHotKey(messageWindow.Handle, HOTKEY_ID_CTRL_SHIFT_I, MOD_CONTROL | MOD_SHIFT, (int)Keys.I);
            messageWindow.LauncherRequested += (s, e) =>
            {
                if (!IsHotKeyEnabled()) return;
                ShowLauncher();
            };

            Application.Run();

            UnhookWindowsHookEx(_hookID);
            UnregisterHotKey(messageWindow.Handle, HOTKEY_ID_CTRL_SHIFT_I);
        }

        private static void ShowLauncher()
        {
            // 他フォーム開いてたら表示しない
            if (IsOtherFormOpen()) return;

            if (_launcher == null) return;
            if (_launcher.IsDisposed) return;
            if (_launcher.Visible) return;

            _launcher.Show();
            _launcher.Activate();
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    proc,
                    GetModuleHandle(curModule.ModuleName),
                    0);
            }
        }

        /// <summary>
        /// グローバルキーボードフックのコールバック
        /// OS全体のキー入力のたびに呼ばれるため、内部で例外が発生してもフック自体が
        /// 無効化されたりアプリがクラッシュしたりしないよう、必ず try/catch で保護
        /// </summary>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (!IsHotKeyEnabled())
                    // 次の処理へイベントを渡す
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);

                if (nCode >= 0)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    // --- 1. キーが離された時の処理 ---
                    if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    {
                        if (IsTargetKey(vkCode))
                        {
                            _isKeyPressed = false; // 押し下げ状態を解除
                        }
                    }

                    // --- 2. キーが押された時の処理
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        if (IsTargetKey(vkCode))
                        {
                            // 既に押されている（長押し中）なら無視
                            if (_isKeyPressed)
                            {
                                // 次の処理へイベントを渡す
                                return CallNextHookEx(_hookID, nCode, wParam, lParam);
                            }

                            _isKeyPressed = true; // 押し下げ状態を記録

                            var now = DateTime.Now;
                            _pressCount = (now - _lastKeyTime).TotalMilliseconds <= DOUBLE_PRESS_MS
                                ? _pressCount + 1
                                : 1;

                            _lastKeyTime = now;

                            if (_pressCount >= GetShiftPressCount())
                            {
                                _launcher.BeginInvoke(new Action(ShowLauncher));
                                _pressCount = 0;
                                _lastKeyTime = DateTime.MinValue;
                            }
                        }
                        else
                        {
                            _pressCount = 0;
                            _lastKeyTime = DateTime.MinValue;
                        }

                    }

                }
            }
            catch (Exception ex)
            {
                // フック内で例外を握りつぶし、システム全体のキー入力やアプリ自体への
                // 影響を防ぐ（デバッグ時にのみ出力を確認できるようにしておく）
                Debug.WriteLine($"[WinSimpleFolderLauncher] HookCallback error: {ex}");
            }

            // 次の処理へイベントを渡す
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private class MessageWindow : NativeWindow
        {
            public event EventHandler LauncherRequested;
            public MessageWindow()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY &&
                    m.WParam.ToInt32() == HOTKEY_ID_CTRL_SHIFT_I)
                {
                    LauncherRequested?.Invoke(this, EventArgs.Empty);
                }
                base.WndProc(ref m);
            }
        }

        /// <summary>
        /// ターゲットのキーが押されたか判定
        /// </summary>
        /// <param name="vk"></param>
        /// <returns></returns>
        private static bool IsTargetKey(int vk)
        {
            return GetTriggerKey() switch
            {
                "Ctrl" => vk == 0xA2 || vk == 0xA3,
                "Alt" => vk == 0xA4 || vk == 0xA5,
                "Space" => vk == 0x20,
                _ => vk == VK_LSHIFT || vk == VK_RSHIFT
            };
        }

        /// <summary>
        /// アイコンを読み込む
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static Icon LoadIcon(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (File.Exists(path))
            {
                try { return new Icon(path); } catch { }
            }
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

    }
}
