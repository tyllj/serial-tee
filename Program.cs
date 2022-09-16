using System;
using System.IO;
using System.IO.Ports;
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
            Console.TreatControlCAsInput = true;
            
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
            catch (Exception e)
            {
                Console.WriteLine("Usage: serialtee PORTNAME0 BAUDRATE0 PORTNAME1 BAUDRATE1");
                Console.WriteLine("Ports available:");
                Console.WriteLine(string.Join('\n', SerialPort.GetPortNames()));
                return;
            }
            
            using var port0 = new SerialPort(port0Name) {BaudRate = port0Baud};
            using var port1 = new SerialPort(port1Name) {BaudRate = port1Baud};

            try
            {
                port0.Open();
                port1.Open();
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"Port {e.FileName} does not exist.");
                return;
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine("Port is used by an other program, or you don't have the required access permissions.");
                return;
            }
            

            var logFilePath = Path.Combine(Environment.CurrentDirectory, "serialtee.log");
            Console.WriteLine($"Logging to: {logFilePath}");
            Console.WriteLine("Press Ctrl+C to close.");
            await using var logger = new LoggerConfiguration()
                .WriteTo.File(logFilePath, flushToDiskInterval: TimeSpan.FromMilliseconds(5000))
                .CreateLogger();

            logger.Information("---- Logging started ----");

            await using var passthrough = new SerialPassthroughLogger(logger, port0, port1);
            
            passthrough.Start();
            
            await LogUserNotesUntilCancelled(logger);
            await passthrough.Stop();
            
            logger.Information("---- Logging ended ----");
            Console.WriteLine("Shutting down.");
        }

        private static async Task LogUserNotesUntilCancelled(ILogger logger)
        {
            await Task.Run(() =>
            {
                try
                {
                    for (;;)
                        logger.Information("User note: {Message}", ReadLine());
                }
                catch (OperationCanceledException) { }
            });
        }

        private static string ReadLine()
        {
            var s = new StringBuilder();
            for (;;)
            {
                var c = Console.ReadKey().KeyChar;
                if (c == 0x03) // ETX
                    throw new OperationCanceledException();

                if (c == '\r')
                    return s.ToString();
                s.Append(c);
            }
        }
    }
}