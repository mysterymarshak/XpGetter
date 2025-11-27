using Serilog;
using XpGetter.Dto;
using XpGetter.Errors;

namespace XpGetter.Extensions;

public static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public void LogError(BaseError error)
        {
            logger.Error(error.Message);
            if (error.Exception is not null)
            {
                logger.Error(error.Exception, string.Empty);
            }
        }
    }
}