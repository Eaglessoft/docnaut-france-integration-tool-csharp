#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
dotnet build Docnaut.FranceConnector.slnx -c Release --nologo
dotnet run --project Docnaut.FranceConnector.FeatureTests -c Release --no-build
dotnet pack Docnaut.FranceConnector.Sdk -c Release --no-build -o artifacts
