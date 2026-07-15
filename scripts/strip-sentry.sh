#!/bin/bash
# Remove Sentry from NzbDroneLogger.cs
FILE="NzbDrone.Common/Instrumentation/NzbDroneLogger.cs"
python3 << 'PYEOF'
import re
f = "NzbDrone.Common/Instrumentation/NzbDroneLogger.cs"
c = open(f).read()
# Remove using statement
c = re.sub(r'using NzbDrone\.Common\.Instrumentation\.Sentry;\n', '', c)
# Remove RegisterSentry call
c = re.sub(r'\s*RegisterSentry\(updateApp, appFolderInfo\);\n', '', c)
# Remove RegisterSentry method (from line 65 to line 93 inclusive)
c = re.sub(r'        private static void RegisterSentry\(bool updateClient, IAppFolderInfo appFolderInfo\)\n        \{\n.*?        \}\n', '', c, flags=re.DOTALL)
open(f, 'w').write(c)
PYEOF

# Remove Sentry from InitializeLogger.cs
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs
sed -i '/var sentryTarget/,/}/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs

# Remove Sentry from ReconfigureLogging.cs
sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
sed -i '/ReconfigureSentry();/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs
python3 << 'PYEOF'
import re
f = "NzbDrone.Core/Instrumentation/ReconfigureLogging.cs"
c = open(f).read()
c = re.sub(r'\s*private void ReconfigureSentry\(\)\s*\{.*?\s*\}\n', '', c, flags=re.DOTALL)
open(f, 'w').write(c)
PYEOF
