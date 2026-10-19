using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
namespace QuestResearch.Recording
{
    // No Unity API on this thread. Immutable strings are copied before enqueueing.
    // Bounded buffering fails visibly instead of silently dropping research records.
    public sealed class MovementSessionWriter
    {
        readonly BlockingCollection<string> queue=new BlockingCollection<string>(120);
        readonly Thread worker;readonly string folder;
        volatile string error;volatile bool done;string closeReason="interrupted";
        public string Error=>error;public bool Done=>done;
        public long RecordsWritten{get;private set;}
        public MovementSessionWriter(string path,string metadata)
        {
            folder=path;Directory.CreateDirectory(folder);
            DurableText(Path.Combine(folder,"session.json"),metadata);
            worker=new Thread(Write){IsBackground=true,Name="Movement session writer"};worker.Start();
        }
        public bool Append(string json)
        {
            if(error!=null||queue.IsAddingCompleted)return false;
            if(queue.TryAdd(json))return true;
            error="Recording buffer full; recording stopped. Existing data retained.";queue.CompleteAdding();return false;
        }
        public void Complete(string reason){closeReason=reason;if(!queue.IsAddingCompleted)queue.CompleteAdding();}
        public bool Wait(int milliseconds)=>worker.Join(milliseconds);
        static string Quote(string value)=>"\""+value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ")+"\"";
        public static void DurableText(string path,string text)
        {
            string temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            {byte[] bytes=new UTF8Encoding(false).GetBytes(text+"\n");stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(temp,path);
        }
        void Write()
        {
            FileStream file=null;StreamWriter writer=null;string partial=null;int chunk=0;
            var clock=Stopwatch.StartNew();double opened=0,flushed=0;
            try
            {
                while(!queue.IsCompleted)
                {
                    if(queue.TryTake(out string line,100))
                    {
                        if(writer==null)
                        {
                            partial=Path.Combine(folder,"movement-"+(++chunk).ToString("D6")+".jsonl.partial");
                            file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                            writer=new StreamWriter(file,new UTF8Encoding(false),65536);opened=flushed=clock.Elapsed.TotalSeconds;
                        }
                        writer.WriteLine(line);RecordsWritten++;
                    }
                    if(writer!=null&&clock.Elapsed.TotalSeconds-flushed>=1){writer.Flush();file.Flush(true);flushed=clock.Elapsed.TotalSeconds;}
                    if(writer!=null&&clock.Elapsed.TotalSeconds-opened>=2)
                    {writer.Flush();file.Flush(true);writer.Dispose();writer=null;file=null;File.Move(partial,partial.Replace(".partial",""));}
                }
                if(writer!=null){writer.Flush();file.Flush(true);writer.Dispose();writer=null;file=null;File.Move(partial,partial.Replace(".partial",""));}
                DurableText(Path.Combine(folder,"closed.json"),"{\"status\":"+Quote(error==null?"closed":"failed")+",\"reason\":"+Quote(closeReason)+",\"records\":"+RecordsWritten+",\"chunks\":"+chunk+",\"error\":"+Quote(error??"")+"}");
            }
            catch(Exception ex){error="Data write failed: "+ex.Message;}
            finally{try{writer?.Dispose();file?.Dispose();}catch(Exception ex){error="Data close failed: "+ex.Message;}done=true;}
        }
    }
}
