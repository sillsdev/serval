# Serval

Serval is a REST API for natural language processing services.

## Development

### CSharpier

All C# code should be formatted using [CSharpier](https://csharpier.com/). The best way to enable support for CSharpier is to install the appropriate [IDE extension](https://csharpier.com/docs/Editors) and configure it to format on save.

### Development locally

- Install MongoDB 6.0 as a replca set and MongoDBCompass and run it on localhost:27017
  - Create the following folders:
  - C:\var\lib\machine\data
  - C:\var\lib\machine\machine
- set the following environment variables:
  - ASPNETCORE_ENVIRONMENT=Development
- Open "Machine.sln" and debug the ApiServer
- Now, you are running the complete environment where everything is being debugged and the mongodb is exposed.

### local dev in ubuntu
* Make sure you install dotnet from the microsoft apr-get repo, otherwise debugging doesn't work!  Also you will need to "searchMicrosoftSymbolServer" for the symbols.
* To have a local MongoDB, use the docker-compose script to create a replica set of one instance.

### Development in Docker Compose

* Build this repository with dotnet build
* Download this repository and place the https://github.com/sillsdev/machine repo in ../machine, relative to this repo.
* Build the machine repo with dotnet build in the root of that repo
* In the serval root, run docker-compose up
* To debug serval and echo together, launch "ServalComb" in VSCode

#### Installation

- Install docker and helm
#### Update with new yaml's:

- Run `helm upgrade machine-api . -f dev-values.yaml`

#### To expose a port and see it in your browser:

- Run: `minikube service machine-api --url`
- In `C:\Windows\System32\drivers\etc\hosts`, enter in a line for `127.0.0.1 machine-api.vcap.me`
- Put the following in a browser: `http://machine-api.vcap.me:<port visible>/swagger`

#### Pod logs:

- Run: `kubectl get pods` to get the currently running pods
- Run: `kubectl logs <pod name>`

### Dallas Rancher

This is the QA staging environment. To access it,

#### To access machine API

- Use the VPN

* In `C:\Windows\System32\drivers\etc\hosts`, enter in a line for `10.3.0.119 machine-api.org`

- go to `https://machine-api.org/swagger` and accept the secuirty warning

#### To update the cluster

- Add the dallas-rke KubeConfig to your kubectl configs
- Run `kubectl config use-context dallas-rke`
- To upgrade mongo:
  - For QA internal Run `helm upgrade mongo deploy_mongo -n nlp`
  - For QA external Run `helm upgrade mongo deploy_mongo -n serval`
- To upgrade serval:
  - For QA internal Run `helm upgrade serval-api deploy -f deploy/qa-int-values.yaml -n nlp`
  - For QA external Run `helm upgrade serval-api deploy -f deploy/qa-ext-values.yaml -n serval`

## API BDD Testing
- Prepare VSC env: follow this guide: https://docs.specflow.org/projects/specflow/en/latest/vscode/vscode-specflow.html
- Get auth0 token using curl:
  - Get Client ID and Client Secret from auth0.com
    - Login, go to Applications-> Applications -> "Machine API (Test Application)", or similar
    - Copy `Client ID` into Environment variable `MACHINE_CLIENT_ID`
    - Copy `Client Secret` into Environment variable `MACHINE_CLIENT_SECRET`
  - Run tests from `Serval.SpecFlowTests`
    - The token will automatically be retrieved from Auth0 when you run the tests