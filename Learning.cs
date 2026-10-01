using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Pulse {
 public class MinuteSample { public double Watts; public DateTime Time; }
 public class ChargeBand { public double Watts; public int Count; public DateTime Updated; }
 public class Profile {
  public int Version=1;
  public bool RecordRest=true;
  public string Device="";
  public HistoryResult History;
  public List<MinuteSample> Normal=new List<MinuteSample>();
  public ChargeBand[] Charge=Enumerable.Range(0,10).Select(i=>new ChargeBand()).ToArray();
 }
 public class Learner {
  public Profile Data=new Profile();
  public bool SaveFailed;
  readonly string path;
  readonly List<double> recent=new List<double>(), minute=new List<double>();
  string mode="";
  DateTime last=DateTime.MinValue, minuteStart=DateTime.MinValue, saved=DateTime.MinValue;
  int band=-1;
  public double Smoothed=-1;
  public int RecentCount { get { return recent.Count; } }
  public Learner(string file) { path=file; Load(); }
  public static double Median(IEnumerable<double> values) { var a=values.OrderBy(v=>v).ToArray();return a.Length==0?-1:(a[(a.Length-1)/2]+a[a.Length/2])/2; }
  public static bool Valid(double v) { return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>0&&v<2000; }
  void Load() {
   if(path==null)return;
   try {
    if(!File.Exists(path)||new FileInfo(path).Length>262144)return;
    using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null})) {
     var p=(Profile)new XmlSerializer(typeof(Profile)).Deserialize(reader);
     if(p.Version!=1||p.Normal==null||p.Charge==null||p.Charge.Length!=10||p.Charge.Any(b=>b==null))return;
     p.Normal=p.Normal.Where(x=>x!=null&&Valid(x.Watts)&&x.Time>DateTime.UtcNow.AddDays(-30)&&x.Time<=DateTime.UtcNow).Take(720).ToList();
     foreach(var b in p.Charge) if(!Valid(b.Watts)||b.Count<0||b.Count>120||b.Updated<DateTime.UtcNow.AddDays(-30)||b.Updated>DateTime.UtcNow) {b.Count=0;b.Watts=0;}
     Data=p;
     if(Data.History!=null&&(!Data.History.Useful||Data.History.Analyzed>DateTime.UtcNow||Data.History.Analyzed<DateTime.UtcNow.AddDays(-30)))Data.History=null;
    }
   } catch { Data=new Profile(); }
  }
  public void Save(DateTime now,bool force) {
   if(path==null||(!force&&(now-saved).TotalMinutes<2))return;
   try {
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temp=path+".tmp";
    using(var writer=File.Create(temp))new XmlSerializer(typeof(Profile)).Serialize(writer,Data);
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    saved=now;SaveFailed=false;
   } catch {SaveFailed=true;}
  }
  public void Reset() {Data=new Profile();Clear();Save(DateTime.UtcNow,true);}
  public void ResetLive(){Clear();}
  public void Import(HistoryResult history,string device) {
   if(string.IsNullOrEmpty(device)||!history.Useful)return;
   if(Data.Device!=device){Data=new Profile{Device=device,RecordRest=Data.RecordRest};Clear();}
   Data.History=history;Save(DateTime.UtcNow,true);
  }
  public bool HasHistory {get{return Data.History!=null&&Data.History.Useful&&Data.History.Analyzed>=DateTime.UtcNow.AddDays(-30)&&Data.History.Analyzed<=DateTime.UtcNow;}}
  void Clear() {recent.Clear();minute.Clear();minuteStart=DateTime.MinValue;Smoothed=-1;band=-1;}
  public void Add(Reading r,DateTime now) {
   if(!string.IsNullOrEmpty(r.Device)&&Data.Device!=r.Device) {Data=new Profile{Device=r.Device,RecordRest=Data.RecordRest};Clear();}
   double gap=(now-last).TotalSeconds;
   if(mode!=r.Mode||gap>20||gap<0)Clear();
   mode=r.Mode;last=now;
   Data.Normal.RemoveAll(x=>x.Time<now.AddDays(-30));
   if(!Valid(r.Rate)||(!r.Charging&&!r.Discharging)) {Clear();return;}
   recent.Add(r.Rate);if(recent.Count>24)recent.RemoveAt(0);
   // Seven-point median rejects short spikes; sustained load wins after ~20 seconds.
   double target=Median(recent.Skip(Math.Max(0,recent.Count-7)));
   Smoothed=Smoothed<0?target:Smoothed+0.3*(target-Smoothed);
   int nextBand=r.Charging&&r.Percent>=0?Math.Min(9,(int)(r.Percent/10)):-1;
   if(nextBand!=band) {minute.Clear();minuteStart=now;band=nextBand;}
   if(minuteStart==DateTime.MinValue)minuteStart=now;
   minute.Add(r.Rate);
   if((now-minuteStart).TotalSeconds>=60) {
    if(minute.Count>=10) {
     double value=Median(minute);
     if(r.Discharging) {Data.Normal.Add(new MinuteSample{Watts=value,Time=now});if(Data.Normal.Count>720)Data.Normal.RemoveAt(0);}
     else if(band>=0) {
      var b=Data.Charge[band];if((now-b.Updated).TotalDays>30)b.Count=0;
      // Slow averaging of minute medians avoids treating one transient as a charging curve.
      b.Watts=b.Count==0?value:b.Watts+(value-b.Watts)/Math.Min(20,b.Count+1);b.Count=Math.Min(120,b.Count+1);b.Updated=now;
     }
    }
    minute.Clear();minuteStart=now;Save(now,false);
   }
  }
  public double NormalWatts {
   get {
    if(Data.Normal.Count<20)return HasHistory?Data.History.Watts:-1;
    double med=Median(Data.Normal.Select(x=>x.Watts));double mad=Median(Data.Normal.Select(x=>Math.Abs(x.Watts-med)));
    double high=med+Math.Max(med*0.6,3*mad),low=Math.Max(0.1,med-Math.Max(med*0.6,3*mad));
    var clean=Data.Normal.Where(x=>x.Watts>=low&&x.Watts<=high).ToList();double live=clean.Average(x=>x.Watts);
    return HasHistory?(live*clean.Count+Data.History.Watts*30)/(clean.Count+30):live;
   }
  }
  public bool Variable { get {if(recent.Count<6)return false;double avg=recent.Average();return Math.Sqrt(recent.Average(x=>(x-avg)*(x-avg)))/avg>0.25;} }
  bool Known(ChargeBand b) {return b.Count>=3&&Valid(b.Watts)&&b.Updated>=last.AddDays(-30);}
  public double Coverage(Reading r) {
   if(!r.Charging||r.Percent<0)return 0;double all=0,known=0;
   for(int i=0;i<10;i++){double width=Math.Max(0,(i+1)*10-Math.Max(r.Percent,i*10));all+=width;if(Known(Data.Charge[i]))known+=width;}
   return all>0?known/all:1;
  }
  public double Estimate(Reading r) {
   if(recent.Count<6||Smoothed<=0)return r.Discharging&&HasHistory?Meter.Minutes(r,NormalWatts):-1;
   if(!r.Charging)return Meter.Minutes(r,Smoothed);
   if(r.Full<=0||r.Remaining<0||r.Percent<0)return -1;
   double sum=0;
   int current=Math.Min(9,(int)(r.Percent/10));
   double scale=Known(Data.Charge[current])?Math.Max(0.4,Math.Min(2.5,Smoothed/Data.Charge[current].Watts)):1;
   for(int i=0;i<10;i++) {
    double width=Math.Max(0,(i+1)*10-Math.Max(r.Percent,i*10));
    double watts=Known(Data.Charge[i])?Data.Charge[i].Watts*scale:Smoothed;
    sum+=r.Full*width/100/watts*60;
   }
   return sum;
  }
  public string Note(Reading r) {
   if(!r.Present)return L.T("Pil algılanmadı");
   if(!r.Charging&&!r.Discharging)return L.T("Şarj / deşarj yok");
   if(recent.Count<6)return r.Discharging&&HasHistory?L.T("Windows geçmişine göre · ilk tahmin"):L.T("Öğreniliyor · ölçümler toplanıyor");
   if(Variable)return L.T("Kullanım değişken · tahmin aralığı geniş");
   if(r.Charging)return Coverage(r)<0.8?L.T("Şarj eğrisi öğreniliyor · yaklaşık süre"):L.T("Şarj geçmişine göre · yaklaşık süre");
   return NormalWatts<0?L.T("Genel kullanım öğreniliyor · yaklaşık süre"):L.T("Dengeli tüketim · yaklaşık süre");
  }
  public string DurationText(Reading r) {
   double value=Estimate(r);if(value<0)return L.T("— dk");
   if(value<1)return L.T("<1 dk");
   double uncertainty=Variable||recent.Count<6?0.25:r.Charging&&Coverage(r)<0.8?0.2:0.1;
   int lo=Math.Max(1,(int)Math.Floor(value*(1-uncertainty)/5)*5),hi=Math.Max(lo+5,(int)Math.Ceiling(value*(1+uncertainty)/5)*5);
   return lo+"–"+hi+L.T(" dk");
  }
 }
}

