// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Security.Policy;

namespace Win11TaskbarPcInfo
{
    public partial class TaskbarInfoControl : UserControl
    {
        public bool VerticalTaskbarMode
        {
            get; private set;
        }

        public TaskbarInfoControl()            
        {
            Disposed += OnDispose;
            this.SetStyle(ControlStyles.EnableNotifyMessage, true);
            try
            {
                Initialize();
                this.BackColor = Color.Transparent;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnDispose(object sender, EventArgs e)
        {
        }
        private void ApplyTheme()
        {
#if false // Not worked
            Win32.EnableDarkMode(this.Handle);
            foreach (Control ctrl in this.Controls)
            {
                Win32.EnableDarkMode(ctrl.Handle);
            }
#else
            bool dark = Win32.IsDarkMode();
            //this.BackColor = dark ? Color.FromArgb(32, 32, 32) : Color.Transparent /*  SystemColors.Control */;
            //this.ForeColor = dark ? Color.White : Color.Black;
            if (dark)
            {
                foreach (Control ctrl in this.Controls)
                {
                    ctrl.BackColor = Color.FromArgb(32, 32, 32);
                    ctrl.ForeColor = Color.White;
                }
            }
#endif
        }

        private void Initialize()
        {

            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Opaque, true);
            //Initialize();

            InitializeComponent();
            AdjustControlSize();
            ApplyTheme();
        }

        private void updateLabel()
        {
            string pcname = Environment.MachineName;
            string ip = "";
            string hostname = Dns.GetHostName();
            IPAddress[] ips = Dns.GetHostAddresses(hostname);
            foreach (IPAddress a in ips)
            {
                if (a.AddressFamily.Equals(AddressFamily.InterNetwork)) // IPv4
                {
                    ip = a.ToString();
                    break;
                }
            }
            string s = pcname;
            if (ip.Length > 0)
            {
                s = s + "\r\n" + ip;
            }
            if (this.label1 != null)
            {
                this.label1.Text = s;
            }
        }

        private void AdjustControlSize()
        {
            int taskbarWidth = Screen.PrimaryScreen.Bounds.Width - Screen.PrimaryScreen.WorkingArea.Width;
            int taskbarHeight = Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;

            // taskbar not being shown
            if (taskbarWidth == 0 && taskbarHeight == 0)
            {
                return;
            }
            int minimumHeight = taskbarHeight;            
            if (minimumHeight < 20)
                minimumHeight = 20;

            if (taskbarWidth > 0 && taskbarHeight == 0)
                VerticalTaskbarMode = true;
            else if (taskbarWidth == 0 && taskbarHeight > 0)
                VerticalTaskbarMode = false;

            //updateLabel();
        }

        private void TaskbarInfoControl_VisibleChanged(object sender, EventArgs e)
        {
            updateLabel();
            ApplyTheme();
        }
    }
}
