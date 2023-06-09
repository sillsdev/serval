# Serval

Serval is a REST API for natural language processing services.

## Development

### CSharpier

All C# code should be formatted using [CSharpier](https://csharpier.com/). The best way to enable support for CSharpier is to install the appropriate [IDE extension](https://csharpier.com/docs/Editors) and configure it to format on save.

### Development locally

- Install MongoDB 6.0 as a replca set and MongoDBCompass and run it on localhost:27017
- set the following environment variables:
  - ASPNETCORE_ENVIRONMENT=Development
- Open "Serval.sln" and debug the ApiServer
- Now, you are running the complete environment where everything is being debugged and the mongodb is exposed.

#### local dev in ubuntu
* Make sure you install dotnet from the microsoft apr-get repo, otherwise debugging doesn't work!  Also you will need to "searchMicrosoftSymbolServer" for the symbols.
* To have a local MongoDB, use the docker-compose script to create a replica set of one instance.

### Development in Docker Compose

* Build this repository with dotnet build
* Download this repository and place the https://github.com/sillsdev/machine repo in ../machine, relative to this repo.
* Build the machine repo with dotnet build in the root of that repo
* In the serval root, run docker-compose up
* To debug serval and echo together, launch "ServalComb" in VSCode

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
- To upgrade serval:
  - For QA internal Run:
    - `kubectl config use-context dallas-rke`
    - `helm upgrade serval deploy/serval -n nlp -f deploy/qa-int-values.yaml`
  - For QA external Run:
    - `kubectl config use-context dallas-rke`
    - `helm upgrade serval deploy/serval -n serval -f deploy/qa-ext-values.yaml`
  - For Production Run:
    - `kubectl config use-context aws-rke`
    - `helm upgrade serval deploy/serval -n serval -f deploy/values.yaml`

### To access Serval API

* Internal QA:
  * Use the VPN
  * In `C:\Windows\System32\drivers\etc\hosts`, enter in a line for `10.3.0.119 serval-api.org`
  * go to `https://machine-api.org/swagger` and accept the security warning
* External QA:
  * go to `https://qa.serval-api.org/swagger` and accept the security warning

### Pod logs:

- Run: `kubectl get pods` to get the currently running pods
- Run: `kubectl logs <pod name>`
- Run: `kubectl describe pod  <pod name>` to check a stalled pod stuck in containercreating

### API E2E Testing
- Get auth0 token using curl:
  - Get Client ID and Client Secret from auth0.com
    - Login, go to Applications-> Applications -> "Machine API (Test Application)", or similar
    - Copy `Client ID` into Environment variable `SERVAL_CLIENT_ID`
    - Copy `Client Secret` into Environment variable `SERVAL_CLIENT_SECRET`
    - Copy the auth0 url into Environment variable `SERVAL_AUTH_URL`
    - Set `SERVAL_HOST_URL` to the api's URL
  - Run tests from `Serval.E2ETests`
    - The token will automatically be retrieved from Auth0 when you run the tests

### Access S3 bucket

- to view files, run `aws s3 ls s3://aqua-ml-data/<deployment environment>`