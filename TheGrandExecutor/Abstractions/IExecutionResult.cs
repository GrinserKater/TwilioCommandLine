﻿using System.Collections.Generic;

namespace TheGrandExecutor.Abstractions
{
	public interface IExecutionResult<T>
	{
		int FetchedCount { get; }
		int SuccessCount { get; }
		int SkippedCount { get; }
		int FailedCount { get; }
		List<string> ErrorMessages { get; }
		string Message { get; set; }

       List<T> EntitiesFetched { get; }
       List<T> EntitiesSucceeded { get; }
       List<T> EntitiesFailed { get; }
       List<T> EntitiesSkipped { get; }
	}
}
