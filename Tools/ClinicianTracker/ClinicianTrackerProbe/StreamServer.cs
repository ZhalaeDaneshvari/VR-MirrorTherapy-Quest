using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace QuestResearch.ClinicianTracker
{
    // One Quest client at a time over TCP (normally 127.0.0.1 through `adb reverse`, i.e. the USB cable).
    // Line protocol, invariant culture, OpenVR convention (right-handed, +Y up, -Z forward, metres, x,y,z,w):
    //   PC->Quest  H,1,<serial>,<model>,<stopwatchHz>,<clinicianHand>      on connect
    //              F,<seq>,<pcTicks>,<pcUtcMs>,<connected>,<valid>,<result>,px,py,pz,qx,qy,qz,qw
    //              C,<command>                                            researcher command
    //              Q,<questTicks>,<pcTicks>                               clock-sync reply
    //   Quest->PC  S,<status text>   P,<questTicks>   L,<log line for the PC console>
    // Sending never blocks tracker polling: a bounded queue drops pose frames (counted) if the Quest stalls.
    public sealed class StreamServer:IDisposable
    {
        readonly TcpListener listener;readonly string header;readonly Stopwatch clock;
        readonly BlockingCollection<string> outgoing=new BlockingCollection<string>(512);
        readonly Thread acceptThread,writeThread;volatile bool stopping;
        TcpClient client;StreamWriter writer;readonly object gate=new object();
        public long Sent{get;private set;}public long Dropped{get;private set;}public int Connections{get;private set;}
        public bool ClientConnected{get{lock(gate)return client!=null;}}
        public string LastStatus{get;private set;}="(Quest app not connected)";
        public event Action<string> Received;
        public StreamServer(int port,string header,Stopwatch clock)
        {
            this.header=header;this.clock=clock;
            listener=new TcpListener(IPAddress.Loopback,port);listener.Start();
            acceptThread=new Thread(Accept){IsBackground=true,Name="Quest accept"};acceptThread.Start();
            writeThread=new Thread(Write){IsBackground=true,Name="Quest writer"};writeThread.Start();
        }
        static string N(double v)=>v.ToString("0.#######",CultureInfo.InvariantCulture);
        public void Frame(TrackerSample s,long missingTicks)
        {
            if(!ClientConnected)return;
            string line=s==null
                ?"F,0,"+missingTicks+","+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+",0,0,-1,0,0,0,0,0,0,1"
                :"F,"+s.Seq+","+s.HostTicks+","+s.UtcUnixMs+","+(s.Connected?1:0)+","+(s.Raw.Valid?1:0)+","+(int)s.Result+","
                 +N(s.Raw.Position[0])+","+N(s.Raw.Position[1])+","+N(s.Raw.Position[2])+","
                 +N(s.Raw.Rotation[0])+","+N(s.Raw.Rotation[1])+","+N(s.Raw.Rotation[2])+","+N(s.Raw.Rotation[3]);
            if(outgoing.TryAdd(line))Sent++;else Dropped++;
        }
        public void Command(string name){if(ClientConnected)outgoing.TryAdd("C,"+name);}
        void Accept()
        {
            while(!stopping)
            {
                TcpClient incoming;
                try{incoming=listener.AcceptTcpClient();}catch(Exception){if(stopping)return;continue;}
                incoming.NoDelay=true;
                lock(gate)
                {
                    try{client?.Close();}catch(Exception){}
                    client=incoming;writer=new StreamWriter(incoming.GetStream(),new UTF8Encoding(false)){NewLine="\n",AutoFlush=false};Connections++;
                    while(outgoing.TryTake(out _)){}// never replay stale frames to a new client
                    outgoing.TryAdd(header);
                }
                LastStatus="Quest app connected";Received?.Invoke("connected");
                new Thread(()=>Read(incoming)){IsBackground=true,Name="Quest reader"}.Start();
            }
        }
        void Read(TcpClient source)
        {
            try
            {
                var reader=new StreamReader(source.GetStream(),Encoding.UTF8);string line;
                while(!stopping&&(line=reader.ReadLine())!=null)
                {
                    if(line.StartsWith("P,")){outgoing.TryAdd("Q,"+line.Substring(2)+","+clock.ElapsedTicks);continue;}
                    if(line.StartsWith("S,"))LastStatus=line.Substring(2);
                    Received?.Invoke(line);
                }
            }
            catch(Exception){}
            lock(gate){if(client==source){client=null;writer=null;}}
            LastStatus="(Quest app disconnected)";Received?.Invoke("disconnected");
        }
        void Write()
        {
            foreach(var line in outgoing.GetConsumingEnumerable())
            {
                StreamWriter w;lock(gate)w=writer;if(w==null)continue;
                try{w.WriteLine(line);if(outgoing.Count==0)w.Flush();}
                catch(Exception){lock(gate){if(writer==w){try{client?.Close();}catch(Exception){}client=null;writer=null;}}}
            }
        }
        public void Dispose()
        {
            stopping=true;outgoing.CompleteAdding();
            try{listener.Stop();}catch(Exception){}
            lock(gate){try{client?.Close();}catch(Exception){}}
        }
        // `adb reverse` maps the Quest's 127.0.0.1:<port> to this PC over USB. Returns a message for the console.
        public static string Reverse(string adb,int port)
        {
            if(adb=="none")return "adb reverse skipped (--adb none).";
            adb??=FindAdb();if(adb==null)return "adb not found; pass --adb <path to adb.exe>. Quest will not reach the PC over USB.";
            try
            {
                var p=Process.Start(new ProcessStartInfo(adb,"reverse tcp:"+port+" tcp:"+port){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true});
                string output=(p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd()).Trim();p.WaitForExit(10000);
                return p.ExitCode==0?"adb reverse tcp:"+port+" OK ("+adb+")":"adb reverse failed (exit "+p.ExitCode+"): "+output+". Is the Quest connected by USB and debugging allowed?";
            }
            catch(Exception e){return "adb reverse failed: "+e.Message;}
        }
        public static string FindAdb()
        {
            const string hub=@"C:\Program Files\Unity\Hub\Editor";const string tail=@"Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe";
            var preferred=Path.Combine(hub,"6000.6.2f1",tail);if(File.Exists(preferred))return preferred;
            if(Directory.Exists(hub))foreach(var d in Directory.GetDirectories(hub)){var c=Path.Combine(d,tail);if(File.Exists(c))return c;}
            foreach(var dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')){var c=Path.Combine(dir.Trim(),"adb.exe");if(File.Exists(c))return c;}
            return null;
        }
    }
}
