# ConAppPollyRetryDeadlock

Démo interactive d'un **deadlock SQL Server** et de sa résolution par **retry automatique**, disponible en deux versions : `.NET Framework 2.0` (retry manuel) et `.NET Framework 4.8.1` (retry via [Polly](https://github.com/App-vNext/Polly)).

---

## Table des matières

1. [Prérequis](#prérequis)
2. [Structure du projet](#structure-du-projet)
3. [Démarrage rapide](#démarrage-rapide)
4. [Qu'est-ce qu'un deadlock ?](#quest-ce-quun-deadlock-)
5. [Comment la démo provoque un deadlock](#comment-la-démo-provoque-un-deadlock)
6. [Stratégie de retry](#stratégie-de-retry)
7. [Sortie console attendue](#sortie-console-attendue)

---

## Prérequis

- Docker Desktop
- .NET Framework 4.8.1 SDK (pour `net481`)
- .NET Framework 2.0 / Visual Studio (pour `net2`)

---

## Structure du projet

```
ConAppPollyRetryDeadlock/
│
├── docker/
│   └── docker-compose.yml          # SQL Server 2022 en conteneur
│
├── src/
│   ├── shared/
│   │   └── ConAppPollyRetryDeadlock.Shared/
│   │       ├── DatabaseSetup.cs    # Création des tables et données de démo
│   │       └── DeadlockSimulator.cs# Logique des transactions A et B + synchro
│   │
│   ├── net481/
│   │   └── ConAppPollyRetryDeadlock/
│   │       ├── Program.cs          # Point d'entrée .NET 4.8.1
│   │       └── RetryHelper.cs      # Retry via Polly + ILogger
│   │
│   └── net2/
│       └── ConAppPollyRetryDeadlock/
│           ├── Program.cs          # Point d'entrée .NET 2.0
│           └── helper/
│               └── RetryHelper.cs  # Retry manuel (boucle while)
```

---

## Démarrage rapide

### 1. Lancer SQL Server

```powershell
docker compose -f docker/docker-compose.yml up -d

# Attendre que le health check soit "healthy"
docker compose -f docker/docker-compose.yml ps
```

### 2. Lancer la démo

Compiler et exécuter le projet `net481` ou `net2` depuis Visual Studio, ou :

```powershell
dotnet run --project src/net481/ConAppPollyRetryDeadlock/ConAppPollyRetryDeadlock.csproj
```

### 3. Arrêter

```powershell
# Garde les données
docker compose -f docker/docker-compose.yml down

# Reset complet (supprime les données)
docker compose -f docker/docker-compose.yml down -v
```

---

## Qu'est-ce qu'un deadlock ?

Un **deadlock** (ou interblocage) survient quand deux transactions se bloquent mutuellement en attendant chacune une ressource verrouillée par l'autre.

### Visualisation

```
  Transaction A                        Transaction B
  ─────────────────                    ─────────────────
  🔒 Verrouille Comptes                🔒 Verrouille Commandes
       │                                    │
       │   ┌────────────────────────────────┘
       │   │     Les deux attendent...
       └───┤────────────────────────────────┐
           │                                │
       ❌ Veut Commandes              ❌ Veut Comptes
       (déjà pris par B)             (déjà pris par A)

              ☠️  DEADLOCK — cycle d'attente infini
```

SQL Server détecte ce cycle et choisit automatiquement une **victime** (la transaction au coût de rollback le plus faible). Cette victime reçoit l'erreur **1205** :

> `Transaction (Process ID xx) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.`

---

## Comment la démo provoque un deadlock

### Données initiales

| Table      | Id | Données            |
|------------|----|--------------------|
| `Comptes`  | 1  | Alice — 1 000,00 € |
| `Commandes`| 1  | Commande — 250,00 €|

### Séquence chronologique

```
Temps  Thread A (RunTransactionA)          Thread B (RunTransactionB)
────── ──────────────────────────────────  ────────────────────────────────────
  T1   BEGIN TX (Serializable)             BEGIN TX (Serializable)
  T2   UPDATE Comptes  → 🔒 verrou IX/X    UPDATE Commandes → 🔒 verrou IX/X
  T3        [SignalAndWait — barrière]           [SignalAndWait — barrière]
            Les deux arrivent ici ↑↑↑
  T4   UPDATE Commandes → ⏳ attend B      UPDATE Comptes   → ⏳ attend A
  T5                   ☠️  SQL Server détecte le deadlock
  T6   ✅ COMMIT                           ❌ Erreur 1205 (victime choisie)
```

### Pourquoi `IsolationLevel.Serializable` ?

Avec `Serializable`, SQL Server pose des **verrous de plage** (range locks) qui ne sont pas relâchés avant le COMMIT. Cela garantit qu'une fois `Comptes` verrouillé par A, B ne peut pas l'acquérir — condition nécessaire pour reproduire le deadlock de façon déterministe.

### Rôle de la barrière (`ManualResetEvent`)

`DeadlockSimulator` utilise un `ManualResetEvent` partagé pour synchroniser les deux threads :

```
Thread A ──── UPDATE Comptes ──── SignalAndWait ─────────────────── UPDATE Commandes ──▶
                                       │ ↑ gate ouvre quand          ▲
                                       │   les 2 ont signalé         │ bloqué par B
Thread B ──── UPDATE Commandes ── SignalAndWait ─────────────────── UPDATE Comptes ───▶
                                             ↑ bloqué par A
```

Sans cette barrière, un thread pourrait terminer avant que l'autre ne pose son premier verrou, et le deadlock ne se produirait pas.

Au **retry**, un seul thread rejoue sa transaction. La barrière expire après 1 000 ms (`BarrierWaitTimeoutMs`) et le thread continue seul — le deadlock ne peut plus se produire.

---

## Stratégie de retry

### .NET 4.8.1 — Polly `WaitAndRetry`

```csharp
Policy
    .Handle<SqlException>(ex => ex.Number == 1205)
    .WaitAndRetry(
        retryCount: 3,
        sleepDurationProvider: attempt =>
            TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)) // 400 ms, 800 ms, 1 600 ms
    );
```

| Tentative | Délai avant retry |
|-----------|-------------------|
| 1 → 2     | 400 ms            |
| 2 → 3     | 800 ms            |
| 3 → 4     | 1 600 ms          |

### .NET 2.0 — Boucle `while` manuelle

```csharp
int delay = DelayMs * attempt; // 500 ms, 1 000 ms, 1 500 ms
Thread.Sleep(delay);
```

---

## Sortie console attendue

```
info: Program[0] === Démo Deadlock SQL Server ===
info: Program[0] -- Initialisation de la base de données...
info: Program[0] -- Tables créées et données insérées.
info: Program[0] -- Lancement des deux threads...
info: RetryHelper[0] [Thread A] Démarrage...
info: RetryHelper[0] [Thread B] Démarrage...
  [Sync] Événements réinitialisés pour ce round.
[Thread A] Verrou sur Comptes...
[Thread B] Verrou sur Commandes...
[Thread A] Comptes verrouillé. Signal envoyé.
[Thread B] Commandes verrouillé. Signal envoyé.
[Thread A] Tente de verrouiller Commandes...
[Thread B] Tente de verrouiller Comptes...
warn: RetryHelper[0] [Thread B] DEADLOCK — tentative 1, retry dans 400ms
  [Sync] Événements réinitialisés pour ce round.
[Thread A] Commit.
info: RetryHelper[0] [Thread A] Succès.
[Thread B] Verrou sur Commandes...
[Thread B] Commandes verrouillé. Signal envoyé.
[Thread B] Aucun pair détecté, poursuite sans synchronisation.
[Thread B] Tente de verrouiller Comptes...
[Thread B] Commit.
info: RetryHelper[0] [Thread B] Succès.
info: Program[0] Les deux transactions ont réussi.