using System.Drawing.Drawing2D;

namespace BayrakCircuitStudio;

/// <summary>
/// Grid-based circuit designer. This is a visual editor, not a SPICE solver.
/// Pin-to-pin connections are stored in the project document.
/// </summary>
public sealed class CircuitCanvas : Control
{
    private CircuitProject _project = new();
    private CircuitPart? _selected;
    private bool _dragging;
    private bool _changedDuringDrag;
    private Point _dragStart;
    private PointF _initialPosition;
    private (Guid id, int pin)? _pendingWire;
    private string? _placementKind;
    private float _zoom = 1f;
    private bool _demoActive;
    private int _demoTick;

    public event Action? ProjectChanged;
    public event Action<CircuitPart?>? SelectionChanged;
    public event Action? GestureStarting;
    public CircuitPart? Selected => _selected;
    public float Zoom => _zoom;

    public CircuitCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.FromArgb(17, 24, 36);
        ForeColor = Color.WhiteSmoke;
        TabStop = true;
    }

    public void LoadProject(CircuitProject project)
    {
        _project = project;
        _selected = null;
        _pendingWire = null;
        _placementKind = null;
        Invalidate();
        SelectionChanged?.Invoke(null);
    }

    public void SetPlacement(string? kind)
    {
        _placementKind = kind;
        _pendingWire = null;
        Cursor = kind is null ? Cursors.Default : Cursors.Cross;
        Invalidate();
    }

    public void SetDemo(bool enabled, int tick)
    {
        _demoActive = enabled;
        _demoTick = tick;
        Invalidate();
    }

    public void DeleteSelected()
    {
        if (_selected is null) return;
        GestureStarting?.Invoke();
        Guid id = _selected.Id;
        _project.Wires.RemoveAll(w => w.FromId == id || w.ToId == id);
        _project.Parts.RemoveAll(p => p.Id == id);
        _selected = null;
        SelectionChanged?.Invoke(null);
        ProjectChanged?.Invoke();
        Invalidate();
    }

    public void ClearWireMode()
    {
        _pendingWire = null;
        _placementKind = null;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        g.ScaleTransform(_zoom, _zoom);

        float w = Width / _zoom, h = Height / _zoom;
        using var gridPen = new Pen(Color.FromArgb(34, 45, 61), 1);
        for (float x = 0; x < w; x += 20) g.DrawLine(gridPen, x, 0, x, h);
        for (float y = 0; y < h; y += 20) g.DrawLine(gridPen, 0, y, w, y);

        using var wirePen = new Pen(Color.FromArgb(81, 207, 150), 2.5f);
        foreach (var wire in _project.Wires)
        {
            var a = _project.Parts.FirstOrDefault(p => p.Id == wire.FromId);
            var b = _project.Parts.FirstOrDefault(p => p.Id == wire.ToId);
            if (a is null || b is null) continue;
            var start = PinLocation(a, wire.FromPin);
            var end = PinLocation(b, wire.ToPin);
            float midX = (start.X + end.X) / 2;
            g.DrawLines(wirePen, new[] { start, new PointF(midX, start.Y),
                new PointF(midX, end.Y), end });
        }

        foreach (var part in _project.Parts) DrawPart(g, part);

        if (_pendingWire is { } pending)
        {
            var p = _project.Parts.FirstOrDefault(x => x.Id == pending.id);
            if (p is not null)
            {
                var start = PinLocation(p, pending.pin);
                using var marker = new Pen(Color.Orange, 2);
                g.DrawEllipse(marker, start.X - 8, start.Y - 8, 16, 16);
            }
        }

        using var hintBrush = new SolidBrush(Color.FromArgb(166, 179, 199));
        if (_placementKind is not null)
            g.DrawString($"Yerleştir: {PartDefinition.Get(_placementKind).DisplayName}  •  ESC: iptal",
                Font, hintBrush, 12, 10);
        else if (_pendingWire is not null)
            g.DrawString("Bağlantıyı tamamlamak için ikinci pine tıkla  •  ESC: iptal",
                Font, hintBrush, 12, 10);
    }

    private void DrawPart(Graphics g, CircuitPart p)
    {
        bool selected = p.Id == _selected?.Id;
        bool board = p.Kind is "ESP32" or "ESP32S3" or "ArduinoUNO";
        bool display = p.Kind is "OLED" or "LCD";
        float width = 140, height = board ? 136 : display ? 96 : 70;
        var rect = new RectangleF(p.X, p.Y, width, height);
        using var fill = new SolidBrush(board ? Color.FromArgb(25, 77, 72) :
            display ? Color.FromArgb(24, 68, 109) : Color.FromArgb(39, 53, 70));
        using var border = new Pen(selected ? Color.FromArgb(255, 193, 85) :
            Color.FromArgb(112, 137, 161), selected ? 3 : 1.4f);
        g.FillRectangle(fill, rect);
        g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);

        using var titleBrush = new SolidBrush(Color.White);
        using var labelBrush = new SolidBrush(Color.FromArgb(170, 195, 214));
        using var titleFont = new Font("Segoe UI", 10, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 8);
        g.DrawString(p.Name, titleFont, titleBrush, p.X + 10, p.Y + 7);
        g.DrawString(PartDefinition.Get(p.Kind).DisplayName, smallFont, labelBrush, p.X + 10, p.Y + 29);
        if (display)
        {
            var screen = new RectangleF(p.X + 12, p.Y + 45, 116, 37);
            using var black = new SolidBrush(Color.FromArgb(2, 16, 27));
            g.FillRectangle(black, screen);
            string content = _demoActive ? $"DEMO  {_demoTick % 100:00}\nSYSTEM READY" : "DISPLAY OFF";
            using var screenFont = new Font("Consolas", 8, FontStyle.Bold);
            using var screenBrush = new SolidBrush(_demoActive ? Color.Aqua : Color.FromArgb(47, 83, 93));
            g.DrawString(content, screenFont, screenBrush, p.X + 17, p.Y + 49);
        }
        else if (p.Kind == "LED")
        {
            bool lit = _demoActive && _demoTick % 2 == 0;
            using var ledBrush = new SolidBrush(lit ? Color.FromArgb(83, 255, 95) : Color.FromArgb(59, 92, 64));
            g.FillEllipse(ledBrush, p.X + 102, p.Y + 22, 25, 25);
            g.DrawString(p.Value, smallFont, labelBrush, p.X + 10, p.Y + 49);
        }
        else if (!board)
            g.DrawString(p.Value, smallFont, labelBrush, p.X + 10, p.Y + 49);
        else
            g.DrawString(p.Value + " • preview", smallFont, labelBrush, p.X + 10, p.Y + 64);

        var definition = PartDefinition.Get(p.Kind);
        using var pinBrush = new SolidBrush(Color.FromArgb(240, 192, 106));
        using var pinFont = new Font("Consolas", 7);
        for (int i = 0; i < definition.Pins.Length; i++)
        {
            PointF pin = PinLocation(p, i);
            g.FillEllipse(pinBrush, pin.X - 4, pin.Y - 4, 8, 8);
            bool left = i % 2 == 0;
            string pinName = definition.Pins[i];
            var sz = g.MeasureString(pinName, pinFont);
            g.DrawString(pinName, pinFont, labelBrush,
                left ? pin.X + 7 : pin.X - sz.Width - 7, pin.Y - 8);
        }
    }

    public static PointF PinLocation(CircuitPart p, int index)
    {
        var def = PartDefinition.Get(p.Kind);
        bool board = p.Kind is "ESP32" or "ESP32S3" or "ArduinoUNO";
        bool display = p.Kind is "OLED" or "LCD";
        float height = board ? 136 : display ? 96 : 70;
        int sideIndex = index / 2;
        int sideTotal = (def.Pins.Length + (index % 2 == 0 ? 1 : 0)) / 2;
        sideTotal = Math.Max(1, sideTotal);
        float y = p.Y + (sideIndex + 1) * height / (sideTotal + 1);
        return new PointF(p.X + (index % 2 == 0 ? 0 : 140), y);
    }

    private PointF ToWorld(Point point) => new(point.X / _zoom, point.Y / _zoom);

    private CircuitPart? HitPart(PointF pos) =>
        _project.Parts.LastOrDefault(p =>
        {
            float height = p.Kind is "ESP32" or "ESP32S3" or "ArduinoUNO" ? 136 :
                p.Kind is "OLED" or "LCD" ? 96 : 70;
            return pos.X >= p.X && pos.X <= p.X + 140 &&
                   pos.Y >= p.Y && pos.Y <= p.Y + height;
        });

    private (CircuitPart part, int pin)? HitPin(PointF pos)
    {
        foreach (var p in _project.Parts.AsEnumerable().Reverse())
            for (int i = 0; i < PartDefinition.Get(p.Kind).Pins.Length; i++)
            {
                var pin = PinLocation(p, i);
                if (Math.Abs(pos.X - pin.X) < 10 && Math.Abs(pos.Y - pin.Y) < 10)
                    return (p, i);
            }
        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        var pos = ToWorld(e.Location);
        if (_placementKind is not null)
        {
            GestureStarting?.Invoke();
            var def = PartDefinition.Get(_placementKind);
            int n = _project.Parts.Count(p => p.Kind == def.Kind) + 1;
            var p = new CircuitPart
            {
                Kind = def.Kind, Name = def.Prefix + n,
                Value = def.DefaultValue, X = MathF.Round(pos.X / 20) * 20,
                Y = MathF.Round(pos.Y / 20) * 20
            };
            _project.Parts.Add(p);
            _selected = p;
            SelectionChanged?.Invoke(p);
            ProjectChanged?.Invoke();
            Invalidate();
            return;
        }

        var hit = HitPin(pos);
        if (hit is { } pinHit)
        {
            if (_pendingWire is null)
                _pendingWire = (pinHit.part.Id, pinHit.pin);
            else
            {
                var start = _pendingWire.Value;
                if (start.id != pinHit.part.Id || start.pin != pinHit.pin)
                {
                    bool duplicate = _project.Wires.Any(w =>
                        (w.FromId == start.id && w.FromPin == start.pin &&
                         w.ToId == pinHit.part.Id && w.ToPin == pinHit.pin) ||
                        (w.ToId == start.id && w.ToPin == start.pin &&
                         w.FromId == pinHit.part.Id && w.FromPin == pinHit.pin));
                    if (!duplicate)
                    {
                        GestureStarting?.Invoke();
                        _project.Wires.Add(new CircuitWire { FromId = start.id,
                            FromPin = start.pin, ToId = pinHit.part.Id, ToPin = pinHit.pin });
                        ProjectChanged?.Invoke();
                    }
                }
                _pendingWire = null;
            }
            Invalidate();
            return;
        }

        _selected = HitPart(pos);
        SelectionChanged?.Invoke(_selected);
        _dragging = _selected is not null;
        _changedDuringDrag = false;
        _dragStart = e.Location;
        if (_selected is not null) _initialPosition = new PointF(_selected.X, _selected.Y);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || _selected is null || e.Button != MouseButtons.Left) return;
        float dx = (e.X - _dragStart.X) / _zoom;
        float dy = (e.Y - _dragStart.Y) / _zoom;
        if (!_changedDuringDrag && Math.Abs(dx) + Math.Abs(dy) > 2)
        {
            GestureStarting?.Invoke();
            _changedDuringDrag = true;
        }
        if (!_changedDuringDrag) return;
        _selected.X = MathF.Max(0, MathF.Round((_initialPosition.X + dx) / 20) * 20);
        _selected.Y = MathF.Max(0, MathF.Round((_initialPosition.Y + dy) / 20) * 20);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_changedDuringDrag) ProjectChanged?.Invoke();
        _dragging = false;
        _changedDuringDrag = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _zoom = Math.Clamp(_zoom + Math.Sign(e.Delta) * .1f, .5f, 2f);
        Invalidate();
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData == Keys.Delete || keyData == Keys.Escape || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
        if (e.KeyCode == Keys.Escape) { ClearWireMode(); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
