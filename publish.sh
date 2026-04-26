#!/bin/sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

mkdir -p $SCRIPT_DIR/dist

linux(){
    dotnet publish src/SPDPatcher/ -c Release -r linux-x64 --property:PublishDir=$SCRIPT_DIR/publish/linux-x64/SPDPatcher --property:PublishSingleFile=true --property:SelfContained=true
    tar -czvf $SCRIPT_DIR/dist/linux-x64.tar.gz -C $SCRIPT_DIR/publish/linux-x64 .
}

windows(){
    dotnet publish src/SPDPatcher/ -c Release -r win-x64 --property:PublishDir=$SCRIPT_DIR/publish/win-x64/SPDPatcher --property:PublishSingleFile=true --property:SelfContained=true
    7z a $SCRIPT_DIR/dist/win-x64.zip $SCRIPT_DIR/publish/win-x64/* -mx0
}

if [ "$#" -eq 0 ]; then
    echo "Usage: $0 {linux|windows}"
    exit 1
fi

TARGETS=""

for arg in "$@"; do
    case $arg in
        linux)
            TARGETS="$TARGETS linux"
            ;;
        windows)
            TARGETS="$TARGETS windows"
            ;;
        all)
            TARGETS="$TARGETS linux windows"
            ;;
        *)
            echo "Usage: $0 {linux|windows}"
            exit 1
    esac
done

for target in $TARGETS; do
    $target
done