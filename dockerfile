FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build-env
WORKDIR /app

RUN apt-get update && apt-get install -y g++ curl cmake

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish ./src/Serval/src/Serval.ApiServer/Serval.ApiServer.csproj -c Release -o out_api_server
RUN dotnet publish ./src/Echo/src/EchoTranslationEngine/EchoTranslationEngine.csproj -c Release -o out_echo_server

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy as production
WORKDIR /app
COPY --from=build-env /app/out_api_server ./api_server
COPY --from=build-env /app/out_echo_server ./echo_server

CMD ["bash"]