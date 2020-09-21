﻿using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SyncClipboard.Control
{
    delegate void Notify(bool notify, bool notifyIconText, string title, string content, string contentSimple, string level);
    public class MainController:System.Windows.Forms.Control
    {
        private string notifyText;
        private static int WM_CLIPBOARDUPDATE = 0x031D;

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenu contextMenu;
        private System.Windows.Forms.MenuItem 退出MenuItem;
        private System.Windows.Forms.MenuItem 设置MenuItem;
        private System.Windows.Forms.MenuItem 开机启动MenuItem;
        private System.Windows.Forms.MenuItem 上传本机MenuItem;
        private System.Windows.Forms.MenuItem 下载远程MenuItem;
        private System.Windows.Forms.MenuItem 检查更新MenuItem;
        private System.Windows.Forms.MenuItem lineMenuItem;


        public SyncService syncService;

        SettingsForm settingsForm;
        bool isSttingsFormExist = false;

        public MainController()
        {
            InitializeComponent();
            this.LoadConfig();
        }
        private void InitializeComponent()
        {
            this.设置MenuItem = new System.Windows.Forms.MenuItem("设置");
            this.开机启动MenuItem = new System.Windows.Forms.MenuItem("开机启动");
            this.上传本机MenuItem = new System.Windows.Forms.MenuItem("上传本机");
            this.下载远程MenuItem = new System.Windows.Forms.MenuItem("下载远程");
            this.退出MenuItem = new System.Windows.Forms.MenuItem("退出");
            this.检查更新MenuItem = new System.Windows.Forms.MenuItem("检查更新");
            this.lineMenuItem = new System.Windows.Forms.MenuItem("-");

            this.设置MenuItem.Click += new System.EventHandler(this.设置MenuItem_Click);
            this.开机启动MenuItem.Click += new System.EventHandler(this.开机启动MenuItem_Click);
            this.上传本机MenuItem.Click += new System.EventHandler(this.上传本机MenuItem_Click);
            this.下载远程MenuItem.Click += new System.EventHandler(this.下载远程MenuItem_Click);
            this.退出MenuItem.Click += new System.EventHandler(this.退出MenuItem_Click);
            this.检查更新MenuItem.Click += new System.EventHandler(this.检查更新MenuItem_Click);
            
            this.contextMenu = new ContextMenu(new MenuItem[] { 
                this.设置MenuItem, 
                this.lineMenuItem.CloneMenu(),
                this.开机启动MenuItem,
                this.上传本机MenuItem,
                this.下载远程MenuItem,
                this.lineMenuItem.CloneMenu(),
                this.检查更新MenuItem,
                this.退出MenuItem
            });

            this.notifyIcon1 = new NotifyIcon();
            this.notifyIcon1.ContextMenu = this.contextMenu;
            this.notifyIcon1.Icon = Properties.Resources.upload;
            this.notifyIcon1.Text = "SyncClipboard";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.BalloonTipClicked += new System.EventHandler(this.notifyIcon1_BalloonTipClicked);
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.设置MenuItem_Click);
        }
        
        public void LoadConfig()
        {
            try
            {
                Config.Load();
            }
            catch
            { 
                MessageBox.Show("配置文件出错", "初始化默认配置(还没做，即将退出)", MessageBoxButtons.YesNo); 
                Application.Exit(); 
                return; 
            }
            if(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", Program.SoftName, null) == null)
            {
                this.开机启动MenuItem.Checked = false;
            }
            else
            {
                this.开机启动MenuItem.Checked = true;
            }
            this.上传本机MenuItem.Checked = Config.IfPush;
            this.下载远程MenuItem.Checked = Config.IfPull;
        }
        public void setLog(bool notify,bool notifyIconText,string title,string content,string contentSimple,string level)
        {
            try 
            { 
                if(notify)
                {
                    notifyText = content;
                    this.notifyIcon1.ShowBalloonTip(5, title, SafeMessage(content), ToolTipIcon.None);
                }
                if (notifyIconText)
                {
                    this.notifyIcon1.Text = Program.SoftName + "\n" + title + "\n" + contentSimple;
                }
                if(level == "erro")
                {
                    notifyIcon1.Icon = Properties.Resources.erro;
                }
                else if(level == "info")
                {
                    notifyIcon1.Icon = Properties.Resources.upload;
                }
            }
            catch (Exception  ex)
            {
                //Console.WriteLine("Setlog错误");
            }
        }
        private void 退出MenuItem_Click(object sender, EventArgs e)
        {
            Config.IfPull = false;
            Config.IfPush = false;
            this.notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void 设置MenuItem_Click(object sender, EventArgs e)
        {
            if (!isSttingsFormExist)
            {
                isSttingsFormExist = true;
                this.settingsForm = new SettingsForm(this);
                settingsForm.ShowDialog();
                isSttingsFormExist = false;
            }
            else
            {
                settingsForm.Activate();
            }
        }

        private void 开机启动MenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.开机启动MenuItem.Checked != true)
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", Program.SoftName, Application.ExecutablePath);
                }
                else
                {
                    Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true).DeleteValue(Program.SoftName, false);
                }
                this.开机启动MenuItem.Checked = !this.开机启动MenuItem.Checked;
            }
            catch
            {
                setLog(true, false, "设置启动项失败", "设置启动项失败", null, "warn");
            }
        }

        private void notifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {
            if (notifyText.Length < 4)
                return;
            if (notifyText.Substring(0,4) == "http" || notifyText.Substring(0,4) == "www.")
                System.Diagnostics.Process.Start(this.notifyText);  
        }

        private void 上传本机MenuItem_Click(object sender, EventArgs e)
        {
            this.上传本机MenuItem.Checked = !this.上传本机MenuItem.Checked;
            Config.IfPush = this.上传本机MenuItem.Checked;
            Config.Save();
        }

        private void 下载远程MenuItem_Click(object sender, EventArgs e)
        {
            this.下载远程MenuItem.Checked = !this.下载远程MenuItem.Checked;
            Config.IfPull = this.下载远程MenuItem.Checked;
            Config.Save();
        }

        private void 检查更新MenuItem_Click(object sender, EventArgs e)
        {
            UpdateChecker updateChecker = new UpdateChecker();
            updateChecker.Check();
        }

        private String SafeMessage(String str)
        {
            if (str.Length > 42)
            {
                return str.Substring(0, 40) + "...";
            }

            return str;
        }
    }
}
