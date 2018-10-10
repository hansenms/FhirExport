# FHIR Exporting tools

This repo contains some example tools for querying and exporting data from a FHIR server. 

## Build

```
cd src\FhirExport
dotnet build
```

## Run

```
dotnet run /FhirServerUrl=https://my-fhir-server-url /FhirQuery=/Observation?subject=Patient/110bcd25-XXXX-XXXX-XXXX-1297901ea002
```