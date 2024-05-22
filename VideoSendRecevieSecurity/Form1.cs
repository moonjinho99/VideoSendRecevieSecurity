using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using FFmpeg.AutoGen;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;


namespace VideoSendRecevieSecurity
{
    public partial class Form1 : Form
    {

        private const int KeySize = 16;
        private const int BlockSize = 16;

        private const int ChunkSize = 1024;

        private VideoCapture _capture;

        private Thread senderThread = null;
        private Thread receivedThread = null;
        private byte[] encodedData;

        private static SeedCrypto sc = new SeedCrypto();

        private byte[] key = Convert.FromBase64String("MTIzNDU2Nzg5MGFiY2RlZg==");
        private byte[] iv = Convert.FromBase64String("ZmVkY2JhOTg3NjU0MzIxMA==");

        public Form1()
        {
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
            InitializeComponent();
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            _capture = new VideoCapture(1);

            senderThread = new Thread(SendVideo);
            senderThread.IsBackground = true;
            senderThread.Start();
        }

        private void linkBtn_Click(object sender, EventArgs e)
        {
            receivedThread = new Thread(ReceiveVideo);
            receivedThread.IsBackground = true;
            receivedThread.Start();
        }

        private void SendVideo()
        {
            using(Socket senderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                senderSocket.Connect(IPAddress.Parse("192.168.56.1"), 9050);

                Mat frame = new Mat();

                while(true)
                {
                    _capture.Read(frame);

                    if(!frame.Empty())
                    {
                        pictureBox1.Image = Image.FromStream(frame.ToMemoryStream());

                        Bitmap image = frame.ToBitmap();
                        image = new Bitmap(image, new System.Drawing.Size(704, 576));

                        encodedData = EncodeToH263(image);

                        byte[] encryptedBytes = sc.Encrypt(encodedData,key,iv);

                        senderSocket.Send(BitConverter.GetBytes(encryptedBytes.Length));

                        for(int i=0; i< encryptedBytes.Length; i += ChunkSize)
                        {
                            int size = Math.Min(ChunkSize, encryptedBytes.Length - i);
                            senderSocket.Send(encryptedBytes, i, size, SocketFlags.None);
                        }
                    }

                    Thread.Sleep(25);
                }
            }    
        }

        private void ReceiveVideo()
        {
            using(Socket receiverSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp))
            {
                IPEndPoint senderEP = new IPEndPoint(IPAddress.Any, 9051);
                receiverSocket.Bind(senderEP);

                byte[] buffer;
                int totalDataSize;
                int receivedDataSize;
                MemoryStream memoryStream = new MemoryStream();

                while(true)
                {
                    buffer = new byte[1024];
                    receiverSocket.Receive(buffer);
                    totalDataSize = BitConverter.ToInt32(buffer, 0);

                    while(memoryStream.Length < totalDataSize)
                    {
                        buffer = new byte[ChunkSize];
                        receivedDataSize = receiverSocket.Receive(buffer);
                        memoryStream.Write(buffer, 0, receivedDataSize);
                    }

                    if(memoryStream.Length == totalDataSize)
                    {
                        byte[] imageData = memoryStream.ToArray();

                        byte[] decryptedBytes = sc.Decrypt(imageData, key, iv);

                        DecodeToH263(decryptedBytes);

                        memoryStream.Dispose();
                        memoryStream = new MemoryStream();
                    }
                }

                Thread.Sleep(25);
            }
        }

        private unsafe byte[] EncodeToH263(Bitmap encodeBitmap)
        {
            var fps = 25;
            var sourceSize = new System.Drawing.Size(encodeBitmap.Width, encodeBitmap.Height);
            var sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
            var destinationSize = sourceSize;
            var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;
            using(var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat,destinationSize,destinationPixelFormat))
            {
                using(var vse = new H263VideoStreamEncoder(fps,destinationSize))
                {
                    byte[] bitmapData;
                    bitmapData = GetBitmapData(encodeBitmap);

                    fixed(byte* pBitmapData = bitmapData)
                    {
                        var data = new byte_ptrArray8 { [0] = pBitmapData };
                        var linesize = new int_array8 { [0] = bitmapData.Length / sourceSize.Height };
                        var avframe = new AVFrame
                        {
                            data = data,
                            linesize = linesize,
                            height = sourceSize.Height
                        };

                        var convertedFrame = vfc.Convert(avframe);

                        byte[] byteData = vse.Encode(convertedFrame);

                        return byteData;
                    }
                }
            }
        }

        private unsafe void DecodeToH263(byte[] encodedData)
        {
            using (H263VideoStreamDecoder decoder = new H263VideoStreamDecoder(25, new System.Drawing.Size(704, 576)))
            {
                decoder.DecodeFrame(encodedData, decoder.FrameSize, out MemoryStream stream);

                pictureBox2.Image = Image.FromStream(stream);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _capture.Release();
            senderThread.Abort();
            receivedThread.Abort();
        }

        private byte[] GetBitmapData(Bitmap frameBitmap)
        {
            var bitmapData = frameBitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, frameBitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var length = bitmapData.Stride * bitmapData.Height;
                var data = new byte[length];
                Marshal.Copy(bitmapData.Scan0, data, 0, length);
                return data;
            }
            finally
            {
                frameBitmap.UnlockBits(bitmapData);
            }
        }
    }
}
