using System;
using System.IO;
using System.Runtime.Versioning;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository;
using mRemoteNG.App.Info;

namespace mRemoteNG.App
{
    [SupportedOSPlatform("windows")]
    public class Logger
    {
        public static readonly Logger Instance = new();

        // Resolved against the executable's folder; the working directory is not guaranteed to be it.
        // A property, not a static field: field initializers run after Instance's and would still be null here.
        private static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, "log4net.config");

        public ILog Log { get; private set; } = null!; // initialized via SetLogPath() called from the constructor

        public static string DefaultLogPath => BuildLogFilePath();

        private Logger()
        {
            Initialize();
        }

        private void Initialize()
        {
            LogManager.CreateRepository("mRemoteNG");

            if (string.IsNullOrEmpty(Properties.OptionsNotificationsPage.Default.LogFilePath))
            {
                Properties.OptionsNotificationsPage.Default.LogFilePath = BuildLogFilePath();
            }

            SetLogPath(Properties.OptionsNotificationsPage.Default.LogToApplicationDirectory ? DefaultLogPath : Properties.OptionsNotificationsPage.Default.LogFilePath);
        }

        public void SetLogPath(string path)
        {
            ILoggerRepository repository = LogManager.GetRepository("mRemoteNG");

            // Read while the configuration is parsed, so the appender never opens a file anywhere else first.
            GlobalContext.Properties["LogFilePath"] = path;

            XmlConfigurator.Configure(repository, new FileInfo(ConfigFilePath));

            IAppender[] appenders = repository.GetAppenders();

            foreach (IAppender appender in appenders)
            {
                RollingFileAppender fileAppender = (RollingFileAppender)appender;
                if (fileAppender is not { Name: "LogFileAppender" }) continue;
                fileAppender.File = path;
                fileAppender.ActivateOptions();
            }

            Log = LogManager.GetLogger("mRemoteNG", "Logger");
        }

        private static string BuildLogFilePath()
        {
            // Keep the log with the rest of the per-user settings: beside confCons.xml, or beside the
            // executable in the portable edition.
            return Path.Combine(SettingsFileInfo.SettingsPath, "mRemoteNG.log");
        }
    }
}