//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PingCastleCloud.Template
{
    public class TemplateManager
    {
        private static string LoadTemplate(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            Stream stream = null;
            GZipStream gzip = null;
            string html = null;
            StreamReader reader = null;
            try
            {
                stream = assembly.GetManifestResourceStream(resourceName);
                gzip = new GZipStream(stream, CompressionMode.Decompress);
                reader = new StreamReader(gzip);
                html = reader.ReadToEnd();
            }
            catch (Exception)
            {
                Trace.WriteLine("Unable to load " + resourceName);
                throw;
            }
            finally
            {
                if (reader != null)
                    reader.Dispose();
            }
            return html;
        }

        public static string LoadResponsiveTemplate()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".responsivetemplate.html.gz");
        }

        public static string LoadBootstrapCss()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".bootstrap.min.css.gz");
        }

        public static string LoadBootstrapJs()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".bootstrap.min.js.gz");
        }

        public static string LoadBootstrapTableCss()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".bootstrap-table.min.css.gz");
        }

        public static string LoadBootstrapTableJs()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".bootstrap-table.min.js.gz");
        }

        public static string LoadPopperJs()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".popper.min.js.gz");
        }

        public static string LoadJqueryJs()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".jquery.min.js.gz");
        }

        public static string LoadReportBaseCss()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".ReportBase.css.gz");
        }

        public static string LoadReportBaseJs()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".ReportBase.js.gz");
        }

        public static string LoadReportMainJs()
        {
            return LoadTemplate(typeof(TemplateManager).Namespace + ".ReportMain.js.gz");
        }
    }
}
