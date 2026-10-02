using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Pulse {
 public static class V2Tests {
  static int checks;
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
  static bool Near(double a,double b,double tolerance=.001){return Math.Abs(a-b)<tolerance;}
  static Reading R(double watts=10){return new Reading{Device="test",Full=80,Remaining=40,Percent=50,Rate=watts,Discharging=true};}
  static void Feed(Learner l,Reading r,DateTime start,int seconds,int step){for(int s=0;s<=seconds;s+=step)l.Add(r,start.AddSeconds(s));}
  public static int Run(){
   checks=0;string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"v2-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   var watch=Stopwatch.StartNew();
   try {
    DateTime t=DateTime.UtcNow.AddDays(-3);
    foreach(int step in new[]{1,5,10,15}) {
     var l=new Learner(null);var r=R();Feed(l,r,t,1800,step);
     Check(Near(l.NormalWatts,10),"learning works at "+step+" seconds");
     Check(Near(l.Data.Days.Sum(x=>x.NormalWh),5),"energy invariant at "+step+" seconds");
     Check(Near(l.Data.Days.Sum(x=>x.NormalSeconds),1800),"coverage invariant at "+step+" seconds");
     Check(Near(l.Estimate(r),240),"runtime stable at "+step+" seconds");
    }
    foreach(int step in new[]{1,5,10}) {
     var l=new Learner(null);var r=R();for(int s=0;s<=1800;s+=step){r.Rate=s%60<40?10:70;l.Add(r,t.AddSeconds(s));}
     Check(Near(l.Data.Days.Sum(x=>x.NormalWh+x.HighWh),15),"real bursts retain 15 Wh at "+step+" seconds");
     Check(Near(l.NormalWatts,30),"periodic bursts learn true 30 W average");
    }
    var live=new Learner(null);var reading=R();Feed(live,reading,t,120,5);reading.Rate=180;live.Add(reading,t.AddSeconds(125));
    Check(Near(live.Smoothed,10),"one spike does not jump ETA");Check(live.Variable,"spike marks variability");
    reading.Rate=80;Feed(live,reading,t.AddSeconds(130),90,5);Check(live.Smoothed>75,"sustained load changes ETA");
    double energy=live.Data.Days.Sum(x=>x.NormalWh+x.HighWh);live.ResetLive();energy=live.Data.Days.Sum(x=>x.NormalWh+x.HighWh);
    live.Add(reading,t.AddHours(3));Check(live.RecentCount==1&&Near(live.Data.Days.Sum(x=>x.NormalWh+x.HighWh),energy),"sleep gap adds no invented energy");
    var ac=new Learner(null);reading=R(70);reading.Online=true;Feed(ac,reading,t,3600,5);
    Check(ac.Data.Days.Count==0&&ac.NormalWatts<0,"plugged-in discharge never trains typical use");Check(ac.Estimate(reading)<0,"plugged-in support drain has no on-battery runtime");
    for(int s=0;s<=2400;s+=5){reading.Remaining=40-s*10.0/3600;ac.Add(reading,t.AddSeconds(3605+s));}
    Check(ac.Data.DischargeErrors.Count==0,"plugged-in battery support cannot train discharge confidence either");
    var typical=new Learner(null);reading=R();Feed(typical,reading,t,3600,5);reading.Rate=48;Feed(typical,reading,t.AddSeconds(3605),1800,5);
    Check(Near(typical.NormalWatts,10),"one long gaming outage cannot replace established normal");Check(typical.Data.Days.Sum(x=>x.HighWh)>20,"gaming energy retained separately");
    for(int day=1;day<=5;day++)Feed(typical,reading,t.AddDays(day-8),1800,5);
    Check(typical.NormalWatts>35,"high use repeated across five days may become the new normal");
    var missing=new Learner(null);reading=R(-1);for(int s=0;s<=600;s+=5){reading.Remaining=40-s*10.0/3600;missing.Add(reading,t.AddSeconds(s));}
    Check(Near(missing.EffectiveRate,10)&&missing.Estimate(reading)>0,"capacity delta supplies missing watts");
    reading.Remaining+=2;missing.Add(reading,t.AddSeconds(605));Check(missing.Estimate(reading)<0,"capacity recalibration clears fallback");
    var zero=new Learner(null);reading=R(0);Feed(zero,reading,t,600,5);Check(zero.Estimate(reading)<0&&zero.Data.Days.Count==0,"flat capacity and zero power do not fabricate learning");
    var charge=new Learner(null);reading=R(30);reading.Online=true;reading.Charging=true;reading.Discharging=false;reading.Percent=85;reading.Remaining=68;
    Feed(charge,reading,t,600,5);Check(charge.Data.Days.Sum(x=>x.ChargeWh)>4.9&&charge.NormalWatts<0,"charge learning separated from typical discharge");
    Check(charge.Data.Charge[17].Sessions==1&&charge.Coverage(reading)==0,"ten minutes are one independent charge session");
    charge.ResetLive();Feed(charge,reading,t.AddSeconds(605),180,5);Check(charge.Data.Charge[17].Sessions==1,"screen-off reset is not another charging session");
    for(int i=17;i<20;i++)charge.Data.Charge[i]=new ChargeBand{Watts=i==17?30:10,Energy=(i==17?30:10)*300.0/3600,Seconds=300,Sessions=3,Updated=t};
    Check(Near(charge.Estimate(reading),56),"learned upper charge taper changes ETA");
    reading.Rate=20;Feed(charge,reading,t.AddSeconds(790),120,5);Check(charge.Estimate(reading)>56,"increased computer load extends charge ETA");charge.Data.Charge[17].Watts=40;Check(charge.Estimate(reading)<0,"net load that prevents predicted final charging suppresses ETA");
    double charged=charge.Data.Days.Sum(x=>x.ChargeWh);reading.Rate=0;reading.Percent=99;reading.Remaining=79.2;Feed(charge,reading,t.AddSeconds(915),600,5);
    Check(charge.ChargePaused&&charge.Estimate(reading)<0,"99 percent paused charging has no countdown");
    Check(charge.Data.Days.Sum(x=>x.ChargeWh)-charged<.1,"paused minutes never dilute charging curve");
    reading.Charging=false;reading.FullReported=true;charge.Add(reading,t.AddSeconds(1520));Check(charge.Note(reading)==L.T("Şarj tamamlandı"),"explicit full report labelled complete");
    reading.FullReported=false;reading.Percent=80;Check(charge.Note(reading)==L.T("Prize bağlı · şarj edilmiyor"),"unknown charge limit is not invented");
    var traversal=new Learner(null);
    for(int cycle=0;cycle<3;cycle++){
     DateTime start=t.AddDays(-10+cycle);traversal.Add(R(),start.AddSeconds(-5));
     var c=R(24);c.Online=c.Charging=true;c.Discharging=false;
     for(int s=0;s<=2400;s+=5){c.Percent=80+s/120.0;c.Remaining=c.Full*c.Percent/100;traversal.Add(c,start.AddSeconds(s));}
    }
    Check(traversal.Data.Charge.Skip(16).All(b=>b.Traversals==3&&b.Sessions==3),"three real band traversals and independent charge sessions recorded");
    Check(traversal.Data.Charge.Skip(16).All(b=>Near(b.TraversalSeconds,1800)&&Near(b.TraversalWh,12)),"observed full-band times preserve net energy");
    var gate=new ActivityGate{Available=true};gate.Display(1);int revision=gate.Revision;gate.Display(2);Check(gate.Accept(revision),"dimming does not reset learning");gate.Display(0);Check(!gate.Accept(revision),"screen off rejects in-flight measurement");
    string file=Path.Combine(root,"profile.xml");var store=new Learner(file);reading=R();Feed(store,reading,t,1800,5);store.Save(DateTime.UtcNow,true);
    Check(new FileInfo(file).Length<20000,"active profile does not rewrite the minute history");
    var reload=new Learner(file);Check(Near(reload.NormalWatts,10)&&reload.Data.Normal.Count==0,"older minutes compact but daily energy survives restart");
    Check(Directory.GetFiles(file+".days","*.xml",SearchOption.AllDirectories).Length==1,"daily archive saved");
    store.RecordRest(1,120,t);store.Save(DateTime.UtcNow,true);reload=new Learner(file);Check(Near(reload.Data.Days.Sum(x=>x.RestWh),1)&&Near(reload.NormalWatts,10),"standby loss persisted separately");
    string oldArchive=Path.Combine(root,"old-archive.xml");var oldStore=new Learner(oldArchive);Feed(oldStore,R(),DateTime.UtcNow.AddDays(-90),1800,5);oldStore.Save(DateTime.UtcNow,true);Check(Near(new Learner(oldArchive).Data.Days.Sum(x=>x.NormalWh),5),"90-day-old data survives without a 30-day deletion rule");
    string journal=Path.Combine(root,"journal.xml");var journalStore=new Learner(journal);Feed(journalStore,R(),DateTime.UtcNow.AddHours(-2),1800,5);journalStore.Save(DateTime.UtcNow,true);var journalReload=new Learner(journal);Check(journalReload.Data.Normal.Count==30&&Near(journalReload.NormalWatts,10),"recent minute journal reloads alongside independent totals");
    string minuteFile=Directory.GetFiles(journal+".days","*.minutes",SearchOption.AllDirectories).Single();File.AppendAllText(minuteFile,"partial record");Check(new Learner(journal).Data.Normal.Count==30,"interrupted final journal line cannot corrupt valid observations");
    reading.Device="other";Feed(reload,reading,t.AddDays(1),1800,5);reload.Save(DateTime.UtcNow,true);Check(Directory.GetDirectories(file+".days").Length==2,"battery replacement retains separate archives");
    reading.Device="test";reload.Add(reading,t.AddDays(2));Check(Near(reload.NormalWatts,10),"returning battery restores its daily history");
    reload.Reset();Check(new Learner(file).NormalWatts<0,"reset current battery persists");
    string old=Path.Combine(root,"legacy.xml");var legacy=new Profile{Version=1,Device="legacy",Charge=Enumerable.Range(0,10).Select(x=>new ChargeBand()).ToArray()};
    for(int i=0;i<30;i++)legacy.Normal.Add(new MinuteSample{Time=t.AddMinutes(i),Watts=12});using(var s=File.Create(old))new XmlSerializer(typeof(Profile)).Serialize(s,legacy);
    var migrated=new Learner(old);Check(migrated.Data.Version==2&&Near(migrated.NormalWatts,12),"v1 migrates without losing approximate history");Check(File.Exists(old+".v1.bak"),"migration preserves original profile");Check(new Learner(old).Data.Days.Sum(x=>x.NormalSeconds)==1800,"migration does not double count after restart");
    string corrupt=Path.Combine(root,"broken.xml");File.WriteAllText(corrupt,"<broken>");var broken=new Learner(corrupt);Check(broken.SaveFailed&&File.Exists(corrupt+".unreadable.bak"),"bad profile backed up before recovery");
    var absolute=NativeBattery.Decode(0x80000000,2,40000,80000,90000,12000,-10000,"a");Check(Near(absolute.Rate,10)&&Near(absolute.Remaining,40),"native mW and mWh converted");
    var relative=NativeBattery.Decode(0xc0000000,2,40,80,90,12000,-10,"b");Check(relative.Rate<0&&relative.Remaining<0&&Near(relative.Percent,50),"relative units never presented as W or Wh");
    var unknown=NativeBattery.Decode(0x80000000,2,uint.MaxValue,uint.MaxValue,0,uint.MaxValue,int.MinValue,"c");Check(unknown.Rate<0&&unknown.Remaining<0&&unknown.Voltage<0,"native sentinels remain unknown");
    var other=NativeBattery.Decode(0x80000000,5,20000,40000,50000,12000,20000,"b");var mixed=NativeBattery.Combine(new List<Reading>{absolute,other});Check(mixed.Rate<0&&!mixed.Charging&&!mixed.Discharging,"mixed battery flows do not train a false net curve");
    var two=NativeBattery.Combine(new List<Reading>{absolute,absolute});Check(Near(two.Rate,20)&&Near(two.Remaining,80)&&Near(two.Percent,50),"multiple batteries aggregate absolute units");
    var meter=new Meter();reading=R(10);meter.Add(reading,t);meter.Add(reading,t.AddSeconds(5));reading.Rate=50;meter.Add(reading,t.AddSeconds(10));meter.Add(reading,t.AddSeconds(20));Check(Near(meter.Average,30),"display mean weighted by elapsed time");
    var calibrated=new Learner(null);reading=R(20);for(int s=0;s<=2400;s+=5){reading.Remaining=60-s*10.0/3600;calibrated.Add(reading,t.AddSeconds(s));}Check(calibrated.Data.DischargeErrors.Count>=6&&calibrated.Data.DischargeErrors.Average()>.9,"forecast error measured against capacity changes");
    var bounded=new Learner(null);reading=R();for(int s=0;s<=4*86400;s+=15)bounded.Add(reading,t.AddDays(-4).AddSeconds(s));Check(bounded.Data.Normal.Count<=2881&&bounded.RecentCount<=120,"recent data bounded without deleting daily totals");Check(bounded.Data.Days.Count>=4,"old days still retained");
    watch.Stop();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"v2-test-result.txt"),"PASS: "+checks+" behavioral checks; elapsed "+watch.ElapsedMilliseconds+" ms.\r\nSynthetic algorithm tests, not cross-device field certification.");return 0;
   }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"v2-test-result.txt"),"FAIL after "+checks+" checks: "+e);return 1;}
   finally{Directory.Delete(root,true);}
  }
 }
}
