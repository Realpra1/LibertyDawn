#!/bin/sh
ENGINEDIR=$(dirname "$0")
if command -v mono >/dev/null 2>&1 && [ "$(grep -c .NETCoreApp,Version= ${ENGINEDIR}/bin/OpenRA.dll)" = "0" ]; then
	RUNTIME_LAUNCHER="mono --debug"
else
	RUNTIME_LAUNCHER="dotnet"
fi

if command -v python3 >/dev/null 2>&1; then
	 LAUNCHPATH=$(python3 -c "import os; print(os.path.realpath('$0'))")
else
	 LAUNCHPATH=$(python -c "import os; print(os.path.realpath('$0'))")
fi

# LibertyDawn supports the CNC mod only. Explicit engine arguments remain
# available for development, but the normal launcher always selects CNC.
MODARG='Game.Mod=cnc'
for ARG in "$@"; do
	case "${ARG}" in
		Game.Mod=ra|Game.Mod=d2k|Game.Mod=ts)
			echo "LibertyDawn supports only Game.Mod=cnc."
			exit 2
			;;
	esac
done

# Launch the engine with the appropriate arguments
${RUNTIME_LAUNCHER} ${ENGINEDIR}/bin/OpenRA.dll Engine.EngineDir=".." Engine.LaunchPath="${LAUNCHPATH}" ${MODARG} "$@"

# Show a crash dialog if something went wrong
if [ $? != 0 ] && [ $? != 1 ]; then
	if [ "$(uname -s)" = "Darwin" ]; then
		LOGS="${HOME}/Library/Application Support/OpenRA/Logs/"
	else
		LOGS="${XDG_CONFIG_HOME:-${HOME}/.config}/openra/Logs"
		if [ ! -d "${LOGS}" ] && [ -d "${HOME}/.openra/Logs" ]; then
			LOGS="${HOME}/.openra/Logs"
		fi
	fi

	test -d Support/Logs && LOGS="${PWD}/Support/Logs"
	ERROR_MESSAGE="OpenRA has encountered a fatal error.\nPlease refer to the crash logs and FAQ for more information.\n\nLog files are located in ${LOGS}\nThe FAQ is available at http://wiki.openra.net/FAQ"
	if command -v zenity > /dev/null; then
		zenity --no-wrap --error --title "OpenRA" --text "${ERROR_MESSAGE}" 2> /dev/null
	elif command -v kdialog > /dev/null; then
		kdialog --title "OpenRA" --error "${ERROR_MESSAGE}"
	else
		echo "${ERROR_MESSAGE}"
	fi
	exit 1
fi
