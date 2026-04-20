using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LocalPlayer.Views;

namespace LocalPlayer;

public class MainForm : Form
{
    private MainPage mainPage;

    // Windows API 设置标题栏颜色
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    public MainForm()
    {
        this.Text = "LocalPlayer";
        this.Size = new Size(1000, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(20, 20, 20);
        
        mainPage = new MainPage();
        mainPage.Dock = DockStyle.Fill;
        this.Controls.Add(mainPage);

        // 设置标题栏为深色
        this.Load += MainForm_Load;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        // 启用深色模式（Win10 20H1+ / Win11）
        int useDarkMode = 1;
        DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }
}