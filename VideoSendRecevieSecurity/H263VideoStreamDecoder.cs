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
    public unsafe class H263VideoStreamDecoder : IDisposable
    {
        private readonly AVCodec* _pCodec;
        private readonly AVCodecContext* _pCodecContext;
        private readonly AVFrame* _pFrame;
        private readonly AVPacket* _pPacket;

        public H263VideoStreamDecoder()
        {
            ffmpeg.avcodec_register_all();
        }

        public H263VideoStreamDecoder(int fps, System.Drawing.Size frameSize)
        {
            var codecId = AVCodecID.AV_CODEC_ID_H263;
            _pCodec = ffmpeg.avcodec_find_decoder(codecId);

            _pCodecContext = ffmpeg.avcodec_alloc_context3(_pCodec);
            _pCodecContext->width = frameSize.Width;
            _pCodecContext->height = frameSize.Height;
            _pCodecContext->time_base = new AVRational { num = 1, den = fps };
            _pCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_BGR24;

            ffmpeg.avcodec_open2(_pCodecContext, _pCodec, null);

            CodecName = ffmpeg.avcodec_get_name(codecId);
            FrameSize = new System.Drawing.Size(_pCodecContext->width, _pCodecContext->height);
            PixelFormat = _pCodecContext->pix_fmt;

            _pPacket = ffmpeg.av_packet_alloc();
            _pFrame = ffmpeg.av_frame_alloc();
        }

        public string CodecName { get; }
        public System.Drawing.Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }

        public unsafe bool DecodeFrame(byte[] encodedData, System.Drawing.Size frameSize, out MemoryStream stream)
        {
            stream = new MemoryStream();
            var sourceSize = frameSize;
            var sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;
            var destinationSize = sourceSize;
            var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;

            using(var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize,destinationPixelFormat))
            {
                int error;

                do
                {
                    try
                    {
                        fixed (byte* pData = encodedData)
                        {
                            AVPacket* pPacket = _pPacket;
                            ffmpeg.av_init_packet(pPacket);
                            pPacket->data = pData;
                            pPacket->size = encodedData.Length;

                            ffmpeg.avcodec_send_packet(_pCodecContext, pPacket);
                        }
                    }
                    finally
                    {
                        ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket);
                    }
                    error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
                } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

                var convertedFrame = vfc.Convert(*_pFrame);
                using (var bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]))
                    bitmap.Save(stream, ImageFormat.Jpeg);

                return true;
            }

        }

        public void Dispose()
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_free(_pFrame);

            ffmpeg.av_packet_unref(_pPacket);
            ffmpeg.av_free(_pPacket);

            ffmpeg.avcodec_close(_pCodecContext);
        }
    }
}
