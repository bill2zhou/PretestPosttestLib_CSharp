using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PretestPosttestLib_CSharp.message
{
    public partial class Form2 : Form
    {
        public Form2(string s1)
        {
            InitializeComponent();
            label1.Text = s1;
        }

        /// <summary>
        /// 窗口标题变化时同步更新标题栏文字
        /// </summary>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (headerTitle != null)
            {
                headerTitle.Text = this.Text;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
