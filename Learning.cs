using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Pulse {
 public class MinuteSample { public double Watts; public DateTime Time; public double Seconds=60; public string Kind="normal"; }
 public class ChargeBand { public double Watts,Seconds,Energy,TraversalSeconds,TraversalWh; public int Count,Sessions,Traversals; public DateTime Updated; public string LastSession=""; }
 public class DaySummary {
  public DateTime Date; public double NormalSeconds,NormalWh,HighSeconds,HighWh,ChargeSeconds,ChargeWh,RestSeconds,RestWh;
  public int Samples; public bool Legacy;
 }
 public class Profile {
  public int Version=2; public bool RecordRest=true; public string Device=""; public HistoryResult History;
  public bool WasOnline; public string ChargeSession="";
  public List<MinuteSample> Normal=new List<MinuteSample>();
  public ChargeBand[] Charge=Enumerable.Range(0,20).Select(i=>new ChargeBand()).ToArray();
  public List<double> DischargeErrors=new List<double>(),ChargeErrors=new List<double>();
  [XmlIgnore] public List<DaySummary> Days=new List<DaySummary>();
 }
 public class Learner {
  public Profile Data=new Profile(); public bool SaveFailed; public double Smoothed=-1,EffectiveRate=-1;
  readonly string path; readonly HashSet<DateTime> dirty=new HashSet<DateTime>();
  class Point { public DateTime At; public double Watts,Seconds; }
  readonly List<Point> recent=new List<Point>();
  readonly List<Point> capacities=new List<Point>();
  readonly List<MinuteSample> pending=new List<MinuteSample>();
  static class XmlCache<T>{internal static readonly XmlSerializer Serializer=new XmlSerializer(typeof(T));}
  DateTime last=DateTime.MinValue,saved=DateTime.MinValue,forecastAt,zeroAt=DateTime.MinValue;
  Reading previous; string mode="",session=Guid.NewGuid().ToString("N"); int band=-1;
  double minuteSeconds,minuteWh,forecastCapacity=-1,forecastWatts,zeroCapacity=-1;bool forecastChanged;
  DateTime traversalStart=DateTime.MinValue;int traversalBand=-1;double traversalFull;
  public int RecentCount {get{return recent.Count;}}
  public Learner(string file){path=file;Load();}
  public static bool Valid(double v){return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>0&&v<2000;}
  public static double Median(IEnumerable<double> values){var a=values.OrderBy(x=>x).ToArray();return a.Length==0?-1:(a[(a.Length-1)/2]+a[a.Length/2])/2;}
  static T ReadXml<T>(string file,long limit) {
   if(new FileInfo(file).Length>limit)throw new InvalidDataException("Profile too large");
   using(var r=XmlReader.Create(file,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=limit}))return (T)XmlCache<T>.Serializer.Deserialize(r);
  }
  static void Atomic<T>(string file,T value) {
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
   string tmp=file+".tmp";using(var s=File.Create(tmp))XmlCache<T>.Serializer.Serialize(s,value);
   if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);
  }
  string Archive {get{using(var hash=System.Security.Cryptography.SHA256.Create())return Path.Combine(path+".days",BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Data.Device))).Replace("-",""));}}
  void Load() {
   if(path==null||!File.Exists(path))return;
   try {
    var p=ReadXml<Profile>(path,4*1024*1024);if(p.Version<1||p.Version>2||p.Normal==null||p.Charge==null)throw new InvalidDataException();
    bool legacy=p.Version==1;
    if(legacy) {
     // Preserve the original; its minute medians are approximate historical observations.
     if(!File.Exists(path+".v1.bak"))File.Copy(path,path+".v1.bak");
     var bands=new Profile().Charge;
     for(int i=0;i<Math.Min(10,p.Charge.Length);i++)if(p.Charge[i]!=null)for(int j=i*2;j<i*2+2;j++)bands[j]=new ChargeBand{Watts=p.Charge[i].Watts,Count=p.Charge[i].Count,Updated=p.Charge[i].Updated};
     p.Charge=bands;p.Version=2;
    }
    if(p.Charge.Length!=20||p.Charge.Any(x=>x==null))throw new InvalidDataException();
    p.Normal=p.Normal.Where(x=>x!=null&&Valid(x.Watts)&&x.Time<=DateTime.UtcNow&&x.Seconds>0&&x.Seconds<=60).ToList();
    Data=p;Data.Days=new List<DaySummary>();pending.AddRange(p.Normal);
    if(legacy)foreach(var m in p.Normal){var d=Day(m.Time);d.NormalSeconds+=m.Seconds;d.NormalWh+=m.Watts*m.Seconds/3600;d.Samples++;d.Legacy=true;}
    else if(Directory.Exists(Archive))foreach(string f in Directory.GetFiles(Archive,"*.xml").OrderByDescending(x=>x).Take(180)) {
     try{var d=ReadXml<DaySummary>(f,65536);if(ValidDay(d))Data.Days.Add(d);}catch{/* Isolate damage to a single day. */}
    }
    foreach(var b in Data.Charge)if(!Valid(b.Watts)||b.Seconds<0||b.Energy<0||b.Updated>DateTime.UtcNow){b.Watts=0;b.Count=0;b.Sessions=0;b.Seconds=0;b.Energy=0;}
    Data.DischargeErrors=CleanErrors(Data.DischargeErrors);Data.ChargeErrors=CleanErrors(Data.ChargeErrors);
    ReadMinutes();Data.Normal=Data.Normal.GroupBy(x=>new{x.Time,x.Seconds,x.Watts,x.Kind}).Select(x=>x.First()).ToList();Trim(DateTime.UtcNow);if(legacy||pending.Count>0)Save(DateTime.UtcNow,true);
   }catch{try{if(!File.Exists(path+".unreadable.bak"))File.Copy(path,path+".unreadable.bak");}catch{}Data=new Profile();SaveFailed=true;}
  }
  static List<double> CleanErrors(List<double> a){return (a??new List<double>()).Where(x=>x>=0&&x<=5&&!double.IsNaN(x)).Take(64).ToList();}
  static bool ValidDay(DaySummary d){return d!=null&&d.Date<=DateTime.UtcNow&&new[]{d.NormalSeconds,d.NormalWh,d.HighSeconds,d.HighWh,d.ChargeSeconds,d.ChargeWh,d.RestSeconds,d.RestWh}.All(x=>x>=0&&!double.IsNaN(x)&&!double.IsInfinity(x));}
  DaySummary Day(DateTime at){DateTime date=at.Date;var d=Data.Days.FirstOrDefault(x=>x.Date==date);if(d==null){d=new DaySummary{Date=date};Data.Days.Add(d);}dirty.Add(date);return d;}
  void Trim(DateTime now){Data.Normal.RemoveAll(x=>x.Time<now.AddHours(-48));}
  void ReadMinutes(){
   if(path==null||!Directory.Exists(Archive))return;
   var seen=new HashSet<string>();
   foreach(string file in Directory.GetFiles(Archive,"*.minutes").OrderByDescending(x=>x).Take(3)){
    if(new FileInfo(file).Length>8*1024*1024)continue;
    foreach(string line in File.ReadLines(file)){
     string[] p=line.Split('|');long ticks;double seconds,watts;
     if(p.Length!=4||!seen.Add(line)||!long.TryParse(p[0],out ticks)||ticks<DateTime.MinValue.Ticks||ticks>DateTime.MaxValue.Ticks||!double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)||!double.TryParse(p[2],NumberStyles.Float,CultureInfo.InvariantCulture,out watts)||seconds<=0||seconds>60||!Valid(watts)||(p[3]!="normal"&&p[3]!="high"&&p[3]!="charge"))continue;
     var time=new DateTime(ticks,DateTimeKind.Utc);if(time<=DateTime.UtcNow&&time>=DateTime.UtcNow.AddHours(-48))Data.Normal.Add(new MinuteSample{Time=time,Seconds=seconds,Watts=watts,Kind=p[3]});
    }
   }
  }
  void AppendMinutes(DateTime now){
   foreach(var group in pending.GroupBy(m=>m.Time.Date)){
    Directory.CreateDirectory(Archive);
    using(var w=new StreamWriter(Path.Combine(Archive,group.Key.ToString("yyyy-MM-dd")+".minutes"),true)){
     // A leading newline isolates a partially written final record after power loss.
     w.WriteLine();foreach(var m in group)w.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0}|{1:R}|{2:R}|{3}",m.Time.Ticks,m.Seconds,m.Watts,m.Kind));
    }
   }
   // Only compact raw rows after their independent daily summary exists on disk.
   if(Directory.Exists(Archive))foreach(string file in Directory.GetFiles(Archive,"*.minutes")){
    DateTime date;if(DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)&&date.AddDays(1)<now.AddHours(-48)&&File.Exists(Path.ChangeExtension(file,"xml")))File.Delete(file);
   }
  }
  public void Save(DateTime now,bool force) {
   if(path==null||(!force&&(now-saved).TotalMinutes<2))return;
   try {
    foreach(var d in Data.Days.Where(x=>dirty.Contains(x.Date)))Atomic(Path.Combine(Archive,d.Date.ToString("yyyy-MM-dd")+".xml"),d);
    // Archived totals are never reconstructed from recent rows, avoiding double counting after a crash.
    AppendMinutes(now);Trim(now);
    var snapshot=new Profile{Version=Data.Version,Device=Data.Device,RecordRest=Data.RecordRest,History=Data.History,WasOnline=Data.WasOnline,ChargeSession=Data.ChargeSession,Charge=Data.Charge,DischargeErrors=Data.DischargeErrors,ChargeErrors=Data.ChargeErrors};
    Atomic(Path.Combine(Archive,"state.profile"),snapshot);Atomic(path,snapshot);dirty.Clear();pending.Clear();saved=now;SaveFailed=false;
    Data.Days=Data.Days.OrderByDescending(x=>x.Date).Take(180).ToList();
   }catch{SaveFailed=true;}
  }
  void DeleteArchive(){if(path!=null&&Directory.Exists(Archive))foreach(string f in Directory.GetFiles(Archive).Where(x=>x.EndsWith(".xml")||x.EndsWith(".minutes")||x.EndsWith(".profile")))File.Delete(f);}
  public void Reset(){try{DeleteArchive();}catch{SaveFailed=true;return;}bool rest=Data.RecordRest;string device=Data.Device;ResetLive(false);Data=new Profile{Device=device,RecordRest=rest};dirty.Clear();pending.Clear();Save(DateTime.UtcNow,true);}
  void Device(string device){if(string.IsNullOrEmpty(device)||Data.Device==device)return;
   ResetLive();if(!string.IsNullOrEmpty(Data.Device))Save(DateTime.UtcNow,true);
   bool rest=Data.RecordRest;Data=new Profile{Device=device,RecordRest=rest};dirty.Clear();pending.Clear();
   if(path!=null&&File.Exists(Path.Combine(Archive,"state.profile")))try{var p=ReadXml<Profile>(Path.Combine(Archive,"state.profile"),4*1024*1024);if(p.Version==2&&p.Device==device&&p.Charge!=null&&p.Charge.Length==20&&p.Charge.All(b=>b!=null)){Data=p;Data.RecordRest=rest;Data.Days=new List<DaySummary>();}}catch{}
   if(path!=null&&Directory.Exists(Archive))foreach(string f in Directory.GetFiles(Archive,"*.xml").OrderByDescending(x=>x).Take(180))try{var d=ReadXml<DaySummary>(f,65536);if(ValidDay(d))Data.Days.Add(d);}catch{}
   ReadMinutes();
  }
  public void Import(HistoryResult h,string device){if(string.IsNullOrEmpty(device)||!h.Useful)return;Device(device);Data.History=h;Save(DateTime.UtcNow,true);}
  public bool HasHistory {get{return Data.History!=null&&Data.History.Useful&&Data.History.Analyzed<=DateTime.UtcNow&&Data.History.Analyzed>=DateTime.UtcNow.AddDays(-30);}}
  void Flush(){if(minuteSeconds>0&&previous!=null)RecordMinute(minuteWh*3600/minuteSeconds,minuteSeconds,last,previous);minuteSeconds=minuteWh=0;}
  public void ResetLive(){ResetLive(true);}
  void ResetLive(bool flush){if(flush)Flush();recent.Clear();capacities.Clear();previous=null;last=DateTime.MinValue;minuteSeconds=minuteWh=0;Smoothed=EffectiveRate=-1;band=-1;forecastCapacity=-1;forecastChanged=false;zeroAt=DateTime.MinValue;traversalStart=DateTime.MinValue;traversalBand=-1;}
  static Reading Copy(Reading r){return new Reading{Device=r.Device,Rate=r.Rate,Remaining=r.Remaining,Full=r.Full,Percent=r.Percent,Charging=r.Charging,Discharging=r.Discharging,Online=r.Online,Present=r.Present};}
  public void RecordRest(double wh,double minutes,DateTime at){if(wh<0||double.IsNaN(wh)||double.IsInfinity(wh)||minutes<1)return;var d=Day(at);d.RestWh+=wh;d.RestSeconds+=minutes*60;Save(at,false);}
  public bool ChargePaused {get{return zeroAt!=DateTime.MinValue&&(last-zeroAt).TotalSeconds>=120;}}
  double ResolveRate(Reading r,DateTime now) {
   if(r.Remaining<0){capacities.Clear();return Valid(r.Rate)?r.Rate:-1;}
   capacities.Add(new Point{At=now,Watts=r.Remaining});capacities.RemoveAll(p=>(now-p.At).TotalSeconds>300);
   var first=capacities[0];double seconds=(now-first.At).TotalSeconds;
   double delta=r.Charging?r.Remaining-first.Watts:first.Watts-r.Remaining;
   double fallback=seconds>=60&&delta>=Math.Max(.05,2*r.GranularityWh)?delta*3600/seconds:-1;
   return Valid(r.Rate)?r.Rate:r.Rate<0&&Valid(fallback)?fallback:-1;
  }
  double WeightedMedian(IEnumerable<Point> points){var a=points.OrderBy(x=>x.Watts).ToList();double half=a.Sum(x=>x.Seconds)/2,total=0;foreach(var p in a){total+=p.Seconds;if(total>=half)return p.Watts;}return -1;}
  public void Add(Reading r,DateTime now) {
   Device(r.Device);double gap=(now-last).TotalSeconds;
   if(r.Online&&(!Data.WasOnline||Data.ChargeSession==""))Data.ChargeSession=Guid.NewGuid().ToString("N");
   Data.WasOnline=r.Online;session=Data.ChargeSession;
   if(mode!=r.Mode||previous!=null&&previous.Online!=r.Online||gap>20||gap<=0||!r.Present)ResetLive();
   mode=r.Mode;gap=last==DateTime.MinValue?0:(now-last).TotalSeconds;
   if(previous!=null&&r.Remaining>=0&&previous.Remaining>=0&&((r.Discharging&&r.Remaining>previous.Remaining+.05)||(r.Charging&&r.Remaining<previous.Remaining-.05))) {ResetLive();gap=0;}
   last=now;
   if(r.Online&&r.Charging&&r.Rate==0){if(zeroAt==DateTime.MinValue||r.Remaining>=0&&zeroCapacity>=0&&Math.Abs(r.Remaining-zeroCapacity)>Math.Max(.01,r.GranularityWh)){zeroAt=now;zeroCapacity=r.Remaining;}}else zeroAt=DateTime.MinValue;
   EffectiveRate=ResolveRate(r,now);
   if(!Valid(EffectiveRate)||(!r.Charging&&!r.Discharging)||ChargePaused){Flush();recent.Clear();Smoothed=-1;forecastCapacity=-1;traversalStart=DateTime.MinValue;traversalBand=-1;previous=Copy(r);return;}
   recent.Add(new Point{At=now,Watts=EffectiveRate,Seconds=gap>0?gap:1});recent.RemoveAll(p=>(now-p.At).TotalSeconds>=120);
   double target=WeightedMedian(recent.Where(p=>(now-p.At).TotalSeconds<30));
   Smoothed=Smoothed<0?target:Smoothed+(1-Math.Exp(-gap/15))*(target-Smoothed);
   TrackTraversal(r,now,gap);
   int nextBand=r.Charging&&r.Percent>=0?Math.Min(19,(int)(r.Percent/5)):-1;
   bool crossed=band!=nextBand;
   if(band<0&&r.Charging)band=nextBand;
   // Integrate the preceding rate across elapsed time. Smoothing is display-only.
   if(gap>0&&previous!=null&&Valid(previous.Rate)) {
    if(r.Charging||r.Discharging&&!r.Online) {
     double left=gap;DateTime cursor=now.AddSeconds(-gap);
     while(left>.000001){double take=Math.Min(left,60-minuteSeconds);minuteSeconds+=take;minuteWh+=previous.Rate*take/3600;left-=take;cursor=cursor.AddSeconds(take);
      if(minuteSeconds>=59.999){RecordMinute(minuteWh*60,60,cursor,r);minuteSeconds=minuteWh=0;}
     }
    }else minuteSeconds=minuteWh=0;
   }
   if(crossed){Flush();band=nextBand;}
   if(r.Charging||r.Discharging&&!r.Online)Calibrate(r,now);else forecastCapacity=-1;
   previous=Copy(r);previous.Rate=EffectiveRate;
  }
  void TrackTraversal(Reading r,DateTime now,double gap){
   if(!r.Charging||r.Full<=0||r.Percent<0)return;
   int current=Math.Min(20,(int)(r.Percent/5));
   if(previous==null){if(current<20&&r.Percent-current*5<.01){traversalBand=current;traversalStart=now;traversalFull=r.Full;}return;}
   if(previous.Percent<0||r.Percent<=previous.Percent)return;
   int before=Math.Min(20,(int)(previous.Percent/5));if(before==current)return;
   if(current!=before+1){traversalStart=DateTime.MinValue;traversalBand=-1;return;}
   double fraction=(current*5-previous.Percent)/(r.Percent-previous.Percent);
   DateTime crossing=now.AddSeconds(-gap*(1-fraction));double seconds=(crossing-traversalStart).TotalSeconds;
   if(traversalBand==before&&traversalStart!=DateTime.MinValue&&seconds>=30&&Math.Abs(r.Full-traversalFull)<r.Full*.05){
    var b=Data.Charge[before];if(b.TraversalSeconds>14400){b.TraversalSeconds*=.9;b.TraversalWh*=.9;}
    b.TraversalSeconds+=seconds;b.TraversalWh+=traversalFull*.05;b.Traversals++;
   }
   traversalBand=current<20?current:-1;traversalStart=crossing;traversalFull=r.Full;
  }
  void RecordMinute(double watts,double seconds,DateTime now,Reading r) {
   double baseline=NormalWatts;bool high=!r.Charging&&baseline>0&&watts>Math.Max(baseline*2,baseline+10);
   string kind=r.Charging?"charge":high?"high":"normal";
   var sample=new MinuteSample{Time=now,Watts=watts,Seconds=seconds,Kind=kind};Data.Normal.Add(sample);if(path!=null)pending.Add(sample);Trim(now);
   var d=Day(now);d.Samples++;
   if(r.Charging){d.ChargeSeconds+=seconds;d.ChargeWh+=watts*seconds/3600;
    if(band>=0){var b=Data.Charge[band];if(b.LastSession!=session&&session!=""){b.Sessions++;b.LastSession=session;}
     if(b.Seconds>7200){b.Seconds*=.9;b.Energy*=.9;}b.Seconds+=seconds;b.Energy+=watts*seconds/3600;b.Watts=b.Energy/b.Seconds*3600;b.Count++;b.Updated=now;}
   }else if(high){d.HighSeconds+=seconds;d.HighWh+=watts*seconds/3600;}else{d.NormalSeconds+=seconds;d.NormalWh+=watts*seconds/3600;}
   Save(now,false);
  }
  void Calibrate(Reading r,DateTime now) {
   if(r.Remaining<0||!Valid(Smoothed)){forecastCapacity=-1;return;}
   if(forecastCapacity<0){forecastCapacity=r.Remaining;forecastAt=now;forecastWatts=Smoothed;forecastChanged=Variable;return;}
   forecastChanged=forecastChanged||Variable;
   double seconds=(now-forecastAt).TotalSeconds;if(seconds<300)return;
   double observed=r.Charging?r.Remaining-forecastCapacity:forecastCapacity-r.Remaining;
   double predicted=forecastWatts*seconds/3600;
   if(!forecastChanged&&observed>=Math.Max(.05,2*r.GranularityWh)&&predicted>0){var errors=r.Charging?Data.ChargeErrors:Data.DischargeErrors;errors.Add(Math.Min(5,Math.Abs(observed-predicted)/observed));if(errors.Count>64)errors.RemoveAt(0);}
   forecastCapacity=r.Remaining;forecastAt=now;forecastWatts=Smoothed;forecastChanged=Variable;
  }
  public double NormalWatts {get {
   var latestDays=Data.Days.Where(d=>d.NormalSeconds+d.HighSeconds>=1200).OrderByDescending(d=>d.Date).Take(7).ToList();
   bool regularHigh=latestDays.Count(d=>d.HighSeconds>=d.NormalSeconds&&d.HighSeconds>=1200)>=5;
   var days=Data.Days.Select(d=>new DaySummary{Date=d.Date,NormalSeconds=d.NormalSeconds+(regularHigh?d.HighSeconds:0),NormalWh=d.NormalWh+(regularHigh?d.HighWh:0)}).Where(d=>d.NormalSeconds>=60).ToList();double seconds=days.Sum(d=>d.NormalSeconds);
   if(seconds<1200)return HasHistory?Data.History.Watts:-1;
   double med=Median(days.Select(d=>d.NormalWh/d.NormalSeconds*3600));
   double mad=Median(days.Select(d=>Math.Abs(d.NormalWh/d.NormalSeconds*3600-med)));
   double threshold=Math.Min(med,Math.Max(med*.6,3*mad));
   var clean=days.Where(d=>Math.Abs(d.NormalWh/d.NormalSeconds*3600-med)<=threshold).ToList();
   DateTime latest=days.Max(d=>d.Date);double energy=0,weight=0;
   foreach(var d in clean){double w=Math.Min(7200,d.NormalSeconds)*Math.Exp(-(latest-d.Date).TotalDays/30);energy+=d.NormalWh/d.NormalSeconds*3600*w;weight+=w;}
   // Windows history is a cold-start prior, never a second vote over overlapping live data.
   return weight>0?energy/weight:-1;
  }}
  bool Warm {get{return recent.Count>1&&(last-recent[0].At).TotalSeconds>=30;}}
  public bool Variable {get{if(!Warm)return false;double seconds=recent.Sum(x=>x.Seconds),avg=recent.Sum(x=>x.Watts*x.Seconds)/seconds;return Math.Sqrt(recent.Sum(x=>(x.Watts-avg)*(x.Watts-avg)*x.Seconds)/seconds)/avg>.25;}}
  bool Known(ChargeBand b){return b.Sessions>=3&&b.Seconds>=180&&Valid(b.Watts);}
  double CurveWatts(ChargeBand b){double measured=b.TraversalSeconds>0?b.TraversalWh*3600/b.TraversalSeconds:-1;return b.Traversals>=3&&Valid(measured)?measured:b.Watts;}
  public double Coverage(Reading r){if(!r.Charging||r.Percent<0)return 0;double all=0,known=0;for(int i=0;i<20;i++){double width=Math.Max(0,(i+1)*5-Math.Max(r.Percent,i*5));all+=width;if(Known(Data.Charge[i]))known+=width;}return all>0?known/all:1;}
  public double Estimate(Reading r) {
   if(!r.Present||r.Online&&!r.Charging||ChargePaused||r.Charging&&r.Rate==0)return -1;
   if(!Warm||!Valid(Smoothed))return r.Discharging&&!r.Online&&HasHistory?Meter.Minutes(r,NormalWatts):-1;
   if(!r.Charging)return Meter.Minutes(r,Smoothed);
   if(r.Full<=0||r.Remaining<0||r.Percent<0)return -1;
   double sum=0;int current=Math.Min(19,(int)(r.Percent/5));
   double offset=Known(Data.Charge[current])?Smoothed-CurveWatts(Data.Charge[current]):0;
   for(int i=current;i<20;i++){double width=Math.Max(0,(i+1)*5-Math.Max(r.Percent,i*5));
    double watts=Known(Data.Charge[i])?CurveWatts(Data.Charge[i])+offset:Smoothed;
    if(watts<=.2)return -1;sum+=r.Full*width/100/watts*60;
   }return sum;
  }
  public string Note(Reading r) {
   if(!r.Present)return L.T("Pil algılanmadı");
   if(r.Online&&r.FullReported)return L.T("Şarj tamamlandı");
   if(r.Online&&(!r.Charging||ChargePaused))return L.T("Prize bağlı · şarj edilmiyor");
   if(r.Charging&&r.Rate==0)return L.T("Şarj durumu doğrulanıyor");
   if(!r.Charging&&!r.Discharging)return L.T("Şarj / deşarj yok");
   if(!Warm)return r.Discharging&&!r.Online&&HasHistory?L.T("Windows geçmişine göre · ilk tahmin"):L.T("Öğreniliyor · ölçümler toplanıyor");
   if(Variable)return L.T("Kullanım değişken · tahmin aralığı geniş");
   if(r.Rate<0&&EffectiveRate>0)return L.T("Kapasite değişimine göre · yaklaşık süre");
   if(r.Charging)return Coverage(r)<.8?L.T("Şarj eğrisi öğreniliyor · yaklaşık süre"):L.T("Şarj geçmişine göre · yaklaşık süre");
   return NormalWatts<0?L.T("Genel kullanım öğreniliyor · yaklaşık süre"):L.T("Dengeli tüketim · yaklaşık süre");
  }
  public string DurationText(Reading r) {
   double value=Estimate(r);if(value<0||value>10080)return L.T("— dk");if(value<1)return L.T("<1 dk");
   var errors=r.Charging?Data.ChargeErrors:Data.DischargeErrors;double uncertainty=Variable||!Warm?.3:r.Charging&&Coverage(r)<.8?.25:.15;
   if(errors.Count>=6){var sorted=errors.OrderBy(x=>x).ToList();uncertainty=Math.Max(uncertainty,Math.Min(.8,sorted[(int)((sorted.Count-1)*.8)]));}
   int lo=Math.Max(1,(int)Math.Floor(value*(1-uncertainty)/5)*5),hi=Math.Max(lo+5,(int)Math.Ceiling(value*(1+uncertainty)/5)*5);
   return lo+"–"+hi+L.T(" dk");
  }
 }
}
