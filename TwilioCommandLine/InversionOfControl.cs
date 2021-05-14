using Microsoft.Extensions.DependencyInjection;
using TheGrandExecutor;
using TheGrandExecutor.Abstractions;
using TwilioHttpClient.Extensions;

namespace TwilioCommandLine
{
    public static class InversionOfControl
    {
        public static ServiceProvider Setup()
        {
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddTwilioClient()
                .AddSingleton<IExecutor, Executor>()
                .BuildServiceProvider();

            return serviceProvider;
        }
    }
}
