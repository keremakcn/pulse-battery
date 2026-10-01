using System;
using System.IO;
using System.Linq;
namespace Pulse {
 public static class StandbyTests {
  public static int CheckNotifications() {
   var gate=new ActivityGate();
   var options=new System.Windows.Interop.HwndSourceParameters("Pulse notification check"){Width=1,Height=1,WindowStyle=0};
   using(var window=new System.Windows.Interop.HwndSource(options))using(var watcher=new PowerWatch(window.Handle,gate,()=>{})) {
    var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
    timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"powerwatch-test-result.txt"),"Registered="+gate.Available+"; DisplayKnown="+gate.DisplayKnown+"; DisplayOn="+gate.DisplayOn);
    return gate.Available&&gate.DisplayKnown?0:1;
   }
  }
  static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
  public static int Run() {
   try {
    var gate=new ActivityGate();Check(!gate.CanLearn,"unknown state must not learn");
    gate.Available=true;gate.Display(1);Check(gate.CanLearn,"screen on allows learning");
    int before=gate.Revision;gate.Display(0);Check(!gate.Accept(before),"in-flight active result crossing screen-off discarded");
    gate.Display(1);Check(!gate.Accept(before),"in-flight result crossing off/on discarded");
    gate.Suspend();Check(!gate.CanLearn,"suspend excludes learning");gate.Display(1);Check(!gate.CanLearn,"screen event cannot override suspend");
    gate.Resume();Check(gate.CanLearn,"resume with screen on permits learning");gate.Display(0);gate.Resume();Check(!gate.CanLearn,"automatic wake with screen off remains excluded");
    gate.Display(2);Check(gate.CanLearn,"dim is still active");gate.Available=false;Check(!gate.CanLearn,"failed notification registration must not learn");gate.Available=true;
    var l=new Learner(null);DateTime t=DateTime.UtcNow;var r=new Reading{Device="test",Full=80,Remaining=60,Percent=75,Rate=20,Discharging=true};
    for(int i=0;i<30;i++)l.Add(r,t.AddSeconds(i*5));
    int normal=l.Data.Normal.Count;var rest=new RestRecord();rest.Begin(r,t,t);gate.Display(0);l.ResetLive();
    r.Rate=0.5;r.Remaining=58;
    for(int i=0;i<240;i++){int rev=gate.Revision;if(gate.Accept(rev))l.Add(r,t.AddMinutes(i));else rest.Observe(r);}
    Check(l.Data.Normal.Count==normal&&l.RecentCount==0,"four hours standby measurements never enter discharge model");
    r.Charging=true;r.Discharging=false;r.Rate=5;for(int i=0;i<60;i++)if(gate.Accept(gate.Revision))l.Add(r,t.AddMinutes(i));
    Check(l.Data.Charge.All(b=>b.Count==0),"standby charge readings never train charging bands");
    r.Charging=false;r.Discharging=true;rest.Finish(r,t.AddHours(4));Check(rest.LastLoss==2&&rest.LastMinutes==240,"standby loss kept separate");
    var noPoll=new RestRecord();r.Remaining=50;noPoll.Begin(r,t,t);r.Remaining=49;noPoll.Finish(r,t.AddHours(2));Check(noPoll.LastLoss==1,"before/after works without standby polling");
    r.Remaining=50;rest.Begin(r,t,t);r.Online=true;rest.Observe(r);r.Online=false;r.Remaining=49;rest.Finish(r,t.AddHours(1));Check(rest.LastLoss<0,"observed AC invalidates drain estimate");
    rest.Begin(r,t.AddSeconds(-30),t);rest.Finish(r,t.AddHours(1));Check(rest.LastLoss<0,"stale baseline is not presented as accurate");
    rest.Begin(r,t,t);r.Device="other";rest.Finish(r,t.AddHours(1));Check(rest.LastLoss<0,"battery change invalidates rest loss");
    gate.Display(1);l.ResetLive();r.Rate=20;l.Add(r,t.AddHours(5));Check(l.RecentCount==1,"wake starts fresh live window");
    l.Data.RecordRest=false;r.Device="third";l.Add(r,t.AddHours(5).AddSeconds(5));Check(!l.Data.RecordRest,"preference survives device initialization");
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"standby-test-result.txt"),"PASS: "+checks+" standby isolation checks.");return 0;
   }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"standby-test-result.txt"),"FAIL: "+e);return 1;}
  }
 }
}
