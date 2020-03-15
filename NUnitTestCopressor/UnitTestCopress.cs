using NUnit.Framework;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System;

namespace NUnitTestCopressor
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        byte[][] readArray ;
        byte[][] writeArray;


        public  void CompressArray(object i)
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

        [Test]
        public void TestCopress()
        {
            string fileName = "test.zip";
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
                int numProcs = 1;
                int bufferSize = 1024;

                byte[][] readArray = new byte[numProcs][];
                byte[][] writeArray = new byte[numProcs][];


                while (srcFile.Position < srcFile.Length)
                {
                
                    threadArray = new Thread[numProcs];
                    for (int partCount = 0; (partCount < numProcs) && (srcFile.Position < srcFile.Length); partCount++)
                    {
                        int _bufferSize = bufferSize;
                        if (srcFile.Length - srcFile.Position < bufferSize)
                        {
                            _bufferSize = (int)(srcFile.Length - srcFile.Position);
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
                        threadArray[waitThread].Join();
                        if (threadArray[waitThread].ThreadState == ThreadState.Stopped)
                        {
                            BitConverter.GetBytes(writeArray[waitThread].Length).CopyTo(writeArray[waitThread], 4);
                            dstFile.Write(writeArray[waitThread], 0, writeArray[waitThread].Length);
                            waitThread++;
                        }
                    }
                }
                srcFile.Close();
                dstFile.Close();
                Assert.Pass();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:" + ex.Message);
                Assert.Fail();
            }
        }
    }
}