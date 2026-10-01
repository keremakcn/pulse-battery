using System;
using System.IO;
using System.Globalization;
using System.Xml;
namespace Pulse {
 public static class HistoryTests {
  static int count;
  static void Check(bool condition,string text){if(!condition)throw new Exception(text);count++;}
  static string Row(DateTime start,double min,double watts,string type,string ac) {
   return string.Format(CultureInfo.InvariantCulture,"<UsageEntry Timestamp='{0}' Duration='{1}' Discharge='{2}' FullChargeCapacity='80000' ChargeCapacity='80000' EntryType='{3}' Ac='{4}'/>",start.ToString("o"),min*TimeSpan.TicksPerMinute,watts*1000*min/60,type,ac);
  }
  public static int Run() {
   string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"history-test.xml"),profile=file+".profile";
   try {
    DateTime now=DateTime.UtcNow;string rows="";
    for(int i=1;i<=6;i++)rows+=Row(now.AddDays(-i),30,10,"Active","0");
    rows+=Row(now.AddDays(-7),30,100,"Active","0");
    rows+=Row(now.AddDays(-8),30,1,"ConnectedStandby","0");
    rows+=Row(now.AddDays(-9),30,10,"Active","1");
    rows+=Row(now.AddDays(-10),1,100,"Active","0");
    rows+=Row(now.AddDays(-1),30,10,"Active","0");
    string xml="<BatteryReport xmlns='http://schemas.microsoft.com/battery/2012'><RecentUsage>"+rows+"</RecentUsage></BatteryReport>";
    File.WriteAllText(file,xml);var h=BatteryHistory.Parse(file,now);
    Check(h.Useful&&h.Included==6&&h.Excluded==5,"outlier/standby/AC/short/duplicate excluded");
    Check(Math.Abs(h.Watts-10)<0.001&&h.Minutes==180,"ticks and mWh units");
    var l=new Learner(profile);l.Import(h,"test");
    Check(l.HasHistory&&l.NormalWatts==10,"history seeds general usage without fabricated samples");
    var r=new Reading{Device="test",Discharging=true,Remaining=40,Full=80,Percent=50,Rate=80};l.Add(r,now);
    Check(l.Estimate(r)==240&&l.Note(r).Contains("Windows"),"initial runtime uses labelled history");
    for(int i=1;i<20;i++)l.Add(r,now.AddSeconds(i*5));
    Check(l.Estimate(r)<35,"live sustained load overrides historical default");
    l.Save(now,true);Check(new Learner(profile).HasHistory,"import survives restart");
    l.Import(h,"test");Check(l.Data.Normal.Count==1,"repeat import does not duplicate minute samples");
    r.Charging=true;r.Discharging=false;l.Add(r,now.AddSeconds(110));Check(l.Estimate(r)<0,"discharge history does not invent charge curve");
    File.WriteAllText(file,"<BatteryReport><RecentUsage>"+Row(now.AddDays(-1),30,10,"Active","0")+"</RecentUsage></BatteryReport>");
    Check(!BatteryHistory.Parse(file,now).Useful,"insufficient history fallback");
    File.WriteAllText(file,"<BatteryReport><RecentUsage>"+Row(now.AddDays(-40),60,10,"Active","0")+"</RecentUsage></BatteryReport>");
    Check(!BatteryHistory.Parse(file,now).Useful,"stale history ignored");
    File.WriteAllText(file,xml.Replace("</BatteryReport>","<History><HistoryEntry BatteryChanged='1' EndDate='"+now.AddDays(-2).ToString("o")+"'/></History></BatteryReport>"));
    Check(!BatteryHistory.Parse(file,now).Useful,"battery replacement cuts off older data");
    File.WriteAllText(file,"<!DOCTYPE BatteryReport [<!ENTITY x SYSTEM 'file:///missing'>]><BatteryReport>&x;</BatteryReport>");
    bool rejected=false;try{BatteryHistory.Parse(file,now);}catch(XmlException){rejected=true;}Check(rejected,"external XML entities prohibited");
    foreach(double gamingWatts in new double[]{32,48}) {
     string normal="";for(int i=1;i<=4;i++)normal+=Row(now.AddDays(-i),20,10,"Active","0");
     File.WriteAllText(file,"<BatteryReport><RecentUsage>"+normal+Row(now.AddDays(-5),30,gamingWatts,"Active","0")+"</RecentUsage></BatteryReport>");
     var outage=BatteryHistory.Parse(file,now);
     Check(outage.Useful&&outage.Outliers==1&&Math.Abs(outage.Watts-10)<0.001,"20-30 percent loss in 30 minutes excluded for an 80 Wh battery");
    }
    string shortNormal="";for(int i=1;i<=3;i++)shortNormal+=Row(now.AddDays(-i),10,10,"Active","0");
    File.WriteAllText(file,"<BatteryReport><RecentUsage>"+shortNormal+Row(now.AddDays(-4),60,48,"Active","0")+"</RecentUsage></BatteryReport>");
    var dominant=BatteryHistory.Parse(file,now);
    Check(dominant.Outliers==1&&dominant.Included==3&&!dominant.Useful,"long extreme cannot outweigh short normal sessions; insufficient retained history stays unknown");
    string regularGaming="";for(int i=1;i<=4;i++)regularGaming+=Row(now.AddDays(-i),30,40,"Active","0");
    File.WriteAllText(file,"<BatteryReport><RecentUsage>"+regularGaming+"</RecentUsage></BatteryReport>");
    var regular=BatteryHistory.Parse(file,now);
    Check(regular.Useful&&regular.Outliers==0&&regular.Watts==40,"consistently high normal usage is not erased by an absolute power threshold");
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"history-test-result.txt"),"PASS: "+count+" history integration checks.");return 0;
   }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"history-test-result.txt"),"FAIL: "+e);return 1;}
   finally {if(File.Exists(file))File.Delete(file);if(File.Exists(profile))File.Delete(profile);}
  }
 }
}
