using NLog;

namespace NzbDrone.Common.Instrumentation.Extensions
{
    public static class SentryLoggerExtensions
    {
        public static LogEventBuilder WriteSentryWarn(this LogEventBuilder builder, string message)
        {
            return builder;
        }

        public static LogEventBuilder WriteSentryWarn(this LogEventBuilder builder, string message, object arg)
        {
            return builder;
        }
    }
}
