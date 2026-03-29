using System;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace ConAppPollyRetryDeadlock
{
	internal class Program
	{
		private const string ConnStr =
			"Server=localhost,1433;Database=master;" +
			"User Id=sa;Password=Demo@1234!;" +
			"TrustServerCertificate=True;";

		static void Main(string[] args)
		{
			using (var loggerFactory = LoggerFactory.Create(builder =>
				builder
					.SetMinimumLevel(LogLevel.Debug)
					.AddSimpleConsole(opts => { opts.SingleLine = true; })))
			{
				var logger = loggerFactory.CreateLogger<Program>();
				RetryHelper.Configure(loggerFactory.CreateLogger(nameof(RetryHelper)));

				logger.LogInformation("=== Démo Deadlock SQL Server ===");
				logger.LogInformation("-- Initialisation de la base de données...");
				DatabaseSetup.Initialize(ConnStr);
				logger.LogInformation("-- Tables créées et données insérées.");

				Exception exA = null;
				Exception exB = null;

				var threadA = new Thread(() =>
				{
					try
					{
						RetryHelper.ExecuteWithRetry("Thread A", () =>
						{
							var sim = DeadlockSimulator.CreateAndStart(ConnStr, isA: true);
							sim.RunTransactionA();
						});
					}
					catch (Exception ex) { exA = ex; }
				});

				var threadB = new Thread(() =>
				{
					try
					{
						RetryHelper.ExecuteWithRetry("Thread B", () =>
						{
							var sim = DeadlockSimulator.CreateAndStart(ConnStr, isA: false);
							sim.RunTransactionB();
						});
					}
					catch (Exception ex) { exB = ex; }
				});

				logger.LogInformation("-- Lancement des deux threads...");
				threadA.Start();
				threadB.Start();
				threadA.Join();
				threadB.Join();

				logger.LogInformation("-- Résultat final --");
				if (exA != null) logger.LogError("Thread A a échoué : {Message}", exA.Message);
				if (exB != null) logger.LogError("Thread B a échoué : {Message}", exB.Message);
				if (exA == null && exB == null)
					logger.LogInformation("Les deux transactions ont réussi.");
			}

			Console.WriteLine("\nAppuie sur une touche pour quitter...");
			Console.ReadKey();
		}
	}
}
