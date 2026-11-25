using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

// A single floating microphone. No taskbar button or rectangular window chrome.
sealed class MicForm : Form {
    readonly ToolTip tip=new ToolTip { InitialDelay=250, ReshowDelay=100, AutoPopDelay=12000 };
    readonly ContextMenuStrip menu=new ContextMenuStrip();
    readonly ToolStripMenuItem action=new ToolStripMenuItem("开始扩声");
    readonly Timer animation=new Timer { Interval=33 };
    readonly Bitmap idleRing=MakeRing(Color.FromArgb(64,155,255));
    readonly Bitmap liveRing=MakeRing(Color.FromArgb(255,66,91));
    readonly Bitmap errorRing=MakeRing(Color.FromArgb(255,177,69));
    readonly Font statusFont=new Font("Microsoft YaHei UI",9f,FontStyle.Bold,GraphicsUnit.Pixel);
    float ringAngle=-90;
    Route route;
    bool busy,closing,dragging,pressed,hovered,failed,missingDriver;
    Point downScreen,downWindow;
    string status="点击开始扩声\n拖动可移动 · 右键可退出";
    public MicForm() {
        Text="MicCast";
        FormBorderStyle=FormBorderStyle.None;
        AutoScaleMode=AutoScaleMode.None;
        ClientSize=new Size(88,88);
        ShowInTaskbar=false; TopMost=true; DoubleBuffered=true;
        BackColor=Color.FromArgb(18,32,54); Cursor=Cursors.Hand;
        StartPosition=FormStartPosition.Manual;
        AccessibleName="投屏麦克风"; AccessibleDescription=status;
        action.Click+=async(s,e)=>await Toggle();
        var help=new ToolStripMenuItem("使用说明");
        help.Click+=(s,e)=>tip.Show("先连接投屏软件并开启电脑声音分享。\n左键开始 / 停止扩声，拖动可移动。\n右键退出，自动恢复原来的声音输出。",this,Width/2,Height,10000);
        var exit=new ToolStripMenuItem("退出"); exit.Click+=(s,e)=>Close();
        var install=new ToolStripMenuItem("安装 VB-CABLE…"); install.Click+=(s,e)=>DriverSetup.ShowGuide(this);
        menu.Items.AddRange(new ToolStripItem[]{action,help,install,new ToolStripSeparator(),exit});
        menu.Opening+=(s,e)=> { action.Text=missingDriver ? "安装声音驱动…" : route==null ? "开始扩声" : "停止扩声"; action.Enabled=!busy; };
        ContextMenuStrip=menu;
        SetStyle(ControlStyles.ResizeRedraw,true);
        animation.Tick+=(s,e)=> { ringAngle=(ringAngle+(route==null ? 2f : 4f))%360; Invalidate(); };
        Shown+=async(s,e)=> {
            using(var graphics=CreateGraphics()) {
                int diameter=(int)Math.Round(88*graphics.DpiX/96f);
                ClientSize=new Size(diameter,diameter);
            }
            var area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(area.Right-Width-28,area.Bottom-Height-76);
            UpdateShape();
            try { Defaults.Restore(); } catch(Exception x) { Error("输出尚未恢复",x.Message); }
            tip.SetToolTip(this,status);
            animation.Start();
            try {
                bool installed=await DriverSetup.IsInstalled();
                if(IsDisposed || route!=null || busy || failed) return;
                missingDriver=!installed;
                if(missingDriver) {
                    SetState("需要安装 VB-CABLE\n点击查看安装说明",true);
                    DriverSetup.ShowGuide(this);
                }
            } catch(Exception x) { if(!IsDisposed) Error("无法检查声音驱动",x.Message); }
        };
        FormClosing+=(s,e)=> {
            if(busy) { closing=true; e.Cancel=true; }
            else if(!Stop(false)) { closing=false; e.Cancel=true; }
        };
    }
    void UpdateShape() {
        using(var shape=new GraphicsPath()) {
            int diameter=Math.Min(ClientSize.Width,ClientSize.Height);
            shape.AddEllipse((ClientSize.Width-diameter)/2+1,(ClientSize.Height-diameter)/2+1,diameter-2,diameter-2);
            var old=Region; Region=new Region(shape); if(old!=null) old.Dispose();
        }
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if(Width>2 && Height>2) UpdateShape(); }
    static Bitmap MakeRing(Color color) {
        // A periodic angular gradient has no head/tail seam. Cache it once;
        // animation only rotates the texture, without joining short arc segments.
        const int size=264;
        var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
            double dx=(x+.5)/3-44,dy=(y+.5)/3-44;
            double distance=Math.Sqrt(dx*dx+dy*dy)-39;
            if(Math.Abs(distance)>5) continue;
            double angular=.18+.82*Math.Pow((1+Math.Cos(Math.Atan2(dy,dx)))/2,3);
            double core=Math.Exp(-distance*distance/1.5);
            double glow=Math.Exp(-distance*distance/10)*.25;
            int alpha=(int)(255*Math.Min(1,core+glow)*angular);
            double white=core*angular*.35;
            bitmap.SetPixel(x,y,Color.FromArgb(alpha,
                (int)(color.R+(255-color.R)*white),
                (int)(color.G+(255-color.G)*white),
                (int)(color.B+(255-color.B)*white)));
        }
        return bitmap;
    }
    static void DrawEtched(Graphics graphics,GraphicsPath path,float width,int opacity) {
        // A shallow groove: dark upper edge, silver lower rim, subdued metal face.
        var saved=graphics.Save();
        graphics.TranslateTransform(-.5f,-.6f);
        using(var shadow=new Pen(Color.FromArgb(opacity*3/4,0,0,0),width+1.1f)) {
            shadow.StartCap=shadow.EndCap=LineCap.Round; graphics.DrawPath(shadow,path);
        }
        graphics.Restore(saved); saved=graphics.Save();
        graphics.TranslateTransform(.55f,.65f);
        using(var rim=new Pen(Color.FromArgb(opacity/3,219,227,235),width+.5f)) {
            rim.StartCap=rim.EndCap=LineCap.Round; graphics.DrawPath(rim,path);
        }
        graphics.Restore(saved);
        using(var metal=new LinearGradientBrush(new RectangleF(20,21,48,38),
            Color.FromArgb(opacity,164,173,184),Color.FromArgb(opacity,104,113,125),90f))
        using(var face=new Pen(metal,width)) {
            face.StartCap=face.EndCap=LineCap.Round; graphics.DrawPath(face,path);
        }
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        float diameter=Math.Min(ClientSize.Width,ClientSize.Height);
        g.TranslateTransform((ClientSize.Width-diameter)/2,(ClientSize.Height-diameter)/2);
        g.ScaleTransform(diameter/88f,diameter/88f);
        bool live=route!=null;
        Color baseColor=failed ? Color.FromArgb(76,47,24) : live ? Color.FromArgb(86,22,38) : Color.FromArgb(18,32,54);
        using(var brush=new SolidBrush(baseColor)) g.FillEllipse(brush,0,0,88,88);
        using(var shade=new LinearGradientBrush(new Rectangle(0,0,88,88),Color.FromArgb(hovered?38:20,255,255,255),Color.FromArgb(18,0,0,0),90f)) g.FillEllipse(shade,2,2,84,84);
        var ringState=g.Save();
        g.TranslateTransform(44,44); g.RotateTransform(ringAngle); g.TranslateTransform(-44,-44);
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.DrawImage(failed ? errorRing : live ? liveRing : idleRing,new RectangleF(0,0,88,88));
        g.Restore(ringState);
        using(var mic=new GraphicsPath()) {
            mic.AddArc(38,23,12,12,180,180);
            mic.AddLine(50,29,50,38); mic.AddArc(38,32,12,12,0,180); mic.CloseFigure();
            mic.StartFigure(); mic.AddArc(33,28,22,22,0,180);
            mic.StartFigure(); mic.AddLine(44,51,44,56);
            mic.StartFigure(); mic.AddLine(39,56,49,56);
            DrawEtched(g,mic,1.8f,255);
        }
        if(live) {
            // Concentric outward waves share the microphone centre, with a quieter outer pair.
            for(int band=0;band<2;band++) using(var waves=new GraphicsPath()) {
                float radius=18+band*5;
                waves.AddArc(44-radius,35-radius,radius*2,radius*2,-32,64);
                waves.StartFigure(); waves.AddArc(44-radius,35-radius,radius*2,radius*2,148,64);
                DrawEtched(g,waves,1.4f,band==0 ? 220 : 155);
            }
        } else if(!busy && !failed) {
            using(var cutout=new Pen(baseColor,4.2f)) g.DrawLine(cutout,32,26,56,50);
            using(var slash=new GraphicsPath()) { slash.AddLine(32,26,56,50); DrawEtched(g,slash,1.7f,255); }
        }
        string label=missingDriver ? "需安装" : failed ? "需处理" : busy ? "连接中" : live ? "扩声中" : "已关闭";
        using(var brush=new SolidBrush(failed ? Color.FromArgb(255,210,143) : live ? Color.FromArgb(255,219,226) : Color.FromArgb(175,210,255)))
        using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center })
            g.DrawString(label,statusFont,brush,new RectangleF(14,63,60,15),format);
        if(Focused && ShowFocusCues) using(var focus=new Pen(Color.FromArgb(200,255,255,255),1f)) { focus.DashStyle=DashStyle.Dot; g.DrawEllipse(focus,10,10,68,68); }
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered=true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered=false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if(e.Button!=MouseButtons.Left) return;
        pressed=true; dragging=false; downScreen=Cursor.Position; downWindow=Location; Capture=true;
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e); if(!pressed) return;
        var p=Cursor.Position; int dx=p.X-downScreen.X,dy=p.Y-downScreen.Y;
        if(Math.Abs(dx)>SystemInformation.DragSize.Width/2 || Math.Abs(dy)>SystemInformation.DragSize.Height/2) dragging=true;
        if(dragging) Location=new Point(downWindow.X+dx,downWindow.Y+dy);
    }
    protected override async void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e); if(e.Button!=MouseButtons.Left || !pressed) return;
        pressed=false; Capture=false;
        if(dragging) {
            var area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
        } else if(ClientRectangle.Contains(e.Location)) await Toggle();
    }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture) pressed=false; }
    protected override async void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter) { e.Handled=true; await Toggle(); }
    }
    void SetState(string message,bool error) {
        failed=error; status=message+"\n拖动可移动 · 右键可退出";
        AccessibleDescription=status; tip.SetToolTip(this,status); Invalidate();
    }
    void Error(string title,string message) {
        SetState(title+"\n"+message,true);
        if(IsHandleCreated && !IsDisposed) tip.Show(title+"\n"+message,this,Width/2,Height,12000);
    }
    bool Stop(bool notify) {
        var old=route; route=null;
        try {
            if(old!=null) old.Dispose(); else Defaults.Restore();
            if(notify) SetState("已停止扩声 · 原输出已恢复\n点击可再次开始",false);
            return true;
        } catch(Exception e) { Error("输出尚未恢复，请重新连接原设备后重试",e.Message); return false; }
    }
    async Task Toggle() {
        if(busy) return;
        if(missingDriver) {
            busy=true;
            try {
                missingDriver=!await DriverSetup.IsInstalled();
                if(missingDriver) DriverSetup.ShowGuide(this);
                else SetState("驱动已就绪 · 点击开始扩声",false);
            } catch(Exception e) { Error("无法检查声音驱动",e.Message); }
            finally { busy=false; if(closing) Close(); }
            return;
        }
        if(route!=null) { Stop(true); return; }
        busy=true; SetState("正在连接内置麦克风…",false); var pending=new Route();
        try {
            await pending.Open();
            if(closing) { pending.Dispose(); return; }
            pending.Fault+=()=> {
                try { if(IsHandleCreated && !IsDisposed) BeginInvoke((Action)(()=> {
                    if(route==pending && Stop(false)) Error("声音设备已断开","重新连接后，点击麦克风重试。");
                })); } catch(InvalidOperationException) { }
            };
            pending.Start(); route=pending;
            SetState("正在扩声 · 点击停止\n麦克风已接通，请在大屏确认声音。",false);
        } catch(Exception e) {
            try { pending.Dispose(); Error("暂时无法扩声",e.Message); }
            catch(Exception r) { Error("输出尚未恢复",e.Message+" "+r.Message); }
        } finally { busy=false; Invalidate(); if(closing) Close(); }
    }
    protected override void Dispose(bool disposing) {
        if(disposing) { animation.Dispose(); tip.Dispose(); menu.Dispose(); idleRing.Dispose(); liveRing.Dispose(); errorRing.Dispose(); statusFont.Dispose(); }
        base.Dispose(disposing);
    }
}
