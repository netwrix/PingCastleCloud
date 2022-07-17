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
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Xml;
using PingCastleCloud.RESTServices;
using PingCastleCloud.RESTServices.Azure;
using PingCastleCloud.Data;
using PingCastleCloud.Reports;
using System.Threading;
using System.Security.Cryptography;
using System.Reflection;
using System.ComponentModel;
using PingCastleCloud.Credentials;
using PingCastleCloud.Tokens;
using PingCastleCloud.Common;

namespace PingCastleCloud
{
    [LicenseProvider(typeof(PingCastleCloud.PingCastleLicenseProvider))]
    class Program : IPingCastleLicenseInfo
    {
        Tasks tasks = new Tasks();

        enum PossibleTasks
        {
            HealthCheck,
            Regen,
            ExportAsGuest,
        }

        Dictionary<PossibleTasks, Func<bool>> actions;
        List<PossibleTasks> requestedActions = new List<PossibleTasks>();

        public static void Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                Trace.WriteLine("Running on dotnet:" + Environment.Version);
                Program program = new Program();
                program.Run(args);
                // dispose the http logger
                Common.HttpClientHelper.EnableLoging(null);
                if (program.tasks.InteractiveMode)
                {
                    Console.WriteLine("=============================================================================");
                    Console.WriteLine("Program launched in interactive mode - press any key to terminate the program");
                    Console.WriteLine("=============================================================================");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                // dispose the http logger
                Common.HttpClientHelper.EnableLoging(null);
                Tasks.DisplayException("main program", ex);
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Tasks.DisplayException("application domain", e.ExceptionObject as Exception);
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // hook required for "System.Runtime.Serialization.ContractNamespaceAttribute"
            var name = new AssemblyName(args.Name);
            Trace.WriteLine("Needing assembly " + name + " unknown (" + args.Name + ")");
            return null;
        }

        private void Run(string[] args)
        {
            PingCastleLicense license = null;
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Trace.WriteLine("PingCastle version " + version.ToString(4));
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--debug-license", StringComparison.InvariantCultureIgnoreCase))
                {
                    EnableLogConsole();
                }
                else if (args[i].Equals("--license", StringComparison.InvariantCultureIgnoreCase) && i + 1 < args.Length)
                {
                    _serialNumber = args[++i];
                }
                else if (args[i].Equals("--out", StringComparison.InvariantCultureIgnoreCase) && i + 1 < args.Length)
                {
                    string filename = args[++i];
                    var fi = new FileInfo(filename);
                    if (!Directory.Exists(fi.DirectoryName))
                    {
                        Directory.CreateDirectory(fi.DirectoryName);
                    }
                    Stream stream = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.Read);
                    StreamWriter writer = new StreamWriter(stream);
                    writer.AutoFlush = true;
                    Console.SetOut(writer);
                }
            }
            Trace.WriteLine("Starting the license checking");
            try
            {
                license = LicenseManager.Validate(typeof(Program), this) as PingCastleLicense;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("the license check failed - please check that the .config file is in the same directory");
                WriteInRed(ex.Message);
                if (args.Length == 0)
                {
                    Console.WriteLine("=============================================================================");
                    Console.WriteLine("Program launched in interactive mode - press any key to terminate the program");
                    Console.WriteLine("=============================================================================");
                    Console.ReadKey();
                }
                return;
            }
            Trace.WriteLine("License checked");
            if (license.EndTime < DateTime.Now)
            {
                WriteInRed("The program is unsupported since: " + license.EndTime.ToString("u") + ")");
                if (args.Length == 0)
                {
                    Console.WriteLine("=============================================================================");
                    Console.WriteLine("Program launched in interactive mode - press any key to terminate the program");
                    Console.WriteLine("=============================================================================");
                    Console.ReadKey();
                }
                return;
            }
            if (license.EndTime < DateTime.MaxValue)
            {
                Console.WriteLine();
            }
            tasks.License = license;

            actions = new Dictionary<PossibleTasks, Func<bool>>
            {
                {PossibleTasks.HealthCheck, tasks.AnalyzeTask},
                {PossibleTasks.Regen, tasks.RegenerateHtmlTask},
                {PossibleTasks.ExportAsGuest, tasks.ExportAsGuestTask }
            };

            ConsoleMenu.Header = @"  \==--O___      PingCastle Cloud (Version " + version.ToString(4) + @"     " + ConsoleMenu.GetBuildDateTime(Assembly.GetExecutingAssembly()) + @")
   \  / \  ¨¨>   Get Active Directory Security at 80% in 20% of the time
    \/   \ ,’    " + (license.EndTime < DateTime.MaxValue ? "End of support: " + license.EndTime.ToShortDateString() : "") + @"
     O¨---O                                                     
      \ ,'       Vincent LE TOUX (contact@pingcastle.com)
       v         twitter: @mysmartlogon       https://www.pingcastle.com";
            if (!ParseCommandLine(args))
                return;
            // Trace to file or console may be enabled here
            Trace.WriteLine("[New run]" + DateTime.Now.ToString("u"));
            Trace.WriteLine("PingCastle version " + version.ToString(4));
            Trace.WriteLine("Running on dotnet:" + Environment.Version);
            if (!String.IsNullOrEmpty(license.CustomerNotice))
            {
                Console.WriteLine(license.CustomerNotice);
            }
            if (!CheckCertificate())
                return;
            foreach (var a in requestedActions)
            {
                var r = actions[a].Invoke();
                if (!r) return;
            }
        }

        private bool CheckCertificate()
        {
            if (!string.IsNullOrEmpty(tasks.thumbprint) || !string.IsNullOrEmpty(tasks.privateKey))
            {
                if (string.IsNullOrEmpty(tasks.thumbprint))
                {
                    WriteInRed("--thumbprint must be completed when --private-key is set");
                    return false;
                }
                if (string.IsNullOrEmpty(tasks.privateKey))
                {
                    WriteInRed("--private-key must be completed when --thumbprint is set");
                    return false;
                }
                if (string.IsNullOrEmpty(tasks.clientid))
                {
                    WriteInRed("--clientid must be set when certificate authentication is configured");
                    return false;
                }
                if (string.IsNullOrEmpty(tasks.tenantid))
                {
                    WriteInRed("--tenantid must be set when certificate authentication is configured");
                    return false;
                }
                if (!string.IsNullOrEmpty(tasks.p12file))
                {
                    WriteInRed("--p12-file cannot be combined with --private-key");
                    return false;
                }
            }
            return true;
        }

        const string basicEditionLicense = "PC2H4sIAAAAAAAEAO29B2AcSZYlJi9tynt/SvVK1+B0oQiAYBMk2JBAEOzBiM3mkuwdaUcjKasqgcplVmVdZhZAzO2dvPfee++999577733ujudTif33/8/XGZkAWz2zkrayZ4hgKrIHz9+fB8/In7NX+PX+DV+A/r/r/F7/nf/y/E/+m//mr82/drS/5/9GvWvkdN/6a9x+mvMfo2CPit+jerXWNLf1a9xTv++pL+Xv8bFr3Hya2S/RkPfltx279cY/xq7v8YO/btDf23T/19Q+5Z+ntPPmn5O6eeC/svprynByOjN9NdYE4z81wAaf9Cv8Wv8Gr/G7/Sn/jb/yh/67+R/SvL3/a9/4R/9F/9j//m/+uf+bqtf8J/8wskf80v+gu/+1v/e3/3yfzj9hf/ZF//HL/v9/vzf/S/O/ozf/2/9q17/y3/Qt36/v+Uf+11+q8e/8f/RFn/E3/vb/cPlT/1P//Cf9OPlP3H4Z/57v8bf/M9s/aK/47f6w7df/BG/8t/8U/6sp//Cf/pbfvVPfHr4t/yVP/hXfvvZr//X/zWXf0b7r/8rf9K/+Auyv+uf+XP+mfkf+ff+c7/L/wNcbrwUFAEAAA==";
        string _serialNumber;
        public string GetSerialNumber()
        {
            if (string.IsNullOrEmpty(_serialNumber))
            {
                // try to load it from the configuration file
                try
                {
                    _serialNumber = PingCastleLicenseSettings.Settings.License;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Exception when getting the license string");
                    Trace.WriteLine(ex.Message);
                    Trace.WriteLine(ex.StackTrace);
                    if (ex.InnerException != null)
                    {
                        Trace.WriteLine(ex.InnerException.Message);
                        Trace.WriteLine(ex.InnerException.StackTrace);
                    }

                }
                if (!String.IsNullOrEmpty(_serialNumber))
                {
                    try
                    {
                        var license = new PingCastleLicense(_serialNumber);
                        return _serialNumber;
                    }
                    catch (Exception ex)
                    {
                        _serialNumber = null;
                        Trace.WriteLine("Exception when verifying the external license");
                        Trace.WriteLine(ex.Message);
                        Trace.WriteLine(ex.StackTrace);
                        if (ex.InnerException != null)
                        {
                            Trace.WriteLine(ex.InnerException.Message);
                            Trace.WriteLine(ex.InnerException.StackTrace);
                        }
                    }

                }
            }
            // fault back to the default license:
            _serialNumber = basicEditionLicense;
            try
            {
                var license = new PingCastleLicense(_serialNumber);
            }
            catch (Exception)
            {
                throw new PingCastleCloudException("Unable to load the license from the .config file and the license embedded in PingCastle is not valid. Check that all files have been copied in the same directory and that you have a valid license");
            }
            return _serialNumber;
        }

        private void WriteInRed(string data)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(data);
            Trace.WriteLine("[Red]" + data);
            Console.ResetColor();
        }

        private void EnableLogFile()
        {
            Trace.AutoFlush = true;
            TextWriterTraceListener listener = new TextWriterTraceListener("trace.log");
            Trace.Listeners.Add(listener);
            Common.HttpClientHelper.EnableLoging(new Logs.SazGenerator());
        }

        private void EnableLogConsole()
        {
            Trace.AutoFlush = true;
            TextWriterTraceListener listener = new TextWriterTraceListener(Console.Out);
            Trace.Listeners.Add(listener);
        }

        public enum DisplayState
        {
            Exit,
            MainMenu,
            Run,
            AskForFile,
            AvancedMenu,
            HealthcheckMenu,
            Next,
            ExportAsGuestMenu,
        }

        // interactive interface
        private bool RunInteractiveMode()
        {
            tasks.InteractiveMode = true;
            Stack<DisplayState> states = new Stack<DisplayState>();
            var state = DisplayState.MainMenu;

            states.Push(state);
            while (states.Count > 0 && states.Peek() != DisplayState.Run)
            {
                switch (state)
                {
                    case DisplayState.MainMenu:
                        state = DisplayMainMenu();
                        break;
                    case DisplayState.HealthcheckMenu:
                        state = DisplayHealthcheckMenu();
                        break;
                    case DisplayState.AvancedMenu:
                        state = DisplayAdvancedMenu();
                        break;
                    case DisplayState.AskForFile:
                        state = DisplayAskForFile();
                        break;
                    case DisplayState.ExportAsGuestMenu:
                        state = DisplayExportAsGuestMenu();
                        break;
                    default:
                        // defensive programming
                        if (state != DisplayState.Exit)
                        {
                            Console.WriteLine("No implementation of state " + state);
                            state = DisplayState.Exit;
                        }
                        break;
                }
                if (state == DisplayState.Exit)
                {
                    states.Pop();
                    if (states.Count > 0)
                        state = states.Peek();
                }
                else
                {
                    states.Push(state);
                }
            }
            return (states.Count > 0);
        }

        private DisplayState DisplayHealthcheckMenu()
        {
            var r = DisplayAskCredential();
            if (r != DisplayState.Next)
                return r;

            tasks.currentCredential = null;
            if (tasks.usePrt)
            {
                tasks.currentCredential = new PRTCredential();
            }
            else
            {
                tasks.currentCredential = new UserCredential();
            }

            //r = DisplayAskTenant();
            //if (r != DisplayState.Next)
            //    return r;

            requestedActions.Add(PossibleTasks.HealthCheck);
            return DisplayState.Run;
        }

        private DisplayState DisplayExportAsGuestMenu()
        {
            var r = DisplayAskCredential();
            if (r != DisplayState.Next)
                return r;

            tasks.currentCredential = null;
            if (tasks.usePrt)
            {
                tasks.currentCredential = new PRTCredential();
            }
            else
            {
                tasks.currentCredential = new UserCredential();
            }


            r = DisplayAskTenant();
            if (r != DisplayState.Next)
                return r;

            do
            {
                ConsoleMenu.Title = "Select the seed";
                ConsoleMenu.Information = @"To start the export, the program need to have a first user. It can be its objectId or its UPN (firstname.lastname@domain.com). The program accept many values if there are separted by a comma.";
                tasks.initForExportAsGuest = ConsoleMenu.AskForString();

                // error message in case the query is not complete
                ConsoleMenu.Notice = "The seed cannot be empty";
            } while (String.IsNullOrEmpty(tasks.initForExportAsGuest));

            requestedActions.Add(PossibleTasks.ExportAsGuest);
            return DisplayState.Run;
        }

        private DisplayState DisplayAskCredential()
        {
            List<ConsoleMenuItem> choices = new List<ConsoleMenuItem>() {
                new ConsoleMenuItem("askcredential","Ask credentials", "The identity may be asked multiple times during the healthcheck."),
            };

            var tokens = TokenFactory.GetRegisteredPRTIdentities();
            if (tokens.Count > 0)
            {
                choices.Insert(0, new ConsoleMenuItem("useprt", "Use SSO with the PRT stored on this computer", "Use the Primary Refresh Token available on this computer to connect automatically without credential prompting."));
            }

            ConsoleMenu.Title = "Which identity do you want to use?";
            ConsoleMenu.Information = "The program will use the choosen identity to perform the operation on the Azure Tenant.";
            int choice = ConsoleMenu.SelectMenu(choices);
            if (choice == 0)
                return DisplayState.Exit;

            tasks.Identity = null;

            string whattodo = choices[choice - 1].Choice;
            switch (whattodo)
            {
                default:
                    break;
                case "askcredential":
                    tasks.usePrt = false;
                    break;
                case "useprt":
                    tasks.usePrt = true;
                    break;

            }

            return DisplayState.Next;
        }

        private DisplayState DisplayAskTenant()
        {
            HttpClientHelper.LogComment = "DisplayAskTenant";
            ManagementApi.TenantListResponse p;
            try
            {
                var graph = new ManagementApi(tasks.currentCredential);
                p = graph.ListTenants();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return DisplayState.Exit;
            }
            HttpClientHelper.LogComment = null;


            List<ConsoleMenuItem> choices = new List<ConsoleMenuItem>();
            foreach (var t in p.responses)
            {
                foreach (var t2 in t.content.value)
                {
                    choices.Add(new ConsoleMenuItem(t2.tenantId, t2.displayName + " (" + t2.countryCode + ")"));
                }
            }

            ConsoleMenu.Title = "Which tenant do you want to use?";
            ConsoleMenu.Information = "The program will use the choosen tenant to perform the operation on the Azure Tenant.";
            int choice = ConsoleMenu.SelectMenu(choices);
            if (choice == 0)
                return DisplayState.Exit;

            string whattodo = choices[choice - 1].Choice;
            tasks.currentCredential.TenantidToQuery = whattodo;

            return DisplayState.Next;
        }

        DisplayState DisplayMainMenu()
        {

            List<ConsoleMenuItem> choices = new List<ConsoleMenuItem>() {
                new ConsoleMenuItem("healthcheck","Score the risk of a domain", "This is the main functionnality of PingCastle. In a matter of minutes, it produces a report which will give you an overview of your Active Directory security. This report can be generated on other domains by using the existing trust links."),
                new ConsoleMenuItem("exportasguest","Export users and group as a Guest", "After the connection, try to export user information as a GUEST."),
                new ConsoleMenuItem("advanced","Open the advanced menu", "This is the place you want to configure PingCastle without playing with command line switches."),
            };

            ConsoleMenu.Title = "What do you want to do?";
            ConsoleMenu.Information = "Using interactive mode.\r\nDo not forget that there are other command line switches like --help that you can use";
            int choice = ConsoleMenu.SelectMenu(choices);
            if (choice == 0)
                return DisplayState.Exit;

            string whattodo = choices[choice - 1].Choice;
            switch (whattodo)
            {
                default:
                case "healthcheck":
                    return DisplayState.HealthcheckMenu;
                case "exportasguest":
                    return DisplayState.ExportAsGuestMenu;
                case "advanced":
                    return DisplayState.AvancedMenu;
            }
        }

        DisplayState DisplayAdvancedMenu()
        {
            List<ConsoleMenuItem> choices = new List<ConsoleMenuItem>() {
                new ConsoleMenuItem("regenerate","Regenerate the html report based on the xml report"),
                new ConsoleMenuItem("log","Enable logging (log is " + (Trace.Listeners.Count > 1 ? "enabled":"disabled") + ")"),
            };

            ConsoleMenu.Title = "What do you want to do?";
            int choice = ConsoleMenu.SelectMenu(choices);
            if (choice == 0)
                return DisplayState.Exit;

            string whattodo = choices[choice - 1].Choice;
            switch (whattodo)
            {
                default:
                case "regenerate":
                    requestedActions.Add(PossibleTasks.Regen);
                    return DisplayState.AskForFile;
                case "log":
                    if (Trace.Listeners.Count <= 1)
                        EnableLogFile();
                    return DisplayState.Exit;
            }
        }

        DisplayState DisplayAskForFile()
        {
            string file = null;
            while (String.IsNullOrEmpty(file) || !File.Exists(file))
            {
                ConsoleMenu.Title = "Select an existing file";
                ConsoleMenu.Information = "Please specify the file to open.";
                file = ConsoleMenu.AskForString();
                ConsoleMenu.Notice = "The file " + file + " was not found";
            }
            tasks.FileOrDirectory = file;
            return DisplayState.Run;
        }

        private bool ParseCommandLine(string[] args)
        {
            bool delayedInteractiveMode = false;
            if (args.Length == 0)
            {
                if (!RunInteractiveMode())
                    return false;
            }
            else
            {
                Trace.WriteLine("Before parsing arguments");
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--clientid":
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --clientid is mandatory");
                                return false;
                            }
                            tasks.clientid = args[++i];
                            break;
                        case "--debug-license":
                            break;
                        case "--regen-report":
                            requestedActions.Add(PossibleTasks.Regen);
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --regen-report is mandatory");
                                return false;
                            }
                            tasks.FileOrDirectory = args[++i];
                            break;
                        case "--healthcheck":
                            requestedActions.Add(PossibleTasks.HealthCheck);
                            break;
                        case "--help":
                            DisplayHelp();
                            return false;
                        case "--interactive":
                            delayedInteractiveMode = true;
                            break;
                        case "--license":
                            i++;
                            break;
                        case "--log":
                            EnableLogFile();
                            break;
                        case "--log-console":
                            EnableLogConsole();
                            break;
                        case "--p12-file":
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --p12-file is mandatory");
                                return false;
                            }
                            tasks.p12file = args[++i];
                            break;
                        case "--p12-pass":
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --p12-pass is mandatory");
                                return false;
                            }
                            tasks.p12passSet = true;
                            tasks.p12pass = args[++i];
                            break;
                        case "--private-key":
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --private-key is mandatory");
                                return false;
                            }
                            tasks.privateKey = args[++i];
                            break;
                        case "--tenantid":
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --tenantid is mandatory");
                                return false;
                            }
                            tasks.tenantid = args[++i];
                            break;
                        case "--thumbprint":
                            if (i + 1 >= args.Length)
                            {
                                WriteInRed("argument for --thumbprint is mandatory");
                                return false;
                            }
                            tasks.thumbprint = args[++i];
                            break;
                        case "--use-prt":
                            tasks.usePrt = true;
                            break;
                        default:
                            WriteInRed("unknow argument: " + args[i]);
                            DisplayHelp();
                            return false;
                    }
                }
                Trace.WriteLine("After parsing arguments");
            }
            if (requestedActions.Count == 0 && !delayedInteractiveMode)
            {
                WriteInRed("You must choose an action to perform.");
                DisplayHelp();
                return false;
            }
            Trace.WriteLine("Things to do OK");
            if (delayedInteractiveMode)
            {
                RunInteractiveMode();
            }
            return true;
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("switch:");
            Console.WriteLine("  --help              : display this message");
            Console.WriteLine("  --interactive       : force the interactive mode");
            Console.WriteLine("  --log               : generate a log file");
            Console.WriteLine("  --log-console       : add log to the console");
            Console.WriteLine("");
            Console.WriteLine("General setting:");
            Console.WriteLine("     --tenantid xx: specify the tenant id to use. Requiered for cert auth");

            Console.WriteLine("Authentication");
            Console.WriteLine("  --use-prt          : use prt to log on");
            Console.WriteLine("");
            Console.WriteLine("Certificate authentication");
            Console.WriteLine("   --clientid xxx : specify the client id to which the certificate is associated");
            Console.WriteLine("   With private key");
            Console.WriteLine("     --thumbprint xxx : specify the thumprint of the certificate configured");
            Console.WriteLine("     --private-key xxx : specify the key file to use (PKCS8)");
            Console.WriteLine("");
            Console.WriteLine("   With P12");
            Console.WriteLine("     --p12-file xxx : specify the P12 file to use");
            Console.WriteLine("     --p12-pass xxx : specify the password to use");

            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");

            Console.WriteLine("Common options when connecting to the AzureAD");

        }

        static void Main3(string[] args)
        {
            IntPtr ppJoinInfo;
            var retValue = NetGetAadJoinInformation(null, out ppJoinInfo);
            string domain;
            if (retValue == 0)
            {
                var joinInfo = (DSREG_JOIN_INFO)Marshal.PtrToStructure(ppJoinInfo, typeof(DSREG_JOIN_INFO));
                domain = new MailAddress(joinInfo.JoinUserEmail).Host;


                NetFreeAadJoinInformation(ppJoinInfo);
            }



        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int NetGetAadJoinInformation(string pcszTenantId, out IntPtr ppJoinInfo);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern void NetFreeAadJoinInformation(IntPtr ppJoinInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DSREG_JOIN_INFO
        {
            public int joinType;
            public IntPtr pJoinCertificate;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string DeviceId;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string IdpDomain;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string TenantId;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string JoinUserEmail;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string TenantDisplayName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string MdmEnrollmentUrl;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string MdmTermsOfUseUrl;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string MdmComplianceUrl;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string UserSettingSyncUrl;
            public IntPtr pUserInfo;
        }
    }
}
