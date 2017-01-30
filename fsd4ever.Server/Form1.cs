using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace fsd4ever.Server
{
    public partial class Form1 : Form
    {
        private readonly BackgroundWorker bw = new BackgroundWorker();
        private readonly HttpListener listener = new HttpListener();

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                button1.Enabled = false;
                if (listener.IsListening)
                {
                    listener.Prefixes.Clear();
                    listener.Abort();
                }
                var portstr = $"{numericUpDown1.Value:F0}";
                listener.Prefixes.Add($"http://*:{portstr}/");
                AppendLine($"Listening on port {portstr}");
                listener.Start();
                if (bw.IsBusy)
                    return;
                bw.DoWork += (o, args) =>
                {
                    while (true)
                    {
                        var ctx = listener.GetContext();
                        AppendLine(ctx.Request.Url.OriginalString);
                        var phpScript = ctx.Request.Url.Segments[1];
                        using (
                            var writer = new StreamWriter(ctx.Response.OutputStream, Encoding.GetEncoding("ISO-8859-1"))
                        )
                        {
                            var ret = HandleRequest(phpScript, ctx.Request.QueryString);
                            writer.WriteLine(ret);
                            ctx.Response.StatusCode = ret != null ? 200 : 404;
                        }
                        
                    }
                };
                bw.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                AppendLine("Error:");
                AppendLine(ex.ToString());
            }
        }

        private static T GetJson<T>(string url)
        {
            using (var wc = new WebClient())
            {
                return JsonConvert.DeserializeObject<T>(wc.DownloadString(url));
            }
        }

        private string HandleRequest(string phpScript, NameValueCollection requestQueryString)
        {
            var sb = new StringBuilder();
            try
            {
                if (phpScript.Equals("tu.php", StringComparison.OrdinalIgnoreCase) ||
                    phpScript.Equals("tu_f.php", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><updates>");
                    foreach (var t in GetJson<TuResponse>("http://xboxunity.net/api/tu/" + requestQueryString["tid"]).Tus){
                        if (!requestQueryString["mid"].Equals(t.Mediaid, StringComparison.OrdinalIgnoreCase) &&
                            phpScript.Equals("tu.php", StringComparison.OrdinalIgnoreCase))
                            break; // Skip this one
                        sb.Append("<title ");
                        sb.Append("titleid=\"" + t.Titleid + "\" ");
                        if (requestQueryString["mid"].Equals(t.Mediaid, StringComparison.OrdinalIgnoreCase))
                            sb.Append("updatename=\"" + t.Displayname + "\" ");
                        else
                            sb.Append("updatename=\"MID:" + t.Mediaid + " " + t.Displayname + "\" ");
                        sb.Append("filename=\"" + t.Filename + "\" ");
                        sb.Append("mediaid=\"" + t.Mediaid + "\" ");
                        sb.Append("link=\"" + t.Url + "\" ");
                        sb.Append("hash=\"" + t.Tuhash.ToLower() + "\" ");
                        sb.Append("release=\"\" ");
                        sb.Append("version=\"" + t.Version + "\" ");
                        sb.Append("filesize=\"" + t.Filesize + "\"");
                        sb.AppendLine("/>");
                    }
                    sb.AppendLine("</updates>");
                }
                else if (phpScript.Equals("q.php", StringComparison.OrdinalIgnoreCase))
                {
                    //TODO: Implement this
                }
                else if (phpScript.Equals("cover.php", StringComparison.OrdinalIgnoreCase))
                {
                    //TODO: Implement this
                }
            }
            catch (Exception ex)
            {
                AppendLine("ERROR:");
                AppendLine(ex.ToString());
            }
            return null;
        }

        public void AppendLine(string value)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLine), value);
                return;
            }
            richTextBox1.AppendText(value + Environment.NewLine);
        }
    }
}
