# Démarrer
docker compose up -d

# Attendre que le health check soit "healthy"
docker compose ps

# Arrêter (garde les données)
docker compose down

# Arrêter + effacer les données (reset propre pour démo)
docker compose down -v