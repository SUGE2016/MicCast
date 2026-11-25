using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Render;

sealed class Route : IDisposable {
    AudioGraph graph;
    AudioDeviceInputNode input;
    AudioDeviceOutputNode output;
    public DeviceInformation Mic,Cable;
    public string CableId;
    public event Action Fault;
    volatile bool failed;
    public long Samples { get { return graph == null ? 0 : (long)graph.CompletedQuantumCount * graph.SamplesPerQuantum; } }
    public async Task Open() {
        var outputs=await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
        var cables=outputs.Where(d=>d.IsEnabled && DeviceSelection.IsCable(d.Name)).ToArray();
        if(cables.Length>1) throw new Exception("找到多个普通 VB-CABLE 播放设备，请停用重复设备后重试。");
        Cable=cables.FirstOrDefault();
        if(Cable==null) throw new Exception("找不到 VB-CABLE 双声道播放设备，请检查虚拟声卡。");
        var match=Regex.Match(Cable.Id,@"\{0\.0\.0\.00000000\}\.\{[0-9A-Fa-f-]{36}\}");
        if(!match.Success) throw new Exception("无法识别虚拟声卡的 Windows 设备编号。");
        CableId=match.Value;
        var inputs=await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
        var microphones=inputs.Where(d=>d.IsEnabled && DeviceSelection.IsMicrophone(d.Name)).ToArray();
        if(microphones.Length>1) throw new Exception("找到多个麦克风阵列，请保留要使用的内置麦克风后重试。");
        Mic=microphones.FirstOrDefault();
        if(Mic==null) throw new Exception("找不到笔记本内置麦克风阵列，请检查录音设备。");
        var settings=new AudioGraphSettings(AudioRenderCategory.Media) { PrimaryRenderDevice=Cable, QuantumSizeSelectionMode=QuantumSizeSelectionMode.LowestLatency };
        var g=await AudioGraph.CreateAsync(settings);
        if(g.Status!=AudioGraphCreationStatus.Success) throw new Exception("打开虚拟声卡失败（"+g.Status+"）。");
        graph=g.Graph;
        graph.UnrecoverableErrorOccurred += (sender,args)=> { failed=true; var f=Fault; if(f!=null) f(); };
        var o=await graph.CreateDeviceOutputNodeAsync();
        if(o.Status!=AudioDeviceNodeCreationStatus.Success) throw new Exception("创建声音输出失败（"+o.Status+"）。");
        output=o.DeviceOutputNode;
        var i=await graph.CreateDeviceInputNodeAsync(MediaCategory.Speech,graph.EncodingProperties,Mic);
        if(i.Status!=AudioDeviceNodeCreationStatus.Success) throw new Exception("打开内置麦克风失败（"+i.Status+"）。请检查 Windows 麦克风权限。");
        input=i.DeviceInputNode; input.AddOutgoingConnection(output);
    }
    public void Start() {
        if(failed) throw new Exception("声音设备已断开，请重新连接后再开始。");
        Defaults.Switch(CableId); graph.Start();
        if(failed) throw new Exception("声音设备已断开，请重新连接后再开始。");
    }
    public void Dispose() {
        var old=graph; graph=null;
        try { if(old!=null) { try { old.Stop(); } finally { old.Dispose(); } } }
        finally { input=null; output=null; Defaults.Restore(); }
    }
}
static class Program {
    [STAThread] static int Main(string[] args) {
        using(var mutex=new Mutex(false,@"Local\MicCast-ESHOW")) {
            bool owned;
            try { owned=mutex.WaitOne(0); } catch(AbandonedMutexException) { owned=true; }
            if(!owned) { if(args.Length==0) MessageBox.Show("投屏麦克风已打开。","投屏麦克风"); return 2; }
            try {
                if(args.Length==1 && args[0]=="--self-test") return Test().GetAwaiter().GetResult();
                if(args.Length==1 && args[0]=="--restore") { Defaults.Restore(); Console.WriteLine("Recovery=PASS"); return 0; }
                if(args.Length!=0) { Console.Error.WriteLine("Usage: MicCast [--self-test|--restore]"); return 2; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MicForm()); return 0;
            } catch(Exception e) { if(args.Length>0) Console.Error.WriteLine(e); else MessageBox.Show(e.Message,"投屏麦克风"); return 1; }
            finally { mutex.ReleaseMutex(); }
        }
    }
    static async Task<int> Test() {
        Defaults.Restore();
        var roles=new[]{Defaults.Get(0),Defaults.Get(1),Defaults.Get(2)};
        try {
            for(int cycle=1;cycle<=2;cycle++) {
                using(var r=new Route()) {
                    int faults=0;
                    r.Fault+=()=>Interlocked.Increment(ref faults);
                    await r.Open(); Console.WriteLine("Input="+r.Mic.Name+" Output="+r.Cable.Name);
                    r.Start(); await Task.Delay(900);
                    bool console=Defaults.Equal(Defaults.Get(0),r.CableId);
                    bool media=Defaults.Equal(Defaults.Get(1),r.CableId);
                    bool communications=Defaults.Equal(Defaults.Get(2),roles[2]);
                    Console.WriteLine("Cycle="+cycle+" Frames="+r.Samples+" ConsoleToCable="+console+" MultimediaToCable="+media+" CommunicationsPreserved="+communications+" Faults="+faults);
                    if(r.Samples<=0 || !console || !media || !communications || faults!=0) throw new Exception("Audio route failed verification.");
                }
                bool restored=Enumerable.Range(0,3).All(i=>Defaults.Equal(Defaults.Get(i),roles[i]));
                Console.WriteLine("Cycle="+cycle+" OriginalRolesRestored="+restored);
                if(!restored) throw new Exception("Default output restoration failed verification.");
            }
        } finally {
            Defaults.Restore();
            Console.WriteLine("FinalOriginalRolesRestored="+Enumerable.Range(0,3).All(i=>Defaults.Equal(Defaults.Get(i),roles[i])));
        }
        Console.WriteLine("SelfTest=PASS"); return 0;
    }
}
