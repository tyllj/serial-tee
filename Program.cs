using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SerialTee
{
    class Program
    {
        static async Task Main(string[] args)
        {

            string port0Name;
            int port0Baud;
            string port1Name;
            int port1Baud;
            
            try
            {
                port0Name = args[0];
                port0Baud = Int32.Parse(args[1]);
                port1Name = args[2];
                port1Baud = Int32.Parse(args[3]);
            }
            catch
            {
                Console.WriteLine("Usage: serialtee PORTNAME0 BAUDRATE0 PORTNAME1 BAUDRATE1");
                return;
            }

            if (args.Length != 2)
                throw new ArgumentException();
            
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, __) =>
            {
                cts.Cancel(); 
                Console.In.Close();
            };

            var logFilePath = Path.Combine(Environment.CurrentDirectory, "serialtee.log");
            await using var logger = new LoggerConfiguration()
                .WriteTo.File(logFilePath)
                .CreateLogger();

            using var port0 = new SerialPort(port0Name) {BaudRate = port0Baud};
            using var port1 = new SerialPort(port1Name) {BaudRate = port1Baud};

            port0.Open();
            port1.Open();
            
            await using var passthrough = new SerialPassthroughLogger(logger, port0, port1);
            
            passthrough.Start();
            
            await LogUserNotesUntilCancelled(logger, cts.Token);
            
            cts.Token.WaitHandle.WaitOne();
        }

        private static async Task LogUserNotesUntilCancelled(ILogger logger, CancellationToken ct)
        {
            try {
                await Task.Run( () =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        logger.Information("User note: {Message}", ReadLine(Console.In, ct));
                    }
                },ct);
            }
            catch (OperationCanceledException)
            { }
        }

        private static string ReadLine(TextReader input, CancellationToken ct)
        {
            int c;
            var s = new StringBuilder();
            for (;;)
            {
                ct.ThrowIfCancellationRequested();
                if ((c = input.Read()) != -1)
                {
                    if (c == '\r')
                        return s.ToString();
                    s.Append((char)c);
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}