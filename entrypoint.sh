#!/bin/sh
set -e

# The Fly.io persistent volume is mounted at /data after image layers are applied.
# Its ownership defaults to root on first mount. Fix it so appuser can write the
# SQLite database (and Hangfire storage, when reminders are enabled).
chown -R appuser:appuser /data 2>/dev/null || true

# Drop to non-root user and exec the app (replaces this shell process).
exec su-exec appuser dotnet Moeltid.dll
