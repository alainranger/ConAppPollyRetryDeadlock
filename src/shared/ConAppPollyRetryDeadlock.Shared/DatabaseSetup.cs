using System.Data.SqlClient;

namespace ConAppPollyRetryDeadlock
{
	public static class DatabaseSetup
	{
		public static void Initialize(string connectionString)
		{
			using (var conn = new SqlConnection(connectionString))
			{
				conn.Open();

				var sql = @"
                    IF OBJECT_ID('Comptes',   'U') IS NOT NULL DROP TABLE Comptes;
                    IF OBJECT_ID('Commandes', 'U') IS NOT NULL DROP TABLE Commandes;

                    CREATE TABLE Comptes (
                        Id      INT PRIMARY KEY,
                        Nom     NVARCHAR(50),
                        Solde   DECIMAL(18,2)
                    );

                    CREATE TABLE Commandes (
                        Id      INT PRIMARY KEY,
                        ClientId INT,
                        Montant  DECIMAL(18,2)
                    );

                    INSERT INTO Comptes   VALUES (1, 'Alice', 1000.00);
                    INSERT INTO Commandes VALUES (1, 1, 250.00);
                ";

				using (var cmd = new SqlCommand(sql, conn))
					cmd.ExecuteNonQuery();
			}
		}
	}
}
