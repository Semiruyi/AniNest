using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LocalPlayer.Views;

namespace LocalPlayer;

public class MainForm : Form
{
    private MainPage mainPage;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainForm()
    {
        this.Text = "LocalPlayer";
        this.StartPosition = FormStartPosition.Manual;  // 改为手动
        this.BackColor = Color.FromArgb(20, 20, 20);
        
        SetSizeToScreenPercent(0.65);
        this.CenterToScreen();  // 添加这行，强制居中
        this.MinimumSize = new Size(800, 500);
        
        mainPage = new MainPage();
        mainPage.Dock = DockStyle.Fill;
        this.Controls.Add(mainPage);

        this.Load += MainForm_Load;
    }

    private void SetSizeToScreenPercent(double percent)
    {
        Screen screen = Screen.FromControl(this);
        Rectangle workingArea = screen.WorkingArea;
        
        int width = (int)(workingArea.Width * percent);
        int height = (int)(workingArea.Height * percent);
        
        this.Size = new Size(width, height);
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        // 启用深色标题栏
        int useDarkMode = 1;
        DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        // 可选的：如果想保持比例，可以在这里处理
        // 目前允许自由调整大小
    }
}