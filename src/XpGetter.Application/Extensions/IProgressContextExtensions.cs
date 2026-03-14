using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Extensions;

public static class IProgressContextExtensions
{
    extension(IProgressContext ctx)
    {
        public void AddFinishedTask(string description)
        {
            ctx.AddTask(Messages.Common.Dummy).SetResult(description);
        }
    }
}