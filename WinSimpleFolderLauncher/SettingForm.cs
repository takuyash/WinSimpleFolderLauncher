using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Drawing;

namespace WinSimpleFolderLauncherINI
{
    /// <summary>
    /// 設定画面
    /// </summary>
    public class SettingsForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private Button btnSave;
        private NumericUpDown numFontSize;
        private ComboBox cmbLang;
        private CheckBox chkEnableHotKey;
        private Label lblPath;
        private Label lblFont;
        private Label lblLang;

        private string iniPath;

        private ComboBox cmbLaunchKeyCount;
        private Label lblLaunchKeyCount;

        private ComboBox cmbTriggerKey;
        private Label lblTriggerKey;

        public SettingsForm(string iniFilePath)
        {
            iniPath = iniFilePath;

            this.Text = LanguageManager.GetString("SettingTitle");
            this.Size = new System.Drawing.Size(450, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // ===== ダークテーマ =====
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            Font uiFont = new Font("Meiryo UI", 9f);

            lblPath = new Label()
            {
                Text = LanguageManager.GetString("SettingPath"),
                Left = 10,
                Top = 10,
                Width = 400,
                ForeColor = Color.Gainsboro,
                Font = uiFont
            };
            this.Controls.Add(lblPath);

            txtPath = new TextBox()
            {
                Left = 10,
                Top = 35,
                Width = 320,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            this.Controls.Add(txtPath);

            btnBrowse = new Button()
            {
                Text = LanguageManager.GetString("SettingBrowse"),
                Left = 340,
                Top = 33,
                Width = 80,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            lblFont = new Label()
            {
                Text = LanguageManager.GetString("SettingFontSize"),
                Left = 10,
                Top = 75,
                Width = 80,
                ForeColor = Color.Gainsboro,
                Font = uiFont
            };
            this.Controls.Add(lblFont);

            numFontSize = new NumericUpDown()
            {
                Left = 110,
                Top = 73,
                Width = 60,
                Minimum = 8,
                Maximum = 20,
                Value = 10,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            this.Controls.Add(numFontSize);

            lblLang = new Label()
            {
                Text = LanguageManager.GetString("SettingLang"),
                Left = 10,
                Top = 115,
                Width = 100,
                ForeColor = Color.Gainsboro,
                Font = uiFont
            };
            this.Controls.Add(lblLang);

            cmbLang = new ComboBox()
            {
                Left = 110,
                Top = 113,
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = uiFont
            };
            cmbLang.Items.Add("日本語");
            cmbLang.Items.Add("en");
            cmbLang.SelectedIndex = LanguageManager.CurrentLanguage == "en" ? 1 : 0;
            cmbLang.SelectedIndexChanged += (s, e) => {
                LanguageManager.SaveLanguage(cmbLang.Text);
                UpdateUI();
            };
            this.Controls.Add(cmbLang);

            chkEnableHotKey = new CheckBox()
            {
                Left = 10,
                Top = 150,
                Width = 300,
                Text = LanguageManager.GetString("SettingEnableHotkey"),
                ForeColor = Color.Gainsboro,
                Font = uiFont
            };
            this.Controls.Add(chkEnableHotKey);

            lblTriggerKey = new Label()
            {
                Text = LanguageManager.GetString("SettingTriggerKey"),
                Left = 10,
                Top = 180,
                Width = 100,
                ForeColor = Color.Gainsboro,
                Font = uiFont
            };
            this.Controls.Add(lblTriggerKey);

            cmbTriggerKey = new ComboBox()
            {
                Left = 110,
                Top = 176,
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = uiFont
            };
            cmbTriggerKey.Items.AddRange(new object[] { "Shift", "Ctrl", "Alt", "Space" });
            cmbTriggerKey.SelectedIndex = 0;
            this.Controls.Add(cmbTriggerKey);

            lblLaunchKeyCount = new Label()
            {
                Text = LanguageManager.GetString("SettingLaunchKeyCount"),
                Left = 10,
                Top = 210,
                Width = 200,
                ForeColor = Color.Gainsboro,
                Font = uiFont
            };
            this.Controls.Add(lblLaunchKeyCount);

            cmbLaunchKeyCount = new ComboBox()
            {
                Left = 220,
                Top = 206,
                Width = 60,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = uiFont
            };
            cmbLaunchKeyCount.Items.AddRange(new object[] { "2", "3", "4", "5" });
            cmbLaunchKeyCount.SelectedIndex = 0;
            this.Controls.Add(cmbLaunchKeyCount);

            btnSave = new Button()
            {
                Text = LanguageManager.GetString("SettingSave"),
                Left = 330,
                Top = 245,
                Width = 90,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 130, 140),
                ForeColor = Color.White
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(90, 150, 160);
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            if (File.Exists(iniPath))
            {
                var ini = IniHelper.ReadIni(iniPath);

                if (ini.ContainsKey("LauncherFolder")) txtPath.Text = ini["LauncherFolder"];
                if (ini.ContainsKey("FontSize") && decimal.TryParse(ini["FontSize"], out decimal fs))
                    numFontSize.Value = Math.Max(numFontSize.Minimum, Math.Min(numFontSize.Maximum, fs));
                if (ini.ContainsKey("EnableHotKey") && bool.TryParse(ini["EnableHotKey"], out bool enabled))
                    chkEnableHotKey.Checked = enabled;
                if (ini.ContainsKey("ShiftPressCount"))
                    cmbLaunchKeyCount.SelectedItem = ini["ShiftPressCount"];
                if (ini.ContainsKey("TriggerKey"))
                    cmbTriggerKey.SelectedItem = ini["TriggerKey"];
            }
            else
            {
                chkEnableHotKey.Checked = true;
            }
        }

        private void UpdateUI()
        {
            this.Text = LanguageManager.GetString("SettingTitle");
            lblPath.Text = LanguageManager.GetString("SettingPath");
            btnBrowse.Text = LanguageManager.GetString("SettingBrowse");
            lblFont.Text = LanguageManager.GetString("SettingFontSize");
            lblLang.Text = LanguageManager.GetString("SettingLang");
            btnSave.Text = LanguageManager.GetString("SettingSave");
            chkEnableHotKey.Text = LanguageManager.GetString("SettingEnableHotkey");
            lblLaunchKeyCount.Text = LanguageManager.GetString("SettingLaunchKeyCount");
            lblTriggerKey.Text = LanguageManager.GetString("SettingTriggerKey");
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = LanguageManager.GetString("DialogSelectDir");
                if (Directory.Exists(txtPath.Text)) dialog.SelectedPath = txtPath.Text;
                if (dialog.ShowDialog() == DialogResult.OK) txtPath.Text = dialog.SelectedPath;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string folder = txtPath.Text.Trim();

            // ① 空チェック
            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show(LanguageManager.GetString("MsgPathReq"));
                return;
            }

            // ② 存在チェック
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(LanguageManager.GetString("MsgDirNotExists"));
                return;
            }

            // ③ ドライブ直下・システムフォルダチェック
            if (IsInvalidRootFolder(folder))
            {
                MessageBox.Show(LanguageManager.GetString("MsgSystemDirError"));
                return;
            }

            // ④ アクセス権チェック
            if (!CanAccessFolder(folder))
            {
                MessageBox.Show(LanguageManager.GetString("MsgAccessDenied"));
                return;
            }

            // ⑤ フォルダ＋ファイル総数チェック
            const int MAX_ITEMS = 1000;
            const int WARNING_ITEMS = 500;

            if (!TryCountItems(folder, MAX_ITEMS, out int count))
            {
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("MsgTooManyItems"), MAX_ITEMS, count));
                return;
            }

            // 警告ライン
            if (count > WARNING_ITEMS)
            {
                var result = MessageBox.Show(
                    string.Format(LanguageManager.GetString("MsgHeavyConfirm"), count),
                    LanguageManager.GetString("MsgConfirmTitle"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;
            }

            // ⑥ 保存
            try
            {
            File.WriteAllText(
                iniPath,
                $"LauncherFolder={txtPath.Text.Trim()}\n" +
                $"FontSize={numFontSize.Value}\n" +
                $"Language={cmbLang.Text}\n" +
                $"EnableHotKey={chkEnableHotKey.Checked}\n" +
                $"TriggerKey={cmbTriggerKey.SelectedItem}\n" +
                $"ShiftPressCount={cmbLaunchKeyCount.SelectedItem}\n"
            );


                MessageBox.Show(LanguageManager.GetString("MsgSaveSuccess"));
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.GetString("MsgSaveFailed") + ex.Message);
            }
        }

        private bool IsInvalidRootFolder(string path)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd('\\');
            string root = Path.GetPathRoot(fullPath).TrimEnd('\\');

            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                || IsProtectedFolder(path);
        }

        private bool CanAccessFolder(string path)
        {
            try
            {
                Directory.GetDirectories(path);
                Directory.GetFiles(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsReparsePoint(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                return (attr & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return true;
            }
        }

        private bool IsProtectedFolder(string path)
        {
            string name = Path.GetFileName(path.TrimEnd('\\'));

            return name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Windows", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryCountItems(string rootPath, int maxCount, out int totalCount)
        {
            totalCount = 0;
            Stack<string> stack = new();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                // 再解析ポイント（ジャンクション・シンボリックリンク）は無視
                if (IsReparsePoint(current))
                    continue;

                // フォルダ自身
                totalCount++;
                if (totalCount > maxCount)
                    return false;

                string[] dirs;
                try
                {
                    dirs = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var dir in dirs)
                {
                    if (IsProtectedFolder(dir))
                        continue;

                    stack.Push(dir);
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                totalCount += files.Length;
                if (totalCount > maxCount)
                    return false;
            }

            return true;
        }
    }
}
