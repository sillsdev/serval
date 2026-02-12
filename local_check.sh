#!/bin/bash
dotnet tool restore
dotnet restore
dotnet csharpier check .
if [ $? -ne 0 ]; then
  exit 1
fi
dotnet build --no-restore --no-incremental -c Release
if [ $? -ne 0 ]; then
  exit 1
fi
docker compose --project-name serval down -t 2
docker compose -f docker-compose.mongo.yml up >/dev/null &
dotnet test --verbosity normal --filter "TestCategory!=E2E&TestCategory!=E2EMissingServices"
