using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace PretestPosttestLib_CSharp.message
{
    public partial class Form1 : Form
    {
        public string message;
        public Form1(string s1)
        {
            InitializeComponent();
            //       label1.Text = s1;
            UpdateLabelText(s1);
        }

        public void UpdateLabelText(string text)
        {
            this.label1.Text = text;
            this.label1.Refresh();
            this.PerformLayout();  // 重新计算布局
        }
    }
}
