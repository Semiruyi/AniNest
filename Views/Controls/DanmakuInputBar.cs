using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace LocalPlayer.Views.Controls;

public class DanmakuInputBar : UserControl
{
    private TextBox inputBox = null!;
    private Button sendButton = null!;
    private Button toggleButton = null!;
    private Button testButton = null!;
    private Color selectedColor = Color.White;

    public event EventHandler<string>? DanmakuSent;
    public event EventHandler? ToggleRequested;
    public event EventHandler? TestDanmakuRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool DanmakuEnabled { get; private set; } = true;

    private static readonly Color[] PresetColors = {
        Color.White, Color.Red, Color.Orange, Color.Yellow,
        Color.LimeGreen, Color.Cyan, Color.DodgerBlue, Color.Magenta,
        Color.HotPink, Color.Gold
    };

    public DanmakuInputBar()
    {
        BackColor = Color.FromArgb(40, 40, 40);
        Height = 36;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        toggleButton = new Button
        {
            Text = "弹",
            Size = new Size(36, 28),
            Location = new Point(4, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        toggleButton.FlatAppearance.BorderSize = 0;
        toggleButton.Click += ToggleButton_Click;

        inputBox = new TextBox
        {
            Location = new Point(46, 5),
            Size = new Size(300, 26),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 11),
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "输入弹幕..."
        };
        inputBox.KeyDown += InputBox_KeyDown;

        sendButton = new Button
        {
            Text = "发送",
            Size = new Size(60, 28),
            Location = new Point(352, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 10),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        sendButton.FlatAppearance.BorderSize = 0;
        sendButton.Click += SendButton_Click;

        testButton = new Button
        {
            Text = "测试",
            Size = new Size(48, 28),
            Location = new Point(296, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(80, 80, 80),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("微软雅黑", 9),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        testButton.FlatAppearance.BorderSize = 0;
        testButton.Click += (s, e) => TestDanmakuRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange(new Control[] { toggleButton, inputBox, testButton, sendButton });
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (sendButton != null && inputBox != null && testButton != null)
        {
            sendButton.Location = new Point(Width - 68, 4);
            testButton.Location = new Point(Width - 68 - 56, 4);
            inputBox.Size = new Size(Width - 46 - 56 - 72 - 8, 26);
        }
    }

    private void SendButton_Click(object? sender, EventArgs e) => SendDanmaku();
    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            SendDanmaku();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }

    private void SendDanmaku()
    {
        string text = inputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        DanmakuSent?.Invoke(this, text);
        inputBox.Text = "";
    }

    private void ToggleButton_Click(object? sender, EventArgs e)
    {
        DanmakuEnabled = !DanmakuEnabled;
        toggleButton.BackColor = DanmakuEnabled ? Color.FromArgb(0, 122, 204) : Color.FromArgb(80, 80, 80);
        toggleButton.ForeColor = DanmakuEnabled ? Color.White : Color.FromArgb(150, 150, 150);
        ToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    public Color GetSelectedColor() => selectedColor;

    public void SetColor(Color color) => selectedColor = color;
}
