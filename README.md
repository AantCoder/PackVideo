# PackVideo

Программа для пересжатия mp4
Версия 1.1 2021
Автор Aant

Программа пересжимает все mp4 файлы в avi с тем же именем, если таких файлов уже не существует.
Рядом с программой должен быть ffmpeg.exe.

Параметры для сжатия: -vcodec h264 -crf 22 -acodec aac

Параметры для сжатия с уменьшением размера: -filter:v scale="iw/2:ih/2" -vcodec h264 -crf 22 -acodec aac

# License

Copyright 2018-2019 Ivanov Vasilii Sergeevich aka Aant
Licensed under the LGPLv2.1

Components used:
* FFmpeg (LGPLv2.1 license) https://ffmpeg.org/