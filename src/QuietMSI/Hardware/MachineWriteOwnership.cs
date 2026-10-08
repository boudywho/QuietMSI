using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace QuietMSI.Hardware;

internal sealed class MachineWriteOwnership : IDisposable
{
    private readonly Mutex _mutex;
    private bool _owned;
    internal MachineWriteOwnership()
    {
        IntPtr descriptor=IntPtr.Zero;
        try
        {
            if(!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;0x00100001;;;BA)(A;;0x00100001;;;SY)",1,out descriptor,out _))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var attributes=new SecurityAttributes { Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor };
            var handle=CreateMutexEx(ref attributes,"Global\\QuietMSI.HardwareChange.v2",0,0x00100001);
            if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            _mutex=new Mutex();_mutex.SafeWaitHandle=new SafeWaitHandle(handle,true);
            try
            {
                _owned=_mutex.WaitOne(0);
                if(!_owned)throw new InvalidOperationException("Another machine-wide QuietMSI transaction owns the hardware.");
            }
            catch(AbandonedMutexException)
            {
                // We acquired abandoned ownership, but prior hardware state may be indeterminate. Fail closed.
                _mutex.ReleaseMutex();_mutex.Dispose();
                throw new InvalidOperationException("Previous hardware transaction owner exited unexpectedly; change denied. Inspect current state.");
            }
            catch { _mutex.Dispose();throw; }
        }
        finally{if(descriptor!=IntPtr.Zero)LocalFree(descriptor);}
    }
    public void Dispose(){if(_owned){_owned=false;_mutex.ReleaseMutex();}_mutex.Dispose();}
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes {public int Length;public IntPtr Descriptor;public int Inherit;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl,uint revision,out IntPtr descriptor,out uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="CreateMutexExW")] private static extern IntPtr CreateMutexEx(ref SecurityAttributes attributes,string name,uint flags,uint access);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
}
