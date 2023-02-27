# Serval

Serval is a REST API for natural language processing services.

## Development

### CSharpier

All C# code should be formatted using [CSharpier](https://csharpier.com/). The best way to enable support for CSharpier is to install the appropriate [IDE extension](https://csharpier.com/docs/Editors) and configure it to format on save.

### Development locally

- Install MongoDB 4.2 and MongoDBCompass and run it on localhost:27017
  - Create the following folders:
  - C:\var\lib\machine\data
  - C:\var\lib\machine\machine
- set the following environment variables:
  - ASPNETCORE_ENVIRONMENT=Development
- Open "Machine.sln" and debug the ApiServer
- Now, you are running the complete environment where everything is being debugged and the mongodb is exposed.


### Development in Docker Compose

Following [this guide](https://stackoverflow.com/questions/55485511/how-to-run-dotnet-dev-certs-https-trust):

- install git and add to path (this will also add openssh)
- create "C:\usr\local\ca-certificates"
- copy docker/development/machine_api.conf into the above folder
- `openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout machine_api.key -out machine_api.crt -config machine_api.conf`
- `openssl pkcs12 -export -out machine_api.pfx -inkey machine_api.key -in machine_api.crt`

### Minikube

#### Installation

- Install docker, minikube and helm
- Run: `minikube addons enable ingress` to install ingress
- Create folder `C:/usr/local`

#### Startup

- Run `minikube start`
- In a new window, run `minikube mount C:\usr\local:/host`
- In a new window, run `minikube dashboard` (this will keep running - do it in a separate cmd window)
- Run `kubectl config use-context minikube`
- Run `cd deploy`
- Run `helm install machine-api . -f dev-values.yaml`

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
- Run `cd deploy`
- Run `helm upgrade machine-api . -f qa-values.yaml`

## API BDD Testing
- Prepare VSC env: follow this guide: https://docs.specflow.org/projects/specflow/en/latest/vscode/vscode-specflow.html
- Get auth0 token using curl:
  - Get Client ID and Client Secret from auth0.com
    - Login, go to Applications-> Applications -> "Machine API (Test Application)", or similar
    - Copy `Client ID` into Environment variable `MACHINE_CLIENT_ID`
    - Copy `Client Secret` into Environment variable `MACHINE_CLIENT_SECRET`
  - Run tests from `SIL.Machine.WebApi.SpecFlowTests`
    - The token will automatically be retrieved from Auth0 when you run the tests 