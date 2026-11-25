using System;
using System.IO;
using System.Linq;

sealed class FakeDevices : IDefaultDevices {
    public string[] Roles = { "speakers", "headphones", "headset" };
    public bool Cascade, FailCable, FailRestore;
    public int Writes;
    public string Get(int role) { return Roles[role]; }
    public void Set(string id, int role) {
        Writes++;
        if ((FailCable && id == "cable" && role == 1) || (FailRestore && id == "speakers"))
            throw new IOException("Simulated device failure");
        if (Cascade && role == 0) Roles = new[] { id, id, id };
        else Roles[role] = id;
    }
}
sealed class FakeStore : IRecoveryStore {
    public string[] Data;
    public bool FailSave;
    public string[] Read() { return Data; }
    public void Save(string[] ids) { if(FailSave) throw new IOException("Disk full"); Data = (string[])ids.Clone(); }
    public void Clear() { Data = null; }
}
static class LogicTests {
    static int passed;
    static void Check(bool value) { if(!value) throw new Exception("Assertion failed"); }
    static void Throws(Action action) {
        try { action(); } catch(Exception) { return; }
        throw new Exception("Expected failure");
    }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Original(FakeDevices d) { Check(d.Roles.SequenceEqual(new[] { "speakers", "headphones", "headset" })); }
    static int Main() {
        try {
            Run("switch and restore distinct roles", ()=> {
                var d=new FakeDevices(); var s=new FakeStore(); var session=new DefaultOutputSession(d,s);
                session.Switch("cable"); Check(d.Roles.SequenceEqual(new[]{"cable","cable","headset"}));
                Check(s.Data.Length==4); session.Restore(); Original(d); Check(s.Data==null);
            });
            Run("restore coupled Windows roles", ()=> {
                var d=new FakeDevices { Cascade=true }; var s=new FakeStore(); var session=new DefaultOutputSession(d,s);
                session.Switch("cable"); Check(d.Roles[2]=="headset"); session.Restore(); Original(d);
            });
            Run("partial switch remains recoverable", ()=> {
                var d=new FakeDevices { FailCable=true }; var s=new FakeStore(); var session=new DefaultOutputSession(d,s);
                Throws(()=>session.Switch("cable")); Check(s.Data!=null); session.Restore(); Original(d);
            });
            Run("failed restore keeps journal for retry", ()=> {
                var d=new FakeDevices { Cascade=true }; var s=new FakeStore(); var session=new DefaultOutputSession(d,s);
                session.Switch("cable"); d.FailRestore=true; Throws(()=>session.Restore()); Check(s.Data!=null);
                d.FailRestore=false; session.Restore(); Original(d); Check(s.Data==null);
            });
            Run("journal saved before first mutation", ()=> {
                var d=new FakeDevices(); var s=new FakeStore { FailSave=true }; var session=new DefaultOutputSession(d,s);
                Throws(()=>session.Switch("cable")); Check(d.Writes==0); Original(d);
            });
            Run("malformed journal blocks mutation", ()=> {
                var d=new FakeDevices(); var s=new FakeStore { Data=new[]{"cable","speakers"} }; var session=new DefaultOutputSession(d,s);
                Throws(()=>session.Switch("cable")); Check(d.Writes==0 && s.Data!=null);
                s.Data=new[]{"cable","speakers","","headset"}; Throws(()=>session.Restore()); Check(d.Writes==0);
            });
            Run("already cable leaves original defaults intact", ()=> {
                var d=new FakeDevices { Roles=new[]{"cable","cable","headset"} }; var s=new FakeStore(); var session=new DefaultOutputSession(d,s);
                session.Switch("cable"); session.Restore(); Check(s.Data==null && d.Writes==0);
            });
            Run("stale journal recovered on next session", ()=> {
                var d=new FakeDevices(); var s=new FakeStore(); new DefaultOutputSession(d,s).Switch("cable");
                var next=new DefaultOutputSession(d,s); next.Switch("cable"); next.Restore(); Original(d);
            });
            Run("repeated start stop and redundant restore", ()=> {
                var d=new FakeDevices(); var s=new FakeStore(); var session=new DefaultOutputSession(d,s);
                for(int i=0;i<3;i++) { session.Switch("cable"); session.Restore(); session.Restore(); Original(d); }
            });
            Run("ordinary stereo cable name selection", ()=> {
                Check(DeviceSelection.IsCable("扬声器 (VB-Audio Virtual Cable)"));
                Check(DeviceSelection.IsCable("CABLE Input (VB-Audio Virtual Cable)"));
                foreach(var name in new[]{"CABLE Output (VB-Audio Virtual Cable)","Speakers (VB-Audio Virtual Cable 16 Ch)","CABLE-A Input (VB-Audio Cable A)","Speakers (Realtek Audio)"})
                    Check(!DeviceSelection.IsCable(name));
            });
            Run("built-in array name selection", ()=> {
                Check(DeviceSelection.IsMicrophone("麦克风阵列 (Realtek Audio)")); Check(DeviceSelection.IsMicrophone("Microphone Array"));
                Check(!DeviceSelection.IsMicrophone("CABLE Output"));
            });
            Run("recovery file round trip and no overwrite", ()=> {
                var dir=Path.Combine(Path.GetTempPath(),"MicCastTests-"+Guid.NewGuid().ToString("N"));
                var path=Path.Combine(dir,"restore.txt"); var store=new RecoveryFile(path);
                try {
                    Check(store.Read()==null); var ids=new[]{"cable","speakers","headphones","headset"};
                    store.Save(ids); Check(store.Read().SequenceEqual(ids));
                    Throws(()=>store.Save(new[]{"other","a","b","c"})); Check(store.Read().SequenceEqual(ids));
                    store.Clear(); Check(store.Read()==null);
                } finally { if(File.Exists(path)) File.Delete(path); if(File.Exists(path+".tmp")) File.Delete(path+".tmp"); Directory.Delete(dir); }
            });
            Console.WriteLine("LogicTests="+passed+" PASS"); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
