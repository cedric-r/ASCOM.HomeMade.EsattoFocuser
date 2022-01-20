# ASCOM.HomeMade.EsattoFocuser
Esatto 3"controller through ASCOM

I encountered issues with my Esatto focuser whic was becoming unresponsive after a number of hours in operation. As I was using the latest version of the drivers at the time (v1.3) and I still could connect with the Esatto application, I assumed that the issue was with the ASCOM drivers, not with the Esatto itself. So I decided to write my own driver to test.

After testing, my driver doesn't become unresponsive after 7 or 8 hours. So I'm guessing there was some sort of resource exhaustion in the official ASCOM driver.

Use at your own risks, this is just for me.
