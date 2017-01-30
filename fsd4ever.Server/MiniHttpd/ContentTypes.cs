using System;
using System.Collections;
using Microsoft.Win32;

namespace fsd4ever.Server.MiniHttpd {
    /// <summary>
    ///     Provides a reference of common MIME content-types, and retrieves additional types from the Windows registry if
    ///     available.
    /// </summary>
    public static class ContentTypes {
        private static readonly Hashtable ExtensionTypes = InitContentTypes();

        private static Hashtable InitContentTypes() {
            var extensionTypes = new Hashtable(StringComparer.OrdinalIgnoreCase) {
                {".bin", "application/octet-stream"},
                {".uu", "application/octet-stream"},
                {".exe", "application/octet-stream"},
                {".ai", "application/postscript"},
                {".eps", "application/postscript"},
                {".ps", "application/postscript"},
                {".latex", "application/x-latex"},
                {".ram", "application/x-pn-realaudio"},
                {".swf", "application/x-shockwave-flash"},
                {".tar", "application/x-tar"},
                {".tcl", "application/x-tcl"},
                {".tex", "application/x-tex"},
                {".zip", "application/zip"},
                {".rar", "application/rar"},
                {".au", "audio/basic"},
                {".mpa", "audio/x-mpeg"},
                {".abs", "audio/x-mpeg"},
                {".mpega", "audio/x-mpeg"},
                {".mp2a", "audio/x-mpeg-2"},
                {".mpa2", "audio/x-mpeg-2"},
                {".wma", "audio/x-ms-wma"},
                {".wav", "audio/x-wav"},
                {".jpeg", "image/jpeg"},
                {".jpg", "image/jpeg"},
                {".jpe", "image/jpeg"},
                {".tiff", "image/tiff"},
                {".tif", "image/tiff"},
                {".bmp", "image/x-ms-bmp"},
                {".png", "image/x-png"},
                {".pnm", "image/x-portable-anymap"},
                {".pbm", "image/x-portable-bitmap"},
                {".pgm", "image/x-portable-graymap"},
                {".ppm", "image/x-portable-pixmap"},
                {".xbm", "image/x-xbitmap"},
                {".xpm", "image/x-xpixmap"},
                {".xwd", "image/x-xwindowdump"},
                {".css", "text/css"},
                {".html", "text/html"},
                {".htm", "text/html"},
                {".js", "text/javascript"},
                {".ls", "text/javascript"},
                {".mocha", "text/javascript"},
                {".txt", "text/plain"},
                {".bat", "text/plain"},
                {".c", "text/plain"},
                {".cpp", "text/plain"},
                {".c++", "text/plain"},
                {".cc", "text/plain"},
                {".h", "text/plain"},
                {".log", "text/plain"},
                {".cs", "text/plain"},
                {".vb", "text/plain"},
                {".mpeg", "video/mpeg"},
                {".mpg", "video/mpeg"},
                {".mpe", "video/mpeg"},
                {".mpv2", "video/mpeg-2"},
                {".mp2v", "video/mpeg-2"},
                {".qt", "video/quicktime"},
                {".mov", "video/quicktime"},
                {".asf", "video/x-ms-asf"},
                {".asx", "video/x-ms-asx"},
                {".wmv", "video/x-ms-wmv"},
                {".avi", "video/x-msvideo"}
            };

            #region Extensions from http://www.utoronto.ca/webdocs/HTMLdocs/Book/Book-3ed/appb/mimetype.html

            #endregion

            return extensionTypes;
        }

        /// <summary>
        ///     Get a MIME content-type from a file extension.
        /// </summary>
        /// <param name="extension">A file extension.</param>
        /// <returns>A MIME compatible file file-type.</returns>
        public static string GetExtensionType(string extension) {
            var ret = ExtensionTypes[extension] as string;
            if (ret == null)
                try {
                    ret = GetContentTypeFromRegistry(extension);
                }
                catch (MemberAccessException) { }
                catch (NotImplementedException) { }
                finally {
                    if (ret != null)
                        ExtensionTypes[extension] = ret;
                }
            return ret;
        }

        private static string GetContentTypeFromRegistry(string extension) {
            var classroot = Registry.ClassesRoot;
            var extkey = classroot.OpenSubKey(extension, writable: false);
            var type = extkey?.GetValue("Content Type");
            return type as string;
        }
    }
}
