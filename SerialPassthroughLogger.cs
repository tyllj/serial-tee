using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;
using Serilog;

namespace SerialTee
{
    public enum Source
    {
        PC,
        BUS
    }

    struct DataSegment
    {
        public ByteBuffer Data;
        public Source Source;
        public TimeSpan TimeStamp;

    }
    
    public class SerialPassthroughLogger : IAsyncDisposable
    {
        private readonly BlockingCollection<DataSegment> _logQueue;
        private readonly ILogger _logger;
        private readonly SerialPort _virtualPort;
        private readonly SerialPort _busPort;
        private readonly ManualResetEventSlim _stopEvent;
        private DateTime _startTime;
        private List<Task> _tasks;

        public SerialPassthroughLogger(ILogger logger, SerialPort virtualPort, SerialPort busPort)
        {
            _logger = logger;
            _virtualPort = virtualPort;
            _busPort = busPort;
            _logQueue = new BlockingCollection<DataSegment>();
            _stopEvent = new ManualResetEventSlim(false);
            _tasks = new List<Task>(3);
        }

        private void Forward(Source source, SerialPort sender, SerialPort receiver)
        {
            
            DataSegment s = new DataSegment() { Source = source, TimeStamp = DateTime.Now.Subtract(_startTime)};
            int i;
            int b;
            int readLength = Math.Min(s.Data.MaxLenght, sender.BytesToRead);
            if (readLength == 0)
            {
                Thread.Sleep(5);
                return;
            }
                
            for (i = 0; i < readLength && (b = sender.ReadByte()) != -1; i++)
                s.Data.Add((byte) b);
            _logQueue.Add(s);
            receiver.BaseStream.Write(s.Data.AsSpan().Slice(0, i));
        }
        
        private async Task ForwardBothDirections()
        {
            await Task.Run(() =>
            {
                while (!_stopEvent.IsSet)
                {
                    Forward(Source.PC, _virtualPort, _busPort);
                    Forward(Source.BUS, _busPort, _virtualPort);
                }
                _logQueue.CompleteAdding();
            });
        }

        private async Task Consume()
        {
            await Task.Run(() =>
            {
                try
                {
                    while (!_logQueue.IsAddingCompleted)
                    {
                        var s = _logQueue.Take();
                        _logger.Information("{Timestamp,10:0.000} {Source} {Payload}", s.TimeStamp.TotalSeconds,
                            s.Source,
                            s.Data.ToHexString());

                    }
                } catch (InvalidOperationException) {}
            });
        }

        public void Start()
        {
            _startTime = DateTime.Now;
            _tasks.Add(Consume());
            _tasks.Add(ForwardBothDirections());
        }

        public async Task Stop()
        {
            _stopEvent.Set();
            await Task.WhenAll(_tasks);
        }

        public async ValueTask DisposeAsync() => await Stop();
    }
}