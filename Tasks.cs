//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using PingCastleCloud.Credentials;
using PingCastleCloud.Data;
using PingCastleCloud.Reports;
using PingCastleCloud.RESTServices;
using PingCastleCloud.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PingCastleCloud
{
    public class Tasks
    {
        public string FileOrDirectory;
        public bool InteractiveMode;
        public PingCastleLicense License;
        public string Identity;
        internal string privateKey;
        internal string tenantid;
        internal string clientid;
        internal string thumbprint;
        internal string p12file;
        internal string p12pass;
        internal bool p12passSet;
        internal bool usePrt;
        internal IAzureCredential currentCredential;
        internal string initForExportAsGuest;

        public bool AnalyzeTask()
        {
            return StartTask("Analyze",
                    () =>
                    {
                        if (currentCredential == null)
                        {
                            if (!string.IsNullOrEmpty(privateKey))
                            {
                                var key = File.ReadAllText(privateKey);
                                currentCredential = CertificateCredential.LoadFromKeyFile(clientid, tenantid, key, thumbprint);
                            }
                            if (!string.IsNullOrEmpty(p12file))
                            {
                                currentCredential = CertificateCredential.LoadFromP12(clientid, tenantid, p12file, p12pass);
                            }
                            if (usePrt)
                            {
                                currentCredential = new PRTCredential(tenantid);
                            }
                        }
                        if (currentCredential == null)
                        {
                            currentCredential = new UserCredential();
                        }
                        var analyze = new PingCastleCloud.Analyzer.Analyzer(currentCredential);
                        var report = analyze.Analyze().GetAwaiter().GetResult();

                        using (var sr = File.OpenWrite("pingcastlecloud_" + report.TenantName + ".json.gz"))
                        using (var gz = new GZipStream(sr, CompressionMode.Compress))
                        using (var sw = new StreamWriter(gz))
                        {
                            sw.Write(report.ToJsonString());
                        }

                        var reportGenerator = new ReportMain();
                        reportGenerator.GenerateReportFile(report, License, "pingcastlecloud_" + report.TenantName + ".html");
                    });         
        }

        public bool RegenerateHtmlTask()
        {
            return StartTask("Regenerate html report",
                    () =>
                    {
                        if (!File.Exists(FileOrDirectory))
                        {
                            WriteInRed("The file " + FileOrDirectory + " doesn't exist");
                            return;
                        }
                        var fi = new FileInfo(FileOrDirectory);
                        HealthCheckCloudData report;
                        using (var sr = File.OpenRead(FileOrDirectory))
                        {
                            if (fi.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                            {
                                using(var gz = new GZipStream(sr, CompressionMode.Decompress))
                                {
                                    report = HealthCheckCloudData.LoadFromStream(gz);
                                }
                            }
                            else
                            {
                                report = HealthCheckCloudData.LoadFromStream(sr);
                            }
                            var reportGenerator = new ReportMain();
                            reportGenerator.GenerateReportFile(report, License, "pingcastlecloud_" + report.TenantName + ".html");

                        }
                    }
                );
        }

        public bool ExportAsGuestTask()
        {
            return StartTask("Export as guest",
                    () =>
                    {
                        if (string.IsNullOrEmpty(currentCredential.TenantidToQuery))
                        {
                            WriteInRed("No tenant ID has been selected");
                            return;
                        }
                        var export = new Export.ExportAsGuest(currentCredential);
                        export.Export(initForExportAsGuest);
                    }
                );
        }


        // function used to encapsulate a task and to fail gracefully with an error message
        // return true is success; false in cas of failure
        delegate void TaskDelegate();
        private bool StartTask(string taskname, TaskDelegate taskdelegate)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Starting the task: " + taskname);
            Console.ResetColor();
            Trace.WriteLine("Starting " + taskname + " at:" + DateTime.Now);
            Stopwatch watch = new Stopwatch();
            watch.Start();
            try
            {
                taskdelegate();
            }
            catch (PingCastleCloudException ex)
            {
                WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                WriteInRed(ex.Message);
                if (ex.InnerException != null)
                {
                    Trace.WriteLine(ex.InnerException.Message);
                }
            }
            // better exception message
            catch (UnauthorizedAccessException ex)
            {
                WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                WriteInRed("Exception: " + ex.Message);
                Trace.WriteLine(ex.StackTrace);
            }
            catch (SmtpException ex)
            {
                WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                WriteInRed("Exception: " + ex.Message);
                WriteInRed("Error code: " + ex.StatusCode);
                Trace.WriteLine("Type:" + ex.GetType().ToString());
                if (ex.InnerException != null)
                {
                    WriteInRed(ex.InnerException.Message);
                }
                WriteInRed("Check the email configuration in the .config file or the network connectivity to solve the problem");
            }
            catch (ReflectionTypeLoadException ex)
            {
                WriteInRed("Exception: " + ex.Message);
                foreach (Type type in new List<Type>(ex.Types))
                {
                    WriteInRed("Was trying to load type: " + type.FullName);
                }
                DisplayException(taskname, ex);
                return false;
            }
            // default exception message
            catch (Exception ex)
            {
                // type EndpointNotFoundException is located in Service Model using dotnet 3.0. What if run on dotnet 2.0 ?
                if (ex.GetType().FullName == "System.ServiceModel.EndpointNotFoundException")
                {
                    WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                    WriteInRed("Exception: " + ex.Message);
                }
                // type DirectoryServicesCOMException not found in dotnet core
                else if (ex.GetType().FullName == "System.DirectoryServices.DirectoryServicesCOMException")
                {
                    WriteInRed("An exception occured while querying the Active Directory");
                    string ExtendedErrorMessage = (string)ex.GetType().GetProperty("ExtendedErrorMessage").GetValue(ex, null);
                    int ExtendedError = (int)ex.GetType().GetProperty("ExtendedError").GetValue(ex, null);
                    WriteInRed("Exception: " + ex.Message + "(" + ExtendedErrorMessage + ")");
                    if (ExtendedError == 234)
                    {
                        WriteInRed("This error occurs when the Active Directory server is under load");
                        WriteInRed("Suggestion: try again and if the error persists, check for AD corruption");
                        WriteInRed("Try our corruption scanner to identify the object or check for AD integrity using ntdsutil.exe");
                    }
                }
                else if (ex.GetType().FullName == "System.DirectoryServices.ActiveDirectory.ActiveDirectoryServerDownException")
                {
                    WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                    WriteInRed("Active Directory not Found: " + ex.Message);
                }
                else if (ex.GetType().FullName == "System.DirectoryServices.ActiveDirectory.ActiveDirectoryObjectNotFoundException")
                {
                    WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                    WriteInRed("Active Directory Not Found: " + ex.Message);
                }
                else
                {
                    DisplayException(taskname, ex);
                    return false;
                }
            }
            watch.Stop();
            Trace.WriteLine("Stoping " + taskname + " at: " + DateTime.Now);
            Trace.WriteLine("The task " + taskname + " took " + watch.Elapsed);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Task " + taskname + " completed");
            Console.ResetColor();
            return true;
        }

        public static void DisplayException(string taskname, Exception ex)
        {
            if (!String.IsNullOrEmpty(taskname))
            {
                WriteInRed("[" + DateTime.Now.ToLongTimeString() + "] An exception occured when doing the task: " + taskname);
                WriteInRed("Note: you can run the program with the switch --log to get more detail");
                Trace.WriteLine("An exception occured when doing the task: " + taskname);
            }
            WriteInRed("Exception: " + ex.Message);
            Trace.WriteLine("Type:" + ex.GetType().ToString());
            var fnfe = ex as FileNotFoundException;
            if (fnfe != null)
            {
                WriteInRed("file:" + fnfe.FileName);
            }
            if (ex.GetType().ToString() == "Novell.Directory.Ldap.LdapException")
            {
                string novelMessage = null;
                int novelResultCode;
                novelResultCode = (int)ex.GetType().GetProperty("ResultCode").GetValue(ex, null);
                novelMessage = ex.GetType().GetProperty("LdapErrorMessage").GetValue(ex, null) as string;
                WriteInRed("message: " + novelMessage);
                WriteInRed("ResultCode: " + novelResultCode);
            }
            WriteInDarkRed(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Trace.WriteLine("innerexception: ");
                DisplayException(null, ex.InnerException);
            }
        }


        private static void WriteInRed(string data)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(data);
            Trace.WriteLine("[Red]" + data);
            Console.ResetColor();
        }

        private static void WriteInDarkRed(string data)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(data);
            Trace.WriteLine("[DarkRed]" + data);
            Console.ResetColor();
        }

        private void DisplayAdvancement(string data)
        {
            string value = "[" + DateTime.Now.ToLongTimeString() + "] " + data;
            Console.WriteLine(value);
            Trace.WriteLine(value);
        }

    }
}
