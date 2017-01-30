using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using fsd4ever.Server.MiniHttpd;
using Newtonsoft.Json;

namespace fsd4ever.Server {
    public partial class Form1 : Form {
        private readonly BackgroundWorker bw = new BackgroundWorker();
        private readonly HttpServer http = new HttpServer();

        public Form1() { InitializeComponent(); }

        private void button1_Click(object sender, EventArgs e) {
            try {
                if (http.IsRunning)
                    http.Stop();
                http.LogRequests = true;
                http.LogConnections = true;
                http.Log = Console.Out;
                http.Port = (int)numericUpDown1.Value;
                http.Start();
                http.ValidRequestReceived += (o, args) => {
                                                 AppendLine(args.Request.Uri.OriginalString);
                                                 var phpScript = args.Request.Uri.Segments[1];
                                                 var ret = HandleRequest(phpScript, args.Request.Query);
                                                 args.Request.Response.ResponseCode = ret != null ? "200" : "404";
                                                 if (ret != null) {
                                                     args.Request.Response.BeginChunkedOutput();
                                                     using (
                                                         var writer =
                                                             new StreamWriter(args.Request.Response.ResponseContent,
                                                                              Encoding.GetEncoding("ISO-8859-1"))) {
                                                         writer.WriteLine(ret);
                                                     }
                                                 }
                                             };
                AppendLine($"Listening on port {http.Port}");
            }
            catch (Exception ex) {
                AppendLine("Error:");
                AppendLine(ex.ToString());
            }
        }

        private static T GetJson<T>(string url) {
            using (var wc = new WebClient()) {
                return JsonConvert.DeserializeObject<T>(wc.DownloadString(url));
            }
        }

        private string HandleRequest(string phpScript, NameValueCollection requestQueryString) {
            var sb = new StringBuilder();
            try {
                if (phpScript.Equals("tu.php", StringComparison.OrdinalIgnoreCase) ||
                    phpScript.Equals("tu_f.php", StringComparison.OrdinalIgnoreCase)) {
                    sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><updates>");
                    foreach (var t in GetJson<TuResponse[]>("http://xboxunity.net/api/tu/" + requestQueryString["tid"])) {
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
                else if (phpScript.Equals("q.php", StringComparison.OrdinalIgnoreCase)) {
                    //TODO: Implement this
                }
                else if (phpScript.Equals("cover.php", StringComparison.OrdinalIgnoreCase)) {
                    //TODO: Implement this
                }
                return sb.ToString();
            }
            catch (Exception ex) {
                AppendLine("ERROR:");
                AppendLine(ex.ToString());
                return null;
            }
        }

        public void AppendLine(string value) {
            if (InvokeRequired) {
                Invoke(new Action<string>(AppendLine), value);
                return;
            }
            richTextBox1.AppendText(value + Environment.NewLine);
        }
    }
}
