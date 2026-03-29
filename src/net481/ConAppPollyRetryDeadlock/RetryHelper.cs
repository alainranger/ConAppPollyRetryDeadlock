using System;
using System.Data.SqlClient;

using Polly;
using Polly.Retry;

namespace ConAppPollyRetryDeadlock
{
	public static class RetryHelper
	{
		private const int SqlDeadlockError = 1205;

		private static readonly RetryPolicy DeadlockPolicy =
			Policy
				.Handle<SqlException>(ex => ex.Number == SqlDeadlockError)
				.WaitAndRetry(
					retryCount: 3,
					sleepDurationProvider: attempt =>
						TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), // 400ms, 800ms, 1600ms
					onRetry: (exception, delay, attempt, context) =>
					{
						var name = context.ContainsKey("name") ? context["name"] : "?";
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine(
							$"  [{name}] DEADLOCK — tentative {attempt}, retry dans {delay.TotalMilliseconds}ms");
						Console.ResetColor();
					});

		public static void ExecuteWithRetry(string operationName, Action operation)
		{
			var context = new Context { ["name"] = operationName };

			Console.WriteLine($"  [{operationName}] Démarrage...");

			DeadlockPolicy.Execute(ctx => operation(), context);

			Console.WriteLine($"  [{operationName}] Succès.");
		}
	}
}
