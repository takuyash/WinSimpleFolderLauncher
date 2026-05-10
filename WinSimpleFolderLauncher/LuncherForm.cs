using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StylishLauncherINI
{
    public static class IniHelper
    {
        public static Dictionary<string, string> ReadIni(string path)
        {
            var result = new Dictionary<string, string>();
            if (!File.Exists(path)) return result;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.StartsWith(";") || trimmed.StartsWith("[")) continue;

                var kv = trimmed.Split(new char[] { '=' }, 2);
                if (kv.Length == 2)
                    result[kv[0].Trim()] = kv[1].Trim();
            }
            return result;
        }
    }

    // アイコン取得用のWin32 API定義
    public static class NativeMethods
    {
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SMALLICON = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }

    public class LauncherForm : Form
    {
        private TreeView fileTree;
        private ContextMenuStrip nodeContextMenu;
        private ToolStripMenuItem menuCopyPath; // 多言語化のため保持
        private List<TreeNode> flatNodeList = new List<TreeNode>();
        private ImageList iconList; // アイコンリスト
        private Label lblNoPath;
        private TextBox txtSearch; // 検索ボックス
        private string currentRootPath = ""; // ルートパス保持用

        // タスクトレイ
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem menuOpen, menuSetting, menuHelp, menuExit; // 多言語化のため保持


        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// ランチャーフォーム画面
        /// </summary>
        /// <param name="initialPath"></param>
        public LauncherForm(string initialPath = "")
        {
            this.Text = "WinSimpleFolderLauncher";
            this.Size = new Size(420, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;

            // AppIconを使用
            if (Program.AppIcon != null)
                this.Icon = Program.AppIcon;

            iconList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(16, 16)
            };

            // ImageListの初期化
            iconList = new ImageList();
            iconList.ColorDepth = ColorDepth.Depth32Bit;
            iconList.ImageSize = new Size(16, 16); // アイコンサイズ

            // ================================
            // 検索ボックス
            // ================================
            txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Meiryo UI", 10f),
                TabIndex = 1
            };
            txtSearch.TextChanged += (s, e) => ReloadTree(currentRootPath);
            txtSearch.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
                {
                    if (fileTree.Nodes.Count > 0) { fileTree.Focus(); e.Handled = true; }
                }
            };

            // ================================
            // タスクトレイ
            // ================================
            trayMenu = new ContextMenuStrip();
            trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            trayMenu.BackColor = Color.FromArgb(35, 35, 35);
            trayMenu.ForeColor = Color.White;

            menuOpen = new ToolStripMenuItem("", null, (s, e) =>
            {
                // 他画面開いてたら起動しない
                if (IsOtherFormOpen())
                    return;

                this.Show();
                this.Activate();
                fileTree.Focus();
            });

            menuSetting = new ToolStripMenuItem("", null, (s, e) =>
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

                var settings = new SettingsForm(iniPath);
                settings.ShowDialog();
                ReloadTree();
            });

            menuHelp = new ToolStripMenuItem("", null, (s, e) =>
            {
                using (var help = new HelpForm())
                {
                    help.ShowDialog();
                }
            });

            menuExit = new ToolStripMenuItem("", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayMenu.Items.Add(menuOpen);
            trayMenu.Items.Add(menuSetting);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(menuHelp);
            trayMenu.Items.Add(menuExit);

            trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Visible = true,
                Text = "WinSimpleFolderLauncher",
                ContextMenuStrip = trayMenu
            };

            trayIcon.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    trayMenu.Show(Cursor.Position);
            };


            // TreeView
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                DrawMode = TreeViewDrawMode.OwnerDrawText,
                HideSelection = false,
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.None,
                ImageList = iconList, // ImageListを紐づけする
                ShowLines = false,
                ShowPlusMinus = true,
                TabIndex = 0
            };

            fileTree.DrawNode += FileTree_DrawNode;
            fileTree.NodeMouseDoubleClick += FileTree_NodeMouseDoubleClick;
            fileTree.KeyDown += FileTree_KeyDown;
            fileTree.NodeMouseClick += FileTree_NodeMouseClick;

            // パス未設定時メッセージ
            lblNoPath = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Meiryo UI", 10f),
                Visible = false
            };

            this.Controls.Add(fileTree);
            this.Controls.Add(txtSearch);
            this.Controls.Add(lblNoPath);

            nodeContextMenu = new ContextMenuStrip();
            menuCopyPath = new ToolStripMenuItem("");
            menuCopyPath.Click += CopyPathItem_Click;
            nodeContextMenu.Items.Add(menuCopyPath);

            // 初期言語適用
            UpdateUILanguage();
            ReloadTree(initialPath);


            this.Shown += (s, e) =>
            {
                BeginInvoke(new Action(ForceForeground));
            };

            this.VisibleChanged += (s, e) =>
            {
                if (!this.Visible) return;

                var mouseScreen = Screen.FromPoint(Cursor.Position);
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(
                    mouseScreen.Bounds.Left + (mouseScreen.Bounds.Width - this.Width) / 2,
                    mouseScreen.Bounds.Top + (mouseScreen.Bounds.Height - this.Height) / 2
                );
            };
        }

        /// <summary>
        /// 他の画面が開かれているときはランチャー画面は表示しない
        /// </summary>
        /// <returns></returns>
        private bool IsOtherFormOpen()
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f == this) continue;     // Launcher自身は除外
                if (f.Visible) return true;  // 他フォーム表示中
            }
            return false;
        }

        /// <summary>
        /// UIの表示文字列を現在の言語設定に更新する
        /// </summary>
        private void UpdateUILanguage()
        {
            menuOpen.Text = LanguageManager.GetString("MenuOpen");
            menuSetting.Text = LanguageManager.GetString("MenuSetting");
            menuHelp.Text = LanguageManager.GetString("MenuHelp");
            menuExit.Text = LanguageManager.GetString("MenuExit");
            menuCopyPath.Text = LanguageManager.GetString("MenuCopyPath");
            lblNoPath.Text = LanguageManager.GetString("LauncherNoPath");
        }

        private void ForceForeground()
        {
            IntPtr fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
            uint thisThread = GetWindowThreadProcessId(this.Handle, IntPtr.Zero);

            // フォアグラウンドスレッドと一時的に結合
            AttachThreadInput(thisThread, fgThread, true);

            this.TopMost = true;
            this.Show();
            SetForegroundWindow(this.Handle);
            this.Activate();
            this.BringToFront();
            this.TopMost = false;

            // 結合解除
            AttachThreadInput(thisThread, fgThread, false);

            fileTree.Focus();
        }

        /// <summary>
        /// Esc で LauncherForm を閉じる（Hide）
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {

            if (keyData == Keys.Enter && fileTree.Focused)
            {
                if (fileTree.SelectedNode != null)
                {
                    OpenFileOrFolder(fileTree.SelectedNode);
                }
                return true;
            }

            if (keyData == Keys.Escape)
            {
                this.Hide();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// リロード処理
        /// </summary>
        /// <param name="rootPath"></param>
        private void ReloadTree(string rootPath = "")
        {
            // 設定変更後の言語を反映
            LanguageManager.LoadSettings();
            UpdateUILanguage();

            fileTree.BeginUpdate(); // 描画停止で高速化
            fileTree.Nodes.Clear();
            flatNodeList.Clear();
            iconList.Images.Clear(); // リロード時にアイコンキャッシュもクリア

            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            var ini = IniHelper.ReadIni(iniPath);

            // フォントサイズ・比率設定の反映
            float fontSize = 10f;
            if (ini.ContainsKey("FontSize") && float.TryParse(ini["FontSize"], out float fs)) fontSize = fs;

            float ratio = fontSize / 10f;
            int iconSize = (int)(16 * ratio);

            Font newFont = new Font("Meiryo UI", fontSize);
            fileTree.Font = newFont;
            txtSearch.Font = newFont;
            lblNoPath.Font = newFont;
            fileTree.ItemHeight = (int)(20 * ratio); // 比率に応じて高さを調整
            iconList.ImageSize = new Size(iconSize, iconSize);

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";
            }
            currentRootPath = rootPath;

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                fileTree.Visible = false;
                lblNoPath.Visible = true;
                fileTree.EndUpdate();
                return;
            }

            fileTree.Visible = true;
            lblNoPath.Visible = false;

            LoadFolder(rootPath, fileTree.Nodes, true, txtSearch.Text.ToLower()); // 第4引数でフィルタ
            fileTree.EndUpdate();

            BuildFlatNodeList(fileTree.Nodes);

            if (fileTree.Nodes.Count > 0 && fileTree.SelectedNode == null)
            {
                fileTree.SelectedNode = fileTree.Nodes[0];
            }

            if (!string.IsNullOrWhiteSpace(txtSearch.Text)) fileTree.ExpandAll();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        private void BuildFlatNodeList(TreeNodeCollection nodes)
        {
            AddNodesToFlatList(nodes, 0);
        }

        private int AddNodesToFlatList(TreeNodeCollection nodes, int index)
        {
            foreach (TreeNode node in nodes)
            {
                string path = node.Tag as string;
                if (File.Exists(path) || Directory.Exists(path))
                {
                    string originalName = Path.GetFileName(path);
                    string keyLabel;
                    if (index < 10) keyLabel = $"{index}: ";
                    else if (index < 36) keyLabel = $"{(char)('A' + index - 10)}: ";
                    else keyLabel = "    ";

                    node.Text = keyLabel + originalName;
                    flatNodeList.Add(node);
                    index++;
                }

                if (node.Nodes.Count > 0)
                    index = AddNodesToFlatList(node.Nodes, index);
            }
            return index;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                fileTree.SelectedNode = e.Node;
                nodeContextMenu.Show(fileTree, e.Location);
            }
        }

        private void CopyPathItem_Click(object sender, EventArgs e)
        {
            if (fileTree.SelectedNode?.Tag != null)
                Clipboard.SetText(fileTree.SelectedNode.Tag.ToString());
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush =
                new System.Drawing.Drawing2D.LinearGradientBrush(
                    this.ClientRectangle,
                    Color.FromArgb(30, 30, 30),
                    Color.FromArgb(45, 45, 60),
                    90f))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }
        }

        /// <summary>
        /// フォーカスが他アプリへ移動したら LauncherForm を閉じる（Hide）
        /// </summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (this.Visible)
            {
                this.Hide();
            }
        }

        /// <summary>
        /// フォルダを再帰的に読み込む
        /// </summary>
        /// <param name="path"></param>
        /// <param name="parentNodes"></param>
        /// <param name="recursive"></param>
        /// <param name="filter"></param>
        private void LoadFolder(
            string path,
            TreeNodeCollection parentNodes,
            bool recursive,
            string filter = "")
        {
            // システム・保護フォルダは最初から除外
            if (IsProtectedFolder(path))
                return;

            // --- フォルダ ---
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            foreach (var dir in directories)
            {
                string dirName = Path.GetFileName(dir);
                var folderNode = new TreeNode(dirName)
                {
                    Tag = dir,
                    ForeColor = Color.LightSkyBlue
                };

                // 子要素（ここも個別に安全化）
                if (recursive)
                {
                    LoadFolder(dir, folderNode.Nodes, recursive, filter);
                }

                // フィルタ判定
                bool match =
                    string.IsNullOrEmpty(filter) ||
                    dirName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    folderNode.Nodes.Count > 0;

                if (match)
                {
                    SetNodeIcon(folderNode, dir);
                    parentNodes.Add(folderNode);
                }
            }

            // --- ファイル ---
            string[] files;
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                if (string.IsNullOrEmpty(filter) ||
                    fileName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var fileNode = new TreeNode(fileName)
                    {
                        Tag = file,
                        ForeColor = Color.FromArgb(224, 224, 224)
                    };

                    SetNodeIcon(fileNode, file);
                    parentNodes.Add(fileNode);
                }
            }
        }

        /// <summary>
        /// アイコン設定
        /// </summary>
        /// <param name="node"></param>
        /// <param name="path"></param>
        private void SetNodeIcon(TreeNode node, string path)
        {
            NativeMethods.SHFILEINFO shinfo = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON;

            IntPtr hImg = NativeMethods.SHGetFileInfo(
                path,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags);

            if (hImg != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    if (!iconList.Images.ContainsKey(path))
                    {
                        using (Icon icon = Icon.FromHandle(shinfo.hIcon))
                        {
                            // ImageList.ImageSizeに合わせてリサイズして追加
                            iconList.Images.Add(path, new Bitmap(icon.ToBitmap(), iconList.ImageSize));
                        }
                    }
                    node.ImageKey = path;
                    node.SelectedImageKey = path;
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }
        }

        /// <summary>
        /// FileTree_DrawNode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileTree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {

            if (e.Node.IsSelected)
            {
                // 選択時の背景色描画
                e.Graphics.FillRectangle(Brushes.DarkCyan, e.Bounds);
            }

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                e.Node.TreeView.Font,
                e.Bounds, // テキスト領域に描画
                Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        /// <summary>
        /// FileTree_NodeMouseDoubleClick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            OpenFileOrFolder(e.Node);
        }

        private void FileTree_KeyDown(object sender, KeyEventArgs e)
        {
            // 上キーで検索ボックスに戻る
            if (e.KeyCode == Keys.Up && fileTree.SelectedNode == fileTree.Nodes[0])
            {
                txtSearch.Focus();
                e.Handled = true;
                return;
            }

            // メイン数字キー
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                int index = e.KeyCode - Keys.D0;
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
            }

            // テンキー
            if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
            {
                int index = e.KeyCode - Keys.NumPad0;
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
            }

            // A-Z
            if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
            {
                int index = 10 + (e.KeyCode - Keys.A);
                if (index < flatNodeList.Count)
                    OpenFileOrFolder(flatNodeList[index]);
                e.Handled = true;
                return;
            }
        }


        /// <summary>
        /// ファイルかフォルダを開く
        /// </summary>
        /// <param name="node"></param>
        private void OpenFileOrFolder(TreeNode node)
        {
            string path = node.Tag as string;

            if (File.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                    this.Hide();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{LanguageManager.GetString("MsgSaveFailed")}{ex.Message}");
                }
            }
            else if (Directory.Exists(path))
            {
                node.Toggle();
            }
        }

        private bool IsProtectedFolder(string path)
        {
            string name = Path.GetFileName(path);

            return name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase);
        }

    }


    public static class TreeNodeExtensions
    {
        public static void Toggle(this TreeNode node)
        {
            if (node.IsExpanded) node.Collapse();
            else node.Expand();
        }
    }

    // ダークテーマ用 ToolStrip
    internal class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(60, 60, 60);
        public override Color MenuItemBorder => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelected => Color.FromArgb(70, 130, 140);
        public override Color ToolStripDropDownBackground => Color.FromArgb(35, 35, 35);
        public override Color ImageMarginGradientBegin => Color.FromArgb(35, 35, 35);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(35, 35, 35);
        public override Color ImageMarginGradientEnd => Color.FromArgb(35, 35, 35);
    }

}