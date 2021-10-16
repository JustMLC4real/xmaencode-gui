# xmaencode-gui
A quickly hacked together GUI to make using the version of xmaencode we use for '06 easier, based on code written for the [Sonic '06 Randomiser Suite](https://github.com/Knuxfan24/Sonic-06-Randomiser-Suite).

# Usage
Select the song files you wish to convert (any that vgmstream should support) and specify an output directory then click Convert. If the loop checkbox is checked, then a start to end loop will be added to the song; unless vgmstream picked up any preexisting loop points, in which case it will instead add them to properly loop the track.

Looping may not work properly depending on the sample rate of the source song. Ones that are 32000hz have been a consistent problem.