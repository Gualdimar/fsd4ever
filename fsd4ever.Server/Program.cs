using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace fsd4ever.Server
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += (sender, args) => Environment.Exit(0); // This is a hack to kill the HTTP server, it may or may not stop otherwise :(
            Application.Run(new Form1());
        }
    }
}
