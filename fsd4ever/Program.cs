using System;
using System.Windows.Forms;

namespace fsd4ever
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!System.IO.File.Exists("xextool.exe"))
                System.IO.File.WriteAllBytes("xextool.exe", Properties.Resources.xextool);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
        }
    }
}
