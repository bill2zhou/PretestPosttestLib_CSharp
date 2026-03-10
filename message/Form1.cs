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
    public partial class Form1 : Form
    {
        public string message;
        public Form1(string s1)
        {
            InitializeComponent();
            label1.Text = s1;
        }
    }
}
