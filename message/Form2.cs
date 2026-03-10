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

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
