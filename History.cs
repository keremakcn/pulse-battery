using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace Pulse {
 public class HistoryResult {
  public double Watts=-1, Minutes;
  public int Included, Excluded, Outliers, Days;
  public DateTime Analyzed;
  public string Message="";
  public bool Useful {get {return Learner.Valid(Watts)&&Included>=3&&Minutes>=60&&Days>=2;}}
  public string Summary {get {return Useful ? Included+L.T(" oturum · ")+Math.Round(Minutes)+L.T(" dk · ")+Outliers+L.T(" aykırı") : Message;}}
 }
 public static class BatteryHistory {
  class Session {public DateTime Start, End;public double Watts, Minutes;}
  static double Number(XmlElement e,string a) {double v;return double.TryParse(e.GetAttribute(a),NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.IsInfinity(v)&&!double.IsNaN(v)?v:-1;}
  static DateTime Date(string v) {DateTime d;return DateTime.TryParse(v,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out d)?d:DateTime.MinValue;}
  static double Median(List<Session> items,Func<Session,double> value) {
   // Equal session votes for detecting exceptions: one long outage cannot set the baseline.
   return Learner.Median(items.Select(value));
  }
  public static HistoryResult Parse(string file,DateTime now) {
   var result=new HistoryResult{Analyzed=now};
   if(new FileInfo(file).Length>8*1024*1024)throw new InvalidDataException(L.T("Rapor beklenenden büyük."));
   var doc=new XmlDocument{XmlResolver=null};
   using(var reader=XmlReader.Create(file,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024}))doc.Load(reader);
   if(doc.DocumentElement==null||doc.DocumentElement.LocalName!="BatteryReport")throw new InvalidDataException(L.T("Pil raporu biçimi tanınmadı."));
   // Ignore pre-replacement sessions when Windows explicitly records a battery change.
   DateTime cutoff=now.AddDays(-14);
   foreach(XmlElement h in doc.SelectNodes("/*/*[local-name()='History']/*[local-name()='HistoryEntry']"))
    if(h.GetAttribute("BatteryChanged")=="1") {DateTime end=Date(h.GetAttribute("EndDate"));if(end>cutoff&&end<=now)cutoff=end;}
   var list=new List<Session>();
   foreach(XmlElement e in doc.SelectNodes("/*/*[local-name()='RecentUsage']/*[local-name()='UsageEntry']")) {
    DateTime start=Date(e.GetAttribute("Timestamp"));double ticks=Number(e,"Duration"),drop=Number(e,"Discharge"),full=Number(e,"FullChargeCapacity"),charge=Number(e,"ChargeCapacity");
    double minutes=ticks/TimeSpan.TicksPerMinute;
    if(e.GetAttribute("Ac")!="0"||e.GetAttribute("EntryType")!="Active"||start<cutoff||start>now||minutes<=0||minutes>720||drop<=0||full<=0||charge<=0||drop>charge*1.05||charge>full*1.05) {result.Excluded++;continue;}
    DateTime end=start.AddMinutes(minutes);
    double watts=drop/1000/(minutes/60);
    if(end>now.AddSeconds(5)||!Learner.Valid(watts)) {result.Excluded++;continue;}
    list.Add(new Session{Start=start,End=end,Watts=watts,Minutes=minutes});
   }
   // Do not double count duplicate or overlapping report rows.
   var clean=new List<Session>();DateTime previous=DateTime.MinValue;
   foreach(var s in list.OrderBy(s=>s.Start)) {if(s.Start<previous){result.Excluded++;continue;}clean.Add(s);previous=s.End;}
   // Adjacent report fragments are one session, not extra votes in the outlier filter.
   var joined=new List<Session>();
   foreach(var s in clean){var p=joined.LastOrDefault();if(p!=null&&(s.Start-p.End).TotalSeconds<=1&&p.Minutes+s.Minutes<=720){p.Watts=(p.Watts*p.Minutes+s.Watts*s.Minutes)/(p.Minutes+s.Minutes);p.Minutes+=s.Minutes;p.End=s.End;}else joined.Add(s);}
   result.Excluded+=joined.Count(s=>s.Minutes<5);clean=joined.Where(s=>s.Minutes>=5).ToList();
   if(clean.Count>=3) {
    double med=Median(clean,s=>s.Watts),mad=Median(clean,s=>Math.Abs(s.Watts-med));
    // A broad spread must not admit sessions above twice the typical rate.
    // This is the general-use profile; live runtime still follows sustained high load.
    double threshold=Math.Min(med,Math.Max(med*0.6,mad*3));
    var retained=clean.Where(s=>Math.Abs(s.Watts-med)<=threshold).ToList();result.Outliers=clean.Count-retained.Count;result.Excluded+=result.Outliers;clean=retained;
   }
   result.Included=clean.Count;result.Minutes=clean.Sum(s=>s.Minutes);result.Days=clean.Select(s=>s.Start.Date).Distinct().Count();
   if(clean.Count>0)result.Watts=clean.Sum(s=>s.Watts*Math.Min(60,s.Minutes))/clean.Sum(s=>Math.Min(60,s.Minutes));
   if(!result.Useful){result.Watts=-1;result.Message=L.T("Yeterli geçmiş yok · canlı öğrenme sürecek");}
   return result;
  }
  public static HistoryResult Generate() {
   string directory=Path.Combine(Path.GetTempPath(),"Pulse-"+Guid.NewGuid().ToString("N")),file=Path.Combine(directory,"battery.xml");
   Directory.CreateDirectory(directory);
   try {
    string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    string exe=Path.Combine(windows,Environment.Is64BitOperatingSystem&&!Environment.Is64BitProcess?"Sysnative":"System32","powercfg.exe");
    using(var p=Process.Start(new ProcessStartInfo(exe,"/batteryreport /xml /duration 14 /output \""+file+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})) {
     if(!p.WaitForExit(30000)){try{p.Kill();p.WaitForExit(3000);}catch{}throw new TimeoutException(L.T("Analiz zaman aşımına uğradı. Yeniden deneyebilirsiniz."));}
     if(p.ExitCode!=0||!File.Exists(file))throw new IOException(L.T("Windows raporu oluşturamadı. Canlı öğrenme devam ediyor."));
    }
    return Parse(file,DateTime.UtcNow);
   } finally {try{if(File.Exists(file))File.Delete(file);if(Directory.Exists(directory))Directory.Delete(directory);}catch{}}
  }
 }
}
