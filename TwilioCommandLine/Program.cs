using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommandManager;
using Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using TheGrandExecutor.Abstractions;
using TwilioCommandLine.Logging;
using TwilioHttpClient.Abstractions;

namespace TwilioCommandLine
{
    class Program
    {
        private static string _logFileName = $"{LoggingUtilities.LogFolder}\\Migration_{{0}}_log_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";

        public static async Task Main(string[] args)
        {
	        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

			var options = Manager.Manage(args);
            if (options.IsEmpty) return;

			if (options.LogToFile)
			{
				Directory.CreateDirectory(LoggingUtilities.LogFolder);
				_logFileName = String.Format(_logFileName, options.ExecutionAction.ToString("G"));
				Trace.Listeners.Add(new TextWriterTraceListener(File.CreateText(_logFileName)));
			}

			try
			{
				ServiceProvider serviceProvider = InversionOfControl.Setup();

				IExecutor grandExecutor = serviceProvider.GetRequiredService<IExecutor>();

				Trace.WriteLine($"Starting {options.ExecutionAction:G} of {(options.UserId != 0 ? $"user with ID {options.UserId}" : $"channel with unique name {options.ChannelUniqueIdentifier}")} - {DateTime.Now.ToShortDateString()}.");

				var sw = new Stopwatch();
				sw.Start();
                IExecutionResult<IResource> executionResult;
				switch (options.ExecutionAction)
				{
					case ExecutionAction.Unblock:
						executionResult = String.IsNullOrWhiteSpace(options.ChannelUniqueIdentifier) ?
							await grandExecutor.SetSingleChannelAttributesUnblockedAsync(options) :
                            await grandExecutor.SetUserChannelsAttributesUnblockedAsync(options);
						break;
					case ExecutionAction.Block:
						executionResult = String.IsNullOrWhiteSpace(options.ChannelUniqueIdentifier) ?
							await grandExecutor.SetSingleChannelAttributesBlockedAsync(options) :
							await grandExecutor.SetUserChannelsAttributesBlockedAsync(options);
                        break;
                    default:
						Trace.WriteLine($"Unsupported execution action {options.ExecutionAction:G}.");
						return;
				}
				sw.Stop();

				LoggingUtilities.WriteExecutionResultLogFiles(executionResult);
				Trace.WriteLine($"Execution finished. Time elapsed: {sw.Elapsed.Seconds}s. Results:");
				Trace.WriteLine(
					$"\tTotal fetched from Twilio: {executionResult.FetchedCount}; successfully processed: {executionResult.SuccessCount}; skipped: {executionResult.SkippedCount}; failed {executionResult.FailedCount}.");

				if (executionResult.FailedCount == 0) return;

                Trace.WriteLine("The following messages were recorded during the execution:");
				Trace.WriteLine($"\t{executionResult.Message}");
				foreach (string message in executionResult.ErrorMessages) Trace.WriteLine($"\t{message}");
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"Exception happened: {ex.Message}.");
			}
			finally
			{
				Trace.Flush();
			}
        }
    }
}
