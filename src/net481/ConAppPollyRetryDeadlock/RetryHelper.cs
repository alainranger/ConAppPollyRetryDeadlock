using System;
using System.Data.SqlClient;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace ConAppPollyRetryDeadlock
{
	public static class RetryHelper
	{
		private const int SqlDeadlockError = 1205;

		private static ILogger _logger = new LoggerFactory().CreateLogger(nameof(RetryHelper));

		public static void Configure(ILogger logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		private static RetryPolicy BuildPolicy() =>
			Policy
				.Handle<SqlException>(ex => ex.Number == SqlDeadlockError)
				.WaitAndRetry(
					retryCount: 3,
					sleepDurationProvider: attempt =>
						TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)),
					onRetry: (exception, delay, attempt, context) =>
					{
						var name = context.ContainsKey("name") ? context["name"] : "?";
						_logger.LogWarning(
							"[{Name}] DEADLOCK — tentative {Attempt}, retry dans {DelayMs}ms",
							name, attempt, delay.TotalMilliseconds);
					});

		public static void ExecuteWithRetry(string operationName, Action operation)
		{
			var context = new Context { ["name"] = operationName };

			_logger.LogInformation("[{OperationName}] Démarrage...", operationName);

			BuildPolicy().Execute(ctx => operation(), context);

			_logger.LogInformation("[{OperationName}] Succès.", operationName);
		}
	}
}
