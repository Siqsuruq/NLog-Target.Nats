using NLog;
using NLog.Config;

namespace NLogNatsExample
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            // Load NLog configuration from NLog.config file
            LogManager.Configuration = new XmlLoggingConfiguration("NLog.config");

            Logger.Info("This is an info message");
            Logger.Debug("This is a debug message");
            Logger.Error("This is an error message");

            Console.WriteLine("Logs have been sent to NATS.");
            Console.ReadLine();
        }
    }
}
