using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace ConAppPollyRetryDeadlock
{
	public class DeadlockSimulator
	{
		private readonly string _connectionString;

		private static readonly object _syncLock = new object();
		private static ManualResetEvent _roundGate = new ManualResetEvent(false);
		private static int _signalsInRound = 0;
		private const int BarrierWaitTimeoutMs = 1000;

		private DeadlockSimulator(string connectionString)
		{
			_connectionString = connectionString;
		}

		public static DeadlockSimulator CreateAndStart(string connectionString, bool isA)
		{
			if (isA)
			{
				lock (_syncLock)
				{
					_roundGate = new ManualResetEvent(false);
					_signalsInRound = 0;
				}
				Console.WriteLine("  [Sync] Événements réinitialisés pour ce round.");
			}
			else
			{
				Thread.Sleep(50);
			}
			return new DeadlockSimulator(connectionString);
		}

		private static void WaitForPeer(string threadName)
		{
			ManualResetEvent gate;
			lock (_syncLock)
			{
				gate = _roundGate;
				_signalsInRound++;
				if (_signalsInRound >= 2)
				{
					gate.Set();
				}
			}

			if (!gate.WaitOne(BarrierWaitTimeoutMs, false))
			{
				Console.WriteLine("[" + threadName + "] Aucun pair détecté, poursuite sans synchronisation.");
			}
		}

		public void RunTransactionA()
		{
			using (var conn = new SqlConnection(_connectionString))
			{
				conn.Open();
				using (var tx = conn.BeginTransaction(IsolationLevel.Serializable))
				{
					Console.WriteLine("[Thread A] Verrou sur Comptes...");
					new SqlCommand("UPDATE Comptes SET Solde = Solde - 50 WHERE Id = 1", conn, tx).ExecuteNonQuery();
					Console.WriteLine("[Thread A] Comptes verrouillé. Signal envoyé.");

					WaitForPeer("Thread A");

					Console.WriteLine("[Thread A] Tente de verrouiller Commandes...");
					new SqlCommand("UPDATE Commandes SET Montant = Montant + 50 WHERE Id = 1", conn, tx).ExecuteNonQuery();

					tx.Commit();
					Console.WriteLine("[Thread A] Commit.");
				}
			}
		}

		public void RunTransactionB()
		{
			using (var conn = new SqlConnection(_connectionString))
			{
				conn.Open();
				using (var tx = conn.BeginTransaction(IsolationLevel.Serializable))
				{
					Console.WriteLine("[Thread B] Verrou sur Commandes...");
					new SqlCommand("UPDATE Commandes SET Montant = Montant - 10 WHERE Id = 1", conn, tx).ExecuteNonQuery();
					Console.WriteLine("[Thread B] Commandes verrouillé. Signal envoyé.");

					WaitForPeer("Thread B");

					Console.WriteLine("[Thread B] Tente de verrouiller Comptes...");
					new SqlCommand("UPDATE Comptes SET Solde = Solde + 10 WHERE Id = 1", conn, tx).ExecuteNonQuery();

					tx.Commit();
					Console.WriteLine("[Thread B] Commit.");
				}
			}
		}
	}
}
