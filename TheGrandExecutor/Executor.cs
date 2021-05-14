﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Options;
using Common.Results;
using TheGrandExecutor.Abstractions;
using TheGrandExecutor.Constants;
using TheGrandExecutor.Enums;
using TheGrandExecutor.Models;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models;

namespace TheGrandExecutor
{
    public class Executor : IExecutor
	{
        private readonly ITwilioHttpClient _twilioClient;
        public Executor(ITwilioHttpClient twilioClient)
		{
			_twilioClient   = twilioClient ?? throw new ArgumentNullException(nameof(twilioClient));
        }

		public async Task<ExecutionResult<IResource>> SetUserChannelsAttributesBlockedAsync(ExecutionOptions executionOptions)
        {
            if (executionOptions == null) throw new ArgumentNullException(nameof(executionOptions));

            var result = new ExecutionResult<IResource>();
            if (executionOptions.UserId <= 0)
            {
				result.Message = "Setting blocked the user failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"{executionOptions.UserId} is invalid.");
				return result;
			}

			ExecutionResult<IResource> userMigrationResult = await UpdateUserChannelsAttributesBlockageFieldsAsync(executionOptions, true);

			if (userMigrationResult.FetchedCount == 0)
            {
                result.Message = $"Updating the channels' attributes for the user {executionOptions.UserId} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Failed to migrate the user with ID {executionOptions.UserId}; reason: {userMigrationResult.Message}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
			}
            CopyExecutionResult(userMigrationResult, result, null);

            if (userMigrationResult.FailedCount == 0) return result;

            result.Message = $"Some issues occured while updating the channels' attributes for the user {executionOptions.UserId}. See ErrorMessages for details.";
            Debug.WriteLine(result.ErrorMessages.Last());
            return result;
        }

        public async Task<ExecutionResult<IResource>> SetUserChannelsAttributesUnblockedAsync(ExecutionOptions executionOptions)
        {
            if (executionOptions == null) throw new ArgumentNullException(nameof(executionOptions));

            var result = new ExecutionResult<IResource>();
            if (executionOptions.UserId <= 0)
            {
                result.Message = "Setting unblocked the user failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"{executionOptions.UserId} is invalid.");
                return result;
            }

            ExecutionResult<IResource> executionResult = await UpdateUserChannelsAttributesBlockageFieldsAsync(executionOptions, false);

            if (executionResult.FetchedCount == 0)
            {
                result.Message = $"Updating the channels' attributes for the user {executionOptions.UserId} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Failed to update the channels' attributes for the user with ID {executionOptions.UserId}; reason: {executionResult.Message}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }
            CopyExecutionResult(executionResult, result, null);

            if (executionResult.FailedCount <= 0) return result;

            result.Message = $"Some issues occured while updating the channels' attributes for the user {executionOptions.UserId}. See ErrorMessages for details.";
            Debug.WriteLine(result.ErrorMessages.Last());
            return result;
        }

        public async Task<ExecutionResult<IResource>> SetSingleChannelAttributesUnblockedAsync(ExecutionOptions executionOptions)
        {
            if (executionOptions == null) throw new ArgumentNullException(nameof(executionOptions));

            var result = new ExecutionResult<IResource>();
            if (String.IsNullOrWhiteSpace(executionOptions.ChannelUniqueIdentifier))
            {
                result.Message = $"Setting unblocked the channel's attributes failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"{executionOptions.ChannelUniqueIdentifier} is invalid.");
                return result;
            }

            int[] blockedListingsIds = null;
            if (executionOptions.ConsiderBlockedListings)
                blockedListingsIds = ReadBlockListingsIdsFromFile(executionOptions.BlockedListingsFileName);

            ExecutionResult<IResource> executionResult =
                await UpdateSingleChannelAttributesBlockageFieldsAsync(executionOptions.ChannelUniqueIdentifier, executionOptions.DateBefore, 
                    executionOptions.DateAfter, blockedListingsIds, null, 0, false);

            if (executionResult.FetchedCount == 0)
            {
                result.Message = $"Updating the attributes of the channel {executionOptions.ChannelUniqueIdentifier} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Failed to  the attributes of the channel {executionOptions.ChannelUniqueIdentifier}; reason: {executionResult.Message}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }
            CopyExecutionResult(executionResult, result, null);

            if (executionResult.FailedCount <= 0) return result;

            result.Message = $"Some issues occured while updating the attributes of the channel {executionOptions.ChannelUniqueIdentifier}. See ErrorMessages for details.";
            Debug.WriteLine(result.ErrorMessages.Last());
            return result;
        }

        public async Task<ExecutionResult<IResource>> SetSingleChannelAttributesBlockedAsync(ExecutionOptions executionOptions)
        {
            if (executionOptions == null) throw new ArgumentNullException(nameof(executionOptions));

            var result = new ExecutionResult<IResource>();
            if (String.IsNullOrWhiteSpace(executionOptions.ChannelUniqueIdentifier))
            {
                result.Message = $"Setting blocked the channel's attributes failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"{executionOptions.ChannelUniqueIdentifier} is invalid.");
                return result;
            }

            ExecutionResult<IResource> executionResult =
                await UpdateSingleChannelAttributesBlockageFieldsAsync(executionOptions.ChannelUniqueIdentifier, executionOptions.DateBefore,
                    executionOptions.DateAfter, null, null, 0, true);

            if (executionResult.FetchedCount == 0)
            {
                result.Message = $"Updating the attributes of the channel {executionOptions.ChannelUniqueIdentifier} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Failed to  the attributes of the channel {executionOptions.ChannelUniqueIdentifier}; reason: {executionResult.Message}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }
            CopyExecutionResult(executionResult, result, null);

            if (executionResult.FailedCount <= 0) return result;
            
            result.Message = $"Some issues occured while updating the attributes of the channel {executionOptions.ChannelUniqueIdentifier}. See ErrorMessages for details.";
            Debug.WriteLine(result.ErrorMessages.Last());
            return result;
        }

		private async Task<ExecutionResult<IResource>> UpdateUserChannelsAttributesBlockageFieldsAsync(ExecutionOptions executionOptions, bool toBlock)
		{
			int? fetchNoMoreThan = null;
			if (executionOptions.ResourceLimit > 0) fetchNoMoreThan = executionOptions.ResourceLimit;
			int entitiesPerPage = executionOptions.PageSize == 0 ? Execution.DefaultPageSize : executionOptions.PageSize;

            var result = new ExecutionResult<IResource>();

            // first we will fetch the user to take a look at their blockedUsers array.
            var userFetchResult = await _twilioClient.UserFetchAsync(executionOptions.UserId.ToString());
            if (!userFetchResult.IsSuccess)
            {
                result.Message = $"Fetching the user {executionOptions.UserId} failed. See ErrorMessages for details.";
                result.ErrorMessages.Add($"Message: [{userFetchResult.FormattedMessage}]; HTTP status code: [{userFetchResult.HttpStatusCode}]");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }
            int[] blockedUserIds = userFetchResult.Payload.Attributes?.BlockedUsers;

            // Unfortunately, smart Twilio engineers decided to return absolutely different object as UserChannelResource rather than ChannelResource.
            HttpClientResult <List<UserChannel>> userChannelsFetchResult = await _twilioClient.UserChannelsBulkRetrieveAsync(executionOptions.UserId.ToString(), entitiesPerPage, fetchNoMoreThan);
			if (!userChannelsFetchResult.IsSuccess)
			{
				result.Message = $"Fetching channels for the user {executionOptions.UserId} failed. See ErrorMessages for details.";
				result.ErrorMessages.Add($"Message: [{userChannelsFetchResult.FormattedMessage}]; HTTP status code: [{userChannelsFetchResult.HttpStatusCode}]");
				Debug.WriteLine(result.ErrorMessages.Last());
				return result;
			}

			if (userChannelsFetchResult.Payload.Count == 0)
			{
				result.Message = $"No channels attributes for the account ID {executionOptions.UserId} to update.";
				return result;
			}

            int[] blockedListingsIds = null;
            if (executionOptions.ConsiderBlockedListings)
                blockedListingsIds = ReadBlockListingsIdsFromFile(executionOptions.BlockedListingsFileName);

            // This piece of code might seem very similar to the one in MigrateChannelsAttributesAsync,
            // but this one is slightly optimised for the single user case.
            foreach (UserChannel userChannel in userChannelsFetchResult.Payload)
			{
                var channelUpdateExecutionResult =
                    await UpdateSingleChannelAttributesBlockageFieldsAsync(userChannel.ChannelSid, executionOptions.DateBefore, executionOptions.DateAfter,
                        blockedListingsIds, blockedUserIds, executionOptions.UserId, toBlock);
				CopyExecutionResult(channelUpdateExecutionResult, result, null);
            }

			if (result.FailedCount > 0)
				result.Message =
					$"Not all channels' attributes updated successfully. {result.FailedCount} failed, {result.SuccessCount} succeeded. See ErrorMessages for details.";

			result.Message = $"Update finished. Totally updated {result.SuccessCount} channels' attributes.";
			return result;
		}

        private async Task<ExecutionResult<IResource>> UpdateSingleChannelAttributesBlockageFieldsAsync(string uniqueName, DateTime? dateBefore, DateTime? dateAfter,
            int[] blockedListingsIds, int[] blockedUsersIds, int currentUserId, bool toBlock)
        {
            var result = new ExecutionResult<IResource>();

			HttpClientResult<Channel> channelFetchResult = await _twilioClient.ChannelFetchAsync(uniqueName);
            if (!channelFetchResult.IsSuccess)
            {
                result.ErrorMessages.Add(
                    $"Failed to retrieve channel with unique name {uniqueName}; reason: {channelFetchResult.FormattedMessage}; HTTP status code: {channelFetchResult.HttpStatusCode}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                return result;
            }

            var channel = channelFetchResult.Payload;
            result.EntitiesFetched.Add(channel);
            if (channel.Attributes == null)
            {
                Trace.WriteLine($"Channel {channel.UniqueName} skipped. Attributes are empty.");
                result.EntitiesSkipped.Add(channel);
                return result;
            }

            if (await IsBlockedDeliberatelyAsync(blockedUsersIds, currentUserId, channel.UniqueName))
            {
                Trace.WriteLine($"Channel {channel.UniqueName} skipped. The channel was blocked by an addresser or by an addressee deliberately.");
                result.EntitiesSkipped.Add(channel);
                return result;
            }

            if (!IsIncludedByDate(channel.DateUpdated, dateBefore, dateAfter))
            {
                Trace.WriteLine($"Channel {channel.UniqueName} skipped. Last updated on {channel.DateUpdated}. Requested time period: {(dateBefore == null ? "" : $"before {dateBefore}")} {(dateAfter == null ? "" : $"after {dateAfter}")}.");
                result.EntitiesSkipped.Add(channel);
				return result;
            }

            if (IsStateAquired(channel, toBlock))
            {
                Trace.WriteLine($"Channel {channel.UniqueName} skipped. Required state [Blocked: {toBlock}] acquired, or no attributes.");
                result.EntitiesSkipped.Add(channel);
                return result;
            }

            // if we provide a list of blocked listings (e.g., from a file), and the channel's listing is in that list,
            // we will not "unblock" it.
            if (!toBlock && IsInBlockedList(blockedListingsIds, channel.Attributes?.Listing?.Id))
            {
                Trace.WriteLine($"Channel {channel.UniqueName} skipped. Found in blocked list when unblocking.");
                result.EntitiesSkipped.Add(channel);
                return result;
            }

            string updatedAttributes = UpdateAttributesBlockageFields(channel.AttributesRaw, toBlock);

            var channelUpdateResult = await _twilioClient.ChannelAttributesUpdateAsync(channel.UniqueName, updatedAttributes);
            if (!channelUpdateResult.IsSuccess)
            {
                result.ErrorMessages.Add(
                    $"Failed to update channel with unique name {channel.UniqueName}; reason: {channelUpdateResult.FormattedMessage}; HTTP status code: {channelUpdateResult.HttpStatusCode}.");
                Debug.WriteLine(result.ErrorMessages.Last());
                result.EntitiesFailed.Add(channel);
				return result;
            }
            Trace.WriteLine($"Atrtibutes of the channel {channel.UniqueName} updated successfully. Status \"Blocked\" changed to {toBlock.ToString().ToLower()}");
            result.EntitiesSucceeded.Add(channel);
            return result;
		}

		private static void CopyExecutionResult(ExecutionResult<IResource> from, /* mutable */ ExecutionResult<IResource> to, string customMessage)
        {
			if (from == null || to == null) throw new ArgumentNullException();

			to.Message = customMessage ?? from.Message;
            to.ErrorMessages.AddRange(from.ErrorMessages);

			to.EntitiesFetched.AddRange(from.EntitiesFetched);
            to.EntitiesFailed.AddRange(from.EntitiesFailed);
            to.EntitiesSucceeded.AddRange(from.EntitiesSucceeded);
            to.EntitiesSkipped.AddRange(from.EntitiesSkipped);
        }
        private bool IsIncludedByDate(DateTime? reference, DateTime? before, DateTime? after)
        {
			if (reference == null || before == null && after == null) return true;
			// Intersection (reference is IN the date interval).
            if (after <= before) return after <= reference && reference <= before;
			// Exclusion (reference is either older than before or younger than after).
            return reference <= before || reference >= after;
        }

        private bool IsStateAquired(Channel channel, bool toBlock)
        {
            if (channel.Attributes == null) return true;
            return (channel.Attributes.IsBlocked && toBlock) || (!channel.Attributes.IsBlocked && !toBlock);
        }

        private bool IsInBlockedList(int[] referenceIds, int? currentId)
        {
            if (referenceIds == null || referenceIds.Length == 0 || currentId == null) return false;
            return referenceIds.Any(id => id == currentId);
        }

        private async Task<bool> IsBlockedDeliberatelyAsync(int[] blockedUsersIds, int currentUserId, string channelUniqueName)
        {
            if (blockedUsersIds == null || blockedUsersIds.Length == 0) return false;

            var channelUniqueNameParts = channelUniqueName.Split('-');
            var secondMembersId = channelUniqueNameParts
                .Skip(1)
                .Select(id => Int32.TryParse(id, out int userId) ? userId : 0)
                .FirstOrDefault(id => id > 0 && id != currentUserId);
            if (secondMembersId == 0) return false;
            if (blockedUsersIds.Any(id => id == secondMembersId)) return true;

            // Worst case - we need to fetch the other member to know if they don't block the current user.
            HttpClientResult<User> secondMemeberFetchResut = await _twilioClient.UserFetchAsync(secondMembersId.ToString());
            if (!secondMemeberFetchResut.IsSuccess)
            {
                Trace.WriteLine(
                    $"Failed to fetch the second channel member with ID  {secondMembersId}; reason: {secondMemeberFetchResut.FormattedMessage}; HTTP status code: {secondMemeberFetchResut.HttpStatusCode}.");
                // If something failed better not to change anything.
                return true;
            }

            var blockedBySecondMember = secondMemeberFetchResut.Payload.Attributes?.BlockedUsers;
            if (blockedBySecondMember == null || blockedBySecondMember.Length == 0) return false;
            return blockedBySecondMember.Any(id => id == currentUserId);
        }

        private string UpdateAttributesBlockageFields(string sourceAttributes, bool toBlock)
        {
            return sourceAttributes
                .Replace($"\"isBlocked\":{(!toBlock).ToString().ToLower()}", $"\"isBlocked\":{toBlock.ToString().ToLower()}")
                .Replace($"\"isListingBlocked\":{(!toBlock).ToString().ToLower()}", $"\"isListingBlocked\":{toBlock.ToString().ToLower()}")
                .Replace($"\"state\":{(toBlock ? (int)ListingState.Active : (int)ListingState.Blocked)}", $"\"state\":{(toBlock ? (int)ListingState.Blocked : (int)ListingState.Active)}");
        }

        private int[] ReadBlockListingsIdsFromFile(string fileName)
        {
            if (!File.Exists(fileName)) return Array.Empty<int>();

           string[] readResult = File.ReadAllLines(fileName);
           return readResult.Select(s => Int32.TryParse(s, out int listingId) ? listingId : 0).Where(id => id > 0).ToArray();
        }
    }
}