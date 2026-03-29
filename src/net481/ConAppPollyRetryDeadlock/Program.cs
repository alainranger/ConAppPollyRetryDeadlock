using System;
using System.Threading;

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
			Console.WriteLine("=== Démo Deadlock SQL Server ===\n");

			Console.WriteLine("-- Initialisation de la base de données...");
			DatabaseSetup.Initialize(ConnStr);
			Console.WriteLine("-- Tables créées et données insérées.\n");

			Exception exA = null;
			Exception exB = null;

			// Chaque retry recrée un simulateur (nouvelles barrières)
			var threadA = new Thread(() =>
			{
				try
				{
					RetryHelper.ExecuteWithRetry("Thread A", () =>
					{
						// Simulateur partagé pour CE retry uniquement
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

			Console.WriteLine("-- Lancement des deux threads...\n");
			threadA.Start();
			threadB.Start();
			threadA.Join();
			threadB.Join();

			Console.WriteLine("\n-- Résultat final --");
			if (exA != null) Console.WriteLine("Thread A a échoué : " + exA.Message);
			if (exB != null) Console.WriteLine("Thread B a échoué : " + exB.Message);
			if (exA == null && exB == null)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("Les deux transactions ont réussi.");
				Console.ResetColor();
			}

			Console.WriteLine("\nAppuie sur une touche pour quitter...");
			Console.ReadKey();
		}
	}
}
