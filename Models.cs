using System.Text.Json;
using System.Text.Json.Serialization;

namespace BayrakCircuitStudio;

public sealed class CircuitProject
{
    public string Name { get; set; } = "Adsız Proje";
    public string Board { get; set; } = "esp32:esp32:esp32";
    public string Sketch { get; set; } = "#include <Arduino.h>\r\n\r\nvoid setup() {\r\n  pinMode(2, OUTPUT);\r\n}\r\n\r\nvoid loop() {\r\n  digitalWrite(2, HIGH);\r\n  delay(1000);\r\n  digitalWrite(2, LOW);\r\n  delay(1000);\r\n}\r\n";
    public List<CircuitPart> Parts { get; set; } = new();
    public List<CircuitWire> Wires { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CircuitProject Clone() =>
        JsonSerializer.Deserialize<CircuitProject>(
            JsonSerializer.Serialize(this, JsonOptions), JsonOptions)
        ?? new CircuitProject();
}

public sealed class CircuitPart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "Resistor";
    public string Name { get; set; } = "R1";
    public float X { get; set; }
    public float Y { get; set; }
    public string Value { get; set; } = "";
}

public sealed class CircuitWire
{
    public Guid FromId { get; set; }
    public int FromPin { get; set; }
    public Guid ToId { get; set; }
    public int ToPin { get; set; }
}

public record PartDefinition(string Kind, string DisplayName, string Prefix, string DefaultValue, string[] Pins)
{
    public static readonly IReadOnlyList<PartDefinition> All = new List<PartDefinition>
    {
        new("ESP32", "ESP32 DevKit", "U", "3.3V", new[] { "3V3", "GND", "GPIO2", "GPIO21", "GPIO22" }),
        new("ESP32S3", "ESP32-S3", "U", "3.3V", new[] { "3V3", "GND", "GPIO2", "GPIO8", "GPIO9" }),
        new("ArduinoUNO", "Arduino UNO", "U", "5V", new[] { "5V", "GND", "D13", "A0", "SDA", "SCL" }),
        new("Resistor", "Direnç", "R", "220 Ω", new[] { "1", "2" }),
        new("Capacitor", "Kondansatör", "C", "100 nF", new[] { "+", "-" }),
        new("Diode", "Diyot", "D", "1N4148", new[] { "A", "K" }),
        new("LED", "LED", "D", "Kırmızı", new[] { "A", "K" }),
        new("MOSFET", "N-MOSFET", "Q", "IRLZ44N", new[] { "G", "D", "S" }),
        new("Transistor", "NPN transistör", "Q", "2N2222", new[] { "B", "C", "E" }),
        new("IC", "Entegre (genel)", "U", "NE555", new[] { "VCC", "GND", "IN", "OUT" }),
        new("OLED", "OLED SSD1306", "DS", "128x64 I2C", new[] { "VCC", "GND", "SDA", "SCL" }),
        new("LCD", "LCD 16x2", "DS", "I2C", new[] { "VCC", "GND", "SDA", "SCL" }),
        new("Button", "Buton", "SW", "NO", new[] { "1", "2" }),
        new("DCSource", "DC güç kaynağı", "V", "5 V", new[] { "+", "-" }),
        new("Ground", "GND", "GND", "0 V", new[] { "GND" }),
    };

    public static PartDefinition Get(string kind) =>
        All.FirstOrDefault(x => x.Kind == kind) ?? All[3];
}
