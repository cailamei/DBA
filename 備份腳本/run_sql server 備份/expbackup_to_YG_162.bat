set LC_ALL=en_US.UTF-8

set year=%DATE:~6,4%
set month=%DATE:~0,2%
set day=%DATE:~3,2%

XCOPY /y   D:\sql_backup\eHR2\eHR2_backup_%year%_%month%_%day%*.bak  D:\run\ftp\
XCOPY /y   D:\sql_backup\eHR3\eHR3_backup_%year%_%month%_%day%*.bak  D:\run\ftp\
XCOPY /y   D:\sql_backup\FXBao\FXBao_backup_%year%_%month%_%day%*.bak  D:\run\ftp\
XCOPY /y   D:\sql_backup\TUNADB2A\TUNADB2A_backup_%year%_%month%_%day%*.bak  D:\run\ftp\
XCOPY /y   D:\sql_backup\PerfAnalysis\PerfAnalysis_backup_%year%_%month%_%day%_*.bak  D:\run\ftp\
XCOPY /y   D:\sql_backup\eHR4\eHR4_backup_%year%_%month%_%day%_*.bak  D:\run\ftp\

C:\WinSCP\WinSCP.exe  /script=D:\run\winscp.txt /log=D:\run\win.log

del /q D:\run\ftp\*.bak