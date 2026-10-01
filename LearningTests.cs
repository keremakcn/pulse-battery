using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Pulse {
 public static class LearningTests {
  static int checks;
  static void Check(bool value,string name) {if(!value)throw new Exception(name);checks++;}
  public static int Run() {
   string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"learning-test-work");Directory.CreateDirectory(root);
   string file=Path.Combine(root,"profile.xml");
   try {
    DateTime t=DateTime.UtcNow.AddDays(-1);
    var r=new Reading{Device="test-device",Percent=50,Full=80,Remaining=40,Discharging=true,Rate=10};
    var l=new Learner(null);l.Add(r,t);Check(l.Estimate(r)<0,"startup must learn");
    for(int i=1;i<30;i++)l.Add(r,t.AddSeconds(i*5));
    Check(Math.Abs(l.Estimate(r)-240)<0.01,"stable runtime");
    r.Rate=180;l.Add(r,t.AddSeconds(150));Check(Math.Abs(l.Smoothed-10)<0.01,"isolated spike rejected");
    Check(l.Variable,"spike widens uncertainty");
    r.Rate=10;for(int i=31;i<65;i++)l.Add(r,t.AddSeconds(i*5));
    Check(!l.Variable,"uncertainty recovers");
    r.Rate=80;for(int i=65;i<83;i++)l.Add(r,t.AddSeconds(i*5));
    Check(l.Smoothed>75&&l.Estimate(r)<33,"sustained load must reduce live runtime");
    l.Add(r,t.AddHours(2));Check(l.RecentCount==1&&l.Estimate(r)<0,"sleep gap resets live history");
    r.Rate=double.NaN;l.Add(r,t.AddHours(2).AddSeconds(5));Check(l.RecentCount==0,"invalid reading resets");
    r.Rate=0;l.Add(r,t.AddHours(2).AddSeconds(10));Check(l.Estimate(r)<0,"zero cannot produce infinity");
    var general=new Learner(null);r.Rate=10;
    for(int i=0;i<=312;i++)general.Add(r,t.AddSeconds(i*5));
    Check(general.Data.Normal.Count>=20&&Math.Abs(general.NormalWatts-10)<0.01,"minute learning");
    for(int i=0;i<5;i++)general.Data.Normal.Add(new MinuteSample{Watts=150,Time=t});
    Check(Math.Abs(general.NormalWatts-10)<0.01,"rare gaming minutes excluded from normal");
    var charge=new Learner(null);r.Charging=true;r.Discharging=false;r.Percent=80;r.Remaining=64;r.Rate=40;
    for(int i=0;i<10;i++)charge.Add(r,t.AddSeconds(i*5));
    Check(charge.Coverage(r)==0,"new charge profile unknown");
    charge.Data.Charge[8]=new ChargeBand{Watts=40,Count=3,Updated=t};charge.Data.Charge[9]=new ChargeBand{Watts=10,Count=3,Updated=t};
    Check(charge.Coverage(r)==1&&Math.Abs(charge.Estimate(r)-60)<0.01,"slow final band included");
    r.Rate=20;for(int i=10;i<50;i++)charge.Add(r,t.AddSeconds(i*5));
    Check(charge.Estimate(r)>80,"changed charger/load adapts curve");
    r.Charging=false;r.Discharging=true;charge.Add(r,t.AddSeconds(250));Check(charge.RecentCount==1,"mode change resets");
    general.Data.Device="test-device";
    var store=new Learner(file);store.Data=general.Data;store.Save(DateTime.UtcNow,true);
    Check(!store.SaveFailed&&File.Exists(file),"profile writes");
    var reloaded=new Learner(file);Check(reloaded.Data.Normal.Count==general.Data.Normal.Count&&reloaded.NormalWatts==10,"restart restores personal profile");
    reloaded.Save(DateTime.UtcNow,true);Check(!reloaded.SaveFailed,"atomic replace");
    r.Device="different-device";reloaded.Add(r,t);Check(reloaded.Data.Normal.Count==0,"different hardware does not inherit history");
    File.WriteAllText(file,"broken XML");Check(new Learner(file).Data.Normal.Count==0,"corrupt file fallback");
    store.Reset();Check(new Learner(file).Data.Normal.Count==0,"reset persists");
    var learnedCharge=new Learner(null);r.Charging=true;r.Discharging=false;r.Percent=85;r.Rate=30;
    for(int i=0;i<=39;i++)learnedCharge.Add(r,t.AddSeconds(i*5));
    Check(learnedCharge.Data.Charge[8].Count>=3&&Math.Abs(learnedCharge.Data.Charge[8].Watts-30)<0.01,"charge band learns minute samples");
    store.Data=learnedCharge.Data;store.Save(DateTime.UtcNow,true);
    Check(new Learner(file).Data.Charge[8].Count>=3,"charge curve survives restart");
    var bounded=new Learner(null);r.Charging=false;r.Discharging=true;r.Rate=10;r.Device="test-device";
    var watch=Stopwatch.StartNew();
    for(int i=0;i<11000;i++)bounded.Add(r,t.AddSeconds(i*5));
    watch.Stop();Check(bounded.Data.Normal.Count<=720&&bounded.RecentCount<=24,"bounded memory history");
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"learning-test-result.txt"),"PASS: "+checks+" behavioral checks.\r\n11,000 learning updates: "+watch.ElapsedMilliseconds+" ms.\r\nTests cover startup, short spike, persistent load, variable usage, sleep, invalid/zero readings, typical usage outliers, charge taper, changed charge power, mode switch, save/reload, corruption, hardware identity, reset, bounded history.\r\nBenchmark is algorithm-only, not whole-app CPU or memory.");
    return 0;
   }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"learning-test-result.txt"),"FAIL: "+e);return 1;}
   finally {if(File.Exists(file))File.Delete(file);if(Directory.Exists(root)&&!Directory.EnumerateFileSystemEntries(root).Any())Directory.Delete(root);}
  }
 }
}
