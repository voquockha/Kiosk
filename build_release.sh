#!/bin/bash

rm -rf output/

mkdir -p output/

dotnet clean
# Build the release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./output/kiosk_device

# Zip the release
zip -r ./output/kiosk_device.zip ./output/kiosk_device

# Upload the release to the server
# scp ./output/kiosk_device.zip root@192.168.1.100:/var/www/html/release.zip