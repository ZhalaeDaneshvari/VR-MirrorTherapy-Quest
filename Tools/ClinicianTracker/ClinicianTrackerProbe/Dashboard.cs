using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace QuestResearch.ClinicianTracker
{
    // Researcher view: a minimal HTTP server bound to 127.0.0.1 only (no network exposure, no admin rights needed).
    // GET / serves dashboard.html, GET /status returns the latest JSON snapshot, POST /cmd queues one researcher
    // command, POST /view opens the live headset view. One request per connection.
    public sealed class Dashboard:IDisposable
    {
        readonly TcpListener listener;readonly Thread thread;volatile bool stopping;
        readonly string html;readonly Func<string> status;readonly Func<string,string> command;readonly Func<string> view;
        public int Port{get;}
        public Dashboard(int port,string htmlPath,Func<string> status,Func<string,string> command,Func<string> view)
        {
            Port=port;this.status=status;this.command=command;this.view=view;
            html=File.Exists(htmlPath)?File.ReadAllText(htmlPath):"<!doctype html><title>Dashboard missing</title><p>dashboard.html not found next to the probe.";
            listener=new TcpListener(IPAddress.Loopback,port);listener.Start();
            thread=new Thread(Serve){IsBackground=true,Name="Researcher dashboard"};thread.Start();
        }
        void Serve()
        {
            while(!stopping)
            {
                TcpClient client;try{client=listener.AcceptTcpClient();}catch(Exception){if(stopping)return;continue;}
                ThreadPool.QueueUserWorkItem(_=>Handle(client));
            }
        }
        void Handle(TcpClient client)
        {
            using(client)
            {
                try
                {
                    client.ReceiveTimeout=2000;var stream=client.GetStream();var reader=new StreamReader(stream,Encoding.ASCII);
                    string request=reader.ReadLine();if(string.IsNullOrEmpty(request))return;
                    var parts=request.Split(' ');if(parts.Length<2)return;
                    int length=0;string line;
                    while(!string.IsNullOrEmpty(line=reader.ReadLine()))
                        if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))int.TryParse(line.Substring(15).Trim(),out length);
                    var body=new char[Math.Min(length,256)];int read=0;while(read<body.Length){int n=reader.Read(body,read,body.Length-read);if(n<=0)break;read+=n;}
                    string method=parts[0],path=parts[1].Split('?')[0];
                    if(method=="GET"&&(path=="/"||path=="/index.html"))Send(stream,200,"text/html; charset=utf-8",html);
                    else if(method=="GET"&&path=="/status")Send(stream,200,"application/json",status());
                    else if(method=="POST"&&path=="/cmd")Send(stream,200,"text/plain; charset=utf-8",command(new string(body,0,read).Trim()));
                    else if(method=="POST"&&path=="/view")Send(stream,200,"text/plain; charset=utf-8",view());
                    else Send(stream,404,"text/plain","not found");
                }
                catch(Exception){}
            }
        }
        static void Send(Stream stream,int code,string type,string text)
        {
            var payload=Encoding.UTF8.GetBytes(text??"");
            var head=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+(code==200?" OK":" Not Found")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+payload.Length
                +"\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            stream.Write(head,0,head.Length);stream.Write(payload,0,payload.Length);stream.Flush();
        }
        public void Dispose(){stopping=true;try{listener.Stop();}catch(Exception){}}
    }
}
