#!/usr/bin/env bash

set -euxo pipefail

COSMOS__ENDPOINT=https://$(docker inspect cosmos -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'):8081

# Try to get the emulator cert in a loop
until sudo curl -ksf "${COSMOS__ENDPOINT}/_explorer/emulator.pem" -o '/usr/local/share/ca-certificates/emulator.crt'; do
  echo "Downloading cert from $COSMOS__ENDPOINT"
  sleep 1
done

sudo update-ca-certificates

echo '{ "Cosmos": { "EndPoint" : "'$COSMOS__ENDPOINT'" } }' >> ./samples/FSharp.CosmosDb.Samples/appsettings.Development.json