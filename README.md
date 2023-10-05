[![Integration & Unit Tests](https://github.com/sillsdev/serval/actions/workflows/ci.yml/badge.svg)](https://github.com/sillsdev/serval/actions/workflows/ci.yml)
[![End-To-End Tests](https://github.com/sillsdev/serval/actions/workflows/ci-e2e.yml/badge.svg)](https://github.com/sillsdev/serval/actions/workflows/ci-e2e.yml)
[![codecov](https://codecov.io/gh/sillsdev/serval/graph/badge.svg?token=0PEQ9LXPK9)](https://codecov.io/gh/sillsdev/serval)

# Serval

Serval is a REST API for natural language processing services.

## Development

### Development in Docker Compose

This is the simplest way to develop.
First, git clone the repo:
  ```
  git clone https://github.com/sillsdev/serval.git
  ```
Then build Serval:
  ```
  dotnet build serval
  ```
Clone the [Machine repo](https://github.com/sillsdev/machine) into an adjacent folder to Serval:
  ```
  git clone https://github.com/sillsdev/machine.git
  ```
Build the Machine repo:
  ```
  dotnet build machine
  ```
Make sure that the environment variable ASPNETCORE_ENVIRONMENT is set to "Development" by running `export ASPNETCORE_ENVIRONMENT=Development` or adding it to your `.bashrc`.
In the Serval root, run docker compose up
  ```
  cd serval && docker compose up
  ```
If using vscode, launch "DockerComb" to debug Serval and the for-testing-only Echo Engine.

### Development locally
Alternatively, you can develop without containerizing Serval.

Install MongoDB 6.0 as a replica set run it on localhost:27017. (You can run `docker compose -f "docker-compose.mongo.yaml" up` from the root of the serval repo to do so).

Make sure that the environment variable ASPNETCORE_ENVIRONMENT is set to "Development" by running `export ASPNETCORE_ENVIRONMENT=Development` or adding it to your `.bashrc`.

Open "Serval.sln" and debug the ApiServer.

## Deployment on kubernetes
There are 3 different environments that Serval is deployed to:
- Internal QA for testing out the deployment
- External QA for clients to test new updates before production
- Production
### To deploy the cluster
- Add the dallas-rke KubeConfig to your kubectl configs
- Run `kubectl config use-context dallas-rke`
- First, startup the storage (using internal qa for example)
- `helm install serval-pvc deploy/serval-pvc -n nlp -f deploy/qa-int-values.yaml`
- Then, startup the database (give it 60 seconds)
- `helm install mongo deploy/mongo -n nlp -f deploy/qa-int-values.yaml`
- Now you can turn on Serval
- `helm install serval deploy/serval -n nlp -f deploy/qa-int-values.yaml`

### To update the cluster
- To upgrade Serval:
  - For QA internal Run:
    - `kubectl config use-context dallas-rke`
    - `helm upgrade serval deploy/serval -n nlp -f deploy/qa-int-values.yaml`
  - For QA external Run:
    - `kubectl config use-context dallas-rke`
    - `helm upgrade serval deploy/serval -n serval -f deploy/qa-ext-values.yaml`
  - For Production Run:
    - `kubectl config use-context aws-rke`
    - `helm upgrade serval deploy/serval -n serval -f deploy/values.yaml`

## Debugging
### To access Serval API
* Internal QA:
  * Use the VPN
  * In `C:\Windows\System32\drivers\etc\hosts`, enter in a line for `10.3.0.119 serval-api.org`
  * go to `https://machine-api.org/swagger` and accept the security warning
* External QA:
  * go to `https://qa.serval-api.org/swagger` and accept the security warning

### To view pod logs:
- Run: `kubectl get pods` to get the currently running pods
- Run: `kubectl logs <pod name>`
- Run: `kubectl describe pod  <pod name>` to check a stalled pod stuck in containercreating

### Running the API E2E Tests
In order to run the E2E tests, you will need to have the appropriate credentials
- Get Client ID and Client Secret from auth0.com
  - Login, go to Applications-> Applications -> "Machine API (Test Application)" or similar
  - Copy `Client ID` into Environment variable `SERVAL_CLIENT_ID`
  - Copy `Client Secret` into Environment variable `SERVAL_CLIENT_SECRET`
  - Copy the auth0 url into Environment variable `SERVAL_AUTH_URL` (e.g. `SERVAL_AUTH_URL=https://sil-appbuilder.auth0.com`)
  - Set `SERVAL_HOST_URL` to the api's URL (e.g. `SERVAL_HOST_URL=http://localhost`)
Now, when you run the tests from `Serval.E2ETests`, the token will automatically be retrieved from Auth0.

### Debugging the S3 bucket

To view files stored in the bucket, run
  ```
  aws s3 ls s3://aqua-ml-data/<deployment environment>
  ```
### Mongo debugging

* First, get the mongo pod name: `kubectl get pods -n serval`
* Then forward to a local port, such as 28015: `kubectl port-forward <pod name> 28015:27017 -n serval`
* Download [MongoDB Compass](https://www.mongodb.com/try/download/compass).
* Then, open MongoDB Compass and connect to `mongodb://localhost:28015/?directConnection=true`


## Development Notes
### CSharpier

All C# code should be formatted using [CSharpier](https://csharpier.com/). The best way to enable support for CSharpier is to install the appropriate [IDE extension](https://csharpier.com/docs/Editors) and configure it to format on save.

### Coding conventions

[Here](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names) is a good overview of naming conventions. [Here](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) is a good overview of coding conventions. If you want to get in to even more detail, check out the [Framework design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/).

