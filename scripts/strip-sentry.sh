#!/bin/bash
# Remove Sentry from NzbDroneLogger.cs - delete RegisterSentry method (lines 65-93)
# and the call (line 42) and using (line 9)
FILE="NzbDrone.Common/Instrumentation/NzbDroneLogger.cs"
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' "$FILE"
sed -i '/RegisterSentry(updateApp, appFolderInfo);/d' "$FILE"
# Delete the RegisterSentry method - use python for reliable multi-line delete
python3 << 'PYEOF'
lines = open("NzbDrone.Common/Instrumentation/NzbDroneLogger.cs").readlines()
out = []
skip = False
for line in lines:
    if "private static void RegisterSentry(" in line:
        skip = True
    if skip and line.strip() == "}" and not any(c in line for c in ["if", "else", "try", "catch", "for", "while", "switch"]):
        skip = False
        continue
    if not skip:
        out.append(line)
open("NzbDrone.Common/Instrumentation/NzbDroneLogger.cs", "w").writelines(out)
PYEOF

# Remove Sentry from InitializeLogger.cs
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs
python3 << 'PYEOF'
lines = open("NzbDrone.Common/Instrumentation/InitializeLogger.cs").readlines()
out = []
skip = False
for line in lines:
    if "var sentryTarget" in line:
        skip = True
    if skip and line.strip() == "}":
        skip = False
        continue
    if not skip:
        out.append(line)
open("NzbDrone.Common/Instrumentation/InitializeLogger.cs", "w").writelines(out)
PYEOF

# Remove Sentry from ReconfigureLogging.cs
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
sed -i '/ReconfigureSentry();/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
python3 << 'PYEOF'
lines = open("NzbDrone.Core/Instrumentation/ReconfigureLogging.cs").readlines()
out = []
skip = False
for line in lines:
    if "private void ReconfigureSentry()" in line:
        skip = True
    if skip and line.strip() == "}" and "ReconfigureSentry" not in line:
        skip = False
        continue
    if not skip:
        out.append(line)
open("NzbDrone.Core/Instrumentation/ReconfigureLogging.cs", "w").writelines(out)
PYEOF
