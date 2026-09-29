using System.Diagnostics;
using System.Runtime.InteropServices;
using WinSimpleFolderLauncher.Helpers;

namespace WinSimpleFolderLauncher.Forms
{
    public class LauncherForm : Form
    {
        // ===== 共通 =====
        private ContextMenuStrip nodeContextMenu;
        private ToolStripMenuItem menuCopyPath; // 多言語化のため保持
        private ImageList iconList;             // アイコンリスト（両表示で共用）
        private Label lblNoPath;
        private string currentRootPath = "";    // ルートパス保持用
        private bool explorerMode = false;      // true: エクスプローラー風 / false: ツリー
        private bool? appliedExplorerMode = null; // 直近で見た目を適用したモード

        private static string IniFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        // ===== ツリー表示（ダーク） =====
        private TreeView fileTree;
        private Panel treeHost;           // ツリーの外側（検索ボックスとの余白用）
        private Panel treeBorder;         // ツリーを囲む枠
        private List<TreeNode> flatNodeList = new List<TreeNode>();
        private Panel searchPanel;        // 検索ボックス用パネル（アイコン＋テキストボックス）
        private PictureBox picSearchIcon; // 検索アイコン（虫眼鏡）
        private TextBox txtSearch;        // 検索ボックス

        // ===== エクスプローラー風表示 =====
        private ListView fileListView;
        private Panel navBar;
        private FlowLayoutPanel breadcrumbPanel;
        private TextBox txtExplorerSearch;
        private List<ListViewItem> flatItemList = new List<ListViewItem>();
        private string currentPath = "";  // 現在表示中のフォルダ（rootPath より上には行かない）

        private static readonly string ExplorerFontName = "Segoe UI";
        private static readonly Color NavBarSeparator = Color.FromArgb(229, 229, 229);
        private static readonly Color SearchBoxBack = Color.FromArgb(243, 243, 243);
        private static readonly Color SearchBoxBorder = Color.FromArgb(213, 213, 213);
        private static readonly Color TreeBorderColor = Color.FromArgb(90, 90, 90); // ツリー表示の枠線色

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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        /// <summary>
        /// ランチャーフォーム画面
        /// </summary>
        /// <param name="initialPath"></param>
        public LauncherForm(string initialPath = "")
        {
            Text = "WinSimpleFolderLauncher";
            Size = new Size(420, 600); // モードに応じて ApplyViewMode で調整
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            KeyPreview = true;

            // AppIconを使用
            if (Program.AppIcon != null)
                Icon = Program.AppIcon;

            // ImageListの初期化
            iconList = new ImageList();
            iconList.ColorDepth = ColorDepth.Depth32Bit;
            iconList.ImageSize = new Size(16, 16); // アイコンサイズ

            // ================================
            // 検索ボックス（ツリー表示用：アイコン付きパネル）
            // 枠線は Paint で自前描画（ツリーの枠と色を揃える）
            // ================================
            searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(4, 1, 2, 1)
            };
            searchPanel.Paint += (s, e) => DrawBorder(e.Graphics, searchPanel.ClientRectangle, TreeBorderColor);

            txtSearch = new TextBox
            {
                Dock = DockStyle.None, // 縦中央寄せのため手動レイアウト
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Meiryo UI", 10f),
                TabIndex = 1
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (!explorerMode) ReloadView(currentRootPath);
            };
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
                {
                    if (fileTree.Nodes.Count > 0) { fileTree.Focus(); e.Handled = true; }
                }
            };

            picSearchIcon = new PictureBox
            {
                Dock = DockStyle.Left,
                Width = 20,
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = CreateSearchIcon(Color.Gainsboro, 14)
            };

            searchPanel.Controls.Add(txtSearch);
            searchPanel.Controls.Add(picSearchIcon);

            // パネルのサイズ・フォントが変わったら、テキストボックスを縦中央に配置し直す
            searchPanel.Resize += (s, e) =>
            {
                searchPanel.Invalidate();
                LayoutCenteredTextBox(searchPanel, txtSearch, picSearchIcon.Width);
            };
            txtSearch.SizeChanged += (s, e) => LayoutCenteredTextBox(searchPanel, txtSearch, picSearchIcon.Width);
            LayoutCenteredTextBox(searchPanel, txtSearch, picSearchIcon.Width);

            // ================================
            // 上部ナビゲーションバー（エクスプローラー風表示用）
            // ================================
            BuildExplorerNavBar();

            // ================================
            // タスクトレイ
            // ================================
            trayMenu = new ContextMenuStrip();

            menuOpen = new ToolStripMenuItem("", null, (s, e) =>
            {
                // 他画面開いてたら起動しない
                if (IsOtherFormOpen())
                    return;

                Show();
                Activate();
                FocusMainControl();
            });

            menuSetting = new ToolStripMenuItem("", null, (s, e) =>
            {
                var settings = new SettingsForm(IniFilePath);
                settings.ShowDialog();
                ReloadView();
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
                Icon = Icon,
                Visible = true,
                Text = "WinSimpleFolderLauncher",
                ContextMenuStrip = trayMenu
            };

            trayIcon.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    trayMenu.Show(Cursor.Position);
            };

            // ================================
            // TreeView（ツリー表示）
            // treeHost（余白） > treeBorder（1px の枠） > fileTree
            // ================================
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

            treeBorder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TreeBorderColor, // これが枠線の色になる
                Padding = new Padding(1)
            };
            treeBorder.Controls.Add(fileTree);

            treeHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 0) // 検索ボックスとの間隔
            };
            treeHost.Controls.Add(treeBorder);

            // ================================
            // ListView（エクスプローラー風の「詳細」ビュー）
            // ================================
            fileListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                GridLines = false,
                BorderStyle = BorderStyle.None,
                SmallImageList = iconList,
                Font = new Font(ExplorerFontName, 9f),
                BackColor = Color.White,
                ForeColor = Color.Black,
                Visible = false
            };
            fileListView.Columns.Add("", 300, HorizontalAlignment.Left); // 名前
            fileListView.Columns.Add("", 150, HorizontalAlignment.Left); // 更新日時

            fileListView.ItemActivate += (s, e) =>
            {
                if (fileListView.SelectedItems.Count > 0)
                    OpenListItem(fileListView.SelectedItems[0]);
            };
            fileListView.KeyDown += FileListView_KeyDown;
            fileListView.MouseClick += FileListView_MouseClick;

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

            // Dock順に注意：Fill系を先に、Top系（検索/ナビバー）を後に追加する
            Controls.Add(treeHost);
            Controls.Add(fileListView);
            Controls.Add(lblNoPath);
            Controls.Add(searchPanel);
            Controls.Add(navBar);

            nodeContextMenu = new ContextMenuStrip();
            menuCopyPath = new ToolStripMenuItem("");
            menuCopyPath.Click += CopyPathItem_Click;
            nodeContextMenu.Items.Add(menuCopyPath);

            // 初期言語適用＆表示
            ReloadView(initialPath);

            Shown += (s, e) =>
            {
                BeginInvoke(new Action(ForceForeground));
                SetCueBanner(txtSearch, LanguageManager.GetString("SearchPlaceholder"));
                UpdateExplorerCueBanner();
            };

            VisibleChanged += (s, e) =>
            {
                if (!Visible) return;

                var mouseScreen = Screen.FromPoint(Cursor.Position);
                StartPosition = FormStartPosition.Manual;
                Location = new Point(
                    mouseScreen.Bounds.Left + (mouseScreen.Bounds.Width - Width) / 2,
                    mouseScreen.Bounds.Top + (mouseScreen.Bounds.Height - Height) / 2
                );
            };
        }

        /// <summary>
        /// 1px の枠線を描画する
        /// </summary>
        private static void DrawBorder(Graphics g, Rectangle bounds, Color color)
        {
            using (var pen = new Pen(color))
            {
                var r = bounds;
                r.Width -= 1;
                r.Height -= 1;
                g.DrawRectangle(pen, r);
            }
        }

        /// <summary>
        /// TextBox（枠なし）をホストパネルの縦中央に配置する。
        /// Dock.Fill だと上詰めになり文字が上にずれて見えるため、手動で中央寄せする。
        /// </summary>
        /// <param name="leftReserved">左側に確保する幅（アイコン分）</param>
        private static void LayoutCenteredTextBox(Panel host, TextBox tb, int leftReserved)
        {
            int left = host.Padding.Left + leftReserved;
            int width = host.ClientSize.Width - left - host.Padding.Right;
            if (width < 1) return;

            int top = (host.ClientSize.Height - tb.Height) / 2;
            if (top < 0) top = 0;

            tb.SetBounds(left, top, width, tb.Height);
        }

        /// <summary>
        /// エクスプローラー風の上部ナビゲーションバー（左:パンくず／右:検索ボックス）を作る
        /// </summary>
        private void BuildExplorerNavBar()
        {
            navBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.White,
                Padding = new Padding(6, 4, 6, 4),
                Visible = false
            };

            var navBarDivider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = NavBarSeparator
            };

            // --- 右側：検索ボックス ---
            var searchContainer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                Padding = new Padding(4, 2, 0, 2)
            };

            const int iconAreaWidth = 22;

            var searchBox = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SearchBoxBack,
                Padding = new Padding(4, 1, 4, 1)
            };
            searchBox.Paint += (s, e) => DrawBorder(e.Graphics, searchBox.ClientRectangle, SearchBoxBorder);

            var searchIconPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = iconAreaWidth,
                BackColor = Color.Transparent
            };
            searchIconPanel.Paint += SearchIcon_Paint;
            searchIconPanel.Resize += (s, e) => searchIconPanel.Invalidate();

            txtExplorerSearch = new TextBox
            {
                Dock = DockStyle.None, // 縦中央寄せのため手動レイアウト
                BackColor = SearchBoxBack,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.None,
                Font = new Font(ExplorerFontName, 9f),
                TabIndex = 1
            };
            txtExplorerSearch.TextChanged += (s, e) =>
            {
                if (explorerMode) PopulateList(txtExplorerSearch.Text);
            };
            txtExplorerSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
                {
                    if (fileListView.Items.Count > 0)
                    {
                        fileListView.Focus();
                        fileListView.Items[0].Selected = true;
                        fileListView.Items[0].Focused = true;
                        e.Handled = true;
                    }
                }
            };
            // ハンドル生成（再生成）時にプレースホルダーを設定し直す
            txtExplorerSearch.HandleCreated += (s, e) => UpdateExplorerCueBanner();

            searchBox.Controls.Add(txtExplorerSearch);
            searchBox.Controls.Add(searchIconPanel);
            searchContainer.Controls.Add(searchBox);

            // サイズ・フォント変更時に、文字を縦中央へ配置し直す
            searchBox.Resize += (s, e) =>
            {
                searchBox.Invalidate();
                LayoutCenteredTextBox(searchBox, txtExplorerSearch, iconAreaWidth);
            };
            txtExplorerSearch.SizeChanged += (s, e) =>
                LayoutCenteredTextBox(searchBox, txtExplorerSearch, iconAreaWidth);
            LayoutCenteredTextBox(searchBox, txtExplorerSearch, iconAreaWidth);

            // --- 左側：パンくずリスト ---
            breadcrumbPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White,
                Padding = new Padding(2, 8, 2, 0)
            };

            navBar.Controls.Add(breadcrumbPanel);
            navBar.Controls.Add(searchContainer);
            navBar.Controls.Add(navBarDivider);
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
        /// 現在の表示モードに応じたコントロールへフォーカスを移す
        /// </summary>
        private void FocusMainControl()
        {
            if (explorerMode) fileListView.Focus();
            else fileTree.Focus();
        }

        /// <summary>
        /// 表示モード（ツリー / エクスプローラー風）に応じて見た目を切り替える
        /// </summary>
        private void ApplyViewMode()
        {
            if (appliedExplorerMode == explorerMode) return;
            appliedExplorerMode = explorerMode;

            Size = explorerMode ? new Size(560, 620) : new Size(420, 600);
            BackColor = explorerMode ? Color.White : SystemColors.Control;

            // ツリー表示は枠が見えるようにフォーム周囲に余白を取る
            Padding = explorerMode ? Padding.Empty : new Padding(6);

            searchPanel.Visible = !explorerMode;
            navBar.Visible = explorerMode;
            if (explorerMode) treeHost.Visible = false;
            else fileListView.Visible = false;

            if (explorerMode)
            {
                lblNoPath.ForeColor = Color.Black;
                lblNoPath.BackColor = Color.White;

                trayMenu.Renderer = new ToolStripProfessionalRenderer();
                trayMenu.BackColor = SystemColors.Control;
                trayMenu.ForeColor = SystemColors.ControlText;
            }
            else
            {
                lblNoPath.ForeColor = Color.LightGray;
                lblNoPath.BackColor = Color.Transparent;

                trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
                trayMenu.BackColor = Color.FromArgb(35, 35, 35);
                trayMenu.ForeColor = Color.White;
            }

            Invalidate();
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
            fileListView.Columns[0].Text = LanguageManager.GetString("ColName");
            fileListView.Columns[1].Text = LanguageManager.GetString("ColModified");

            // ハンドル生成済みの場合のみ設定可能
            if (txtSearch.IsHandleCreated)
                SetCueBanner(txtSearch, LanguageManager.GetString("SearchPlaceholder"));
            UpdateExplorerCueBanner();
        }

        /// <summary>
        /// 検索ボックスの左に表示する虫眼鏡アイコンを生成する（ツリー表示用）
        /// </summary>
        private Bitmap CreateSearchIcon(Color color, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(color, 1.6f))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int circleSize = (int)(size * 0.62);
                g.DrawEllipse(pen, 1, 1, circleSize, circleSize);
                g.DrawLine(pen, circleSize - 1, circleSize - 1, size - 1, size - 1);
            }
            return bmp;
        }

        /// <summary>
        /// 検索ボックス左の虫眼鏡アイコンを描画する（エクスプローラー風表示用）
        /// パネルの中央に描画する
        /// </summary>
        private void SearchIcon_Paint(object sender, PaintEventArgs e)
        {
            var panel = (Control)sender;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            const int circle = 9;   // 円の直径
            const int handle = 4;   // 持ち手の長さ
            int total = circle + handle;

            int ox = (panel.Width - total) / 2;
            int oy = (panel.Height - total) / 2;

            using (var pen = new Pen(Color.FromArgb(120, 120, 120), 1.4f))
            {
                var rect = new Rectangle(ox, oy, circle, circle);
                g.DrawEllipse(pen, rect);
                g.DrawLine(pen, rect.Right - 1, rect.Bottom - 1, rect.Right + handle, rect.Bottom + handle);
            }
        }

        /// <summary>
        /// TextBoxにネイティブのプレースホルダー（Cue Banner）を設定する
        /// </summary>
        /// <param name="showOnFocus">true: フォーカス中も表示 / false: フォーカスで消える</param>
        private void SetCueBanner(TextBox tb, string text, bool showOnFocus = true)
        {
            SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)(showOnFocus ? 1 : 0), text);
        }

        /// <summary>
        /// エクスプローラー風の検索ボックスのプレースホルダーを「(フォルダ名)の検索」に更新する
        /// </summary>
        private void UpdateExplorerCueBanner()
        {
            if (!txtExplorerSearch.IsHandleCreated || string.IsNullOrEmpty(currentPath)) return;

            string folderName = Path.GetFileName(currentPath.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(folderName)) folderName = currentPath;

            SetCueBanner(
                txtExplorerSearch,
                string.Format(LanguageManager.GetString("SearchPlaceholderIn"), folderName),
                false);
        }

        private void ForceForeground()
        {
            IntPtr fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
            uint thisThread = GetWindowThreadProcessId(Handle, IntPtr.Zero);

            // フォアグラウンドスレッドと一時的に結合
            AttachThreadInput(thisThread, fgThread, true);

            TopMost = true;
            Show();
            SetForegroundWindow(Handle);
            Activate();
            BringToFront();
            TopMost = false;

            // 結合解除
            AttachThreadInput(thisThread, fgThread, false);

            FocusMainControl();
        }

        /// <summary>
        /// Esc で LauncherForm を閉じる（Hide）
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
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
            if (keyData == Keys.Enter)
            {
                if (!explorerMode && fileTree.Focused)
                {
                    if (fileTree.SelectedNode != null)
                        OpenFileOrFolder(fileTree.SelectedNode);
                    return true;
                }

                if (explorerMode && fileListView.Focused)
                {
                    if (fileListView.SelectedItems.Count > 0)
                        OpenListItem(fileListView.SelectedItems[0]);
                    return true;
                }
            }

            if (keyData == Keys.Escape)
            {
                Hide();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// config.ini を読み直し、表示モードに応じて再構築する
        /// </summary>
        private void ReloadView(string rootPath = "")
        {
            // 設定変更後の言語を反映
            LanguageManager.LoadSettings();

            var ini = IniHelper.ReadIni(IniFilePath);

            // 表示モード（ViewStyle=Explorer のときだけエクスプローラー風）
            explorerMode = ini.TryGetValue("ViewStyle", out string style)
                && string.Equals(style, "Explorer", StringComparison.OrdinalIgnoreCase);
            ApplyViewMode();
            UpdateUILanguage();

            // フォントサイズ・比率設定の反映
            float fontSize = 10f;
            if (ini.ContainsKey("FontSize") && float.TryParse(ini["FontSize"], out float fs)) fontSize = fs;
            float ratio = fontSize / 10f;
            int iconSize = (int)(16 * ratio);

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                rootPath = ini.ContainsKey("LauncherFolder") ? ini["LauncherFolder"] : "";
            }
            currentRootPath = rootPath;

            iconList.Images.Clear(); // リロード時にアイコンキャッシュもクリア
            iconList.ImageSize = new Size(iconSize, iconSize);

            if (explorerMode) ReloadExplorerView(fontSize);
            else ReloadTreeView(fontSize, ratio);
        }

        /// <summary>
        /// ツリー表示のリロード
        /// </summary>
        private void ReloadTreeView(float fontSize, float ratio)
        {
            fileListView.Items.Clear();
            flatItemList.Clear();

            fileTree.BeginUpdate(); // 描画停止で高速化
            fileTree.Nodes.Clear();
            flatNodeList.Clear();

            Font newFont = new Font("Meiryo UI", fontSize);
            fileTree.Font = newFont;
            txtSearch.Font = newFont;
            lblNoPath.Font = newFont;
            fileTree.ItemHeight = (int)(20 * ratio); // 比率に応じて高さを調整

            if (string.IsNullOrWhiteSpace(currentRootPath) || !Directory.Exists(currentRootPath))
            {
                treeHost.Visible = false;
                lblNoPath.Visible = true;
                fileTree.EndUpdate();
                return;
            }

            treeHost.Visible = true;
            lblNoPath.Visible = false;

            LoadFolder(currentRootPath, fileTree.Nodes, true, txtSearch.Text.ToLower()); // 第4引数でフィルタ

            // ★ キー番号（"0: " など）をノード文字列に付与する処理は、
            //    EndUpdate の前（＝ツリーのレイアウト確定前）に行う。
            //    描画後に Text を変えると、ノードの幅が古いままになり文字が切れる原因になる。
            BuildFlatNodeList(fileTree.Nodes);

            fileTree.EndUpdate();

            if (fileTree.Nodes.Count > 0 && fileTree.SelectedNode == null)
            {
                fileTree.SelectedNode = fileTree.Nodes[0];
            }

            if (!string.IsNullOrWhiteSpace(txtSearch.Text)) fileTree.ExpandAll();
        }

        /// <summary>
        /// エクスプローラー風表示のリロード（ルートフォルダから表示し直す）
        /// </summary>
        private void ReloadExplorerView(float fontSize)
        {
            fileTree.Nodes.Clear();
            flatNodeList.Clear();

            Font newFont = new Font(ExplorerFontName, fontSize * 0.9f);
            fileListView.Font = newFont;
            txtExplorerSearch.Font = newFont;
            breadcrumbPanel.Font = newFont;
            lblNoPath.Font = new Font(ExplorerFontName, fontSize);

            if (string.IsNullOrWhiteSpace(currentRootPath) || !Directory.Exists(currentRootPath))
            {
                fileListView.Visible = false;
                navBar.Visible = false;
                lblNoPath.Visible = true;
                return;
            }

            fileListView.Visible = true;
            navBar.Visible = true;
            lblNoPath.Visible = false;

            NavigateTo(currentRootPath);
        }

        /// <summary>
        /// 指定フォルダへ移動し、パンくずリストと一覧を更新する
        /// </summary>
        private void NavigateTo(string path)
        {
            if (!Directory.Exists(path)) return;

            currentPath = path;
            txtExplorerSearch.Text = ""; // フォルダ移動時は絞り込みをリセット
            RebuildBreadcrumb();
            UpdateExplorerCueBanner();
            PopulateList("");
        }

        /// <summary>
        /// パンくずリストを ルート 〜 currentPath の範囲で組み立てる
        /// </summary>
        private void RebuildBreadcrumb()
        {
            breadcrumbPanel.SuspendLayout();
            foreach (Control c in breadcrumbPanel.Controls) c.Dispose();
            breadcrumbPanel.Controls.Clear();

            var crumbs = new List<(string name, string path)>();
            string rootName = Path.GetFileName(currentRootPath.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(rootName)) rootName = currentRootPath;
            crumbs.Add((rootName, currentRootPath));

            string rel = currentPath.Length > currentRootPath.Length
                ? currentPath.Substring(currentRootPath.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : "";

            if (!string.IsNullOrEmpty(rel))
            {
                var parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                string acc = currentRootPath;
                foreach (var part in parts)
                {
                    acc = Path.Combine(acc, part);
                    crumbs.Add((part, acc));
                }
            }

            for (int i = 0; i < crumbs.Count; i++)
            {
                bool isLast = (i == crumbs.Count - 1);
                var crumb = crumbs[i];

                if (i > 0)
                {
                    var sep = new Label
                    {
                        Text = ">",
                        AutoSize = true,
                        Margin = new Padding(4, 3, 4, 0),
                        ForeColor = Color.FromArgb(140, 140, 140),
                        Font = new Font(ExplorerFontName, 9f)
                    };
                    breadcrumbPanel.Controls.Add(sep);
                }

                var link = new LinkLabel
                {
                    Text = crumb.name,
                    AutoSize = true,
                    Margin = new Padding(0, 3, 0, 0),
                    LinkColor = isLast ? Color.Black : Color.FromArgb(0, 102, 204),
                    ActiveLinkColor = Color.FromArgb(0, 78, 161),
                    VisitedLinkColor = isLast ? Color.Black : Color.FromArgb(0, 102, 204),
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    Enabled = !isLast,
                    Font = new Font(ExplorerFontName, 9f)
                };
                string targetPath = crumb.path;
                link.LinkClicked += (s, e) => NavigateTo(targetPath);
                breadcrumbPanel.Controls.Add(link);
            }

            breadcrumbPanel.ResumeLayout();
        }

        /// <summary>
        /// 現在のフォルダの内容を一覧表示する（filter で名前を絞り込み）
        /// </summary>
        private void PopulateList(string filter)
        {
            fileListView.BeginUpdate();
            fileListView.Items.Clear();
            flatItemList.Clear();

            if (!Directory.Exists(currentPath))
            {
                fileListView.EndUpdate();
                return;
            }

            filter = (filter ?? "").ToLower();

            string[] directories;
            string[] files;
            try
            {
                directories = Directory.GetDirectories(currentPath)
                    .Where(d => !IsProtectedFolder(d))
                    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (UnauthorizedAccessException) { directories = new string[0]; }
            catch (IOException) { directories = new string[0]; }

            try
            {
                files = Directory.GetFiles(currentPath)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (UnauthorizedAccessException) { files = new string[0]; }
            catch (IOException) { files = new string[0]; }

            int index = 0;

            foreach (var dir in directories)
            {
                string dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(filter) && dirName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var item = CreateListItem(dir, dirName, index, isDirectory: true);
                fileListView.Items.Add(item);
                flatItemList.Add(item);
                index++;
            }

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(filter) && fileName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var item = CreateListItem(file, fileName, index, isDirectory: false);
                fileListView.Items.Add(item);
                flatItemList.Add(item);
                index++;
            }

            fileListView.EndUpdate();
        }

        private ListViewItem CreateListItem(string fullPath, string displayName, int index, bool isDirectory)
        {
            string keyLabel;
            if (index < 10) keyLabel = $"{index}: ";
            else if (index < 36) keyLabel = $"{(char)('A' + index - 10)}: ";
            else keyLabel = "";

            DateTime updated = isDirectory ? Directory.GetLastWriteTime(fullPath) : File.GetLastWriteTime(fullPath);

            var item = new ListViewItem(keyLabel + displayName)
            {
                Tag = fullPath
            };
            item.SubItems.Add(updated.ToString("yyyy/MM/dd HH:mm"));
            item.ImageKey = GetIconKey(fullPath);

            return item;
        }

        /// <summary>
        /// アイコンを ImageList にキャッシュし、キー(=パス)を返す（エクスプローラー風表示用）
        /// </summary>
        private string GetIconKey(string path)
        {
            if (iconList.Images.ContainsKey(path))
                return path;

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
                    using (Icon icon = Icon.FromHandle(shinfo.hIcon))
                    {
                        iconList.Images.Add(path, new Bitmap(icon.ToBitmap(), iconList.ImageSize));
                    }
                    return path;
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }

            return null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
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

        private void FileListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = fileListView.GetItemAt(e.X, e.Y);
                if (item != null)
                {
                    item.Selected = true;
                    nodeContextMenu.Show(fileListView, e.Location);
                }
            }
        }

        private void CopyPathItem_Click(object sender, EventArgs e)
        {
            string path = null;

            if (explorerMode)
            {
                if (fileListView.SelectedItems.Count > 0)
                    path = fileListView.SelectedItems[0].Tag as string;
            }
            else
            {
                path = fileTree.SelectedNode?.Tag as string;
            }

            if (path != null)
                Clipboard.SetText(path);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // エクスプローラー風は白一色（BackColor）
            if (explorerMode)
            {
                base.OnPaintBackground(e);
                return;
            }

            using (var brush =
                new System.Drawing.Drawing2D.LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(30, 30, 30),
                    Color.FromArgb(45, 45, 60),
                    90f))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        /// <summary>
        /// フォーカスが他アプリへ移動したら LauncherForm を閉じる（Hide）
        /// </summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible)
            {
                Hide();
            }
        }

        /// <summary>
        /// フォルダを再帰的に読み込む（ツリー表示用）
        /// </summary>
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
        /// アイコン設定（ツリー表示用）
        /// </summary>
        private void SetNodeIcon(TreeNode node, string path)
        {
            string key = GetIconKey(path);
            if (key != null)
            {
                node.ImageKey = key;
                node.SelectedImageKey = key;
            }
        }

        /// <summary>
        /// ツリーのノード描画。
        /// TextRenderer の既定パディングを無効化し、実際の文字幅に合わせて描画領域を広げることで、
        /// 文字が右端で切れないようにする。
        /// </summary>
        private void FileTree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.Width <= 0 || e.Bounds.Height <= 0)
                return;

            Font font = e.Node.NodeFont ?? e.Node.TreeView.Font;

            const TextFormatFlags flags =
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.Left |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine;

            // 実際に必要な文字幅を測る
            Size textSize = TextRenderer.MeasureText(
                e.Graphics,
                e.Node.Text,
                font,
                new Size(int.MaxValue, e.Bounds.Height),
                flags);

            // ノードの Bounds が実際の文字より狭くても、文字が入る幅を確保する
            int width = Math.Max(e.Bounds.Width, textSize.Width + 6);
            var drawRect = new Rectangle(e.Bounds.X, e.Bounds.Y, width, e.Bounds.Height);

            if (e.Node.IsSelected)
            {
                // 選択時の背景色描画
                e.Graphics.FillRectangle(Brushes.DarkCyan, drawRect);
            }

            var textRect = new Rectangle(drawRect.X + 2, drawRect.Y, drawRect.Width - 2, drawRect.Height);

            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                font,
                textRect,
                Color.White,
                flags);
        }

        private void FileTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            OpenFileOrFolder(e.Node);
        }

        private void FileTree_KeyDown(object sender, KeyEventArgs e)
        {
            // 上キーで検索ボックスに戻る
            if (e.KeyCode == Keys.Up && fileTree.Nodes.Count > 0 && fileTree.SelectedNode == fileTree.Nodes[0])
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

        private void FileListView_KeyDown(object sender, KeyEventArgs e)
        {
            // 先頭項目でUpキー -> 検索ボックスへ戻る
            if (e.KeyCode == Keys.Up && fileListView.SelectedIndices.Count > 0 && fileListView.SelectedIndices[0] == 0)
            {
                txtExplorerSearch.Focus();
                e.Handled = true;
                return;
            }

            // Backspace -> 一つ上のフォルダへ（ルートより上へは行かない）
            if (e.KeyCode == Keys.Back)
            {
                if (!string.Equals(currentPath.TrimEnd(Path.DirectorySeparatorChar),
                                   currentRootPath.TrimEnd(Path.DirectorySeparatorChar),
                                   StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Directory.GetParent(currentPath)?.FullName;
                    if (parent != null) NavigateTo(parent);
                }
                e.Handled = true;
                return;
            }

            int index = -1;

            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)            // メイン数字キー
                index = e.KeyCode - Keys.D0;
            else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9) // テンキー
                index = e.KeyCode - Keys.NumPad0;
            else if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)         // A-Z
                index = 10 + (e.KeyCode - Keys.A);

            if (index >= 0)
            {
                if (index < flatItemList.Count)
                    OpenListItem(flatItemList[index]);
                e.Handled = true;
                e.SuppressKeyPress = true; // ListViewの頭文字ジャンプを抑止
            }
        }

        /// <summary>
        /// ファイルかフォルダを開く（ツリー表示用）
        /// </summary>
        private void OpenFileOrFolder(TreeNode node)
        {
            string path = node.Tag as string;

            if (File.Exists(path))
            {
                StartFile(path);
            }
            else if (Directory.Exists(path))
            {
                node.Toggle();
            }
        }

        /// <summary>
        /// ファイルなら起動、フォルダなら中へ移動する（エクスプローラー風表示用）
        /// </summary>
        private void OpenListItem(ListViewItem item)
        {
            string path = item.Tag as string;

            if (File.Exists(path))
            {
                StartFile(path);
            }
            else if (Directory.Exists(path))
            {
                NavigateTo(path);
            }
        }

        private void StartFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LanguageManager.GetString("MsgSaveFailed")}{ex.Message}");
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