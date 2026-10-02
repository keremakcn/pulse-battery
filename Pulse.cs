using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Pulse {
 public class Reading {
  public string Device="";
  public double Percent=-1, Remaining=-1, Full=-1, Design=-1, Rate=-1, Voltage=-1;
  public double GranularityWh=.025;
  public bool Charging, Discharging, Online, Present=true, Detailed, Native, FullReported;
  public string Mode { get { return !Present ? "none" : Charging ? "charge" : Discharging ? "discharge" : Online ? "ac" : "idle"; } }
 }
 public class Meter {
  public readonly List<double> Samples=new List<double>();
  public readonly List<DateTime> Times=new List<DateTime>();
  string mode="",device="";bool online; DateTime last=DateTime.MinValue,displayAt=DateTime.MinValue;
  double displayedRate=-1;
  public void Clear(){Samples.Clear();Times.Clear();displayedRate=-1;displayAt=DateTime.MinValue;}
  public void Add(Reading r){Add(r,DateTime.UtcNow);}
  public void Add(Reading r,DateTime now) {
   if(mode!=r.Mode||device!=r.Device||online!=r.Online||(now-last).TotalSeconds>20||now<=last||Samples.Count!=Times.Count)Clear();
   mode=r.Mode;device=r.Device;online=r.Online;last=now;
   if(r.Rate<0||double.IsNaN(r.Rate)||double.IsInfinity(r.Rate)) {Clear();return;}
   Samples.Add(r.Rate);Times.Add(now);
   while(Times.Count>1&&((now-Times[0]).TotalSeconds>300||Times.Count>301)){Samples.RemoveAt(0);Times.RemoveAt(0);}
   if(displayAt==DateTime.MinValue){displayedRate=r.Rate;displayAt=now;}
   else if((now-displayAt).TotalSeconds>=5){displayedRate=WindowAverage(5);displayAt=now;}
  }
  // Display only: keep the raw graph and learner inputs unchanged.
  public double DisplayRate {get{return Samples.Count>0&&Samples.Count==Times.Count?displayedRate:-1;}}
  public double MinuteAverage {get{return WindowAverage(60);}}
  public double RecentAverage {get{return WindowAverage(5);}}
  double WindowAverage(double seconds) {
    if(Samples.Count==0||Samples.Count!=Times.Count)return -1;
    DateTime cutoff=Times[Times.Count-1].AddSeconds(-seconds);double sum=0;int count=0;
    for(int i=Samples.Count-1;i>=0&&Times[i]>cutoff;i--){sum+=Samples[i];count++;}
    return count>0?sum/count:-1;
  }
  public double SpanSeconds {get{return Times.Count<2?0:(Times[Times.Count-1]-Times[0]).TotalSeconds;}}
  public double Average { get {if(Samples.Count==0)return -1;if(SpanSeconds<=0)return Samples.Average();double sum=0;for(int i=1;i<Times.Count;i++)sum+=Samples[i-1]*(Times[i]-Times[i-1]).TotalSeconds;return sum/SpanSeconds;} }
  public static double Minutes(Reading r,double average) {
   if(average<=0 || r.Remaining<0) return -1;
   if(r.Charging) return r.Full>0 ? Math.Max(0,r.Full-r.Remaining)/average*60 : -1;
   return r.Discharging ? r.Remaining/average*60 : -1;
  }
 }
 public static class Battery {
  [StructLayout(LayoutKind.Sequential)] struct Power { public byte AC,Flag,Percent,Reserved; public uint Life,FullLife; }
  [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out Power status);
  static double Num(ManagementBaseObject o,string key) { try { var v=o[key]; if(v==null) return -1; double d=Convert.ToDouble(v); return d<0 || d>=2147483647 ? -1 : d; } catch { return -1; } }
  static bool Yes(ManagementBaseObject o,string key) { try { return Convert.ToBoolean(o[key]); } catch { return false; } }
  static Dictionary<string,double> Capacities(string cls,string prop) {
   var map=new Dictionary<string,double>();
   try { using(var s=new ManagementObjectSearcher("root\\wmi","SELECT * FROM "+cls)) { s.Options.Timeout=TimeSpan.FromSeconds(3); using(var rows=s.Get()) foreach(ManagementObject o in rows) { using(o) { map[Convert.ToString(o["InstanceName"])]=Num(o,prop); } } } } catch {}
   return map;
  }
  static Dictionary<string,double> full=new Dictionary<string,double>(), design=new Dictionary<string,double>(); static DateTime metadata=DateTime.MinValue;
  public static Reading Read() { var native=NativeBattery.Read(); if(native!=null) { Power status; if(GetSystemPowerStatus(out status)) { native.Online=native.Online||status.AC==1; native.FullReported=native.Online&&!native.Charging&&status.Percent==100; } return native; } return ReadFallback(); }
  static Reading ReadFallback() {
   var r=new Reading(); Power p;
   if(GetSystemPowerStatus(out p)) { r.Present=p.Flag==255 || (p.Flag&128)==0; r.Online=p.AC==1; r.Charging=p.Flag!=255 && (p.Flag&8)!=0; r.Discharging=r.Present && p.AC==0; r.Percent=p.Percent<=100?p.Percent:-1; }
   if(!r.Present) return r;
   if((DateTime.UtcNow-metadata).TotalSeconds>60) { full=Capacities("BatteryFullChargedCapacity","FullChargedCapacity"); design=Capacities("BatteryStaticData","DesignedCapacity"); metadata=DateTime.UtcNow; }
   try {
    var list=new List<Reading>();
    using(var s=new ManagementObjectSearcher("root\\wmi","SELECT * FROM BatteryStatus")) { s.Options.Timeout=TimeSpan.FromSeconds(3); using(var rows=s.Get()) foreach(ManagementObject o in rows) { using(o) {
     if(o["Active"]!=null && !Yes(o,"Active")) continue;
     var b=new Reading(); var id=Convert.ToString(o["InstanceName"]); b.Device=id; b.Charging=Yes(o,"Charging"); b.Discharging=Yes(o,"Discharging"); b.Online=Yes(o,"PowerOnline"); b.Remaining=Num(o,"RemainingCapacity"); b.Voltage=Num(o,"Voltage"); b.Full=full.ContainsKey(id)?full[id]:-1; b.Design=design.ContainsKey(id)?design[id]:-1; b.Rate=Num(o,b.Charging?"ChargeRate":"DischargeRate"); if(!b.Charging&&!b.Discharging) b.Rate=-1; list.Add(b);
    } } }
    if(list.Count>0) {
     r.Device=string.Join("|",list.Select(b=>b.Device).OrderBy(x=>x)); r.Detailed=true; r.Charging=list.Any(b=>b.Charging); r.Discharging=!r.Charging&&list.Any(b=>b.Discharging); r.Online=list.Any(b=>b.Online);
     r.Remaining=list.All(b=>b.Remaining>=0)?list.Sum(b=>b.Remaining)/1000:-1; r.Full=list.All(b=>b.Full>0)?list.Sum(b=>b.Full)/1000:-1; r.Design=list.All(b=>b.Design>0)?list.Sum(b=>b.Design)/1000:-1;
     bool mixed=list.Any(b=>b.Charging)&&list.Any(b=>b.Discharging);
     r.Rate=!mixed && list.All(b=>b.Rate>=0)?list.Sum(b=>b.Rate)/1000:-1; r.Voltage=list.Count==1&&list[0].Voltage>0?list[0].Voltage/1000:-1;
     if(list.Count==1&&r.Full>0&&r.Remaining>=0) r.Percent=Math.Min(100,r.Remaining/r.Full*100);
    }
   } catch {}
   // Without battery capability flags, WMI values cannot safely be labelled as absolute W/Wh.
   r.Rate=r.Remaining=r.Full=r.Design=-1;r.Detailed=false;r.Device="";
   return r;
  }
 }
 public class Program {
  static System.Threading.Mutex instance;
  Reading current; bool analyzing; int historyGeneration,readCount,paintCount,sourceRevision; string analysisMessage="";
  ActivityGate activity=new ActivityGate();RestRecord rest=new RestRecord();PowerWatch powerWatch;
  DateTime currentAt=DateTime.MinValue;bool wasActive,resumeBaseline; readonly DateTime clockStart=DateTime.UtcNow; readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew(); DateTime ClockNow {get{return clockStart.Add(clock.Elapsed);}}
  Window window; Learner learner=new Learner(null); Meter meter=new Meter(); Forms.NotifyIcon tray; System.Drawing.Icon trayIcon; bool busy,closing; DispatcherTimer timer;
  TextBlock T(string name) { return (TextBlock)window.FindName(name); }
  static string N(double v) { return v<0 ? "—" : v.ToString("0.0",L.Culture); }
  [STAThread] public static int Main(string[] args) {
   L.Initialize(args);
   if(args.Contains("--language-test"))return L.CheckLanguages();
   if(args.Contains("--standby-test"))return StandbyTests.Run();
   if(args.Contains("--powerwatch-test"))return StandbyTests.CheckNotifications();
   if(args.Length==2&&args[0]=="--history-generate") {try{var h=BatteryHistory.Generate();File.WriteAllText(args[1],h.Summary+"\nTypical watts: "+h.Watts.ToString("0.00",CultureInfo.InvariantCulture));return h.Useful?0:2;}catch(Exception e){File.WriteAllText(args[1],e.Message);return 1;}}
   if(args.Length==3&&args[0]=="--history-check") {try {var h=BatteryHistory.Parse(args[1],DateTime.UtcNow);File.WriteAllText(args[2],h.Summary+"\nTypical watts: "+h.Watts.ToString("0.00",CultureInfo.InvariantCulture)+"\nDays: "+h.Days);return h.Useful?0:2;}catch(Exception e){File.WriteAllText(args[2],e.Message);return 1;}}
   if(args.Contains("--history-test"))return HistoryTests.Run();
   if(args.Contains("--v2-test"))return V2Tests.Run();
   if(args.Contains("--learning-test")) return LearningTests.Run();
   if(args.Contains("--self-test")) {
    var r=new Reading { Remaining=40, Full=80, Discharging=true,Rate=20 }; var m=new Meter();m.Add(r);
    if(Meter.Minutes(r,20)!=120) return 1;
    r.Discharging=false;r.Charging=true;m.Add(r);if(m.Samples.Count!=1||Meter.Minutes(r,40)!=60) return 2;
    r.Rate=-1;m.Add(r);if(m.Average!=-1||Meter.Minutes(r,0)!=-1) return 3;
    r.Remaining=90;if(Meter.Minutes(r,40)!=0) return 4;
    r.Charging=false;if(Meter.Minutes(r,40)!=-1) return 5;
    File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),"PASS: discharge, charge, mode reset, unavailable rate, zero rate, full battery, idle.");return 0;
   }
   if(args.Contains("--probe")) { var r=Battery.Read(); File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"probe.txt"),string.Format(CultureInfo.InvariantCulture,"mode={0}\npercent={1:F1}\nrate_W={2}\nremaining_Wh={3}\nfull_Wh={4}\ndesign_Wh={5}\ndetailed={6}",r.Mode,r.Percent,r.Rate,r.Remaining,r.Full,r.Design,r.Detailed));return 0; }
   if(!args.Contains("--preview")&&!args.Contains("--smoke-test")) {
    bool first;
    instance=new System.Threading.Mutex(true,"Local\\Pulse-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out first);
    if(!first) {MessageBox.Show(L.T("Pulse zaten çalışıyor. Sistem tepsisindeki simgeden açabilirsiniz."),"Pulse");instance.Dispose();return 0;}
   }
   var app=new Application(); var ui=new Program();ui.Build();
   if(args.Contains("--preview")) {
    ui.activity.Available=true;ui.activity.Display(1);
    var r=new Reading {Percent=74,Remaining=58.7,Full=79.3,Design=90,Rate=18.4,Voltage=12.5,Discharging=true,Detailed=true,Native=true};
    DateTime demoTime=DateTime.UtcNow.AddSeconds(-90);foreach(double d in new double[]{15,14,17,16,19,15,16,14,13,17,21,18,20,16,15,18,17,18.4}) {r.Rate=d;demoTime=demoTime.AddSeconds(5);ui.meter.Add(r,demoTime);ui.learner.Add(r,demoTime);}
    if(args.Contains("--idle")){r.Percent=99;r.Remaining=r.Full*.99;r.Rate=0;r.Online=true;r.Discharging=false;r.Charging=false;ui.learner.Add(r,demoTime.AddSeconds(5));ui.meter.Add(r,demoTime.AddSeconds(5));}
    ui.Paint(r);ui.T("DemoLabel").Text=L.T("ÖRNEK VERİ");var visual=(FrameworkElement)ui.window.Content;visual.Resources=ui.window.Resources;System.Windows.Documents.TextElement.SetForeground(visual,ui.window.Foreground);System.Windows.Documents.TextElement.SetFontFamily(visual,ui.window.FontFamily);ui.window.Content=null;visual.Measure(new Size(372,590));visual.Arrange(new Rect(0,0,372,590));visual.UpdateLayout();ui.Graph();visual.UpdateLayout();
    var bmp=new RenderTargetBitmap(744,1180,192,192,PixelFormats.Pbgra32);bmp.Render(visual);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,args.Contains("--idle")?(L.Turkish?"idle-tr.png":"idle-en.png"):(L.Turkish?"preview-tr.png":"preview-en.png")))) enc.Save(f);return 0;
   }
   bool smoke=args.Contains("--smoke-test");
   ui.learner=new Learner(smoke?System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-work","profile.xml"):System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pulse","profile-v1.xml")); ui.SetupTray(); ui.window.SourceInitialized+=(s,e)=>{ui.powerWatch=new PowerWatch(new System.Windows.Interop.WindowInteropHelper(ui.window).Handle,ui.activity,ui.PowerChanged);};ui.window.Loaded+=async (s,e)=>await ui.Refresh(); ui.timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};ui.timer.Tick+=async(s,e)=>await ui.Refresh();ui.timer.Start();if(smoke)ui.Smoke();app.Run(ui.window);return 0;
  }
  void Build() {
   using(var stream=typeof(Program).Assembly.GetManifestResourceStream("Widget.xaml")) using(var reader=new StreamReader(stream))window=(Window)XamlReader.Parse(L.Xaml(reader.ReadToEnd()));
   using(var iconStream=typeof(Program).Assembly.GetManifestResourceStream("Pulse.ico")) {
    var decoder=BitmapDecoder.Create(iconStream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
    var icon=decoder.Frames.OrderByDescending(f=>f.PixelWidth).First();icon.Freeze();window.Icon=icon;
   }
   var area=SystemParameters.WorkArea;window.Left=Math.Max(area.Left,area.Right-window.Width-24);window.Top=area.Top+36;
   // Include the top border and padding, while keeping title-bar buttons clickable.
   window.PreviewMouseLeftButtonDown+=(s,e)=>{
    var header=(Grid)window.FindName("DragArea");
    double bottom=header.TranslatePoint(new Point(0,header.ActualHeight),window).Y;
    if(e.GetPosition(window).Y>bottom)return;
    for(var node=e.OriginalSource as DependencyObject;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
     if(node is System.Windows.Controls.Primitives.ButtonBase)return;
    if(System.Windows.Input.Mouse.LeftButton==System.Windows.Input.MouseButtonState.Pressed){e.Handled=true;window.DragMove();}
   };
   ((Button)window.FindName("Quit")).Click+=(s,e)=>window.Close();
   ((Button)window.FindName("Hide")).Click+=(s,e)=>{if(tray!=null) window.Hide();};
   ((Button)window.FindName("Pin")).Click+=(s,e)=>{ window.Topmost=!window.Topmost;((Button)s).Content=window.Topmost?"◆":"◇";};
   window.IsVisibleChanged+=(s,e)=>{SetInterval();if(window.IsVisible&&current!=null)Paint(current);};
   window.Closed+=(s,e)=>{closing=true;if(timer!=null)timer.Stop();if(powerWatch!=null)powerWatch.Dispose();if(tray!=null)tray.Dispose();if(trayIcon!=null)trayIcon.Dispose();learner.ResetLive();learner.Save(DateTime.UtcNow,true);};
   ((Canvas)window.FindName("Chart")).SizeChanged+=(s,e)=>Graph();
   ((Button)window.FindName("Analyze")).Click+=async(s,e)=>await AnalyzeHistory();
  }
  async Task AnalyzeHistory() {
   if(analyzing)return;
   if(current==null||string.IsNullOrEmpty(current.Device)) {analysisMessage=L.T("Önce batarya bilgisi okunmalı");T("HistoryStatus").Text=analysisMessage;return;}
   analyzing=true;int generation=historyGeneration;string device=current.Device;
   ((Button)window.FindName("Analyze")).IsEnabled=false;T("HistoryStatus").Text=L.T("Windows pil geçmişi inceleniyor…");
   try {
    var report=await Task.Run(()=>BatteryHistory.Generate());
    if(closing||generation!=historyGeneration||current.Device!=device)return;
    if(report.Useful)learner.Import(report,device);
    analysisMessage=report.Summary;
   }catch(Exception e){if(!closing)analysisMessage=e is TimeoutException?e.Message:L.T("Rapor okunamadı · canlı öğrenme devam ediyor");}
   finally {analyzing=false;if(!closing){((Button)window.FindName("Analyze")).IsEnabled=true;if(current!=null)Paint(current);}}
  }
  void SetupTray() {
   trayIcon=System.Drawing.Icon.ExtractAssociatedIcon(typeof(Program).Assembly.Location);
   tray=new Forms.NotifyIcon{Icon=trayIcon,Text=L.T("Pulse · Pil widget’ı"),Visible=true}; var menu=new Forms.ContextMenuStrip();var restOption=new Forms.ToolStripMenuItem(L.T("Bekleme kaybını göster")){Checked=learner.Data.RecordRest,CheckOnClick=true};restOption.CheckedChanged+=(s,e)=>{learner.Data.RecordRest=restOption.Checked;rest.Clear();learner.Save(DateTime.UtcNow,true);if(current!=null)Paint(current);};menu.Items.Add(restOption);menu.Items.Add(L.T("Pulse’u göster"),null,(s,e)=>{window.Show();window.Activate();});menu.Items.Add(L.T("Öğrenme geçmişini sıfırla"),null,(s,e)=>{historyGeneration++;analysisMessage="";learner.Reset();learner.Data.RecordRest=restOption.Checked;rest.Clear();learner.Save(DateTime.UtcNow,true);T("NormalUse").Text=L.T("Genel kullanım profili öğreniliyor");});menu.Items.Add(L.T("Kapat"),null,(s,e)=>window.Close());tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>{window.Show();window.Activate();}; }
  void PowerChanged() {
   bool active=activity.CanLearn;
   if(wasActive&&!active&&learner.Data.RecordRest)rest.Begin(current,currentAt,ClockNow);
   if(sourceRevision!=activity.SourceRevision&&rest.Pending)rest.Invalidate();
   if(active!=wasActive||sourceRevision!=activity.SourceRevision){learner.ResetLive();meter.Clear();}
   sourceRevision=activity.SourceRevision;
   if(!wasActive&&active)resumeBaseline=true;
   wasActive=active;
   SetInterval();
   if(current!=null)Paint(current);
  }
  void SetInterval(){if(timer!=null)timer.Interval=TimeSpan.FromSeconds(!activity.CanLearn?60:window.IsVisible&&(current==null||current.Native)?1:5);}
  void Smoke() {
   // Isolated diagnostic run: no real profile writes, no extra visible window.
   window.Left=-30000;window.Top=-30000;window.ShowInTaskbar=false;
   int tick=0,reads=0,paints=0;double cpu=0,at=0;var report=new List<string>();
   var diagnostic=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};
   diagnostic.Tick+=(s,e)=>{tick++;using(var p=System.Diagnostics.Process.GetCurrentProcess()){
    double elapsed=clock.Elapsed.TotalSeconds;
    if(tick==1){cpu=p.TotalProcessorTime.TotalSeconds;at=elapsed;reads=readCount;paints=paintCount;}
    if(tick==4||tick==7){report.Add(string.Format(CultureInfo.InvariantCulture,"{0}: {1:F2}s, single-core CPU {2:F3}%, working set {3:F1} MiB, private bytes {4:F1} MiB, reads {5}, paints {6}, native {7}, active {8}",tick==4?"visible-offscreen":"tray",elapsed-at,(p.TotalProcessorTime.TotalSeconds-cpu)/(elapsed-at)*100,p.WorkingSet64/1048576.0,p.PrivateMemorySize64/1048576.0,readCount-reads,paintCount-paints,current!=null&&current.Native,activity.CanLearn));cpu=p.TotalProcessorTime.TotalSeconds;at=elapsed;reads=readCount;paints=paintCount;
     if(tick==4)window.Hide();else{diagnostic.Stop();File.WriteAllLines(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-test-result.txt"),report);window.Close();}
    }
   }};diagnostic.Start();
  }
  async Task Refresh() {
   if(busy||closing||(!activity.CanLearn&&!learner.Data.RecordRest&&activity.Available))return;
   busy=true;int revision=activity.Revision;
   try {
    var r=await Task.Run(()=>Battery.Read());if(closing||revision!=activity.Revision)return;
    readCount++;current=r;currentAt=ClockNow;SetInterval();
    if(!activity.Accept(revision)){if(learner.Data.RecordRest)rest.Observe(r);if(window.IsVisible)Paint(r);return;}
    if(learner.Data.RecordRest&&rest.Pending){rest.Finish(r,currentAt);learner.RecordRest(rest.LastLoss,rest.LastMinutes,currentAt);if(rest.LastLoss>=0)analysisMessage="";}
    if(resumeBaseline){resumeBaseline=false;Paint(r);return;}
    learner.Add(r,currentAt);meter.Add(r,currentAt);if(window.IsVisible)Paint(r);
   }catch {T("Footer").Text=L.T("Pil verisi okunamadı · tekrar deneniyor");}finally{busy=false;}
  }
  void Paint(Reading r) {
   paintCount++;
   current=r;
   T("Percent").Text=r.Percent<0?"—":Math.Round(r.Percent).ToString();
   T("Status").Text=!r.Present?L.T("PİL ALGILANMADI"):r.Charging&&!learner.ChargePaused?L.T("ŞARJ OLUYOR"):r.Online?L.T("PRİZE BAĞLI"):r.Discharging?L.T("PİLDEN ÇALIŞIYOR"):L.T("PİL DURUMU BEKLENİYOR");
   T("Eta").Text=learner.DurationText(r);
   T("EtaCaption").Text=r.Charging&&!learner.ChargePaused?L.T("tam doluma tahmini"):r.Discharging&&!r.Online?L.T("tahmini kalan süre"):L.T("şarj / deşarj yok");
   T("EstimateNote").Text=learner.Note(r);


   ((Border)window.FindName("BatteryFill")).Width=310*Math.Max(0,r.Percent)/100;
   ((Border)window.FindName("BatteryFill")).Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(r.Percent>=0&&r.Percent<=20?"#EDB77A":"#B8F4CC"));
   T("RateLabel").Text=r.Charging?L.T("Güncel Şarj Gücü"):L.T("Güncel Tüketim");T("Rate").Text=N(meter.DisplayRate);T("Rate").ToolTip=T("RateLabel").ToolTip=L.T("5 saniyede bir yenilenir · son 5 saniyenin ortalaması");T("Average").Text=L.T("1 dk ort. ")+N(meter.MinuteAverage)+" W";T("Average").ToolTip=L.T("Son 1 dakikanın ortalaması · başlangıçta mevcut ölçümler kullanılır");
   T("Capacity").Text=N(r.Remaining)+" / "+N(r.Full)+" Wh";T("Voltage").Text=r.Voltage>0?N(r.Voltage)+L.T(" V · batarya voltajı"):L.T("Voltaj bilgisi sunulmuyor");
   T("Health").Text=r.Design>0&&r.Full>0?L.Percent(r.Full/r.Design*100):"—";
   T("Design").Text=r.Design>0?L.T("Tasarım: ")+N(r.Design)+" Wh":L.T("Üretici verisi sunulmuyor");
   T("Footer").Text=!r.Present?L.T("Windows bir pil bildirmiyor"):r.Detailed?(r.Native?L.T("●  Canlı · 1 sn aralıkla"):L.T("●  Canlı · 5 sn aralıkla")):L.T("Sınırlı veri · Windows pil durumu");
   double normal=learner.NormalWatts;T("NormalUse").Text=normal>0&&r.Full>0?L.T("Genel kullanım · tam pille ≈ ")+Math.Round(r.Full/normal*60)+L.T(" dk"):L.T("Genel kullanım profili öğreniliyor");
   T("NormalUse").ToolTip=L.T("Bu cihazdaki tipik tüketimden hesaplanır. Windows geçmişi veya en az 20 dakika canlı ölçüm kullanılır; aykırı dakika ölçümleri dışarıda tutulur. Kalan süre mevcut kullanıma göre ayrıca hesaplanır.");
   T("HistoryStatus").Text=analyzing?L.T("Windows pil geçmişi inceleniyor…"):analysisMessage!=""?analysisMessage:learner.HasHistory?learner.Data.History.Summary:L.T("İlk tahmini Windows geçmişinle başlat");
   ((Button)window.FindName("Analyze")).Content=learner.HasHistory?L.T("Pil geçmişini yeniden analiz et"):L.T("Pil geçmişimi analiz et");
   if(learner.SaveFailed)T("Footer").Text=L.T("Geçmiş kaydedilemiyor · bu oturum öğreniyor");
   if(!activity.CanLearn){T("Eta").Text=L.T("— dk");T("EstimateNote").Text=L.T("Bekleme ölçümleri öğrenmeye dahil edilmez");T("Footer").Text=activity.Available?L.T("Ekran kapalı / uyku · öğrenme duraklatıldı"):L.T("Güç durumu alınamadı · öğrenme duraklatıldı");}
   if(learner.Data.RecordRest&&rest.LastLoss>=0&&!analyzing&&analysisMessage=="") {T("HistoryStatus").Text=L.T("Son bekleme: ")+N(rest.LastLoss)+" Wh / "+Math.Round(rest.LastMinutes)+L.T(" dk");T("HistoryStatus").ToolTip=L.T("Ekran kapalı/uyku döneminin yaklaşık net pil kaybı. Genel kullanım ve şarj öğrenmesine katılmaz.");}
   T("ChartNote").Text=meter.Samples.Count<2?L.T("Ölçümler toplanıyor"):L.T("Son ")+Math.Round(meter.SpanSeconds)+L.T(" saniye");Graph();
  }
  void Graph() {
   var c=(Canvas)window.FindName("Chart");c.Children.Clear();double w=c.ActualWidth,h=c.ActualHeight-10;if(w<=0||h<=0||meter.Samples.Count<2)return;
   var line=new Polyline{Stroke=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8F4CC")),StrokeThickness=1.8,StrokeLineJoin=PenLineJoin.Round};double max=Math.Max(1,meter.Samples.Max()*1.25);
   for(int i=0;i<meter.Samples.Count;i++){double x=meter.SpanSeconds>0?(meter.Times[i]-meter.Times[0]).TotalSeconds/meter.SpanSeconds:(double)i/(meter.Samples.Count-1);line.Points.Add(new Point(w*x,h-meter.Samples[i]/max*h));}c.Children.Add(line);
  }
 }
}
