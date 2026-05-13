using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MultimediaPlayer
{
    public class MainForm : Form
    {
        private readonly WmpPlayerView screen = new WmpPlayerView();
        private readonly ListBox playlist = new ListBox();
        private readonly Button open = new Button(), play = new Button(), pause = new Button(), stop = new Button(), full = new Button(), remove = new Button();
        private readonly TrackBar seek = new TrackBar(), volume = new TrackBar();
        private readonly Label status = new Label();
        private readonly CheckBox loop = new CheckBox();
        private readonly Timer timer = new Timer();
        private readonly List<string> files = new List<string>();
        private bool seeking;
        private bool isFullScreen;
        private Form fullScreenForm;
        private Control originalParent;
        private int originalIndex;
        private TableLayoutPanelCellPosition originalCell;

        public MainForm()
        {
            Text = "1123305 - 多媒體播放器";
            MinimumSize = new Size(960, 580);
            Font = new Font("Microsoft JhengHei UI", 10f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));

            playlist.Dock = DockStyle.Fill;
            playlist.DoubleClick += (s, e) => LoadSelected(true);
            root.Controls.Add(playlist, 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.Controls.Add(right, 1, 0);

            screen.Dock = DockStyle.Fill;
            screen.BackColor = Color.Black;
            // AxHost 沒有 BorderStyle 屬性；外框改用 BackColor/容器即可，避免編譯錯誤。
            right.Controls.Add(screen, 0, 0);

            seek.Dock = DockStyle.Fill;
            seek.Minimum = 0;
            seek.Maximum = 1000;
            seek.TickFrequency = 100;
            seek.MouseDown += (s, e) => seeking = true;
            seek.MouseUp += (s, e) =>
            {
                seeking = false;
                screen.PositionMs = seek.Value;
            };
            right.Controls.Add(seek, 0, 1);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Configure(open, "加入媒體");
            Configure(play, "播放");
            Configure(pause, "暫停/續播");
            Configure(stop, "停止");
            Configure(full, "全螢幕");
            Configure(remove, "移除");
            loop.Text = "循環";
            loop.AutoSize = true;
            loop.Padding = new Padding(8, 8, 0, 0);
            loop.CheckedChanged += (s, e) => screen.Loop = loop.Checked;

            volume.Minimum = 0;
            volume.Maximum = 100;
            volume.Value = 80;
            volume.Width = 150;
            volume.TickFrequency = 10;
            volume.Scroll += (s, e) => screen.Volume = volume.Value;

            buttons.Controls.AddRange(new Control[]
            {
                open, play, pause, stop, full, remove, loop,
                new Label { Text = "音量", AutoSize = true, Padding = new Padding(15, 10, 0, 0) }, volume
            });
            right.Controls.Add(buttons, 0, 2);

            status.Dock = DockStyle.Fill;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Text = "支援 WAV / MP3 / WMA / AVI / WMV / MP4。播放能力取決於 Windows Media Player 與系統解碼器。";
            right.Controls.Add(status, 0, 3);
            Controls.Add(root);

            open.Click += (s, e) => AddFiles();
            play.Click += (s, e) => LoadSelected(true);
            pause.Click += (s, e) => screen.PauseOrResume();
            stop.Click += (s, e) => screen.Stop();
            full.Click += (s, e) => ToggleFullScreen();
            remove.Click += (s, e) => RemoveSelected();
            timer.Interval = 250;
            timer.Tick += Timer_Tick;
            timer.Start();
            FormClosing += (s, e) => screen.CloseMedia();
        }

        private void Configure(Button b, string text)
        {
            b.Text = text;
            b.AutoSize = true;
            b.Height = 32;
        }

        private void AddFiles()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Media files|*.wav;*.mp3;*.wma;*.avi;*.wmv;*.mp4;*.m4v;*.mpg;*.mpeg|All files (*.*)|*.*",
                Multiselect = true
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                foreach (var f in dlg.FileNames)
                {
                    files.Add(f);
                    playlist.Items.Add(Path.GetFileName(f));
                }
                if (playlist.SelectedIndex < 0 && playlist.Items.Count > 0) playlist.SelectedIndex = 0;
            }
        }

        private void RemoveSelected()
        {
            int i = playlist.SelectedIndex;
            if (i < 0) return;
            files.RemoveAt(i);
            playlist.Items.RemoveAt(i);
            if (playlist.Items.Count > 0) playlist.SelectedIndex = Math.Min(i, playlist.Items.Count - 1);
        }

        private void LoadSelected(bool autoPlay)
        {
            int i = playlist.SelectedIndex;
            if (i < 0 || i >= files.Count) return;

            try
            {
                screen.Open(files[i], autoPlay, volume.Value, loop.Checked);
                seek.Maximum = Math.Max(1000, screen.LengthMs);
                status.Text = "目前檔案：" + files[i] + "    長度：" + FormatMs(screen.LengthMs);
            }
            catch (Exception ex)
            {
                MessageBox.Show("無法開啟媒體：" + ex.Message + "\r\n請確認檔案存在，且 Windows Media Player 可以播放此格式。", "播放失敗");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            int len = screen.LengthMs;
            int pos = screen.PositionMs;
            if (len > 0)
            {
                if (!seeking)
                {
                    seek.Maximum = Math.Max(1, len);
                    seek.Value = Math.Max(0, Math.Min(pos, len));
                }
                status.Text = "播放進度：" + FormatMs(pos) + " / " + FormatMs(len);
            }
        }

        private void ToggleFullScreen()
        {
            if (!isFullScreen)
            {
                originalParent = screen.Parent;
                originalIndex = originalParent.Controls.GetChildIndex(screen);
                if (originalParent is TableLayoutPanel tlp0) originalCell = tlp0.GetCellPosition(screen);

                fullScreenForm = new Form
                {
                    WindowState = FormWindowState.Maximized,
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = Color.Black,
                    KeyPreview = true
                };
                fullScreenForm.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) ToggleFullScreen(); };
                fullScreenForm.FormClosing += (s, e) => RestoreFromFullScreen();

                originalParent.Controls.Remove(screen);
                fullScreenForm.Controls.Add(screen);
                screen.Dock = DockStyle.Fill;
                isFullScreen = true;
                fullScreenForm.Show(this);
            }
            else
            {
                RestoreFromFullScreen();
                if (fullScreenForm != null) fullScreenForm.Close();
            }
        }

        private void RestoreFromFullScreen()
        {
            if (!isFullScreen || originalParent == null) return;
            if (screen.Parent != null) screen.Parent.Controls.Remove(screen);
            if (originalParent is TableLayoutPanel tlp)
            {
                tlp.Controls.Add(screen, originalCell.Column, originalCell.Row);
            }
            else
            {
                originalParent.Controls.Add(screen);
            }
            originalParent.Controls.SetChildIndex(screen, originalIndex);
            screen.Dock = DockStyle.Fill;
            isFullScreen = false;
        }

        private static string FormatMs(int ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            return t.ToString(t.Hours > 0 ? @"h\:mm\:ss" : @"mm\:ss");
        }
    }

    // 使用 Windows Media Player ActiveX，避免 MCI 對 MP4 / Codec 支援不足造成 277 錯誤。
    // 不需要設計器，也不需要 AxInterop.WMPLib 檔案，直接用 CLSID 建立 ActiveX 控制項。
    public class WmpPlayerView : AxHost
    {
        private dynamic ocx;
        private bool paused;

        public WmpPlayerView() : base("6BF52A52-394A-11d3-B153-00C04F79FAA6")
        {
            BackColor = Color.Black;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            ocx = GetOcx();
            try
            {
                ocx.uiMode = "none";
                ocx.enableContextMenu = false;
                ocx.stretchToFit = true;
                ocx.settings.autoStart = false;
                ocx.settings.volume = 80;
            }
            catch { }
        }

        public int Volume
        {
            get { try { return (int)ocx.settings.volume; } catch { return 0; } }
            set { try { ocx.settings.volume = Math.Max(0, Math.Min(100, value)); } catch { } }
        }

        public bool Loop
        {
            set { try { ocx.settings.setMode("loop", value); } catch { } }
        }

        public int LengthMs
        {
            get
            {
                try
                {
                    if (ocx == null || ocx.currentMedia == null) return 0;
                    return (int)(ocx.currentMedia.duration * 1000.0);
                }
                catch { return 0; }
            }
        }

        public int PositionMs
        {
            get
            {
                try { return (int)(ocx.controls.currentPosition * 1000.0); }
                catch { return 0; }
            }
            set
            {
                try { ocx.controls.currentPosition = Math.Max(0, value) / 1000.0; }
                catch { }
            }
        }

        public void Open(string file, bool autoPlay, int volume, bool loop)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("找不到檔案", file);
            if (ocx == null) CreateControl();
            ocx.URL = file;
            Volume = volume;
            Loop = loop;
            paused = false;
            if (autoPlay) ocx.controls.play();
        }

        public void PauseOrResume()
        {
            try
            {
                if (paused)
                {
                    ocx.controls.play();
                    paused = false;
                }
                else
                {
                    ocx.controls.pause();
                    paused = true;
                }
            }
            catch { }
        }

        public void Stop()
        {
            try
            {
                ocx.controls.stop();
                paused = false;
                PositionMs = 0;
            }
            catch { }
        }

        public void CloseMedia()
        {
            try { ocx.close(); } catch { }
        }
    }
}
