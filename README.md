[![Integration & Unit Tests](https://github.com/sillsdev/serval/actions/workflows/ci.yml/badge.svg)](https://github.com/sillsdev/serval/actions/workflows/ci.yml)
[![End-To-End Tests](https://github.com/sillsdev/serval/actions/workflows/ci-e2e.yml/badge.svg)](https://github.com/sillsdev/serval/actions/workflows/ci-e2e.yml)
[![codecov](https://codecov.io/gh/sillsdev/serval/graph/badge.svg?token=0PEQ9LXPK9)](https://codecov.io/gh/sillsdev/serval)

# Serval - an API supporting Computer Aided Translation for all the languages of the world!

Serval is a REST API for natural language processing services for **all languages**.
For the REST documentation use the Swagger site [here](https://prod.serval-api.org/swagger/index.html).

# Serval Architecture

Serval is designed using a microservice architecture with the following deployments:
* Serval.APIServer
  * This is the top level API layer.  All REST calls are made through this layer.
  * It primarily handles the definition of files and corpora, per-client permissions, validation of requests and assembling NLP requests from files and database entries, such as inserting pretranslations into the USFM structure.
  * This includes all components under `./src/Serval/src` except for the auto-generated Client and gRPC projects, which are used to handle communication with the API layer.
* Serval.Machine.EngineServer
  * Exposes multiple engine types to the APIServer
  * Can be directly invoked by the APIServer
  * Handles short-lived NLP requests, including loading SMT models for on-demand word graphs.
  * Queues up jobs for the JobServer
  * Primary functionality is defined in Serval.Machine.Shared, with the deployment configuration is defined in Serval.Machine.EngineServer
  * Reports back status to the APILayer over gRPC
* Serval.Machine.JobServer
  * Executes Hangfire NLP jobs
  * Queues up ClearML NLP jobs, using the ClearML API and the S3 bucket
  * Preprocesses training and inferencing data
  * Postprocesses inferencing results and models from the ClearML job
  * Primary functionality is defined in Serval.Machine.Shared, with the deployment configuration is defined in Serval.Machine.JobServer
  * Reports back status to the APILayer over gRPC
* Echo.EchoTranslationEngine
  * The echo engine is for testing both the API layer and the deployment in general.
Other components of the microservice are:
* SIL.DataAccess
  * Abstracts all MongoDB operations
  * Enables in-memory database for testing purposes
  * Replicates the functionality of [EF Core](https://learn.microsoft.com/en-us/ef/core/)
* ServiceToolkit
  * Defines common models, configurations and services both used by the Serval.APIServal deployment and the Serval.Machine deployments.


# Development

## Setting up Your Environment

* Use VS Code with all the recommended extensions
* Development is supported in Ubuntu and Windows WSL2
  * For Ubuntu, use [microsoft's distribution of .net](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu)
  * Ubuntu 22.04 and 24.04 are currently supported
* Install the repositories:
  * To develop Serval, you will also likely need to make changes to the [Machine repo](https://github.com/sillsdev/machine) as well - they are intricately tied.
  * To enable Serval to use your current edits in Machine (rather than the nuget package) you need to install Machine in an adjacent folder to Serval
    * i.e., if your serval repo is cloned into /home/username/repos/serval, machine should be in /home/username/repos/machine
  * Make sure that you build Machine using before you build Serval

## Option 1: Docker Compose local testing deployment

These instructions are for developing/testing using docker-compose (rather than locally/bare metal)
With both the serval and machine repos installed, in the serval root folder, run `./docker_deploy.sh`
To debug in VSCode, launch "DockerComb" after to containers come up (about 5 -10 seconds).  This will allow you to debug all 4 serval containers.

## Option 2: Bare metal local testing deployment
Alternatively, you can develop without containerizing Serval.

Install MongoDB 8.0 as a replica set run it on localhost:27017. (You can run `docker compose -f docker-compose.mongo.yml up` from the root of the serval repo to do so).

Make sure that the environment variable ASPNETCORE_ENVIRONMENT is set to "Development" by running `export ASPNETCORE_ENVIRONMENT=Development` or adding it to your `.bashrc`.

Open "Serval.sln" and debug the ApiServer.

## Coding guidelines

Coding guidelines are documented [on the wiki](https://github.com/sillsdev/serval/wiki/Development-Guide)

## (Optional) Get your machine.py images setup
When jobs are run, they are queued up on ClearML.  If you want to have your own agents for integration testing (and you have a GPU with 24GB RAM), you can do the following:
* clone the [machine.py repo](https://github.com/sillsdev/machine.py)
* Build the docker image with `docker build . -t local.mpy` for a GPU image or `docker build . -f dockerfile.cpu_only -t local.mpy.cpu_only` for a CPU only image.
* Register your machine as a ClearML agent (see dev team for details)
  * Make sure you do NOT "always pull the image"!  The images you are building are stored locally.
* Set the following environment variables:
```
export MACHINE_PY_IMAGE=local.mpy
export MACHINE_PY_CPU_IMAGE=local.mpy.cpu_only
```

### Running the API E2E Tests
In order to run the E2E tests, you will need to have the appropriate credentials
- Get Client ID and Client Secret from auth0.com
  - Login, go to Applications-> Applications -> "Machine API (Test Application)" or similar
  - Copy `Client ID` into Environment variable `SERVAL_CLIENT_ID`
  - Copy `Client Secret` into Environment variable `SERVAL_CLIENT_SECRET`
  - Copy the auth0 url into Environment variable `SERVAL_AUTH_URL` (e.g. `SERVAL_AUTH_URL=https://sil-appbuilder.auth0.com`)
  - Set `SERVAL_HOST_URL` to the api's URL (e.g. `SERVAL_HOST_URL=http://localhost`)
Now, when you run the tests from `Serval.E2ETests`, the token will automatically be retrieved from Auth0.

## Special thanks to

BugSnag for error reporting:

<a href="https://www.bugsnag.com/">
   <img src="https://www.bugsnag.com/wp-content/uploads/2023/06/63bc40cd9d502eda8ea74ce7_Bugsnag-Full-Color-1.svg" alt="bugsnag" width="200"/>
</a>
