echo %date%========== del backup files start============ >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.dmp /d -28 /c "cmd /c echo @path"  >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.dmp /d -28 /c "cmd /c del /f /Q @path" 
forfiles /p "D:\FTP_DB\Backup" /s /m *.log /d -28 /c "cmd /c echo @path"  >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.log /d -28 /c "cmd /c del /f /Q @path" 
forfiles /p "D:\FTP_DB\Backup" /s /m *.gz /d -28 /c "cmd /c echo @path"  >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.gz /d -28 /c "cmd /c del /f /Q @path"
forfiles /p "D:\FTP_DB\Backup" /s /m *.bak /d -28 /c "cmd /c echo @path"  >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.bak /d -28 /c "cmd /c del /f /Q @path"  
forfiles /p "D:\FTP_DB\Backup" /s /m *.sql /d -28 /c "cmd /c echo @path"  >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.sql /d -28 /c "cmd /c del /f /Q @path" 
forfiles /p "D:\FTP_DB\Backup" /s /m *.7z /d -28 /c "cmd /c echo @path"  >> C:\run\del_backup_files.log
forfiles /p "D:\FTP_DB\Backup" /s /m *.7z /d -28 /c "cmd /c del /f /Q @path" 
echo %date%########### del backup files end ############# >> C:\run\del_backup_files.log