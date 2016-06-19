using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using System.Numerics;

namespace VideoEffects
{
    public sealed class PreviewVideoEffect : IBasicVideoEffect
    {
        private CanvasDevice canvasDevice;
        private Matrix5x4 desaturate = new Matrix5x4
        {
            M11 = 1,
            M12 = 0,
            M13 = 0,
            M14 = 0,
            M21 = 0,
            M22 = 0,
            M23 = 0,
            M24 = 0,
            M31 = 0,
            M32 = 0,
            M33 = 1,
            M34 = 0,
            M41 = 0,
            M42 = 0,
            M43 = 0,
            M44 = 1,
            M51 = 0,
            M52 = 0,
            M53 = -0.5f,
            M54 = 0
        };
        Vector2 brightnessWhitePoint = new Vector2(0.5f, 1);

        public bool IsReadOnly { get { return false; } }

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties { get { return new List<VideoEncodingProperties>(); } }

        public MediaMemoryTypes SupportedMemoryTypes { get { return MediaMemoryTypes.Gpu; } }

        public bool TimeIndependent { get { return true; } }

        public void Close(MediaEffectClosedReason reason)
        {
        }

        public void DiscardQueuedFrames()
        {
        }

        public void SetProperties(IPropertySet configuration)
        {
        }

        public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
        }

        public void ProcessFrame(ProcessVideoFrameContext context)
        {          
            var inputSurface = context.InputFrame.Direct3DSurface;
            var outputSurface = context.OutputFrame.Direct3DSurface;

            using (CanvasBitmap inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, inputSurface))
            using (CanvasRenderTarget renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, outputSurface))
            using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
            using (PosterizeEffect background = new PosterizeEffect { Source = inputBitmap, BlueValueCount = 2, GreenValueCount = 2, RedValueCount = 2 })
            using (var rgbToHueEffect = new RgbToHueEffect { Source = background, OutputColorSpace = EffectHueColorSpace.Hsl })
            using (var colorMatrixEffect = new ColorMatrixEffect { Source = rgbToHueEffect, ColorMatrix = desaturate })
            using (var hueToRgbEffect = new HueToRgbEffect { Source = colorMatrixEffect, SourceColorSpace = EffectHueColorSpace.Hsl })
            using (var brightnessEffect = new BrightnessEffect { Source = hueToRgbEffect, WhitePoint = brightnessWhitePoint })
            using (InvertEffect invertEffect = new InvertEffect { Source = brightnessEffect }) 
            using (var composite = new CompositeEffect { Sources = { invertEffect } })
            {
                ds.DrawImage(composite);
            }
        }
    }
}
