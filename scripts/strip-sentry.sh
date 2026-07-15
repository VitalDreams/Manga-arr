#!/bin/bash
# Remove Sentry from NzbDroneLogger.cs
FILE="NzbDrone.Common/Instrumentation/NzbDroneLogger.cs"
python3 -c "
import re
f='$FILE'
c=open(f).read()
c=re.sub(r'using NzbDrone\.Common\.Instrumentation\.Sentry;\n','',c)
c=re.sub(r'\s*RegisterSentry\(updateApp, appFolderInfo\);\n','',c)
c=re.sub(r'\s*private static void RegisterSentry\(.*?\n\s*\}\n','',c,flags=re.DOTALL)
open(f,'w').write(c)
"
# Remove Sentry from InitializeLogger.cs
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs
sed -i '/var sentryTarget/,/}/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs
# Remove Sentry from ReconfigureLogging.cs
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
sed -i '/ReconfigureSentry();/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
sed -i '/private void ReconfigureSentry/,/^[[:space:]]*private void SetSyslog/ { /^[[:space:]]*private void SetSyslog/!d; }' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
