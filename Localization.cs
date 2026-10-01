using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Pulse {
 public static class L {
  public static bool Turkish {get;private set;}
  public static CultureInfo Culture {get {return CultureInfo.GetCultureInfo(Turkish?"tr-TR":"en-US");}}
  static readonly Dictionary<string,string> English=new Dictionary<string,string> {
   {"Bekleme kaybını göster","Show standby battery loss"},
   {"Bekleme ölçümleri öğrenmeye dahil edilmez","Standby readings are excluded from learning"},
   {"Ekran kapalı / uyku · öğrenme duraklatıldı","Screen off / sleep · learning paused"},
   {"Güç durumu alınamadı · öğrenme duraklatıldı","Power status unavailable · learning paused"},
   {"Son bekleme: ","Last standby: "},
   {"Ekran kapalı/uyku döneminin yaklaşık net pil kaybı. Genel kullanım ve şarj öğrenmesine katılmaz.","Approximate net battery loss while the screen was off or asleep. Excluded from typical-use and charge learning."},
   {"Pulse · Pil widget’ı","Pulse · Battery widget"},
   {"Pulse zaten çalışıyor. Sistem tepsisindeki simgeden açabilirsiniz.","Pulse is already running. Open it from the system tray."},
   {"ÖRNEK VERİ","SAMPLE DATA"},
   {"Önce batarya bilgisi okunmalı","Waiting for battery information"},
   {"Windows pil geçmişi inceleniyor…","Analyzing Windows battery history…"},
   {"Rapor okunamadı · canlı öğrenme devam ediyor","Report unavailable · learning continues"},
   {"Pulse’u göster","Show Pulse"},
   {"Öğrenme geçmişini sıfırla","Reset learning history"},
   {"Genel kullanım profili öğreniliyor","Learning your typical usage"},
   {"Kapat","Exit"},
   {"Pil verisi okunamadı · tekrar deneniyor","Battery data unavailable · retrying"},
   {"PİL ALGILANMADI","NO BATTERY DETECTED"},
   {"ŞARJ OLUYOR","CHARGING"},
   {"PİLDEN ÇALIŞIYOR","ON BATTERY"},
   {"PRİZE BAĞLI","PLUGGED IN"},
   {"PİL DURUMU BEKLENİYOR","WAITING FOR BATTERY"},
   {"tam doluma tahmini","estimated until full"},
   {"tahmini kalan süre","estimated time left"},
   {"şarj / deşarj yok","not charging or discharging"},
   {"ANLIK ŞARJ GÜCÜ","CHARGING POWER"},
   {"ANLIK TÜKETİM","DISCHARGE RATE"},
   {"ort. ","avg. "},
   {" V · batarya voltajı"," V · battery voltage"},
   {"Voltaj bilgisi sunulmuyor","Voltage unavailable"},
   {"Tasarım: ","Design: "},
   {"Üretici verisi sunulmuyor","Manufacturer data unavailable"},
   {"Windows bir pil bildirmiyor","Windows reports no battery"},
   {"●  Canlı · 5 sn aralıkla","●  Live · every 5 seconds"},
   {"Sınırlı veri · Windows pil durumu","Limited data · Windows battery status"},
   {"Genel kullanım · tam pille ≈ ","Typical use · full battery ≈ "},
   {" dk"," min"},
   {"Bu cihazdaki tipik tüketimden hesaplanır. Windows geçmişi veya en az 20 dakika canlı ölçüm kullanılır; aykırı dakika ölçümleri dışarıda tutulur. Kalan süre mevcut kullanıma göre ayrıca hesaplanır.","Based on typical use on this device, using Windows history or at least 20 minutes of live readings. Unusual readings are excluded. Time remaining is calculated separately for the current workload."},
   {"İlk tahmini Windows geçmişinle başlat","Start with your Windows battery history"},
   {"Pil geçmişini yeniden analiz et","Reanalyze battery history"},
   {"Pil geçmişimi analiz et","Analyze my battery history"},
   {"Geçmiş kaydedilemiyor · bu oturum öğreniyor","History cannot be saved · learning this session"},
   {"Ölçümler toplanıyor","Collecting readings"},
   {"Son ","Last "},
   {" saniye"," seconds"},
   {"Pil algılanmadı","No battery detected"},
   {"Şarj / deşarj yok","Not charging or discharging"},
   {"Windows geçmişine göre · ilk tahmin","Windows history · initial estimate"},
   {"Öğreniliyor · ölçümler toplanıyor","Learning · collecting readings"},
   {"Kullanım değişken · tahmin aralığı geniş","Variable usage · wider estimate"},
   {"Şarj eğrisi öğreniliyor · yaklaşık süre","Learning charge curve · approximate"},
   {"Şarj geçmişine göre · yaklaşık süre","Charge history · approximate"},
   {"Genel kullanım öğreniliyor · yaklaşık süre","Learning typical usage · approximate"},
   {"Dengeli tüketim · yaklaşık süre","Steady usage · approximate"},
   {"— dk","— min"},
   {"<1 dk","<1 min"},
   {" oturum · "," sessions · "},
   {" dk · "," min · "},
   {" aykırı"," outliers"},
   {"Rapor beklenenden büyük.","The report is larger than expected."},
   {"Pil raporu biçimi tanınmadı.","Unrecognized battery report format."},
   {"Yeterli geçmiş yok · canlı öğrenme sürecek","Not enough history · learning continues"},
   {"Analiz zaman aşımına uğradı. Yeniden deneyebilirsiniz.","Analysis timed out. Please try again."},
   {"Windows raporu oluşturamadı. Canlı öğrenme devam ediyor.","Windows could not create the report. Live learning continues."},
   {"Her zaman üstte","Always on top"},
   {"Sistem tepsisine küçült","Minimize to tray"},
   {"PİL OKUNUYOR","READING BATTERY"},
   {"Ölçümler bekleniyor","Waiting for readings"},
   {"ort. — W","avg. — W"},
   {"Veri bekleniyor","Waiting for data"},
   {"ŞU AN / TAM DOLUM","CURRENT / FULL"},
   {"Voltaj bilgisi bekleniyor","Waiting for voltage"},
   {"PİL SAĞLIĞI","BATTERY HEALTH"},
   {"Tasarım kapasitesi: —","Design capacity: —"},
   {"Windows kayıtlarını cihazında analiz eder. Pili zorlayan bir test yapmaz; hiçbir veri göndermez.","Analyzes Windows history locally. No battery stress test and no data uploads."},
   {"az yer. net bilgi.","small space. clear insight."},
   {" /  PİL"," /  BATTERY"}
  };
  public static bool IsTurkish(CultureInfo culture) {return culture.TwoLetterISOLanguageName=="tr";}
  public static int CheckLanguages() {
   var before=CultureInfo.CurrentUICulture;
   foreach(string name in new[]{"tr-TR","en-US","en-GB","de-DE"}) {
    Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo(name);Initialize(new string[0]);
    bool tr=name=="tr-TR";
    if(Turkish!=tr||T(" dk")!=(tr?" dk":" min")||1.5.ToString("0.0",Culture)!=(tr?"1,5":"1.5")||Percent(88)!=(tr?"%88":"88%"))return 1;
    if(!tr)foreach(var pair in English)if(T(pair.Key)!=pair.Value)return 2;
   }
   Thread.CurrentThread.CurrentUICulture=before;Initialize(new string[0]);return 0;
  }
  public static void Initialize(string[] args) {
   var chosen=CultureInfo.CurrentUICulture;
   if(args.Contains("--lang=tr"))chosen=CultureInfo.GetCultureInfo("tr-TR");
   if(args.Contains("--lang=en"))chosen=CultureInfo.GetCultureInfo("en-US");
   Turkish=IsTurkish(chosen);
   Thread.CurrentThread.CurrentUICulture=Culture;
   Thread.CurrentThread.CurrentCulture=Culture;
   CultureInfo.DefaultThreadCurrentUICulture=Culture;
   CultureInfo.DefaultThreadCurrentCulture=Culture;
  }
  public static string T(string text) {string value;return !Turkish&&English.TryGetValue(text,out value)?value:text;}
  public static string Percent(double value) {return Turkish?"%"+Math.Round(value).ToString(Culture):Math.Round(value).ToString(Culture)+"%";}
  public static string Xaml(string source) {
   var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(source);
   foreach(XmlElement element in doc.SelectNodes("//*"))
    foreach(XmlAttribute attribute in element.Attributes)
     if(attribute.LocalName=="Text"||attribute.LocalName=="Content"||attribute.LocalName=="ToolTip"||attribute.LocalName=="Title")attribute.Value=T(attribute.Value);
   return doc.OuterXml;
  }
 }
}
