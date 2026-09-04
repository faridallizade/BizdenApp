#!/usr/bin/env bash
set -euo pipefail

if ! docker info >/dev/null 2>&1; then
  echo "Docker Desktop is not running. Start it and run this script again."
  exit 1
fi

docker compose up --build --detach

echo ""
echo "Bizdən local stack is running:"
echo "  Web:        http://localhost:55173"
echo "  API:        http://localhost:55080"
echo "  API health: http://localhost:55080/health"
echo "  PostgreSQL: localhost:55432 (database: bizden_dev)"
echo ""
echo "Logs: docker compose logs --follow"
