using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LocalPlayer.Views;

namespace LocalPlayer;

public class MainForm : Form
{
    private MainPage mainPage;
    private PlayerPage? playerPage;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainForm()
    {
        this.Text = "LocalPlayer";
        this.StartPosition = FormStartPosition.Manual;
        this.BackColor = Color.FromArgb(20, 20, 20);
        
        SetSizeToScreenPercent(0.65);
        this.CenterToScreen();
        this.MinimumSize = new Size(800, 500);
        
        mainPage = new MainPage();
        mainPage.Dock = DockStyle.Fill;
        
        // 订阅事件
        mainPage.FolderSelected += MainPage_FolderSelected;
        
        this.Controls.Add(mainPage);
        this.Load += MainForm_Load;
    }

    // 事件处理方法
    private void MainPage_FolderSelected(object? sender, string folderPath, string folderName)
    {
        // 移除旧播放页
        if (playerPage != null)
        {
            this.Controls.Remove(playerPage);
            playerPage.Dispose();
        }
        
        // 创建新播放页
        playerPage = new PlayerPage();
        playerPage.Dock = DockStyle.Fill;
        playerPage.BackRequested += PlayerPage_BackRequested;
        
        // 加载文件夹
        playerPage.LoadFolder(folderPath, folderName);
        
        // 切换显示
        mainPage.Visible = false;
        this.Controls.Add(playerPage);
    }

    private void PlayerPage_BackRequested(object? sender, EventArgs e)
    {
        // 返回首页
        if (playerPage != null)
        {
            this.Controls.Remove(playerPage);
            playerPage.Dispose();
            playerPage = null;
        }
        mainPage.Visible = true;
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
        int useDarkMode = 1;
        DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }
}