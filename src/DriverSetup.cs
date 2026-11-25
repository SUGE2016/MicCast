using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Enumeration;

static class DriverSetup {
    public const string DownloadUrl="https://vb-audio.com/Cable/";
    public static async Task<bool> IsInstalled() {
        var outputs=await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
        return outputs.Any(d=>d.IsEnabled && DeviceSelection.IsCable(d.Name));
    }
    public static void ShowGuide(IWin32Window owner) {
        using(var dialog=new Form()) {
            dialog.Text="MicCast · 首次使用";
            dialog.FormBorderStyle=FormBorderStyle.FixedDialog;
            dialog.MaximizeBox=false; dialog.MinimizeBox=false; dialog.ShowInTaskbar=false;
            dialog.StartPosition=FormStartPosition.CenterScreen;
            dialog.AutoScaleDimensions=new SizeF(96,96); dialog.AutoScaleMode=AutoScaleMode.Dpi;
            dialog.ClientSize=new Size(460,245); dialog.BackColor=Color.FromArgb(249,250,252);
            dialog.Font=new Font("Microsoft YaHei UI",10);
            var title=new Label { Text="还需要一个声音驱动",Bounds=new Rectangle(24,24,412,34),Font=new Font(dialog.Font.FontFamily,17,FontStyle.Bold) };
            var description=new Label {
                Text="请从 VB-Audio 官网安装普通 VB-CABLE 驱动，\n完成后重启 Windows，再打开 MicCast。\n\n驱动负责把麦克风声音交给投屏软件。\n如果已安装，请启用普通双声道播放设备。",
                Bounds=new Rectangle(26,73,410,112)
            };
            var download=new Button { Text="打开官方下载页",Bounds=new Rectangle(24,197,200,33) };
            download.Click+=(s,e)=> {
                try { Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute=true }); }
                catch(Exception x) { MessageBox.Show(dialog,"无法打开浏览器，请访问：\n"+DownloadUrl+"\n\n"+x.Message,"下载 VB-CABLE"); }
            };
            var later=new Button { Text="稍后",Bounds=new Rectangle(332,197,104,33),DialogResult=DialogResult.Cancel };
            dialog.Controls.AddRange(new Control[]{title,description,download,later});
            dialog.AcceptButton=download; dialog.CancelButton=later;
            dialog.ShowDialog(owner);
        }
    }
}
