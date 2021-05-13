using System.Threading.Tasks;
using Common.Options;
using TheGrandExecutor.Models;
using TwilioHttpClient.Abstractions;

namespace TheGrandExecutor.Abstractions
{
	public interface IExecutor
	{
		Task<ExecutionResult<IResource>> SetUserChannelsAttributesBlockedAsync(ExecutionOptions executionOptions);
		Task<ExecutionResult<IResource>> SetUserChannelsAttributesUnblockedAsync(ExecutionOptions executionOptions);
		Task<ExecutionResult<IResource>> SetSingleChannelAttributesBlockedAsync(ExecutionOptions executionOptions);
		Task<ExecutionResult<IResource>> SetSingleChannelAttributesUnblockedAsync(ExecutionOptions executionOptions);
	}
}
