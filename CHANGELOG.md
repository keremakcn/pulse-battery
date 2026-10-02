## Window dragging fix — 2026-10-02

- The top border and empty padding above the title now move the window when dragged. Pin, hide and close buttons retain their normal behavior.

## Power display update — 2026-10-02

- Current Consumption / Güncel Tüketim now holds its value for five seconds, then refreshes using the last-five-second mean. The smaller 1 min avg. / 1 dk ort. uses the last minute. Charging power follows the same cadence. Raw graph and learning inputs are unchanged.

## Branding update — 2026-10-02

- Added the supplied multi-resolution ICO to the executable, window and system tray. Embedded assets keep the executable portable.
- Added the supplied logo to the Turkish and English README headers.

# 1.1.0-beta.1 — 2026-10-01

- Time-weighted energy learning; display smoothing no longer erases real short bursts. Learning works at 1/5/10/15-second polling intervals.
- AC-connected usage never trains typical battery use. Sleep and display-off exclusion remains mandatory; dimming alone no longer resets the live model.
- Separate indefinite daily archives for normal/high-load/charging/standby energy. Recent journals append incrementally; small active model writes are batched. Version 1 is backed up and migrated as approximate historical data.
- Windows battery native IOCTL reader with unit/sentinel validation, granularity, cached metadata and battery identity. Relative or unverified units are not displayed as W/Wh.
- 1-second visible native polling, 5-second tray/fallback polling, no hidden graph redraws. Source changes invalidate in-flight reads.
- Capacity-delta fallback, independent charge-session evidence, 5% bands and measured complete-band durations. Paused charging at 99% or another level does not create an endless countdown or contaminate the curve.
- 14-day fresh battery reports retained; adjacent fragments merged before outlier detection. Imported history is a startup prior, without overlapping live-history double weighting.
- Approximate ranges widen from steady-workload prediction errors; real full-cycle calibration and cross-device field tests are still pending.
- Updated English/Turkish text, migration notes, validation report and isolated smoke test.

# Sürüm notları

## 1.0.0 — 2026-10-01

Pulse’un ilk sürümü.

- Ekran kapalı/uyku ölçümleri aktif tüketim ve şarj öğrenmesinden ayrılır; isteğe bağlı bekleme net pil kaybı gösterimi eklendi.

- Windows görüntüleme diline göre otomatik Türkçe/İngilizce arayüz; diğer dillerde İngilizce.

- Anlık pil tüketimi ve şarj gücü, kapasite, voltaj ve desteklenen cihazlarda pil sağlığı.
- Sürüklenebilir pencere, her zaman üstte tutma ve sistem tepsisine küçültme.
- Kısa tüketim sıçramalarını süzen, sürekli yüksek yüke uyum sağlayan kalan süre tahmini.
- Cihazda saklanan kullanım profili ve doluluk seviyesine göre öğrenilen şarj eğrisi.
- Windows pil geçmişinden başlangıç analizi; uyku, prizde kullanım ve aykırı tüketimin filtrelenmesi.
- Yaklaşık süre aralıkları, öğrenme durumu ve geçmişi sıfırlama.

### Doğrulama ve sınırlar

16 geçmiş analizi kontrolü, 24 öğrenme kontrolü ve temel süre hesapları geçti. Windows pil raporunun oluşturulması ve analiz edilmesi mevcut bilgisayarda doğrulandı. Farklı laptoplarda saha doğrulaması henüz yapılmadı. Şarj sınırları ve göreli kapasite bildiren sürücülerle ilgili ayrıntılar README’de açıklanır. Sürüm kod imzalı değildir.
