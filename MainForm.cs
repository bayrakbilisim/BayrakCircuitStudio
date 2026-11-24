using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.Json;

namespace BayrakCircuitStudio;

public sealed class MainForm : Form
{
    private CircuitProject _project = new();
    private string? _filePath;
    private bool _dirty;
    private bool _loading;
    private readonly Stack<CircuitProject> _undo = new();
    private readonly Stack<CircuitProject> _redo = new();

    private readonly CircuitCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly RichTextBox _editor = new()
    {
        Dock = DockStyle.Fill, AcceptsTab = true, WordWrap = false,
        Font = new Font("Consolas", 11), BackColor = Color.FromArgb(20, 27, 39),
        ForeColor = Color.FromArgb(225, 233, 242), BorderStyle = BorderStyle.None
    };
    private readonly TextBox _output = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 9),
        BackColor = Color.FromArgb(13, 20, 29), ForeColor = Color.FromArgb(104, 220, 158)
    };
    private readonly PropertyGrid _properties = new()
    {
        Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true
    };
    private readonly TreeView _palette = new()
    {
        Dock = DockStyle.Fill, HideSelection = false,
        BackColor = Color.FromArgb(28, 38, 52), ForeColor = Color.WhiteSmoke,
        BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10)
    };
    private readonly ComboBox _board = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 155
    };
    private readonly ToolStripStatusLabel _status = new("Hazır");
    private readonly System.Windows.Forms.Timer _demoTimer = new() { Interval = 1000 };
    private int _demoTicks;

    public MainForm()
    {
        Text = "Bayrak Circuit Studio • V0.1";
        Width = 1500;
        Height = 900;
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(25, 33, 45);
        ForeColor = Color.WhiteSmoke;
        KeyPreview = true;
        InitializeShell();
        WireEvents();
        _editor.Text = _project.Sketch;
        _canvas.LoadProject(_project);
        _board.SelectedIndex = 0;
        UpdateTitle();
        Log("Bayrak Circuit Studio V0.1 hazır.");
        Log("Not: Çalıştır, yalnızca görsel demo yürütür; Arduino/ESP32 kodunu emüle etmez.");
        Log("Derle: Yüklü Arduino CLI üzerinden gerçek derleme yapar.");
    }

    private void InitializeShell()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(35, 47, 63), ForeColor = Color.White };
        var file = new ToolStripMenuItem("Dosya");
        file.DropDownItems.Add("Yeni", null, (_, _) => NewProject());
        file.DropDownItems.Add("Aç...", null, (_, _) => OpenProject());
        file.DropDownItems.Add("Kaydet", null, (_, _) => SaveProject(false));
        file.DropDownItems.Add("Farklı Kaydet...", null, (_, _) => SaveProject(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("PNG olarak dışa aktar...", null, (_, _) => ExportPng());
        file.DropDownItems.Add("Çıkış", null, (_, _) => Close());
        var edit = new ToolStripMenuItem("Düzenle");
        edit.DropDownItems.Add("Geri Al", null, (_, _) => Undo());
        edit.DropDownItems.Add("Yinele", null, (_, _) => Redo());
        edit.DropDownItems.Add("Seçili bileşeni sil", null, (_, _) => _canvas.DeleteSelected());
        var help = new ToolStripMenuItem("Yardım");
        help.DropDownItems.Add("Kısayollar ve kullanım", null, (_, _) =>
            MessageBox.Show(this,
                "Soldaki bileşene çift tıkla, tuvale tıklayarak yerleştir.\n" +
                "Pinden pine tıklayarak kablo çek.\n" +
                "Bileşeni sürükleyerek taşı; Delete ile sil.\n" +
                "ESC: araç veya kablo bağlantısını iptal et.\n" +
                "Fare tekeri: zoom.\n" +
                "Ctrl+N: Yeni • Ctrl+O: Aç • Ctrl+S: Kaydet\n" +
                "Ctrl+Z: Geri Al • Ctrl+Y: Yinele\n\n" +
                "Çalıştır: görsel demo. Derle: Arduino CLI ile gerçek sketch derleme.",
                "Kullanım"));
        menu.Items.AddRange(new ToolStripItem[] { file, edit, help });
        MainMenuStrip = menu;

        var bar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.FromArgb(42, 56, 74),
            ForeColor = Color.White,
            Padding = new Padding(5, 5, 5, 5),
            ImageScalingSize = new Size(20, 20)
        };
        void AddAction(string name, EventHandler handler) =>
            bar.Items.Add(new ToolStripButton(name, null, handler) { DisplayStyle = ToolStripItemDisplayStyle.Text, ForeColor = Color.White });
        AddAction("Yeni", (_, _) => NewProject());
        AddAction("Aç", (_, _) => OpenProject());
        AddAction("Kaydet", (_, _) => SaveProject(false));
        bar.Items.Add(new ToolStripSeparator());
        AddAction("↶ Geri", (_, _) => Undo());
        AddAction("↷ İleri", (_, _) => Redo());
        AddAction("ESC / Seç", (_, _) => _canvas.ClearWireMode());
        bar.Items.Add(new ToolStripSeparator());
        AddAction("▶ Çalıştır (Demo)", (_, _) => StartDemo());
        AddAction("■ Durdur", (_, _) => StopDemo());
        AddAction("✓ Derle (CLI)", async (_, _) => await CompileAsync());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(new ToolStripLabel("Kart: ") { ForeColor = Color.White });
        _board.Items.AddRange(new object[]
        {
            "ESP32 Dev Module", "ESP32-S3 Dev Module", "Arduino UNO"
        });
        bar.Items.Add(new ToolStripControlHost(_board));

        var status = new StatusStrip
        {
            BackColor = Color.FromArgb(32, 44, 58), ForeColor = Color.White
        };
        status.Items.Add(_status);
        _status.ForeColor = Color.WhiteSmoke;

        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = _palette.BackColor };
        leftPanel.Controls.Add(_palette);
        leftPanel.Controls.Add(new Label
        {
            Text = "BİLEŞEN KÜTÜPHANESİ",
            Dock = DockStyle.Top, Height = 28, ForeColor = Color.LightSkyBlue,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        });
        var right = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            SplitterWidth = 6, BackColor = Color.FromArgb(55, 70, 85)
        };
        right.Panel1.Controls.Add(_editor);
        var editLabel = new Label
        {
            Text = "  main.ino  |  Arduino / ESP32 C++",
            Dock = DockStyle.Top, Height = 28, ForeColor = Color.LightSkyBlue,
            BackColor = Color.FromArgb(33, 43, 57)
        };
        right.Panel1.Controls.Add(editLabel);

        var bottomRight = new TabControl { Dock = DockStyle.Fill };
        var consoleTab = new TabPage("Derleme / Seri çıktı") { BackColor = _output.BackColor };
        consoleTab.Controls.Add(_output);
        var propertiesTab = new TabPage("Özellikler");
        propertiesTab.Controls.Add(_properties);
        bottomRight.TabPages.Add(consoleTab);
        bottomRight.TabPages.Add(propertiesTab);
        right.Panel2.Controls.Add(bottomRight);

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterWidth = 6, BackColor = Color.FromArgb(55, 70, 85)
        };
        mainSplit.Panel1.Controls.Add(leftPanel);

        var workSplit = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterWidth = 6, BackColor = Color.FromArgb(55, 70, 85)
        };
        workSplit.Panel1.Controls.Add(_canvas);
        workSplit.Panel2.Controls.Add(right);
        mainSplit.Panel2.Controls.Add(workSplit);

        Controls.Add(mainSplit);
        Controls.Add(status);
        Controls.Add(bar);
        Controls.Add(menu);
        menu.Dock = DockStyle.Top;
        bar.Dock = DockStyle.Top;
        mainSplit.BringToFront();
        // Set split distances only after the controls have their actual size.
        Shown += (_, _) =>
        {
            mainSplit.SplitterDistance = 230;
            workSplit.SplitterDistance = Math.Max(300, workSplit.Width - 430);
            right.SplitterDistance = Math.Max(200, right.Height - 245);
        };

        var micro = new TreeNode("Mikrodenetleyiciler");
        var passive = new TreeNode("Pasif elemanlar");
        var semiconductor = new TreeNode("Yarı iletkenler");
        var screen = new TreeNode("Ekranlar");
        var misc = new TreeNode("Güç / Giriş");
        foreach (var def in PartDefinition.All)
        {
            var node = new TreeNode(def.DisplayName) { Tag = def.Kind };
            switch (def.Kind)
            {
                case "ESP32": case "ESP32S3": case "ArduinoUNO": micro.Nodes.Add(node); break;
                case "Resistor": case "Capacitor": passive.Nodes.Add(node); break;
                case "LED": case "Diode": case "MOSFET": case "Transistor": case "IC":
                    semiconductor.Nodes.Add(node); break;
                case "OLED": case "LCD": screen.Nodes.Add(node); break;
                default: misc.Nodes.Add(node); break;
            }
        }
        _palette.Nodes.AddRange(new[] { micro, passive, semiconductor, screen, misc });
        _palette.ExpandAll();
    }

    private void WireEvents()
    {
        _palette.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Node.Tag is string kind)
            {
                _canvas.SetPlacement(kind);
                _status.Text = "Yerleştirme aracı: " + e.Node.Text;
            }
        };
        _canvas.GestureStarting += Snapshot;
        _canvas.ProjectChanged += MarkDirty;
        _canvas.SelectionChanged += p => _properties.SelectedObject = p;
        _properties.PropertyValueChanged += (_, _) => { MarkDirty(); _canvas.Invalidate(); };
        _editor.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _project.Sketch = _editor.Text;
            MarkDirty();
        };
        _board.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _project.Board = CurrentFqbn();
            MarkDirty();
        };
        _demoTimer.Tick += (_, _) =>
        {
            _demoTicks++;
            _canvas.SetDemo(true, _demoTicks);
            _status.Text = $"Görsel demo • t={_demoTicks} sn";
        };
        FormClosing += (_, e) =>
        {
            if (!ConfirmDiscard()) e.Cancel = true;
        };
    }

    private string CurrentFqbn() => _board.SelectedIndex switch
    {
        1 => "esp32:esp32:esp32s3",
        2 => "arduino:avr:uno",
        _ => "esp32:esp32:esp32"
    };

    private void Snapshot()
    {
        _undo.Push(_project.Clone());
        if (_undo.Count > 50)
        {
            var keep = _undo.Take(50).Reverse().ToArray();
            _undo.Clear();
            foreach (var s in keep) _undo.Push(s);
        }
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(_project.Clone());
        Restore(_undo.Pop());
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(_project.Clone());
        Restore(_redo.Pop());
    }

    private void Restore(CircuitProject state)
    {
        StopDemo();
        _project = state;
        _loading = true;
        _editor.Text = _project.Sketch;
        _board.SelectedIndex = _project.Board.Contains("esp32s3") ? 1 :
            _project.Board.StartsWith("arduino") ? 2 : 0;
        _loading = false;
        _canvas.LoadProject(_project);
        MarkDirty();
    }

    private void MarkDirty()
    {
        _dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle() =>
        Text = $"Bayrak Circuit Studio V0.1 — {_project.Name}{(_dirty ? " *" : "")}";

    private bool ConfirmDiscard() =>
        !_dirty || MessageBox.Show(this, "Kaydedilmemiş değişiklikler kaybolacak. Devam edilsin mi?",
            "Değişiklikler", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void NewProject()
    {
        if (!ConfirmDiscard()) return;
        StopDemo();
        _project = new CircuitProject();
        _filePath = null;
        _undo.Clear();
        _redo.Clear();
        _loading = true;
        _editor.Text = _project.Sketch;
        _board.SelectedIndex = 0;
        _loading = false;
        _canvas.LoadProject(_project);
        _dirty = false;
        UpdateTitle();
        Log("Yeni proje oluşturuldu.");
    }

    private void OpenProject()
    {
        if (!ConfirmDiscard()) return;
        using var dialog = new OpenFileDialog
        {
            Filter = "Bayrak Circuit Studio (*.bcs)|*.bcs|JSON (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var project = JsonSerializer.Deserialize<CircuitProject>(
                File.ReadAllText(dialog.FileName, Encoding.UTF8),
                CircuitProject.JsonOptions) ?? throw new InvalidDataException("Boş proje");
            StopDemo();
            _project = project;
            _filePath = dialog.FileName;
            _undo.Clear();
            _redo.Clear();
            _loading = true;
            _editor.Text = _project.Sketch;
            _board.SelectedIndex = _project.Board.Contains("esp32s3") ? 1 :
                _project.Board.StartsWith("arduino") ? 2 : 0;
            _loading = false;
            _canvas.LoadProject(_project);
            _dirty = false;
            UpdateTitle();
            Log("Proje açıldı: " + dialog.FileName);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Açma hatası"); }
    }

    private void SaveProject(bool saveAs)
    {
        if (saveAs || _filePath is null)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Bayrak Circuit Studio (*.bcs)|*.bcs",
                FileName = _project.Name + ".bcs"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _filePath = dialog.FileName;
        }
        try
        {
            _project.Name = Path.GetFileNameWithoutExtension(_filePath);
            _project.Sketch = _editor.Text;
            _project.Board = CurrentFqbn();
            File.WriteAllText(_filePath!, JsonSerializer.Serialize(_project,
                CircuitProject.JsonOptions), Encoding.UTF8);
            _dirty = false;
            UpdateTitle();
            Log("Kaydedildi: " + _filePath);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Kaydetme hatası"); }
    }

    private void ExportPng()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG görüntü (*.png)|*.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        using var image = new Bitmap(_canvas.Width, _canvas.Height);
        _canvas.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
        Log("PNG kaydedildi: " + dialog.FileName);
    }

    private void StartDemo()
    {
        _demoTicks = 0;
        _canvas.SetDemo(true, 0);
        _demoTimer.Start();
        Log("GÖRSEL DEMO başlatıldı. Bu işlem sketch kodunu yürütmez.");
    }

    private void StopDemo()
    {
        _demoTimer.Stop();
        _canvas.SetDemo(false, 0);
        _status.Text = "Durduruldu";
    }

    private async Task CompileAsync()
    {
        _project.Sketch = _editor.Text;
        string? sketchDir = null;
        try
        {
            sketchDir = Path.Combine(Path.GetTempPath(), "BayrakCircuitStudio",
                "Sketch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sketchDir);
            string sketchName = Path.GetFileName(sketchDir);
            await File.WriteAllTextAsync(Path.Combine(sketchDir, sketchName + ".ino"),
                _project.Sketch, Encoding.UTF8);
            string cli = Environment.GetEnvironmentVariable("ARDUINO_CLI_PATH") ?? "arduino-cli";
            Log("Derleniyor: " + CurrentFqbn());
            Log("Komut: arduino-cli compile --fqbn " + CurrentFqbn());
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = cli, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("compile");
            process.StartInfo.ArgumentList.Add("--fqbn");
            process.StartInfo.ArgumentList.Add(CurrentFqbn());
            process.StartInfo.ArgumentList.Add(sketchDir);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Log(await stdout);
            Log(await stderr);
            Log(process.ExitCode == 0 ? "DERLEME BAŞARILI" :
                $"DERLEME BAŞARISIZ (exit code {process.ExitCode})");
        }
        catch (Exception ex)
        {
            Log("Derleme başlatılamadı: " + ex.Message);
            Log("Arduino CLI kurulumunu ve Arduino/ESP32 core paketlerini kontrol edin.");
        }
        finally
        {
            if (sketchDir is not null)
            {
                try { Directory.Delete(sketchDir, true); }
                catch (IOException) { /* Kullanımda kalan derleme çıktıları */ }
                catch (UnauthorizedAccessException) { /* Temizlik daha sonra yapılabilir */ }
            }
        }
    }

    private void Log(string message)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(message)); return; }
        if (string.IsNullOrWhiteSpace(message)) return;
        _output.AppendText($"[{DateTime.Now:HH:mm:ss}] {message.TrimEnd()}{Environment.NewLine}");
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.N: NewProject(); return true;
            case Keys.Control | Keys.O: OpenProject(); return true;
            case Keys.Control | Keys.S: SaveProject(false); return true;
            case Keys.Control | Keys.Z when !_editor.Focused: Undo(); return true;
            case Keys.Control | Keys.Y when !_editor.Focused: Redo(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
