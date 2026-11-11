using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace QuestResearch.Clinician
{
    // Raw clinician-tracker frame exactly as streamed by the Windows probe, plus Quest receipt time.
    public struct ClinicianTrackerFrame
    {
        public long Sequence,PcTicks,PcUtcMs,ReceivedTicks;
        public bool TrackerConnected,Valid;public int Result;
        public Vector3 RawPosition;public Quaternion RawRotation;// OpenVR: right-handed, +Y up, -Z forward, metres
        public bool Running=>TrackerConnected&&Valid&&Result==200;// ETrackingResult.Running_OK
        // Unity is left-handed: mirror z. S R S with S=diag(1,1,-1) is quaternion (-x,-y,z,w).
        public Vector3 Position=>new Vector3(RawPosition.x,RawPosition.y,-RawPosition.z);
        public Quaternion Rotation=>new Quaternion(-RawRotation.x,-RawRotation.y,RawRotation.z,RawRotation.w);
    }

    // TCP client for the Windows probe's `stream` mode (normally 127.0.0.1 through `adb reverse`).
    // No Unity API on the worker threads; commands are dispatched on the main thread in Update.
    public sealed class ClinicianTrackerLink : MonoBehaviour
    {
        [SerializeField] string host="127.0.0.1";
        [SerializeField] int port=47800;
        readonly object gate=new object();
        readonly ConcurrentQueue<string> commands=new ConcurrentQueue<string>(),outgoing=new ConcurrentQueue<string>();
        ClinicianTrackerFrame latest;bool haveFrame;
        Thread worker;TcpClient client;volatile bool running,connected;
        public bool Connected=>connected;
        public string Serial{get;private set;}
        public string ClinicianHand{get;private set;}="right";
        public long PcFrequency{get;private set;}=10000000;
        public long Frames{get;private set;}
        public int Connections{get;private set;}
        // Clock mapping from the lowest-RTT ping in the last 10: pcSeconds + offset = Quest Stopwatch seconds.
        public double RoundTripMs{get;private set;}=-1;
        public double PcToQuestOffsetSeconds{get;private set;}=double.NaN;
        public event Action<string> Command;
        public event Action<bool> ConnectionChanged;
        public string Host=>host;public int Port=>port;
        bool lastConnected;
        public bool TryLatest(out ClinicianTrackerFrame frame){lock(gate){frame=latest;return haveFrame;}}
        public void Send(string line){if(connected&&line!=null)outgoing.Enqueue(line.Replace('\n',' '));}
        public static double Seconds(long ticks)=>ticks/(double)Stopwatch.Frequency;
        void OnEnable(){running=true;worker=new Thread(Run){IsBackground=true,Name="Clinician tracker link"};worker.Start();}
        void OnDisable()
        {
            running=false;try{client?.Close();}catch(Exception){}
            worker?.Join(1000);worker=null;connected=false;
            if(lastConnected){lastConnected=false;ConnectionChanged?.Invoke(false);}
        }
        void Update()
        {
            if(connected!=lastConnected){lastConnected=connected;ConnectionChanged?.Invoke(connected);}
            while(commands.TryDequeue(out var c))Command?.Invoke(c);
        }
        void Run()
        {
            while(running)
            {
                try
                {
                    using(var c=new TcpClient{NoDelay=true})
                    {
                        if(!c.ConnectAsync(host,port).Wait(1000)||!c.Connected)throw new IOException("connect timeout");
                        client=c;var stream=c.GetStream();
                        var writer=new StreamWriter(stream,new UTF8Encoding(false)){NewLine="\n",AutoFlush=false};
                        while(outgoing.TryDequeue(out _)){}
                        lock(gate)haveFrame=false;
                        connected=true;Connections++;
                        var sender=new Thread(()=>Write(writer)){IsBackground=true,Name="Clinician tracker sender"};sender.Start();
                        var reader=new StreamReader(stream,Encoding.UTF8);string line;
                        while(running&&(line=reader.ReadLine())!=null)Parse(line);
                        connected=false;sender.Join(500);
                    }
                }
                catch(Exception){}
                connected=false;client=null;
                if(running)Thread.Sleep(500);
            }
        }
        void Write(StreamWriter writer)
        {
            long nextPing=0;
            try
            {
                while(running&&connected)
                {
                    long now=Stopwatch.GetTimestamp();
                    if(now>=nextPing){writer.WriteLine("P,"+now.ToString(CultureInfo.InvariantCulture));nextPing=now+Stopwatch.Frequency;}
                    bool any=false;while(outgoing.TryDequeue(out var line)){writer.WriteLine(line);any=true;}
                    writer.Flush();if(!any)Thread.Sleep(5);
                }
            }
            catch(Exception){connected=false;try{client?.Close();}catch(Exception){}}
        }
        static double D(string s)=>double.Parse(s,NumberStyles.Float,CultureInfo.InvariantCulture);
        static long L(string s)=>long.Parse(s,NumberStyles.Integer,CultureInfo.InvariantCulture);
        readonly double[] rtts=new double[10],offsets=new double[10];int pingCount;
        void Parse(string line)
        {
            long received=Stopwatch.GetTimestamp();
            try
            {
                var p=line.Split(',');
                switch(p[0])
                {
                    case "F":
                        if(p.Length<14)return;
                        var f=new ClinicianTrackerFrame{Sequence=L(p[1]),PcTicks=L(p[2]),PcUtcMs=L(p[3]),TrackerConnected=p[4]=="1",Valid=p[5]=="1",
                            Result=(int)L(p[6]),RawPosition=new Vector3((float)D(p[7]),(float)D(p[8]),(float)D(p[9])),
                            RawRotation=new Quaternion((float)D(p[10]),(float)D(p[11]),(float)D(p[12]),(float)D(p[13])),ReceivedTicks=received};
                        lock(gate){latest=f;haveFrame=true;}Frames++;return;
                    case "H":
                        if(p.Length>=6){Serial=p[2];PcFrequency=Math.Max(1,L(p[4]));ClinicianHand=p[5]=="left"?"left":"right";}
                        commands.Enqueue("header");return;
                    case "C":if(p.Length>=2)commands.Enqueue(p[1]);return;
                    case "Q":
                        if(p.Length<3)return;
                        long sent=L(p[1]);double rtt=Seconds(received-sent);
                        double offset=Seconds(sent+(received-sent)/2)-L(p[2])/(double)PcFrequency;
                        int i=pingCount++%rtts.Length;rtts[i]=rtt;offsets[i]=offset;
                        int best=0;int n=Math.Min(pingCount,rtts.Length);for(int k=1;k<n;k++)if(rtts[k]<rtts[best])best=k;
                        RoundTripMs=rtts[best]*1000;PcToQuestOffsetSeconds=offsets[best];return;
                }
            }
            catch(FormatException){}catch(OverflowException){}
        }
        // Estimated Quest-clock time of the PC sample, or NaN before the first ping reply.
        public double PcSampleQuestSeconds(ClinicianTrackerFrame f)=>f.PcTicks/(double)PcFrequency+PcToQuestOffsetSeconds;
    }
}
