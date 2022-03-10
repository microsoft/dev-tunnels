#!/bin/bash
source $( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )/common/helpers.sh

pushd "$REPO_ROOT/src/Tunnel/"

BUILD_FILTER="/p:BuildFilter='TunnelService TokenService'"

echo "Restore Solution"
execute "dotnet restore /m /v:m $BUILD_FILTER $REPO_ROOT/dirs.proj"

echo "Build Solution"
execute "dotnet build --no-restore --configuration Release /m /v:m $BUILD_FILTER $REPO_ROOT/dirs.proj"

popd
