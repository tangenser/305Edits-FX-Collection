using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Diagnostics;
class MaskClipboardReader {
 [DllImport("user32.dll")]static extern bool OpenClipboard(IntPtr owner);
 [DllImport("user32.dll")]static extern bool CloseClipboard();
 [DllImport("user32.dll")]static extern uint EnumClipboardFormats(uint format);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClipboardFormatName(uint f,StringBuilder s,int max);
 [DllImport("user32.dll")]static extern IntPtr GetClipboardData(uint format);
 [DllImport("kernel32.dll")]static extern UIntPtr GlobalSize(IntPtr h);
 [DllImport("kernel32.dll")]static extern IntPtr GlobalLock(IntPtr h);
 [DllImport("kernel32.dll")]static extern bool GlobalUnlock(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetClipboardOwner();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
 [STAThread]static void Main(){Console.OutputEncoding=new UTF8Encoding(false);var json=new JavaScriptSerializer{MaxJsonLength=180*1024*1024};try{
  uint pid;GetWindowThreadProcessId(GetClipboardOwner(),out pid);string owner=Process.GetProcessById((int)pid).ProcessName;
  if(owner.IndexOf("Premiere",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Скопируйте маску в Premiere через Ctrl+C");
  bool opened=false;for(int i=0;i<20&&!opened;i++){opened=OpenClipboard(IntPtr.Zero);if(!opened)System.Threading.Thread.Sleep(50);}if(!opened)throw new Exception("Буфер обмена занят. Повторите попытку");
  string xml=null,selectedFormat=null;
  try{uint format=0;while((format=EnumClipboardFormats(format))!=0){var sb=new StringBuilder(512);GetClipboardFormatName(format,sb,512);string name=sb.ToString();if(!name.StartsWith("PPro",StringComparison.OrdinalIgnoreCase)||name.IndexOf("VideoComponent",StringComparison.OrdinalIgnoreCase)<0)continue;var handle=GetClipboardData(format);ulong count=GlobalSize(handle).ToUInt64();if(count==0||count>128*1024*1024)continue;var memory=GlobalLock(handle);if(memory==IntPtr.Zero)continue;try{var bytes=new byte[(int)count];Marshal.Copy(memory,bytes,0,bytes.Length);string value=Encoding.UTF8.GetString(bytes).TrimEnd('\0');if(value.StartsWith("<?xml")&&value.Contains("<PremiereData")){xml=Convert.ToBase64String(bytes);selectedFormat=name;}}finally{GlobalUnlock(handle);}}}finally{CloseClipboard();}
  if(xml==null)throw new Exception("В буфере нет маски Premiere. Скопируйте всю маску в Effect Controls, а не клип на таймлайне");Console.WriteLine(json.Serialize(new{ok=true,format=selectedFormat,xmlBase64=xml}));
 }catch(Exception e){Console.WriteLine(json.Serialize(new{ok=false,error=e.Message}));Environment.ExitCode=1;}}
}
