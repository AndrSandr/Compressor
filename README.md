The program for compression and decompression of files of any size. Files are compressed by parallel processes (1 block=1Mb). The number of parallel processes is equal to the number of processors in the system.

Command parameters:
-c [Full path to file] // file compression. After compressed, a .zip extension is added to the file name.
-d [Full path to file] // decompress the file.

Example:
BelkaCompressor.exe -c D: \ test.pdf // the resulting file D: \ test.pdf.zip
BelkaCompressor.exe -d D: \ test.pdf.zip // the resulting file D: \ test.pdf

Error messages writing to BelkaCompressor.exe.log
