#!/bin/bash
set -e

CONFIG_DIR="/root/.config/Readarr"
CONFIG_FILE="${CONFIG_DIR}/config.xml"

# Create config directory if it doesn't exist
mkdir -p "${CONFIG_DIR}"

# If config.xml doesn't exist, copy the default template
if [ ! -f "${CONFIG_FILE}" ]; then
    echo "First run detected - creating default config.xml with authentication disabled..."
    cp /app/config.xml.template "${CONFIG_FILE}"
    echo "Config created at ${CONFIG_FILE}"
fi

# Execute the main application
exec dotnet Readarr.dll "$@"
