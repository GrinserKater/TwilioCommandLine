using System;
using CommandManager.Enums;

namespace CommandManager
{
    public class ExecutionOptions
    {
        public ExecutionAction ExecutionAction { get; set; }
        public int PageSize { get; set; }
        public int ResourceLimit { get; set; }
        public bool LogToFile { get; set; }
        public bool ConsiderBlockedListings { get; set; }
        public string BlockedListingsFileName { get; set; }
        public int UserId { get; set; }
        public string ChannelUniqueIdentifier { get; set; }
        public DateTime? DateBefore { get; set; }
        public DateTime? DateAfter { get; set; }

        public bool IsEmpty => ExecutionAction == ExecutionAction.Undefined && PageSize == 0;
        public static ExecutionOptions Empty => new ExecutionOptions { ExecutionAction = ExecutionAction.Undefined };
    }
}
