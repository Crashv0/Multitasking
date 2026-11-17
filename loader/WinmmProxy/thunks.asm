EXTERN g_winmm:QWORD
.code
Thunk_CloseDriver PROC
    jmp QWORD PTR [g_winmm + 0]
Thunk_CloseDriver ENDP
Thunk_DefDriverProc PROC
    jmp QWORD PTR [g_winmm + 8]
Thunk_DefDriverProc ENDP
Thunk_DriverCallback PROC
    jmp QWORD PTR [g_winmm + 16]
Thunk_DriverCallback ENDP
Thunk_DrvGetModuleHandle PROC
    jmp QWORD PTR [g_winmm + 24]
Thunk_DrvGetModuleHandle ENDP
Thunk_GetDriverModuleHandle PROC
    jmp QWORD PTR [g_winmm + 32]
Thunk_GetDriverModuleHandle ENDP
Thunk_OpenDriver PROC
    jmp QWORD PTR [g_winmm + 40]
Thunk_OpenDriver ENDP
Thunk_PlaySound PROC
    jmp QWORD PTR [g_winmm + 48]
Thunk_PlaySound ENDP
Thunk_PlaySoundA PROC
    jmp QWORD PTR [g_winmm + 56]
Thunk_PlaySoundA ENDP
Thunk_PlaySoundW PROC
    jmp QWORD PTR [g_winmm + 64]
Thunk_PlaySoundW ENDP
Thunk_SendDriverMessage PROC
    jmp QWORD PTR [g_winmm + 72]
Thunk_SendDriverMessage ENDP
Thunk_WOWAppExit PROC
    jmp QWORD PTR [g_winmm + 80]
Thunk_WOWAppExit ENDP
Thunk_auxGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 88]
Thunk_auxGetDevCapsA ENDP
Thunk_auxGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 96]
Thunk_auxGetDevCapsW ENDP
Thunk_auxGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 104]
Thunk_auxGetNumDevs ENDP
Thunk_auxGetVolume PROC
    jmp QWORD PTR [g_winmm + 112]
Thunk_auxGetVolume ENDP
Thunk_auxOutMessage PROC
    jmp QWORD PTR [g_winmm + 120]
Thunk_auxOutMessage ENDP
Thunk_auxSetVolume PROC
    jmp QWORD PTR [g_winmm + 128]
Thunk_auxSetVolume ENDP
Thunk_joyConfigChanged PROC
    jmp QWORD PTR [g_winmm + 136]
Thunk_joyConfigChanged ENDP
Thunk_joyGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 144]
Thunk_joyGetDevCapsA ENDP
Thunk_joyGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 152]
Thunk_joyGetDevCapsW ENDP
Thunk_joyGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 160]
Thunk_joyGetNumDevs ENDP
Thunk_joyGetPos PROC
    jmp QWORD PTR [g_winmm + 168]
Thunk_joyGetPos ENDP
Thunk_joyGetPosEx PROC
    jmp QWORD PTR [g_winmm + 176]
Thunk_joyGetPosEx ENDP
Thunk_joyGetThreshold PROC
    jmp QWORD PTR [g_winmm + 184]
Thunk_joyGetThreshold ENDP
Thunk_joyReleaseCapture PROC
    jmp QWORD PTR [g_winmm + 192]
Thunk_joyReleaseCapture ENDP
Thunk_joySetCapture PROC
    jmp QWORD PTR [g_winmm + 200]
Thunk_joySetCapture ENDP
Thunk_joySetThreshold PROC
    jmp QWORD PTR [g_winmm + 208]
Thunk_joySetThreshold ENDP
Thunk_mciDriverNotify PROC
    jmp QWORD PTR [g_winmm + 216]
Thunk_mciDriverNotify ENDP
Thunk_mciDriverYield PROC
    jmp QWORD PTR [g_winmm + 224]
Thunk_mciDriverYield ENDP
Thunk_mciExecute PROC
    jmp QWORD PTR [g_winmm + 232]
Thunk_mciExecute ENDP
Thunk_mciFreeCommandResource PROC
    jmp QWORD PTR [g_winmm + 240]
Thunk_mciFreeCommandResource ENDP
Thunk_mciGetCreatorTask PROC
    jmp QWORD PTR [g_winmm + 248]
Thunk_mciGetCreatorTask ENDP
Thunk_mciGetDeviceIDA PROC
    jmp QWORD PTR [g_winmm + 256]
Thunk_mciGetDeviceIDA ENDP
Thunk_mciGetDeviceIDFromElementIDA PROC
    jmp QWORD PTR [g_winmm + 264]
Thunk_mciGetDeviceIDFromElementIDA ENDP
Thunk_mciGetDeviceIDFromElementIDW PROC
    jmp QWORD PTR [g_winmm + 272]
Thunk_mciGetDeviceIDFromElementIDW ENDP
Thunk_mciGetDeviceIDW PROC
    jmp QWORD PTR [g_winmm + 280]
Thunk_mciGetDeviceIDW ENDP
Thunk_mciGetDriverData PROC
    jmp QWORD PTR [g_winmm + 288]
Thunk_mciGetDriverData ENDP
Thunk_mciGetErrorStringA PROC
    jmp QWORD PTR [g_winmm + 296]
Thunk_mciGetErrorStringA ENDP
Thunk_mciGetErrorStringW PROC
    jmp QWORD PTR [g_winmm + 304]
Thunk_mciGetErrorStringW ENDP
Thunk_mciGetYieldProc PROC
    jmp QWORD PTR [g_winmm + 312]
Thunk_mciGetYieldProc ENDP
Thunk_mciLoadCommandResource PROC
    jmp QWORD PTR [g_winmm + 320]
Thunk_mciLoadCommandResource ENDP
Thunk_mciSendCommandA PROC
    jmp QWORD PTR [g_winmm + 328]
Thunk_mciSendCommandA ENDP
Thunk_mciSendCommandW PROC
    jmp QWORD PTR [g_winmm + 336]
Thunk_mciSendCommandW ENDP
Thunk_mciSendStringA PROC
    jmp QWORD PTR [g_winmm + 344]
Thunk_mciSendStringA ENDP
Thunk_mciSendStringW PROC
    jmp QWORD PTR [g_winmm + 352]
Thunk_mciSendStringW ENDP
Thunk_mciSetDriverData PROC
    jmp QWORD PTR [g_winmm + 360]
Thunk_mciSetDriverData ENDP
Thunk_mciSetYieldProc PROC
    jmp QWORD PTR [g_winmm + 368]
Thunk_mciSetYieldProc ENDP
Thunk_midiConnect PROC
    jmp QWORD PTR [g_winmm + 376]
Thunk_midiConnect ENDP
Thunk_midiDisconnect PROC
    jmp QWORD PTR [g_winmm + 384]
Thunk_midiDisconnect ENDP
Thunk_midiInAddBuffer PROC
    jmp QWORD PTR [g_winmm + 392]
Thunk_midiInAddBuffer ENDP
Thunk_midiInClose PROC
    jmp QWORD PTR [g_winmm + 400]
Thunk_midiInClose ENDP
Thunk_midiInGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 408]
Thunk_midiInGetDevCapsA ENDP
Thunk_midiInGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 416]
Thunk_midiInGetDevCapsW ENDP
Thunk_midiInGetErrorTextA PROC
    jmp QWORD PTR [g_winmm + 424]
Thunk_midiInGetErrorTextA ENDP
Thunk_midiInGetErrorTextW PROC
    jmp QWORD PTR [g_winmm + 432]
Thunk_midiInGetErrorTextW ENDP
Thunk_midiInGetID PROC
    jmp QWORD PTR [g_winmm + 440]
Thunk_midiInGetID ENDP
Thunk_midiInGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 448]
Thunk_midiInGetNumDevs ENDP
Thunk_midiInMessage PROC
    jmp QWORD PTR [g_winmm + 456]
Thunk_midiInMessage ENDP
Thunk_midiInOpen PROC
    jmp QWORD PTR [g_winmm + 464]
Thunk_midiInOpen ENDP
Thunk_midiInPrepareHeader PROC
    jmp QWORD PTR [g_winmm + 472]
Thunk_midiInPrepareHeader ENDP
Thunk_midiInReset PROC
    jmp QWORD PTR [g_winmm + 480]
Thunk_midiInReset ENDP
Thunk_midiInStart PROC
    jmp QWORD PTR [g_winmm + 488]
Thunk_midiInStart ENDP
Thunk_midiInStop PROC
    jmp QWORD PTR [g_winmm + 496]
Thunk_midiInStop ENDP
Thunk_midiInUnprepareHeader PROC
    jmp QWORD PTR [g_winmm + 504]
Thunk_midiInUnprepareHeader ENDP
Thunk_midiOutCacheDrumPatches PROC
    jmp QWORD PTR [g_winmm + 512]
Thunk_midiOutCacheDrumPatches ENDP
Thunk_midiOutCachePatches PROC
    jmp QWORD PTR [g_winmm + 520]
Thunk_midiOutCachePatches ENDP
Thunk_midiOutClose PROC
    jmp QWORD PTR [g_winmm + 528]
Thunk_midiOutClose ENDP
Thunk_midiOutGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 536]
Thunk_midiOutGetDevCapsA ENDP
Thunk_midiOutGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 544]
Thunk_midiOutGetDevCapsW ENDP
Thunk_midiOutGetErrorTextA PROC
    jmp QWORD PTR [g_winmm + 552]
Thunk_midiOutGetErrorTextA ENDP
Thunk_midiOutGetErrorTextW PROC
    jmp QWORD PTR [g_winmm + 560]
Thunk_midiOutGetErrorTextW ENDP
Thunk_midiOutGetID PROC
    jmp QWORD PTR [g_winmm + 568]
Thunk_midiOutGetID ENDP
Thunk_midiOutGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 576]
Thunk_midiOutGetNumDevs ENDP
Thunk_midiOutGetVolume PROC
    jmp QWORD PTR [g_winmm + 584]
Thunk_midiOutGetVolume ENDP
Thunk_midiOutLongMsg PROC
    jmp QWORD PTR [g_winmm + 592]
Thunk_midiOutLongMsg ENDP
Thunk_midiOutMessage PROC
    jmp QWORD PTR [g_winmm + 600]
Thunk_midiOutMessage ENDP
Thunk_midiOutOpen PROC
    jmp QWORD PTR [g_winmm + 608]
Thunk_midiOutOpen ENDP
Thunk_midiOutPrepareHeader PROC
    jmp QWORD PTR [g_winmm + 616]
Thunk_midiOutPrepareHeader ENDP
Thunk_midiOutReset PROC
    jmp QWORD PTR [g_winmm + 624]
Thunk_midiOutReset ENDP
Thunk_midiOutSetVolume PROC
    jmp QWORD PTR [g_winmm + 632]
Thunk_midiOutSetVolume ENDP
Thunk_midiOutShortMsg PROC
    jmp QWORD PTR [g_winmm + 640]
Thunk_midiOutShortMsg ENDP
Thunk_midiOutUnprepareHeader PROC
    jmp QWORD PTR [g_winmm + 648]
Thunk_midiOutUnprepareHeader ENDP
Thunk_midiStreamClose PROC
    jmp QWORD PTR [g_winmm + 656]
Thunk_midiStreamClose ENDP
Thunk_midiStreamOpen PROC
    jmp QWORD PTR [g_winmm + 664]
Thunk_midiStreamOpen ENDP
Thunk_midiStreamOut PROC
    jmp QWORD PTR [g_winmm + 672]
Thunk_midiStreamOut ENDP
Thunk_midiStreamPause PROC
    jmp QWORD PTR [g_winmm + 680]
Thunk_midiStreamPause ENDP
Thunk_midiStreamPosition PROC
    jmp QWORD PTR [g_winmm + 688]
Thunk_midiStreamPosition ENDP
Thunk_midiStreamProperty PROC
    jmp QWORD PTR [g_winmm + 696]
Thunk_midiStreamProperty ENDP
Thunk_midiStreamRestart PROC
    jmp QWORD PTR [g_winmm + 704]
Thunk_midiStreamRestart ENDP
Thunk_midiStreamStop PROC
    jmp QWORD PTR [g_winmm + 712]
Thunk_midiStreamStop ENDP
Thunk_mixerClose PROC
    jmp QWORD PTR [g_winmm + 720]
Thunk_mixerClose ENDP
Thunk_mixerGetControlDetailsA PROC
    jmp QWORD PTR [g_winmm + 728]
Thunk_mixerGetControlDetailsA ENDP
Thunk_mixerGetControlDetailsW PROC
    jmp QWORD PTR [g_winmm + 736]
Thunk_mixerGetControlDetailsW ENDP
Thunk_mixerGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 744]
Thunk_mixerGetDevCapsA ENDP
Thunk_mixerGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 752]
Thunk_mixerGetDevCapsW ENDP
Thunk_mixerGetID PROC
    jmp QWORD PTR [g_winmm + 760]
Thunk_mixerGetID ENDP
Thunk_mixerGetLineControlsA PROC
    jmp QWORD PTR [g_winmm + 768]
Thunk_mixerGetLineControlsA ENDP
Thunk_mixerGetLineControlsW PROC
    jmp QWORD PTR [g_winmm + 776]
Thunk_mixerGetLineControlsW ENDP
Thunk_mixerGetLineInfoA PROC
    jmp QWORD PTR [g_winmm + 784]
Thunk_mixerGetLineInfoA ENDP
Thunk_mixerGetLineInfoW PROC
    jmp QWORD PTR [g_winmm + 792]
Thunk_mixerGetLineInfoW ENDP
Thunk_mixerGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 800]
Thunk_mixerGetNumDevs ENDP
Thunk_mixerMessage PROC
    jmp QWORD PTR [g_winmm + 808]
Thunk_mixerMessage ENDP
Thunk_mixerOpen PROC
    jmp QWORD PTR [g_winmm + 816]
Thunk_mixerOpen ENDP
Thunk_mixerSetControlDetails PROC
    jmp QWORD PTR [g_winmm + 824]
Thunk_mixerSetControlDetails ENDP
Thunk_mmDrvInstall PROC
    jmp QWORD PTR [g_winmm + 832]
Thunk_mmDrvInstall ENDP
Thunk_mmGetCurrentTask PROC
    jmp QWORD PTR [g_winmm + 840]
Thunk_mmGetCurrentTask ENDP
Thunk_mmTaskBlock PROC
    jmp QWORD PTR [g_winmm + 848]
Thunk_mmTaskBlock ENDP
Thunk_mmTaskCreate PROC
    jmp QWORD PTR [g_winmm + 856]
Thunk_mmTaskCreate ENDP
Thunk_mmTaskSignal PROC
    jmp QWORD PTR [g_winmm + 864]
Thunk_mmTaskSignal ENDP
Thunk_mmTaskYield PROC
    jmp QWORD PTR [g_winmm + 872]
Thunk_mmTaskYield ENDP
Thunk_mmioAdvance PROC
    jmp QWORD PTR [g_winmm + 880]
Thunk_mmioAdvance ENDP
Thunk_mmioAscend PROC
    jmp QWORD PTR [g_winmm + 888]
Thunk_mmioAscend ENDP
Thunk_mmioClose PROC
    jmp QWORD PTR [g_winmm + 896]
Thunk_mmioClose ENDP
Thunk_mmioCreateChunk PROC
    jmp QWORD PTR [g_winmm + 904]
Thunk_mmioCreateChunk ENDP
Thunk_mmioDescend PROC
    jmp QWORD PTR [g_winmm + 912]
Thunk_mmioDescend ENDP
Thunk_mmioFlush PROC
    jmp QWORD PTR [g_winmm + 920]
Thunk_mmioFlush ENDP
Thunk_mmioGetInfo PROC
    jmp QWORD PTR [g_winmm + 928]
Thunk_mmioGetInfo ENDP
Thunk_mmioInstallIOProcA PROC
    jmp QWORD PTR [g_winmm + 936]
Thunk_mmioInstallIOProcA ENDP
Thunk_mmioInstallIOProcW PROC
    jmp QWORD PTR [g_winmm + 944]
Thunk_mmioInstallIOProcW ENDP
Thunk_mmioOpenA PROC
    jmp QWORD PTR [g_winmm + 952]
Thunk_mmioOpenA ENDP
Thunk_mmioOpenW PROC
    jmp QWORD PTR [g_winmm + 960]
Thunk_mmioOpenW ENDP
Thunk_mmioRead PROC
    jmp QWORD PTR [g_winmm + 968]
Thunk_mmioRead ENDP
Thunk_mmioRenameA PROC
    jmp QWORD PTR [g_winmm + 976]
Thunk_mmioRenameA ENDP
Thunk_mmioRenameW PROC
    jmp QWORD PTR [g_winmm + 984]
Thunk_mmioRenameW ENDP
Thunk_mmioSeek PROC
    jmp QWORD PTR [g_winmm + 992]
Thunk_mmioSeek ENDP
Thunk_mmioSendMessage PROC
    jmp QWORD PTR [g_winmm + 1000]
Thunk_mmioSendMessage ENDP
Thunk_mmioSetBuffer PROC
    jmp QWORD PTR [g_winmm + 1008]
Thunk_mmioSetBuffer ENDP
Thunk_mmioSetInfo PROC
    jmp QWORD PTR [g_winmm + 1016]
Thunk_mmioSetInfo ENDP
Thunk_mmioStringToFOURCCA PROC
    jmp QWORD PTR [g_winmm + 1024]
Thunk_mmioStringToFOURCCA ENDP
Thunk_mmioStringToFOURCCW PROC
    jmp QWORD PTR [g_winmm + 1032]
Thunk_mmioStringToFOURCCW ENDP
Thunk_mmioWrite PROC
    jmp QWORD PTR [g_winmm + 1040]
Thunk_mmioWrite ENDP
Thunk_mmsystemGetVersion PROC
    jmp QWORD PTR [g_winmm + 1048]
Thunk_mmsystemGetVersion ENDP
Thunk_sndPlaySoundA PROC
    jmp QWORD PTR [g_winmm + 1056]
Thunk_sndPlaySoundA ENDP
Thunk_sndPlaySoundW PROC
    jmp QWORD PTR [g_winmm + 1064]
Thunk_sndPlaySoundW ENDP
Thunk_timeBeginPeriod PROC
    jmp QWORD PTR [g_winmm + 1072]
Thunk_timeBeginPeriod ENDP
Thunk_timeEndPeriod PROC
    jmp QWORD PTR [g_winmm + 1080]
Thunk_timeEndPeriod ENDP
Thunk_timeGetDevCaps PROC
    jmp QWORD PTR [g_winmm + 1088]
Thunk_timeGetDevCaps ENDP
Thunk_timeGetSystemTime PROC
    jmp QWORD PTR [g_winmm + 1096]
Thunk_timeGetSystemTime ENDP
Thunk_timeGetTime PROC
    jmp QWORD PTR [g_winmm + 1104]
Thunk_timeGetTime ENDP
Thunk_timeKillEvent PROC
    jmp QWORD PTR [g_winmm + 1112]
Thunk_timeKillEvent ENDP
Thunk_timeSetEvent PROC
    jmp QWORD PTR [g_winmm + 1120]
Thunk_timeSetEvent ENDP
Thunk_waveInAddBuffer PROC
    jmp QWORD PTR [g_winmm + 1128]
Thunk_waveInAddBuffer ENDP
Thunk_waveInClose PROC
    jmp QWORD PTR [g_winmm + 1136]
Thunk_waveInClose ENDP
Thunk_waveInGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 1144]
Thunk_waveInGetDevCapsA ENDP
Thunk_waveInGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 1152]
Thunk_waveInGetDevCapsW ENDP
Thunk_waveInGetErrorTextA PROC
    jmp QWORD PTR [g_winmm + 1160]
Thunk_waveInGetErrorTextA ENDP
Thunk_waveInGetErrorTextW PROC
    jmp QWORD PTR [g_winmm + 1168]
Thunk_waveInGetErrorTextW ENDP
Thunk_waveInGetID PROC
    jmp QWORD PTR [g_winmm + 1176]
Thunk_waveInGetID ENDP
Thunk_waveInGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 1184]
Thunk_waveInGetNumDevs ENDP
Thunk_waveInGetPosition PROC
    jmp QWORD PTR [g_winmm + 1192]
Thunk_waveInGetPosition ENDP
Thunk_waveInMessage PROC
    jmp QWORD PTR [g_winmm + 1200]
Thunk_waveInMessage ENDP
Thunk_waveInOpen PROC
    jmp QWORD PTR [g_winmm + 1208]
Thunk_waveInOpen ENDP
Thunk_waveInPrepareHeader PROC
    jmp QWORD PTR [g_winmm + 1216]
Thunk_waveInPrepareHeader ENDP
Thunk_waveInReset PROC
    jmp QWORD PTR [g_winmm + 1224]
Thunk_waveInReset ENDP
Thunk_waveInStart PROC
    jmp QWORD PTR [g_winmm + 1232]
Thunk_waveInStart ENDP
Thunk_waveInStop PROC
    jmp QWORD PTR [g_winmm + 1240]
Thunk_waveInStop ENDP
Thunk_waveInUnprepareHeader PROC
    jmp QWORD PTR [g_winmm + 1248]
Thunk_waveInUnprepareHeader ENDP
Thunk_waveOutBreakLoop PROC
    jmp QWORD PTR [g_winmm + 1256]
Thunk_waveOutBreakLoop ENDP
Thunk_waveOutClose PROC
    jmp QWORD PTR [g_winmm + 1264]
Thunk_waveOutClose ENDP
Thunk_waveOutGetDevCapsA PROC
    jmp QWORD PTR [g_winmm + 1272]
Thunk_waveOutGetDevCapsA ENDP
Thunk_waveOutGetDevCapsW PROC
    jmp QWORD PTR [g_winmm + 1280]
Thunk_waveOutGetDevCapsW ENDP
Thunk_waveOutGetErrorTextA PROC
    jmp QWORD PTR [g_winmm + 1288]
Thunk_waveOutGetErrorTextA ENDP
Thunk_waveOutGetErrorTextW PROC
    jmp QWORD PTR [g_winmm + 1296]
Thunk_waveOutGetErrorTextW ENDP
Thunk_waveOutGetID PROC
    jmp QWORD PTR [g_winmm + 1304]
Thunk_waveOutGetID ENDP
Thunk_waveOutGetNumDevs PROC
    jmp QWORD PTR [g_winmm + 1312]
Thunk_waveOutGetNumDevs ENDP
Thunk_waveOutGetPitch PROC
    jmp QWORD PTR [g_winmm + 1320]
Thunk_waveOutGetPitch ENDP
Thunk_waveOutGetPlaybackRate PROC
    jmp QWORD PTR [g_winmm + 1328]
Thunk_waveOutGetPlaybackRate ENDP
Thunk_waveOutGetPosition PROC
    jmp QWORD PTR [g_winmm + 1336]
Thunk_waveOutGetPosition ENDP
Thunk_waveOutGetVolume PROC
    jmp QWORD PTR [g_winmm + 1344]
Thunk_waveOutGetVolume ENDP
Thunk_waveOutMessage PROC
    jmp QWORD PTR [g_winmm + 1352]
Thunk_waveOutMessage ENDP
Thunk_waveOutOpen PROC
    jmp QWORD PTR [g_winmm + 1360]
Thunk_waveOutOpen ENDP
Thunk_waveOutPause PROC
    jmp QWORD PTR [g_winmm + 1368]
Thunk_waveOutPause ENDP
Thunk_waveOutPrepareHeader PROC
    jmp QWORD PTR [g_winmm + 1376]
Thunk_waveOutPrepareHeader ENDP
Thunk_waveOutReset PROC
    jmp QWORD PTR [g_winmm + 1384]
Thunk_waveOutReset ENDP
Thunk_waveOutRestart PROC
    jmp QWORD PTR [g_winmm + 1392]
Thunk_waveOutRestart ENDP
Thunk_waveOutSetPitch PROC
    jmp QWORD PTR [g_winmm + 1400]
Thunk_waveOutSetPitch ENDP
Thunk_waveOutSetPlaybackRate PROC
    jmp QWORD PTR [g_winmm + 1408]
Thunk_waveOutSetPlaybackRate ENDP
Thunk_waveOutSetVolume PROC
    jmp QWORD PTR [g_winmm + 1416]
Thunk_waveOutSetVolume ENDP
Thunk_waveOutUnprepareHeader PROC
    jmp QWORD PTR [g_winmm + 1424]
Thunk_waveOutUnprepareHeader ENDP
Thunk_waveOutWrite PROC
    jmp QWORD PTR [g_winmm + 1432]
Thunk_waveOutWrite ENDP
END
