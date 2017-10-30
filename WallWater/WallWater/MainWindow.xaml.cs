using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private FastBitmap _originalImage = null;
        public int _currentHeightBuffer = 0;
        public int _newHeightBuffer = 0;
        private byte[] _bitmapOriginalBytes;
        private Random _r = new Random();
        DispatcherTimer waterTime  = new DispatcherTimer();
        System.Drawing.Image bitmap;
        ImageBrush _imageBrush= new ImageBrush();
        byte[] _bufferBits;

        private static int[][][] _waveHeight;


        public MainWindow()
        {
            InitializeComponent();
            bitmap = new System.Drawing.Bitmap("Test.jpg");
            _BITMAP_WIDTH = bitmap.Width;
            _BITMAP_HEIGHT = bitmap.Height;
            ImageGrid.Background = _imageBrush;
            _wb = new WriteableBitmap(_BITMAP_WIDTH, _BITMAP_HEIGHT, 96,96,PixelFormats.Bgr32,null);
            _waveHeight = new int[2][][];
            for (int i = 0; i < 2; i++)
            {
                _waveHeight[i] = new int[_BITMAP_WIDTH][];
                for (int j = 0; j < _BITMAP_WIDTH; j++)
                {
                    _waveHeight[i][j] = new int[_BITMAP_HEIGHT];
                }
            }


            CreateBitmap();

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

                Marshal.Copy(_bufferBits, 0, _wb.BackBuffer, _bufferBits.Length);

                _wb.AddDirtyRect(new Int32Rect(0, 0, (int)_wb.Width, (int)_wb.Height));
                _wb.Unlock();
                _imageBrush.ImageSource = _wb;
            }));
        }

        private void CreateBitmap()
        {
            _originalImage = new FastBitmap((Bitmap)(bitmap).Clone(), _BITS);
            _originalImage.LockBits();
            _bitmapOriginalBytes = new byte[_BITS * _BITMAP_WIDTH * _BITMAP_HEIGHT];
            _bufferBits = new byte[_BITS * _BITMAP_WIDTH * _BITMAP_HEIGHT];
            Marshal.Copy(_originalImage.Data().Scan0, _bitmapOriginalBytes, 0, _bitmapOriginalBytes.Length);
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
                            _waveHeight[_currentHeightBuffer][_x][_y]= (int)(height * Math.Cos((Single)_distance * _ratio));
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

            _currentHeightBuffer = _newHeightBuffer;
            DrawToBrush();
        }
        private void waterTime_Tick(object sender, EventArgs e)
        {
            waterTime.Stop();
            PaintWater();
            waterTime.Start();
        }
      
      
        private void ImageGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ImageGrid);
            DropWater((int)(pos.X *_BITMAP_WIDTH/ImageGrid.ActualWidth), (int)(pos.Y*_BITMAP_HEIGHT/ImageGrid.ActualHeight), 20, 50);
        }
    }
}
