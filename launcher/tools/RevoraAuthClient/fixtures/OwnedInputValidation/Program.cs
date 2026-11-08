using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

if(args.Length!=1||!int.TryParse(args[0],out int pid))return 2;
using var process=Process.GetProcessById(pid);
const string root=@"C:\Users\Administrator\Desktop\Revora\research\isolated\direct-native-transport-20260905\client\Env_DX9";
string path=Path.Combine(root,"Revora-Owned-Input-Validation.exe");
if(!string.Equals(process.MainModule?.FileName,path,StringComparison.OrdinalIgnoreCase)||
   Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!="0F71CC853767099E025F804F423FE05028EB03465FBF4414B33332C0081667C2")return 3;
var found=new Dictionary<int,List<nint>>();
Native.EnumWindows((window,_)=>{
 Native.GetWindowThreadProcessId(window,out uint owner);
 if(owner==pid)Native.EnumChildWindows(window,(child,__)=>{
  int id=Native.GetDlgCtrlID(child);
  if(id is 0x7e01 or 0x7e02 or 5680){if(!found.TryGetValue(id,out var entries))found[id]=entries=[];if(!entries.Contains(child))entries.Add(child);}
  return true;
 },0);
 return true;
},0);
if(new[]{0x7e01,0x7e02,5680}.Any(id=>!found.TryGetValue(id,out var entries)||entries.Count!=1))return 4;
nint user=found[0x7e01][0],password=found[0x7e02][0],login=found[5680][0];
var className=new StringBuilder(32);Native.GetClassNameW(password,className,32);
if(className.ToString()!="Edit"||(Native.GetWindowLongW(password,-16)&0x20)==0)return 5;
Console.WriteLine("Owned controls verified; private synthetic input channel ready.");
char[] secret=new char[257];int count=0;
try{
 while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(count>0)secret[--count]='\0';continue;}if(count>=256)return 6;secret[count++]=key.KeyChar;}
 if(count!=12){Console.WriteLine($"Synthetic input length rejected: {count}");return 7;}
 unsafe {fixed(char* u="TestPlayer\0"){if(Native.SendMessageTimeoutW(user,0x000c,0,(nint)u,2,3000,out _)==0)return 8;}}
 unsafe {fixed(char* p=secret){if(Native.SendMessageTimeoutW(password,0x000c,0,(nint)p,2,3000,out _)==0){Console.WriteLine("Owned text assignment timeout");return 8;}}}
 Native.SendMessageTimeoutW(password,0x000e,0,0,2,3000,out nint lengthResult);
 int observed=(int)lengthResult;
 if(observed!=count){Console.WriteLine($"Owned control length mismatch: expected={count}, observed={observed}");return 9;}
}finally{Array.Clear(secret);}
// Click only; never type into legacy controls or retrieve password text.
if(!Native.PostMessageW(login,0x201,1,0x00100020)||!Native.PostMessageW(login,0x202,0,0x00100020))return 10;
Console.WriteLine("One owned-control TestPlayer submission dispatched; input buffer cleared.");
var observedStates=new HashSet<string>();
var clock=Stopwatch.StartNew();
while(clock.Elapsed<TimeSpan.FromSeconds(15)){
 int bytes=0;Native.GetExtendedTcpTable(0,ref bytes,false,2,5,0);
 if(bytes>0&&bytes<=1024*1024){
  nint table=Marshal.AllocHGlobal(bytes);
  try{
   if(Native.GetExtendedTcpTable(table,ref bytes,false,2,5,0)==0){
    int rows=Marshal.ReadInt32(table);
    if(rows>=0&&4L+rows*24L<=bytes)for(int i=0;i<rows;i++){
     nint row=table+4+i*24;if(Marshal.ReadInt32(row,20)!=pid)continue;
     int encodedPort=Marshal.ReadInt32(row,16);int port=((encodedPort&255)<<8)|((encodedPort>>8)&255);
     if(port is not (9958 or 9959))continue;
     string categorical=$"remote_port={port},tcp_state={Marshal.ReadInt32(row)}";
     if(observedStates.Add(categorical))Console.WriteLine($"{DateTimeOffset.UtcNow:O} {categorical}");
    }
   }
  }finally{Marshal.FreeHGlobal(table);}
 }
 Thread.Sleep(10);
}
return 0;

static class Native{
 [DllImport("iphlpapi.dll")]public static extern uint GetExtendedTcpTable(nint table,ref int size,bool order,uint family,int tableClass,uint reserved);
 public delegate bool Callback(nint window,nint parameter);
 [DllImport("user32.dll")]public static extern bool EnumWindows(Callback callback,nint parameter);
 [DllImport("user32.dll")]public static extern bool EnumChildWindows(nint window,Callback callback,nint parameter);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(nint window,out uint pid);
 [DllImport("user32.dll")]public static extern int GetDlgCtrlID(nint window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassNameW(nint window,StringBuilder text,int capacity);
 [DllImport("user32.dll")]public static extern int GetWindowLongW(nint window,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern bool SetWindowTextW(nint window,string text);
 [DllImport("user32.dll")]public static extern int GetWindowTextLengthW(nint window);
 [DllImport("user32.dll")]public static extern nint SendMessageTimeoutW(nint window,uint message,nint w,nint l,uint flags,uint timeout,out nint result);
 [DllImport("user32.dll")]public static extern bool PostMessageW(nint window,uint message,nint w,nint l);
}
