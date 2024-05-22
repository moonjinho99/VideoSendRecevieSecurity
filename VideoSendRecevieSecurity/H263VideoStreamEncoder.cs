using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;
using FFmpeg.AutoGen;
using System.Drawing;
using System.Drawing.Imaging;

namespace VideoSendRecevieSecurity
{
    public sealed unsafe class H263VideoStreamEncoder : IDisposable
    {

        private readonly System.Drawing.Size _frameSize;
        private readonly int _linesizeU;
        private readonly int _linesizeV;
        private readonly int _linesizeY;
        private readonly AVCodec* _pCodec;
        private readonly AVCodecContext* _pCodecContext;
        private readonly int _uSize;
        private readonly int _ySize;

        static H263VideoStreamEncoder()
        {
            ffmpeg.avcodec_register_all();
        }

        public H263VideoStreamEncoder(int fps, System.Drawing.Size frameSize)
        {
            _frameSize = frameSize;

            var codecId = AVCodecID.AV_CODEC_ID_H263;
            _pCodec = ffmpeg.avcodec_find_encoder(codecId);
            if (_pCodec == null) throw new InvalidOperationException("Codec not found");

            _pCodecContext = ffmpeg.avcodec_alloc_context3(_pCodec);
            _pCodecContext->width = frameSize.Width;
            _pCodecContext->height = frameSize.Height;

            _pCodecContext->time_base = new AVRational { num = 1, den = fps };
            _pCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            ffmpeg.avcodec_open2(_pCodecContext, _pCodec, null);

            _linesizeY = frameSize.Width;
            _linesizeU = frameSize.Width / 2;
            _linesizeV = frameSize.Width / 2;

            _ySize = _linesizeY * frameSize.Height;
            _uSize = _linesizeU * frameSize.Height / 2;
        }

        public void Dispose()
        {
            ffmpeg.avcodec_close(_pCodecContext);
            ffmpeg.av_free(_pCodecContext);
            ffmpeg.av_free(_pCodec);
        }

        public byte[] Encode(AVFrame frame)
        {
            if (frame.format != (int)_pCodecContext->pix_fmt) throw new ArgumentException("Invalid pixel format.", nameof(frame));
            if (frame.width != _frameSize.Width) throw new ArgumentException("Invalid width.", nameof(frame));
            if (frame.height != _frameSize.Height) throw new ArgumentException("Invalid height.", nameof(frame));
            if (frame.linesize[0] != _linesizeY) throw new ArgumentException("Invalid Y linesize.", nameof(frame));
            if (frame.linesize[1] != _linesizeU) throw new ArgumentException("Invalid U linesize.", nameof(frame));
            if (frame.linesize[2] != _linesizeV) throw new ArgumentException("Invalid V linesize.", nameof(frame));
            if (frame.data[1] - frame.data[0] != _ySize) throw new ArgumentException("Invalid Y data size.", nameof(frame));
            if (frame.data[2] - frame.data[1] != _uSize) throw new ArgumentException("Invalid U data size.", nameof(frame));

            var pPacket = ffmpeg.av_packet_alloc();

            try
            {
                int error;
                do
                {
                    ffmpeg.avcodec_send_frame(_pCodecContext, &frame);

                    error = ffmpeg.avcodec_receive_packet(_pCodecContext, pPacket);
                } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

                byte[] encodedData = new byte[pPacket->size];
                Marshal.Copy((IntPtr)pPacket->data, encodedData, 0, pPacket->size);

                return encodedData;
            }
            finally
            {
                ffmpeg.av_packet_unref(pPacket);
            }
        }

        private static byte[] GetBitmapData(Bitmap frameBitmap)
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
