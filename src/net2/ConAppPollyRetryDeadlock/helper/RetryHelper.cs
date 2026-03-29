using System;
using System.Data.SqlClient;
using System.Threading;

namespace ConAppPollyRetryDeadlock
{
	internal static class RetryHelper
	{
		private const int MaxRetries = 3;
		private const int DelayMs = 500;

		// Numéro d'erreur SQL Server pour deadlock victim
		private const int SqlDeadlockError = 1205;

		public static void ExecuteWithRetry(string operationName, Action operation)
		{
			int attempt = 0;

			while (true)
			{
				attempt++;
				try
				{
					Console.WriteLine($"  [{operationName}] Tentative {attempt}/{MaxRetries}...");
					operation();
					Console.WriteLine($"  [{operationName}] Succès à la tentative {attempt}.");
					return;
				}
				catch (SqlException ex) when (ex.Number == SqlDeadlockError)
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"  [{operationName}] DEADLOCK détecté (tentative {attempt}).");
					Console.ResetColor();

					if (attempt >= MaxRetries)
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine($"  [{operationName}] Maximum de retries atteint. Abandon.");
						Console.ResetColor();
						throw;
					}

					int delay = DelayMs * attempt; // back-off exponentiel simple
					Console.WriteLine($"  [{operationName}] Retry dans {delay}ms...");
					Thread.Sleep(delay);
				}
			}
		}
	}
}
