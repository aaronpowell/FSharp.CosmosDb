#!/usr/bin/env bash

set -euxo pipefail

ipAddress=https://$(docker inspect fsharp_cosmos -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'):8081

# Try to get the emulator cert in a loop
until sudo curl -ksf "${ipAddress}/_explorer/emulator.pem" -o '/usr/local/share/ca-certificates/emulator.crt'; do
  echo "Downloading cert from $ipAddress"
  sleep 1
done

sudo update-ca-certificates

if [ ! -f ./samples/FSharp.CosmosDb.Samples/appsettings.Development.json ]
then
  echo '{ "Cosmos": { "EndPoint" : "'$ipAddress'" } }' >> ./samples/FSharp.CosmosDb.Samples/appsettings.Development.json
fi
