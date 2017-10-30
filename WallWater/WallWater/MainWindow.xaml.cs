#define _BUFFERED_RENDERING
#define _JAGGED_ARRAYS

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WallWater
{

    internal struct DropData
    {
        public int x;
        public int y;
        public int radius;
        public int height;
    }
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static int _BITMAP_WIDTH = 0;
        private static int _BITMAP_HEIGHT = 0;
        private static int _BITS = 4; /* Dont change this, it 24 bit bitmaps are not supported*/
        private static DropData[] _drops;
        private FastBitmap _image = null;
        private FastBitmap _originalImage = null;
        public int _currentHeightBuffer = 0;
        public int _newHeightBuffer = 0;
        private byte[] _bitmapOriginalBytes;
        private Random _r = new Random();
        DispatcherTimer waterTime  = new DispatcherTimer();
        System.Drawing.Image bitmap;
        ImageBrush _imageBrush= new ImageBrush();
        byte[] _bufferBits;

#if _JAGGED_ARRAYS
        private static int[][][] _waveHeight;
#endif
#if _RECTANGULAR_ARRAYS
        private static int[,,] _waveHeight;
#endif
#if _LINEAR_ARRAYS
        private static int[] _waveHeight;
#endif


        public MainWindow()
        {
            InitializeComponent();
            bitmap = new System.Drawing.Bitmap("Test.jpg");
            _BITMAP_WIDTH = bitmap.Width;
            _BITMAP_HEIGHT = bitmap.Height;
            ImageGrid.Background = _imageBrush;
            _wb = new WriteableBitmap(_BITMAP_WIDTH, _BITMAP_HEIGHT, 96,96,PixelFormats.Bgr32,null);
#if _JAGGED_ARRAYS
            _waveHeight = new int[2][][];
            for (int i = 0; i < 2; i++)
            {
                _waveHeight[i] = new int[_BITMAP_WIDTH][];
                for (int j = 0; j < _BITMAP_WIDTH; j++)
                {
                    _waveHeight[i][j] = new int[_BITMAP_HEIGHT];
                }
            }
#endif
#if _RECTANGULAR_ARRAYS
            _waveHeight = new int[_BITMAP_WIDTH, _BITMAP_HEIGHT, 2];
#endif

#if _LINEAR_ARRAYS
            _waveHeight = new int[_BITMAP_WIDTH * _BITMAP_HEIGHT * 2];
#endif


            CreateBitmap();
            CreateWaterDrops();

            waterTime.Tick += waterTime_Tick;
            waterTime.Interval = TimeSpan.FromMilliseconds(10);
            this.waterTime.Start();
        }
        WriteableBitmap _wb;
        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, uint count);

        private void DrawToBrush()
        {

            if(null == _wb)
            {
                return;
            }
            this.Dispatcher.Invoke(new Action(() => {
                _wb.Lock();
                _image.LockBits();
                memcpy(_wb.BackBuffer, _image.Data().Scan0, (uint)(_wb.BackBufferStride * _wb.Height));
                _wb.AddDirtyRect(new Int32Rect(0, 0, (int)_wb.Width, (int)_wb.Height));
                _image.Release();
                _wb.Unlock();
                _imageBrush.ImageSource = _wb;
            }));
        }

        private void CreateBitmap()
        {
            _originalImage = new FastBitmap((Bitmap)(bitmap).Clone(), _BITS);
            _originalImage.LockBits();
            _image = new FastBitmap((Bitmap)(bitmap).Clone(), _BITS);
            _bitmapOriginalBytes = new byte[_BITS * _image.Width() * _image.Height()];
            _bufferBits = new byte[_BITS * _image.Width() * _image.Height()];
            _image.LockBits();
            Marshal.Copy(_image.Data().Scan0, _bitmapOriginalBytes, 0, _bitmapOriginalBytes.Length);
            _image.Release();
        }

        private void DropWater(int x, int y, int radius, int height)
        {
            long _distance;
            int _x;
            int _y;
            Single _ratio;

            _ratio = (Single)((Math.PI / (Single)radius));

            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    _x = x + i;
                    _y = y + j;
                    if ((_x >= 0) && (_x <= _BITMAP_WIDTH - 1) && (_y >= 0) && (_y <= _BITMAP_HEIGHT - 1))
                    {
                        _distance = (long)Math.Sqrt(i * i + j * j);
                        if (_distance <= radius)
                        {
#if _JAGGED_ARRAYS
                            _waveHeight[_currentHeightBuffer][_x][_y]= (int)(height * Math.Cos((Single)_distance * _ratio));
#endif
#if _RECTANGULAR_ARRAYS
                            _waveHeight[_x,_y,_currentHeightBuffer] = (int)(height * Math.Cos((Single)_distance * _ratio));
#endif
#if _LINEAR_ARRAYS
                            _waveHeight[INDEX3D(_x, _y, _currentHeightBuffer)] = (int)(height * Math.Cos((Single)_distance * _ratio));
#endif
                        }
                    }
                }
            }
        }

        private void PaintWater()
        {
            _newHeightBuffer = (_currentHeightBuffer + 1) % 2;
            var curWaveHeight = _waveHeight[_currentHeightBuffer];
            var newWaveHeight = _waveHeight[_newHeightBuffer];
            Array.Copy(_bitmapOriginalBytes, _bufferBits, _bufferBits.Length);
            //
            // 
            //
            int _offX = 0;
            int _offY = 0;

            for (int _x = 1; _x < _BITMAP_WIDTH - 1; _x++)
            {
                var cw1= curWaveHeight[_x-1];
                var cw2 = curWaveHeight[_x];
                var cw3 = curWaveHeight[_x+1];
                var nw1 = newWaveHeight[_x-1];
                var nw2 = newWaveHeight[_x];
                var nw3 = newWaveHeight[_x + 1];
                for (int _y = 1; _y < _BITMAP_HEIGHT - 1; _y++)
                {

                    //
                    //  Simulate movement.
                    //
                    unchecked
                    {
                        nw2[_y] = ((
                            cw1[_y] +
                            cw1[_y - 1] +
                            cw2[_y - 1] +
                            cw3[_y - 1] +
                            cw3[_y] +
                            cw3[_y + 1] +
                            cw2[_y + 1] +
                            cw1[_y + 1]) >> 2)
                        - nw2[_y];
                    }
                    //
                    //  Dampenning.
                    //
                    nw2[_y] -= (nw2[_y] >> 5);
                    //
                    //
                    //
                    _offX = ((nw1[_y] - nw3[_y])) >> 3;
                    _offY = ((nw2[_y - 1] - nw2[_y + 1])) >> 3;

                    //
                    //  Nothing to do
                    //
                    if ((_offX == 0) && (_offY == 0)) continue;
                    //
                    //  Fix boundaries
                    //
                    if (_x + _offX <= 0) _offX = -_x;
                    if (_x + _offX >= _BITMAP_WIDTH - 1) _offX = _BITMAP_WIDTH - _x - 1;
                    if (_y + _offY <= 0) _offY = -_y;
                    if (_y + _offY >= _BITMAP_HEIGHT - 1) _offY = _BITMAP_HEIGHT - _y - 1;
                    if ((_offX == 0) && (_offY == 0)) continue;

                    var offset = _BITS * (_x + _y * _BITMAP_WIDTH);
                    var offset2 = _BITS * (_x + _offX + (_y + _offY) * _BITMAP_WIDTH);
                    _bufferBits[offset + 0] = _bitmapOriginalBytes[offset2 + 0];
                    _bufferBits[offset + 1] = _bitmapOriginalBytes[offset2 + 1];
                    _bufferBits[offset + 2] = _bitmapOriginalBytes[offset2 + 2];
                }
            }
            _image.LockBits();
            Marshal.Copy(_bufferBits,0,_image.Data().Scan0, _bufferBits.Length);
            _currentHeightBuffer = _newHeightBuffer;
            DrawToBrush();
        }
        private void waterTime_Tick(object sender, EventArgs e)
        {
            if (_image.IsLocked()) return;
            waterTime.Stop();
            PaintWater();
            waterTime.Start();
        }
      
        private void CreateWaterDrops()
        {
            int _dropX;
            int _dropY;
            int _dropRadius;
            int _height;

            int _percent = (int)(0.0015 * (this.Width + this.Height));
            _drops = new DropData[100];

            for (int i = 0; i < _drops.Length; i++)
            {
                _dropX = _r.Next(_BITMAP_WIDTH);
                _dropY = _r.Next(_BITMAP_HEIGHT);
                _height = _r.Next(400);
                _dropRadius = _r.Next(4 * _percent);

                if (_dropRadius < 4) _dropRadius = 4;

                _drops[i].x = _dropX;
                _drops[i].y = _dropY;
                _drops[i].radius = _dropRadius;
                _drops[i].height = _height;
            }

        }

        private void ImageGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ImageGrid);
            DropWater((int)(pos.X *_BITMAP_WIDTH/ImageGrid.ActualWidth), (int)(pos.Y*_BITMAP_HEIGHT/ImageGrid.ActualHeight), 20, 50);
        }
    }
}
