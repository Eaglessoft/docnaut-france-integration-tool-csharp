#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
export DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1
export DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1
export DOTNET_USE_POLLING_FILE_WATCHER=1
exec dotnet watch --no-hot-reload --project Docnaut.FranceConnector.TestTool run --no-launch-profile -- --urls http://0.0.0.0:8083
