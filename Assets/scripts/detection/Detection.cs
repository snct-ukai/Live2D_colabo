using System;
using System.IO;
using System.Numerics;
using DlibDotNet;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCvSharp;
using UnityEngine;
using UnityEngine.Events;

namespace detection
{
    public sealed class Detection : MonoBehaviour
    {
        [Serializable] private class DetectParam: UnityEvent<Param> { }
        [SerializeField] private DetectParam onDetect = new();
        [Serializable] private class PreviewMat: UnityEvent<Mat> { }
        [SerializeField] private PreviewMat onPreview = new();

        private bool _isRunning;
        
        private static FrontalFaceDetector _faceDetector;
        private static ShapePredictor _shapePredictor;

        private static int _cantFindFrames;
        private static Rectangle _face = Rectangle.Empty;
        
        private void Start()
        {
            _faceDetector = Dlib.GetFrontalFaceDetector();
            using var fs = File.OpenRead(Application.dataPath + "/StreamingAssets/shape_predictor_68_face_landmarks.dat");
            var bytes = new byte[fs.Length];
            // ReSharper disable once MustUseReturnValue
            fs.Read(bytes, 0, bytes.Length);
            _shapePredictor = ShapePredictor.Deserialize(bytes);
        }
        private void Update()
        {
            if (!_isRunning) return;
            Detect();
        }
        
        public void StartDetection() => _isRunning = true;
        
        public void StopDetection() => _isRunning = false;

        private void Detect()
        {
            if(!_isRunning) return;
            CameraManager.Instance.GetFrame(out var mat);
            
            var width = mat.Width;
            var height = mat.Height;
            var elemSize = mat.ElemSize();
            
            var array = new byte[width * height * elemSize];
            Marshal.Copy(mat.Data, array, 0, array.Length);
            var image = Dlib.LoadImageData<RgbPixel>(
                array,
                (uint)height, 
                (uint)width,
                (uint)(width * elemSize));
            
            var faces = _faceDetector.Operator(image);
            
            if (faces.Length == 0)
            {
                if (_cantFindFrames >= 10 || _face.IsEmpty) return;
                _cantFindFrames++;
            }
            else
            {
                _face = faces[0];
                _cantFindFrames = 0;
            }

            var shapes = _shapePredictor.Detect(image, _face);
            
            var points = new Complex[68];
            mat.Rectangle(new OpenCvSharp.Point(0, 0), new OpenCvSharp.Point(width, height), Scalar.White, -1);
            Parallel.For(0, 68, i =>
            {
                var point = shapes.GetPart((uint)i);
                points[i] = new Complex(point.X, point.Y);
                mat.DrawMarker(new OpenCvSharp.Point(point.X, point.Y), Scalar.Red, MarkerTypes.Cross, 5, 2);
                mat.PutText(i.ToString(), new OpenCvSharp.Point(point.X, point.Y), HersheyFonts.HersheySimplex, 0.1, Scalar.Red);
            });
            var param = CoordinateParser.Parse(points, image, mat);
            onDetect.Invoke(param);
            mat.PutText($"pitch: {param.ParamAngleX} yaw:{param.ParamAngleY} roll:{param.ParamAngleZ}", new OpenCvSharp.Point(40, 40), HersheyFonts.HersheySimplex, 0.3, Scalar.Red);
            Cv2.Flip(mat, mat, FlipMode.X);
            onPreview.Invoke(mat);
        }
    }
}
