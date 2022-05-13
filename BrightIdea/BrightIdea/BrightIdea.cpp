#pragma warning(disable: 6031)

#include "windows.h"
#include <stdio.h>
#include <physicalmonitorenumerationapi.h>
#include <highlevelmonitorconfigurationapi.h>
#include <iostream>
#include <string>

HANDLE hMonitor;

VOID CALLBACK TimerRoutine(PVOID lpParam, BOOLEAN /*TimerOrWaitFired*/)
{
    int delta = *(int *)lpParam;

    DWORD min = 0, cur = 0, max = 0;
    if (GetMonitorBrightness(hMonitor, &min, &cur, &max))
    {
        // Set monitor brightness takes about 50 milliseconds to return. There are other functions
		// that can tweak the color temperature, contrast, RGB drive, and RGB gain, but they are
		// similarly slow. https://docs.microsoft.com/en-us/windows/win32/api/_monitor/
		SetMonitorBrightness(hMonitor, cur + delta);
		SetMonitorBrightness(hMonitor, cur);
    }
}

int main(int argc, char *argv[])
{
    if (argc != 3)
    {
		std::cout << "Usage: brightidea <period_in_ms> <delta>" << std::endl;
		std::cout << "Example: brightidea 1000 -40" << std::endl;
        return 0;
    }

    int period_ms = atoi(argv[1]);
    int delta = atoi(argv[2]);

    std::cout << "Move this window to the monitor(s) you want to manipulate." << std::endl;
    std::cout << "Press <enter> to begin.";
    std::getchar();

    HWND win_handle = GetConsoleWindow();
    HMONITOR hMon = MonitorFromWindow(win_handle, MONITOR_DEFAULTTONEAREST);

    if (hMon == nullptr)
        return -1;

    DWORD num_of_monitors;
    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, &num_of_monitors))
        return -2;

    LPPHYSICAL_MONITOR pPhysical_monitors = (LPPHYSICAL_MONITOR)malloc(num_of_monitors * sizeof(PHYSICAL_MONITOR));
    if (pPhysical_monitors == nullptr)
        return -3;

    if (!GetPhysicalMonitorsFromHMONITOR(hMon, num_of_monitors, pPhysical_monitors))
        return -4;

    hMonitor = pPhysical_monitors[0].hPhysicalMonitor;

    HANDLE hTimerQueue = CreateTimerQueue();
    if (NULL == hTimerQueue)
    {
        printf("CreateTimerQueue failed (%d)\n", GetLastError());
        return -5;
    }

    HANDLE hTimer = NULL;
    if (!CreateTimerQueueTimer(&hTimer, hTimerQueue, TimerRoutine, &delta, 0, period_ms, 0))
    {
        printf("CreateTimerQueueTimer failed (%d)\n", GetLastError());
        return -6;
    }

    std::cout << "Running... Press <enter> to exit. ";
    std::getchar();

    return 0;
}
