Add-Type -AssemblyName System.IO.Compression.FileSystem
function Unzip
{
    param([string]$zipfile, [string]$outpath)

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

$activity = Get-Content .\activity.json | ConvertFrom-Json 
$activityProps = $activity.typeProperties.extendedProperties

$invocation = (Get-Variable MyInvocation).Value
$directorypath = Split-Path $invocation.MyCommand.Path
$zippath = $directorypath + '\publish.zip'
Unzip $zippath $directorypath

$auth = $activityProps.AzureAD_Authority
$clientId = $activityProps.AzureAD_ClientId
$clientSecret = $activityProps.AzureAD_ClientSecret
$aud = $activityProps.AzureAD_Audience
$con = $activityProps.StorageConnectionString
$folder = $activityProps.BlobFolder
$fhirServerUrl = $activityProps.FhirServerUrl
$fhirQuery = $activityProps.FhirQuery
$OutputFile = $activityProps.OutputFile

.\publish\FhirExport.exe /AzureAD_Authority=$auth /AzureAD_ClientId=$clientId /AzureAD_ClientSecret=$clientSecret /AzureAD_Audience=$aud /StorageConnectionString=$con /BlobFolder=$folder /FhirServerUrl=$fhirServerUrl /FhirQuery=$fhirQuery /OutputFile=$OutputFile
