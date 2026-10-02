using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Pulse {
 // Documented Windows battery IOCTLs. Handles are read-only and requests never wait for a change.
 public static class NativeBattery {
  [StructLayout(LayoutKind.Sequential)] struct Interface {public int Size;public Guid Guid;public int Flags;public IntPtr Reserved;}
  [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string enumerator,IntPtr parent,uint flags);
  [DllImport("setupapi.dll",SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref Interface data);
  [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref Interface data,IntPtr detail,uint size,out uint required,IntPtr device);
  [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool DeviceIoControl(SafeFileHandle file,uint code,byte[] input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
  static Guid guid=new Guid("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
  class Device {public string Path,Id;public uint Tag,Flags,Design,Full;public double Granularity=.025;public DateTime Metadata;}
  static readonly List<Device> devices=new List<Device>();static DateTime enumerated=DateTime.MinValue;
  static byte[] Input(params uint[] values){var b=new byte[values.Length*4];for(int i=0;i<values.Length;i++)Array.Copy(BitConverter.GetBytes(values[i]),0,b,i*4,4);return b;}
  static byte[] Query(SafeFileHandle h,uint code,byte[] input,int size){var b=new byte[size];int returned;if(!DeviceIoControl(h,code,input,input.Length,b,b.Length,out returned,IntPtr.Zero)||returned==0)return null;Array.Resize(ref b,returned);return b;}
  static uint U(byte[] b,int offset){return BitConverter.ToUInt32(b,offset);}
  static void Enumerate() {
   devices.Clear();enumerated=DateTime.UtcNow;IntPtr set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,18);if(set==new IntPtr(-1))return;
   try{for(uint i=0;i<32;i++){var data=new Interface{Size=Marshal.SizeOf(typeof(Interface))};if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref data))break;
    uint size;SetupDiGetDeviceInterfaceDetail(set,ref data,IntPtr.Zero,0,out size,IntPtr.Zero);if(size<6||size>65536)continue;
    IntPtr detail=Marshal.AllocHGlobal((int)size);try{Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);if(SetupDiGetDeviceInterfaceDetail(set,ref data,detail,size,out size,IntPtr.Zero))devices.Add(new Device{Path=Marshal.PtrToStringUni(IntPtr.Add(detail,4))});}finally{Marshal.FreeHGlobal(detail);}
   }}finally{SetupDiDestroyDeviceInfoList(set);}
  }
  public static Reading Decode(uint flags,uint state,uint capacity,uint full,uint design,uint voltage,int rate,string identity) {
   bool relative=(flags&0x40000000)!=0;
   var r=new Reading{Device=identity,Present=true,Detailed=true,Native=true,Online=(state&1)!=0,Discharging=(state&2)!=0,Charging=(state&4)!=0};
   if(full>0&&full!=uint.MaxValue&&capacity!=uint.MaxValue)r.Percent=Math.Min(100,capacity*100.0/full);
   if(!relative){r.Remaining=capacity==uint.MaxValue?-1:capacity/1000.0;r.Full=full==uint.MaxValue||full==0?-1:full/1000.0;r.Design=design==uint.MaxValue||design==0?-1:design/1000.0;
    bool signOk=r.Charging&&rate>=0||r.Discharging&&rate<=0||!r.Charging&&!r.Discharging&&rate==0;
    r.Rate=rate==int.MinValue||!signOk?-1:Math.Abs((double)rate)/1000;
   }
   r.Voltage=voltage==uint.MaxValue||voltage==0?-1:voltage/1000.0;return r;
  }
  public static Reading Combine(List<Reading> list) {
   if(list.Count==0)return null;var r=new Reading{Present=true,Detailed=true,Native=true,Device=string.Join("|",list.Select(x=>x.Device).OrderBy(x=>x)),Online=list.Any(x=>x.Online)};
   r.Charging=list.Any(x=>x.Charging);r.Discharging=!r.Charging&&list.Any(x=>x.Discharging);
   r.Remaining=list.All(x=>x.Remaining>=0)?list.Sum(x=>x.Remaining):-1;r.Full=list.All(x=>x.Full>0)?list.Sum(x=>x.Full):-1;r.Design=list.All(x=>x.Design>0)?list.Sum(x=>x.Design):-1;
   bool mixed=list.Any(x=>x.Charging)&&list.Any(x=>x.Discharging);
   r.Rate=!mixed&&list.All(x=>x.Rate>=0)?list.Sum(x=>x.Rate):-1;
   if(r.Full>0&&r.Remaining>=0)r.Percent=Math.Min(100,r.Remaining/r.Full*100);else if(list.Count==1)r.Percent=list[0].Percent;
   if(list.Count==1)r.Voltage=list[0].Voltage;
   r.GranularityWh=list.Sum(x=>x.GranularityWh);
   // Mixed battery flows cannot safely train a net-power curve without per-pack modelling.
   if(mixed){r.Charging=false;r.Discharging=false;}
   return r;
  }
  public static Reading Read() {
   try {
    if((DateTime.UtcNow-enumerated).TotalMinutes>=5)Enumerate();var rows=new List<Reading>();
    foreach(var d in devices)using(var h=CreateFile(d.Path,0x80000000,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
     if(h.IsInvalid){enumerated=DateTime.MinValue;return null;}
     var tag=Query(h,0x294040,Input(0),4);if(tag==null||tag.Length<4||U(tag,0)==0){enumerated=DateTime.MinValue;return null;}uint value=U(tag,0);
     if(d.Tag!=value||(DateTime.UtcNow-d.Metadata).TotalMinutes>=5) {
      var info=Query(h,0x294044,Input(value,0,0),36);if(info==null||info.Length<36)return null;
      d.Flags=U(info,0);d.Design=U(info,12);d.Full=U(info,16);d.Tag=value;d.Metadata=DateTime.UtcNow;
      var id=Query(h,0x294044,Input(value,7,0),2048);string unique=id==null?"":Encoding.Unicode.GetString(id).TrimEnd('\0');
      d.Id="native:"+(string.IsNullOrWhiteSpace(unique)?d.Path+"#tag:"+value+"#design:"+d.Design:unique);
      var scales=Query(h,0x294044,Input(value,1,0),128);if(scales!=null)for(int j=0;j+8<=scales.Length;j+=8){uint grain=U(scales,j);if(grain>0&&grain<uint.MaxValue)d.Granularity=Math.Max(d.Granularity,grain/1000.0);}
     }
     if((d.Flags&0x80000000)==0||(d.Flags&0x20000000)!=0)continue;
     var status=Query(h,0x29404c,Input(value,0,0,0,0),16);if(status==null||status.Length<16){enumerated=DateTime.MinValue;return null;}
     var reading=Decode(d.Flags,U(status,0),U(status,4),d.Full,d.Design,U(status,8),BitConverter.ToInt32(status,12),d.Id);reading.GranularityWh=d.Granularity;rows.Add(reading);
    }return Combine(rows);
   }catch{enumerated=DateTime.MinValue;return null;}
  }
 }
}
