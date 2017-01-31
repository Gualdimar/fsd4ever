using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace fsd4ever
{
    public partial class Main : Form
    {
        private int _urlLength = 0x3A;
        private int _tuOffset = 0x2CA36C;
        private int _covtermOffset = 0x2CBE1C;
        private int _covidOffset = 0x2CBE58;
        private string _tuLink = "?tid=%08x&mid=%08x";
        private string _covByTermLink = "?query=SearchTerm&id=";
        private string _covByIDLink = "?query=TitleID&id=";
        private string xexfile = "default-binary.xex";

        public Main()
        {
            InitializeComponent();
            tuURL.MaxLength = _urlLength - _tuLink.Length;
            coverURL.MaxLength = _urlLength - _covByTermLink.Length;
        }

        private void Generate_Click(object sender,EventArgs e)
        {
            if (File.Exists(xexfile))
                File.Delete(xexfile);

            File.WriteAllBytes(xexfile,Properties.Resources.default_binary);
            using (var bw = new BinaryWriter(File.Open(xexfile,FileMode.Open,FileAccess.Write,FileShare.None)))
            {
                bw.Seek(_tuOffset,SeekOrigin.Begin);
                bw.Write(Encoding.ASCII.GetBytes(tuURL.Text + _tuLink));
                bw.Seek(_covtermOffset,SeekOrigin.Begin);
                bw.Write(Encoding.ASCII.GetBytes(coverURL.Text + _covByTermLink));
                bw.Seek(_covidOffset,SeekOrigin.Begin);
                bw.Write(Encoding.ASCII.GetBytes(coverURL.Text + _covByIDLink));
            }

            var xextool = Process.Start("xextool.exe",@"-e e -o default.xex " + xexfile);
            xextool.WaitForExit();

            File.Delete(xexfile);

            MessageBox.Show("Done");
        }
    }
}
