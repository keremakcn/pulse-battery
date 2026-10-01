# Pulse 1.0.0 · Öğrenen pil widget’ı

[English documentation](README.en.md)

Windows 10/11 laptoplar için küçük, Türkçe/İngilizce pil widget’ı. Pulse.exe dosyasına çift tıklayın; kurulum veya internet bağlantısı gerekmez. .NET Framework 4.8 hedeflenir.

![Pulse görünümü — örnek veriler](preview.png)

## İndirme ve güncelleme

GitHub Releases bölümündeki `Pulse-v1.0.0-Windows.zip` paketini indirin, dosyaları çıkarın ve `Pulse.exe` dosyasını açın. Önceki kopyanız varsa Pulse’u sistem tepsisinden kapatıp eski EXE’yi yeni dosyayla değiştirin. Masaüstüne kopyalanmış dosyalar otomatik güncellenmez. Öğrenilmiş profil uygulama klasöründen ayrı tutulduğu için korunur. Önceki filtreyle analiz yaptıysanız “Pil geçmişini yeniden analiz et” düğmesini kullanın.

Bu sürüm kod imzalı değildir; Windows bilinmeyen yayıncı uyarısı gösterebilir. Ayrıntılı sürüm notları: [CHANGELOG.md](CHANGELOG.md).

## Dil

Uygulama açılışta Windows kullanıcısının görüntüleme dilini algılar. Türkçe için Türkçe, İngilizce ve diğer diller için İngilizce kullanılır. Arayüz, tepsi menüsü, ipuçları, hata/durum mesajları, süre birimleri ve sayı biçimleri birlikte değişir. Çevrimiçi hizmet kullanılmaz. Windows dili değiştirildiğinde Pulse’u kapatıp açın. Gerekirse `Pulse.exe --lang=tr` veya `Pulse.exe --lang=en` ile dil elle seçilebilir.

`Pulse.exe --language-test` otomatik dil seçimini, desteklenmeyen dilde İngilizceye dönüşü ve sayı/birim biçimlerini sınar. `--preview` seçili dile göre `preview-tr.png` veya `preview-en.png` üretir.

## Kullanım

- Pulse yazısından tutup sürükleyin.
- ◇ / ◆: her zaman üstte tutmayı açar veya kapatır.
- −: sistem tepsisine küçültür; simgeye çift tıklayarak geri getirin.
- × veya tepsi menüsündeki Kapat: uygulamayı kapatır.
- Tepsi menüsündeki “Öğrenme geçmişini sıfırla”: cihazda öğrenilen profili temizler.
- Aynı Windows kullanıcısı ve oturumunda ikinci bir kopya açılmaz.

## Kişiye ve cihaza özel öğrenme

### Uyku ve ekran kapalı kullanım

Uyku veya ekran kapalıyken gelen ölçümler genel tüketim, canlı süre ortalaması, şarj eğrisi ve aktif grafik geçmişine eklenmez. Bu koruma her zaman açıktır. Uyku/uyanma ve oturum ekran durumu Windows bildirimleriyle izlenir. Ekran kapalı indirme gibi aktif işler de tedbiren dışarıda tutulur; bu alan kesin bir Modern Standby teşhisi değildir. Bildirimler alınamazsa canlı öğrenme duraklatılır ve durum belirtilir.

Tepsi menüsündeki **Bekleme kaybını göster** seçeneği varsayılan olarak açıktır ve cihazdaki profile kaydedilir. Windows çalışmaya izin verirse beklemede en sık 60 saniyede bir ölçüm alınır; uygulama duraklatılırsa ölçüm zorlanmaz. Cihazı uyanık tutma veya uyandırma isteği gönderilmez. Geçiş sırasında başlamış eski ölçümler atılır; uyanışın ilk yeni okuması sınır ölçümüdür, ardından aktif öğrenme yeni örneklerle başlar.

Uygun başlangıç ve bitiş okumaları varsa son beklemenin yaklaşık **net kapasite kaybı (Wh)** ve süresi gösterilir; genel kullanım hesabına aktarılmaz. Bu sonuç yalnızca mevcut uygulama oturumunda tutulur. Başlangıç ölçümü eskiyse, pil değişirse, kapasite verisi eksikse veya şarj/priz kullanımı gözlenirse kayıp gösterilmez. Uygulama duraklatılmışken gözlenemeyen kısa şarjlar tespit edilemeyebilir; bu nedenle sonuç toplam tüketim değil yaklaşık net farktır. Seçeneği kapatmak bekleme takibini kapatır; uyku ölçümlerini öğrenmeden dışlama korumasını kapatmaz.

`--standby-test` 19 senaryoyla ölçümlerin ayrılmasını sınar. `--powerwatch-test` bilgisayarı uyutmadan Windows bildirimlerine kaydı doğrular. Gerçek kapak kapatma/Modern Standby döngüsü bu sürümde fiziksel olarak test edilmemiştir.

Hazır bir kullanıcı profili içermez. İlk açılıştaki “Pil geçmişimi analiz et” düğmesiyle Windows pil geçmişini başlangıç profiline dönüştürebilir. Analiz yapılmazsa veya yeterli kayıt yoksa uygulama açıkken veri toplar. Geçmiş kullanılmıyorsa ilk yaklaşık 30 saniye canlı tahmin için, en az 20 dakikalık geçerli pil kullanımı da genel kullanım profili için gereklidir. Bunlar doğruluk garantisi değildir; farklı kullanım günleriyle profil daha temsil edici olur.

Ölçümler 5 saniyede bir alınır. Ağ bağlantısı, bulut, yapay zekâ modeli veya ek servis kullanılmaz. Her Windows kullanıcısının profili kendi LocalAppData/Pulse/profile-v1.xml dosyasında tutulur. Programın dağıtım paketi kullanıcı geçmişi içermez. Bataryanın Windows aygıt kimliği değişirse profil sıfırlanır. Aynı kimlikle yapılan fiziksel pil değişimi otomatik olarak ayırt edilemeyebilir; menüden geçmişi sıfırlayın.

### Windows geçmişiyle başlangıç analizi

“Pil geçmişimi analiz et” düğmesi arka planda powercfg /batteryreport /xml /duration 14 çalıştırır. Bu bir yük testi değildir; pil zorlanmaz. Komut yalnızca düğmeye basıldığında çalışır, her açılışta veya 5 saniyelik döngüde tekrarlanmaz. Rapor en fazla 30 saniye beklenir, geçici XML yerel olarak işlenir ve ardından silinir. Profilde yalnızca özet güç, toplam süre, kayıt/gün sayıları ve analiz zamanı kalır. Raporun kendisi dağıtım paketine eklenmez.

RecentUsage içindeki Active ve Ac=0 kayıtları kullanılır. 5 dakikadan kısa / 12 saatten uzun, eski, sıfır/negatif tüketimli, çelişkili kapasite içeren, yinelenen veya örtüşen kayıtlar dışarıda tutulur. Süre 100 nanosaniyelik Windows biriminden saate, tüketim mWh’den Wh’ye çevrilir. Uyku ve prizde kullanım analize katılmaz. Windows’un pil değişti işaretinden önceki kayıtlar kullanılmaz.

Aykırı tüketimi belirlerken her oturum eşit oy kullanır: oturum medyanı ve medyan mutlak sapma hesaplanır. Böylece tek uzun, yüksek tüketimli oturum normal kullanım eşiğini belirleyemez. Sapma eşiği medyanın %60’ı ile mutlak sapmanın üç katından büyük olanıdır; ancak medyanın kendisini aşamaz. Tipik tüketimin iki katından yüksek oturumlar genel profile katılmaz. Filtrelenmiş oturumlardan ortalama alınırken süre ağırlığı en fazla 60 dakikadır. Elektrik kesintisi/oyun tespit edilmez; kullanıcıya göre sıra dışı tüketim ayrılır. Düzenli yüksek tüketim kullanıcının normaliyse otomatik olarak silinmez. En az 3 uygun oturum, toplam 60 dakika ve 2 farklı UTC gün gerekir. Yetersiz ya da okunamayan raporda canlı öğrenme devam eder. “Aykırı” sayısı yalnızca tüketim filtresinde ayrılan oturumları gösterir; uyku, prizde kullanım ve diğer geçersiz kayıtlar bu sayıya dahil değildir.

Geçmiş, ilk canlı ölçümler gelene kadar geniş bir süre aralığıyla ve “Windows geçmişine göre” etiketiyle kullanılır. Canlı öğrenme ilerledikçe genel kullanım hesabında tarihsel özete 30 dakikalık sabit ağırlık, canlı dakikalara kendi sayıları kadar ağırlık verilir. Aynı raporu yeniden analiz etmek örnekleri çoğaltmaz, özeti günceller. 30 günden eski analiz kullanılmaz. Sürekli yüksek yükte canlı kalan süre geçmişteki iyimser değere bağlı kalmaz.

Windows raporu cihazın geçmişidir; aynı cihazdaki başka Windows oturumlarının pil kullanımını da içerebilir. Profil dosyası Windows kullanıcısına özeldir. Rapor geçmiş adaptör performansını veya ayrıntılı şarj eğrisini güvenilir biçimde ayırmadığından, şarj eğrisi yalnızca canlı ölçümlerden öğrenilir. Geçmişle başlamak doğruluk garantisi değildir; tahmin başarısı gerçek kullanımda ölçülmelidir.

### Canlı kalan süre

Son 7 ölçümün medyanı, yumuşatılmış bir ortalamayı besler. Kısa sıçramalar bastırılır; yüksek tüketim yaklaşık 20 saniye devam ederse tahmin yeni yüke uyum sağlamaya başlar. Böylece oyun devam ederken kalan süre gereğinden uzun tutulmaz. Şarj/deşarj değişiminde, geçersiz güç verisinde veya 20 saniyeden uzun ölçüm boşluğunda canlı hesap yeniden başlar. Öğrenilmiş genel profil korunur.

Gösterilen süre aralığı yaklaşık bir kullanım payıdır; istatistiksel olarak kalibre edilmiş güven aralığı değildir. Normalde ±%10, değişken tüketimde ±%25, henüz öğrenilmemiş şarj eğrisinde ±%20 pay uygulanır ve sınırlar 5 dakikaya yuvarlanır. “Kullanım değişken” etiketi son ölçümlerin değişkenliğini belirtir.

Grafik ve “ort.” alanı ham ölçümleri gösterir; süre tahmini ayrı olarak yumuşatılır. Bu nedenle ort. W ile süre hesabı birebir aynı olmayabilir.

### Genel kullanımda pil ömrü

Dakikalık medyanlar saklanır. En az 20 örnekten sonra tipik tüketim hesaplanır; genel medyanın etrafındaki aykırı düşük/yüksek dakika değerleri dışarıda tutulur. Eşik medyanın %60’ı ile medyan mutlak sapmanın üç katından büyük olanıdır. Genel kullanım süresi, mevcut tam dolum kapasitesinin bu tipik tüketime bölünmesidir; tamamlanmış pil boşaltma oturumlarının doğrudan ortalaması değildir.

Elektrik kesintisi veya oyun oynandığı kesin olarak tespit edilmez. Nadir yüksek tüketim ölçümleri istatistiksel olarak ayrılır. Kullanıcının çoğu kullanımı oyun ise bu zamanla normal kullanım profiline dönüşebilir; kişisel genel kullanımı temsil etmesi amaçlanır.

Son 30 gün içindeki en fazla 720 dakikalık örnek tutulur. Bu sınır uzun süre açık kullanımda son 12 saatlik geçerli pil ölçümüne karşılık gelir; 30 günlük eksiksiz günlük tutulmaz.

### Şarj süresi

Doluluk %10’luk 10 bölüme ayrılır; her bölümün dakika medyanlarından şarj gücü öğrenilir. Bölüm başına en az 3 dakika gözlem sonrası o bölümün hızı kullanılır. Kalan bölümlerin süreleri toplanır; böylece sona yaklaşırken gözlenen yavaşlama hesaba katılır. Mevcut güç önceki eğriyi ölçeklendirerek adaptör veya yük değişimine uyum sağlar. Görülmemiş bölümlerde mevcut güç kullanılır ve “Şarj eğrisi öğreniliyor” gösterilir. Eğri 30 gün güncellenmezse eski kabul edilir.

Farklı adaptörler ayrı profillerde tutulmaz. Üreticinin %80 gibi özel şarj sınırları algılanmaz; hedef donanımın bildirdiği tam dolum kapasitesidir. Şarjın durduğu bildirildiğinde süre gösterilmez. Gerçek doğruluk farklı laptoplarda ve tam şarj döngülerinde ayrıca ölçülmelidir.

## Hafiflik ve gizlilik

Canlı öğrenme penceresi 24 örnek, grafik 60 örnek, kalıcı normal profil 720 dakika ve şarj eğrisi 10 bölümle sınırlıdır. Dosya en sık 2 dakikada bir, ayrıca çıkışta ve sıfırlamada yazılır. Ham, kesintisiz günlük tutulmaz. Dosya bozuksa boş profille başlanır; kayıt başarısızsa ekranda belirtilir ve öğrenme o oturumda devam eder.

Uygulama başlangıca otomatik eklenmez, pil ayarlarını değiştirmez, süreç/adaptör/oyun isimleri toplamaz. Pencere konumu ve üstte tutma tercihi oturumlar arasında saklanmaz.

## Donanım sınırları

Sunulmayan veriler “—” gösterilir. Ayrıntılı ölçümler okunamazsa Windows temel pil durumu kullanılır. Pil sağlığı, tam dolum / tasarım kapasitesidir. Göreli kapasite birimi kullanan sürücüler doğrulanmamıştır. Birden çok pil zıt yönde çalışıyorsa güç ve süre gösterilmez.

## Kaynak ve doğrulama

Pulse.cs, Learning.cs, LearningTests.cs, Widget.xaml ve build.ps1 kaynak dosyalarıdır. build.ps1 Windows’un .NET Framework derleyicisiyle derler; harici paket gerektirmez.

- `Pulse.exe --self-test`: temel süre hesapları.
- `Pulse.exe --learning-test`: sıçrama, sürekli yük, uyku, geçersiz veri, aykırı genel kullanım, şarj yavaşlaması, adaptör/yük değişimi, profil kaydetme/yükleme, bozuk dosya, farklı aygıt, sıfırlama ve bellek sınırları.
- `Pulse.exe --history-test`: geçmiş filtresi, süre/enerji birimleri, ilk tahmin, kalıcılık, tekrar analiz, pil değişimi ve güvenli XML okuma testleri.
- `Pulse.exe --probe`: gerçek pil verisini probe.txt dosyasına yazar.
- `Pulse.exe --preview`: örnek verilerle preview.png üretir; bu görsel gerçek ölçüm değildir.

Testler sentetik senaryolardır; farklı donanımlarda saha doğruluğunun veya tüm uygulamanın CPU/RAM tüketiminin garantisi değildir. Öğrenme testindeki hız ölçümü yalnızca hesaplama döngüsüne aittir.

Windows kaynakları: root/wmi BatteryStatus, BatteryFullChargedCapacity, BatteryStaticData ve GetSystemPowerStatus.

- https://learn.microsoft.com/en-us/windows/win32/api/batclass/ns-batclass-battery_wmi_status
- https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-battery

- https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options#batteryreport


