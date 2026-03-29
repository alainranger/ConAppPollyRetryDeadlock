using System;
using System.Data.SqlClient;
using System.Threading;

namespace ConAppPollyRetryDeadlock
{
	internal delegate void Operation();

	internal static class RetryHelper
	{
		private const int MaxRetries = 3;
		private const int DelayMs = 500;
		private const int SqlDeadlockError = 1205;

		public static void ExecuteWithRetry(string operationName, Operation operation)
		{
			int attempt = 0;

			while (true)
			{
				attempt++;
				try
				{
					Console.WriteLine("  [" + operationName + "] Tentative " + attempt + "/" + MaxRetries + "...");
					operation();
					Console.WriteLine("  [" + operationName + "] Succès à la tentative " + attempt + ".");
					return;
				}
				catch (SqlException ex)
				{
					if (ex.Number != SqlDeadlockError) throw;

					Console.WriteLine("  [" + operationName + "] DEADLOCK détecté (tentative " + attempt + ").");

					if (attempt >= MaxRetries)
					{
						Console.WriteLine("  [" + operationName + "] Maximum de retries atteint. Abandon.");
						throw;
					}

					int delay = DelayMs * attempt;
					Console.WriteLine("  [" + operationName + "] Retry dans " + delay + "ms...");
					Thread.Sleep(delay);
				}
			}
		}
	}
}
