using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Pulse {
 // Conservatively exclude screen-off time as well as explicit suspend.
 public class ActivityGate {
  public bool DisplayKnown, DisplayOn, Suspended, Available;
  public int Revision,SourceRevision;
  public bool CanLearn {get{return Available&&DisplayKnown&&DisplayOn&&!Suspended;}}
  public void Display(int value){if(value<0||value>2)return;bool next=value!=0;if(!DisplayKnown||DisplayOn!=next)Revision++;DisplayKnown=true;DisplayOn=next;}
  public void Suspend(){Suspended=true;Revision++;}
  public void Resume(){Suspended=false;Revision++;}
  public void PowerSource(){SourceRevision++;Revision++;}
  public bool Accept(int requestRevision){return CanLearn&&requestRevision==Revision;}
 }
 public class RestRecord {
  public bool Pending; public double LastLoss=-1, LastMinutes; public int Observations;
  double startCapacity;string device;DateTime start;bool valid;
  public void Begin(Reading r,DateTime lastReading,DateTime now) {
   Pending=true;start=now;Observations=0;
   valid=r!=null&&r.Remaining>=0&&!r.Online&&!r.Charging&&r.Device!=""&&(now-lastReading).TotalSeconds<=15;
   startCapacity=r==null?-1:r.Remaining;device=r==null?"":r.Device;
  }
  public void Observe(Reading r) {
   if(!Pending)return;Observations++;
   if(r.Device!=device||r.Remaining<0||r.Online||r.Charging||r.Remaining>startCapacity)valid=false;
  }
  public void Finish(Reading r,DateTime now) {
   if(!Pending)return;Observe(r);Pending=false;LastMinutes=(now-start).TotalMinutes;
   LastLoss=valid&&LastMinutes>=1?Math.Max(0,startCapacity-r.Remaining):-1;
  }
  public void Clear(){Pending=false;LastLoss=-1;LastMinutes=0;}
  public void Invalidate(){valid=false;}
 }
 public sealed class PowerWatch:IDisposable {
  static readonly Guid DisplayGuid=new Guid("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr RegisterPowerSettingNotification(IntPtr window,ref Guid setting,uint flags);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr RegisterSuspendResumeNotification(IntPtr window,uint flags);
  [DllImport("user32.dll")] static extern bool UnregisterPowerSettingNotification(IntPtr handle);
  [DllImport("user32.dll")] static extern bool UnregisterSuspendResumeNotification(IntPtr handle);
  readonly HwndSource source;readonly ActivityGate gate;readonly Action changed;
  IntPtr display,suspend;
  public PowerWatch(IntPtr window,ActivityGate state,Action callback) {
   source=HwndSource.FromHwnd(window);gate=state;changed=callback;source.AddHook(Hook);
   Guid guid=DisplayGuid;display=RegisterPowerSettingNotification(window,ref guid,0);suspend=RegisterSuspendResumeNotification(window,0);
   gate.Available=display!=IntPtr.Zero&&suspend!=IntPtr.Zero;changed();
  }
  IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr data,ref bool handled) {
   if(message!=0x218)return IntPtr.Zero;
   int code=w.ToInt32();bool update=false;
   if(code==4){gate.Suspend();update=true;}
   else if(code==7||code==18){gate.Resume();update=true;}
   else if(code==10){gate.PowerSource();update=true;}
   else if(code==0x8013&&data!=IntPtr.Zero&&Marshal.ReadInt32(data,16)>=4&&(Guid)Marshal.PtrToStructure(data,typeof(Guid))==DisplayGuid){gate.Display(Marshal.ReadInt32(data,20));update=true;}
   if(update)changed();return IntPtr.Zero;
  }
  public void Dispose(){source.RemoveHook(Hook);if(display!=IntPtr.Zero)UnregisterPowerSettingNotification(display);if(suspend!=IntPtr.Zero)UnregisterSuspendResumeNotification(suspend);}
 }
}
