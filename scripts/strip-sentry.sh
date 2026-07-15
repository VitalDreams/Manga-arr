#!/bin/bash
# Remove Sentry from source files
# NzbDroneLogger.cs: remove using (line 9), call (line 42), and method (lines 65-93)
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/NzbDroneLogger.cs
sed -i '/RegisterSentry(updateApp, appFolderInfo);/d' NzbDrone.Common/Instrumentation/NzbDroneLogger.cs
sed -i '65,93d' NzbDrone.Common/Instrumentation/NzbDroneLogger.cs

# InitializeLogger.cs: remove using and sentryTarget block
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs
sed -i '/var sentryTarget/,/}/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs

# ReconfigureLogging.cs: remove using, call, and method
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
sed -i '/ReconfigureSentry();/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
# Remove ReconfigureSentry method (starts at "private void ReconfigureSentry()" and ends at next "}" at same indent)
python3 -c "
lines = open('NzbDrone.Core/Instrumentation/ReconfigureLogging.cs').readlines()
out, skip, depth = [], False, 0
for line in lines:
    if 'private void ReconfigureSentry()' in line:
        skip, depth = True, 0
    if skip:
        depth += line.count('{') - line.count('}')
        if depth <= 0 and '{' not in line:
            skip = False
            continue
    if not skip:
        out.append(line)
open('NzbDrone.Core/Instrumentation/ReconfigureLogging.cs', 'w').writelines(out)
"
