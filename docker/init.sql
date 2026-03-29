IF DB_ID('DeadlockDemo') IS NULL
    CREATE DATABASE DeadlockDemo;
GO

USE DeadlockDemo;
GO

IF OBJECT_ID('Comptes',   'U') IS NULL
    CREATE TABLE Comptes (
        Id    INT PRIMARY KEY,
        Nom   NVARCHAR(50),
        Solde DECIMAL(18,2)
    );

IF OBJECT_ID('Commandes', 'U') IS NULL
    CREATE TABLE Commandes (
        Id       INT PRIMARY KEY,
        ClientId INT,
        Montant  DECIMAL(18,2)
    );

IF NOT EXISTS (SELECT 1 FROM Comptes WHERE Id = 1)
    INSERT INTO Comptes   VALUES (1, 'Alice', 1000.00);

IF NOT EXISTS (SELECT 1 FROM Commandes WHERE Id = 1)
    INSERT INTO Commandes VALUES (1, 1, 250.00);