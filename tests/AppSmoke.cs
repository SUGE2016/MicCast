using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

static class AppSmoke {
    [STAThread] static int Main(string[] args) {
        try {
            string exe=Path.GetFullPath(args[0]);
            if(File.Exists(exe+".config")) throw new Exception("Single-EXE smoke test must not have an adjacent config file.");
            using(var process=Process.Start(new ProcessStartInfo(exe,"--invalid-option") { UseShellExecute=false,CreateNoWindow=true })) {
                if(!process.WaitForExit(10000)) { process.Kill(); throw new Exception("Standalone EXE startup timed out."); }
                if(process.ExitCode!=2) throw new Exception("Standalone EXE returned unexpected exit code.");
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var assembly=Assembly.LoadFrom(exe);
            using(var license=assembly.GetManifestResourceStream("MicCast.LICENSE")) {
                if(license==null) throw new Exception("Embedded license missing.");
            }
            var formType=assembly.GetType("MicForm",true);
            int idlePixel=0,livePixel=0;
            for(int state=0;state<3;state++) {
                using(var form=(Form)Activator.CreateInstance(formType,true)) {
                    form.ClientSize=new Size(132,132);
                    if(state==1) formType.GetField("route",BindingFlags.Instance|BindingFlags.NonPublic)
                        .SetValue(form,Activator.CreateInstance(assembly.GetType("Route",true),true));
                    if(state==2) {
                        formType.GetField("missingDriver",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
                        formType.GetField("failed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
                    }
                    using(var bitmap=new Bitmap(132,132)) {
                        form.DrawToBitmap(bitmap,new Rectangle(0,0,132,132));
                        using(var graphics=Graphics.FromImage(bitmap)) {
                            var bounds=form.Region.GetBounds(graphics);
                            if(Math.Abs(bounds.Width-bounds.Height)>.1f) throw new Exception("Widget region is not circular.");
                        }
                        if(state==0) idlePixel=bitmap.GetPixel(66,18).ToArgb();
                        if(state==1) livePixel=bitmap.GetPixel(66,18).ToArgb();
                    }
                }
            }
            if(idlePixel==livePixel) throw new Exception("Idle and live rendering are indistinguishable.");
            Console.WriteLine("AppSmoke=PASS (standalone EXE, embedded license, circular idle/live/setup rendering; no audio activated)");
            return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
