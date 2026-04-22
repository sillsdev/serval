FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build-env
WORKDIR /app

RUN apt-get update && apt-get install -y g++ curl cmake

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish ./src/Serval/src/Serval.ApiServer/Serval.ApiServer.csproj -c Release -o out_api_server

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS production
# libgomp needed for thot
RUN apt-get update && apt-get install -y libgomp1
WORKDIR /app
COPY --from=build-env /app/out_api_server ./api_server

CMD ["bash"]