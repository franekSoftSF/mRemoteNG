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
            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                // Instance is a static field, so anything thrown here surfaces as a
                // TypeInitializationException and the application never starts, with the real cause
                // hidden. Running without a log file is the lesser failure.
                EnsureLog().Error("Could not initialize logging; continuing without a log file.", ex);
            }
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
            try
            {
                ILoggerRepository repository = LogManager.GetRepository("mRemoteNG");

                // Read while the configuration is parsed, so the appender never opens a file anywhere else first.
                GlobalContext.Properties["LogFilePath"] = path;

                XmlConfigurator.Configure(repository, new FileInfo(ConfigFilePath));

                IAppender[] appenders = repository.GetAppenders();

                foreach (IAppender appender in appenders)
                {
                    if (appender is not RollingFileAppender { Name: "LogFileAppender" } fileAppender) continue;
                    fileAppender.File = path;
                    fileAppender.ActivateOptions();
                }

                Log = LogManager.GetLogger("mRemoteNG", "Logger");
            }
            catch (Exception ex)
            {
                // Called from the options page too, where a path the user typed must not take the
                // dialog down; any appender already working keeps writing.
                EnsureLog().Error($"Could not set up logging to \"{path}\"; continuing without it.", ex);
            }
        }

        // Keeps Log non-null for callers that do not check it. The default repository has no file
        // appender configured, so this logger discards what it is given.
        private ILog EnsureLog() => Log ??= LogManager.GetLogger(typeof(Logger));

        private static string BuildLogFilePath()
        {
            // Keep the log with the rest of the per-user settings: beside confCons.xml, or beside the
            // executable in the portable edition.
            return Path.Combine(SettingsFileInfo.SettingsPath, "mRemoteNG.log");
        }
    }
}