using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmegaScript.Scripts
{
    public partial class strainHalf : Form
    {
        public strainHalf()
        {
            InitializeComponent();
        }

        public int whichOne;

        private void oneMLDelvBtn_Click(object sender, EventArgs e)
        {
            whichOne = 1;
            this.Close();
        }

        private void twoMLDelvBtn_Click(object sender, EventArgs e)
        {
            whichOne = 2;
            this.Close();
        }

        private void halfDelBtn_Click(object sender, EventArgs e)
        {
            whichOne = 3;
            this.Close();
        }
    }
}
