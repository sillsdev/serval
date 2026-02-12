# Serval API Example

This example application will generate a pre-translation USFM draft using the Serval API, and display it in the terminal window.

## Pre-Requisites

 * .NET SDK 10.0
 * You must have a Serval Client ID and Client Secret before running this example.

## Setup

Before running, you must configure your Serval Client Id and Client Secret via `dotnet user-secrets`:
```
dotnet user-secrets set "Serval:ClientId" "your_client_id_here"
dotnet user-secrets set "Serval:ClientSecret" "your_client_secret_here"
```

## Run

To run this example after configuring your user secrets, execute the following command from a terminal window:

```
dotnet run
```
