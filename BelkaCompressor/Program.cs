using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Reflection;


namespace BelkaCompressor
{
    class Program
    {
        const int bufferSize = 1024 * 1024; //размер буфера для сжатия
        static int numProcs = Environment.ProcessorCount;
        static byte[][] readArray = new byte[numProcs][];//массив для чтения блоков файла по количеству процессоров в системе.
        static byte[][] writeArray = new byte[numProcs][];//массив для записи блоков в файл
        static bool isCancel = false;

        static void CancelKeyPress(object sender, ConsoleCancelEventArgs _args)
        {
            if (_args.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                Console.WriteLine("\nCancel.");
                _args.Cancel = true;
                isCancel = true;
                writeToLog("Process canceled by user.");
            }
        }

        static void writeToLog(string toLog)//запись в лог об ошибке
        {
            string logFileName = Assembly.GetEntryAssembly().Location + ".log";
            StreamWriter logFile = new StreamWriter(logFileName, true);
            logFile.WriteLine(DateTime.Now + " " + toLog);
            logFile.Flush();
            logFile.Close();
        }
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPress);//процесс можно будет прервать (Ctrl+C)
            try
            {
                if (args.Length == 0 || args.Length > 2)
                {
                    throw new Exception("Please enter arguments up to the following pattern:\n -c or (-d) [Source file]");
                }
                if (args[0].ToLower() != "-c" && args[0].ToLower() != "-d")
                {
                    throw new Exception("First argument shall be \"-c\" or \"-d\".");
                }
                if (args[1].Length == 0)
                {
                    throw new Exception("No source file name was specified.");
                }
                if (!File.Exists(args[1]))
                {
                    throw new Exception("No source file was found.");
                }
                if (args[0].ToLower() == "-c")
                    CompressFile(args[1]);
                else
                    DecompressFile(args[1]);
                Console.WriteLine("\nSuccess. Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                string errorMsg = "Error is occured!\n Method: "+ ex.TargetSite+"\n Error description "+ ex.Message;
                Console.WriteLine(errorMsg);
                writeToLog(errorMsg);
                Console.WriteLine("Press any key...");
                Console.ReadKey();
                return;
            }
        }
        public static void CompressFile(string fileName)
        {

            string dstFileName = fileName + ".zip";
            try
            {
                if (File.Exists(dstFileName))
                {
                    File.Delete(dstFileName);
                }
                FileStream srcFile = new FileStream(fileName, FileMode.Open);
                FileStream dstFile = new FileStream(fileName + ".zip", FileMode.Append);

                Thread[] threadArray;

                Console.Write("Start compressing: " + fileName + "...");

                while (srcFile.Position < srcFile.Length)
                {
                    if (isCancel)
                        break;
                    Console.Write("#");
                    threadArray = new Thread[numProcs];
                    for (int partCount = 0; (partCount < numProcs) && (srcFile.Position < srcFile.Length); partCount++)
                    {
                        int _bufferSize = bufferSize;
                        if (srcFile.Length - srcFile.Position < bufferSize)
                        {
                            _bufferSize = (int)(srcFile.Length - srcFile.Position);//для последного блока задаем меньший размер буффера
                        }
                        readArray[partCount] = new byte[_bufferSize];
                        srcFile.Read(readArray[partCount], 0, _bufferSize);

                        threadArray[partCount] = new Thread(CompressArray);
                        threadArray[partCount].Start(partCount);
                    }

                    int waitThread = 0;
                    while (waitThread < numProcs)
                    {
                        if (threadArray[waitThread] == null)
                            break;
                        threadArray[waitThread].Join();//ждем завершения очередного процесса
                        if (threadArray[waitThread].ThreadState == ThreadState.Stopped)
                        {
                            //для будущей распаковки надо записать полученный упакованный размер в начало блока
                            BitConverter.GetBytes(writeArray[waitThread].Length).CopyTo(writeArray[waitThread], 4);
                            dstFile.Write(writeArray[waitThread], 0, writeArray[waitThread].Length);
                            waitThread++;
                        }
                    }
                }
                srcFile.Close();
                dstFile.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:" + ex.Message);
                writeToLog("ERROR:" + ex.Message);
            }
        }

        public static void CompressArray(object i)
        {
            using (MemoryStream memStr = new MemoryStream(readArray[(int)i].Length))
            {
                using (GZipStream gzip = new GZipStream(memStr, CompressionMode.Compress))
                {
                    gzip.Write(readArray[(int)i], 0, readArray[(int)i].Length);
                }
                writeArray[(int)i] = memStr.ToArray();
            }
        }

        public static void DecompressFile(string inFileName)
        {
            try
            {
                FileStream srcFile = new FileStream(inFileName, FileMode.Open);
                FileStream dstFile = new FileStream(inFileName.Remove(inFileName.Length - 3), FileMode.Create);
                int decompressBlockLength;
                int compressedBlockLength;
                Thread[] threadArray;
                Console.Write("Start decompressing...");
                byte[] bufLen = new byte[8];


                while (srcFile.Position < srcFile.Length)
                {
                    if (isCancel)
                        break;
                    Console.Write("#");
                    threadArray = new Thread[numProcs];
                    for (int partCount = 0;(partCount < numProcs) && (srcFile.Position < srcFile.Length);partCount++)
                    {
                        srcFile.Read(bufLen, 0, 8);
                        compressedBlockLength = BitConverter.ToInt32(bufLen, 4);//читаем длинну запакованного блока
                        readArray[partCount] = new byte[compressedBlockLength];
                        bufLen.CopyTo(readArray[partCount], 0);

                        srcFile.Read(readArray[partCount], 8, compressedBlockLength - 8);
                        decompressBlockLength = BitConverter.ToInt32(readArray[partCount], compressedBlockLength - 4);//длинна распакованного блока
                        writeArray[partCount] = new byte[decompressBlockLength];

                        threadArray[partCount] = new Thread(DecompressArray);
                        threadArray[partCount].Start(partCount);
                    }
                    int waitThread = 0;
                    while (waitThread < numProcs)
                    {
                        if (threadArray[waitThread] == null)
                            break;
                        threadArray[waitThread].Join();//ждем завершения очередного процесса
                        if (threadArray[waitThread].ThreadState == ThreadState.Stopped)
                        {
                            dstFile.Write(writeArray[waitThread], 0, writeArray[waitThread].Length);
                            waitThread++;
                        }
                    }
                }
                srcFile.Close();
                dstFile.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:" + ex.Message);
                writeToLog("ERROR:" + ex.Message);
            }
        }

        public static void DecompressArray(object i)
        {
            using (MemoryStream memStr = new MemoryStream(readArray[(int)i]))
            {
                using (GZipStream ds = new GZipStream(memStr, CompressionMode.Decompress))
                {
                    ds.Read(writeArray[(int)i], 0, writeArray[(int)i].Length);
                }
            }
        }
    }
}
