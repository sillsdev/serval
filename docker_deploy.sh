docker compose --file ./docker-compose.yml --project-name serval down -t 2
dotnet build ../machine
dotnet build .
docker compose -f ./docker-compose.yml up