#!/usr/bin/env bash

set -euxo pipefail

# Try to get the emulator cert in a loop
until sudo curl -ksf "${COSMOS__ENDPOINT}/_explorer/emulator.pem" -o '/usr/local/share/ca-certificates/emulator.crt'; do
  echo "Downloading cert from $COSMOS__ENDPOINT"
  sleep 1
done

sudo update-ca-certificates
