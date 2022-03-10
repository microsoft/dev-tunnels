#!/bin/bash

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "${SCRIPT_DIR}/../../" && pwd )"

execute() {
    echo $1
    bash -c "$1"
    exitCode=$?
    if [ $exitCode -ne 0 ]; then
        echo "Error running command '$1': Failed with exit code $exitCode"
        exit $exitCode
    fi
}
