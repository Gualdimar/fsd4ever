# fsd4ever
FreestyleDash 3.775 web scripts to renew TU&amp;cover dowload features and tool for creating patched default.xex

Latest download: https://github.com/Gualdimar/fsd4ever/releases
-----
How to use:
-----
- put php scripts on your hosting
- launch fsd4ever.exe and paste full links to tu.php(or tu_f.php, see below) and cover.php with "http://" in the beginning
  - example: http://example.org/fsd/tu.php or http://10.10.10.10/fsd/tu.php
- press "Generate" button
- copy default.xex created in the application directory to your xbox

tu.php or tu_f.php:
-----
  - tu.php - shows TU for all MediaIDs  
  - tu_f.ph - shows TU only for your MediaID  

fsd4ever.Server by Swizzy
-----
fsd4ever.Server is a HTTP Server you can use on your local computer instead of a fully fledged server...

NOTE: The server only seem to work on port 80 (FSD limitation) however, it can be configured to use another port...

NOTE: Port 80 is generally in use by your webbrowsers, so, close those first and you may need to kill an additional process in windows like so:
netstat -a -o -n | find ":80"
- Look for anything that says "TCP :80 .....", the last number is what you're looking for (it's the ProcessID)
taskkill /F /PID 
this has to be done in a Admin command prompt
