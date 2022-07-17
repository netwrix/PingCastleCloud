//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using PingCastleCloud.Data;
using PingCastleCloud.Template;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PingCastleCloud.Report
{
    public abstract class ReportBase
    {

        protected StringBuilder sb = new StringBuilder();

        protected string ReportID;
        protected string PingCastleEnterpriseBaseUrl = "https://www.pingcastle.com/reports/";

        private List<string> CSSToAdd = new List<string> { TemplateManager.LoadBootstrapCss() };
        private List<string> JSToAdd = new List<string> { TemplateManager.LoadJqueryJs(), TemplateManager.LoadPopperJs(), TemplateManager.LoadBootstrapJs() };

        public string GenerateReportFile(string filename)
        {
            ReferenceJSAndCSS();

            var reportSB = new StringBuilder(TemplateManager.LoadResponsiveTemplate());

            Hook(reportSB);

            sb.Length = 0;

            sb.Length = 0;
            GenerateCspMeta();
            AddLine();
            Add("<title>");
            GenerateTitleInformation();
            Add("</title>");
            /*Add("<base href=\"");
            Add(PingCastleEnterpriseBaseUrl);
            if (!string.IsNullOrEmpty(ReportID))
            {
                AddEncoded(ReportID);
                Add("/");
            }
            Add("\">");*/
            Add(favicon);
            GenerateCss();
            reportSB = reportSB.Replace("<%=Header%>", sb.ToString());

            sb.Length = 0;
            GenerateBodyInformation();
            reportSB = reportSB.Replace("<%=Body%>", sb.ToString());

            sb.Length = 0;
            GenerateFooterInformation();
            GenerateJavascript();
            reportSB = reportSB.Replace("<%=Footer%>", sb.ToString());

            var html = reportSB.ToString();
            if (!String.IsNullOrEmpty(filename))
            {
                File.WriteAllText(filename, html);
            }
            return html;
        }

        private void GenerateJavascript()
        {
            foreach (var script in JSToAdd)
            {
                AddLine(@"<script type=""text/javascript"">");
                AddLine(script);
                AddLine("</script>");
            }
        }

        private void GenerateCss()
        {
            foreach (var css in CSSToAdd)
            {
                AddLine(@"<style type=""text/css"">");
                AddLine(css);
                AddLine("</style>");
            }
        }

        private void GenerateCspMeta()
        {
            Add(@"<meta http-equiv=""Content-Security-Policy"" content=""default-src 'self'; script-src ");
            foreach (var script in JSToAdd)
            {
                ComputeCSPHash(script);
            }
            Add(@" 'unsafe-inline'; style-src ");
            foreach (var css in CSSToAdd)
            {
                ComputeCSPHash(css);
            }
            Add(@" 'unsafe-inline'; object-src 'none'; base-uri https://www.pingcastle.com ; img-src data: https://www.pingcastle.com;""/>");
        }

        private void ComputeCSPHash(string css)
        {
            using (var sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes("\n" + css.Replace("\r", "") + "\n"));
                Add("'sha256-");
                Add(Convert.ToBase64String(bytes));
                Add("' ");
            }
        }

        protected void AddStyle(string style)
        {
            CSSToAdd.Add(style);
        }

        protected void AddScript(string script)
        {
            JSToAdd.Add(script);
        }


        protected virtual void Hook(StringBuilder sbHtml)
        {

        }

        protected void AddLink(string link)
        {
            sb.Append("<a href=\"");
            sb.Append(link);
            sb.Append("\">");
            sb.Append(link);
            sb.Append("</a>");
        }

        protected void AddListStart()
        {
            Add(@"<ul><li>");
        }

        protected void AddListContinue()
        {
            Add(@"</li><li>");
        }

        protected void AddListEnd()
        {
            Add(@"</li></ul>");
        }

        protected void AddLine(string text)
        {
            sb.AppendLine(text);
        }

        protected void AddLine()
        {
            sb.AppendLine();
        }

        protected void Add(int value)
        {
            sb.Append(value);
        }

        protected void Add(ulong value)
        {
            sb.Append(value);
        }

        protected void Add(bool value)
        {
            sb.Append(value);
        }

        protected void Add(string text)
        {
            sb.Append(text);
        }

        protected void DescribeBegin()
        {
            Add(@"<dl class='row'>");
        }
        protected void DescribeLabel(string label, string id = null)
        {
            Add(@"<dt class='col-sm-3'");
            if (!string.IsNullOrEmpty(id))
            {
                Add(" id='label_");
                Add(id);
                Add("'");
            }
            Add(">");
            AddEncoded(label);
            Add("</dt>");
        }
        protected void DescribeValue(string value, string id)
        {
            Add(@"<dd class='col-sm-9'");
            if (!string.IsNullOrEmpty(id))
            {
                Add(" id='label_");
                Add(id);
                Add("'");
            }
            Add(">");
            AddEncoded(value);
            Add("</dd>");
        }
        protected void DescribeEnd()
        {
            Add(@"</dl>");
        }

        public static string Encode(string stringToencode)
        {
            // could have use HttpUtility.HtmlEncode but not dotnet core compliant
            if (string.IsNullOrEmpty(stringToencode)) return stringToencode;

            string returnString = stringToencode;

            returnString = returnString.Replace(" & ", "&amp;");
            returnString = returnString.Replace("'", "&apos;");
            returnString = returnString.Replace("\"", "&quot;");
            returnString = returnString.Replace(">", "&gt;");
            returnString = returnString.Replace("<", "&lt;");

            return returnString;
        }

        private static bool NeedEscape(string src, int i)
        {
            char c = src[i];
            return c < 32 || c == '"' || c == '\\'
                // Broken lead surrogate
                || (c >= '\uD800' && c <= '\uDBFF' &&
                    (i == src.Length - 1 || src[i + 1] < '\uDC00' || src[i + 1] > '\uDFFF'))
                // Broken tail surrogate
                || (c >= '\uDC00' && c <= '\uDFFF' &&
                    (i == 0 || src[i - 1] < '\uD800' || src[i - 1] > '\uDBFF'))
                // To produce valid JavaScript
                || c == '\u2028' || c == '\u2029'
                // Escape "</" for <script> tags
                || (c == '/' && i > 0 && src[i - 1] == '<');
        }

        public static string EscapeJsonString(string src)
        {
            if (String.IsNullOrEmpty(src))
                return String.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            int start = 0;
            for (int i = 0; i < src.Length; i++)
                if (NeedEscape(src, i))
                {
                    sb.Append(src, start, i - start);
                    switch (src[i])
                    {
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '/': sb.Append("\\/"); break;
                        default:
                            sb.Append("\\u");
                            sb.Append(((int)src[i]).ToString("x04"));
                            break;
                    }
                    start = i + 1;
                }
            sb.Append(src, start, src.Length - start);
            return sb.ToString();
        }

        protected void AddEncoded(string text)
        {
            sb.Append(Encode(text));
        }

        protected void AddJsonEncoded(string text)
        {
            sb.Append(EscapeJsonString(text));
        }

        protected void Add(DateTime date)
        {
            sb.Append(date.ToString("u"));
        }

        protected void AddAnchor(string label)
        {
            Add(@"<a name=""");
            Add(label);
            Add(@"""></a>");
        }

        protected void AddParagraph(string content)
        {
            Add("<div class='row'><div class='col-lg-12'><p>");
            Add(content);
            Add("</p></div></div>");
        }

        protected void AddBeginTable(string ariaLabel, bool SimpleTable = false, string id = null)
        {
            Add(@"
		<div class=""row"">
			<div class=""col-md-12 table-responsive"">
				<table class=""table table-striped table-bordered");
            if (SimpleTable)
            {
                Add(" nopaging");
            }
            else
            {
                Add(@""" data-toggle=""table"" data-pagination=""true"" data-search=""true""");
            }
            if (!string.IsNullOrEmpty(id))
            {
                Add(" id=\"");
                AddEncoded(id);
                Add("\"");
            }
            if (!string.IsNullOrEmpty(ariaLabel))
            {
                Add(" aria-label=\"");
                AddEncoded(ariaLabel);
                Add("\"");
            }
            Add(@">
					<thead>
					<tr>");
        }

        protected void AddBeginTableData()
        {
            Add(@"</tr>
					</thead>
					<tbody>");
        }

        private bool footerMode = false;
        protected void AddEndTable(GenerateContentDelegate footer = null)
        {
            Add(@"</tbody>");
            if (footer != null)
            {
                Add("<tfoot>");
                AddBeginRow();
                footerMode = true;
                footer();
                footerMode = false;
                AddEndRow();
                Add("</tfoot>");
            }
            else
            {
                Add("<tfoot></tfoot>");
            }
            Add(@"
				</table>
			</div>
		</div>
");
        }

        protected void AddBeginRow()
        {
            Add(@"<tr>");
        }

        protected void AddEndRow()
        {
            Add(@"</tr>");
        }

        protected void AddBeginTooltip(bool wide = false, bool html = false)
        {
            Add(@"&nbsp;<i class=""info-mark d-print-none has-tooltip"" data-bs-placement=""bottom"" data1-bs-toggle=""tooltip""");
            if (wide)
            {
                Add(@" data-bs-template=""<div class='tooltip' role='tooltip'><div class='tooltip-arrow'></div><div class='tooltip-inner tooltip-wide'></div></div>"" ");
            }
            if (html)
            {
                Add(@" data-bs-html=""true"" ");
            }
            Add(@" title="""" data-bs-original-title=""");
        }

        protected void AddEndTooltip()
        {
            Add(@""">?</i>");
        }

        protected void AddHeaderText(string text, string tooltip, bool widetooltip, bool html = false)
        {
            AddHeaderText(text, tooltip, 0, 0, widetooltip, html);
        }

        protected void AddHeaderText(string text, 
            string tooltip = null, 
            int rowspan = 0, 
            int colspan = 0, 
            bool widetooltip = false, 
            bool html = false,
            string datafield = null,
            string dataformatter = null
            )
        {
            Add(@"<th  data-sortable=""true"" ");
            if (rowspan != 0)
            {
                Add(@" rowspan=""");
                Add(rowspan);
                Add(@"""");
            }
            if (colspan != 0)
            {
                Add(@" colspan=""");
                Add(colspan);
                Add(@"""");
            }
            if (!string.IsNullOrEmpty(datafield))
            {
                Add(" data-field='");
                Add(datafield);
                Add("'");
            }
            if (!string.IsNullOrEmpty(dataformatter))
            {
                Add(" data-formatter='");
                Add(dataformatter);
                Add("'");
            }
            Add(@">");
            AddEncoded(text);
            if (!string.IsNullOrEmpty(tooltip))
            {
                AddBeginTooltip(widetooltip, html);
                // important: not encoded to pass html formatting
                Add(tooltip);
                AddEndTooltip();
            }
            Add("</th>");
        }

        protected void AddCellBool(bool? value, bool highlight = false, bool IsGood = false)
        {
            if (value != null)
            {
                AddCellText(value.Value.ToString(), highlight, IsGood);
            }
            else
            {
                AddCellText();
            }
        }

        protected void AddCellText(string text = null, bool highlight = false, bool IsGood = false)
        {
            Add("<td class='text'>");
            if (footerMode)
                Add("<b>");
            if (highlight)
            {
                if (IsGood)
                    Add("<span class=\"ticked\">");
                else
                    Add("<span class=\"unticked\">");
            }
            AddEncoded(text);
            if (highlight)
                Add("</span>");
            if (footerMode)
                Add("</b>");
            Add(@"</td>");
        }

        protected void AddCellTextNoWrap(string text)
        {
            Add(@"<td class='text text-nowrap'>");
            if (footerMode)
                Add("<b>");
            Add(text);
            if (footerMode)
                Add("</b>");
            Add(@"</td>");
        }

        protected void AddCellDateNoWrap(DateTime text)
        {
            Add(@"<td class='text text-nowrap'>");
            if (footerMode)
                Add("<b>");
            Add(text);
            if (footerMode)
                Add("</b>");
            Add(@"</td>");
        }

        protected void AddCellNumScore(int num)
        {
            Add("<td class='num ");
            if (num == 100)
            {
                Add("score_100");
            }
            else if (num >= 75)
            {
                Add("score_over75");
            }
            else if (num >= 50)
            {
                Add("score_over50");
            }
            else if (num >= 25)
            {
                Add("score_over25");
            }
            else
            {
                Add("score_below25");
            }
            Add("'>");
            Add(num);
            Add(@"</td>");
        }

        protected void AddCellNum(int num, bool HideIfZero = false)
        {
            Add("<td class='num'>");
            if (footerMode)
                Add("<b>");
            if (!(HideIfZero && num == 0))
            {
                Add(num);
            }
            if (footerMode)
                Add("</b>");
            Add(@"</td>");
        }

        protected void AddCellDate(DateTime date)
        {
            Add("<td>");
            if (footerMode)
                Add("<b>");
            if (date == DateTime.MinValue)
                Add("Not set");
            else
                Add(date);
            if (footerMode)
                Add("</b>");
            Add(@"</td>");
        }

        protected enum ShowModalType
        {
            Normal,
            XL,
            FullScreen
        }

        protected void AddBeginModal(string id, string title, ShowModalType modalType = ShowModalType.Normal)
        {
            Add(@"
<!--TAB dependancy -->
<div class=""modal");
            if (modalType == ShowModalType.FullScreen)
            {
                Add(" modal-full-screen");
            }
            Add(@""" id=""");
            Add(id);
            Add(@""" tabindex=""-1"" role=""dialog"" aria-hidden=""true"">
	<div class=""modal-dialog");
            if (modalType == ShowModalType.XL)
            {
                Add(" modal-xl");
            }
            else if (modalType == ShowModalType.FullScreen)
            {
                Add(" modal-full-screen-dialog");
            }
            Add(@""" role=""dialog"">
		<div class=""modal-content");
            if (modalType == ShowModalType.FullScreen)
            {
                Add(" modal-full-screen-content");
            }
            Add(@""">
			<div class=""modal-header"">
				<h4 class=""modal-title"">");
            AddEncoded(title);
            Add(@"</h4>
					<button type=""button"" class=""btn-close"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
			</div>
			<div class=""modal-body");
            if (modalType == ShowModalType.FullScreen)
            {
                Add(" modal-full-screen-body");
            }
            Add(@""">
");
        }

        protected void AddEndModal(ShowModalType modalType = ShowModalType.Normal)
        {
            Add(@"
			</div>
			<div class=""modal-footer");
            if (modalType == ShowModalType.FullScreen)
            {
                Add(" modal-full-screen-footer");
            }
            Add(@""">
				<button type=""button"" class=""btn btn-secondary"" data-bs-dismiss=""modal"">Close</button>
			</div>
		</div>
	</div>
</div>");
        }

        protected delegate void GenerateContentDelegate();

        protected abstract void GenerateFooterInformation();

        protected abstract void GenerateTitleInformation();

        protected abstract void ReferenceJSAndCSS();

        protected abstract void GenerateBodyInformation();

        protected static string favicon = @"<link href=""data:image/x-icon;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAE10lEQVRYw71XfWwTdRhet4xBssFigMja61rafbK5bGFuiqh1ISaMP2QI++jaXtvUZGKIigUkUTTzmwUxIQtRYBZHr3e9rjYOY4zLlprMEDROJagZAzEgMYiTZJvr7zrmc0cHXekXxfWSJ9f2rvc8977P+76/X0YGDsKubiSuwh2ELy8SfPVZGek+QG4hTjkhDPUrPh8grOYJwhXlCV9tSZMAdnUDBEwRZ8HsTcivE0bxJQR1QEyJ0L8+e2EFcEWlhJFfuS3gFoIQcpG4VMcRmUbcd58wqJeJ/2FYfhGwDegG9gKrUhfAly4H0UgUAWGQTyAiw8Sl3kv6qpaDcDMwDswCQaCH5fqWpCRA8D2YQ1zK/vgCQnCprgv9j9aD8FCIfA4XAGVqAj7fIBrxg6QEMIop4i7ZADI7IIQJ8AP5qaeBobYnJ0D5O6LwsIvzrADhQWAUGHJxfEOA1TwE09LEU6EUPnsk8y4rQfO0VIqxyW+IHgh4KnVWs6HaaDI/85J9t+Z4r1Pl599ZSbjiRlz/Ec8QcD4rRdRdrCOcNlcYaE6mErS1qITxGAYkeKDzam9VsdlibWptaz8DCPp2488Wq20H8daUg/TCndGS/wN8IUWXK9YSvix2k4NaCvkdi0I+DvJ9gwcbKIOR3gXiqy2t+tk5QITvoqNODZLRONELSs9mqI/wohshJl8YbJPNF+BZk4sb/BGGOw/y1lefb1WD6DDIp8LJQwK+P9bZrIQ3hpLykFTOiq/x3F1o/QW3K+GkLhPqGgir3omS9OOmoQCrXW+hjTVtesMAyIKR5CJw7fL2DlsZxDuSEyAhAAE9SIk8I9TVZHC1Fmc9zrpL7k3yPx1rCvTthkYx39GI54Drk7TZCrNp3k2uiqSUvoJSXnbr7UG6FuQjoY4mdrcXOp59biXe7nQ88hBm4A0TBJgR3pkE5KOYO83EW50zL/8gfDuiq/105NjHFPL7aRICZlGSL2NOPCblNzr5jJhSvHmtwFfI7qgAEL4ZIWDkaI8DKTAyILiRSEC7weSYYCtLQHI5Cvk0DNoDbxXic/QSBGEF8A0wDVwBbKc+sS2maVpsOJ1IxQiIpmMJgFC//9CTqATFdxGO/xu/7YG5l8VtQvCAKKKAdfdtQkutHufqNchVN1S/9i+jrurc2aI0muitIDoBwkuRUYHAM512Won7D4DwN8kLDDWGkG8F+aK7XB2pWvCAb6W8ie3XiXUCo2Dxu/7c0ZpCq9n0AEJuB+mwWAHAWXx/an/X+3l+91v3T/Loii7Vi3B5vdD/eGYKqyP1nhhGQi4VPyAyXeLQ+XBfE2WizRtNtEWsoHWAFzgNvM5xHDqdPuXl2ZbEQ0l+DSE+Cedv7uOZfJAOhBk4AFjuZX24VjJPUouTQqfXfUIFwvMRVdR1L+tDcSidS66rUcNjfNsqELrDyCdg6m2pC3CX5krDIvFQgSeobuKpXIq1YBmI3wN6ARoClqQsQPDVZUFAbwLyvzCwdmOEL537H0pYxvHeLAj4XzYq++P0c2xeVE2o7+wF3KiobaE+ENHPqUFcq8Ucly3wVk2lQ5gn5/UAp/ywaNDgKXsatmpckThY/gitiq5J+Xap8tK3WeXLb+6UGMUvWC03EV9ddkY6D2y9cpDrN9CU1kHAgub7P7CsZhuj7eUMAAAAAElFTkSuQmCC"" rel=""icon"" type=""image/x-icon"" />";
        protected static string brandLogo = @"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";

        /*protected void Brand(ADHealthCheckingLicense license)
        {
            if (!string.IsNullOrEmpty(license.Edition))
            {
                try
                {
                    if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["BrandLogo"]))
                        brandLogo = ConfigurationManager.AppSettings["BrandLogo"];
                    if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["BrandCss"]))
                        AddStyle(ConfigurationManager.AppSettings["BrandCss"]);
                    if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["BrandJs"]))
                        AddScript(ConfigurationManager.AppSettings["BrandJs"]);
                }
                catch (Exception)
                {

                }
            }
        }*/

        protected void GenerateNavigation(string title, string domain, DateTime generationDate)
        {
            Add(@"
<nav class=""navbar py-0 navbar-expand-lg navbar-custom fixed-top border-bottom"">
	<div class=""container"">
		<a href=""#"" class=""navbar-brand"">
			<img src=""data:image/png;base64,");
            Add(brandLogo);
            Add(@""" />
		</a>
		<button class=""navbar-toggler"" type=""button"" data-bs-toggle=""collapse"" data-bs-target=""#navbarToggler"" aria-controls=""navbarToggler"" aria-expanded=""false"" aria-label=""Toggle navigation"">
			<span class=""navbar-toggler-icon""></span>
		</button>

		<div class=""collapse navbar-collapse"" id=""navbarToggler"">
			<ul class=""navbar-nav mr-auto"">
				<li class=""nav-item active"">
					<a class=""nav-link p-3"" href=""#"" role=""button"">
");
            if (String.IsNullOrEmpty(domain))
                Add(title);
            else
                Add(domain);
            Add(@"
					</a>
				</li>
				<li class=""nav-item"">
					<a class=""nav-link p-3"" href=""#"" role=""button"">
");
            Add(generationDate.ToString("yyyy-MM-dd"));
            Add(@"
					</a>
				</li>
				<li class=""nav-item"">
					<a class=""nav-link p-3"" role=""button"" href=""#modalAbout"" data-bs-toggle=""modal"">About</a>
				</li>
			</ul>
		</div>
	</div>
</nav>
");
        }

        protected string GenerateUniqueID(string input, long seed)
        {
            var d = new Dictionary<char, KeyValuePair<int, int>>()
            {
                { '.', new KeyValuePair<int, int>(0x3, 3 ) },
                { 'A', new KeyValuePair<int, int>(0xF, 4 ) },
                { 'L', new KeyValuePair<int, int>(0xE, 4 ) },
                { 'O', new KeyValuePair<int, int>(0xD, 4 ) },
                { 'C', new KeyValuePair<int, int>(0xB, 4 ) },
                { 'E', new KeyValuePair<int, int>(0xA, 4 ) },
                { 'R', new KeyValuePair<int, int>(0x5, 4 ) },
                { 'S', new KeyValuePair<int, int>(0x4, 4 ) },
                { 'T', new KeyValuePair<int, int>(0x2, 4 ) },
                { 'I', new KeyValuePair<int, int>(0x1, 4 ) },
                { 'N', new KeyValuePair<int, int>(0x0, 4 ) },
                { 'M', new KeyValuePair<int, int>(0x18, 5 ) },
                { 'D', new KeyValuePair<int, int>(0x12, 5 ) },
                { 'P', new KeyValuePair<int, int>(0x7, 5 ) },
                { 'F', new KeyValuePair<int, int>(0x6, 5 ) },
                { 'G', new KeyValuePair<int, int>(0x32, 6 ) },
                { 'U', new KeyValuePair<int, int>(0x23, 6 ) },
                { '-', new KeyValuePair<int, int>(0x21, 6 ) },
                { 'B', new KeyValuePair<int, int>(0x4F, 7 ) },
                { 'Y', new KeyValuePair<int, int>(0x4C, 7 ) },
                { 'H', new KeyValuePair<int, int>(0x45, 7 ) },
                { 'Z', new KeyValuePair<int, int>(0x44, 7 ) },
                { '0', new KeyValuePair<int, int>(0xCF, 8 ) },
                { '1', new KeyValuePair<int, int>(0xCE, 8 ) },
                { 'K', new KeyValuePair<int, int>(0xCC, 8 ) },
                { 'X', new KeyValuePair<int, int>(0x9D, 8 ) },
                { 'W', new KeyValuePair<int, int>(0x9C, 8 ) },
                { 'V', new KeyValuePair<int, int>(0x9B, 8 ) },
                { '6', new KeyValuePair<int, int>(0x83, 8 ) },
                { '2', new KeyValuePair<int, int>(0x82, 8 ) },
                { '3', new KeyValuePair<int, int>(0x80, 8 ) },
                { '5', new KeyValuePair<int, int>(0x19B, 9 ) },
                { '9', new KeyValuePair<int, int>(0x19A, 9 ) },
                { '7', new KeyValuePair<int, int>(0x135, 9 ) },
                { '8', new KeyValuePair<int, int>(0x103, 9 ) },
                { '4', new KeyValuePair<int, int>(0x269, 10 ) },
                { 'Q', new KeyValuePair<int, int>(0x268, 10 ) },
                { 'J', new KeyValuePair<int, int>(0x205, 10 ) },
                { '_', new KeyValuePair<int, int>(0x204, 10 ) },
            };
            string o = null;
            foreach (char c in input.ToUpperInvariant())
            {
                if (!d.ContainsKey(c))
                    continue;
                var t = d[c];
                var v = Convert.ToString(t.Key, 2);
                if (t.Value > v.Length)
                    o += new string('0', t.Value - v.Length) + v;
                else
                    o += v.Substring(v.Length - t.Value, t.Value);
            }
            o += Convert.ToString(0x80, 2);
            byte[] bytes = new byte[o.Length / 8 + 1];
            for (int i = 0; i < bytes.Length - 1; ++i)
            {
                bytes[i] = Convert.ToByte(o.Substring(8 * i, 8), 2);
            }
            bytes[bytes.Length - 1] = (byte)(seed % 256);
            return Convert.ToBase64String(bytes).Replace("=", "");
        }

        protected void GenerateAbout()
        {
            GenerateAbout(@"<p><strong>Generated by <a href=""https://www.pingcastle.com"">Ping Castle</a> all rights reserved</strong></p>
<p>Open source components:</p>
<ul>
<li><a href=""https://getbootstrap.com/"">Bootstrap</a> licensed under the <a href=""https://tldrlegal.com/license/mit-license"">MIT license</a></li>
<li><a href=""https://bootstrap-table.com/"">Bootstrap Table </a> licensed under the <a href=""https://tldrlegal.com/license/mit-license"">MIT license</a></li>
<li><a href=""https://popper.js.org/"">Popper.js</a> licensed under the <a href=""https://tldrlegal.com/license/mit-license"">MIT license</a></li>
<li><a href=""https://jquery.org"">JQuery</a> licensed under the <a href=""https://tldrlegal.com/license/mit-license"">MIT license</a></li>
</ul>");
        }
        protected void GenerateAbout(string aboutString)
        {
            Add(@"
<!-- Modal -->
<div class=""modal"" id=""modalAbout"" role=""dialog"">
    <div class=""modal-dialog modal-lg"">
        <!-- Modal content-->
        <div class=""modal-content"">
            <div class=""modal-header"">
                <h4 class=""modal-title"">About</h4>
                <button type=""button"" class=""btn-close"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
            </div>
            <div class=""modal-body"">
                <div class=""row"">
                     <div class=""col-lg-12"">
");
            Add(aboutString);
            Add(@"
                     </div>
                </div>
            </div>
            <div class=""modal-footer"">
                <button type=""button"" class=""btn btn-secondary"" data-bs-dismiss=""modal"">Close</button>
            </div>
        </div>

    </div>
</div>
");
        }

        protected virtual void GenerateSection(string title, GenerateContentDelegate generateContent)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9-]");
            string id = "section" + rgx.Replace(title, "");
            Add(@"
<!-- Section " + title + @" -->
<div id=""" + id + @""">
	<div class=""row"">
		<div class=""col-lg-12"">
			<div class=""starter-template"">
				<div class=""card mb-4"">
					<div class=""card-header"">
						<h1 class=""card-title""><a data-bs-toggle=""collapse"" aria-expanded=""true"" href=""#panel" + id + @""">" + title + @"</a></h1>
					</div>
					<div class=""card-body collapse show"" id=""panel" + id + @""">
");
            generateContent();
            Add(@"
					</div>
				</div>
			</div>
		</div>
	</div>
</div>
<div class=""pagebreak""> </div>
<!-- Section " + title + @" end -->
");
        }

        protected void GenerateSubSection(string title, string section = null)
        {
            Add(@"
		<!-- SubSection ");
            AddEncoded(title);
            Add(@" -->");
            if (!string.IsNullOrEmpty(section))
            {
                AddAnchor(section);
            }
            Add(@"
		<div class=""row""><div class=""col-lg-12 mt-2"">
			<h2>");
            AddEncoded(title);
            Add(@"</h2>
		</div></div>
        <!-- SubSection ");
            AddEncoded(title);
            Add(@" end -->");
        }

        protected void GenerateAccordion(string accordion, GenerateContentDelegate content)
        {
            Add(@"
		<!-- Accordion ");
            AddEncoded(accordion);
            Add(@" -->
<div id=""");
            Add(accordion);
            Add(@""">
<div class=""card"">
    ");
            content();
            Add(@"</div></div>
			<!-- Accordion ");
            Add(accordion);
            Add(@" end -->
");

        }

        protected void GenerateAccordionDetailForDetail(string id, string dataParent, string title, int itemCount, GenerateContentDelegate content)
        {
            GenerateAccordionDetail(id, dataParent, title,
                () =>
                {
                    Add(@"<i class=""float-end"">[");
                    Add((int)itemCount);
                    Add(@"]</i>");
                }, content);
        }

        protected void GenerateAccordionDetail(string id, string dataParent, string title, GenerateContentDelegate header, GenerateContentDelegate content)
        {
            Add(@"
    <div class=""card-header"" id=""heading");
            Add(id);
            Add("\">");
            AddAnchor("anchor" + id);
            Add(@"
      <span class=""card-title mb-0"">
        <button class=""btn btn-link"" data-bs-toggle=""collapse"" data-bs-target=""#");
            Add(id);
            Add(@""" aria-expanded=""false"" aria-controls=""");
            Add(id);
            Add(@""">
          ");
            Add(title);
            Add(@"
        </button>
      </span>
");
            if (header != null)
            {
                header();
            }
            Add(@"
    </div>

    <div id=""");
            Add(id);
            Add(@""" class=""collapse"" aria-labelledby=""heading");
            Add(id);
            Add(@""" data-parent=""#");
            Add(dataParent);
            Add(@""">
      <div class=""card-body"">
        ");
            content();
            Add(@"
    </div>
  </div>");
        }

        private static string GenerateId(string title)
        {
            return "section" + title.Replace(" ", "");
        }

        protected void GenerateTabHeader(string title, string selectedTab, bool defaultIfTabEmpty = false)
        {
            string id = GenerateId(title);
            bool isActive = (String.IsNullOrEmpty(selectedTab) ? defaultIfTabEmpty : selectedTab == id);
            Add(@"<li class=""nav-item""><a href=""#");
            Add(id);
            Add(@""" class=""nav-link ");
            if (isActive)
                Add(@"active");
            Add(@""" role=""tab"" data-bs-toggle=""tab""");
            Add(@" id=""bs-");
            Add(id);
            Add(@"""");
            Add(@">");
            Add(title);
            Add("</a></li>");
        }

        protected void GenerateSectionFluid(string title, GenerateContentDelegate generateContent, string selectedTab, bool defaultIfTabEmpty = false)
        {
            string id = GenerateId(title);
            bool isActive = (String.IsNullOrEmpty(selectedTab) ? defaultIfTabEmpty : selectedTab == id);
            Add(@"
<!-- Section ");
            Add(title);
            Add(@" -->
<div class=""tab-pane");
            if (isActive)
                Add(" active");
            Add(@""" id=""");
            Add(id);
            Add(@""">
<div class=""row""><div class=""col-lg-12"">
	<div class=""starter-template"">
		<div class=""row""><div class=""col-lg-12"">
			<h1>");
            Add(title);
            Add(@"</h1>
		</div></div>
");
            generateContent();
            Add(@"
			</div>
		</div>
	</div>
</div>
<!-- Section " + title + @" end -->
");
        }

        protected int OrderOS(string os1, string os2)
        {
            int ios1 = OSToInt(os1);
            int ios2 = OSToInt(os2);
            if (ios1 > 0 && ios2 > 0)
            {
                if (ios1 > ios2)
                    return 1;
                else if (ios1 < ios2)
                    return -1;
                return 0;
            }
            else if (ios1 > 0)
                return -1;
            else if (ios2 > 0)
                return 1;
            return String.Compare(os1, os2);
        }

        // this function is used to sort operating system based not on name but on its order
        // the value returned doesn't have a meaning at all. It is used for a comparison & sort
        protected int OSToInt(string os)
        {
            switch (os)
            {
                case "Windows XP":
                    return 1;
                case "Windows Vista":
                    return 2;
                case "Windows 7":
                    return 3;
                case "Windows 8":
                    return 4;
                case "Windows 10":
                    return 5;
                case "Windows 11":
                    return 6;
                case "Windows NT":
                    return 7;
                case "Windows 2000":
                    return 8;
                case "Windows 2003":
                    return 9;
                case "Windows 2008":
                    return 10;
                case "Windows 2012":
                    return 11;
                case "Windows 2016":
                    return 12;
                case "Windows 2019":
                    return 13;
                case "Windows 2022":
                    return 14;
                case "Windows Embedded":
                    return 15;
                case "OperatingSystem not set":
                    return 16;
            }
            return 100;
        }

        protected void GenerateGauge(int percentage)
        {
            Add(@"<svg viewBox=""0 0 400 230"" ><g transform=""translate(190, 190)""><path class=""arc chart-first"" d=""M-170,2.0818995585505004e-14A170,170 0 0,1 -120.20815280171311,-120.20815280171307L-86.26702730475881,-86.26702730475878A122,122 0 0,0 -122,1.494069094959771e-14Z""></path><path class=""arc chart-second"" d=""M-117.165698922674,-123.17548049819537A170,170 0 0,1 -3.122849337825751e-14,-170L-2.2411036424396564e-14,-122A122,122 0 0,0 -84.08361922686016,-88.39652129870491Z""></path><path class=""arc chart-third"" d=""M4.249557305501125,-169.94687776686945A170,170 0 0,1 120.20815280171306,-120.20815280171311L86.26702730475877,-86.26702730475881A122,122 0 0,0 3.0496823015949253,-121.96187698563573Z""></path><path class=""arc chart-quart"" d=""M123.17548049819526,-117.16569892267411A170,170 0 0,1 170,-1.9262832252003132e-13L122,-1.382391491026107e-13A122,122 0 0,0 88.39652129870484,-84.08361922686025Z""></path><circle class=""needle-center"" cx=""0"" cy=""0"" r=""18""></circle><path class=""needle"" d=""");
            GenerateChartNeedle(percentage);
            Add(@"""></path></g><text transform=""translate(3.8, 190)"" font-size=""15"">0</text><text transform=""translate(176.74418604651163, 12.666666666666666)"" font-size=""15"">50</text><text transform=""translate(368.93203883495147, 190)"" font-size=""15"">100</text></svg>");
        }

        protected void GenerateChartNeedle(int percentage)
        {
            double leftX, leftY, rightX, rightY, thetaRad, topX, topY;
            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";
            thetaRad = percentage * Math.PI / 100;
            topX = -144 * Math.Cos(thetaRad);
            topY = -144 * Math.Sin(thetaRad);
            leftX = -18 * Math.Cos(thetaRad - Math.PI / 2);
            leftY = -18 * Math.Sin(thetaRad - Math.PI / 2);
            rightX = -18 * Math.Cos(thetaRad + Math.PI / 2);
            rightY = -18 * Math.Sin(thetaRad + Math.PI / 2);
            Add("M ");
            Add(leftX.ToString(nfi));
            Add(" ");
            Add(leftY.ToString(nfi));
            Add(" L ");
            Add(topX.ToString(nfi));
            Add(" ");
            Add(topY.ToString(nfi));
            Add(" L ");
            Add(rightX.ToString(nfi));
            Add(" ");
            Add(rightY.ToString(nfi));
        }

        protected string NewLineToBR(string data)
        {
            if (String.IsNullOrEmpty(data))
                return data;
            return data.Replace("\r\n", "<br>\r\n");
        }

        public static string GetEnumDescription(Enum value)
        {
            // Get the Description attribute value for the enum value
            FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(
                    typeof(DescriptionAttribute), false);

            if (attributes.Length > 0)
            {
                return attributes[0].Description;
            }
            else
            {
                return value.ToString();
            }
        }
    }
}
