# Bayrak Circuit Studio

**C# / .NET 10 Windows Forms tabanlı elektronik devre tasarım editörü – V0.1 (ilk prototip).**

## Çalışan özellikler
- Kategorilere ayrılmış 15 bileşen modeli: Arduino UNO, ESP32, ESP32-S3, LED, direnç, kondansatör, MOSFET, OLED vb.
- Izgaralı devre editörü; bileşen yerleştirme, sürükleyerek taşıma, silme, iki pin arasında bağlantı çizimi, zoom.
- Projeyi \`.bcs\` (JSON) olarak kaydetme / açma.
- Devre PNG dışa aktarımı.
- Arduino/ESP32 sketch editörü.
- **Arduino CLI kuruluysa** gerçek Arduino/ESP32 sketch derleme (gerekli kart core paketleri ayrıca kurulmalıdır).
- Görsel demo: LED yanıp sönmesi ve OLED/LCD ekranında örnek çıktı.

## Önemli teknik sınır
**V0.1 henüz bir elektronik simülatörü değildir.** “Çalıştır (Demo)” yalnızca ekran ve LED için görsel önizleme yapar; sketch kodunu yürütmez, devre bağlantılarını elektriksel olarak çözmez. Arduino CLI butonu yalnızca *derleme* yapar. Gerçek AVR / ESP32 emülasyonu, çevre birimi modelleri ve ngspice entegrasyonu sonraki sürümler için planlanmıştır.

## Kurulum
1. Windows 10/11 ve .NET 10 SDK kur.
2. Visual Studio 2022'nin .NET 10 destekleyen güncel sürümünü veya Visual Studio 2026'yı kullan.
3. Depoyu klonla: \`git clone https://github.com/bayrakbilisim/BayrakCircuitStudio.git\`
4. Proje klasöründe: \`dotnet run\`
5. Arduino kodu derlemek için ayrıca [Arduino CLI](https://arduino.github.io/arduino-cli/latest/installation/) kur. PATH üzerinde değilse \`ARDUINO_CLI_PATH\` ortam değişkenini CLI'nin tam yoluna ayarla.
6. Kart paketlerini yükle: \`arduino-cli core update-index\`, \`arduino-cli core install arduino:avr\`, ESP32 için önce Espressif board manager URL'sini yapılandır ve \`arduino-cli core install esp32:esp32\` çalıştır.

## Kullanım
- Sol menüde bir bileşene çift tıkla, ardından tuvale tıklayarak yerleştir.
- Kablo oluşturmak için önce bir pin noktasına, ardından diğer pine tıkla.
- Taşımak için bileşeni sürükle; seçip \`Delete\` ile sil.
- \`Esc\`: bileşen yerleştirme / kablo bağlantısı modundan çık.
- Fare tekerleği: yakınlaştır / uzaklaştır.
- \`Ctrl+S\`: kaydet, \`Ctrl+O\`: aç, \`Ctrl+Z\`: devre işleminde geri al.

## Yol haritası
V0.2: Kod editörü iyileştirmesi, pin adlandırma ve proje veri doğrulama.
V0.3: Gerçek AVR firmware emülasyonu ve GPIO olay sistemi.
V0.4+: OLED / LCD / I2C / SPI çevre birimi davranış modelleri.
V0.5+: ESP32 ailesi için işlemci emülatörü desteği araştırması.
V0.6+: ngspice tabanlı analog çözümleme.

## Lisans
Henüz bir lisans dosyası eklenmemiştir. Kaynak kod GitHub'da görünür olsa da otomatik olarak açık kaynak lisanslı değildir.
