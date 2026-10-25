using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WinSimpleFolderLauncher.Helpers;

namespace WinSimpleFolderLauncher.Forms
{
    public class HelpForm : Form
    {

        private const string GitHubRepoUrl = "https://github.com/takuyash/WinSimpleFolderLauncher";

        private const string HelpUrl = "https://takuyash.github.io/WinSimpleFolderLauncherSite/docs.html";

        private const string LicenseUrl = "https://github.com/takuyash/WinSimpleFolderLauncher/blob/main/LICENSE";

        // ===== レイアウト =====
        private const int FormWidth = 440;
        private const int HeaderHeight = 100;
        private const int CardMargin = 20;
        private const int CardHeight = 58;
        private const int CardGap = 10;
        private const int AppIconSize = 48;

        // ===== 配色（トレイメニューのダーク配色に合わせる） =====
        private static readonly TrayMenuPalette Palette = TrayMenuPalette.Dark;
        private static readonly Color HeaderBegin = Color.FromArgb(38, 52, 62);
        private static readonly Color HeaderEnd = Color.FromArgb(45, 45, 60);
        private static readonly Color UpdateAccent = Color.FromArgb(255, 160, 70);

        private static readonly Font TitleFont = new Font("Segoe UI Semibold", 14f);
        private static readonly Font BadgeFont = new Font("Segoe UI", 8.5f);

        private HelpLinkCard repoCard, usageCard, licenseCard;
        private HelpLinkCard _updateCard;
        private int contentBottom; // 最後のカードの下端

        public HelpForm()
        {
            Text = LanguageManager.GetString("HelpTitle");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            DoubleBuffered = true;
            KeyPreview = true;

            if (Program.AppIcon != null)
                Icon = Program.AppIcon;

            // ===== ダークテーマ =====
            BackColor = Palette.Background;
            ForeColor = Palette.Text;

            int y = HeaderHeight + 16;

            repoCard = AddCard(ref y, TrayMenuIcons.Code(Palette.Accent, 20), Palette.Accent, GitHubRepoUrl);
            usageCard = AddCard(ref y, TrayMenuIcons.Book(Palette.Accent, 20), Palette.Accent, HelpUrl);
            licenseCard = AddCard(ref y, TrayMenuIcons.Document(Palette.Accent, 20), Palette.Accent, LicenseUrl);

            contentBottom = y - CardGap;
            ClientSize = new Size(FormWidth, contentBottom + CardMargin);

            UpdateUI();

            // 非同期でチェック
            Load += async (s, e) => await CheckForUpdateAsync();

            LanguageManager.LanguageChanged += UpdateUI;
        }

        /// <summary>
        /// リンクカードを y の位置に追加し、y を次のカードの位置へ進める
        /// </summary>
        private HelpLinkCard AddCard(ref int y, Image icon, Color accent, string url)
        {
            var card = new HelpLinkCard(icon, accent, url)
            {
                Bounds = new Rectangle(CardMargin, y, FormWidth - CardMargin * 2, CardHeight),
                TabIndex = Controls.Count
            };
            Controls.Add(card);
            y += CardHeight + CardGap;
            return card;
        }

        private void UpdateUI()
        {
            Text = LanguageManager.GetString("HelpTitle");

            repoCard.Title = LanguageManager.GetString("HelpRepo");
            repoCard.Description = LanguageManager.GetString("HelpRepoDesc");
            usageCard.Title = LanguageManager.GetString("HelpUsage");
            usageCard.Description = LanguageManager.GetString("HelpUsageDesc");
            licenseCard.Title = LanguageManager.GetString("HelpLicense");
            licenseCard.Description = LanguageManager.GetString("HelpLicenseDesc");

            if (_updateCard != null)
                _updateCard.Description = LanguageManager.GetString("HelpUpdateDesc");
        }

        private string GetVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? $"v{v.ToString(3)}" : "Unknown";
        }

        /// <summary>
        /// タイトルバーをダークにする（Windows 10 20H1 以降で有効）
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            }
            catch
            {
                // 非対応環境では標準のタイトルバーのまま
            }
        }

        /// <summary>
        /// ヘッダー（アプリアイコン・アプリ名・バージョン）を描画する
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var header = new Rectangle(0, 0, ClientSize.Width, HeaderHeight);

            using (var brush = new LinearGradientBrush(header, HeaderBegin, HeaderEnd, 0f))
                g.FillRectangle(brush, header);

            // 下側にアクセントカラーの細いライン
            using (var brush = new LinearGradientBrush(header, Palette.Accent, Color.FromArgb(0, Palette.Accent), 0f))
                g.FillRectangle(brush, 0, header.Bottom - 2, header.Width, 2);

            int iconLeft = 24;
            int iconTop = (HeaderHeight - AppIconSize) / 2;
            if (Program.AppIcon != null)
            {
                using (var sized = new Icon(Program.AppIcon, AppIconSize, AppIconSize))
                    g.DrawIcon(sized, new Rectangle(iconLeft, iconTop, AppIconSize, AppIconSize));
            }

            int textLeft = iconLeft + AppIconSize + 16;
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, "WinSimpleFolderLauncher", TitleFont, new Point(textLeft, iconTop - 1), Palette.Text, flags);

            // バージョンのバッジ
            string version = GetVersion();
            Size textSize = TextRenderer.MeasureText(g, version, BadgeFont, Size.Empty, flags);
            var badge = new Rectangle(textLeft, iconTop + 30, textSize.Width + 16, textSize.Height + 6);
            using (var path = TrayMenuRenderer.CreateRoundedRect(badge, badge.Height / 2))
            using (var fill = new SolidBrush(Color.FromArgb(45, Palette.Accent)))
            using (var pen = new Pen(Color.FromArgb(120, Palette.Accent)))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, version, BadgeFont, new Point(badge.X + 8, badge.Y + 3), Palette.Accent, flags);
        }

        /// <summary>
        /// Esc で閉じる
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WinSimpleFolderLauncher");

                var json = await client.GetStringAsync(
                    "https://api.github.com/repos/takuyash/WinSimpleFolderLauncher/releases/latest");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tag = root.GetProperty("tag_name").GetString(); // v1.2.3
                var url = root.GetProperty("html_url").GetString();

                if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(url) || IsDisposed) return;

                var latest = new Version(tag.TrimStart('v', 'V'));
                var current = Assembly.GetExecutingAssembly().GetName().Version;

                if (current != null && latest > current)
                {
                    int y = contentBottom + CardGap + 6;
                    _updateCard = AddCard(ref y, TrayMenuIcons.Download(UpdateAccent, 20), UpdateAccent, url);
                    _updateCard.Title = string.Format(LanguageManager.GetString("HelpUpdate"), latest);
                    _updateCard.Description = LanguageManager.GetString("HelpUpdateDesc");
                    _updateCard.Emphasized = true;

                    ClientSize = new Size(FormWidth, _updateCard.Bottom + CardMargin);
                }
            }
            catch
            {
                // 失敗しても何もしない
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            LanguageManager.LanguageChanged -= UpdateUI;
            base.OnFormClosed(e);
        }
    }

    /// <summary>
    /// アイコン・タイトル・説明を持つクリック可能なカード（ヘルプ画面のリンク用）
    /// </summary>
    internal class HelpLinkCard : Control
    {
        private static readonly TrayMenuPalette Palette = TrayMenuPalette.Dark;
        private static readonly Color CardBack = Color.FromArgb(42, 42, 48);
        private static readonly Color CardHover = Color.FromArgb(50, 54, 62);
        private static readonly Color CardBorder = Color.FromArgb(58, 58, 66);

        private static readonly Font CardTitleFont = new Font("Meiryo UI", 10f, FontStyle.Bold);
        private static readonly Font CardDescFont = new Font("Meiryo UI", 8.5f);

        private readonly Image icon;
        private readonly Color accent;
        private readonly string url;
        private bool hovering;
        private string title = "";
        private string description = "";
        private bool emphasized;

        public string Title
        {
            get => title;
            set { title = value ?? ""; Invalidate(); }
        }

        public string Description
        {
            get => description;
            set { description = value ?? ""; Invalidate(); }
        }

        // true: アクセントカラーで枠線を常に表示する（アップデート通知用）
        public bool Emphasized
        {
            get => emphasized;
            set { emphasized = value; Invalidate(); }
        }

        public HelpLinkCard(Image icon, Color accent, string url)
        {
            this.icon = icon;
            this.accent = accent;
            this.url = url;

            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);

            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            OpenUrl();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OpenUrl();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);

        private void OpenUrl()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // ブラウザが起動できなくても何もしない
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Palette.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool active = hovering || Focused;

            Color border = emphasized || active ? Color.FromArgb(active ? 200 : 130, accent) : CardBorder;

            using (var path = TrayMenuRenderer.CreateRoundedRect(rect, 8))
            using (var fill = new SolidBrush(active ? CardHover : CardBack))
            using (var pen = new Pen(border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            // アイコン（アクセントカラーの丸い背景付き）
            const int circle = 36;
            var circleRect = new Rectangle(12, (Height - circle) / 2, circle, circle);
            using (var fill = new SolidBrush(Color.FromArgb(active ? 60 : 38, accent)))
                g.FillEllipse(fill, circleRect);
            if (icon != null)
            {
                g.DrawImage(icon,
                    circleRect.X + (circle - icon.Width) / 2,
                    circleRect.Y + (circle - icon.Height) / 2,
                    icon.Width, icon.Height);
            }

            // タイトルと説明
            int textLeft = circleRect.Right + 14;
            int textWidth = Width - textLeft - 36;
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

            TextRenderer.DrawText(g, title, CardTitleFont,
                new Rectangle(textLeft, 10, textWidth, 20), emphasized ? accent : Palette.Text, flags);
            TextRenderer.DrawText(g, description, CardDescFont,
                new Rectangle(textLeft, 32, textWidth, 18), Palette.SubText, flags);

            // 右端の矢印（ホバー時に少し右へ動かす）
            int arrowX = Width - 24 + (active ? 3 : 0);
            int midY = Height / 2;
            using (var pen = new Pen(active ? accent : Palette.SubText, 1.6f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(pen, new[] { new Point(arrowX - 4, midY - 5), new Point(arrowX + 1, midY), new Point(arrowX - 4, midY + 5) });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) icon?.Dispose();
            base.Dispose(disposing);
        }
    }
}
