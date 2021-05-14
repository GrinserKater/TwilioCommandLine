using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Common.Enums;
using Common.Options;

namespace CommandManager
{
    public class Manager
    {
        public static void ShowUsageHint()
        {
			var usageHint = new StringBuilder("Usage: TwilioCommandLine ");
			usageHint.AppendLine($"--{ExecutionAction.Unblock:G} | --{ExecutionAction.Block:G}");
            usageHint.AppendLine("Mandatory arguments:");
            usageHint.AppendLine($"\t--{Constants.CommandLineParameters.UserIdArgument} <userId> | {Constants.CommandLineParameters.UniqueIdentifierArgument} <channel unique identifier>");
            usageHint.AppendLine("Optional arguments:");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.PageSizeArgument}]");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.LimitArgument} | --{Constants.CommandLineParameters.AllArgument}]");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.LogToFileArgument}]");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.BeforeArgument} <date> --{Constants.CommandLineParameters.AfterArgument}] <date>");
            usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.BlockedListingsArgument} <file name>]");
			Trace.WriteLine(usageHint);
        }

        public static ExecutionOptions Manage(string[] args)
        {
			if (args == null || args.Length == 0)
			{
				ShowUsageHint();
				return  ExecutionOptions.Empty;
			}

			string[] arguments = args.Select(a => a.Trim('-').ToLower()).ToArray();
            var executionActions = Enum.GetNames(typeof(ExecutionAction));
            var executionAction = arguments.FirstOrDefault(a => executionActions.Any(ms => ms.ToLower() == a.ToLower()));
            if (String.IsNullOrWhiteSpace(executionAction))
			{
				ShowUsageHint();
				return ExecutionOptions.Empty;
			}

			var options = new ExecutionOptions
			{
				ExecutionAction = (ExecutionAction)Enum.Parse(typeof(ExecutionAction), executionAction, true)
			};

			options.UserId = ExtractNextPositionIntegerParameter(arguments, Constants.CommandLineParameters.UserIdArgument, null, null);
			if (options.UserId == 0)
			{
				options.ChannelUniqueIdentifier = ExtractNextPositionStringParameter(arguments, Constants.CommandLineParameters.UniqueIdentifierArgument);
                if (String.IsNullOrWhiteSpace(options.ChannelUniqueIdentifier))
                {
                    ShowUsageHint();
                    return ExecutionOptions.Empty;
				}
			}

            options.DateBefore = ExtractNextPositionDateTimeParameter(arguments, Constants.CommandLineParameters.BeforeArgument);
            options.DateAfter = ExtractNextPositionDateTimeParameter(arguments, Constants.CommandLineParameters.AfterArgument);
            if (options.DateBefore.HasValue && options.DateAfter.HasValue && options.DateBefore == options.DateAfter)
            {
                ShowUsageHint();
                return ExecutionOptions.Empty;
            }

            int pageSize = ExtractNextPositionIntegerParameter(arguments, Constants.CommandLineParameters.PageSizeArgument, Constants.Limits.MaxAllowedPageSize,
				Constants.Limits.DefaultPageSize);
			int resourceLimit = ExtractNextPositionIntegerParameter(arguments, Constants.CommandLineParameters.LimitArgument, null, Constants.Limits.DefaultLimit);
			string logToFile = arguments.ElementAtOrDefault(Array.IndexOf(arguments, Constants.CommandLineParameters.LogToFileArgument));
            string blockedListingsFromFile = arguments.ElementAtOrDefault(Array.IndexOf(arguments, Constants.CommandLineParameters.BlockedListingsArgument));

            if (!String.IsNullOrWhiteSpace(blockedListingsFromFile))
            {
                options.BlockedListingsFileName = ExtractNextPositionStringParameter(arguments, Constants.CommandLineParameters.BlockedListingsArgument);
                if (String.IsNullOrWhiteSpace(options.BlockedListingsFileName))
                {
                    ShowUsageHint();
                    return ExecutionOptions.Empty;
                }
                options.ConsiderBlockedListings = true;
            }

            if (arguments.Contains(Constants.CommandLineParameters.AllArgument)) resourceLimit = 0;

			options.PageSize = pageSize;
			options.ResourceLimit = resourceLimit;
			options.LogToFile = !String.IsNullOrWhiteSpace(logToFile);
            return options;
		}

        private static int ExtractNextPositionIntegerParameter(string[] arguments, string parameterName, int? maxLimit, int? defaultValue)
        {
	        int result = Int32.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, parameterName.ToLower()) + 1), out int value) ? value : maxLimit ?? defaultValue ?? 0;
            if (maxLimit.HasValue && !defaultValue.HasValue) return result;
            return result > maxLimit ? defaultValue.Value : result;
        }

        private static DateTime? ExtractNextPositionDateTimeParameter(string[] arguments, string parameterName)
        {
	        if(!DateTime.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, parameterName.ToLower()) + 1), out DateTime value)) return null;
            return value;
        }

        private static string ExtractNextPositionStringParameter(string[] arguments, string parameterName)
        {
            string result = arguments.ElementAtOrDefault(Array.IndexOf(arguments, parameterName.ToLower()) + 1);
			if (String.IsNullOrWhiteSpace(result) || result.StartsWith("--")) return String.Empty;
			return result;
        }
	}
}
