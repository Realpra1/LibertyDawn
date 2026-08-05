#!/bin/sh
ENGINEDIR=$(dirname "$0")
case "${1}" in
	ra|d2k|ts)
		echo "LibertyDawn utilities support only the cnc mod."
		exit 2
		;;
esac

if command -v mono >/dev/null 2>&1 && [ "$(grep -c .NETCoreApp,Version= ${ENGINEDIR}/bin/OpenRA.Utility.dll)" = "0" ]; then
	RUNTIME_LAUNCHER="mono --debug"
else
	RUNTIME_LAUNCHER="dotnet"
fi

ENGINE_DIR=.. ${RUNTIME_LAUNCHER} ${ENGINEDIR}/bin/OpenRA.Utility.dll "$@"
